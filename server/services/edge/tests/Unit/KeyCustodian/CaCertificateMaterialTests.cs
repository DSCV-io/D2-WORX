// -----------------------------------------------------------------------
// <copyright file="CaCertificateMaterialTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="CaCertificateMaterial"/>. A CA
/// certificate is public (presented on the wire), so it is NOT redacted; but the
/// raw bytes should not dump in log output (verbosity/noise — byte-count sentinel
/// instead). Value equality is content-based.
/// </summary>
public sealed class CaCertificateMaterialTests
{
    private static readonly byte[] sr_validBytes = [0x30, 0x82, 0x01, 0x0A];

    [Fact]
    public void FromTrusted_NonEmptyBytes_RoundTrips()
    {
        var cert = CaCertificateMaterial.FromTrusted(sr_validBytes);
        cert.Bytes.ToArray().Should().BeEquivalentTo(sr_validBytes);
    }

    [Fact]
    public void FromTrusted_EmptyBytes_ThrowsArgumentException()
    {
        var act = () => CaCertificateMaterial.FromTrusted(ReadOnlyMemory<byte>.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromTrusted_EmptyArray_ThrowsArgumentException()
    {
        var act = () => CaCertificateMaterial.FromTrusted(Array.Empty<byte>());
        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // ToString — byte-count sentinel (not raw bytes)
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_ContainsByteCount()
    {
        var cert = CaCertificateMaterial.FromTrusted(sr_validBytes);
        cert.ToString().Should().Contain("4");
    }

    [Fact]
    public void ToString_DoesNotContainRawHexValues()
    {
        var cert = CaCertificateMaterial.FromTrusted(new byte[] { 0xDE, 0xAD });
        var str = cert.ToString();
        str.Should().NotContain("222"); // decimal 0xDE
        str.Should().NotContain("173"); // decimal 0xAD
    }

    // -----------------------------------------------------------------------
    // Value equality — content-based, not backing-array identity
    // -----------------------------------------------------------------------

    [Fact]
    public void Equals_SameContentDifferentBackingArrays_AreEqual()
    {
        var a = CaCertificateMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xCC });
        var b = CaCertificateMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xCC });

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentContent_AreNotEqual()
    {
        var a = CaCertificateMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xCC });
        var b = CaCertificateMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xFF });

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var a = CaCertificateMaterial.FromTrusted(sr_validBytes);
        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ReferenceEqual_ReturnsTrue()
    {
        var a = CaCertificateMaterial.FromTrusted(sr_validBytes);
        a.Equals(a).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // PII posture — a CA certificate is public, intentionally NOT redacted
    // -----------------------------------------------------------------------

    [Fact]
    public void Bytes_HasNoRedactDataAttribute()
    {
        var prop = typeof(CaCertificateMaterial).GetProperty(nameof(CaCertificateMaterial.Bytes));
        prop.Should().NotBeNull();
        prop.GetCustomAttribute<RedactDataAttribute>().Should().BeNull(
            "a CA certificate is presented on the wire and pinned as a trust anchor — "
            + "intentionally not redacted, like the JWKS signing key's public material");
    }
}
