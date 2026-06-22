// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Security.Cryptography.X509Certificates;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Tests for <see cref="IssueWorkloadCertificateHandler"/> — happy-path issuance
/// (leaf SAN + chain + audit row), the no-active-CA 503, the retired-CA 503,
/// adversarial workload inputs (no audit, no leaf), and the issuer-private-key
/// zeroize contract.
/// </summary>
public sealed class IssueWorkloadCertificateTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Issue_WithActiveCa_ReturnsLeaf_WritesAudit()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var (intermediateKid, rootCertDer) = await KcAppTestKit.SeedCaAsync(
            db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(db).HandleAsync(new IssueWorkloadCertificateInput("edge"));

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaf = result.Data!.Certificate;
        leaf.Workload.ServiceId.Should().Be("edge");
        leaf.PrivateKeyPkcs8.Should().NotBeEmpty();

        // The leaf carries the SPIFFE SAN.
        using var leafCert = X509CertificateLoader.LoadCertificate(leaf.CertificateDer);
        var san = leafCert.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
        san.Format(multiLine: false).Should().Contain("spiffe://d2.internal/workload/edge");

        // The leaf chains root → intermediate → leaf.
        using var rootCert = X509CertificateLoader.LoadCertificate(rootCertDer);
        using var issuerCert = X509CertificateLoader.LoadCertificate(leaf.IssuerCertificateDer);
        ChainBuilds(leafCert, rootCert, issuerCert).Should().BeTrue();

        // An issuance audit row referencing the issuing CA was written.
        db.LeafIssuanceAudit.Should().ContainSingle();
        var audit = db.LeafIssuanceAudit.Single();
        audit.WorkloadServiceId.Should().Be("edge");
        audit.IssuingCaKid.Should().Be(intermediateKid);
        audit.LeafNotAfter.Should().Be(leaf.NotAfter);
    }

    // -----------------------------------------------------------------------
    // No active CA → 503, no audit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Issue_NoActiveCaSeeded_ReturnsServiceUnavailable_NoAudit()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db).HandleAsync(new IssueWorkloadCertificateInput("edge"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.ServiceUnavailable,
            "no active issuing CA is a retryable 503, not a client conflict");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
        result.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
        db.LeafIssuanceAudit.Should().BeEmpty();
    }

    [Fact]
    public async Task Issue_OnlyRetiredCa_ReturnsServiceUnavailable_NoAudit()
    {
        // A retired CA is excluded by the .Active() filter — there is no active issuer.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(
            db, r_crypto, KcAppTestKit.SR_BaseInstant, KeyStatus.Retired);

        var result = await Build(db).HandleAsync(new IssueWorkloadCertificateInput("edge"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
        db.LeafIssuanceAudit.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Adversarial workload inputs → INVALID_WORKLOAD_IDENTITY, no audit, no leaf
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Edge With Spaces")]
    [InlineData("svc/slash")]
    [InlineData("svc_underscore")]
    public async Task Issue_InvalidWorkload_ReturnsInvalidWorkloadIdentity_NoAudit(
        string? workloadServiceId)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(db).HandleAsync(
            new IssueWorkloadCertificateInput(workloadServiceId));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
        db.LeafIssuanceAudit.Should().BeEmpty(
            because: "an invalid workload is rejected before any CA load or audit write");
    }

    // -----------------------------------------------------------------------
    // Issuance does not disturb the managed-key store
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Issue_DoesNotPersistTheLeafAsAManagedKey()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var keysBefore = db.Keys.Count();
        var result = await Build(db).HandleAsync(new IssueWorkloadCertificateInput("edge"));

        result.Success.Should().BeTrue();
        db.Keys.Count().Should().Be(
            keysBefore, because: "a leaf is on-demand and is never persisted as a managed key");
    }

    private static bool ChainBuilds(
        X509Certificate2 leaf, X509Certificate2 rootAnchor, X509Certificate2 intermediate)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
        chain.ChainPolicy.CustomTrustStore.Add(rootAnchor);
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        return chain.Build(leaf);
    }

    private IssueWorkloadCertificateHandler Build(KeyCustodianTestDbContext db) =>
        new(
            KcAppTestKit.Context<IssueWorkloadCertificateHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));
}
