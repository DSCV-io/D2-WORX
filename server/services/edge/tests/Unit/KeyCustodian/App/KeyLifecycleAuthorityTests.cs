// -----------------------------------------------------------------------
// <copyright file="KeyLifecycleAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using Microsoft.Extensions.Logging;

/// <summary>
/// The System-plane-only lifecycle authority, proven at BOTH seams: the pure
/// <see cref="KeyLifecycleAuthority"/> rule matrix (all five origins; the
/// fail-closed <c>Unestablished</c> type-zero deny checked FIRST), and the gate
/// through EVERY real lifecycle command handler — per handler × per non-System
/// origin, the deny leaves the store untouched (no key row, no state transition,
/// no audit row, no announce) and fires the <c>capability = lifecycle</c>
/// authority-rejection counter + the <c>AuthorityRejected</c> forensic log.
/// </summary>
public sealed class KeyLifecycleAuthorityTests
{
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // Pure rule — the full origin matrix
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeLifecycleMutation_System_Allowed()
    {
        KeyLifecycleAuthority.AuthorizeLifecycleMutation(RequestOrigin.System)
            .Success.Should().BeTrue(
                "the in-host System worker plane is the only legitimate lifecycle driver");
    }

    [Fact]
    public void AuthorizeLifecycleMutation_Unestablished_Denied_RequestOriginUnestablished()
    {
        var result = KeyLifecycleAuthority.AuthorizeLifecycleMutation(
            RequestOrigin.Unestablished);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED,
            "the type-zero fail-closed deny is checked FIRST — a context no boundary "
            + "established surfaces the specific origin-unestablished code");
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public void AuthorizeLifecycleMutation_NonSystemEstablishedOrigin_DeniedForbidden(
        RequestOrigin origin)
    {
        var result = KeyLifecycleAuthority.AuthorizeLifecycleMutation(origin);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "no cross-process or user-plane lifecycle authority exists — a future admin "
            + "transport must consciously extend the rule");
    }

    // -----------------------------------------------------------------------
    // Gate proven per failing path THROUGH each real handler
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.Unestablished)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task GenerateKey_NonSystemOrigin_Denied_NoRowNoAudit_TelemetryFired(
        RequestOrigin origin)
    {
        // A canonical (domain, type) pair that WOULD generate under System — the deny
        // is attributable only to the authority gate.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GenerateKeyHandler>();
        var handler = new GenerateKeyHandler(
            KcAppTestKit.ContextWithOrigin(origin, logger),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));

        var tags = NewTagCapture();
        D2Result<KeySummary?> result;

        using (var listener = BuildListener(tags))
        {
            listener.Start();
            result = await handler.HandleAsync(
                new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));
        }

        AssertDenied(result, origin);
        db.Keys.Should().BeEmpty(because: "a denied generate writes no key row");
        db.Audit.Should().BeEmpty(because: "a denied generate writes no audit row");
        AssertDenyTelemetry(tags, logger.Entries, origin, "generate-key");
    }

    [Theory]
    [InlineData(RequestOrigin.Unestablished)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task ActivateKey_NonSystemOrigin_Denied_KeyStaysPending_TelemetryFired(
        RequestOrigin origin)
    {
        // A soaked pending key that WOULD activate under System.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, KeyDomain.COOKIE, KeyType.Secret, KeyStatus.Pending, created);
        var logger = new CapturingLogger<ActivateKeyHandler>();
        var handler = new ActivateKeyHandler(
            KcAppTestKit.ContextWithOrigin(origin, logger),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            new TestClock(created + Duration.FromHours(2)));

        var tags = NewTagCapture();
        D2Result<KeySummary?> result;

        using (var listener = BuildListener(tags))
        {
            listener.Start();
            result = await handler.HandleAsync(new ActivateKeyInput(kid));
        }

        AssertDenied(result, origin);
        db.Keys.Single().Status.Should().Be(
            KeyStatus.Pending, because: "a denied activation performs no state transition");
        db.Audit.Should().BeEmpty();
        AssertDenyTelemetry(tags, logger.Entries, origin, "activate-key");
    }

    [Theory]
    [InlineData(RequestOrigin.Unestablished)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task RotateKey_NonSystemOrigin_Denied_StatusesUnchanged_TelemetryFired(
        RequestOrigin origin)
    {
        // An active incumbent + soaked pending successor that WOULD rotate under System.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.COOKIE,
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);
        var pendingKid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, KeyDomain.COOKIE, KeyType.Secret, KeyStatus.Pending, created);
        var announcer = new RecordingAnnouncer();
        var logger = new CapturingLogger<RotateKeyHandler>();
        var handler = new RotateKeyHandler(
            KcAppTestKit.ContextWithOrigin(origin, logger),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            announcer,
            r_crypto,
            new TestClock(created + Duration.FromDays(30)));

        var tags = NewTagCapture();
        D2Result<RotateKeyOutput?> result;

        using (var listener = BuildListener(tags))
        {
            listener.Start();
            result = await handler.HandleAsync(new RotateKeyInput(KeyDomain.COOKIE));
        }

        AssertDenied(result, origin);
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(KeyStatus.Active);
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(KeyStatus.Pending);
        db.Audit.Should().BeEmpty();
        announcer.Calls.Should().BeEmpty(because: "a denied rotation announces nothing");
        AssertDenyTelemetry(tags, logger.Entries, origin, "rotate-key");
    }

    [Theory]
    [InlineData(RequestOrigin.Unestablished)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task RetireKey_NonSystemOrigin_Denied_KeyStaysRetiring_TelemetryFired(
        RequestOrigin origin)
    {
        // A grace-elapsed retiring key that WOULD retire under System.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.COOKIE,
            KeyType.Secret,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: created);
        var logger = new CapturingLogger<RetireKeyHandler>();
        var handler = new RetireKeyHandler(
            KcAppTestKit.ContextWithOrigin(origin, logger),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            new TestClock(created + Duration.FromDays(30)));

        var tags = NewTagCapture();
        D2Result<KeySummary?> result;

        using (var listener = BuildListener(tags))
        {
            listener.Start();
            result = await handler.HandleAsync(new RetireKeyInput(kid));
        }

        AssertDenied(result, origin);
        db.Keys.Single().Status.Should().Be(KeyStatus.Retiring);
        db.Audit.Should().BeEmpty();
        AssertDenyTelemetry(tags, logger.Entries, origin, "retire-key");
    }

    [Theory]
    [InlineData(RequestOrigin.Unestablished)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task CompromiseKey_NonSystemOrigin_Denied_KeyStaysLive_TelemetryFired(
        RequestOrigin origin)
    {
        // A live active key that WOULD compromise under System (valid reason supplied).
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.COOKIE,
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);
        var announcer = new RecordingAnnouncer();
        var logger = new CapturingLogger<CompromiseKeyHandler>();
        var handler = new CompromiseKeyHandler(
            KcAppTestKit.ContextWithOrigin(origin, logger),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            announcer,
            r_crypto,
            new TestClock(created));

        var tags = NewTagCapture();
        D2Result<CompromiseKeyOutput?> result;

        using (var listener = BuildListener(tags))
        {
            listener.Start();
            result = await handler.HandleAsync(
                new CompromiseKeyInput { Kid = kid, Reason = "operator reason" });
        }

        AssertDenied(result, origin);
        db.Keys.Single().Status.Should().Be(
            KeyStatus.Active, because: "a denied compromise performs no state transition");
        db.Audit.Should().BeEmpty();
        announcer.Calls.Should().BeEmpty(because: "a denied compromise announces nothing");
        AssertDenyTelemetry(tags, logger.Entries, origin, "compromise-key");
    }

    [Theory]
    [InlineData(RequestOrigin.Unestablished)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task RunDueRotations_NonSystemOrigin_Denied_NoBootstrap_TelemetryFired(
        RequestOrigin origin)
    {
        // A full bootstrap map over an empty store WOULD bootstrap every non-CA domain
        // under System — the deny must leave the store completely empty.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var policy = KcAppTestKit.BuildPolicyProvider(r_options);
        var optionsAccessor = KcAppTestKit.BuildOptionsAccessor();
        var logger = new CapturingLogger<RunDueRotationsHandler>();

        var generate = new GenerateKeyHandler(
            KcAppTestKit.SystemContext<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            optionsAccessor,
            r_crypto,
            clock);
        var activate = new ActivateKeyHandler(
            KcAppTestKit.SystemContext<ActivateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            policy,
            r_crypto,
            clock);
        var rotate = new RotateKeyHandler(
            KcAppTestKit.SystemContext<RotateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            policy,
            new RecordingAnnouncer(),
            r_crypto,
            clock);
        var retire = new RetireKeyHandler(
            KcAppTestKit.SystemContext<RetireKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            policy,
            clock);
        var getPlan = new GetRotationPlanHandler(
            KcAppTestKit.Context<GetRotationPlanHandler>(), db, policy, clock);

        var handler = new RunDueRotationsHandler(
            KcAppTestKit.ContextWithOrigin(origin, logger),
            db,
            getPlan,
            generate,
            activate,
            rotate,
            retire);

        var bootstrap = KeyDomain.All
            .Where(d => d.KeyType != KeyType.X509CaCertificate)
            .ToDictionary(d => d.Value, d => d.KeyType, StringComparer.Ordinal);

        var tags = NewTagCapture();
        D2Result<RunDueRotationsOutput?> result;

        using (var listener = BuildListener(tags))
        {
            listener.Start();
            result = await handler.HandleAsync(new RunDueRotationsInput(bootstrap));
        }

        AssertDenied(result, origin);
        db.Keys.Should().BeEmpty(because: "a denied orchestration bootstraps nothing");
        db.Audit.Should().BeEmpty();
        AssertDenyTelemetry(tags, logger.Entries, origin, "run-due-rotations");
    }

    [Theory]
    [InlineData(RequestOrigin.Unestablished)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task SeedCertificateAuthority_NonSystemOrigin_Denied_ProviderNeverConsulted(
        RequestOrigin origin)
    {
        // The throwing CA-provider fake proves the gate fires BEFORE the provider is
        // consulted — a denied seed never touches key material at all.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<SeedCertificateAuthorityHandler>();
        var handler = new SeedCertificateAuthorityHandler(
            KcAppTestKit.ContextWithOrigin(origin, logger),
            KcAppTestKit.NullClassifier(),
            db,
            new UnreachableCaProviderFake(),
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));

        var tags = NewTagCapture();
        D2Result<SeedCertificateAuthorityOutput?> result;

        using (var listener = BuildListener(tags))
        {
            listener.Start();
            result = await handler.HandleAsync(new SeedCertificateAuthorityInput());
        }

        AssertDenied(result, origin);
        db.Keys.Should().BeEmpty(because: "a denied seed persists nothing");
        db.Audit.Should().BeEmpty();
        AssertDenyTelemetry(tags, logger.Entries, origin, "seed-certificate-authority");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static List<(string Capability, string Reason)> NewTagCapture() => [];

    private static MeterListener BuildListener(List<(string Capability, string Reason)> tags)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && instrument.Name == _AUTHORITY_REJECTIONS)
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, tagList, _) =>
        {
            string capability = string.Empty;
            string reason = string.Empty;

            foreach (var tag in tagList)
            {
                if (tag.Key == KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY)
                    capability = tag.Value?.ToString() ?? string.Empty;

                if (tag.Key == KeyCustodianMetrics.AuthorityRejections.TAG_REASON)
                    reason = tag.Value?.ToString() ?? string.Empty;
            }

            tags.Add((capability, reason));
        });

        return listener;
    }

    private static void AssertDenied<TOutput>(D2Result<TOutput?> result, RequestOrigin origin)
    {
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        if (origin == RequestOrigin.Unestablished)
        {
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED,
                "the type-zero fail-closed arm is checked FIRST");
        }
    }

    private static void AssertDenyTelemetry(
        List<(string Capability, string Reason)> tags,
        ConcurrentQueue<(EventId EventId, string Message)> logEntries,
        RequestOrigin origin,
        string operation)
    {
        var expectedReason = origin == RequestOrigin.Unestablished
            ? KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED
            : KeyCustodianMetrics.AuthorityRejections.Reason.NOT_SYSTEM;

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.LIFECYCLE, expectedReason),
            "every lifecycle deny fires the authority-rejection counter with the "
            + "lifecycle capability tag and the arm-specific reason");

        logEntries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.LIFECYCLE,
                    StringComparison.Ordinal)
                && e.Message.Contains(operation, StringComparison.Ordinal),
            "every lifecycle deny fires the AuthorityRejected forensic log naming the "
            + "denied operation");
    }

    /// <summary>
    /// CA-provider fake that throws on any access — proves the authority gate denies
    /// BEFORE the seed path consults the provider.
    /// </summary>
    private sealed class UnreachableCaProviderFake : ICaProvider
    {
        public D2Result<LoadedCaMaterial> GetSeedCaMaterial() =>
            throw new InvalidOperationException(
                "the CA provider must never be consulted on a denied seed");
    }

    /// <summary>Thread-safe capturing logger for asserting log entries by EventId.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<(EventId EventId, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((eventId, formatter(state, exception)));
    }
}
