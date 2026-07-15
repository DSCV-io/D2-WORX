// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { TK } from "@dcsv-io/d2-i18n-keys";
import { describe, expect, it } from "vitest";
import { Temporal } from "temporal-polyfill";
import { LocalAnchoredEvent, ZonedInstant } from "../src/types.js";

describe("ZonedInstant", () => {
  it("create_validInstantAndCanonicalIANA_returnsOk", () => {
    const instant = Temporal.Instant.fromEpochMilliseconds(1_000_000);

    const result = ZonedInstant.create(instant, "America/Edmonton");

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("America/Edmonton");
    expect(Temporal.Instant.compare(result.data!.instant, instant)).toBe(0);
  });

  it("create_alreadyCanonical_storesAsIs", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "Europe/London",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("Europe/London");
  });

  it("create_differentInstants_areNotSame", () => {
    const a = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(1000),
      "UTC",
    ).data!;
    const b = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(2000),
      "UTC",
    ).data!;

    expect(Temporal.Instant.compare(a.instant, b.instant)).not.toBe(0);
  });

  it("create_differentIANA_areNotSame", () => {
    const instant = Temporal.Instant.fromEpochMilliseconds(1000);
    const a = ZonedInstant.create(instant, "America/Edmonton").data!;
    const b = ZonedInstant.create(instant, "America/Vancouver").data!;

    expect(a.ianaIdentifier).not.toBe(b.ianaIdentifier);
  });

  // --- IANA validation: rejection ---

  it("create_undefinedIANA_returnsValidationFailed", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      undefined,
    );

    expect(result.success).toBe(false);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]!.field).toBe("ianaIdentifier");
    expect(result.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.errors.NOT_NULL_VIOLATION.key,
    );
  });

  it("create_emptyIANA_returnsValidationFailed", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "",
    );

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.errors.NOT_NULL_VIOLATION.key,
    );
  });

  it.each([" ", "   ", "\t", "\n"])(
    "create_whitespaceIANA_returnsValidationFailed (%j)",
    (whitespace) => {
      const result = ZonedInstant.create(
        Temporal.Instant.fromEpochMilliseconds(0),
        whitespace,
      );

      expect(result.success).toBe(false);
      expect(result.inputErrors[0]!.errors[0]!.key).toBe(
        TK.common.errors.NOT_NULL_VIOLATION.key,
      );
    },
  );

  it("create_invalidIANAZone_returnsValidationFailed", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "Invalid/Zone",
    );

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.time.INVALID_IANA_IDENTIFIER.key,
    );
  });

  it.each(["UTC+5", "+05:00", "-08:00"])(
    "create_fixedOffsetNotation_returnsValidationFailed (%s)",
    (offset) => {
      const result = ZonedInstant.create(
        Temporal.Instant.fromEpochMilliseconds(0),
        offset,
      );

      expect(result.success).toBe(false);
      expect(result.inputErrors[0]!.errors[0]!.key).toBe(
        TK.common.time.INVALID_IANA_IDENTIFIER.key,
      );
    },
  );

  it.each(["123", "5", "+5"])(
    "create_plainNumericString_returnsValidationFailed (%s)",
    (numeric) => {
      const result = ZonedInstant.create(
        Temporal.Instant.fromEpochMilliseconds(0),
        numeric,
      );

      expect(result.success).toBe(false);
      expect(result.inputErrors[0]!.errors[0]!.key).toBe(
        TK.common.time.INVALID_IANA_IDENTIFIER.key,
      );
    },
  );

  // --- IANA normalization: acceptance + canonicalization ---

  it("create_deprecatedAliasUSPacific_normalizesToAmericaLosAngeles", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "US/Pacific",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("America/Los_Angeles");
  });

  it("create_deprecatedAliasUSEastern_normalizesToAmericaNewYork", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "US/Eastern",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("America/New_York");
  });

  it("create_renamedZoneAsiaSaigon_normalizesToAsiaHoChiMinh", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "Asia/Saigon",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("Asia/Ho_Chi_Minh");
  });

  it("create_renamedZoneAsiaCalcutta_normalizesToAsiaKolkata", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "Asia/Calcutta",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("Asia/Kolkata");
  });

  it("create_alreadyCanonicalIANA_storesAsIs_americaEdmonton", () => {
    const result = ZonedInstant.create(
      Temporal.Instant.fromEpochMilliseconds(0),
      "America/Edmonton",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("America/Edmonton");
  });

  // --- Case-sensitivity behavior (cross-language divergence documentation) ---

  it.each(["america/new_york", "AMERICA/NEW_YORK", "America/new_york"])(
    "create_lowercaseOrMixedCaseIANA_normalizesToCanonical_documentsIntlBehavior (%s)",
    (nonCanonicalCase) => {
      // Node's Intl.DateTimeFormat is CASE-INSENSITIVE — it accepts
      // lowercase / mixed-case IANA inputs and resolves them to the
      // canonical-cased form via resolvedOptions().timeZone.
      //
      // CROSS-LANGUAGE DIVERGENCE (.NET ↔ TS):
      // .NET NodaTime tzdb lookup IS case-sensitive (GetZoneOrNull returns
      // null for non-canonical case → ValidationFailed). See
      // public/packages/dotnet/tests/Unit/Time/ZonedInstantTests.cs:
      // Create_LowercaseCanonicalName_BehaviorDocumented.
      //
      // Callers crossing the .NET/TS boundary MUST pass canonical-cased
      // IANA strings to stay parity-aligned. The TS side cannot reject
      // non-canonical case without re-implementing tzdb lookup outside of
      // Intl; the divergence is documented here so future readers don't
      // assume symmetric rejection.
      const result = ZonedInstant.create(
        Temporal.Instant.fromEpochMilliseconds(0),
        nonCanonicalCase,
      );

      expect(result.success).toBe(true);
      expect(result.data!.ianaIdentifier).toBe("America/New_York");
    },
  );

  it("recordEquality_aliasFormVsCanonicalForm_areStructurallyEqual", () => {
    const instant = Temporal.Instant.fromEpochMilliseconds(12345);

    const aliasForm = ZonedInstant.create(instant, "US/Pacific").data!;
    const canonicalForm = ZonedInstant.create(
      instant,
      "America/Los_Angeles",
    ).data!;

    expect(aliasForm.ianaIdentifier).toBe(canonicalForm.ianaIdentifier);
    expect(
      Temporal.Instant.compare(aliasForm.instant, canonicalForm.instant),
    ).toBe(0);
  });

  // --- Instant value invariance / boundary ---

  it("create_epochInstant_storesCorrectly", () => {
    const epoch = Temporal.Instant.fromEpochMilliseconds(0);
    const result = ZonedInstant.create(epoch, "UTC");

    expect(result.success).toBe(true);
    expect(Temporal.Instant.compare(result.data!.instant, epoch)).toBe(0);
  });

  it("create_farFutureInstant_storesCorrectly", () => {
    const farFuture = Temporal.Instant.from("9000-01-01T00:00:00Z");
    const result = ZonedInstant.create(farFuture, "UTC");

    expect(result.success).toBe(true);
    expect(Temporal.Instant.compare(result.data!.instant, farFuture)).toBe(0);
  });

  it("create_farPastInstant_storesCorrectly", () => {
    const farPast = Temporal.Instant.from("1700-01-01T00:00:00Z");
    const result = ZonedInstant.create(farPast, "UTC");

    expect(result.success).toBe(true);
    expect(Temporal.Instant.compare(result.data!.instant, farPast)).toBe(0);
  });
});

