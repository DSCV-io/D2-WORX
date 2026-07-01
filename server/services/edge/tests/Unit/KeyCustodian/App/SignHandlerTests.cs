// -----------------------------------------------------------------------
// <copyright file="SignHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.Clients;
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
/// minter-required on this surface; every other domain signs cross-process per policy),
/// the deny-path telemetry fires THROUGH the handler (closing the call-site gap), and a
/// granted sign produces a signature that verifies against the key's public half.
/// </summary>
public sealed class SignHandlerTests
{
    private const string _AUDIT = "audit";
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";
    private const string _CROSS_PROCESS_REJECTIONS = "d2.keycustodian.cross_process_signing_rejections";

    private static readonly byte[] sr_input = "header.payload"u8.ToArray();
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task Sign_CrossProcessGrantedDomain_ReturnsSignatureAndKid_VerifiableAgainstPublicKey()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var kid = await SeedSigningKey(db, _AUDIT);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [_AUDIT])))
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.Success.Should().BeTrue();
        result.Data!.Kid.Should().Be(kid);

        var spki = db.Keys.Single(k => k.Kid == kid).PublicKeyMaterial!;
        using var verifier = RSA.Create();
        verifier.ImportSubjectPublicKeyInfo(spki, out _);

        var signature = Base64Url.DecodeFromChars(result.Data!.Signature);
        verifier.VerifyData(sr_input, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue("the handler signed with the active audit signing key");
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
        await SeedSigningKey(db, _AUDIT);

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "files", Policy(("files", ["client-secret"])))
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED);
    }

    [Fact]
    public async Task Sign_CrossProcessNoCaller_ReturnsForbidden()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _AUDIT);

        var result = await Build(db, RequestOrigin.CrossProcessHop, null, Policy())
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sign_InProcessNonRootDomain_ReturnsSigningDomainNotAuthorized()
    {
        // The general surface signs non-root domains CROSS-PROCESS only; an in-process
        // origin on this surface is denied (the minter path is separate, D4).
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _AUDIT);

        var result = await Build(db, RequestOrigin.InProcessModule, null, Policy())
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED);
    }

    [Theory]
    [InlineData(_AUDIT)]
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

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [_AUDIT])))
            .HandleAsync(new SignInput("not-a-real-domain", sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sign_EmptyInput_ReturnsEmptySigningInput()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _AUDIT);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [_AUDIT])))
            .HandleAsync(new SignInput(_AUDIT, []));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_EMPTY_SIGNING_INPUT);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sign_NoActiveKey_ReturnsSigningKeyUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [_AUDIT])))
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE);
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Sign_CorruptKeyMaterial_ReturnsPreconditionViolated()
    {
        // Real-wrapped but cryptographically corrupt material — decrypts cleanly then fails
        // PKCS#8 import in the signing rule → flagged 500 (no throw).
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedKeyWithCorruptMaterialAsync(
            db,
            r_crypto,
            _AUDIT,
            KeyType.RsaSigning,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant,
            corruptPlaintext: [0x01, 0x02, 0x03, 0x04],
            activatedAt: KcAppTestKit.SR_BaseInstant);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", [_AUDIT])))
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Sign_WithoutRequiredScope_ReturnsForbidden_BeforeAuthorityOrCrypto()
    {
        // No internal.kc.sign scope on the request context → BaseHandler's per-handler
        // ScopeRequirement gate rejects with Forbidden BEFORE the authority rule, the DB, or
        // any crypto runs. The caller + policy WOULD otherwise authorize (files → audit,
        // cross-process) and no key is seeded, so a Forbidden (not 503 SIGNING_KEY_UNAVAILABLE)
        // proves the scope gate fired first.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "files",
                Policy(("files", [_AUDIT])),
                scopes: new HashSet<string>(StringComparer.Ordinal))
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the in-process internal.kc.sign scope gate is fail-closed");
    }

    [Fact]
    public async Task Sign_WithRequiredScope_PassesScopeGate_AndSigns()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var kid = await SeedSigningKey(db, _AUDIT);

        // internal.kc.sign present → the scope gate admits the call; a granted domain signs.
        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "files",
                Policy(("files", [_AUDIT])),
                scopes: new HashSet<string>(StringComparer.Ordinal) { Scopes.Internal.Kc.Sign })
            .HandleAsync(new SignInput(_AUDIT, sr_input));

        result.Success.Should().BeTrue("the request carries the required internal.kc.sign scope");
        result.Data!.Kid.Should().Be(kid);
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

    [Fact]
    public async Task Sign_UnauthorizedDomainDeny_FiresNotInAllowedSet_NoCrossProcessCounter()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedSigningKey(db, _AUDIT);

        var authorityTags = new List<(string Capability, string Reason)>();
        var crossProcess = new List<long>();

        using (var listener = BuildListener(authorityTags, crossProcess))
        {
            listener.Start();

            await Build(db, RequestOrigin.CrossProcessHop, "files", Policy(("files", ["client-secret"])))
                .HandleAsync(new SignInput(_AUDIT, sr_input));
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
                .HandleAsync(new SignInput(_AUDIT, sr_input));
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

            await Build(db, RequestOrigin.Unestablished, "files", Policy(("files", [_AUDIT])))
                .HandleAsync(new SignInput(_AUDIT, sr_input));
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
