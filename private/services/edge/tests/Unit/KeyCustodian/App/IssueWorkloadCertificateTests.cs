// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography.X509Certificates;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Private.Auth;
using D2.Shared.Auth.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// The real <see cref="IssueWorkloadCertificateHandler"/> matrix driven THROUGH the
/// handler over an in-memory DbContext + the interceptor-shaped request context:
/// the fail-closed authority arms (unestablished / non-cross-process plane /
/// absent peer) each deny with NO leaf, NO audit row, an untouched store, and the
/// issuance-capability deny telemetry; the uniform 400 <c>INVALID_CSR</c> covers
/// every CSR failure class; the ordering pins prove authority precedes CSR
/// validation precedes the CA load (no parse / CA-state oracle); the scope gate
/// fires before the rule; and the positive self-issue arm mints a leaf certifying
/// EXACTLY the CSR's key with a SAN that is ALWAYS the authenticated peer â€” a
/// forged CSR subject/SAN never reaches the leaf (the no-forgery invariant).
/// </summary>
public sealed class IssueWorkloadCertificateTests
{
    private const string _AUTHORITY_REJECTIONS = "d2.keycustodian.authority_rejections";
    private const string _LEAVES_ISSUED = "d2.keycustodian.leaf_certificates_issued";
    private const string _NO_ACTIVE_CA = "d2.keycustodian.no_active_issuing_ca";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    public static TheoryData<string> InvalidCsrCases() =>
        ["garbage", "truncated", "oversized", "pop-broken", "rsa", "p384", "empty"];

    // -----------------------------------------------------------------------
    // Authority deny matrix â€” every arm through the REAL handler, with telemetry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Issue_UnestablishedOrigin_Denied_First_WithTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var logger = new CapturingLogger<IssueWorkloadCertificateHandler>();
        var (csr, _) = KcAppTestKit.BuildP256Csr();

        var (tags, listener) = AuthorityListener();