describe("LocalAnchoredEvent", () => {
  it("create_validScheduledLocalAndIANA_returnsOk_nextFireUtcAbsent", () => {
    const scheduled = Temporal.PlainDateTime.from("2026-03-14T09:00:00");
    const result = LocalAnchoredEvent.create(scheduled, "Europe/Berlin");

    expect(result.success).toBe(true);
    expect(result.data!.nextFireUtc).toBeUndefined();
    expect(result.data!.ianaIdentifier).toBe("Europe/Berlin");
    expect(result.data!.scheduledLocal.year).toBe(2026);
  });

  it("create_withNextFireUtc_storesValue", () => {
    const next = Temporal.Instant.fromEpochMilliseconds(123_456_000);
    const result = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-06-01T08:30:00"),
      "America/Edmonton",
      next,
    );

    expect(result.success).toBe(true);
    expect(Temporal.Instant.compare(result.data!.nextFireUtc!, next)).toBe(0);
  });

  it("create_withoutNextFireUtc_isAbsent", () => {
    const result = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-01-01T00:00:00"),
      "UTC",
    );

    expect(result.success).toBe(true);
    expect(result.data!.nextFireUtc).toBeUndefined();
  });

  it("create_americaArgentinaBuenosAires_storesAsIs", () => {
    const result = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-01-01T00:00:00"),
      "America/Argentina/Buenos_Aires",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("America/Argentina/Buenos_Aires");
  });

  // --- IANA validation ---

  it("create_undefinedIANA_returnsValidationFailed", () => {
    const result = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-01-01T00:00:00"),
      undefined,
    );

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.errors.NOT_NULL_VIOLATION.key,
    );
  });

  it("create_emptyIANA_returnsValidationFailed", () => {
    const result = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-01-01T00:00:00"),
      "",
    );

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.errors.NOT_NULL_VIOLATION.key,
    );
  });

  it("create_invalidIANAZone_returnsValidationFailed", () => {
    const result = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-01-01T00:00:00"),
      "Invalid/Zone",
    );

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.time.INVALID_IANA_IDENTIFIER.key,
    );
  });

  it.each(["UTC+5", "+05:00"])(
    "create_fixedOffsetNotation_returnsValidationFailed (%s)",
    (offset) => {
      const result = LocalAnchoredEvent.create(
        Temporal.PlainDateTime.from("2026-01-01T00:00:00"),
        offset,
      );

      expect(result.success).toBe(false);
      expect(result.inputErrors[0]!.errors[0]!.key).toBe(
        TK.common.time.INVALID_IANA_IDENTIFIER.key,
      );
    },
  );

  it("create_deprecatedAliasUSPacific_normalizesToAmericaLosAngeles", () => {
    const result = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-01-01T00:00:00"),
      "US/Pacific",
    );

    expect(result.success).toBe(true);
    expect(result.data!.ianaIdentifier).toBe("America/Los_Angeles");
  });

  // --- Calendar edge cases (PlainDateTime constructor throws) ---

  it("plainDateTime_leapDayInLeapYear2024_constructsSuccessfully", () => {
    const dt = Temporal.PlainDateTime.from("2024-02-29T09:00:00");
    expect(dt.day).toBe(29);
  });

  it("plainDateTime_leapDay2000_Feb29_constructsSuccessfully", () => {
    // Year 2000 IS a leap year (div by 400).
    const dt = Temporal.PlainDateTime.from("2000-02-29T12:00:00");
    expect(dt.day).toBe(29);
  });

  it("plainDateTime_leapDayInNonLeapYear2025_throws", () => {
    expect(() => Temporal.PlainDateTime.from("2025-02-29T09:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_leapDay1900_Feb29_throws", () => {
    // Year 1900: div by 100 but NOT div by 400 → NOT a leap year.
    expect(() => Temporal.PlainDateTime.from("1900-02-29T09:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_Feb30_throws", () => {
    expect(() => Temporal.PlainDateTime.from("2026-02-30T09:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_April31_throws", () => {
    expect(() => Temporal.PlainDateTime.from("2026-04-31T09:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_September31_throws", () => {
    // September has 30 days; day 31 must throw.
    expect(() => Temporal.PlainDateTime.from("2026-09-31T09:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_November31_throws", () => {
    // November has 30 days; day 31 must throw.
    expect(() => Temporal.PlainDateTime.from("2026-11-31T09:00:00")).toThrow(
      RangeError,
    );
  });

  // --- Year boundary documentation (Temporal ISO bounds) ---
  //
  // Temporal supports a wider ISO year range than NodaTime:
  // - Temporal: [-271821, +275760] (millisecond-distance from Unix epoch)
  // - NodaTime: [-9998, 9999] (ISO calendar; see
  //   public/packages/dotnet/tests/Unit/Time/LocalAnchoredEventTests.cs —
  //   LocalDateTime_YearMinNodaTime / LocalDateTime_YearMaxNodaTime)
  //
  // CROSS-LANGUAGE DIVERGENCE: an event with year > 9999 round-trips fine
  // on the TS side but throws ArgumentOutOfRangeException on the .NET side.
  // Callers crossing the boundary MUST stay within the NodaTime range to
  // remain parity-safe.

  it("plainDateTime_year1_constructsSuccessfully", () => {
    const dt = new Temporal.PlainDateTime(1, 1, 1, 0, 0);
    expect(dt.year).toBe(1);
  });

  it("plainDateTime_year9999_constructsSuccessfully", () => {
    const dt = new Temporal.PlainDateTime(9999, 12, 31, 23, 59);
    expect(dt.year).toBe(9999);
  });

  it("plainDateTime_yearTemporalIsoMax275760_constructsSuccessfully", () => {
    const dt = Temporal.PlainDateTime.from("+275760-01-01T00:00:00");
    expect(dt.year).toBe(275760);
  });

  it("plainDateTime_yearTemporalIsoMin_negative271820_constructsSuccessfully", () => {
    const dt = Temporal.PlainDateTime.from("-271820-01-01T00:00:00");
    expect(dt.year).toBe(-271820);
  });

  it("plainDateTime_yearOverflowAboveTemporalMax_throws", () => {
    expect(() => Temporal.PlainDateTime.from("+275761-01-01T00:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_yearOverflowBelowTemporalMin_throws", () => {
    expect(() => Temporal.PlainDateTime.from("-271821-01-01T00:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_hour24_throws", () => {
    expect(() => Temporal.PlainDateTime.from("2026-06-01T24:00:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_minute60_throws", () => {
    expect(() => Temporal.PlainDateTime.from("2026-06-01T09:60:00")).toThrow(
      RangeError,
    );
  });

  it("plainDateTime_second60_clamped_noLeapSecondSupport", () => {
    // Temporal explicitly does not model leap seconds; per the spec, parsing
    // ":60" CLAMPS to :59 (does NOT throw). Behavior divergence from .NET
    // NodaTime (which throws on second=60) — both engines agree on "no
    // leap-second support" but disagree on the error mode. Documented here
    // so future readers know not to rely on a throw for invalid-second input.
    const dt = Temporal.PlainDateTime.from("2026-06-01T09:30:60");
    expect(dt.second).toBe(59);
  });

  // --- computeNextFire happy paths ---

  it("computeNextFire_unambiguousLocalTimeUTC_returnsCorrectInstant", () => {
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-06-15T12:00:00"),
      "UTC",
    ).data!;

    const fire = ev.computeNextFire();

    expect(fire.success).toBe(true);
    expect(
      Temporal.Instant.compare(
        fire.data!,
        Temporal.Instant.from("2026-06-15T12:00:00Z"),
      ),
    ).toBe(0);
  });

  it("computeNextFire_sameInputCalledTwice_isDeterministic", () => {
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-06-15T12:00:00"),
      "America/New_York",
    ).data!;

    const a = ev.computeNextFire().data!;
    const b = ev.computeNextFire().data!;

    expect(Temporal.Instant.compare(a, b)).toBe(0);
  });

  it("computeNextFire_sameLocalDifferentZones_differentInstants", () => {
    const local = Temporal.PlainDateTime.from("2026-06-15T12:00:00");
    const ny = LocalAnchoredEvent.create(local, "America/New_York").data!;
    const la = LocalAnchoredEvent.create(local, "America/Los_Angeles").data!;

    const nyFire = ny.computeNextFire().data!;
    const laFire = la.computeNextFire().data!;

    expect(Temporal.Instant.compare(nyFire, laFire)).not.toBe(0);
    // LA fires 3 hours later in UTC than NY (LA is 3h behind NY).
    expect(laFire.epochMilliseconds - nyFire.epochMilliseconds).toBe(
      3 * 60 * 60 * 1000,
    );
  });

  it("computeNextFire_USSpringForward_2_30AM_disambiguationCompatibleMapsForward", () => {
    // March 8 2026 = US DST spring-forward. 2:00-3:00 AM EST doesn't
    // exist. compatible maps 2:30 AM forward → 3:30 AM EDT clock = 7:30 UTC.
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-03-08T02:30:00"),
      "America/New_York",
    ).data!;

    const fire = ev.computeNextFire().data!;

    expect(
      Temporal.Instant.compare(
        fire,
        Temporal.Instant.from("2026-03-08T07:30:00Z"),
      ),
    ).toBe(0);
  });

  it("computeNextFire_EuropeanSpringForward_disambiguationCompatibleMapsForward", () => {
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-03-29T01:30:00"),
      "Europe/London",
    ).data!;

    const fire = ev.computeNextFire().data!;

    expect(
      Temporal.Instant.compare(
        fire,
        Temporal.Instant.from("2026-03-29T01:30:00Z"),
      ),
    ).toBe(0);
  });

  it("computeNextFire_AustralianSpringForward_disambiguationCompatibleMapsForward", () => {
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-10-04T02:30:00"),
      "Australia/Sydney",
    ).data!;

    const fire = ev.computeNextFire().data!;

    expect(
      Temporal.Instant.compare(
        fire,
        Temporal.Instant.from("2026-10-03T16:30:00Z"),
      ),
    ).toBe(0);
  });

  it("computeNextFire_USFallBack_1_30AM_disambiguationCompatiblePicksEarlier", () => {
    // November 1 2026 = US DST fall-back. 1:30 AM occurs twice; compatible
    // picks earlier (EDT) → 5:30 UTC, NOT 6:30 UTC (EST).
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2026-11-01T01:30:00"),
      "America/New_York",
    ).data!;

    const fire = ev.computeNextFire().data!;

    expect(
      Temporal.Instant.compare(
        fire,
        Temporal.Instant.from("2026-11-01T05:30:00Z"),
      ),
    ).toBe(0);
  });

  it("computeNextFire_EuropeanFallBack_disambiguationCompatiblePicksEarlier", () => {
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from("2025-10-26T01:30:00"),
      "Europe/London",
    ).data!;

    const fire = ev.computeNextFire().data!;

    expect(
      Temporal.Instant.compare(
        fire,
        Temporal.Instant.from("2025-10-26T00:30:00Z"),
      ),
    ).toBe(0);
  });
});
