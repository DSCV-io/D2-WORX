// -----------------------------------------------------------------------
// <copyright file="IsoDurationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Time;
using NodaTime;
using Xunit;

/// <summary>
/// Adversarial suite for the lossless ISO-8601 ↔ NodaTime
/// <see cref="Duration"/> bridge (<see cref="IsoDuration"/>). Exercises
/// whole-unit + decimal-fraction (nanosecond) round-tripping, the canonical
/// output form, the no-floating-point invariant, malformed-input fail-loud
/// (error-as-value, never a throw), and overflow handling.
/// </summary>
public sealed class IsoDurationTests
{
    // -------------------------------------------------------------------------
    // Parse — whole-unit forms round-trip to the exact value
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("PT1H30M", 90L * 60L * 1_000_000_000L)]
    [InlineData("P1DT2H3M4S", 93_784L * 1_000_000_000L)]
    [InlineData("PT45S", 45L * 1_000_000_000L)]
    [InlineData("PT0S", 0L)]
    [InlineData("P1D", 86_400L * 1_000_000_000L)]
    [InlineData("PT90M", 90L * 60L * 1_000_000_000L)]
    [InlineData("PT3600S", 3_600L * 1_000_000_000L)]
    [InlineData("PT26H", 26L * 3_600L * 1_000_000_000L)]
    public void Parse_WholeUnitForms_ReturnsExactNanoseconds(string iso, long expectedNanos)
    {
        var result = IsoDuration.Parse(iso);

        result.Success.Should().BeTrue();
        result.Data.ToInt64Nanoseconds().Should().Be(expectedNanos);
    }

    [Fact]
    public void Parse_UnbalancedComponents_AreAcceptedAndValueEquivalentToBalanced()
    {
        // "PT90M" (the Temporal-emitted unbalanced form) MUST parse to the same
        // value as its balanced "PT1H30M" rendering — value equality, not
        // string identity, is the cross-language contract.
        var unbalanced = IsoDuration.Parse("PT90M").Data;
        var balanced = IsoDuration.Parse("PT1H30M").Data;

        unbalanced.Should().Be(balanced);
    }

    // -------------------------------------------------------------------------
    // Parse — sub-second decimal-fraction seconds to nanosecond precision
    // (the load-bearing capability the NodaTime built-ins lack)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("PT0.123456789S", 123_456_789L)]
    [InlineData("PT0.000000001S", 1L)]
    [InlineData("PT0.5S", 500_000_000L)]
    [InlineData("PT0.1S", 100_000_000L)]
    [InlineData("PT1.000000001S", 1_000_000_001L)]
    [InlineData("PT23H59M59.999999999S", (((23L * 3_600L) + (59L * 60L) + 59L) * 1_000_000_000L) + 999_999_999L)]
    public void Parse_SubSecondDecimalFraction_IsNanosecondLossless(string iso, long expectedNanos)
    {
        var result = IsoDuration.Parse(iso);

        result.Success.Should().BeTrue();
        result.Data.ToInt64Nanoseconds().Should().Be(
            expectedNanos,
            $"'{iso}' must parse to exactly {expectedNanos} nanoseconds with no float-rounding loss");
    }

    [Fact]
    public void Parse_NanosecondFraction_EqualsFromNanosecondsConstruction()
    {
        // The parse path must produce the SAME Duration as a direct nanosecond
        // construction — proving the decimal-to-nanosecond conversion is exact
        // integer math (a right-pad), not a float multiply.
        var parsed = IsoDuration.Parse("PT0.123456789S").Data;

        parsed.Should().Be(Duration.FromNanoseconds(123_456_789L));
    }

    [Fact]
    public void Parse_ShortFraction_RightPadsToNanoseconds_NoFloatRounding()
    {
        // "0.1" seconds is exactly 100_000_000 ns. A float multiply
        // (0.1 * 1e9) is representable here, but "0.123456789" is NOT exactly
        // representable as a double — this case pins that the conversion does
        // not route through a float at all.
        IsoDuration.Parse("PT0.1S").Data.ToInt64Nanoseconds().Should().Be(100_000_000L);
        IsoDuration.Parse("PT0.123456789S").Data.ToInt64Nanoseconds().Should().Be(123_456_789L);
    }

    // -------------------------------------------------------------------------
    // Parse — negative durations
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("-PT1H", -3_600L * 1_000_000_000L)]
    [InlineData("-PT0.5S", -500_000_000L)]
    [InlineData("-P1DT2H3M4S", -93_784L * 1_000_000_000L)]
    public void Parse_NegativeDuration_ReturnsExactNegativeValue(string iso, long expectedNanos)
    {
        var result = IsoDuration.Parse(iso);

        result.Success.Should().BeTrue();
        result.Data.ToInt64Nanoseconds().Should().Be(expectedNanos);
    }

    // -------------------------------------------------------------------------
    // Format — canonical output
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_Zero_RendersPt0S()
    {
        IsoDuration.Format(Duration.Zero).Should().Be("PT0S");
    }

    [Theory]
    [InlineData(123_456_789L, "PT0.123456789S")]
    [InlineData(500_000_000L, "PT0.5S")]
    [InlineData(100_000_000L, "PT0.1S")]
    [InlineData(1L, "PT0.000000001S")]
    [InlineData(1_000_000_000L, "PT1S")]
    [InlineData(45L * 1_000_000_000L, "PT45S")]
    public void Format_SubSecondAndSeconds_TrimsTrailingFractionZeros(long nanos, string expected)
    {
        IsoDuration.Format(Duration.FromNanoseconds(nanos)).Should().Be(expected);
    }

    [Theory]
    [InlineData(90L * 60L * 1_000_000_000L, "PT1H30M")]
    [InlineData(93_784L * 1_000_000_000L, "PT26H3M4S")]
    [InlineData(3_600L * 1_000_000_000L, "PT1H")]
    public void Format_BalancesIntoHoursMinutesSeconds(long nanos, string expected)
    {
        IsoDuration.Format(Duration.FromNanoseconds(nanos)).Should().Be(expected);
    }

    [Fact]
    public void Format_Negative_CarriesLeadingMinus()
    {
        IsoDuration.Format(Duration.FromNanoseconds(-500_000_000L)).Should().Be("-PT0.5S");
        IsoDuration.Format(Duration.FromHours(-1)).Should().Be("-PT1H");
    }

    // -------------------------------------------------------------------------
    // Round-trip — Parse ∘ Format and Format ∘ Parse
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("PT1H30M")]
    [InlineData("P1DT2H3M4S")]
    [InlineData("PT45S")]
    [InlineData("PT0.123456789S")]
    [InlineData("PT0.5S")]
    [InlineData("PT0S")]
    [InlineData("PT23H59M59.999999999S")]
    [InlineData("-PT1H")]
    public void RoundTrip_ParseThenFormatThenParse_PreservesValue(string iso)
    {
        var first = IsoDuration.Parse(iso);
        first.Success.Should().BeTrue();

        var formatted = IsoDuration.Format(first.Data);
        var second = IsoDuration.Parse(formatted);

        second.Success.Should().BeTrue();
        second.Data.Should().Be(first.Data, $"'{iso}' must survive a Parse→Format→Parse round-trip");
    }

    [Fact]
    public void RoundTrip_SubSecondDecimal_IsByteStable()
    {
        // The sub-second-only case has no larger unit to balance into, so the
        // canonical Format output is byte-identical to the input — this is the
        // exact lossless contract the flipped AD-9 cross-language test asserts.
        const string iso = "PT0.123456789S";

        var parsed = IsoDuration.Parse(iso).Data;
        IsoDuration.Format(parsed).Should().Be(iso);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(123_456_789L)]
    [InlineData(500_000_000L)]
    [InlineData(86_400L * 1_000_000_000L)]
    [InlineData(-123_456_789L)]
    public void RoundTrip_FormatThenParse_PreservesValue(long nanos)
    {
        var original = Duration.FromNanoseconds(nanos);

        var formatted = IsoDuration.Format(original);
        var reparsed = IsoDuration.Parse(formatted);

        reparsed.Success.Should().BeTrue();
        reparsed.Data.Should().Be(original);
    }

    // -------------------------------------------------------------------------
    // Malformed input — fail-loud as error-as-value (NEVER a throw)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_NullEmptyOrWhitespace_ReturnsValidationFailed(string? iso)
    {
        var result = IsoDuration.Parse(iso);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Field.Should().Be("iso");
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_DURATION.Key);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("P")] // no component
    [InlineData("PT")] // empty time section
    [InlineData("-P")]
    [InlineData("-PT")]
    [InlineData("1H30M")] // missing leading P
    [InlineData("PT1H30")] // trailing number with no designator
    [InlineData("P1Y")] // year designator unsupported (calendar-relative)
    [InlineData("P1M")] // month designator unsupported (ambiguous with minutes; calendar-relative)
    [InlineData("P1W")] // week designator unsupported
    [InlineData("P1YT1H")]
    [InlineData("PT1.S")] // decimal point with no fraction digits
    [InlineData("PT0.1234567890S")] // 10 fraction digits — beyond nanosecond resolution
    [InlineData("PT1H30M S")] // embedded whitespace
    [InlineData("PT+1H")] // sign on a component
    [InlineData("PT1.5H")] // fractional hours unsupported (only seconds may be fractional)
    [InlineData("PT1,5S")] // comma decimal separator
    [InlineData("Q1H")]
    [InlineData("PT1HX")]
    public void Parse_Malformed_ReturnsValidationFailed_NotThrow(string iso)
    {
        // Error-as-value: a malformed wire string must surface a wrapped
        // failure carrying INVALID_DURATION — never throw.
        var act = () => IsoDuration.Parse(iso);

        var result = act.Should().NotThrow().Subject;
        result.Success.Should().BeFalse($"'{iso}' is not a valid ISO-8601 duration");
        result.InputErrors[0].Field.Should().Be("iso");
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_DURATION.Key);
    }

    // -------------------------------------------------------------------------
    // Overflow — out-of-range magnitudes fail-loud, no throw
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_OverflowingDayCount_ReturnsValidationFailed_NotThrow()
    {
        // A days field far beyond NodaTime's representable Duration range
        // (~104 million days) must surface a wrapped failure, not throw.
        var act = () => IsoDuration.Parse("P999999999999999999999D");

        var result = act.Should().NotThrow().Subject;
        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_DURATION.Key);
    }

    [Fact]
    public void Parse_MaxRepresentableMagnitude_WithinInt64Nanoseconds_RoundTrips()
    {
        // ~292 years is the Int64-nanosecond ceiling; a value just inside it
        // round-trips losslessly (proves the int64 path is the lossless floor,
        // not an artificial cap).
        var nearMax = Duration.FromNanoseconds(long.MaxValue);

        var formatted = IsoDuration.Format(nearMax);
        var reparsed = IsoDuration.Parse(formatted);

        reparsed.Success.Should().BeTrue();
        reparsed.Data.Should().Be(nearMax);
    }

    // -------------------------------------------------------------------------
    // No-float invariant — a value not exactly representable as a double
    // survives the full pipeline (would drift if any double math existed)
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ValueNotExactlyRepresentableAsDouble_SurvivesExactly()
    {
        // 0.123456789 s is NOT exactly representable as an IEEE-754 double; if
        // any step routed through a double the nanosecond count would drift off
        // 123_456_789. The integer (right-pad) path keeps it exact.

        for (long ns = 100_000_000L; ns <= 999_999_999L; ns += 111_111_111L)
        {
            var iso = IsoDuration.Format(Duration.FromNanoseconds(ns));
            IsoDuration.Parse(iso).Data.ToInt64Nanoseconds().Should().Be(ns);
        }
    }
}
