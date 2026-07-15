// -----------------------------------------------------------------------
// <copyright file="IssuedWorkloadCertificateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="IssuedWorkloadCertificate"/> — the
/// on-demand leaf handed back to the caller. All-public material under the CSR
/// flow: the workload generates its own keypair and the leaf private key never
/// enters KeyCustodian, so the type carries no secret member (asserted
/// STRUCTURALLY — the strictly-stronger successor to the old private-key pins)
/// and its <c>ToString</c> stays material-free.
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
            null!, [1], [2], sr_notBefore, sr_notAfter);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_EmptyCertificate_Throws()
    {
        var act = () => new IssuedWorkloadCertificate(
            sr_workload, [], [2], sr_notBefore, sr_notAfter);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_EmptyIssuerCertificate_Throws()
    {
        var act = () => new IssuedWorkloadCertificate(
            sr_workload, [1], [], sr_notBefore, sr_notAfter);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_ValidArguments_ExposesAllFields()
    {
        var leaf = new IssuedWorkloadCertificate(
            sr_workload, [1, 2], [5, 6], sr_notBefore, sr_notAfter);

        leaf.Workload.ServiceId.Should().Be("edge");
        leaf.CertificateDer.Should().Equal([1, 2]);
        leaf.IssuerCertificateDer.Should().Equal([5, 6]);
        leaf.NotBefore.Should().Be(sr_notBefore);
        leaf.NotAfter.Should().Be(sr_notAfter);
    }

    [Fact]
    public void Type_HasNoPrivateKeyMember_Structural()
    {
        // The strictly-stronger successor to the old empty-private-key / Zero() /
        // ToString-redaction pins: under the CSR flow no private key exists here at
        // all, so a secret member is unrepresentable on the type.
        typeof(IssuedWorkloadCertificate).GetProperties()
            .Should().NotContain(
                p => p.Name.Contains("PrivateKey") || p.Name.Contains("Pkcs8"),
                "the CSR flow keeps the leaf private key workload-side — never here");

        typeof(IssuedWorkloadCertificate).GetMethods()
            .Should().NotContain(
                m => m.Name == "Zero",
                "with no secret member there is nothing to zero");
    }

    [Fact]
    public void ToString_EmitsByteCounts_NeverRawMaterial()
    {
        var cert = RandomNumberGenerator.GetBytes(4);
        var issuer = RandomNumberGenerator.GetBytes(4);
        var leaf = new IssuedWorkloadCertificate(
            sr_workload, cert, issuer, sr_notBefore, sr_notAfter);

        var str = leaf.ToString();
        str.Should().Contain("edge");
        str.Should().Contain("[4 bytes]", "byte arrays render as counts, not dumps");
        str.Should().NotContain(
            Convert.ToHexString(cert), "even public DER never dumps raw into a log line");
    }
}
