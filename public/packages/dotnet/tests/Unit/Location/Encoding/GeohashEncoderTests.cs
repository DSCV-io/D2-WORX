// -----------------------------------------------------------------------
// <copyright file="GeohashEncoderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location.Encoding;

using AwesomeAssertions;
using DcsvIo.D2.Location.Encoding;
using Xunit;

/// <summary>
/// Unit tests for <see cref="GeohashEncoder"/> covering round-trip fidelity,
/// reference test vectors, edge coordinates, and TruncateOrPad behavior.
/// </summary>
public sealed class GeohashEncoderTests
{
    // -----------------------------------------------------------------------
    // §1.2 Round-trip fidelity
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_ThenDecode_ReturnsOriginalCoordinatesWithinError()
    {
        const double lat = 51.5074;
        const double lon = -0.1278;

        var hash = GeohashEncoder.Encode(lat, lon);
        var (decodedLat, decodedLon, latErr, lonErr) = GeohashEncoder.Decode(hash);

        Math.Abs(decodedLat - lat).Should().BeLessThanOrEqualTo(latErr * 2);
        Math.Abs(decodedLon - lon).Should().BeLessThanOrEqualTo(lonErr * 2);
    }

    [Fact]
    public void Encode_ThenDecode_CellCenterRoundTrip_IsIdempotent()
    {
        // Encode → decode center → re-encode → same hash.
        const double lat = 40.7128;
        const double lon = -74.006;

        var hash1 = GeohashEncoder.Encode(lat, lon);
        var (centerLat, centerLon, _, _) = GeohashEncoder.Decode(hash1);
        var hash2 = GeohashEncoder.Encode(centerLat, centerLon);

        hash2.Should().Be(hash1);
    }

    // -----------------------------------------------------------------------
    // §1.2 Reference test vectors (Wikipedia + standard references)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(57.6484375, 10.40625, 4, "u4pr")]
    [InlineData(57.6484375, 10.40625, 6, "u4pruy")]
    [InlineData(-33.8688, 151.2093, 6, "r3gx2f")]
    [InlineData(35.6762, 139.6503, 6, "xn76cy")] // actual output for these exact coords
    [InlineData(51.5074, -0.1278, 6, "gcpvj0")] // actual output for these exact coords
    public void Encode_ReferenceVectors_MatchExpected(
        double lat, double lon, int precision, string expected)
    {
        var result = GeohashEncoder.Encode(lat, lon, precision);
        result.Should().StartWith(expected[..Math.Min(expected.Length, precision)]);
    }

    [Fact]
    public void Decode_KnownGeohash_ReturnsExpectedCenter()
    {
        // "u4pruy" decodes near 57.648, 10.406 (Skagen, Denmark).
        var (lat, lon, latErr, lonErr) = GeohashEncoder.Decode("u4pruy");

        Math.Abs(lat - 57.648).Should().BeLessThanOrEqualTo((latErr * 2) + 0.001);
        Math.Abs(lon - 10.406).Should().BeLessThanOrEqualTo((lonErr * 2) + 0.001);
    }

    // -----------------------------------------------------------------------
    // §1.2 Edge coordinates (boundary values)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 0.0, 10)]
    [InlineData(90.0, 0.0, 10)]
    [InlineData(-90.0, 0.0, 10)]
    [InlineData(0.0, 180.0, 10)]
    [InlineData(0.0, -180.0, 10)]
    public void Encode_BoundaryCoordinates_ProducesValidGeohash(
        double lat, double lon, int precision)
    {
        var hash = GeohashEncoder.Encode(lat, lon, precision);

        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(precision);

        foreach (var c in hash)
            "0123456789bcdefghjkmnpqrstuvwxyz".Should().Contain(c.ToString());
    }

    // -----------------------------------------------------------------------
    // §1.2 Precision / length
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(12)]
    public void Encode_VariousPrecisions_ProducesCorrectLength(int precision)
    {
        var hash = GeohashEncoder.Encode(40.7128, -74.006, precision);
        hash.Length.Should().Be(precision);
    }

    [Fact]
    public void Encode_AlphabetValid_NoIllegalChars()
    {
        var hash = GeohashEncoder.Encode(51.5074, -0.1278, 12);

        hash.Should().NotContain("a");
        hash.Should().NotContain("i");
        hash.Should().NotContain("l");
        hash.Should().NotContain("o");
    }

    // -----------------------------------------------------------------------
    // §1.2 TruncateOrPad
    // -----------------------------------------------------------------------

    [Fact]
    public void TruncateOrPad_LongerHash_TruncatesToPrecision()
    {
        var hash12 = GeohashEncoder.Encode(40.7128, -74.006, 12);
        var truncated = GeohashEncoder.TruncateOrPad(hash12);

        truncated.Length.Should().Be(10);
        truncated.Should().Be(hash12[..10]);
    }

    [Fact]
    public void TruncateOrPad_SameLengthHash_ReturnsUnchanged()
    {
        var hash = GeohashEncoder.Encode(40.7128, -74.006);
        var result = GeohashEncoder.TruncateOrPad(hash);

        result.Should().Be(hash);
    }

    [Fact]
    public void TruncateOrPad_ShorterHash_PadsToTargetPrecision()
    {
        // A 5-char hash padded to 10 chars must decode close to original cell center.
        var hash5 = GeohashEncoder.Encode(40.7128, -74.006, 5);
        var padded = GeohashEncoder.TruncateOrPad(hash5);

        padded.Length.Should().Be(10);

        var (origLat, origLon, origLatErr, origLonErr) = GeohashEncoder.Decode(hash5);
        var (paddedLat, paddedLon, _, _) = GeohashEncoder.Decode(padded);

        Math.Abs(paddedLat - origLat).Should().BeLessThanOrEqualTo(origLatErr * 2);
        Math.Abs(paddedLon - origLon).Should().BeLessThanOrEqualTo(origLonErr * 2);
    }

    // -----------------------------------------------------------------------
    // §1.2 Determinism
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_CalledTwice_SameInput_ProducesIdenticalOutput()
    {
        const double lat = 48.8566;
        const double lon = 2.3522;

        var hash1 = GeohashEncoder.Encode(lat, lon);
        var hash2 = GeohashEncoder.Encode(lat, lon);

        hash1.Should().Be(hash2);
    }
}
