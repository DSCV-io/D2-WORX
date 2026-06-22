// -----------------------------------------------------------------------
// <copyright file="TemporalRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecDto;

using System.IO;
using System.Text.Json;
using D2.Shared.I18n;
using NodaTime.Text;
using NodaTime.TimeZones;
using GenTemporal = D2.Edge.Tests.TypeSpecDto.Generated;

/// <summary>
/// Temporal-adversarial round-trip suite for the TypeSpec-emitted temporal DTOs.
/// Each test maps a domain value to the emitted WIRE DTO shape
/// (<see cref="DateTimeOffset"/> / offset-free ISO string / ISO-8601 duration)
/// and back, asserting nothing is lost — the same lossless contract the handler
/// body implements via <c>D2.Shared.Time</c> smart constructors + the NodaTime
/// pattern set. The composite cases prove the IANA zone NAME survives the wire
/// (a bare <see cref="DateTimeOffset"/> carries an offset but NOT the IANA id).
///
/// These exercise the REAL <c>D2.Shared.Time</c> seams (<see cref="ZonedInstant"/>,
/// <see cref="LocalAnchoredEvent"/>) and the REAL generated wire records in
/// <c>D2.Edge.Tests.TypeSpecDto.Generated</c> — no test doubles. The TypeScript
/// half (<c>temporal-round-trip.test.ts</c>) drives the SAME
/// <c>contracts/temporal/temporal-adversarial.fixture.json</c>, so an identical
/// wire string materializes to the equivalent domain value in both languages.
/// </summary>
public sealed class TemporalRoundTripTests
{
    private static readonly JsonSerializerOptions sr_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // -------------------------------------------------------------------------
    // RT-1..6 — per-type round-trip (domain → wire → domain == original)
    // -------------------------------------------------------------------------

    [Fact]
    public void RT1_UtcDateTime_InstantRoundTripsThroughDateTimeOffsetWire()
    {
        foreach (var fx in ScalarFixtures("instant"))
        {
            var original = ParseInstant(fx.Wire);

            // Domain → wire (DateTimeOffset, "O") → domain.
            var wire = original.ToDateTimeOffset();
            var json = JsonSerializer.Serialize(wire);
            var parsedWire = JsonSerializer.Deserialize<DateTimeOffset>(json);
            var roundTripped = Instant.FromDateTimeOffset(parsedWire);

            roundTripped.Should().Be(original, $"fixture '{fx.Id}' instant must survive the DateTimeOffset wire");
        }
    }

    [Fact]
    public void RT2_OffsetDateTime_PreservesOffsetAndInstant_NotNormalizedToUtc()
    {
        foreach (var fx in ScalarFixtures("offset"))
        {
            var original = OffsetDateTimePattern.Rfc3339.Parse(fx.Wire).Value;

            // Domain → wire (DateTimeOffset, "O" preserves the offset) → domain.
            var wire = original.ToDateTimeOffset();
            var json = JsonSerializer.Serialize(wire);
            var parsedWire = JsonSerializer.Deserialize<DateTimeOffset>(json);
            var roundTripped = OffsetDateTime.FromDateTimeOffset(parsedWire);

            // The offset is preserved (NOT normalized to +00:00).
            roundTripped.Offset.Should().Be(original.Offset, $"fixture '{fx.Id}' offset must NOT be normalized to UTC");
            roundTripped.Offset.Ticks.Should().NotBe(0, "the test fixture uses a non-UTC offset");

            // And the underlying instant agrees.
            roundTripped.ToInstant().Should().Be(original.ToInstant(), $"fixture '{fx.Id}' instant must survive");

            // Cross-check the declared offset matches the fixture's expected minutes.
            (roundTripped.Offset.Milliseconds / 60_000).Should().Be(fx.ExpectedOffsetMinutes!.Value);
        }
    }

    [Fact]
    public void RT3_PlainDate_LocalDateRoundTripsOffsetFree()
    {
        foreach (var fx in ScalarFixtures("date"))
        {
            var original = LocalDatePattern.Iso.Parse(fx.Wire).Value;

            var wire = LocalDatePattern.Iso.Format(original);
            var roundTripped = LocalDatePattern.Iso.Parse(wire).Value;

            roundTripped.Should().Be(original, $"fixture '{fx.Id}' date must survive");

            // AD-10 — no invented offset / zone marker on a date wire string.
            wire.Should().NotContainAny("+", "Z");
            wire.Should().Be(fx.Wire);
        }
    }

