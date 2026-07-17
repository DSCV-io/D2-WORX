// -----------------------------------------------------------------------
// <copyright file="PublicKeyMaterialTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="PublicKeyMaterial"/>.
/// Public key bytes are not secret, but the raw bytes should not appear in
/// log output (verbosity/noise — byte-count sentinel instead).
/// </summary>
public sealed class PublicKeyMaterialTests
{
    private static readonly byte[] sr_validBytes = [0xAA, 0xBB, 0xCC, 0xDD];

    // -----------------------------------------------------------------------
    // FromTrusted — valid
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_NonEmptyBytes_RoundTrips()
    {
        var pub = PublicKeyMaterial.FromTrusted(sr_validBytes);
        pub.Bytes.ToArray().Should().BeEquivalentTo(sr_validBytes);
    }

    // -----------------------------------------------------------------------
    // FromTrusted — empty bytes guard
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_EmptyBytes_ThrowsArgumentException()
    {
        var act = () => PublicKeyMaterial.FromTrusted(ReadOnlyMemory<byte>.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromTrusted_EmptyArray_ThrowsArgumentException()
    {
        var act = () => PublicKeyMaterial.FromTrusted(Array.Empty<byte>());
        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // ToString — byte-count sentinel (not raw bytes)
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_ContainsByteCount()
    {
        var pub = PublicKeyMaterial.FromTrusted(sr_validBytes);
        var str = pub.ToString();
        str.Should().Contain("4");
    }

    [Fact]
    public void ToString_DoesNotContainRawHex()
    {
        // Distinctive sentinels
        var pub = PublicKeyMaterial.FromTrusted(new byte[] { 0xDE, 0xAD });
        var str = pub.ToString();
        str.Should().NotContain("222"); // decimal 0xDE
        str.Should().NotContain("173"); // decimal 0xAD
    }

    // -----------------------------------------------------------------------
    // Value equality — content-based, not backing-array identity
    // -----------------------------------------------------------------------

    [Fact]
    public void Equals_SameContentDifferentBackingArrays_AreEqual()
    {
        // Two independent copies of the same bytes — different backing arrays.
        var a = PublicKeyMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });
        var b = PublicKeyMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentContent_AreNotEqual()
    {
        var a = PublicKeyMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });
        var b = PublicKeyMaterial.FromTrusted(new byte[] { 0xAA, 0xBB, 0xCC, 0xFF });

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var a = PublicKeyMaterial.FromTrusted(sr_validBytes);
        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ReferenceEqual_ReturnsTrue()
    {
        var a = PublicKeyMaterial.FromTrusted(sr_validBytes);
        a.Equals(a).Should().BeTrue();
    }
}