        using (listener)
        {
            var result = await Build(db, RequestOrigin.Unestablished, "edge", logger: logger)
                .HandleAsync(new IssueWorkloadCertificateInput(csr));

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED,
                "the type-zero fail-closed arm runs FIRST");
            AssertNoLeafNoAudit(db, result);
        }

        tags.Should().Contain((
            KeyCustodianMetrics.AuthorityRejections.Capability.ISSUANCE,
            KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED));
        logger.Entries.Should().Contain(e => e.EventId.Id == 9512);
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.InProcessModule)]
    [InlineData(RequestOrigin.System)]
    public async Task Issue_NonCrossProcessPlane_Denied_WithTelemetry(RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var logger = new CapturingLogger<IssueWorkloadCertificateHandler>();
        var (csr, _) = KcAppTestKit.BuildP256Csr();

        var (tags, listener) = AuthorityListener();

        using (listener)
        {
            var result = await Build(db, origin, "edge", logger: logger)
                .HandleAsync(new IssueWorkloadCertificateInput(csr));

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED,
                "issuance is cross-process-only â€” the plane deny is the uniform 403");
            AssertNoLeafNoAudit(db, result);
        }

        tags.Should().Contain((
            KeyCustodianMetrics.AuthorityRejections.Capability.ISSUANCE,
            KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE));
        logger.Entries.Should().Contain(e => e.EventId.Id == 9512);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Issue_CrossProcessNoPeer_Denied_WithTelemetry(string? caller)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var logger = new CapturingLogger<IssueWorkloadCertificateHandler>();
        var (csr, _) = KcAppTestKit.BuildP256Csr();

        var (tags, listener) = AuthorityListener();

        using (listener)
        {
            var result = await Build(db, RequestOrigin.CrossProcessHop, caller, logger: logger)
                .HandleAsync(new IssueWorkloadCertificateInput(csr));

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "a cross-process hop with no authenticated mTLS peer is fail-closed");
            AssertNoLeafNoAudit(db, result);
        }

        tags.Should().Contain((
            KeyCustodianMetrics.AuthorityRejections.Capability.ISSUANCE,
            KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT));
        logger.Entries.Should().Contain(e => e.EventId.Id == 9512);
    }

    [Fact]
    public async Task Issue_MissingScope_Forbidden_BeforeTheRule()
    {
        // No internal.kc.issue scope on the request context â†’ BaseHandler's
        // per-handler ScopeRequirement gate fires BEFORE the authority rule. The
        // request is otherwise fully authorized (cross-process + peer + valid CSR +
        // seeded CA), so a Forbidden here proves the scope gate, and the absence of
        // the ISSUANCE_NOT_AUTHORIZED code proves the rule never ran.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var (csr, _) = KcAppTestKit.BuildP256Csr();

        var result = await Build(
                db,
                RequestOrigin.CrossProcessHop,
                "edge",
                scopes: new HashSet<string>(StringComparer.Ordinal))
            .HandleAsync(new IssueWorkloadCertificateInput(csr));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the in-process internal.kc.issue scope gate is fail-closed");
        AssertNoLeafNoAudit(db, result);
    }

    // -----------------------------------------------------------------------
    // Ordering pins â€” no oracle for unauthenticated probers
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Issue_GarbageCsrFromUnauthorizedContext_403_Not400_NoCsrParseOracle()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.EdgeInbound, "edge")
            .HandleAsync(new IssueWorkloadCertificateInput(
                RandomNumberGenerator.GetBytes(64)));

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().NotBe(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR,
            "authority precedes CSR validation â€” an unauthorized prober learns "
            + "nothing about CSR parsing");
    }

    [Fact]
    public async Task Issue_NoCaAndWrongPlane_403_Not503_NoCaStateOracle()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var (csr, _) = KcAppTestKit.BuildP256Csr();

        var result = await Build(db, RequestOrigin.InProcessModule, "edge")
            .HandleAsync(new IssueWorkloadCertificateInput(csr));

        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "authority precedes the CA load â€” an unauthorized caller learns nothing "
            + "about CA state");
    }

    [Fact]
    public async Task Issue_InvalidCsrWithUnseededCa_400_Not503_CsrValidationPrecedesCaLoad()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.CrossProcessHop, "edge")
            .HandleAsync(new IssueWorkloadCertificateInput(
                RandomNumberGenerator.GetBytes(64)));

        result.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "CSR validation runs before any store access â€” the 400 fires even with "
            + "no CA seeded");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
    }

    // -----------------------------------------------------------------------
    // The uniform 400 INVALID_CSR matrix â€” through the real handler
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(InvalidCsrCases))]
    public async Task Issue_InvalidCsr_Uniform400_NoLeaf_NoAudit(string kind)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var csr = kind switch
        {
            "garbage" => RandomNumberGenerator.GetBytes(256),
            "truncated" => KcAppTestKit.BuildP256Csr().Der.AsSpan(0, 40).ToArray(),
            "oversized" => new byte[CsrVerification.MAX_CSR_DER_BYTES + 1],
            "pop-broken" => KcAppTestKit.BuildPopBrokenCsr(),
            "rsa" => KcAppTestKit.BuildRsaCsr(),
            "p384" => KcAppTestKit.BuildP384Csr(),
            _ => [],
        };

        var result = await Build(db, RequestOrigin.CrossProcessHop, "edge")
            .HandleAsync(new IssueWorkloadCertificateInput(csr));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR,
            "every CSR failure class folds into the one coarse 400 â€” no "
            + "which-check-failed leak");
        AssertNoLeafNoAudit(db, result);
    }

    // -----------------------------------------------------------------------
    // The 503 no-active-CA arm â€” authorized caller, valid CSR, no intermediate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Issue_AuthorizedValidCsr_NoActiveCa_503_WithTelemetry()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var logger = new CapturingLogger<IssueWorkloadCertificateHandler>();
        var (csr, _) = KcAppTestKit.BuildP256Csr();

        var (noCaTotal, listener) = CounterListener(_NO_ACTIVE_CA);

        using (listener)
        {
            var result = await Build(db, RequestOrigin.CrossProcessHop, "edge", logger: logger)
                .HandleAsync(new IssueWorkloadCertificateInput(csr));

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
            AssertNoLeafNoAudit(db, result);
        }

        noCaTotal.Should().Contain(1);
        logger.Entries.Should().Contain(e => e.EventId.Id == 9507);
    }

    // -----------------------------------------------------------------------
    // The positive self-issue arm + the no-forgery invariant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Issue_AuthorizedValidCsr_MintsLeaf_KeyPairsSanFromPeer_AuditWritten()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var (intermediateKid, rootDer) = await KcAppTestKit.SeedCaAsync(
            db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var logger = new CapturingLogger<IssueWorkloadCertificateHandler>();
        var (csr, csrSpki) = KcAppTestKit.BuildP256Csr();

        var (issuedTotal, listener) = CounterListener(_LEAVES_ISSUED);

        using (listener)
        {
            var result = await Build(db, RequestOrigin.CrossProcessHop, "edge", logger: logger)
                .HandleAsync(new IssueWorkloadCertificateInput(csr));

            result.Success.Should().BeTrue();
            var issued = result.Data!.Certificate;

            using var leaf = X509CertificateLoader.LoadCertificate(issued.CertificateDer);

            // The leaf certifies EXACTLY the CSR's key.
            leaf.PublicKey.ExportSubjectPublicKeyInfo().Should().Equal(
                csrSpki, "the leaf pairs with the caller-generated key");

            // The SAN is the authenticated PEER identity.
            leaf.Extensions
                .OfType<X509SubjectAlternativeNameExtension>()
                .Single()
                .Format(multiLine: false)
                .Should().Contain("spiffe://d2.internal/workload/edge");

            // The chain builds: root anchor â†’ issuing intermediate â†’ leaf.
            using var root = X509CertificateLoader.LoadCertificate(rootDer);
            using var intermediate = X509CertificateLoader.LoadCertificate(
                issued.IssuerCertificateDer);
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.ExtraStore.Add(intermediate);
            chain.Build(leaf).Should().BeTrue("the leaf chains to the fetched trust anchor");

            // The audit row is the single write on the leaf path.
            db.LeafIssuanceAudit.Should().ContainSingle()
                .Which.IssuingCaKid.Should().Be(intermediateKid);
        }

        issuedTotal.Should().Contain(1);
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 9515, "the issuance forensic log complements the audit row");
    }

    [Fact]
    public async Task Issue_ForgedCsrSanAndSubject_LeafSanIsStillThePeer_NoForgery()
    {
        // THE no-forgery invariant: a proof-of-possession-VALID CSR claiming a
        // DIFFERENT identity (subject CN=files + SAN spiffe://â€¦/files) still yields
        // a leaf whose SAN is the AUTHENTICATED peer ("edge") â€” nothing from the
        // CSR except its public key reaches the leaf.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var (forgedCsr, csrSpki) = KcAppTestKit.BuildP256CsrWithForgedSan("files");

        var result = await Build(db, RequestOrigin.CrossProcessHop, "edge")
            .HandleAsync(new IssueWorkloadCertificateInput(forgedCsr));

        result.Success.Should().BeTrue(
            "a PoP-valid P-256 CSR is acceptable regardless of its subject â€” the "
            + "subject is structurally inert");

        using var leaf = X509CertificateLoader.LoadCertificate(
            result.Data!.Certificate.CertificateDer);

        var san = leaf.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .Single()
            .Format(multiLine: false);
        san.Should().Contain(
            "spiffe://d2.internal/workload/edge",
            "the SAN is ALWAYS the authenticated peer");
        san.Should().NotContain(
            "spiffe://d2.internal/workload/files",
            "the forged SAN never reaches the leaf");

        leaf.SubjectName.Name.Should().Contain(
            "CN=edge", "the subject too comes from the peer, never the CSR");
        leaf.PublicKey.ExportSubjectPublicKeyInfo().Should().Equal(
            csrSpki, "only the CSR's public key survives into the leaf");
    }

    [Fact]
    public void Output_HasNoPrivateKeyMember_Structural()
    {
        // The wire-facing output wraps the all-public VO â€” no private-key member is
        // representable anywhere on the issuance output surface.
        typeof(IssueWorkloadCertificateOutput).GetProperties()
            .Should().ContainSingle(p => p.Name == "Certificate");
        typeof(IssuedWorkloadCertificate).GetProperties()
            .Should().NotContain(
                p => p.Name.Contains("PrivateKey") || p.Name.Contains("Pkcs8"));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void AssertNoLeafNoAudit(
        KeyCustodianTestDbContext db,
        D2Result<IssueWorkloadCertificateOutput?> result)
    {
        result.Data.Should().BeNull("no leaf may be minted on a rejected request");
        db.LeafIssuanceAudit.Should().BeEmpty("a rejected issuance writes no audit row");
    }

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

    private IssueWorkloadCertificateHandler Build(
        KeyCustodianTestDbContext db,
        RequestOrigin origin,
        string? caller,
        IReadOnlySet<string>? scopes = null,
        ILogger<IssueWorkloadCertificateHandler>? logger = null)
    {
        // Default to the required internal.kc.issue scope so the BaseHandler
        // ScopeRequirement gate admits the call; pass an explicit set to exercise
        // the gate itself.
        var grantedScopes = scopes
            ?? new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Issue };

        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        return new IssueWorkloadCertificateHandler(
            KcAppTestKit.ContextWithOriginAndCaller(origin, caller, grantedScopes, logger),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            new CaLeafSigningCapability(db, r_crypto, clock),
            clock);
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
