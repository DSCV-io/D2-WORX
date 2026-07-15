// -----------------------------------------------------------------------
// <copyright file="CoordinatesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Location.ValueObjects;
using Xunit;

/// <summary>
/// Adversarial test coverage for <see cref="Coordinates"/> per §7.1 matrix:
/// happy path, out-of-range / NaN / Infinity, boundary values, AccuracyMeters
/// not in hash, 3-factory cross-equivalence, round-trip determinism, invalid
/// format inputs, HashId invariants, and canonical normalization.
/// </summary>
public sealed class CoordinatesTests
{
    // -----------------------------------------------------------------------
    // §1.2 Happy path — Create(lat, lon)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_ValidLatLon_ReturnsOk_WithAllFieldsPopulated()
    {
        var result = Coordinates.Create(40.7128, -74.006);

        result.Success.Should().BeTrue();
        var coord = result.Data!;
        coord.Latitude.Should().BeApproximately(40.7128, 0.0001);
        coord.Longitude.Should().BeApproximately(-74.006, 0.0001);
        coord.Geohash.Should().NotBeNullOrEmpty();
        coord.Geohash.Length.Should().Be(10);
        coord.PlusCode.Should().NotBeNullOrEmpty();
        coord.HashId.Should().NotBeNullOrEmpty();
        coord.HashId.Should().StartWith("v1.");
    }

    [Fact]
    public void Create_ValidLatLon_AccuracyMeters_StoredAsMetadata()
    {
        var result = Coordinates.Create(40.7128, -74.006, accuracyMeters: 5.0);

        result.Success.Should().BeTrue();
        result.Data!.AccuracyMeters.Should().Be(5.0);
    }

