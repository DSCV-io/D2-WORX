// -----------------------------------------------------------------------
// <copyright file="DemographicsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts;

using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Time;
using DcsvIo.D2.Validation.Abstractions;
using NodaTime;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="Demographics"/>: all-null rejection plus
/// the date-of-birth bounds. The "current date" is resolved from an injected
/// <see cref="TestClock"/> fixed to a known instant, so every boundary
/// (future, born-today, exactly-150-years, leap-day) is deterministic and never
/// depends on the wall clock.
/// </summary>
public sealed class DemographicsTests
{
    // "today" (UTC) is 2026-06-02 for every fixed-clock test in this class.
    private static readonly LocalDate sr_today = new(2026, 6, 2);

    // -----------------------------------------------------------------------
    // All-null degenerate record
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllNull_ReturnsDemographicsEmptyRecord()
    {
        var result = Demographics.Create(clock: FixedClock());

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.DEMOGRAPHICS_EMPTY_RECORD.Key);
    }

    // -----------------------------------------------------------------------
    // Single-field happy paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_BiologicalSexOnly_ReturnsOk()
    {
        var result = Demographics.Create(
            biologicalSex: BiologicalSex.Female, clock: FixedClock());

        result.Success.Should().BeTrue();
        result.Data!.BiologicalSex.Should().Be(BiologicalSex.Female);
        result.Data!.DateOfBirth.Should().BeNull();
    }

    [Fact]
    public void Create_DateOfBirthOnly_ReturnsOk()
    {
        var dob = new LocalDate(1990, 3, 15);
        var result = Demographics.Create(dob, clock: FixedClock());

        result.Success.Should().BeTrue();
        result.Data!.DateOfBirth.Should().Be(dob);
        result.Data!.BiologicalSex.Should().BeNull();
    }

    [Theory]
    [InlineData(BiologicalSex.Male)]
    [InlineData(BiologicalSex.Female)]
    [InlineData(BiologicalSex.Intersex)]
    [InlineData(BiologicalSex.Unspecified)]
    public void Create_EachBiologicalSexMember_ReturnsOk(BiologicalSex sex)
    {
        var result = Demographics.Create(biologicalSex: sex, clock: FixedClock());

        result.Success.Should().BeTrue();
        result.Data!.BiologicalSex.Should().Be(sex);
    }

    // -----------------------------------------------------------------------
    // DOB — future boundary
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_DateOfBirthTomorrow_ReturnsDobFuture()
    {
        var tomorrow = sr_today.PlusDays(1);
        var result = Demographics.Create(tomorrow, clock: FixedClock());

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.DOB_FUTURE.Key);
    }

    [Fact]
    public void Create_DateOfBirthToday_ReturnsOk_BornToday()
    {
        var result = Demographics.Create(sr_today, clock: FixedClock());

        result.Success.Should().BeTrue();
        result.Data!.DateOfBirth.Should().Be(sr_today);
    }

    // -----------------------------------------------------------------------
    // DOB — too-old boundary (150 years)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_DateOfBirthExactly150Years_ReturnsOk_FloorInclusive()
    {
        var floor = sr_today.PlusYears(-150);
        var result = Demographics.Create(floor, clock: FixedClock());

        result.Success.Should().BeTrue();
        result.Data!.DateOfBirth.Should().Be(floor);
    }

    [Fact]
    public void Create_DateOfBirthOneDayBeyond150Years_ReturnsDobTooOld()
    {
        var beyond = sr_today.PlusYears(-150).PlusDays(-1);
        var result = Demographics.Create(beyond, clock: FixedClock());

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.DOB_TOO_OLD.Key);
    }

    [Fact]
    public void Create_DateOfBirth151Years_ReturnsDobTooOld()
    {
        var old = sr_today.PlusYears(-151);
        var result = Demographics.Create(old, clock: FixedClock());

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.DOB_TOO_OLD.Key);
    }

    // -----------------------------------------------------------------------
    // DOB — leap-day adversarial
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_LeapDayDateOfBirth_ReturnsOk()
    {
        // Feb 29, 2000 is a valid leap-day in the past relative to the fixed clock.
        var leapDay = new LocalDate(2000, 2, 29);
        var result = Demographics.Create(leapDay, clock: FixedClock());

        result.Success.Should().BeTrue();
        result.Data!.DateOfBirth.Should().Be(leapDay);
    }

    [Fact]
    public void Create_LeapDayBornToday_OnLeapYearClock_ReturnsOk()
    {
        // A clock fixed to a leap day; a DOB equal to that day is the born-today edge.
        var leapClock = MakeClockAt(2024, 2, 29);
        var leapToday = new LocalDate(2024, 2, 29);

        var result = Demographics.Create(leapToday, clock: leapClock);

        result.Success.Should().BeTrue();
        result.Data!.DateOfBirth.Should().Be(leapToday);
    }

    // -----------------------------------------------------------------------
    // DOB — leap-day at 150y PlusYears boundary
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_LeapDayDob_JustUnder150Years_ReturnsOk()
    {
        // Clock: 2030-02-28 → floor = PlusYears(-150) = 1880-02-28.
        // DOB 1880-02-29 is NOT < floor 1880-02-28 → within the 150y window → Ok.
        // (1880 is a leap year, so 1880-02-29 is a valid calendar date.)
        var clock = MakeClockAt(2030, 2, 28);
        var dob = new LocalDate(1880, 2, 29);

        var result = Demographics.Create(dob, clock: clock);

        result.Success.Should().BeTrue();
        result.Data!.DateOfBirth.Should().Be(dob);
    }

    [Fact]
    public void Create_LeapDayDob_AtOrPast150Years_ReturnsDobTooOld()
    {
        // Clock: 2030-03-01 → floor = PlusYears(-150) = 1880-03-01.
        // DOB 1880-02-29 IS < floor 1880-03-01 → beyond the 150y window → DOB_TOO_OLD.
        var clock = MakeClockAt(2030, 3, 1);
        var dob = new LocalDate(1880, 2, 29);

        var result = Demographics.Create(dob, clock: clock);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.DOB_TOO_OLD.Key);
    }

    // -----------------------------------------------------------------------
    // DOB — year-boundary adversarial
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_YearBoundaryDecember31_ReturnsOk()
    {
        var dec31 = new LocalDate(2025, 12, 31);
        var result = Demographics.Create(dec31, clock: FixedClock());

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_YearBoundaryJanuary1_ReturnsOk()
    {
        var jan1 = new LocalDate(2026, 1, 1);
        var result = Demographics.Create(jan1, clock: FixedClock());

        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Both fields populated simultaneously
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_BothFields_ReturnsOk_AllFieldsPopulated()
    {
        var dob = new LocalDate(1985, 4, 12);
        var result = Demographics.Create(dob, BiologicalSex.Male, FixedClock());

        result.Success.Should().BeTrue();
        result.Data!.DateOfBirth.Should().Be(dob);
        result.Data!.BiologicalSex.Should().Be(BiologicalSex.Male);
    }

    // -----------------------------------------------------------------------
    // Default clock — no clock supplied (wall-clock path executes cleanly)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_DefaultClock_PastDateOfBirth_ReturnsOk()
    {
        var result = Demographics.Create(new LocalDate(1985, 7, 4));

        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Helpers — a deterministic fixed clock so every DOB boundary is reproducible
    // -----------------------------------------------------------------------

    private static TestClock FixedClock()
        => new(Instant.FromUtc(2026, 6, 2, 0, 0));

    private static TestClock MakeClockAt(int year, int month, int day)
        => new(Instant.FromUtc(year, month, day, 0, 0));
}
