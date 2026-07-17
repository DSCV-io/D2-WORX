// -----------------------------------------------------------------------
// <copyright file="LocalAnchoredEventTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using System;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Time;
using NodaTime;
using Xunit;

public sealed class LocalAnchoredEventTests
{
    [Fact]
    public void Create_ValidScheduledLocalAndIANA_ReturnsOk_WithNullNextFireUtc()
    {
        var scheduled = new LocalDateTime(2026, 3, 14, 9, 0);

        var result = LocalAnchoredEvent.Create(scheduled, "Europe/Berlin");

        result.Success.Should().BeTrue();
        result.Data!.ScheduledLocal.Should().Be(scheduled);
        result.Data.IANAIdentifier.Should().Be("Europe/Berlin");
        result.Data.NextFireUtc.Should().BeNull();
    }

    [Fact]
    public void Create_WithExplicitNextFireUtc_StoresValue()
    {
        var scheduled = new LocalDateTime(2026, 6, 1, 8, 30);
        var next = Instant.FromUnixTimeSeconds(123456);

        var result = LocalAnchoredEvent.Create(scheduled, "America/Edmonton", next);

        result.Success.Should().BeTrue();
        result.Data!.NextFireUtc.Should().Be(next);
    }

    [Fact]
    public void Create_OmittingNextFireUtc_StoresNull()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            "UTC");

        result.Success.Should().BeTrue();
        result.Data!.NextFireUtc.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var scheduled = new LocalDateTime(2026, 1, 1, 12, 0);
        var next = Instant.FromUnixTimeSeconds(0);

        var a = LocalAnchoredEvent.Create(scheduled, "America/Edmonton", next).Data!;
        var b = LocalAnchoredEvent.Create(scheduled, "America/Edmonton", next).Data!;

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentScheduledLocal_AreNotEqual()
    {
        var a = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            "UTC").Data!;
        var b = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 2, 0, 0),
            "UTC").Data!;

        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentIANA_AreNotEqual()
    {
        var scheduled = new LocalDateTime(2026, 1, 1, 0, 0);

        var a = LocalAnchoredEvent.Create(scheduled, "America/Edmonton").Data!;
        var b = LocalAnchoredEvent.Create(scheduled, "America/Vancouver").Data!;

        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentNextFireUtc_AreNotEqual()
    {
        var scheduled = new LocalDateTime(2026, 1, 1, 0, 0);

        var a = LocalAnchoredEvent.Create(scheduled, "UTC").Data!;
        var b = LocalAnchoredEvent.Create(
            scheduled,
            "UTC",
            Instant.FromUnixTimeSeconds(1)).Data!;

        a.Should().NotBe(b);
    }

    [Fact]
    public void LocalAnchoredEvent_IsSealed()
    {
        typeof(LocalAnchoredEvent).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Create_AlreadyCanonicalIANA_StoresAsIs_AmericaArgentinaBuenosAires()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            "America/Argentina/Buenos_Aires");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/Argentina/Buenos_Aires");
    }

    // --- IANA validation: rejection ---

    [Fact]
    public void Create_NullIANA_ReturnsValidationFailed_RequiredViolation()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            null);

        result.Success.Should().BeFalse();
        result.InputErrors.Should().ContainSingle();
        result.InputErrors[0].Field.Should().Be("ianaIdentifier");
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void Create_EmptyStringIANA_ReturnsValidationFailed_RequiredViolation()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            string.Empty);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WhitespaceOnlyIANA_ReturnsValidationFailed_RequiredViolation(
        string whitespace)
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            whitespace);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void Create_InvalidZoneName_ReturnsValidationFailed_InvalidIANA()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            "Invalid/Zone");

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    [Theory]
    [InlineData("UTC+5")]
    [InlineData("+05:00")]
    public void Create_FixedOffsetNotation_ReturnsValidationFailed_InvalidIANA(
        string fixedOffset)
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            fixedOffset);

        result.Success.Should().BeFalse();
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    // --- IANA normalization: acceptance + canonicalization ---

    [Fact]
    public void Create_DeprecatedAliasUSPacific_NormalizesToAmericaLosAngeles()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            "US/Pacific");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("America/Los_Angeles");
    }

    [Fact]
    public void Create_RenamedZoneAsiaSaigon_NormalizesToAsiaHoChiMinh()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            "Asia/Saigon");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("Asia/Ho_Chi_Minh");
    }

    [Fact]
    public void Create_RenamedZoneAsiaCalcutta_NormalizesToAsiaKolkata()
    {
        var result = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 1, 1, 0, 0),
            "Asia/Calcutta");

        result.Success.Should().BeTrue();
        result.Data!.IANAIdentifier.Should().Be("Asia/Kolkata");
    }

    [Fact]
    public void RecordEquality_TwoFormsOfSameZoneViaAlias_AreEqual()
    {
        var scheduled = new LocalDateTime(2026, 1, 1, 0, 0);

        var aliasForm = LocalAnchoredEvent.Create(scheduled, "US/Pacific").Data!;
        var canonicalForm = LocalAnchoredEvent
            .Create(scheduled, "America/Los_Angeles")
            .Data!;

        aliasForm.Should().Be(canonicalForm);
        aliasForm.GetHashCode().Should().Be(canonicalForm.GetHashCode());
    }

    // --- LocalDateTime calendar edge cases (NodaTime constructor throws) ---

    [Fact]
    public void LocalDateTime_LeapDayInLeapYear2024_Feb29_DoesNotThrow_PassesToCreate()
    {
        // Year 2024 IS a leap year (div by 4, not div by 100).
        var scheduled = new LocalDateTime(2024, 2, 29, 9, 0);

        var result = LocalAnchoredEvent.Create(scheduled, "America/Edmonton");

        result.Success.Should().BeTrue();
        result.Data!.ScheduledLocal.Day.Should().Be(29);
    }

    [Fact]
    public void LocalDateTime_LeapDayInGregorianLeapEdge2000_Feb29_DoesNotThrow()
    {
        // Year 2000 IS a leap year (div by 400 — the Gregorian century-leap
        // exception to the "div by 100 not leap" rule).
        var scheduled = new LocalDateTime(2000, 2, 29, 12, 0);

        var result = LocalAnchoredEvent.Create(scheduled, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.ScheduledLocal.Day.Should().Be(29);
    }

    [Fact]
    public void LocalDateTime_LeapDayInNonLeapYear2025_Feb29_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2025, 2, 29, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_LeapDayInGregorianEdge1900_Feb29_LocalDateTimeThrows()
    {
        // Year 1900: div by 100 but NOT div by 400 → NOT a leap year (the
        // Gregorian exception to the "div by 4 is leap" rule).
        var act = () => _ = new LocalDateTime(1900, 2, 29, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_Feb30_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 2, 30, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_April31_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 4, 31, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_September31_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 9, 31, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_November31_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 11, 31, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_Month0_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 0, 1, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_Month13_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 13, 1, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_Day0_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 6, 0, 9, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_Hour24_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 6, 1, 24, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_Minute60_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(2026, 6, 1, 9, 60);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_Second60_NoLeapSecondSupport_LocalDateTimeThrows()
    {
        // NodaTime explicitly does not model leap seconds; the second-of-minute
        // domain is [0, 59]. This test documents the throw + the no-support
        // contract.
        var act = () => _ = new LocalDateTime(2026, 6, 1, 9, 30, 60);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LocalDateTime_YearMinNodaTime_DoesNotThrow_PassesToCreate()
    {
        // NodaTime ISO calendar supports years from -9998 to 9999. Lower bound
        // is min year that does NOT throw.
        var scheduled = new LocalDateTime(-9998, 1, 1, 0, 0);

        var result = LocalAnchoredEvent.Create(scheduled, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.ScheduledLocal.Year.Should().Be(-9998);
    }

    [Fact]
    public void LocalDateTime_YearMaxNodaTime_DoesNotThrow_PassesToCreate()
    {
        var scheduled = new LocalDateTime(9999, 12, 31, 23, 59);

        var result = LocalAnchoredEvent.Create(scheduled, "UTC");

        result.Success.Should().BeTrue();
        result.Data!.ScheduledLocal.Year.Should().Be(9999);
    }

    [Fact]
    public void LocalDateTime_YearOverflowAboveNodaMax_LocalDateTimeThrows()
    {
        var act = () => _ = new LocalDateTime(10_000, 1, 1, 0, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- ComputeNextFire happy paths ---

    [Fact]
    public void ComputeNextFire_UnambiguousLocalTimeUTC_ReturnsCorrectInstant()
    {
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 6, 15, 12, 0),
            "UTC").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
        fire.Data.Should().Be(Instant.FromUtc(2026, 6, 15, 12, 0, 0));
    }

    [Fact]
    public void ComputeNextFire_UnambiguousLocalTimeAmericaEdmonton_ReturnsCorrectInstant()
    {
        // June 15 2026 in Edmonton = MDT (UTC-6); 12:00 local = 18:00 UTC.
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 6, 15, 12, 0),
            "America/Edmonton").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
        fire.Data.Should().Be(Instant.FromUtc(2026, 6, 15, 18, 0, 0));
    }

    [Fact]
    public void ComputeNextFire_SameInputCalledTwice_ReturnsIdenticalInstant_Deterministic()
    {
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 6, 15, 12, 0),
            "America/New_York").Data!;

        var first = ev.ComputeNextFire();
        var second = ev.ComputeNextFire();

        first.Data.Should().Be(second.Data);
    }

    [Fact]
    public void ComputeNextFire_SameLocalDifferentZones_ProducesDifferentInstants()
    {
        var local = new LocalDateTime(2026, 6, 15, 12, 0);

        var ny = LocalAnchoredEvent.Create(local, "America/New_York").Data!;
        var la = LocalAnchoredEvent.Create(local, "America/Los_Angeles").Data!;

        var nyFire = ny.ComputeNextFire().Data;
        var laFire = la.ComputeNextFire().Data;

        nyFire.Should().NotBe(laFire);
        (laFire - nyFire).Should().Be(Duration.FromHours(3));
    }

    // --- ComputeNextFire DST spring-forward (skipped local times) ---

    [Fact]
    public void ComputeNextFire_USSpringForward_2_30AM_Skipped_LenientMapsForwardToPostGap()
    {
        // March 8 2026 = US DST spring-forward (second Sunday of March).
        // 2:00-3:00 AM EST doesn't exist. Lenient maps 2:30 AM forward by
        // the DST offset → 3:30 AM EDT clock = 7:30 UTC.
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 3, 8, 2, 30),
            "America/New_York").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
        fire.Data.Should().Be(Instant.FromUtc(2026, 3, 8, 7, 30, 0));
    }

    [Fact]
    public void ComputeNextFire_USSpringForwardSkippedTime_2_00AMExact_LenientMapsForward()
    {
        // Exact gap boundary — 2:00 AM is the first instant that doesn't exist.
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 3, 8, 2, 0),
            "America/New_York").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();

        // 2:00 AM EST + 1h DST offset = 3:00 AM EDT clock = 7:00 UTC.
        fire.Data.Should().Be(Instant.FromUtc(2026, 3, 8, 7, 0, 0));
    }

    [Fact]
    public void ComputeNextFire_EuropeanSpringForwardSkippedTime_LenientMapsForward()
    {
        // March 29 2026 = BST start in Europe/London (last Sunday of March).
        // 1:00-2:00 AM GMT doesn't exist. Lenient maps 1:30 AM forward
        // → 2:30 AM BST clock = 1:30 UTC.
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 3, 29, 1, 30),
            "Europe/London").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
        fire.Data.Should().Be(Instant.FromUtc(2026, 3, 29, 1, 30, 0));
    }

    [Fact]
    public void ComputeNextFire_SouthernHemisphereSpringForward_AustraliaSydney_LenientMapsForward()
    {
        // Australian DST starts first Sunday of October. October 2026: first
        // Sunday is Oct 4. 2:00-3:00 AM AEST doesn't exist on that day.
        // 2:30 AM AEST + 1h DST = 3:30 AM AEDT (UTC+11) → 16:30 UTC Oct 3
        // (Sydney is UTC+10 in standard time, UTC+11 in DST; 2:30 + the gap
        // resolves to 3:30 AEDT which is 16:30 the previous UTC day).
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 10, 4, 2, 30),
            "Australia/Sydney").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
        fire.Data.Should().Be(Instant.FromUtc(2026, 10, 3, 16, 30, 0));
    }

    // --- ComputeNextFire DST fall-back (ambiguous local times) ---

    [Fact]
    public void ComputeNextFire_USFallBackAmbiguousTime_1_30AM_LenientPicksEarlierInstant()
    {
        // November 1 2026 = US DST fall-back (first Sunday of November).
        // 1:00-2:00 AM occurs twice (once EDT, once EST). Lenient picks the
        // earlier (EDT) instant → 1:30 AM EDT = 5:30 UTC, NOT 1:30 AM EST
        // (which would be 6:30 UTC).
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2026, 11, 1, 1, 30),
            "America/New_York").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
        fire.Data.Should().Be(Instant.FromUtc(2026, 11, 1, 5, 30, 0));
    }

    [Fact]
    public void ComputeNextFire_EuropeanFallBackAmbiguousTime_LenientPicksEarlier()
    {
        // October 26 2025 = BST→GMT fall-back in Europe/London (last Sunday).
        // 1:00-2:00 AM occurs twice (BST then GMT). Lenient picks earlier
        // (BST) → 1:30 AM BST = 0:30 UTC.
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2025, 10, 26, 1, 30),
            "Europe/London").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
        fire.Data.Should().Be(Instant.FromUtc(2025, 10, 26, 0, 30, 0));
    }

    // --- ComputeNextFire historical tzdb edges ---

    [Fact]
    public void ComputeNextFire_EgyptHistoricalDSTAbolition_2011_UsesHistoricalRules()
    {
        // Egypt observed DST in 2010 (+03 in summer), abolished it in 2011,
        // and re-observed in some later years. For 2010, July 15 noon local
        // = +03 offset → 09:00 UTC. For 2012, no DST → +02 offset → 10:00
        // UTC. The delta is exactly 1 hour, proving the lib applies the
        // historically-correct rule for each year.
        var ev2010 = LocalAnchoredEvent.Create(
            new LocalDateTime(2010, 7, 15, 12, 0),
            "Africa/Cairo").Data!;
        var ev2012 = LocalAnchoredEvent.Create(
            new LocalDateTime(2012, 7, 15, 12, 0),
            "Africa/Cairo").Data!;

        var fire2010 = ev2010.ComputeNextFire().Data;
        var fire2012 = ev2012.ComputeNextFire().Data;

        // 2012 (no DST, +02) should be 1 hour LATER in UTC than 2010 (+03)
        // because +02 means UTC is 2h behind local, so local-12:00 = UTC-10:00.
        // Strip the year-2-year date delta and compare offsets via UTC hour.
        fire2010.InUtc().Hour.Should().Be(9);
        fire2012.InUtc().Hour.Should().Be(10);
    }

    [Fact]
    public void ComputeNextFire_SamoaDateLineShift_2011_LenientHandlesSkippedDay()
    {
        // Samoa skipped Dec 30 2011 entirely (UTC-11 → UTC+13). Any local
        // time on that date is non-existent. Lenient must not throw; it
        // resolves forward into a valid instant. The exact resolution is
        // implementation-defined; this test ensures the lib does NOT throw
        // on this insane edge case.
        var ev = LocalAnchoredEvent.Create(
            new LocalDateTime(2011, 12, 30, 12, 0),
            "Pacific/Apia").Data!;

        var fire = ev.ComputeNextFire();

        fire.Success.Should().BeTrue();
    }

    // --- ComputeNextFire cross-zone parity / determinism ---

    [Fact]
    public void ComputeNextFire_RecomputeMatchesStoredNextFireUtc_WhenSchedulingUnchanged()
    {
        var local = new LocalDateTime(2026, 6, 15, 12, 0);
        var bootstrap = LocalAnchoredEvent.Create(local, "America/Edmonton").Data!;
        var computed = bootstrap.ComputeNextFire().Data;

        var event2 = LocalAnchoredEvent
            .Create(local, "America/Edmonton", computed)
            .Data!;

        var recomputed = event2.ComputeNextFire().Data;
        event2.NextFireUtc.Should().Be(recomputed);
    }

    [Fact]
    public void Create_NormalizedIANAEqualityHoldsAfterNextFireUtcComputed()
    {
        var local = new LocalDateTime(2026, 6, 15, 12, 0);
        var bootstrap = LocalAnchoredEvent.Create(local, "America/Los_Angeles").Data!;
        var computed = bootstrap.ComputeNextFire().Data;

        var aliasInput = LocalAnchoredEvent
            .Create(local, "US/Pacific", computed)
            .Data!;
        var canonicalInput = LocalAnchoredEvent
            .Create(local, "America/Los_Angeles", computed)
            .Data!;

        aliasInput.Should().Be(canonicalInput);
    }
}