    [Fact]
    public void RT4_PlainTime_LocalTimeRoundTripsOffsetFree()
    {
        foreach (var fx in ScalarFixtures("time"))
        {
            var original = LocalTimePattern.ExtendedIso.Parse(fx.Wire).Value;

            var wire = LocalTimePattern.ExtendedIso.Format(original);
            var roundTripped = LocalTimePattern.ExtendedIso.Parse(wire).Value;

            roundTripped.Should().Be(original, $"fixture '{fx.Id}' time must survive");

            // AD-10 — offset-free.
            wire.Should().NotContainAny("+", "Z");
        }
    }

    [Fact]
    public void RT5_PlainDateTime_LocalDateTimeRoundTripsOffsetFree()
    {
        foreach (var fx in ScalarFixtures("localDateTime"))
        {
            var original = LocalDateTimePattern.ExtendedIso.Parse(fx.Wire).Value;

            var wire = LocalDateTimePattern.ExtendedIso.Format(original);
            var roundTripped = LocalDateTimePattern.ExtendedIso.Parse(wire).Value;

            roundTripped.Should().Be(original, $"fixture '{fx.Id}' wall-clock must survive");

            // AD-10 — the wall-clock string carries NO offset (inventing one corrupts it).
            wire.Should().NotContainAny("+", "Z");
        }
    }

    [Fact]
    public void RT6_Duration_RoundTripsViaIso8601()
    {
        foreach (var fx in ScalarFixtures("duration"))
        {
            // The wire is ISO-8601 "P…T…" (cross-language with Temporal.Duration).
            // NodaTime has no built-in ISO-8601 Duration pattern, so the .NET path
            // bridges via the D2.Shared.Time IsoDuration helper (int64-nanosecond,
            // no float) — lossless including sub-second decimal-fraction seconds.
            var original = ParseIsoDuration(fx.Wire);

            var wire = FormatIsoDuration(original);
            var roundTripped = ParseIsoDuration(wire);

            roundTripped.Should().Be(original, $"fixture '{fx.Id}' duration must survive the ISO-8601 wire");
        }
    }

    // -------------------------------------------------------------------------
    // RT-7 — ZonedInstantWire composite (IANA NAME survives — the load-bearing proof)
    // -------------------------------------------------------------------------

    [Fact]
    public void RT7_ZonedInstantWire_RoundTrips_AndIanaNameSurvives()
    {
        var instant = ParseInstant("2026-05-27T16:30:00Z");
        const string iana = "America/Los_Angeles";

        var domain = ZonedInstant.Create(instant, iana).Data!;

        // Domain → emitted wire record → domain (mirrors the handler body).
        var wire = new GenTemporal.ZonedInstantWire(domain.Instant.ToDateTimeOffset(), domain.IANAIdentifier);
        var roundTripped = ZonedInstant
            .Create(Instant.FromDateTimeOffset(wire.Instant), wire.ZoneId)
            .Data!;

        roundTripped.Instant.Should().Be(domain.Instant, "the instant must survive the wire");

        // THE load-bearing assertion: the IANA NAME survives — not merely the offset.
        // A broken impl that dropped the zone (kept only the DateTimeOffset) would pass
        // an instant-only check but FAIL here.
        roundTripped.IANAIdentifier.Should().Be(iana, "the canonical IANA name must survive the ZonedInstant round-trip");
        wire.ZoneId.Should().Be("America/Los_Angeles", "the wire must carry the IANA name, not just an offset");
    }

    [Fact]
    public void RT7_ZonedInstantWire_AliasInput_CanonicalIanaNameSurvives()
    {
        var instant = ParseInstant("2026-06-15T19:00:00Z");

        // Input a deprecated alias — the canonical name must survive the wire.
        var domain = ZonedInstant.Create(instant, "US/Pacific").Data!;
        domain.IANAIdentifier.Should().Be("America/Los_Angeles");

        var wire = new GenTemporal.ZonedInstantWire(domain.Instant.ToDateTimeOffset(), domain.IANAIdentifier);
        var roundTripped = ZonedInstant
            .Create(Instant.FromDateTimeOffset(wire.Instant), wire.ZoneId)
            .Data!;

        roundTripped.IANAIdentifier.Should().Be("America/Los_Angeles", "the canonical IANA name (not the alias) survives");
    }

