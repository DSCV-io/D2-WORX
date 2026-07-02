// -----------------------------------------------------------------------
// <copyright file="JwtSigningCapabilityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.Clients;
using D2.Shared.Context.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Tests for the dedicated JWT-minter capability <see cref="JwtSigningCapability"/>: it
/// signs the active <c>jwks-signing</c> key ONLY from the in-process-module plane (the
/// signature verifies against the published JWK), and denies every other established
/// origin (the minter rule is the sole authority — possession plus plane).
/// </summary>
public sealed class JwtSigningCapabilityTests
{
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";

    private static readonly byte[] sr_input = "header.payload"u8.ToArray();
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task SignJwt_InProcessModule_SignsJwksSigning_VerifiesAgainstPublishedJwk()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var kid = await SeedJwksSigningKey(db);

        var result = await Build(db, RequestOrigin.InProcessModule)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

        result.Success.Should().BeTrue();
        result.Data!.Kid.Should().Be(kid, "the minter signs with the active jwks-signing key");

        // Reconstruct the verifier from the PUBLISHED JWK (n/e), exactly as a cluster
        // consumer would, and confirm the signature verifies.
        var spki = db.Keys.Single(k => k.Kid == kid).PublicKeyMaterial!;
        var jwk = JwkProjection.ToJwk(kid, spki);

        using var verifier = RSA.Create();
        verifier.ImportParameters(new RSAParameters
        {
            Modulus = Base64Url.DecodeFromChars(jwk.N),
            Exponent = Base64Url.DecodeFromChars(jwk.E),
        });

        var signature = Base64Url.DecodeFromChars(result.Data!.Signature);
        verifier.VerifyData(sr_input, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue("the minter's signature verifies against the published JWK");
    }

    [Theory]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.System)]
    public async Task SignJwt_NonInProcessOrigin_DeniedForbidden(RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedJwksSigningKey(db);

        var result = await Build(db, origin)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "only the in-process-module plane may reach the cluster-signing root");
    }

    [Fact]
    public async Task SignJwt_UnestablishedOrigin_ReturnsRequestOriginUnestablished()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedJwksSigningKey(db);

        var result = await Build(db, RequestOrigin.Unestablished)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
    }

    [Fact]
    public async Task SignJwt_NoActiveKey_ReturnsSigningKeyUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.InProcessModule)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE);
    }

    [Fact]
    public async Task SignJwt_EmptySigningInput_ReturnsEmptySigningInput()
    {
        // The shared signing core rejects a zero-length payload before any key load or
        // crypto — proven through the minter, the surface that reaches the core.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedJwksSigningKey(db);

        var result = await Build(db, RequestOrigin.InProcessModule)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, []));

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_EMPTY_SIGNING_INPUT);
    }

    [Fact]
    public async Task SignJwt_CapSizedInput_Signs()
    {
        // A signing input exactly at the 16 KiB cap is accepted and signs — the cap is a
        // ceiling, not an off-by-one exclusive bound. Proven through the minter, the surface
        // that reaches the shared core where the cap lives.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedJwksSigningKey(db);
        var input = new byte[16 * 1024];

        var result = await Build(db, RequestOrigin.InProcessModule)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, input));

        result.Success.Should().BeTrue("a 16 KiB signing input is exactly at the cap");
    }

    [Fact]
    public async Task SignJwt_OverCapInput_ReturnsSigningInputTooLarge()
    {
        // One byte over the 16 KiB cap → permanent 400, rejected in the shared signing core
        // before any key load or crypto (asserted via the emitted constant, §26.21) — both
        // the general sign surface and this minter inherit the cap.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedJwksSigningKey(db);
        var input = new byte[(16 * 1024) + 1];

        var result = await Build(db, RequestOrigin.InProcessModule)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, input));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE);
    }

    [Fact]
    public async Task SignJwt_CorruptKeyMaterial_ReturnsPreconditionViolated()
    {
        // Real-wrapped but cryptographically corrupt material — decrypts cleanly then
        // fails PKCS#8 import in the signing rule → flagged 500 (no throw), proven
        // through the minter, the surface that reaches the shared core.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedKeyWithCorruptMaterialAsync(
            db,
            r_crypto,
            KeyDomain.JWKS_SIGNING,
            KeyType.RsaSigning,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant,
            corruptPlaintext: [0x01, 0x02, 0x03, 0x04],
            activatedAt: KcAppTestKit.SR_BaseInstant);

        var result = await Build(db, RequestOrigin.InProcessModule)
            .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
    }

    [Fact]
    public async Task SignJwt_NullInput_Throws()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var capability = Build(db, RequestOrigin.InProcessModule);

        var act = async () => await capability.SignJwtAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SignJwt_UnestablishedOrigin_FiresOriginUnestablishedTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedJwksSigningKey(db);
        var logger = new CapturingLogger<JwtSigningCapability>();

        var authorityTags = new List<(string Capability, string Reason)>();

        using (var listener = BuildListener(authorityTags))
        {
            listener.Start();

            var result = await Build(db, RequestOrigin.Unestablished, logger)
                .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
        }

        authorityTags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED),
            "a silent minter deny leaves the highest-stakes authority event untraced");
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.SIGN, StringComparison.Ordinal)
                && e.Message.Contains("jwks-signing", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.System)]
    public async Task SignJwt_NonInProcessOrigin_FiresNotInProcessTelemetry(RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await SeedJwksSigningKey(db);
        var logger = new CapturingLogger<JwtSigningCapability>();

        var authorityTags = new List<(string Capability, string Reason)>();

        using (var listener = BuildListener(authorityTags))
        {
            listener.Start();

            var result = await Build(db, origin, logger)
                .SignJwtAsync(new SignInput(KeyDomain.JWKS_SIGNING, sr_input));

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        authorityTags.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_PROCESS),
            "an established-but-wrong-plane minter invocation must surface a bounded reason");
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9512
                && e.Message.Contains(
                    KeyCustodianMetrics.AuthorityRejections.Capability.SIGN, StringComparison.Ordinal)
                && e.Message.Contains("jwks-signing", StringComparison.Ordinal));
    }

    private static MeterListener BuildListener(List<(string Capability, string Reason)> authorityTags)
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

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
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

    private async Task<string> SeedJwksSigningKey(KeyCustodianTestDbContext db) =>
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.JWKS_SIGNING,
            KeyType.RsaSigning,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant,
            activatedAt: KcAppTestKit.SR_BaseInstant);

    private JwtSigningCapability Build(
        KeyCustodianTestDbContext db,
        RequestOrigin origin,
        ILogger<JwtSigningCapability>? logger = null) =>
        new(
            db,
            r_crypto,
            new MutableRequestContext { Origin = origin },
            logger ?? NullLogger<JwtSigningCapability>.Instance);

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