    // -----------------------------------------------------------------------
    // §1.2 Out-of-range latitude
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(91.0)]
    [InlineData(-91.0)]
    [InlineData(100.0)]
    [InlineData(-100.0)]
    [InlineData(180.0)]
    public void Create_OutOfRangeLat_ReturnsValidationFailed(double lat)
    {
        var result = Coordinates.Create(lat, 0.0);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.LATITUDE_RANGE.Key);
    }

    // -----------------------------------------------------------------------
    // §1.2 Out-of-range longitude
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(181.0)]
    [InlineData(-181.0)]
    [InlineData(200.0)]
    [InlineData(-200.0)]
    public void Create_OutOfRangeLon_ReturnsValidationFailed(double lon)
    {
        var result = Coordinates.Create(0.0, lon);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.LONGITUDE_RANGE.Key);
    }

    // -----------------------------------------------------------------------
    // §1.2 NaN and Infinity inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_NaNLatitude_ReturnsValidationFailed_FiniteRequired()
    {
        var result = Coordinates.Create(double.NaN, 0.0);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_FINITE_REQUIRED.Key);
    }

    [Fact]
    public void Create_NaNLongitude_ReturnsValidationFailed_FiniteRequired()
    {
        var result = Coordinates.Create(0.0, double.NaN);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_FINITE_REQUIRED.Key);
    }

    [Fact]
    public void Create_PositiveInfinityLatitude_ReturnsValidationFailed_FiniteRequired()
    {
        var result = Coordinates.Create(double.PositiveInfinity, 0.0);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_FINITE_REQUIRED.Key);
    }

    [Fact]
    public void Create_NegativeInfinityLongitude_ReturnsValidationFailed_FiniteRequired()
    {
        var result = Coordinates.Create(0.0, double.NegativeInfinity);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_FINITE_REQUIRED.Key);
    }

    [Fact]
    public void Create_PositiveInfinityLongitude_ReturnsValidationFailed_FiniteRequired()
    {
        // Exercises the double.IsFinite(longitude) guard on the positive-infinity path
        // (the negative-infinity path is covered separately above).
        var result = Coordinates.Create(0.0, double.PositiveInfinity);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_FINITE_REQUIRED.Key);
    }

    [Fact]
    public void Create_NegativeAccuracy_ReturnsValidationFailed_FiniteRequired()
    {
        var result = Coordinates.Create(40.7128, -74.006, accuracyMeters: -1.0);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_FINITE_REQUIRED.Key);
    }

    [Fact]
    public void Create_NaNAccuracy_ReturnsValidationFailed_FiniteRequired()
    {
        var result = Coordinates.Create(40.7128, -74.006, accuracyMeters: double.NaN);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_FINITE_REQUIRED.Key);
    }

    // -----------------------------------------------------------------------
    // §1.2 Boundary values (inclusive bounds must succeed)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(90.0, 0.0)]
    [InlineData(-90.0, 0.0)]
    [InlineData(0.0, 180.0)]
    [InlineData(0.0, -180.0)]
    [InlineData(90.0, 180.0)]
    [InlineData(-90.0, -180.0)]
    public void Create_BoundaryValues_ReturnsOk(double lat, double lon)
    {
        var result = Coordinates.Create(lat, lon);
        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // §1.2 AccuracyMeters NOT in hash (Decision 4)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_SameLatLon_DifferentAccuracy_ProducesSameHashId()
    {
        var r1 = Coordinates.Create(40.7128, -74.006, accuracyMeters: 1.0);
        var r2 = Coordinates.Create(40.7128, -74.006, accuracyMeters: 999.0);

        r1.Success.Should().BeTrue();
        r2.Success.Should().BeTrue();
        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_SameLatLon_NullVsNonNullAccuracy_ProducesSameHashId()
    {
        var r1 = Coordinates.Create(40.7128, -74.006, accuracyMeters: null);
        var r2 = Coordinates.Create(40.7128, -74.006, accuracyMeters: 50.0);

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // §1.2 HashId invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_StartsWithV1Prefix()
    {
        var result = Coordinates.Create(40.7128, -74.006);
        result.Data!.HashId.Should().StartWith("v1.");
    }

    [Fact]
    public void Create_HashId_HasCorrectLength()
    {
        // "v1." (3) + 64 hex chars (SHA-256) = 67
        var result = Coordinates.Create(40.7128, -74.006);
        result.Data!.HashId.Length.Should().Be(67);
    }

    [Fact]
    public void Create_HashId_IsLowercaseHex()
    {
        var result = Coordinates.Create(40.7128, -74.006);
        var hexPart = result.Data!.HashId[3..];
        hexPart.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    // -----------------------------------------------------------------------
    // §1.2 Determinism
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CalledTwice_SameInput_ProducesIdenticalHashId()
    {
        var r1 = Coordinates.Create(40.7128, -74.006);
        var r2 = Coordinates.Create(40.7128, -74.006);

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // §1.2 3-factory cross-equivalence
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_ThenFromGeohash_SameCanonicalCell_ProducesSameHashId()
    {
        var fromLatLon = Coordinates.Create(40.7128, -74.006);
        var geohash = fromLatLon.Data!.Geohash;

        var fromGeohash = Coordinates.FromGeohash(geohash);

        fromGeohash.Success.Should().BeTrue();
        fromGeohash.Data!.HashId.Should().Be(fromLatLon.Data.HashId);
    }

    [Fact]
    public void Create_ThenFromPlusCode_SameCanonicalCell_LatLonWithinSnapTolerance()
    {
        // OLC → geohash snap introduces up to ~2m drift. Verify within tolerance.
        var fromLatLon = Coordinates.Create(40.7128, -74.006);
        var plusCode = fromLatLon.Data!.PlusCode;

        var fromPlusCode = Coordinates.FromPlusCode(plusCode);

        fromPlusCode.Success.Should().BeTrue();
        Math.Abs(fromPlusCode.Data!.Latitude - fromLatLon.Data.Latitude).Should().BeLessThan(0.001);
        Math.Abs(fromPlusCode.Data!.Longitude - fromLatLon.Data.Longitude)
            .Should().BeLessThan(0.001);
    }

    [Fact]
    public void CrossFactory_GeohashFromCreate_MatchesRoundTrip()
    {
        var r1 = Coordinates.Create(40.7128, -74.006);
        var r2 = Coordinates.FromGeohash(r1.Data!.Geohash);

        r2.Data!.HashId.Should().Be(r1.Data.HashId);
    }

    // -----------------------------------------------------------------------
    // §1.2 FromGeohash — valid inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void FromGeohash_ValidHash_ReturnsOk()
    {
        var result = Coordinates.FromGeohash("dr5regw3pp");

        result.Success.Should().BeTrue();
        result.Data!.Geohash.Should().Be("dr5regw3pp");
    }

    [Fact]
    public void FromGeohash_ValidHash_PopulatesAllFields()
    {
        var result = Coordinates.FromGeohash("u4pruy0k85");

        result.Success.Should().BeTrue();
        var coord = result.Data!;
        coord.Geohash.Should().NotBeNullOrEmpty();
        coord.PlusCode.Should().NotBeNullOrEmpty();
        coord.HashId.Should().StartWith("v1.");
        coord.Latitude.Should().BeInRange(-90.0, 90.0);
        coord.Longitude.Should().BeInRange(-180.0, 180.0);
    }

    // -----------------------------------------------------------------------
    // §1.2 FromGeohash — precision normalization
    // -----------------------------------------------------------------------

    [Fact]
    public void FromGeohash_12CharInput_StoredGeohashIs10Chars()
    {
        // 12-char geohash → truncated to 10 in stored Geohash field.
        var result = Coordinates.FromGeohash("dr5regw3ppzz");

        result.Success.Should().BeTrue();
        result.Data!.Geohash.Length.Should().Be(10);
    }

    [Fact]
    public void FromGeohash_ShortHash_PadsToCanonical10Chars()
    {
        // 5-char hash → decoded to cell center → re-encoded at 10 chars.
        var result = Coordinates.FromGeohash("dr5re");

        result.Success.Should().BeTrue();
        result.Data!.Geohash.Length.Should().Be(10);
    }

    // -----------------------------------------------------------------------
    // §1.2 FromGeohash — invalid inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void FromGeohash_EmptyString_ReturnsValidationFailed()
    {
        var result = Coordinates.FromGeohash(string.Empty);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_GEOHASH_INVALID.Key);
    }

    [Fact]
    public void FromGeohash_NullString_ReturnsValidationFailed()
    {
        var result = Coordinates.FromGeohash(null!);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_GEOHASH_INVALID.Key);
    }

    [Fact]
    public void FromGeohash_WhitespaceOnly_ReturnsValidationFailed()
    {
        var result = Coordinates.FromGeohash("   ");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_GEOHASH_INVALID.Key);
    }

    [Theory]
    [InlineData("INVALID!")]
    [InlineData("abcd")]
    [InlineData("xyz123lmno")]
    [InlineData("dr5regw3ppzzzzzzzz")]
    public void FromGeohash_InvalidFormat_ReturnsValidationFailed(string geohash)
    {
        var result = Coordinates.FromGeohash(geohash);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_GEOHASH_INVALID.Key);
    }

    // -----------------------------------------------------------------------
    // §1.2 FromPlusCode — valid inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void FromPlusCode_ValidCode_ReturnsOk()
    {
        var result = Coordinates.FromPlusCode("87G7MQ8V+RG");

        result.Success.Should().BeTrue();
        result.Data!.HashId.Should().StartWith("v1.");
        result.Data!.Geohash.Length.Should().Be(10);
    }

    // -----------------------------------------------------------------------
    // §1.2 FromPlusCode — invalid inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void FromPlusCode_EmptyString_ReturnsValidationFailed()
    {
        var result = Coordinates.FromPlusCode(string.Empty);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_PLUSCODE_INVALID.Key);
    }

    [Fact]
    public void FromPlusCode_NullString_ReturnsValidationFailed()
    {
        var result = Coordinates.FromPlusCode(null!);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_PLUSCODE_INVALID.Key);
    }

    [Theory]
    [InlineData("NOPLUS")]
    [InlineData("87G7MQ8VRG")]
    [InlineData("87G7MQ8V+")]
    [InlineData("+87G7MQ8V")]
    [InlineData("AI+BC")]
    public void FromPlusCode_InvalidFormat_ReturnsValidationFailed(string plusCode)
    {
        var result = Coordinates.FromPlusCode(plusCode);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_PLUSCODE_INVALID.Key);
    }

    // -----------------------------------------------------------------------
    // §1.2 Cross-factory hash agreement
    // -----------------------------------------------------------------------

    [Fact]
    public void CrossFactory_HashAgreement_CreateAndFromGeohash_ProduceSameHashId()
    {
        var fromCreate = Coordinates.Create(40.7128, -74.006);
        var fromGeohash = Coordinates.FromGeohash(fromCreate.Data!.Geohash);

        fromGeohash.Data!.HashId.Should().Be(fromCreate.Data.HashId);
    }

    // -----------------------------------------------------------------------
    // §1.2 Geohash field uses canonical 10-char geohash alphabet
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_StoredGeohash_UsesOnlyValidAlphabet()
    {
        var result = Coordinates.Create(40.7128, -74.006);
        var geohash = result.Data!.Geohash;

        geohash.Should().NotContain("a");
        geohash.Should().NotContain("i");
        geohash.Should().NotContain("l");
        geohash.Should().NotContain("o");
    }

    // -----------------------------------------------------------------------
    // §1.2 PlusCode format validation
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_StoredPlusCode_ContainsPlusSeparatorAtPosition8()
    {
        var result = Coordinates.Create(40.7128, -74.006);
        var plusCode = result.Data!.PlusCode;

        plusCode.IndexOf('+').Should().Be(8);
    }

    [Fact]
    public void Create_StoredPlusCode_Is13Chars_PinsProductionCodeLength()
    {
        // Production codeLength = 12 → 8 pair digits + '+' + 4 grid digits = 13 chars.
        const int expected_pluscode_length = 13;
        var coord = Coordinates.Create(40.7128, -74.006).Data!;

        coord.PlusCode.Length.Should().Be(expected_pluscode_length);
    }

    // -----------------------------------------------------------------------
    // §1.2 Zero accuracy is valid (non-negative boundary)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_ZeroAccuracy_ReturnsOk()
    {
        var result = Coordinates.Create(40.7128, -74.006, accuracyMeters: 0.0);
        result.Success.Should().BeTrue();
        result.Data!.AccuracyMeters.Should().Be(0.0);
    }

    // -----------------------------------------------------------------------
    // Security-adversarial: CRLF / NUL byte injection rejected by FromGeohash
    // -----------------------------------------------------------------------

    [Fact]
    public void FromGeohash_CrLfInjected_ValidationFailed()
    {
        var result = Coordinates.FromGeohash("u4pruydqqv\r\nINJECT");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_GEOHASH_INVALID.Key);
    }

    [Fact]
    public void FromGeohash_NullByteInjected_ValidationFailed()
    {
        var result = Coordinates.FromGeohash("u4pruyd\0qv");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_GEOHASH_INVALID.Key);
    }

    [Fact]
    public void FromPlusCode_CrLfInjected_ValidationFailed()
    {
        var result = Coordinates.FromPlusCode("849VCWC8+R9\r\n");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_PLUSCODE_INVALID.Key);
    }

    [Fact]
    public void FromPlusCode_NullByteInjected_ValidationFailed()
    {
        var result = Coordinates.FromPlusCode("849VCWC8\0+R9");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.COORDINATES_PLUSCODE_INVALID.Key);
    }

    // -----------------------------------------------------------------------
    // Hash-stability regression — golden-value pin (T9)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_KnownInput_MatchesGoldenValue()
    {
        // Pin: Coordinates.Create(40.7128, -74.006) →
        //   geohash-10 cell center "dr5regw3pp"
        //   → SHA-256(UTF-8("dr5regw3pp")) → v1.<64-hex>.
        // Golden value sourced from parity-fixtures.json case "coords-only-nyc-create".
        // A hash algorithm change causes this to fail and prevents silent drift.
        const string expected_hash =
            "v1.3c5339b07059d200d45867d4478967a34592818af0445a2aea3c2bd3ff54ef95";
        var result = Coordinates.Create(40.7128, -74.006);
        result.Data!.HashId.Should().Be(expected_hash);
    }
}