    // -------------------------------------------------------------------------
    // RT-8 — LocalAnchoredEventWire composite (all 3 fields + recomputed next-fire)
    // -------------------------------------------------------------------------

    [Fact]
    public void RT8_LocalAnchoredEventWire_RoundTrips_AllFieldsAndNextFireAgree()
    {
        var local = ParseLocalDateTime("2026-03-08T02:30:00");
        const string iana = "America/New_York";
        var nextFire = LocalAnchoredEvent.Create(local, iana).Data!.ComputeNextFire().Data;

        var domain = LocalAnchoredEvent.Create(local, iana, nextFire).Data!;

        // Domain → emitted wire record → domain.
        var wire = new GenTemporal.LocalAnchoredEventWire(
            LocalDateTimePattern.ExtendedIso.Format(domain.ScheduledLocal),
            domain.IANAIdentifier,
            domain.NextFireUtc!.Value.ToDateTimeOffset());

        var roundTripped = LocalAnchoredEvent.Create(
            LocalDateTimePattern.ExtendedIso.Parse(wire.ScheduledLocal).Value,
            wire.IanaZone,
            Instant.FromDateTimeOffset(wire.NextFireUtc!.Value)).Data!;

        roundTripped.ScheduledLocal.Should().Be(domain.ScheduledLocal);
        roundTripped.IANAIdentifier.Should().Be(iana);
        roundTripped.NextFireUtc.Should().Be(domain.NextFireUtc);

        // The recomputed next-fire agrees (DST-correct).
        roundTripped.ComputeNextFire().Data.Should().Be(domain.NextFireUtc!.Value);
    }

    // -------------------------------------------------------------------------
    // AD-1..3 — DST gap / overlap across US / Europe / Australia (fixture-driven)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("us-spring-forward-skipped-2-30")]
    [InlineData("us-fall-back-ambiguous-1-30-picks-earlier")]
    [InlineData("european-spring-forward-skipped")]
    [InlineData("european-fall-back-ambiguous-picks-earlier")]
    [InlineData("australian-spring-forward-skipped")]
    public void AD1to3_DstGapAndOverlap_ComputeNextFireMatchesFixture(string fixtureId)
    {
        var fx = LoadCompositeFixture(fixtureId);

        var domain = LocalAnchoredEvent.Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana).Data!;

        // Round-trip the wall-clock + iana through the wire, then recompute next-fire.
        var wire = new GenTemporal.LocalAnchoredEventWire(
            LocalDateTimePattern.ExtendedIso.Format(domain.ScheduledLocal),
            domain.IANAIdentifier,
            NextFireUtc: null);
        var roundTripped = LocalAnchoredEvent.Create(
            LocalDateTimePattern.ExtendedIso.Parse(wire.ScheduledLocal).Value,
            wire.IanaZone).Data!;

