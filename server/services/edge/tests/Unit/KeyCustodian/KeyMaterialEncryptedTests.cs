// -----------------------------------------------------------------------
// <copyright file="KeyMaterialEncryptedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using D2.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// Adversarial unit tests for <see cref="KeyMaterialEncrypted"/>.
/// Validates construction guards and the <c>ToString</c>/<c>PrintMembers</c>
/// PII trap — raw bytes must never appear in log output.
/// </summary>
public sealed class KeyMaterialEncryptedTests
{
    private static readonly byte[] sr_validBytes = [0x01, 0x02, 0x03, 0x04];

    // -----------------------------------------------------------------------
    // FromTrusted — valid
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_NonEmptyBytes_RoundTrips()
    {
        var mat = KeyMaterialEncrypted.FromTrusted(sr_validBytes);
        mat.Bytes.ToArray().Should().BeEquivalentTo(sr_validBytes);
    }

    // -----------------------------------------------------------------------
    // FromTrusted — empty bytes guard
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_EmptyBytes_ThrowsArgumentException()
    {
        var act = () => KeyMaterialEncrypted.FromTrusted(ReadOnlyMemory<byte>.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromTrusted_EmptyArray_ThrowsArgumentException()
    {
        var act = () => KeyMaterialEncrypted.FromTrusted(Array.Empty<byte>());
        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // PII trap — ToString must NOT contain raw bytes
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_DoesNotContainRawByteValues()
    {
        // Byte sequence: 0xDE, 0xAD — distinctive sentinel
        var mat = KeyMaterialEncrypted.FromTrusted(new byte[] { 0xDE, 0xAD });
        var str = mat.ToString();

        // Must NOT contain hex representations of the raw bytes
        str.Should().NotContain("DE");
        str.Should().NotContain("AD");
        str.Should().NotContain("222"); // decimal 0xDE
        str.Should().NotContain("173"); // decimal 0xAD
    }

    [Fact]
    public void ToString_ContainsRedactionSentinel()
    {
        var mat = KeyMaterialEncrypted.FromTrusted(sr_validBytes);
        var str = mat.ToString();
        str.Should().Contain("REDACTED");
    }

    [Fact]
    public void ToString_ContainsByteCount()
    {
        var mat = KeyMaterialEncrypted.FromTrusted(sr_validBytes);
        var str = mat.ToString();
        str.Should().Contain("4"); // 4 bytes
    }

    // -----------------------------------------------------------------------
    // Value equality — content-based, not backing-array identity
    // -----------------------------------------------------------------------

    [Fact]
    public void Equals_SameContentDifferentBackingArrays_AreEqual()
    {
        // Two independent copies of the same bytes — different backing arrays.
        var a = KeyMaterialEncrypted.FromTrusted(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var b = KeyMaterialEncrypted.FromTrusted(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentContent_AreNotEqual()
    {
        var a = KeyMaterialEncrypted.FromTrusted(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var b = KeyMaterialEncrypted.FromTrusted(new byte[] { 0x01, 0x02, 0x03, 0xFF });

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var a = KeyMaterialEncrypted.FromTrusted(sr_validBytes);
        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ReferenceEqual_ReturnsTrue()
    {
        var a = KeyMaterialEncrypted.FromTrusted(sr_validBytes);
        a.Equals(a).Should().BeTrue();
    }
}
