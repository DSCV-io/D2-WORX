// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateIssuanceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Tests for the pure <see cref="WorkloadCertificateIssuance"/> rule — real BCL
/// crypto over a CALLER-SUPPLIED public key. Asserts the leaf certifies EXACTLY
/// the supplied key (its SubjectPublicKeyInfo round-trips), is signed by the
/// intermediate (full chain builds), carries the supplied identity's SPIFFE SAN,
/// the client-auth + server-auth EKU, the digital-signature key usage, is NOT a
/// CA, honors the validity window, produces NO private-key output anywhere
/// (structural — the rule generates no keypair), and never throws.
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
            var leafPublicKey = BuildLeafPublicKey(out var suppliedSpki);

            var result = WorkloadCertificateIssuance.IssueLeaf(
                workload,
                leafPublicKey,
                intermediateCert,
                intermediateKey,
                Duration.FromHours(24),
                new TestClock(sr_now));

            result.Success.Should().BeTrue();
            using var leaf = X509CertificateLoader.LoadCertificate(result.Data!.CertificateDer);

            // The leaf certifies EXACTLY the supplied public key.
            leaf.PublicKey.ExportSubjectPublicKeyInfo().Should().Equal(
                suppliedSpki,
                "the rule signs the supplied key — it never substitutes one of its own");

            // SAN carries the SPIFFE URI of the SUPPLIED identity.
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
        }
        finally
        {
            rootCert.Dispose();
            intermediateCert.Dispose();
            intermediateKey.Dispose();
        }
    }

    [Fact]
    public void IssueLeaf_ProducesNoPrivateKeyOutput_Structural()
    {
        // The strictly-stronger successor to the returned-PKCS#8 pins: the result
        // type carries NO private-key member at all — the rule generates no keypair,
        // so a private key is unrepresentable in its output.
        typeof(IssuedWorkloadCertificate).GetProperties()
            .Should().NotContain(
                p => p.Name.Contains("PrivateKey") || p.Name.Contains("Pkcs8"),
                "the issuance output is all-public — the leaf key never exists in KeyCustodian");

        typeof(IssuedWorkloadCertificate).GetMethods()
            .Should().NotContain(
                m => m.Name == "Zero",
                "with no private member there is nothing to zero");
    }

    [Fact]
    public void IssueLeaf_HonorsValidityWindow()
    {
        var (rootCert, intermediateCert, intermediateKey) = BuildCa();

        try
        {
            var result = WorkloadCertificateIssuance.IssueLeaf(
                WorkloadIdentity.FromTrusted("files"),
                BuildLeafPublicKey(out _),
                intermediateCert,
                intermediateKey,
                Duration.FromHours(12),
                new TestClock(sr_now));

            // notBefore is front-backdated by the fixed clock-skew allowance (5 min);
            // notAfter = now + validity is unchanged (forward validity never shortened).
            result.Data!.NotBefore.Should().Be(sr_now - Duration.FromMinutes(5));
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
                BuildLeafPublicKey(out _),
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
    public void IssueLeaf_NullLeafPublicKey_ReturnsInvalidCertificateRequest()
    {
        var (rootCert, intermediateCert, intermediateKey) = BuildCa();

        try
        {
            var result = WorkloadCertificateIssuance.IssueLeaf(
                WorkloadIdentity.FromTrusted("edge"),
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
            BuildLeafPublicKey(out _),
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
                BuildLeafPublicKey(out _),
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
                BuildLeafPublicKey(out _),
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

    /// <summary>
    /// Builds the caller-side P-256 public key the rule signs (the shape
    /// <c>CsrVerification</c> extracts from a verified CSR) and surfaces its SPKI
    /// for the pairing assertion.
    /// </summary>
    /// <param name="spki">The SubjectPublicKeyInfo of the built key.</param>
    /// <returns>The public key.</returns>
    private static PublicKey BuildLeafPublicKey(out byte[] spki)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        spki = key.ExportSubjectPublicKeyInfo();

        var request = new CertificateRequest(
            "CN=keyholder", key, HashAlgorithmName.SHA256);
        var loaded = CertificateRequest.LoadSigningRequest(
            request.CreateSigningRequest(), HashAlgorithmName.SHA256);

        return loaded.PublicKey;
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
