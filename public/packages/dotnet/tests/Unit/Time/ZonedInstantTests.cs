// -----------------------------------------------------------------------
// <copyright file="ZonedInstantTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Time;
using NodaTime;
using Xunit;

public sealed class ZonedInstantTests
{
    [Fact]
    public void Create_ValidInstantAndCanonicalIANA_ReturnsOk()
    {
        var instant = Instant.FromUnixTimeSeconds(1000);

        var result = ZonedInstant.Create(instant, "America/Edmonton");

        result.Success.Should().BeTrue();
        result.Data!.Instant.Should().Be(instant);
        result.Data.IANAIdentifier.Should().Be("America/Edmonton");
    }

    [Fact]
    public void Create_CanonicalIANAInput_StoresInputUnchanged()
    {
        var result = ZonedInstant.Create(
            Instant.FromUnixTimeSeconds(0),
            "America/Edmonton");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/Edmonton");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var instant = Instant.FromUnixTimeSeconds(500);

        var a = ZonedInstant.Create(instant, "Europe/London").Data!;
        var b = ZonedInstant.Create(instant, "Europe/London").Data!;

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentInstant_AreNotEqual()
    {
        var a = ZonedInstant.Create(Instant.FromUnixTimeSeconds(1), "UTC").Data!;
        var b = ZonedInstant.Create(Instant.FromUnixTimeSeconds(2), "UTC").Data!;

        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentIANA_AreNotEqual()
    {
        var instant = Instant.FromUnixTimeSeconds(1);

        var a = ZonedInstant.Create(instant, "America/Edmonton").Data!;
        var b = ZonedInstant.Create(instant, "America/Vancouver").Data!;

        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_SameInstantSameCanonicalIANA_AreEqual_AcrossFactoryCalls()
    {
        var instant = Instant.FromUnixTimeSeconds(42);

        var a = ZonedInstant.Create(instant, "UTC").Data!;
        var b = ZonedInstant.Create(instant, "UTC").Data!;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ZonedInstant_IsSealed()
    {
        typeof(ZonedInstant).IsSealed.Should().BeTrue();
    }

    // --- IANA validation: rejection ---

    [Fact]
    public void Create_NullIANA_ReturnsValidationFailed_RequiredViolation()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), null);

        result.Success.Should().BeFalse();
        result.InputErrors.Should().ContainSingle();
        result.InputErrors[0].Field.Should().Be("ianaIdentifier");
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void Create_EmptyStringIANA_ReturnsValidationFailed_RequiredViolation()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), string.Empty);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_WhitespaceOnlyIANA_ReturnsValidationFailed_RequiredViolation(
        string whitespace)
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), whitespace);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void Create_InvalidZoneName_ReturnsValidationFailed_InvalidIANA()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "Invalid/Zone");

        result.Success.Should().BeFalse();
        result.InputErrors[0].Field.Should().Be("ianaIdentifier");
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    [Theory]
    [InlineData("UTC+5")]
    [InlineData("GMT+05:00")]
    [InlineData("+05:00")]
    [InlineData("-08:00")]
    public void Create_FixedOffsetNotation_ReturnsValidationFailed_InvalidIANA(
        string fixedOffset)
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), fixedOffset);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    [Fact]
    public void Create_EtcGMTPlusN_IsAcceptedAsValidTzdbZone()
    {
        // Etc/GMT+N IS in tzdb (as fixed-offset IANA zones). Documents the
        // acceptance contract — IANA-only does NOT mean "no fixed-offset
        // zones", it means "no offset NOTATION as a substitute for IANA."
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "Etc/GMT+5");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("Etc/GMT+5");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("5")]
    [InlineData("+5")]
    public void Create_PlainNumericString_ReturnsValidationFailed_InvalidIANA(
        string numericString)
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), numericString);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    [Theory]
    [InlineData("america/new_york")]
    [InlineData("AMERICA/NEW_YORK")]
    public void Create_LowercaseCanonicalName_BehaviorDocumented(string nonCanonicalCase)
    {
        // NodaTime tzdb lookup IS case-sensitive — GetZoneOrNull returns null
        // for these. Document the contract: callers must pass the canonical
        // case form. Case-insensitive matching would be a different feature.
        var result = ZonedInstant.Create(
            Instant.FromUnixTimeSeconds(0),
            nonCanonicalCase);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    [Fact]
    public void Create_TrailingWhitespaceIANA_ReturnsValidationFailed()
    {
        // GetZoneOrNull does NOT trim — documents that callers must trim
        // BEFORE calling Create. Whitespace-bounded inputs that survive the
        // Falsey() guard (e.g. " UTC ") still fail at the tzdb lookup.
        var result = ZonedInstant.Create(
            Instant.FromUnixTimeSeconds(0),
            "America/New_York ");

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    // --- IANA normalization: acceptance + canonicalization ---

    [Fact]
    public void Create_DeprecatedAliasUSPacific_NormalizesToAmericaLosAngeles()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "US/Pacific");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/Los_Angeles");
    }

    [Fact]
    public void Create_DeprecatedAliasUSEastern_NormalizesToAmericaNewYork()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "US/Eastern");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/New_York");
    }

    [Fact]
    public void Create_DeprecatedAliasUSMountain_NormalizesToAmericaDenver()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "US/Mountain");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/Denver");
    }

    [Fact]
    public void Create_DeprecatedAliasUSCentral_NormalizesToAmericaChicago()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "US/Central");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/Chicago");
    }

    [Fact]
    public void Create_RenamedZoneAsiaSaigon_NormalizesToAsiaHoChiMinh()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "Asia/Saigon");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("Asia/Ho_Chi_Minh");
    }

    [Fact]
    public void Create_RenamedZoneAsiaCalcutta_NormalizesToAsiaKolkata()
    {
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "Asia/Calcutta");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("Asia/Kolkata");
    }

    [Fact]
    public void Create_AlreadyCanonicalIANA_StoresAsIs_AmericaEdmonton()
    {
        var result = ZonedInstant.Create(
            Instant.FromUnixTimeSeconds(0),
            "America/Edmonton");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/Edmonton");
    }

    [Fact]
    public void Create_AlreadyCanonicalIANA_StoresAsIs_EuropeLondon()
    {
        var result = ZonedInstant.Create(
            Instant.FromUnixTimeSeconds(0),
            "Europe/London");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("Europe/London");
    }

    [Fact]
    public void Create_ZuluUTCAlias_NormalizesToCanonicalUTC_BehaviorDocumented()
    {
        // The "Zulu" alias is in tzdb. NodaTime's TzdbDateTimeZoneSource
        // canonicalizes it to "Etc/UTC". This test documents the resolved
        // canonical name without hard-coding a specific casing for the
        // future-proofing case where the tzdb might change canonical naming.
        var result = ZonedInstant.Create(Instant.FromUnixTimeSeconds(0), "Zulu");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().BeOneOf("Etc/UTC", "UTC");
    }

    [Fact]
    public void RecordEquality_TwoFormsOfSameZoneViaAlias_AreEqual()
    {
        var instant = Instant.FromUnixTimeSeconds(12345);

        var aliasForm = ZonedInstant.Create(instant, "US/Pacific").Data!;
        var canonicalForm = ZonedInstant.Create(instant, "America/Los_Angeles").Data!;

        aliasForm.Should().Be(canonicalForm);
        aliasForm.GetHashCode().Should().Be(canonicalForm.GetHashCode());
    }

    // --- Instant value invariance / boundary ---

    [Fact]
    public void Create_EpochInstant_StoresCorrectly()
    {
        var epoch = Instant.FromUnixTimeSeconds(0);

        var result = ZonedInstant.Create(epoch, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.Instant.Should().Be(epoch);
    }

    [Fact]
    public void Create_FarFutureInstant_StoresCorrectly()
    {
        var farFuture = Instant.FromUtc(9000, 1, 1, 0, 0, 0);

        var result = ZonedInstant.Create(farFuture, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.Instant.Should().Be(farFuture);
    }

    [Fact]
    public void Create_FarPastInstant_StoresCorrectly()
    {
        var farPast = Instant.FromUtc(1700, 1, 1, 0, 0, 0);

        var result = ZonedInstant.Create(farPast, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.Instant.Should().Be(farPast);
    }

    [Fact]
    public void Create_MaxInstant_NoOverflow()
    {
        var result = ZonedInstant.Create(Instant.MaxValue, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.Instant.Should().Be(Instant.MaxValue);
    }

    [Fact]
    public void Create_MinInstant_NoUnderflow()
    {
        var result = ZonedInstant.Create(Instant.MinValue, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.Instant.Should().Be(Instant.MinValue);
    }

    [Fact]
    public void Create_SameInstantDifferentZones_InstantValueIdentical()
    {
        var instant = Instant.FromUnixTimeSeconds(1_700_000_000);

        var a = ZonedInstant.Create(instant, "America/New_York").Data!;
        var b = ZonedInstant.Create(instant, "Asia/Tokyo").Data!;

        a.Instant.Should().Be(b.Instant);
        a.IANAIdentifier.Should().NotBe(b.IANAIdentifier);
    }
}
