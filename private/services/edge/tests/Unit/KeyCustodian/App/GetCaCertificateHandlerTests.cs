// -----------------------------------------------------------------------
// <copyright file="GetCaCertificateHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography.X509Certificates;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Private.Auth;
using D2.Shared.Auth.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// The real <see cref="GetCaCertificateHandler"/> matrix: a seeded two-tier CA
/// serves the root + intermediate DER (both parse; the intermediate chains to the
/// root); a missing / partial / malformed tier is the retryable 503 with the
/// unavailable telemetry (counter + 9514); the deny matrix (unestablished /
/// unserved plane / identity-absent) fires the ca-cert capability telemetry; the
/// scope gate runs before the rule; and the output surface is structurally free of
/// private material.
/// </summary>
public sealed class GetCaCertificateHandlerTests
{
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";
    private const string _NO_ACTIVE_CA = "d2.keycustodian.no_active_issuing_ca";

    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // Happy path â€” both tiers seeded
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public async Task GetCaCertificate_SeededCa_ReturnsChain_OnBothServedPlanes(
        RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var (_, _, rootDer) = await KcAppTestKit.SeedCaHierarchyAsync(
            db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(db, origin, "files")
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.Success.Should().BeTrue();
        var output = result.Data!;

        // Both tiers parse as X.509, the root round-trips the seeded anchor, and
        // the intermediate chains to it.
        using var root = X509CertificateLoader.LoadCertificate(output.RootCertificateDer);
        using var intermediate = X509CertificateLoader.LoadCertificate(
            output.IntermediateCertificateDer);

        output.RootCertificateDer.Should().Equal(rootDer);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.Build(intermediate).Should().BeTrue(
            "the served intermediate chains to the served root");
    }

    [Fact]
    public void Output_HasNoPrivateMaterialMember_Structural()
    {
        typeof(D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput).GetProperties()
            .Should().NotContain(
                p => p.Name.Contains("PrivateKey")
                    || p.Name.Contains("Pkcs8")
                    || p.Name.Contains("KeyMaterial"),
                "the chain fetch serves public certificates only");
    }

    // -----------------------------------------------------------------------
    // 503 â€” missing / partial tiers
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCaCertificate_NothingSeeded_503_WithTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetCaCertificateHandler>();

        var (noCaTotal, listener) = CounterListener(_NO_ACTIVE_CA);

        using (listener)
        {
            var result = await Build(db, RequestOrigin.CrossProcessHop, "files", logger: logger)
                .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
        }

        noCaTotal.Should().Contain(1);
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9514, "the ca-cert 503 has its own forensic delegate");
    }

    [Fact]
    public async Task GetCaCertificate_RootOnly_NoIntermediate_503()
    {
        // A partial chain is not "the chain" â€” root present, intermediate missing.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files")
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task GetCaCertificate_IntermediateOnly_NoRoot_503()
    {
        // The inverse partial chain â€” intermediate present, root missing.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files")
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task GetCaCertificate_InactiveTiers_503()
    {
        // Rows exist but neither tier is ACTIVE â€” still the retryable 503.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaRootAsync(
            db, r_crypto, KcAppTestKit.SR_BaseInstant, KeyStatus.Retired);
        await KcAppTestKit.SeedCaAsync(
            db, r_crypto, KcAppTestKit.SR_BaseInstant, KeyStatus.Retired);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files")
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetCaCertificate_ActiveRootWrongKeyType_503()
    {
        // A malformed ACTIVE row in the root domain â€” an active row whose KeyType is
        // NOT X509CaCertificate is corruption, treated as the tier being absent (503).
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildOptions(),
            KeyDomain.MTLS_CA_ROOT,
            KeyType.AesPayload,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files")
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task GetCaCertificate_ActiveIntermediateNullCertificateMaterial_503()
    {
        // Root tier valid; the intermediate's ACTIVE row is a CA-typed key carrying
        // NULL certificate material (corruption) â€” the malformed-active-row guard
        // treats the tier as absent (503).
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var record = new KeyRecord
        {
            Kid = KidMinting.Mint(),
            KeyDomain = KeyDomain.MTLS_CA_INTERMEDIATE,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = r_crypto.Encrypt(new byte[32]),
            PublicKeyMaterial = null,
            CaCertificate = null,
            CreatedAt = KcAppTestKit.SR_BaseInstant,
            Status = KeyStatus.Active,
            ActivatedAt = KcAppTestKit.SR_BaseInstant,
        };

        db.Keys.Add(record);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await Build(db, RequestOrigin.CrossProcessHop, "files")
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    // -----------------------------------------------------------------------
    // Deny matrix â€” with the ca-cert capability telemetry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCaCertificate_Unestablished_Denied_First_WithTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<GetCaCertificateHandler>();

        var (tags, listener) = AuthorityListener();

        using (listener)
        {
            var result = await Build(db, RequestOrigin.Unestablished, "files", logger: logger)
                .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
        }

        tags.Should().Contain((
            KeyCustodianMetrics.AuthorityRejections.Capability.CA_CERT,
            KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED));
        logger.Entries.Should().Contain(e => e.EventId.Id == 9512);
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.System)]
    public async Task GetCaCertificate_UnservedPlane_Denied_WithTelemetry(RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var (tags, listener) = AuthorityListener();

        using (listener)
        {
            var result = await Build(db, origin, "files")
                .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED,
                "the internal trust anchor never rides an unserved plane");
        }

        tags.Should().Contain((
            KeyCustodianMetrics.AuthorityRejections.Capability.CA_CERT,
            KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE));
    }

    [Fact]
    public async Task GetCaCertificate_IdentityAbsent_Denied_WithTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var (tags, listener) = AuthorityListener();

        using (listener)
        {
            var result = await Build(db, RequestOrigin.CrossProcessHop, caller: null)
                .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        tags.Should().Contain((
            KeyCustodianMetrics.AuthorityRejections.Capability.CA_CERT,
            KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT));
    }

    [Fact]
    public async Task GetCaCertificate_MissingScope_Forbidden_BeforeTheRule()
    {
        // No internal.kc.cacert scope â†’ the BaseHandler scope gate fires first; the
        // absence of the CA_CERTIFICATE_NOT_AUTHORIZED code (this context would
        // otherwise be fully authorized) proves the rule never ran.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "files",
                scopes: new HashSet<string>(StringComparer.Ordinal))
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the in-process internal.kc.cacert scope gate is fail-closed");
        result.ErrorCode.Should().NotBe(
            KeyCustodianErrorCodes.KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED,
            "the scope gate fires before the authority rule");
    }

    [Fact]
    public async Task GetCaCertificate_UnauthorizedWithNoCa_403_Not503_NoCaStateOracle()
    {
        // Authority precedes the store reads â€” an unauthorized caller learns
        // nothing about CA state.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.EdgeInbound, "files")
            .HandleAsync(new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput());

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (ConcurrentBag<(string Capability, string Reason)> Tags, MeterListener Listener)
        AuthorityListener()
    {
        var tags = new ConcurrentBag<(string, string)>();
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

        listener.Start();
        return (tags, listener);
    }

    private static (ConcurrentBag<long> Values, MeterListener Listener) CounterListener(
        string instrumentName)
    {
        var values = new ConcurrentBag<long>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && instrument.Name == instrumentName)
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((_, value, _, _) => values.Add(value));
        listener.Start();
        return (values, listener);
    }

    private static GetCaCertificateHandler Build(
        KeyCustodianTestDbContext db,
        RequestOrigin origin,
        string? caller,
        IReadOnlySet<string>? scopes = null,
        ILogger<GetCaCertificateHandler>? logger = null)
    {
        // Default to the required internal.kc.cacert scope so the BaseHandler
        // ScopeRequirement gate admits the call; pass an explicit set to exercise
        // the gate itself.
        var grantedScopes = scopes
            ?? new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Cacert };

        return new GetCaCertificateHandler(
            KcAppTestKit.ContextWithOriginAndCaller(origin, caller, grantedScopes, logger),
            db);
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
