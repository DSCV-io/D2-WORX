// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateIssuanceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Tests for the pure <see cref="WorkloadCertificateIssuance"/> rule — real BCL
/// crypto. Asserts the leaf is signed by the intermediate (full chain builds), the
/// SPIFFE SAN, the client-auth + server-auth EKU, the digital-signature key usage,
/// that the leaf is NOT a CA, the validity window, and that the rule never throws.
/// </summary>
public sealed class WorkloadCertificateIssuanceTests
{
    private const string _CLIENT_AUTH_OID = "1.3.6.1.5.5.7.3.2";
    private const string _SERVER_AUTH_OID = "1.3.6.1.5.5.7.3.1";

    private static readonly Instant sr_now = Instant.FromUtc(2026, 1, 1, 0, 0);

    // -----------------------------------------------------------------------
    // IssueLeaf — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void IssueLeaf_Valid_LeafChainsRootIntermediateLeaf_WithSpiffeSan()
    {
        var (rootCert, intermediateCert, intermediateKey) = BuildCa();

        try
        {
            var workload = WorkloadIdentity.FromTrusted("edge");

            var result = WorkloadCertificateIssuance.IssueLeaf(
                workload,
                intermediateCert,
                intermediateKey,
                Duration.FromHours(24),
                new TestClock(sr_now));

            result.Success.Should().BeTrue();
            using var leaf = X509CertificateLoader.LoadCertificate(result.Data!.CertificateDer);

            // SAN carries the SPIFFE URI.
            var san = leaf.Extensions
                .OfType<X509SubjectAlternativeNameExtension>()
                .Single();
            san.Format(multiLine: false).Should().Contain("spiffe://d2.internal/workload/edge");

            // EKU: both client-auth and server-auth.
            var eku = leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
            var ekuOids = eku.EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value).ToList();
            ekuOids.Should().Contain(_CLIENT_AUTH_OID);
            ekuOids.Should().Contain(_SERVER_AUTH_OID);

            // Key usage: DigitalSignature ONLY — KeyEncipherment is an RSA key-transport
            // concept that does not apply to ECDSA P-256 under TLS 1.3.
            var usage = leaf.Extensions.OfType<X509KeyUsageExtension>().Single();
            usage.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.DigitalSignature);
            usage.KeyUsages.Should().NotHaveFlag(
                X509KeyUsageFlags.KeyEncipherment,
                because: "KeyEncipherment is an RSA concept and must not appear on ECDSA leaves");

            // A leaf must NOT be a CA.
            var basic = leaf.Extensions.OfType<X509BasicConstraintsExtension>().Single();
            basic.CertificateAuthority.Should().BeFalse(
                because: "a workload leaf must never be able to sign other certificates");

            // Full chain root → intermediate → leaf builds with the root anchor +
            // the intermediate as an extra store cert.
            BuildsChain(leaf, [rootCert], [intermediateCert]).Should().BeTrue();

            // The returned leaf private key is present + zeroizable.
            result.Data!.PrivateKeyPkcs8.Should().NotBeEmpty();
        }
        finally
        {
            rootCert.Dispose();
            intermediateCert.Dispose();
            intermediateKey.Dispose();
        }
    }

    [Fact]
    public void IssueLeaf_HonorsValidityWindow()
    {
        var (rootCert, intermediateCert, intermediateKey) = BuildCa();

        try
        {
            var result = WorkloadCertificateIssuance.IssueLeaf(
                WorkloadIdentity.FromTrusted("files"),
                intermediateCert,
                intermediateKey,
                Duration.FromHours(12),
                new TestClock(sr_now));

            result.Data!.NotBefore.Should().Be(sr_now);
            result.Data!.NotAfter.Should().Be(sr_now + Duration.FromHours(12));
        }
        finally
        {
            rootCert.Dispose();
            intermediateCert.Dispose();
            intermediateKey.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // IssueLeaf — adversarial (never throws → flagged result)
    // -----------------------------------------------------------------------

    [Fact]
    public void IssueLeaf_NullWorkload_ReturnsInvalidCertificateRequest()
    {
        var (rootCert, intermediateCert, intermediateKey) = BuildCa();

        try
        {
            var result = WorkloadCertificateIssuance.IssueLeaf(
                null,
                intermediateCert,
                intermediateKey,
                Duration.FromHours(24),
                new TestClock(sr_now));

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
        }
        finally
        {
            rootCert.Dispose();
            intermediateCert.Dispose();
            intermediateKey.Dispose();
        }
    }

    [Fact]
    public void IssueLeaf_NullIssuerCert_ReturnsInvalidCertificateRequest()
    {
        using var key = ECDsa.Create();
        var result = WorkloadCertificateIssuance.IssueLeaf(
            WorkloadIdentity.FromTrusted("edge"),
            null,
            key,
            Duration.FromHours(24),
            new TestClock(sr_now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IssueLeaf_NonPositiveValidity_ReturnsInvalidCertificateRequest(int hours)
    {
        var (rootCert, intermediateCert, intermediateKey) = BuildCa();

        try
        {
            var result = WorkloadCertificateIssuance.IssueLeaf(
                WorkloadIdentity.FromTrusted("edge"),
                intermediateCert,
                intermediateKey,
                Duration.FromHours(hours),
                new TestClock(sr_now));

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
        }
        finally
        {
            rootCert.Dispose();
            intermediateCert.Dispose();
            intermediateKey.Dispose();
        }
    }

    [Fact]
    public void IssueLeaf_NullClock_ReturnsInvalidCertificateRequest()
    {
        var (rootCert, intermediateCert, intermediateKey) = BuildCa();

        try
        {
            var result = WorkloadCertificateIssuance.IssueLeaf(
                WorkloadIdentity.FromTrusted("edge"),
                intermediateCert,
                intermediateKey,
                Duration.FromHours(24),
                clock: null);

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
        }
        finally
        {
            rootCert.Dispose();
            intermediateCert.Dispose();
            intermediateKey.Dispose();
        }
    }

    private static (
        X509Certificate2 RootCert,
        X509Certificate2 IntermediateCert,
        ECDsa IntermediateKey) BuildCa()
    {
        var clock = new TestClock(sr_now);

        var root = CaCertificateGeneration.GenerateRootCa(
            "D2 Root", Duration.FromDays(3650), clock).Data!;

        using var rootKey = ECDsa.Create();
        rootKey.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);
        var rootCert = X509CertificateLoader.LoadCertificate(root.CertificateDer);

        var intermediate = CaCertificateGeneration.GenerateIntermediateCa(
            "D2 Issuing CA", rootCert, rootKey, Duration.FromDays(365), clock).Data!;

        var intermediateKey = ECDsa.Create();
        intermediateKey.ImportPkcs8PrivateKey(intermediate.PrivateKeyPkcs8, out _);
        var intermediateCert = X509CertificateLoader.LoadCertificate(intermediate.CertificateDer);

        return (rootCert, intermediateCert, intermediateKey);
    }

    private static bool BuildsChain(
        X509Certificate2 leaf, X509Certificate2[] trustAnchors, X509Certificate2[] extraStore)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

        foreach (var anchor in trustAnchors)
            chain.ChainPolicy.CustomTrustStore.Add(anchor);

        foreach (var extra in extraStore)
            chain.ChainPolicy.ExtraStore.Add(extra);

        return chain.Build(leaf);
    }
}