        roundTripped.ComputeNextFire().Data.Should().Be(
            ParseInstant(fx.ExpectedUtc),
            $"fixture '{fx.Id}' must resolve to the DST-correct UTC, identical to the TS engine");
    }

    // -------------------------------------------------------------------------
    // AD-4 — invalid IANA → ValidationFailed (NOT a throw)
    // -------------------------------------------------------------------------

    [Fact]
    public void AD4_InvalidIana_ReturnsValidationFailed_NotThrow()
    {
        var instant = ParseInstant("2026-05-27T14:30:00Z");
        var local = ParseLocalDateTime("2026-05-27T09:00:00");

        // Create returns a wrapped failure (error-as-value) — never throws — and
        // carries the INVALID_IANA_IDENTIFIER key + the 400 status.
        var zi = ZonedInstant.Create(instant, "Not/AZone");
        zi.Success.Should().BeFalse();
        zi.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        zi.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);

        var lae = LocalAnchoredEvent.Create(local, "Not/AZone");
        lae.Success.Should().BeFalse();
        lae.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        lae.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    // -------------------------------------------------------------------------
    // AD-5 — fixed-offset-vs-IANA: a fixed offset is REJECTED as an IANA id
    // (the core reason the composite exists). The IANA-name survival is RT-7.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("+05:00")]
    [InlineData("-08:00")]
    [InlineData("UTC+5")]
    public void AD5_FixedOffsetNotation_RejectedAsIana(string fixedOffset)
    {
        var instant = ParseInstant("2026-05-27T14:30:00Z");

        var result = ZonedInstant.Create(instant, fixedOffset);

        result.Success.Should().BeFalse($"'{fixedOffset}' is a fixed offset, not an IANA zone name");
        result.InputErrors[0].Errors.Should().ContainSingle(
            m => m.Key == TK.Common.Time.INVALID_IANA_IDENTIFIER.Key);
    }

    // -------------------------------------------------------------------------
    // AD-6 — IANA alias normalization survives the wire (canonical name)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("iana-normalization-us-pacific-alias", "America/Los_Angeles")]
    [InlineData("iana-normalization-asia-saigon-renamed", "Asia/Ho_Chi_Minh")]
    public void AD6_IanaAlias_NormalizesToCanonical_AndSurvivesWire(string fixtureId, string expectedCanonical)
    {
        var fx = LoadCompositeFixture(fixtureId);

        var domain = LocalAnchoredEvent.Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana).Data!;
        domain.IANAIdentifier.Should().Be(expectedCanonical);

        // Survives the wire round-trip byte-identically.
        var wire = new GenTemporal.LocalAnchoredEventWire(
            LocalDateTimePattern.ExtendedIso.Format(domain.ScheduledLocal),
            domain.IANAIdentifier,
            NextFireUtc: null);
        var roundTripped = LocalAnchoredEvent.Create(
            LocalDateTimePattern.ExtendedIso.Parse(wire.ScheduledLocal).Value,
            wire.IanaZone).Data!;

        roundTripped.IANAIdentifier.Should().Be(expectedCanonical, "the canonical IANA name survives the wire");
        roundTripped.ComputeNextFire().Data.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    // -------------------------------------------------------------------------
    // AD-7 — leap year / leap day / impossible calendar date
    // -------------------------------------------------------------------------

    [Fact]
    public void AD7_LeapDay_Feb29_ValidInLeapYear_RoundTrips()
    {
        // Feb 29 2024 is a valid leap day.
        var leap = new LocalDateTime(2024, 2, 29, 12, 0, 0);

        var wire = LocalDateTimePattern.ExtendedIso.Format(leap);
        var roundTripped = LocalDateTimePattern.ExtendedIso.Parse(wire).Value;

        roundTripped.Should().Be(leap);
        wire.Should().Be("2024-02-29T12:00:00");
    }

    [Fact]
    public void AD7_ImpossibleCalendarDate_ThrowsAtConstructionSite()
    {
        // Feb 29 in a non-leap year — NodaTime's LocalDateTime ctor throws
        // ArgumentOutOfRangeException at the construction site, BEFORE Create is
        // reached. The emitter does NOT change this (documented behavior). The TS
        // counterpart throws RangeError — the divergence is documented in AD-7 (TS),
        // each language asserting its own documented behavior.
        var act = () => new LocalDateTime(2026, 2, 29, 12, 0, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -------------------------------------------------------------------------
    // AD-8 — year boundary + min/max representable instant
    // -------------------------------------------------------------------------

    [Fact]
    public void AD8_YearBoundary_And_MinMaxInstant_RoundTripThroughWire()
    {
        // Year boundary (also covered by the fixture).
        var boundary = ParseInstant("2025-12-31T23:59:59.9999999Z");
        Instant.FromDateTimeOffset(boundary.ToDateTimeOffset()).Should().Be(boundary);

        // Min / max representable DateTimeOffset instants survive the wire.
        var minDto = DateTimeOffset.MinValue;
        var maxDto = DateTimeOffset.MaxValue;
        var minJson = JsonSerializer.Serialize(minDto);
        var maxJson = JsonSerializer.Serialize(maxDto);
        JsonSerializer.Deserialize<DateTimeOffset>(minJson).Should().Be(minDto);
        JsonSerializer.Deserialize<DateTimeOffset>(maxJson).Should().Be(maxDto);
    }

    // -------------------------------------------------------------------------
    // AD-9 — sub-second precision (Duration nanosecond + Instant 100ns tick)
    // -------------------------------------------------------------------------

    [Fact]
    public void AD9_Duration_NanosecondValue_IsLosslessAtTheDomainLevel()
    {
        // The NodaTime Duration VALUE is nanosecond-lossless: a sub-second nanosecond
        // duration survives the FromNanoseconds → ToInt64Nanoseconds round-trip exactly.
        var nanos = Duration.FromNanoseconds(123_456_789L);
        nanos.ToInt64Nanoseconds().Should().Be(123_456_789L, "Duration is nanosecond-precise");
    }

    [Fact]
    public void AD9_Duration_SubSecondIsoDecimalNotation_RoundTripsLossless_BothLanguages()
    {
        // LOSSLESS (was a surfaced boundary; now bridged by the D2.Shared.Time
        // IsoDuration helper). ISO-8601 allows a decimal fraction on the seconds
        // field ("PT0.123456789S"); TS Temporal.Duration round-trips that natively
        // and the .NET helper now matches — computing total nanoseconds as int64
        // (NO float), so the wire string stays an ISO-8601 STRING that materializes
        // to the equivalent Duration value in BOTH languages.
        const string iso = "PT0.123456789S";

        var parsed = ParseIsoDuration(iso);
        parsed.ToInt64Nanoseconds().Should().Be(
            123_456_789L,
            "the sub-second decimal-fraction seconds parse to the exact nanosecond count");

        // Sub-second-only has no larger unit to balance into, so the canonical
        // Format output is byte-identical — "PT0.123456789S" → Duration →
        // "PT0.123456789S" exactly.
        FormatIsoDuration(parsed).Should().Be(iso);

        // Cross-language parity: the TS half (temporal-round-trip.test.ts AD-9)
        // asserts Temporal.Duration.from(iso).total({unit:"nanoseconds"}) === the
        // SAME 123_456_789 value from the SAME shared fixture, so an identical
        // wire string materializes to the equivalent domain value in both.
        var fixtureWire = ScalarFixtures("duration")
            .Single(fx => fx.Id == "duration-subsecond-nanos")
            .Wire;
        fixtureWire.Should().Be(iso, "both language suites drive this shared fixture string");
        ParseIsoDuration(fixtureWire).ToInt64Nanoseconds().Should().Be(123_456_789L);
    }

    [Fact]
    public void AD9_Duration_HmsWithSubSecond_RoundTripsLossless()
    {
        // A combined hours/minutes/seconds + fractional duration round-trips
        // losslessly by VALUE (the cross-language contract is total-nanosecond
        // equality, not byte-identical strings — a Duration carries no authored-
        // unit memory). The shared fixture drives the TS half identically.
        const string iso = "PT1H2M3.123456789S";
        var expectedNanos = (((1L * 3_600L) + (2L * 60L) + 3L) * 1_000_000_000L) + 123_456_789L;

        var parsed = ParseIsoDuration(iso);
        parsed.ToInt64Nanoseconds().Should().Be(expectedNanos);

        // Format → re-parse preserves the value.
        ParseIsoDuration(FormatIsoDuration(parsed)).Should().Be(parsed);

        var fixtureWire = ScalarFixtures("duration")
            .Single(fx => fx.Id == "duration-hms-with-subsecond")
            .Wire;
        ParseIsoDuration(fixtureWire).ToInt64Nanoseconds().Should().Be(expectedNanos);
    }

    [Fact]
    public void AD9_Instant_SevenDigitFractional_SurvivesDateTimeOffsetWire()
    {
        // DateTimeOffset "O" carries 7-digit fractional seconds (100ns ticks).
        var instant = ParseInstant("2026-03-08T07:30:00.1234567Z");
        var wire = instant.ToDateTimeOffset();
        var json = JsonSerializer.Serialize(wire);
        var roundTripped = Instant.FromDateTimeOffset(JsonSerializer.Deserialize<DateTimeOffset>(json));

        roundTripped.Should().Be(instant, "100ns-tick fractional seconds survive the DateTimeOffset wire");
    }

    // -------------------------------------------------------------------------
    // AD-10 — no-invented-offset on plain-local types (asserted inline in RT-3/4/5
    // above; this consolidates the contract across every plain-local fixture).
    // -------------------------------------------------------------------------

    [Fact]
    public void AD10_PlainLocalWireStrings_CarryNoOffset()
    {
        foreach (var fx in ScalarFixtures("date").Concat(ScalarFixtures("time")).Concat(ScalarFixtures("localDateTime")))
        {
            fx.OffsetFree.Should().BeTrue($"fixture '{fx.Id}' is a plain-local kind and must be offset-free");
            fx.Wire.Should().NotContainAny("+", "Z");

            // A '-' in a date ("2026-11-01") is a separator, not an offset sign — confirm
            // no trailing offset by re-parsing and re-formatting offset-free.
            fx.Wire.Should().NotContain("+00:00");
        }
    }

    // -------------------------------------------------------------------------
    // AD-11 — optional nextFireUtc null → C# DateTimeOffset? null round-trips
    // -------------------------------------------------------------------------

    [Fact]
    public void AD11_OptionalNextFireUtc_Absent_RoundTripsAsNull()
    {
        var local = ParseLocalDateTime("2026-06-15T12:00:00");
        var domain = LocalAnchoredEvent.Create(local, "UTC").Data!;
        domain.NextFireUtc.Should().BeNull();

        // Wire carries a null DateTimeOffset? for the absent optional.
        var wire = new GenTemporal.LocalAnchoredEventWire(
            LocalDateTimePattern.ExtendedIso.Format(domain.ScheduledLocal),
            domain.IANAIdentifier,
            NextFireUtc: null);
        wire.NextFireUtc.Should().BeNull();

        var roundTripped = LocalAnchoredEvent.Create(
            LocalDateTimePattern.ExtendedIso.Parse(wire.ScheduledLocal).Value,
            wire.IanaZone,
            wire.NextFireUtc is null ? null : Instant.FromDateTimeOffset(wire.NextFireUtc.Value)).Data!;

        roundTripped.NextFireUtc.Should().BeNull("the absent optional round-trips to null");
    }

    [Fact]
    public void AD11_OptionalInstantField_Null_SerializesAndRoundTrips()
    {
        // The TemporalInput.OptionalInstant wire field is DateTimeOffset? — a null
        // value serializes as JSON null and round-trips to null (the C# half of the
        // null→undefined contract; TS normalizes JSON null to undefined).
        DateTimeOffset? absent = null;
        var json = JsonSerializer.Serialize(absent);
        json.Should().Be("null");
        JsonSerializer.Deserialize<DateTimeOffset?>(json).Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // AD-12 — historical tzdb offset change (pre-DST-era date)
    // -------------------------------------------------------------------------

    [Fact]
    public void AD12_HistoricalTzdbOffset_ResolvesForThatDate_RoundTrips()
    {
        // A 1950 New York date predates the modern DST schedule; NodaTime's tzdb
        // resolves the historically-correct offset for that date. The wall-clock +
        // iana survive the wire and ComputeNextFire uses the tzdb-correct offset.
        var local = ParseLocalDateTime("1950-06-15T12:00:00");
        var domain = LocalAnchoredEvent.Create(local, "America/New_York").Data!;

        var zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        var expected = local.InZone(zone, Resolvers.LenientResolver).ToInstant();

        var wire = new GenTemporal.LocalAnchoredEventWire(
            LocalDateTimePattern.ExtendedIso.Format(domain.ScheduledLocal),
            domain.IANAIdentifier,
            NextFireUtc: null);
        var roundTripped = LocalAnchoredEvent.Create(
            LocalDateTimePattern.ExtendedIso.Parse(wire.ScheduledLocal).Value,
            wire.IanaZone).Data!;

        roundTripped.ComputeNextFire().Data.Should().Be(
            expected,
            "the historical tzdb offset for the fixture date is used (tzdb-version-sensitive — "
                + "surface + acknowledge if a tzdb update shifts this)");
    }

    // -------------------------------------------------------------------------
    // NV-2 — round-trip comparator non-tautology (one-second + DST-policy +
    // IANA-canonicalization divergences are DETECTED)
    // -------------------------------------------------------------------------

    [Fact]
    public void NV2_Comparator_DetectsOneSecondInstantDivergence()
    {
        var instant = ParseInstant("2026-05-27T14:30:00Z");
        var diverged = instant + Duration.FromSeconds(1);

        instant.Should().NotBe(diverged, "a 1-second epoch divergence must be detectable");
    }

    [Fact]
    public void NV2_Comparator_DetectsDstPolicyDivergence()
    {
        var correct = ParseInstant("2026-03-08T07:30:00Z"); // post-gap
        var preGap = correct - Duration.FromHours(1);

        correct.Should().NotBe(preGap, "a DST-policy divergence (pre-gap vs post-gap) must be detectable");
    }

    [Fact]
    public void NV2_Comparator_DetectsIanaCanonicalizationDivergence()
    {
        var domain = ZonedInstant.Create(ParseInstant("2026-06-15T19:00:00Z"), "US/Pacific").Data!;

        domain.IANAIdentifier.Should().NotBe("US/Pacific", "the un-canonicalized alias must differ from the canonical");
        domain.IANAIdentifier.Should().Be("America/Los_Angeles");
    }

    // -------------------------------------------------------------------------
    // Cross-language equivalence — the fixture wire value materializes to the
    // equivalent domain value here, exactly as in the TS suite (same fixture).
    // -------------------------------------------------------------------------

    [Fact]
    public void CrossLang_ScalarFixtures_MaterializeToEquivalentDomainValues()
    {
        // Every scalar fixture round-trips here; the TS suite asserts the SAME wire
        // strings round-trip to the equivalent Temporal values — the cross-language
        // contract is that both halves consume the identical fixture file.
        ScalarFixtures("instant").Should().NotBeEmpty();
        ScalarFixtures("offset").Should().NotBeEmpty();
        ScalarFixtures("date").Should().NotBeEmpty();
        ScalarFixtures("time").Should().NotBeEmpty();
        ScalarFixtures("localDateTime").Should().NotBeEmpty();
        ScalarFixtures("duration").Should().NotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Fixture plumbing
    // -------------------------------------------------------------------------

    private static Instant ParseInstant(string isoZ) =>
        InstantPattern.ExtendedIso.Parse(isoZ).Value;

    private static LocalDateTime ParseLocalDateTime(string isoLocal) =>
        LocalDateTimePattern.ExtendedIso.Parse(isoLocal).Value;

    // ISO-8601 "P…T…" duration ↔ NodaTime Duration via the D2.Shared.Time
    // IsoDuration helper. NodaTime exposes no built-in ISO-8601 Duration
    // pattern (DurationPattern.Roundtrip is the colon form; PeriodPattern uses
    // explicit unit fields with no decimal-fraction seconds), so the helper
    // is the lossless bridge — including sub-second decimal-fraction seconds
    // to nanosecond precision — that the handler body uses to stay parity-
    // aligned with the TS Temporal.Duration ISO-8601 wire.
    private static Duration ParseIsoDuration(string iso) =>
        IsoDuration.Parse(iso).Data;

    private static string FormatIsoDuration(Duration duration) =>
        IsoDuration.Format(duration);

    private static IEnumerable<ScalarFixture> ScalarFixtures(string kind)
    {
        foreach (var fx in LoadDoc().ScalarRoundTripFixtures)
        {
            if (fx.Kind == kind)
                yield return fx;
        }
    }

    private static CompositeFixture LoadCompositeFixture(string id)
    {
        foreach (var fx in LoadDoc().Fixtures)
        {
            if (fx.Id == id)
                return fx;
        }

        throw new KeyNotFoundException($"composite fixture '{id}' not found");
    }

    private static FixtureFile LoadDoc()
    {
        var path = FindFixturePath();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FixtureFile>(json, sr_jsonOptions)
            ?? throw new InvalidDataException($"could not parse {path}");
    }

    private static string FindFixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "contracts",
                "temporal",
                "temporal-adversarial.fixture.json");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "could not locate contracts/temporal/temporal-adversarial.fixture.json "
                + "by walking up from " + AppContext.BaseDirectory);
    }

    private sealed record FixtureFile(
        int SchemaVersion,
        CompositeFixture[] Fixtures,
        ScalarFixture[] ScalarRoundTripFixtures);

    private sealed record CompositeFixture(
        string Id,
        string ScheduledLocal,
        string Iana,
        string ExpectedUtc);

    private sealed record ScalarFixture(
        string Id,
        string Kind,
        string Wire,
        int? ExpectedOffsetMinutes,
        bool OffsetFree);
}
