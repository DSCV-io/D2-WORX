// -----------------------------------------------------------------------
// <copyright file="PlusCodeEncoderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Location.Encoding;

using AwesomeAssertions;
using D2.Shared.Location.Encoding;
using Xunit;

/// <summary>
/// Unit tests for <see cref="PlusCodeEncoder"/> covering IsValid, round-trip
/// fidelity, reference test vectors, and edge cases.
/// </summary>
public sealed class PlusCodeEncoderTests
{
    // -----------------------------------------------------------------------
    // §1.2 IsValid — happy path
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("87G7MQ8V+RG")]
    [InlineData("87G7PX2C+X2")]
    [InlineData("8FW4V75V+8F")]
    [InlineData("8Q7XMM8V+RG")]
    public void IsValid_ValidPlusCodes_ReturnsTrue(string plusCode)
    {
        PlusCodeEncoder.IsValid(plusCode).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // §1.2 IsValid — invalid inputs
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("INVALID")]
    [InlineData("87G7MQ8VRG")]
    [InlineData("87G7MQ8V++RG")]
    [InlineData("87G7MQ8V+")]
    [InlineData("+RG")]
    [InlineData("AI+BC")]
    public void IsValid_InvalidPlusCodes_ReturnsFalse(string? plusCode)
    {
        PlusCodeEncoder.IsValid(plusCode).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // §1.2 Round-trip fidelity
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_ThenDecode_ReturnsCoordinatesWithinError()
    {
        const double lat = 40.7128;
        const double lon = -74.006;

        var plusCode = PlusCodeEncoder.Encode(lat, lon, codeLength: 10);
        var (decodedLat, decodedLon, latErr, lonErr) = PlusCodeEncoder.Decode(plusCode);

        Math.Abs(decodedLat - lat).Should().BeLessThanOrEqualTo(latErr * 4);
        Math.Abs(decodedLon - lon).Should().BeLessThanOrEqualTo(lonErr * 4);
    }

    [Fact]
    public void Encode_IsValid_ProducesValidCode()
    {
        var code = PlusCodeEncoder.Encode(51.5074, -0.1278, codeLength: 10);
        PlusCodeEncoder.IsValid(code).Should().BeTrue();
    }

    [Fact]
    public void Encode_ContainsPlusSeparator_AtPosition8()
    {
        var code = PlusCodeEncoder.Encode(35.6762, 139.6503, codeLength: 10);

        code.IndexOf('+').Should().Be(8);
    }

    // -----------------------------------------------------------------------
    // §1.2 Edge coordinates
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(89.9, 0.0)]
    [InlineData(-89.9, 0.0)]
    [InlineData(0.0, 179.9)]
    [InlineData(0.0, -179.9)]
    public void Encode_BoundaryCoordinates_ProducesValidPlusCode(double lat, double lon)
    {
        var code = PlusCodeEncoder.Encode(lat, lon, codeLength: 10);
        PlusCodeEncoder.IsValid(code).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // §1.2 OLC alphabet — no invalid characters
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_UsesOnlyOlcAlphabet()
    {
        const string validChars = "23456789CFGHJMPQRVWX0+";
        var code = PlusCodeEncoder.Encode(40.7128, -74.006, codeLength: 10);

        foreach (var c in code)
            validChars.Should().Contain(c.ToString());
    }

    // -----------------------------------------------------------------------
    // §1.2 Determinism
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_CalledTwice_SameInput_ProducesIdenticalOutput()
    {
        const double lat = 48.8566;
        const double lon = 2.3522;

        var code1 = PlusCodeEncoder.Encode(lat, lon);
        var code2 = PlusCodeEncoder.Encode(lat, lon);

        code1.Should().Be(code2);
    }

    // -----------------------------------------------------------------------
    // §1.2 Decode produces center within cell
    // -----------------------------------------------------------------------

    [Fact]
    public void Decode_EncodeThenDecode_CenterIsIdempotent()
    {
        const double lat = 51.5074;
        const double lon = -0.1278;

        var code1 = PlusCodeEncoder.Encode(lat, lon);
        var (cLat, cLon, _, _) = PlusCodeEncoder.Decode(code1);
        var code2 = PlusCodeEncoder.Encode(cLat, cLon);

        code2.Should().Be(code1);
    }

    // -----------------------------------------------------------------------
    // §2.1 Regression tests — F-A2-1 closure.
    // Pins the FULL_PAIRS = 4 invariant + Decode pair-count derivation.
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_HasExactly8DigitsBeforePlusSeparator_PinsFullPairsCount()
    {
        // Regression: prior bug computed fullPairs = pairDigits / 2 → 5 pairs (10 chars).
        // OLC spec mandates 4 full pairs = 8 prefix digits before '+'.
        var code = PlusCodeEncoder.Encode(40.7128, -74.006, codeLength: 10);

        var plusIdx = code.IndexOf('+');
        plusIdx.Should().Be(8);

        // Belt-and-braces: prefix has 8 chars, suffix has 2 chars.
        var prefix = code[..plusIdx];
        var suffix = code[(plusIdx + 1)..];
        prefix.Length.Should().Be(8);
        suffix.Length.Should().Be(2);
    }

    [Fact]
    public void Decode_FullPairCountDerivedFromSeparatorPosition_NotStrippedLength()
    {
        // Regression: prior bug derived pair count from stripped-+ length → wrong fullPairs.
        // The Decode pair count must come from the prefix.Length / 2 with the
        // OLC-spec separator at position 8 → fullPairs = 4. Encoding then re-decoding
        // a known-canonical input must round-trip with the same prefix length.
        var code = PlusCodeEncoder.Encode(40.7128, -74.006, codeLength: 10);
        var (decodedLat, decodedLon, latErr, lonErr) = PlusCodeEncoder.Decode(code);

        // If fullPairs were wrong, decode would project to a completely different
        // lat/lon — these bounds would blow up. Generous 4× error tolerance.
        Math.Abs(decodedLat - 40.7128).Should().BeLessThanOrEqualTo(latErr * 4);
        Math.Abs(decodedLon - -74.006).Should().BeLessThanOrEqualTo(lonErr * 4);

        // Re-encode the center and confirm idempotency (proves pair-count invariant on both sides).
        var reEncoded = PlusCodeEncoder.Encode(decodedLat, decodedLon, codeLength: 10);
        reEncoded.Should().Be(code);
    }
}
