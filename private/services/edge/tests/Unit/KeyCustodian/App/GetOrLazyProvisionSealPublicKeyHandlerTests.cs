// -----------------------------------------------------------------------
// <copyright file="GetOrLazyProvisionSealPublicKeyHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Private.Auth;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using D2.Shared.Utilities.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The <see cref="GetOrLazyProvisionSealPublicKeyHandler"/> matrix driven through the REAL handler over an
/// in-memory DbContext + a controllable <c>MutableRequestContext</c>: the seal-encrypt
/// authority deny branches (every origin ├ù identity) with their telemetry, the
/// authority-before-validation ordering (no serviceId oracle), serviceId validation, lazy
/// provisioning of the per-service ECDH keypair (one Active row + Generated/Activated audit +
/// the provisioned counter + 9516 log), the no-Active 503 when a Pending blocks provisioning,
/// the Active+Retiring serving shape, and the in-process scope gate. The public SPKI is served
/// straight from <c>PublicKeyMaterial</c> and is NOT redacted; the full sealΓåÆopen crypto
/// round-trip lives in the Testcontainers integration gate.
/// </summary>
public sealed class GetOrLazyProvisionSealPublicKeyHandlerTests
{
    private const string _AUDIT = "audit";
    private const string _FILES = "files";
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";
    private const string _SEAL_PROVISIONED = "d2.keycustodian.seal_keypairs_provisioned";
    private const string _SEAL_UNAVAILABLE = "d2.keycustodian.seal_key_unavailable";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // 1. Authority deny â€” unestablished origin (fail-closed first arm)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_UnestablishedOrigin_Denied_FiresOriginUnestablishedTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetOrLazyProvisionSealPublicKeyHandler>();
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await BuildPublic(db, RequestOrigin.Unestablished, _FILES, logger)
                .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            result.Data.Should().BeNull("no key material is returned on a deny");
        }

        db.Keys.Should().BeEmpty("a denied request never provisions a key");

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_ENCRYPT,
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED));

        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_ENCRYPT,
                    StringComparison.Ordinal)
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Target.NONE, StringComparison.Ordinal),
            "the 9512 AuthorityRejected log carries the seal-encrypt capability + the none target");
    }

    // -----------------------------------------------------------------------
    // 2. Authority deny â€” unserved plane (System / EdgeInbound). InProcessModule +
    //    CrossProcessHop are the served planes and proceed to provision.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.System)]
    [InlineData(RequestOrigin.EdgeInbound)]
    public async Task GetOrLazyProvisionSealPublicKey_UnservedPlane_Denied_FiresUnauthorizedPlaneTelemetry(
        RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await BuildPublic(db, origin, _FILES)
                .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

            result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_NOT_AUTHORIZED);
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        db.Keys.Should().BeEmpty("an unserved-plane deny never provisions a key");

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_ENCRYPT,
                KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE));
    }

    [Theory]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task GetOrLazyProvisionSealPublicKey_ServedPlane_ProceedsAndProvisions(RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPublic(db, origin, _FILES)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

        result.Success.Should().BeTrue(
            "seal-encrypt serves the cross-process hop + the in-process module planes");
        db.Keys.Should().ContainSingle("the served plane provisioned the target's seal keypair");
    }

    // -----------------------------------------------------------------------
    // 3. Authority deny â€” served plane, no caller identity (fail-closed peer)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetOrLazyProvisionSealPublicKey_ServedPlaneNoCaller_Denied_FiresIdentityAbsentTelemetry(
        string? caller)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await BuildPublic(db, RequestOrigin.CrossProcessHop, caller)
                .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        db.Keys.Should().BeEmpty();

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_ENCRYPT,
                KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT));
    }

    // -----------------------------------------------------------------------
    // 4. In-process scope gate (defense-in-depth, before authority/validation)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_WithoutRequiredScope_ReturnsForbidden()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPublic(
                db,
                RequestOrigin.CrossProcessHop,
                _FILES,
                scopes: new HashSet<string>(StringComparer.Ordinal))
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "the internal.kc.seal.encrypt scope gate is fail-closed");
        db.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_WrongScope_ReturnsForbidden()
    {
        // Holding the SEAL OPEN scope does not satisfy the SEAL ENCRYPT requirement.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPublic(
                db,
                RequestOrigin.CrossProcessHop,
                _FILES,
                scopes: new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Seal.Open })
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        db.Keys.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // 5. serviceId validation â€” 400 on garbage (authorized caller)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad service")]
    [InlineData("under_score")]
    [InlineData("dot.dot")]
    [InlineData("cafÃ©")]
    [InlineData("æ—¥æœ¬")]
    [InlineData(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaX")]
    public async Task GetOrLazyProvisionSealPublicKey_GarbageServiceId_Returns400InvalidWorkloadIdentity_NoWrite(
        string serviceId)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPublic(db, RequestOrigin.CrossProcessHop, _FILES)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(serviceId));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        db.Keys.Should().BeEmpty("an invalid serviceId never reaches the store");
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_NullServiceId_Returns400InvalidWorkloadIdentity()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPublic(db, RequestOrigin.CrossProcessHop, _FILES)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(null!));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
        db.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_UppercaseServiceId_NormalizesAndProvisions()
    {
        // The shared workload grammar trims + lowercases, so an uppercase serviceId is a
        // VALID input that normalizes â€” NOT a 400. It provisions the seal:audit domain.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPublic(db, RequestOrigin.CrossProcessHop, _FILES)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput("AUDIT"));

        result.Success.Should().BeTrue("an uppercase serviceId normalizes to a valid workload id");
        db.Keys.Single().KeyDomain.Should().Be("seal:audit");
    }

    // -----------------------------------------------------------------------
    // 6. Ordering â€” authority precedes validation (no serviceId oracle)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_UnauthorizedCaller_GarbageServiceId_Returns403_NoValidationOracle()
    {
        // An unauthorized (unserved-plane) caller supplying a garbage serviceId gets the
        // uniform 403 â€” the authority arm denies BEFORE the serviceId is validated, so no
        // 400 confirms whether the requested serviceId is well-formed.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPublic(db, RequestOrigin.System, _FILES)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput("bad service"));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_NOT_AUTHORIZED);
        result.ErrorCode.Should().NotBe(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY,
            "authority denies before validation â€” no serviceId oracle");
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        db.Keys.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // 7. Lazy provisioning â€” first request provisions one Active EcdhSealing key
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_FirstRequest_ProvisionsOneActiveSealKey_FiresCounterAndLog()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetOrLazyProvisionSealPublicKeyHandler>();
        var provisioned = new List<long>();

        GetOrLazyProvisionSealPublicKeyOutput served;

        using (var listener = BuildProvisionedListener(provisioned))
        {
            listener.Start();

            var result = await BuildPublic(db, RequestOrigin.CrossProcessHop, _FILES, logger)
                .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

            result.Success.Should().BeTrue();
            served = result.Data!;
        }

        // Exactly one Active EcdhSealing key under the seal:audit domain.
        var row = db.Keys.Should().ContainSingle().Which;
        row.KeyDomain.Should().Be("seal:audit");
        row.KeyType.Should().Be(KeyType.EcdhSealing);
        row.Status.Should().Be(KeyStatus.Active, "provisioning inline-activates the fresh key");
        row.ActivatedAt.Should().NotBeNull();

        // Served shape: the active kid leads, its public SPKI is non-empty.
        served.ActiveKid.Should().Be(row.Kid);
        served.Entries.Should().ContainSingle();
        served.Entries[0].Kid.Should().Be(row.Kid);
        served.Entries[0].PublicSpki.Should().NotBeNullOrEmpty();

        // Both a Generated and an Activated audit row were written in the same transaction.
        db.Audit.Select(a => a.Action).Should().Contain(
            [KeyAuditAction.Generated, KeyAuditAction.Activated]);

        provisioned.Should().Contain(1L, "the provisioning path increments the seal counter");

        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9516
                && e.Message.Contains(_AUDIT, StringComparison.Ordinal)
                && e.Message.Contains(_FILES, StringComparison.Ordinal),
            "the 9516 log names the provisioned service + the triggering caller");
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_SecondRequest_ReusesActive_NoSecondProvision()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = BuildPublic(db, RequestOrigin.CrossProcessHop, _FILES);

        var first = await handler.HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));
        var second = await handler.HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();

        db.Keys.Should().ContainSingle("the second request reuses the provisioned key");
        second.Data!.ActiveKid.Should().Be(
            first.Data!.ActiveKid, "both requests serve the same active kid");
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_LivePendingNoActive_Returns503Unavailable_FiresLog()
    {
        // A live Pending successor (no Active) blocks provisioning â€” the domain is a
        // retryable not-ready window, never a second provision.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetOrLazyProvisionSealPublicKeyHandler>();
        var unavailable = new List<long>();

        await SeedSealKey(db, "seal:audit", KeyStatus.Pending);

        using (var listener = BuildUnavailableListener(unavailable))
        {
            listener.Start();

            var result = await BuildPublic(db, RequestOrigin.CrossProcessHop, _FILES, logger)
                .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE);
            result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        db.Keys.Should().ContainSingle("no second key was provisioned while a Pending exists");

        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9517 && e.Message.Contains(_AUDIT, StringComparison.Ordinal),
            "the 9517 SealKeyUnavailable warning names the service");

        unavailable.Should().Contain(
            1L, "the 503 no-active-key path increments the seal-unavailable counter");
    }

    // -----------------------------------------------------------------------
    // 8. Serving shape â€” Active + Retiring (active leads)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_ActivePlusRetiring_ServesActiveFirst()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var active = await SeedSealKey(
            db, "seal:audit", KeyStatus.Active, activatedAt: Instant(30));
        var retiring = await SeedSealKey(
            db, "seal:audit", KeyStatus.Retiring, activatedAt: Instant(10), retiringAt: Instant(35));

        var result = await BuildPublic(db, RequestOrigin.CrossProcessHop, _FILES)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_AUDIT));

        result.Success.Should().BeTrue();
        result.Data!.ActiveKid.Should().Be(active);
        result.Data.Entries.Select(e => e.Kid).Should().Equal(active, retiring);
        result.Data.Entries.Should().OnlyContain(e => e.PublicSpki.Length > 0);
    }

    // -----------------------------------------------------------------------
    // 9. Redaction posture â€” the served public SPKI is NOT redacted
    // -----------------------------------------------------------------------

    [Fact]
    public void SealPublicEntry_PublicSpki_IsNotRedacted()
    {
        // Public key material is wire-public and must be visible in logs / telemetry â€”
        // the DTO property carries NO [RedactData] marker (contrast the private DTO).
        typeof(SealPublicEntry).GetProperty(nameof(SealPublicEntry.PublicSpki))!
            .GetCustomAttribute<RedactDataAttribute>()
            .Should().BeNull("the public sealing SPKI is harmless to over-share");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Instant Instant(int minutes) =>
        KcAppTestKit.SR_BaseInstant.Plus(Duration.FromMinutes(minutes));

    private static MeterListener BuildAuthorityListener(
        List<(string Capability, string Reason)> tags)
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

        listener.SetMeasurementEventCallback<long>((_, _, measurementTags, _) =>
        {
            string capability = string.Empty;
            string reason = string.Empty;

            foreach (var tag in measurementTags)
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

    private static MeterListener BuildProvisionedListener(List<long> provisioned)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && instrument.Name == _SEAL_PROVISIONED)
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((_, value, _, _) => provisioned.Add(value));

        return listener;
    }

    private static MeterListener BuildUnavailableListener(List<long> unavailable)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && instrument.Name == _SEAL_UNAVAILABLE)
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((_, value, _, _) => unavailable.Add(value));

        return listener;
    }

    private Task<string> SeedSealKey(
        KeyCustodianTestDbContext db,
        string domain,
        KeyStatus status,
        NodaTime.Instant? activatedAt = null,
        NodaTime.Instant? retiringAt = null) =>
        KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            domain,
            KeyType.EcdhSealing,
            status,
            KcAppTestKit.SR_BaseInstant,
            activatedAt: activatedAt ?? KcAppTestKit.SR_BaseInstant,
            retiringAt: retiringAt);

    private GetOrLazyProvisionSealPublicKeyHandler BuildPublic(
        KeyCustodianTestDbContext db,
        RequestOrigin origin,
        string? caller,
        ILogger<GetOrLazyProvisionSealPublicKeyHandler>? logger = null,
        IReadOnlySet<string>? scopes = null)
    {
        var grantedScopes = scopes
            ?? new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Seal.Encrypt };

        var ctx = new HandlerContext<GetOrLazyProvisionSealPublicKeyHandler>(
            new MutableRequestContext
            {
                Origin = origin,
                ImmediateCaller = caller,
                Scopes = grantedScopes,
            },
            logger ?? NullLogger<GetOrLazyProvisionSealPublicKeyHandler>.Instance);

        return new GetOrLazyProvisionSealPublicKeyHandler(
            ctx,
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));
    }

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
