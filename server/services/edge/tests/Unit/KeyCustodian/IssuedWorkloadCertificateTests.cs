// -----------------------------------------------------------------------
// <copyright file="IssuedWorkloadCertificateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="IssuedWorkloadCertificate"/> — the
/// on-demand leaf handed back to the caller. The leaf private key is secret (must
/// zero on demand + never appear in <c>ToString</c>); the certificate, chain, and
/// validity are public.
/// </summary>
public sealed class IssuedWorkloadCertificateTests
{
    private static readonly WorkloadIdentity sr_workload = WorkloadIdentity.FromTrusted("edge");
    private static readonly Instant sr_notBefore = Instant.FromUtc(2026, 1, 1, 0, 0);
    private static readonly Instant sr_notAfter = Instant.FromUtc(2026, 1, 2, 0, 0);

    [Fact]
    public void Ctor_NullWorkload_Throws()
    {
        var act = () => new IssuedWorkloadCertificate(
            null!, [1], [2], [3], sr_notBefore, sr_notAfter);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_EmptyCertificate_Throws()
    {
        var act = () => new IssuedWorkloadCertificate(
            sr_workload, [], [2], [3], sr_notBefore, sr_notAfter);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_EmptyPrivateKey_Throws()
    {
        var act = () => new IssuedWorkloadCertificate(
            sr_workload, [1], [], [3], sr_notBefore, sr_notAfter);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_EmptyIssuerCertificate_Throws()
    {
        var act = () => new IssuedWorkloadCertificate(
            sr_workload, [1], [2], [], sr_notBefore, sr_notAfter);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_ValidArguments_ExposesAllFields()
    {
        var leaf = new IssuedWorkloadCertificate(
            sr_workload, [1, 2], [3, 4], [5, 6], sr_notBefore, sr_notAfter);

        leaf.Workload.ServiceId.Should().Be("edge");
        leaf.CertificateDer.Should().Equal([1, 2]);
        leaf.PrivateKeyPkcs8.Should().Equal([3, 4]);
        leaf.IssuerCertificateDer.Should().Equal([5, 6]);
        leaf.NotBefore.Should().Be(sr_notBefore);
        leaf.NotAfter.Should().Be(sr_notAfter);
    }

    [Fact]
    public void Zero_WipesPrivateKey_LeavesPublicMaterial()
    {
        var privateKey = RandomNumberGenerator.GetBytes(32);
        var cert = RandomNumberGenerator.GetBytes(16);
        var issuer = RandomNumberGenerator.GetBytes(16);
        var leaf = new IssuedWorkloadCertificate(
            sr_workload, cert, privateKey, issuer, sr_notBefore, sr_notAfter);

        leaf.Zero();

        leaf.PrivateKeyPkcs8.Should().OnlyContain(b => b == 0);
        leaf.CertificateDer.Should().Equal(cert);
        leaf.IssuerCertificateDer.Should().Equal(issuer);
    }

    [Fact]
    public void ToString_RedactsPrivateKey()
    {
        var leaf = new IssuedWorkloadCertificate(
            sr_workload,
            RandomNumberGenerator.GetBytes(4),
            RandomNumberGenerator.GetBytes(8),
            RandomNumberGenerator.GetBytes(4),
            sr_notBefore,
            sr_notAfter);

        var str = leaf.ToString();
        str.Should().Contain("REDACTED");
        str.Should().Contain("edge");
        str.Should().NotContain(Convert.ToHexString(leaf.PrivateKeyPkcs8));
    }
}
