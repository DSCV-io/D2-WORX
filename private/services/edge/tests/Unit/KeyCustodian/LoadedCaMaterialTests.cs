// -----------------------------------------------------------------------
// <copyright file="LoadedCaMaterialTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using D2.Edge.KeyCustodian.App.Infrastructure.Vault;

/// <summary>
/// Adversarial unit tests for <see cref="LoadedCaMaterial"/> — the zero-after-wrap
/// carrier for the loaded dev CA hierarchy. Both private keys must zero on demand
/// and never appear in <c>ToString</c>; the certificates are public.
/// </summary>
public sealed class LoadedCaMaterialTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Ctor_NullArgument_Throws(int nullIndex)
    {
        var args = new[] { Bytes(4), Bytes(8), Bytes(4), Bytes(8) };
        args[nullIndex] = null!;

        var act = () => new LoadedCaMaterial(args[0], args[1], args[2], args[3]);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Ctor_EmptyArgument_Throws(int emptyIndex)
    {
        var args = new[] { Bytes(4), Bytes(8), Bytes(4), Bytes(8) };
        args[emptyIndex] = [];

        var act = () => new LoadedCaMaterial(args[0], args[1], args[2], args[3]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Zero_WipesBothPrivateKeys_LeavesCertificates()
    {
        var rootCert = Bytes(16);
        var rootKey = Bytes(32);
        var intermediateCert = Bytes(16);
        var intermediateKey = Bytes(32);
        var material = new LoadedCaMaterial(rootCert, rootKey, intermediateCert, intermediateKey);

        material.Zero();

        material.RootPrivateKeyPkcs8.Should().OnlyContain(b => b == 0);
        material.IntermediatePrivateKeyPkcs8.Should().OnlyContain(b => b == 0);
        material.RootCertificateDer.Should().Equal(rootCert);
        material.IntermediateCertificateDer.Should().Equal(intermediateCert);
    }

    [Fact]
    public void ToString_RedactsBothPrivateKeys_ShowsCertByteCounts()
    {
        var material = new LoadedCaMaterial(Bytes(16), Bytes(32), Bytes(16), Bytes(32));

        var str = material.ToString();

        str.Should().Contain("REDACTED");
        str.Should().NotContain(Convert.ToHexString(material.RootPrivateKeyPkcs8));
        str.Should().NotContain(Convert.ToHexString(material.IntermediatePrivateKeyPkcs8));
    }

    private static byte[] Bytes(int n) => RandomNumberGenerator.GetBytes(n);
}
