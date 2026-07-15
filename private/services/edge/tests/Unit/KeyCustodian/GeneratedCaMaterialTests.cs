// -----------------------------------------------------------------------
// <copyright file="GeneratedCaMaterialTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="GeneratedCaMaterial"/> — the zero-after-wrap
/// carrier for a freshly-generated CA's private key + certificate. The private key
/// must zero on demand and never appear in <c>ToString</c>; the certificate is
/// public.
/// </summary>
public sealed class GeneratedCaMaterialTests
{
    [Fact]
    public void Ctor_NullPrivateKey_Throws()
    {
        var act = () => new GeneratedCaMaterial(null!, [1, 2, 3]);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullCertificate_Throws()
    {
        var act = () => new GeneratedCaMaterial([1, 2, 3], null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_EmptyPrivateKey_Throws()
    {
        var act = () => new GeneratedCaMaterial([], [1, 2, 3]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_EmptyCertificate_Throws()
    {
        var act = () => new GeneratedCaMaterial([1, 2, 3], []);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Zero_WipesPrivateKey_LeavesCertificate()
    {
        var privateKey = RandomNumberGenerator.GetBytes(32);
        var cert = RandomNumberGenerator.GetBytes(16);
        var material = new GeneratedCaMaterial(privateKey, cert);

        material.Zero();

        material.PrivateKeyPkcs8.Should().OnlyContain(b => b == 0);
        material.CertificateDer.Should().Equal(cert);
    }

    [Fact]
    public void ToString_RedactsPrivateKey_ShowsCertByteCount()
    {
        var material = new GeneratedCaMaterial(
            RandomNumberGenerator.GetBytes(8), RandomNumberGenerator.GetBytes(4));

        var str = material.ToString();
        str.Should().Contain("REDACTED");
        str.Should().NotContain(Convert.ToHexString(material.PrivateKeyPkcs8));
    }
}
