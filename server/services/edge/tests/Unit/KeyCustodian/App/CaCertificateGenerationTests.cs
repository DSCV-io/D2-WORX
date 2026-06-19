// -----------------------------------------------------------------------
// <copyright file="CaCertificateGenerationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Tests for the pure <see cref="CaCertificateGeneration"/> rule — real BCL crypto,
/// fast + deterministic. Asserts the self-signed root + intermediate-signed-by-root
/// hierarchy, ECDSA P-256 / SHA-256, the correct basic-constraints / key-usage, the
/// validity windows, the chain builds, and that the rule never throws (a crypto
/// failure becomes a flagged <c>D2Result</c>).
/// </summary>
public sealed class CaCertificateGenerationTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 1, 1, 0, 0);

    // -----------------------------------------------------------------------
    // GenerateRootCa — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void GenerateRootCa_Valid_ProducesSelfSignedP256CaCert()
    {
        var result = CaCertificateGeneration.GenerateRootCa(
            "D2 Internal Root CA", Duration.FromDays(3650), new TestClock(sr_now));

        result.Success.Should().BeTrue();
        using var cert = X509CertificateLoader.LoadCertificate(result.Data!.CertificateDer);

        // Self-signed: subject == issuer.
        cert.SubjectName.Name.Should().Be(cert.IssuerName.Name);
        cert.SubjectName.Name.Should().Contain("D2 Internal Root CA");

        // ECDSA P-256.
        cert.PublicKey.Oid.Value.Should().Be("1.2.840.10045.2.1"); // id-ecPublicKey
        using var pub = cert.GetECDsaPublicKey();
        pub.Should().NotBeNull();
        pub.KeySize.Should().Be(256);

        // SHA-256 signature.
        cert.SignatureAlgorithm.Value.Should().Be("1.2.840.10045.4.3.2"); // ecdsa-with-SHA256

        // CA with path length 1.
        var basic = cert.Extensions.OfType<X509BasicConstraintsExtension>().Single();
        basic.CertificateAuthority.Should().BeTrue();
        basic.HasPathLengthConstraint.Should().BeTrue();
        basic.PathLengthConstraint.Should().Be(1);

        // KeyCertSign present.
        var usage = cert.Extensions.OfType<X509KeyUsageExtension>().Single();
        usage.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.KeyCertSign);
    }

    [Fact]
    public void GenerateRootCa_HonorsValidityWindow()
    {
        var result = CaCertificateGeneration.GenerateRootCa(
            "Root", Duration.FromDays(10), new TestClock(sr_now));

        using var cert = X509CertificateLoader.LoadCertificate(result.Data!.CertificateDer);
        cert.NotBefore.ToUniversalTime().Should().BeCloseTo(
            sr_now.ToDateTimeUtc(), TimeSpan.FromSeconds(2));
        cert.NotAfter.ToUniversalTime().Should().BeCloseTo(
            sr_now.ToDateTimeUtc().AddDays(10), TimeSpan.FromSeconds(2));
    }

    // -----------------------------------------------------------------------
    // GenerateRootCa — adversarial (never throws → flagged result)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateRootCa_EmptySubject_ReturnsInvalidCertificateRequest(string? subject)
    {
        var result = CaCertificateGeneration.GenerateRootCa(
            subject, Duration.FromDays(3650), new TestClock(sr_now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerateRootCa_NonPositiveValidity_ReturnsInvalidCertificateRequest(int days)
    {
        var result = CaCertificateGeneration.GenerateRootCa(
            "Root", Duration.FromDays(days), new TestClock(sr_now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    [Fact]
    public void GenerateRootCa_NullClock_ReturnsInvalidCertificateRequest()
    {
        var result = CaCertificateGeneration.GenerateRootCa(
            "Root", Duration.FromDays(3650), clock: null);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    // -----------------------------------------------------------------------
    // GenerateIntermediateCa — happy path + chain builds
    // -----------------------------------------------------------------------

    [Fact]
    public void GenerateIntermediateCa_SignedByRoot_ChainsToRoot_PathLengthZero()
    {
        var clock = new TestClock(sr_now);

        var root = CaCertificateGeneration.GenerateRootCa(
            "D2 Root", Duration.FromDays(3650), clock).Data!;

        using var rootKey = ECDsa.Create();
        rootKey.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);
        using var rootCert = X509CertificateLoader.LoadCertificate(root.CertificateDer);

        var intermediateResult = CaCertificateGeneration.GenerateIntermediateCa(
            "D2 Issuing CA", rootCert, rootKey, Duration.FromDays(365), clock);

        intermediateResult.Success.Should().BeTrue();

        using var intermediate = X509CertificateLoader.LoadCertificate(
            intermediateResult.Data!.CertificateDer);

        // Issued by the root.
        intermediate.IssuerName.Name.Should().Be(rootCert.SubjectName.Name);

        // CA with path length 0 (signs only leaves).
        var basic = intermediate.Extensions.OfType<X509BasicConstraintsExtension>().Single();
        basic.CertificateAuthority.Should().BeTrue();
        basic.PathLengthConstraint.Should().Be(0);

        // The chain root → intermediate builds with the root as the only trust anchor.
        BuildsChain(intermediate, rootCert).Should().BeTrue();
    }

    [Fact]
    public void GenerateIntermediateCa_NullRootCert_ReturnsInvalidCertificateRequest()
    {
        using var rootKey = ECDsa.Create();
        var result = CaCertificateGeneration.GenerateIntermediateCa(
            "Issuing", null, rootKey, Duration.FromDays(365), new TestClock(sr_now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    [Fact]
    public void GenerateIntermediateCa_NullRootKey_ReturnsInvalidCertificateRequest()
    {
        var root = CaCertificateGeneration.GenerateRootCa(
            "Root", Duration.FromDays(3650), new TestClock(sr_now)).Data!;
        using var rootCert = X509CertificateLoader.LoadCertificate(root.CertificateDer);

        var result = CaCertificateGeneration.GenerateIntermediateCa(
            "Issuing", rootCert, null, Duration.FromDays(365), new TestClock(sr_now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    [Fact]
    public void GenerateIntermediateCa_EmptySubject_ReturnsInvalidCertificateRequest()
    {
        var root = CaCertificateGeneration.GenerateRootCa(
            "Root", Duration.FromDays(3650), new TestClock(sr_now)).Data!;
        using var rootKey = ECDsa.Create();
        rootKey.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);
        using var rootCert = X509CertificateLoader.LoadCertificate(root.CertificateDer);

        var result = CaCertificateGeneration.GenerateIntermediateCa(
            "  ", rootCert, rootKey, Duration.FromDays(365), new TestClock(sr_now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    private static bool BuildsChain(
        X509Certificate2 leafOrIntermediate, params X509Certificate2[] trustAnchors)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

        foreach (var anchor in trustAnchors)
            chain.ChainPolicy.CustomTrustStore.Add(anchor);

        return chain.Build(leafOrIntermediate);
    }
}
