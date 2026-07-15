// -----------------------------------------------------------------------
// <copyright file="GetOrLazyProvisionOwnSealPrivateKeyHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionOwnSealPrivateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Observability;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The <see cref="GetOrLazyProvisionOwnSealPrivateKeyHandler"/> matrix driven through the REAL handler ΓÇö the
/// seal-decrypt hard gate. The controls it pins: seal-decrypt is cross-process-ONLY (in-process /
/// edge / system planes are ALL denied at the plane arm, before key selection); the op carries NO
/// target, so the key is selected purely from the authenticated caller (self-only is
/// structural ΓÇö a forged in-process caller never reaches provisioning, and a foreign caller on
/// the served plane gets its OWN key, never another service's); the private key is
/// root-UNWRAPPED before serving; the served PKCS#8 carries the <c>SecretInformation</c>
/// redaction marker and never appears in any log; the deny telemetry; the in-process scope
/// gate; and lazy provisioning + the no-Active 503.
/// </summary>
public sealed class GetOrLazyProvisionOwnSealPrivateKeyHandlerTests
{
    private const string _AUDIT = "audit";
    private const string _FILES = "files";
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";
    private const string _SEAL_PROVISIONED = "d2.keycustodian.seal_keypairs_provisioned";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // 1. Authority deny â€” unestablished origin (fail-closed first arm)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_UnestablishedOrigin_Denied_FiresOriginUnestablishedTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetOrLazyProvisionOwnSealPrivateKeyHandler>();
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await BuildPrivate(db, RequestOrigin.Unestablished, _AUDIT, logger)
                .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            result.Data.Should().BeNull("no private key is returned on a deny");
        }

        db.Keys.Should().BeEmpty("a denied request never provisions a key");

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_DECRYPT,
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED));

        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_DECRYPT,
                    StringComparison.Ordinal)
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Target.NONE, StringComparison.Ordinal),
            "the 9512 log carries the seal-decrypt capability + the targetless none marker");
    }

    // -----------------------------------------------------------------------
    // 2. The seal-decrypt hard gate â€” EVERY non-cross-process plane is denied
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.InProcessModule)]
    [InlineData(RequestOrigin.System)]
    [InlineData(RequestOrigin.EdgeInbound)]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_NonCrossProcessPlane_Denied_UnauthorizedPlane(
        RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await BuildPrivate(db, origin, _AUDIT)
                .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_NOT_AUTHORIZED,
                "seal-decrypt is cross-process ONLY â€” no unforgeable in-process identity exists");
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        db.Keys.Should().BeEmpty();

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_DECRYPT,
                KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE));
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_ForgedInProcessCaller_DeniedAtPlaneArm_NeverProvisions()
    {
        // THE structural plane-arm pin: an in-process caller presenting a foreign ImmediateCaller
        // ("audit") is denied AT THE PLANE ARM â€” it never reaches key selection, so no
        // audit seal key is provisioned or served. In-process identity is forgeable, so the
        // decrypt arm refuses the whole plane.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var provisioned = new List<long>();

        using (var listener = BuildProvisionedListener(provisioned))
        {
            listener.Start();

            var result = await BuildPrivate(db, RequestOrigin.InProcessModule, _AUDIT)
                .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

            result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_NOT_AUTHORIZED);
        }

        db.Keys.Should().BeEmpty("a forged in-process caller never provisions or reaches a key");
        provisioned.Should().NotContain(1L, "no provisioning counter fires on the plane deny");
    }

    // -----------------------------------------------------------------------
    // 3. Self-only is structural â€” the key is selected from the caller, not a target.
    //    Two callers on the SAME (authorized) plane each get their OWN key.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_TwinCallers_EachGetsOwnKey_NeverAnothers()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var auditResult = await BuildPrivate(db, RequestOrigin.CrossProcessHop, _AUDIT)
            .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());
        var filesResult = await BuildPrivate(db, RequestOrigin.CrossProcessHop, _FILES)
            .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

        auditResult.Success.Should().BeTrue();
        filesResult.Success.Should().BeTrue();

        // The domain is selected from the caller â€” audit â†’ seal:audit, files â†’ seal:files.
        db.Keys.Select(k => k.KeyDomain).Should().BeEquivalentTo(
            new[] { "seal:audit", "seal:files" });

        // Neither caller ever receives the other's kid.
        filesResult.Data!.ActiveKid.Should().NotBe(
            auditResult.Data!.ActiveKid, "each caller gets its OWN key, never another service's");

        var auditKid = db.Keys.Single(k => k.KeyDomain == "seal:audit").Kid;
        var filesKid = db.Keys.Single(k => k.KeyDomain == "seal:files").Kid;
        auditResult.Data!.ActiveKid.Should().Be(auditKid);
        filesResult.Data!.ActiveKid.Should().Be(filesKid);
    }

    // -----------------------------------------------------------------------
    // 4. Fail-closed peer + scope gate
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_CrossProcessNoCaller_Denied_IdentityAbsent(string? caller)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var tags = new List<(string Capability, string Reason)>();

        using (var listener = BuildAuthorityListener(tags))
        {
            listener.Start();

            var result = await BuildPrivate(db, RequestOrigin.CrossProcessHop, caller)
                .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        db.Keys.Should().BeEmpty();

        tags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_DECRYPT,
                KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT));
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_WithoutRequiredScope_ReturnsForbidden()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPrivate(
                db,
                RequestOrigin.CrossProcessHop,
                _AUDIT,
                scopes: new HashSet<string>(StringComparer.Ordinal))
            .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "the internal.kc.seal.open scope gate is fail-closed");
        db.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_WrongScope_ReturnsForbidden()
    {
        // Holding the SEAL ENCRYPT scope does not satisfy the SEAL OPEN requirement.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await BuildPrivate(
                db,
                RequestOrigin.CrossProcessHop,
                _AUDIT,
                scopes: new HashSet<string>(StringComparer.Ordinal)
                {
                    ProductScopes.Internal.Kc.Seal.Encrypt,
                })
            .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        db.Keys.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // 5. Lazy provisioning + root-unwrap correctness
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_FirstRequest_ProvisionsAndServesUnwrappedPrivateKey()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetOrLazyProvisionOwnSealPrivateKeyHandler>();
        var provisioned = new List<long>();

        GetOrLazyProvisionOwnSealPrivateKeyOutput served;

        using (var listener = BuildProvisionedListener(provisioned))
        {
            listener.Start();

            var result = await BuildPrivate(db, RequestOrigin.CrossProcessHop, _AUDIT, logger)
                .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

            result.Success.Should().BeTrue();
            served = result.Data!;
        }

        var row = db.Keys.Should().ContainSingle().Which;
        row.KeyDomain.Should().Be("seal:audit", "the domain is selected from the caller identity");
        row.KeyType.Should().Be(KeyType.EcdhSealing);
        row.Status.Should().Be(KeyStatus.Active);

        served.ActiveKid.Should().Be(row.Kid);
        served.Entries.Should().ContainSingle();

        // Root-unwrap ran: the served bytes import as a real P-256 ECDH private key (the
        // stored KeyMaterialEncrypted is root-wrapped + longer + not importable directly).
        var pkcs8 = served.Entries[0].PrivatePkcs8;
        pkcs8.Should().NotBeNullOrEmpty();
        var importAct = () =>
        {
            using var ecdh = ECDiffieHellman.Create();
            ecdh.ImportPkcs8PrivateKey(pkcs8, out _);
        };
        importAct.Should().NotThrow("the served private key is a valid root-unwrapped P-256 PKCS#8");

        pkcs8.Should().NotEqual(
            row.KeyMaterialEncrypted, "the served bytes are UNWRAPPED, not the stored ciphertext");

        provisioned.Should().Contain(1L);

        logger.Entries.Should().Contain(e => e.EventId.Id == 9516);
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_LivePendingNoActive_Returns503Unavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetOrLazyProvisionOwnSealPrivateKeyHandler>();

        await SeedSealKey(db, "seal:audit", KeyStatus.Pending);

        var result = await BuildPrivate(db, RequestOrigin.CrossProcessHop, _AUDIT, logger)
            .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE);
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        db.Keys.Should().ContainSingle("no second key was provisioned while a Pending exists");
        logger.Entries.Should().Contain(e => e.EventId.Id == 9517);
    }

    // -----------------------------------------------------------------------
    // 6. Redaction â€” private PKCS#8 is SecretInformation and never appears in logs
    // -----------------------------------------------------------------------

    [Fact]
    public void SealPrivateEntry_PrivatePkcs8_IsRedactedAsSecretInformation()
    {
        var attribute = typeof(SealPrivateEntry)
            .GetProperty(nameof(SealPrivateEntry.PrivatePkcs8))!
            .GetCustomAttribute<RedactDataAttribute>();

        // The private sealing key MUST carry a redaction marker â€” it is secret material.
        Assert.NotNull(attribute);
        attribute.Reason.Should().Be(
            RedactReason.SecretInformation,
            "a private key is a SECRET, never classified as PersonalInformation");
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_PrivateKeyBytes_NeverAppearInLogs()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetOrLazyProvisionOwnSealPrivateKeyHandler>();

        var result = await BuildPrivate(db, RequestOrigin.CrossProcessHop, _AUDIT, logger)
            .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

        result.Success.Should().BeTrue();
        var pkcs8 = result.Data!.Entries[0].PrivatePkcs8;

        var hex = Convert.ToHexString(pkcs8);
        var base64 = Convert.ToBase64String(pkcs8);

        logger.Entries.Should().OnlyContain(
            e => !e.Message.Contains(hex, StringComparison.OrdinalIgnoreCase)
                && !e.Message.Contains(base64, StringComparison.Ordinal),
            "no handler log line ever renders the unwrapped private key material");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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

    private Task<string> SeedSealKey(
        KeyCustodianTestDbContext db,
        string domain,
        KeyStatus status) =>
        KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            domain,
            KeyType.EcdhSealing,
            status,
            KcAppTestKit.SR_BaseInstant);

    private GetOrLazyProvisionOwnSealPrivateKeyHandler BuildPrivate(
        KeyCustodianTestDbContext db,
        RequestOrigin origin,
        string? caller,
        ILogger<GetOrLazyProvisionOwnSealPrivateKeyHandler>? logger = null,
        IReadOnlySet<string>? scopes = null)
    {
        var grantedScopes = scopes
            ?? new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Seal.Open };

        var ctx = new HandlerContext<GetOrLazyProvisionOwnSealPrivateKeyHandler>(
            new MutableRequestContext
            {
                Origin = origin,
                ImmediateCaller = caller,
                Scopes = grantedScopes,
            },
            logger ?? NullLogger<GetOrLazyProvisionOwnSealPrivateKeyHandler>.Instance);

        return new GetOrLazyProvisionOwnSealPrivateKeyHandler(
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
