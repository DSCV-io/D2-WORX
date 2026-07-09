// -----------------------------------------------------------------------
// <copyright file="SignHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.App.Application.Signing;
using D2.Edge.KeyCustodian.Client.Signing;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The general <see cref="SignHandler"/> authority matrix driven through the REAL handler
/// over an in-memory DbContext + a controllable <c>MutableRequestContext</c>: the
/// established <c>Origin</c> + <c>ImmediateCaller</c> gate every request through the
/// refined rule (Unestablished denies; <c>jwks-signing</c> is categorically
/// minter-required on this surface; the CA trust anchors are never-signable for every
/// origin; every other domain signs cross-process per policy), the deny-path telemetry
/// fires THROUGH the handler (closing the call-site gap), and an authority-passing
/// request against a non-signing-bound domain is sharply rejected with the permanent
/// 400 key-type mismatch — never the retryable 503. The shared signing core's
/// happy path (signature verification, empty input, no active key, corrupt material)
/// is exercised through the minter capability in <c>JwtSigningCapabilityTests</c> —
/// at this catalog no generally-signable RSA-bound domain exists.
/// </summary>
public sealed class SignHandlerTests : IDisposable
{
    private const string _PAYLOAD = FixturePayloadDomains.PAYLOAD_A;
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";
    private const string _CROSS_PROCESS_REJECTIONS = "d2.keycustodian.cross_process_signing_rejections";

    private static readonly byte[] sr_input = "header.payload"u8.ToArray();

    private readonly IDisposable r_fixtureSeam = FixturePayloadDomains.Register();
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    /// <summary>Unregisters the fixture payload domain (ref-counted, per-test-instance).</summary>
    public void Dispose() => r_fixtureSeam.Dispose();

    [Fact]
    public void KeyDomainSigner_StaysInternal_NoExternalSigningOracle()
    {
        // Invariant: KeyDomainSigner stays internal, so the shared signing core is never
        // reachable from outside the App assembly — no external caller can bypass an
        // authority gate to reach a raw signing oracle over every managed signing key.
        typeof(KeyDomainSigner).IsNotPublic.Should().BeTrue(
            "the signing core stays internal so every path to it clears an authority gate");
    }

