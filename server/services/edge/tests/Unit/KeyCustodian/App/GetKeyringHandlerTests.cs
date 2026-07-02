// -----------------------------------------------------------------------
// <copyright file="GetKeyringHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.Clients;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// The <see cref="GetKeyringHandler"/> matrix driven through the REAL handler over an
/// in-memory DbContext + a controllable <c>MutableRequestContext</c>: input validation, the
/// authority deny branches (every origin × caller × policy) with their telemetry, the
/// active+retiring serving shape (ordering + exclusions + the no-active 503),
/// the defense-in-depth key-type fork (reachable only via an injected validator-forbidden
/// grant) vs the production no-oracle 403, and the in-process scope gate. The real
/// encrypt/decrypt round-trip lives in the Testcontainers integration gate; here the unwrap
/// is proven by the 32-byte AES key length.
/// </summary>
public sealed class GetKeyringHandlerTests
{
    private const string _AUDIT = "audit";
    private const string _NOTIFICATIONS = "notifications";
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";
    private const string _EMPTY_KEYRING = "d2.keycustodian.empty_keyring_served";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // 1. Input validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-domain")]
    public async Task GetKeyring_InvalidOrUnknownDomain_Returns400UnknownKeyDomain(string? domain)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])))
            .HandleAsync(new GetKeyringInput(domain!));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetKeyring_CaseVariantDomain_NormalizesAndSucceeds()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var activeKid = await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])))
            .HandleAsync(new GetKeyringInput("AUDIT"));

        result.Success.Should().BeTrue("a case-variant domain normalizes to the catalog value");
        result.Data!.ActiveKid.Should().Be(activeKid);
    }

    // -----------------------------------------------------------------------
    // 2. Happy path — serving shape
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetKeyring_ActiveOnly_ReturnsSingleEntry_UnwrappedTo32Bytes()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var activeKid = await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])))
            .HandleAsync(new GetKeyringInput(_AUDIT));

        result.Success.Should().BeTrue();
        result.Data!.ActiveKid.Should().Be(activeKid);
        result.Data.Entries.Should().ContainSingle();
        result.Data.Entries[0].Kid.Should().Be(activeKid);

        // 32 bytes proves the root-unwrap ran (the stored material is wrapped + longer).
        result.Data.Entries[0].KeyBytes.Should().HaveCount(32);

        // AAD is the frozen "d2/<domain>" convention.
        result.Data.AadContext.Should().Equal("d2/audit"u8.ToArray());
    }

    [Fact]
    public async Task GetKeyring_ActivePlusRetiring_OrdersActiveFirstThenRetiringNewestActivatedFirst()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var active = await SeedPayloadKey(db, _AUDIT, KeyStatus.Active, activatedAt: Instant(30));

        // Two retiring rows with distinct ActivatedAt — newest-activated-first ordering.
        var retiringNewer = await SeedPayloadKey(
            db, _AUDIT, KeyStatus.Retiring, activatedAt: Instant(20), retiringAt: Instant(35));
        var retiringOlder = await SeedPayloadKey(
            db, _AUDIT, KeyStatus.Retiring, activatedAt: Instant(10), retiringAt: Instant(35));

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])))
            .HandleAsync(new GetKeyringInput(_AUDIT));

        result.Success.Should().BeTrue();
        result.Data!.ActiveKid.Should().Be(active);
        result.Data.Entries.Select(e => e.Kid).Should().Equal(
            active, retiringNewer, retiringOlder);
        result.Data.Entries.Should().OnlyContain(e => e.KeyBytes.Length == 32);
    }

    // -----------------------------------------------------------------------
    // 3. No active key → 503 (+ counter + 9513 log). The 503 fires even when
    //    Retiring/Pending rows exist (no active kid = unusable keyring).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetKeyring_NoKeysAtAll_Returns503_FiresCounterAndLog()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetKeyringHandler>();
        var emptyKeyring = new List<long>();

        using (var listener = BuildEmptyKeyringListener(emptyKeyring))
        {
            listener.Start();

            var result = await Build(
                    db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])), logger)
                .HandleAsync(new GetKeyringInput(_AUDIT));

            result.Success.Should().BeFalse();

            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_KEY_UNAVAILABLE);

            result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        emptyKeyring.Should().Contain(
            1L, "the no-active-key path increments SR_EmptyKeyringServed");

        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9513 && e.Message.Contains(_AUDIT, StringComparison.Ordinal),
            "the 9513 warning names the domain");

        // The 9513 log carries the domain ONLY — no kid, no key material.
        logger.Entries.Where(e => e.EventId.Id == 9513)
            .Should().OnlyContain(
                e => !e.Message.Contains("KeyBytes", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(KeyStatus.Pending)]
    [InlineData(KeyStatus.Retiring)]
    public async Task GetKeyring_NoActiveButOtherRows_Returns503(KeyStatus status)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, status, retiringAt: Instant(10));

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])))
            .HandleAsync(new GetKeyringInput(_AUDIT));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_KEY_UNAVAILABLE,
            "a keyring with no active kid is unusable — 503 even with pending/retiring rows");
    }

    // -----------------------------------------------------------------------
    // 3b. State exclusions — compromised / retired never served
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyStatus.Compromised)]
    [InlineData(KeyStatus.Retired)]
    public async Task GetKeyring_ExcludedStateAlongsideActive_NeverServesTheExcludedKey(
        KeyStatus excluded)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var activeKid = await SeedPayloadKey(
            db, _AUDIT, KeyStatus.Active, activatedAt: Instant(30));
        var excludedKid = await SeedPayloadKey(
            db, _AUDIT, excluded, activatedAt: Instant(10), retiringAt: Instant(15));

        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])))
            .HandleAsync(new GetKeyringInput(_AUDIT));

        result.Success.Should().BeTrue();
        result.Data!.Entries.Select(e => e.Kid).Should().Equal(activeKid);
        result.Data.Entries.Should().NotContain(
            e => e.Kid == excludedKid, $"a {excluded} key is never served");
    }

    // -----------------------------------------------------------------------
    // 4. Authority deny branches (through the REAL handler) + telemetry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetKeyring_UnestablishedOrigin_Denied_FiresOriginUnestablishedTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await Build(
                    db, RequestOrigin.Unestablished, "audit", Policy(("audit", [_AUDIT])))
                .HandleAsync(new GetKeyringInput(_AUDIT));

            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
            result.Data.Should().BeNull("no key bytes are returned on a deny");
        }

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.KEYRING,
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED));
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.System)]
    public async Task GetKeyring_UnservedPlane_Denied_FiresUnauthorizedPlaneTelemetry(
        RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);
        var logger = new CapturingLogger<GetKeyringHandler>();
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await Build(
                    db, origin, "audit", Policy(("audit", [_AUDIT])), logger)
                .HandleAsync(new GetKeyringInput(_AUDIT));

            // Uniform 403 wire code — telemetry distinguishes the plane deny.
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED);
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.KEYRING,
                KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE));
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.KEYRING,
                    StringComparison.Ordinal)
                && e.Message.Contains(_AUDIT, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetKeyring_ServedPlaneNoCaller_Denied_FiresIdentityAbsentTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await Build(db, RequestOrigin.CrossProcessHop, null, Policy())
                .HandleAsync(new GetKeyringInput(_AUDIT));

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.KEYRING,
                KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT));
    }

    [Fact]
    public async Task GetKeyring_CallerNotInPolicy_Denied_FiresNotInAllowedSetTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await Build(
                    db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_NOTIFICATIONS])))
                .HandleAsync(new GetKeyringInput(_AUDIT));

            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED);
        }

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.KEYRING,
                KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_ALLOWED_SET));
    }

    [Fact]
    public async Task GetKeyring_UnknownCaller_EmptyPolicy_Denied()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "ghost", Policy())
            .HandleAsync(new GetKeyringInput(_AUDIT));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED);
    }

    // -----------------------------------------------------------------------
    // 5. The no-oracle ordering: a non-payload domain requested by an otherwise-valid
    //    caller (holding a real payload grant, NOT the non-payload one) is denied at
    //    authority (403) — uniform with an unauthorized payload domain, no domain-type
    //    distinction. This is the PRODUCTION path.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    [InlineData(KeyDomain.COOKIE)]
    [InlineData(KeyDomain.CLIENT_SECRET)]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE)]
    public async Task GetKeyring_NonPayloadDomain_ValidCaller_Returns403NotAuthorized_NoOracle(
        string nonPayload)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        // The caller holds a real payload grant (audit) but NOT the non-payload domain.
        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "audit", Policy(("audit", [_AUDIT])))
            .HandleAsync(new GetKeyringInput(nonPayload));

        // Same 403 code as an unauthorized PAYLOAD domain — no domain-type oracle. The
        // type fork (400) is NOT reached because authority denies first.
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED);
        result.ErrorCode.Should().NotBe(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH,
            "the authority arm denies a non-payload domain BEFORE the type fork — no oracle");
    }

    // -----------------------------------------------------------------------
    // 6. Defense-in-depth key-type fork (reachable ONLY by injecting a
    //    validator-forbidden non-payload grant directly into the policy).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    [InlineData(KeyDomain.COOKIE)]
    [InlineData(KeyDomain.CLIENT_SECRET)]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE)]
    public async Task GetKeyring_ValidatorBypassedNonPayloadGrant_Returns400TypeMismatch(
        string nonPayload)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        // Inject a grant the boot validator would REFUSE (a non-payload domain), so the
        // authority arm passes and the defense-in-depth type fork fires with a 400.
        var result = await Build(
                db, RequestOrigin.CrossProcessHop, "edge", Policy(("edge", [nonPayload])))
            .HandleAsync(new GetKeyringInput(nonPayload));

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH,
            "with authority bypassed, the belt-and-braces type fork rejects a non-payload domain");
    }

    // -----------------------------------------------------------------------
    // 7. In-process scope gate (defense-in-depth, fires before authority/binding)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetKeyring_WithoutRequiredScope_ReturnsForbidden_BeforeAuthorityOrBinding()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);

        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "audit",
                Policy(("audit", [_AUDIT])),
                scopes: new HashSet<string>(StringComparer.Ordinal))
            .HandleAsync(new GetKeyringInput(_AUDIT));

        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the in-process internal.kc.keyring scope gate is fail-closed");
    }

    [Fact]
    public async Task GetKeyring_WithRequiredScope_PassesScopeGate_Succeeds()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedPayloadKey(db, _AUDIT, KeyStatus.Active);

        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "audit",
                Policy(("audit", [_AUDIT])),
                scopes: new HashSet<string>(StringComparer.Ordinal) { Scopes.Internal.Kc.Keyring })
            .HandleAsync(new GetKeyringInput(_AUDIT));

        result.Success.Should().BeTrue(
            "the request carrying the required scope reaches the handler");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Instant Instant(int minutes) =>
        KcAppTestKit.SR_BaseInstant.Plus(Duration.FromMinutes(minutes));

    private static IKeyringDomainAuthorityPolicy Policy(
        params (string Workload, string[] Domains)[] grants)
    {
        var options = new KeyringDomainAuthorityOptions();

        foreach (var (workload, domains) in grants)
            options.AllowedKeyringDomainsByWorkload[workload] = [.. domains];

        return new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));
    }

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

    private static MeterListener BuildEmptyKeyringListener(List<long> emptyKeyring)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && instrument.Name == _EMPTY_KEYRING)
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((_, value, _, _) => emptyKeyring.Add(value));

        return listener;
    }

    private Task<string> SeedPayloadKey(
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
            KeyType.AesPayload,
            status,
            KcAppTestKit.SR_BaseInstant,
            activatedAt: activatedAt ?? KcAppTestKit.SR_BaseInstant,
            retiringAt: retiringAt);

    private GetKeyringHandler Build(
        KeyCustodianTestDbContext db,
        RequestOrigin origin,
        string? caller,
        IKeyringDomainAuthorityPolicy policy,
        ILogger<GetKeyringHandler>? logger = null,
        IReadOnlySet<string>? scopes = null)
    {
        var grantedScopes = scopes
            ?? new HashSet<string>(StringComparer.Ordinal) { Scopes.Internal.Kc.Keyring };

        var ctx = new HandlerContext<GetKeyringHandler>(
            new MutableRequestContext
            {
                Origin = origin,
                ImmediateCaller = caller,
                Scopes = grantedScopes,
            },
            logger ?? NullLogger<GetKeyringHandler>.Instance);

        return new GetKeyringHandler(ctx, db, r_crypto, policy);
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