    [Theory]
    [InlineData(_PAYLOAD)]
    [InlineData(KeyDomain.COOKIE)]
    public async Task Sign_CrossProcessGrantedNonSigningDomain_Returns400Mismatch_Not503(
        string domain)
    {
        // The caller + policy authorize, but the domain's bound key type can never
        // hold a signing key — a PERMANENT 400, not the retryable 503 that "no active
        // key yet" would produce. No key is seeded on purpose: the 400 (rather than
        // SIGNING_KEY_UNAVAILABLE) proves the sharp reject fires before the key load.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [domain])))
            .HandleAsync(new SignInput(domain, sr_input));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH);
    }

    [Fact]
    public async Task Sign_CrossProcessGrantedNonSigningDomain_WithAnomalousRsaKey_Still400()
    {
        // Even an anomalous store row (an RSA key persisted in an AES-bound domain)
        // cannot resurrect the general surface — the binding, not the store contents,
        // decides. Regression-pins that the reject is structural.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _PAYLOAD);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [_PAYLOAD])))
            .HandleAsync(new SignInput(_PAYLOAD, sr_input));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH);
    }

    [Fact]
    public async Task Sign_CrossProcessJwksSigning_ReturnsMinterCapabilityRequired()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, KeyDomain.JWKS_SIGNING);

        // Even a misconfigured policy granting jwks-signing cannot make the general surface
        // allow — the cluster root is reachable only through the minter capability.
        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "edge", Policy(("edge", [KeyDomain.JWKS_SIGNING])))
            .HandleAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED);
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sign_InProcessJwksSigning_StillReturnsMinterCapabilityRequired()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, KeyDomain.JWKS_SIGNING);

        var result = await Build(db, RequestOrigin.InProcessModule, null, Policy())
            .HandleAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED,
            "the general surface rejects jwks-signing for every origin, even in-process");
    }

    [Fact]
    public async Task Sign_CrossProcessUnauthorizedDomain_ReturnsSigningDomainNotAuthorized()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _PAYLOAD);

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "files", Policy(("files", ["client-secret"])))
            .HandleAsync(new SignInput(_PAYLOAD, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED);
    }

    [Fact]
    public async Task Sign_CrossProcessNoCaller_ReturnsForbidden()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _PAYLOAD);

        var result = await Build(db, RequestOrigin.CrossProcessHop, null, Policy())
            .HandleAsync(new SignInput(_PAYLOAD, sr_input));

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sign_InProcessNonRootDomain_ReturnsSigningDomainNotAuthorized()
    {
        // The general surface signs non-root domains CROSS-PROCESS only; an in-process
        // origin on this surface is denied (the minter path is separate).
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _PAYLOAD);

        var result = await Build(db, RequestOrigin.InProcessModule, null, Policy())
            .HandleAsync(new SignInput(_PAYLOAD, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED);
    }

    [Theory]
    [InlineData(_PAYLOAD)]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    public async Task Sign_UnestablishedOrigin_SignsNothing(string domain)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, domain);

        // The scoped default — no boundary established the origin. Signs nothing, for any
        // domain (including jwks-signing): the unestablished-origin arm is first + fail-closed.
        var result = await Build(db, RequestOrigin.Unestablished, "files", Policy(("files", [domain])))
            .HandleAsync(new SignInput(domain, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
    }

    [Fact]
    public async Task Sign_UnknownDomain_ReturnsUnknownKeyDomain()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [_PAYLOAD])))
            .HandleAsync(new SignInput("not-a-real-domain", sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sign_WithoutRequiredScope_ReturnsForbidden_BeforeAuthorityOrBinding()
    {
        // No internal.kc.sign scope on the request context → BaseHandler's per-handler
        // ScopeRequirement gate rejects with Forbidden BEFORE the authority rule, the
        // binding check, the DB, or any crypto runs. The caller + policy WOULD otherwise
        // authorize (files → the fixture payload domain, cross-process) and the binding check
        // would then surface the 400 type mismatch — so a Forbidden (not the 400) proves the
        // scope gate fired first.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "files",
                Policy(("files", [_PAYLOAD])),
                scopes: new HashSet<string>(StringComparer.Ordinal))
            .HandleAsync(new SignInput(_PAYLOAD, sr_input));

        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the in-process internal.kc.sign scope gate is fail-closed");
        result.ErrorCode.Should().NotBe(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH,
            "the scope gate fires before the domain binding check");
    }

    [Fact]
    public async Task Sign_WithRequiredScope_PassesScopeGate_ReachesAuthorityAndBinding()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        // internal.kc.sign present → the scope gate admits the call; the request then
        // reaches the authority rule + the binding check, whose 400 mismatch (not a
        // Forbidden) proves the scope gate passed.
        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "files",
                Policy(("files", [_PAYLOAD])),
                scopes: new HashSet<string>(StringComparer.Ordinal) { Scopes.Internal.Kc.Sign })
            .HandleAsync(new SignInput(_PAYLOAD, sr_input));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH,
            "the request carrying the required scope reaches the binding check");
    }

    [Fact]
    public async Task Sign_JwksSigningDeny_FiresMinterRequiredTelemetry_ThroughRealHandler()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<SignHandler>();

        var authorityTags = new List<(string Capability, string Reason)>();
        var crossProcess = new List<long>();

        using (var listener = BuildListener(authorityTags, crossProcess))
        {
            listener.Start();

            var result = await Build(db, RequestOrigin.CrossProcessHop, "edge", Policy(), logger)
                .HandleAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

            result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED);
        }

        authorityTags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED));
        crossProcess.Should().Contain(
            1L, "a general-surface attempt to reach the cluster root fires the highest-severity counter");
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.SIGN, StringComparison.Ordinal)
                && e.Message.Contains("jwks-signing", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE)]
    public async Task Sign_CaDomainDeny_FiresNeverSignableTelemetry_AndCrossProcessCounter(
        string caDomain)
    {
        // A CA-domain signing attempt is a crown-jewel attempt: 403 through the real
        // handler with the never-signable reason tag, the highest-severity
        // cross-process rejection counter, AND the AuthorityRejected forensic log.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<SignHandler>();

        var authorityTags = new List<(string Capability, string Reason)>();
        var crossProcess = new List<long>();

        using (var listener = BuildListener(authorityTags, crossProcess))
        {
            listener.Start();

            var result = await Build(
                    db, RequestOrigin.CrossProcessHop, "edge", Policy(("edge", [caDomain])), logger)
                .HandleAsync(new SignInput(caDomain, sr_input));

            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED);
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        authorityTags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.NEVER_SIGNABLE));
        crossProcess.Should().Contain(
            1L, "a CA-domain signing attempt fires the highest-severity counter");
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                    StringComparison.Ordinal)
                && e.Message.Contains(caDomain, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sign_UnauthorizedDomainDeny_FiresNotInAllowedSet_NoCrossProcessCounter()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _PAYLOAD);

        var authorityTags = new List<(string Capability, string Reason)>();
        var crossProcess = new List<long>();

        using (var listener = BuildListener(authorityTags, crossProcess))
        {
            listener.Start();

            await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", ["client-secret"])))
                .HandleAsync(new SignInput(_PAYLOAD, sr_input));
        }

        authorityTags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_ALLOWED_SET));
        crossProcess.Should().NotContain(1L, "a policy-scope deny is not a cluster-root attempt");
    }

    [Fact]
    public async Task Sign_NoCallerDeny_FiresIdentityAbsentTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var authorityTags = new List<(string Capability, string Reason)>();
        var crossProcess = new List<long>();

        using (var listener = BuildListener(authorityTags, crossProcess))
        {
            listener.Start();

            await Build(db, RequestOrigin.CrossProcessHop, null, Policy())
                .HandleAsync(new SignInput(_PAYLOAD, sr_input));
        }

        authorityTags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT));
    }

    [Fact]
    public async Task Sign_UnestablishedDeny_FiresOriginUnestablishedTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var authorityTags = new List<(string Capability, string Reason)>();
        var crossProcess = new List<long>();

        using (var listener = BuildListener(authorityTags, crossProcess))
        {
            listener.Start();

            await Build(db, RequestOrigin.Unestablished, "files", Policy(("files", [_PAYLOAD])))
                .HandleAsync(new SignInput(_PAYLOAD, sr_input));
        }

        authorityTags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED));
    }

    private static MeterListener BuildListener(
        List<(string Capability, string Reason)> authorityTags, List<long> crossProcess)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && (instrument.Name == _AUTHORITY_REJECTIONS
                        || instrument.Name == _CROSS_PROCESS_REJECTIONS))
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == _CROSS_PROCESS_REJECTIONS)
            {
                crossProcess.Add(value);
                return;
            }

            string capability = string.Empty;
            string reason = string.Empty;

            foreach (var tag in tags)
            {
                if (tag.Key == KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY)
                    capability = tag.Value?.ToString() ?? string.Empty;

                if (tag.Key == KeyCustodianMetrics.AuthorityRejections.TAG_REASON)
                    reason = tag.Value?.ToString() ?? string.Empty;
            }

            authorityTags.Add((capability, reason));
        });

        return listener;
    }

    private static ISigningDomainAuthorityPolicy Policy(
        params (string Workload, string[] Domains)[] grants)
    {
        var options = new SigningDomainAuthorityOptions();

        foreach (var (workload, domains) in grants)
            options.AllowedSigningDomainsByWorkload[workload] = [.. domains];

        return new OptionsSigningDomainAuthorityPolicy(Options.Create(options));
    }

    private Task<string> SeedSigningKey(KeyCustodianTestDbContext db, string domain) =>
        KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            domain,
            KeyType.RsaSigning,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant,
            activatedAt: KcAppTestKit.SR_BaseInstant);

    private SignHandler Build(
        KeyCustodianTestDbContext db,
        RequestOrigin origin,
        string? caller,
        ISigningDomainAuthorityPolicy policy,
        ILogger<SignHandler>? logger = null,
        IReadOnlySet<string>? scopes = null)
    {
        // Default to the required internal.kc.sign scope so the BaseHandler ScopeRequirement
        // gate admits the call; pass an explicit set to exercise the gate itself.
        var grantedScopes = scopes
            ?? new HashSet<string>(StringComparer.Ordinal) { Scopes.Internal.Kc.Sign };

        var ctx = new HandlerContext<SignHandler>(
            new MutableRequestContext
            {
                Origin = origin,
                ImmediateCaller = caller,
                Scopes = grantedScopes,
            },
            logger ?? NullLogger<SignHandler>.Instance);

        return new SignHandler(ctx, db, r_crypto, policy);
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
