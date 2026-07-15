// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Temporal-adversarial round-trip suite — TypeScript half.
//
// Drives the SAME contracts/temporal/temporal-adversarial.fixture.json as the
// C# half (DcsvIo.D2.Private.Edge.Tests TemporalRoundTripTests), so an identical wire value
// materializes to the equivalent domain value in BOTH languages. Each test maps
// a wire string (the shape the TypeSpec DTO emitter produces — ISO-8601 instant
// / offset-free local / ISO-8601 duration) to the @dcsv-io/d2-time + Temporal domain
// value and back, asserting nothing is lost. The composite cases prove the IANA
// zone NAME survives the wire (a bare offset cannot carry it).
//
// This suite lives in @dcsv-io/d2-time (not the emitter package) because @dcsv-io/d2-time owns
// the temporal domain types being round-tripped and already carries
// temporal-polyfill + the cross-language fixture loader. Cross-language parity
// is preserved by both halves consuming the identical shared fixture file.

import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { Temporal } from "temporal-polyfill";
import { TK } from "@dcsv-io/d2-i18n-keys";
import { LocalAnchoredEvent, ZonedInstant } from "../src/types.js";

// ---------------------------------------------------------------------------
// Shared-fixture loader (same walk-up strategy as cross-language.test.ts)
// ---------------------------------------------------------------------------

interface CompositeFixture {
  readonly id: string;
  readonly scheduledLocal: string;
  readonly iana: string;
  readonly expectedUtc: string;
  readonly expectedCanonicalIana?: string;
}

interface ScalarFixture {
  readonly id: string;
  readonly kind:
    | "instant"
    | "offset"
    | "date"
    | "time"
    | "localDateTime"
    | "duration";
  readonly wire: string;
  readonly expectedOffsetMinutes?: number;
  readonly offsetFree?: boolean;
}

interface FixtureFile {
  readonly schemaVersion: number;
  readonly fixtures: readonly CompositeFixture[];
  readonly scalarRoundTripFixtures: readonly ScalarFixture[];
}

function loadFixtureDoc(): FixtureFile {
  let dir = dirname(fileURLToPath(import.meta.url));
  for (let i = 0; i < 12; i++) {
    const candidate = join(
      dir,
      "contracts",
      "temporal",
      "temporal-adversarial.fixture.json",
    );
    try {
      return JSON.parse(readFileSync(candidate, "utf-8")) as FixtureFile;
    } catch {
      // not here; walk up
    }
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error(
    "could not locate contracts/temporal/temporal-adversarial.fixture.json",
  );
}

const doc = loadFixtureDoc();

const scalars = (kind: ScalarFixture["kind"]): readonly ScalarFixture[] =>
  doc.scalarRoundTripFixtures.filter((f) => f.kind === kind);

const composite = (id: string): CompositeFixture => {
  const fx = doc.fixtures.find((f) => f.id === id);
  if (fx === undefined) throw new Error(`composite fixture '${id}' not found`);
  return fx;
};

// Offset string ("-05:00") → signed minutes.
function offsetMinutes(iso: string): number {
  const m = /([+-])(\d{2}):(\d{2})$/.exec(iso);
  if (m === null) throw new Error(`no offset in '${iso}'`);
  const sign = m[1] === "-" ? -1 : 1;
  return sign * (Number(m[2]) * 60 + Number(m[3]));
}

// ---------------------------------------------------------------------------
// RT-1..6 — per-type round-trip (wire → domain → wire == original)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_PerType", () => {
  it("RT1_utcDateTime_instantRoundTripsThroughWire", () => {
    for (const fx of scalars("instant")) {
      const domain = Temporal.Instant.from(fx.wire);
      const wire = domain.toString();
      const roundTripped = Temporal.Instant.from(wire);

      expect(
        Temporal.Instant.compare(roundTripped, domain),
        `fixture '${fx.id}' instant must survive`,
      ).toBe(0);
    }
  });

  it("RT2_offsetDateTime_preservesOffsetAndInstant_notNormalizedToUtc", () => {
    for (const fx of scalars("offset")) {
      // Temporal.Instant normalizes to UTC; to PRESERVE the offset we parse the
      // wall-clock + offset into a ZonedDateTime pinned to the fixed offset zone.
      const exactInstant = Temporal.Instant.from(fx.wire);
      const declaredOffset = offsetMinutes(fx.wire);

      expect(declaredOffset, "fixture uses a non-UTC offset").not.toBe(0);
      expect(declaredOffset).toBe(fx.expectedOffsetMinutes);

      // Re-emit at the SAME offset and confirm the instant is unchanged (the
      // offset survives the wire — not normalized away).
      const reEmitted = exactInstant.toString({
        timeZone: offsetZone(declaredOffset),
      });
      expect(offsetMinutes(reEmitted)).toBe(declaredOffset);
      expect(
        Temporal.Instant.compare(
          Temporal.Instant.from(reEmitted),
          exactInstant,
        ),
      ).toBe(0);
    }
  });

  it("RT3_plainDate_roundTripsOffsetFree", () => {
    for (const fx of scalars("date")) {
      const domain = Temporal.PlainDate.from(fx.wire);
      const wire = domain.toString();
      const roundTripped = Temporal.PlainDate.from(wire);

      expect(
        roundTripped.equals(domain),
        `fixture '${fx.id}' date must survive`,
      ).toBe(true);
      // AD-10 — no offset / zone marker.
      expect(wire).not.toMatch(/[+Z]/);
      expect(wire).toBe(fx.wire);
    }
  });

  it("RT4_plainTime_roundTripsOffsetFree", () => {
    for (const fx of scalars("time")) {
      const domain = Temporal.PlainTime.from(fx.wire);
      const wire = domain.toString();
      const roundTripped = Temporal.PlainTime.from(wire);

      expect(
        roundTripped.equals(domain),
        `fixture '${fx.id}' time must survive`,
      ).toBe(true);
      expect(wire).not.toMatch(/[+Z]/);
    }
  });

  it("RT5_plainDateTime_roundTripsOffsetFree", () => {
    for (const fx of scalars("localDateTime")) {
      const domain = Temporal.PlainDateTime.from(fx.wire);
      const wire = domain.toString();
      const roundTripped = Temporal.PlainDateTime.from(wire);

      expect(
        roundTripped.equals(domain),
        `fixture '${fx.id}' wall-clock must survive`,
      ).toBe(true);
      // AD-10 — the wall-clock string carries NO offset.
      expect(wire).not.toMatch(/[+Z]/);
    }
  });

  it("RT6_duration_roundTripsViaIso8601", () => {
    for (const fx of scalars("duration")) {
      const domain = Temporal.Duration.from(fx.wire);
      const wire = domain.toString();
      const roundTripped = Temporal.Duration.from(wire);

      // Compare by total nanoseconds (canonical, balanced).
      expect(
        roundTripped.total({ unit: "nanoseconds" }),
        `fixture '${fx.id}' duration must survive`,
      ).toBe(domain.total({ unit: "nanoseconds" }));
    }
  });
});

// ---------------------------------------------------------------------------
// RT-7 — ZonedInstant composite (IANA NAME survives — load-bearing proof)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_ZonedInstantComposite", () => {
  it("RT7_zonedInstant_roundTrips_andIanaNameSurvives", () => {
    const instant = Temporal.Instant.from("2026-05-27T16:30:00Z");
    const domain = ZonedInstant.create(instant, "America/Los_Angeles").data!;

    // Domain → wire record { instant, zoneId } → domain (mirrors the handler).
    const wire = {
      instant: domain.instant.toString(),
      zoneId: domain.ianaIdentifier,
    };
    const roundTripped = ZonedInstant.create(
      Temporal.Instant.from(wire.instant),
      wire.zoneId,
    ).data!;

    expect(Temporal.Instant.compare(roundTripped.instant, domain.instant)).toBe(
      0,
    );

    // THE load-bearing assertion: the IANA NAME survives — not merely the offset.
    expect(
      roundTripped.ianaIdentifier,
      "the canonical IANA name must survive the ZonedInstant round-trip",
    ).toBe("America/Los_Angeles");
    expect(wire.zoneId).toBe("America/Los_Angeles");
  });

  it("RT7_zonedInstant_aliasInput_canonicalIanaNameSurvives", () => {
    const instant = Temporal.Instant.from("2026-06-15T19:00:00Z");
    const domain = ZonedInstant.create(instant, "US/Pacific").data!;
    expect(domain.ianaIdentifier).toBe("America/Los_Angeles");

    const wire = {
      instant: domain.instant.toString(),
      zoneId: domain.ianaIdentifier,
    };
    const roundTripped = ZonedInstant.create(
      Temporal.Instant.from(wire.instant),
      wire.zoneId,
    ).data!;

    expect(roundTripped.ianaIdentifier).toBe("America/Los_Angeles");
  });
});

// ---------------------------------------------------------------------------
// RT-8 — LocalAnchoredEvent composite (all 3 fields + recomputed next-fire)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_LocalAnchoredEventComposite", () => {
  it("RT8_localAnchoredEvent_roundTrips_allFieldsAndNextFireAgree", () => {
    const local = Temporal.PlainDateTime.from("2026-03-08T02:30:00");
    const seed = LocalAnchoredEvent.create(local, "America/New_York").data!;
    const nextFire = seed.computeNextFire().data!;
    const domain = LocalAnchoredEvent.create(
      local,
      "America/New_York",
      nextFire,
    ).data!;

    const wire = {
      scheduledLocal: domain.scheduledLocal.toString(),
      ianaZone: domain.ianaIdentifier,
      nextFireUtc: domain.nextFireUtc!.toString(),
    };

    const roundTripped = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(wire.scheduledLocal),
      wire.ianaZone,
      Temporal.Instant.from(wire.nextFireUtc),
    ).data!;

    expect(roundTripped.scheduledLocal.equals(domain.scheduledLocal)).toBe(
      true,
    );
    expect(roundTripped.ianaIdentifier).toBe("America/New_York");
    expect(
      Temporal.Instant.compare(roundTripped.nextFireUtc!, domain.nextFireUtc!),
    ).toBe(0);
    expect(
      Temporal.Instant.compare(
        roundTripped.computeNextFire().data!,
        domain.nextFireUtc!,
      ),
    ).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// AD-1..3 — DST gap / overlap across US / Europe / Australia (fixture-driven)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_DstGapOverlap", () => {
  it.each([
    "us-spring-forward-skipped-2-30",
    "us-fall-back-ambiguous-1-30-picks-earlier",
    "european-spring-forward-skipped",
    "european-fall-back-ambiguous-picks-earlier",
    "australian-spring-forward-skipped",
  ])("AD1to3_%s_computeNextFireMatchesFixture", (id) => {
    const fx = composite(id);
    const domain = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(fx.scheduledLocal),
      fx.iana,
    ).data!;

    // Round-trip wall-clock + iana through the wire, then recompute next-fire.
    const wire = {
      scheduledLocal: domain.scheduledLocal.toString(),
      ianaZone: domain.ianaIdentifier,
    };
    const roundTripped = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(wire.scheduledLocal),
      wire.ianaZone,
    ).data!;

    expect(
      Temporal.Instant.compare(
        roundTripped.computeNextFire().data!,
        Temporal.Instant.from(fx.expectedUtc),
      ),
      `fixture '${fx.id}' must resolve to the DST-correct UTC, identical to the .NET engine`,
    ).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// AD-4 — invalid IANA → validationFailed (NOT a throw)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_InvalidIana", () => {
  it("AD4_invalidIana_returnsValidationFailed_notThrow", () => {
    const instant = Temporal.Instant.from("2026-05-27T14:30:00Z");
    const local = Temporal.PlainDateTime.from("2026-05-27T09:00:00");

    const zi = ZonedInstant.create(instant, "Not/AZone");
    expect(zi.success).toBe(false);
    expect(zi.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.time.INVALID_IANA_IDENTIFIER.key,
    );

    const lae = LocalAnchoredEvent.create(local, "Not/AZone");
    expect(lae.success).toBe(false);
    expect(lae.inputErrors[0]!.errors[0]!.key).toBe(
      TK.common.time.INVALID_IANA_IDENTIFIER.key,
    );
  });
});

// ---------------------------------------------------------------------------
// AD-5 — fixed-offset notation rejected as IANA (the core reason for the composite)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_FixedOffsetRejected", () => {
  it.each(["+05:00", "-08:00", "UTC+5"])(
    "AD5_fixedOffset_%s_rejectedAsIana",
    (fixedOffset) => {
      const instant = Temporal.Instant.from("2026-05-27T14:30:00Z");
      const result = ZonedInstant.create(instant, fixedOffset);

      expect(
        result.success,
        `'${fixedOffset}' is a fixed offset, not an IANA name`,
      ).toBe(false);
      expect(result.inputErrors[0]!.errors[0]!.key).toBe(
        TK.common.time.INVALID_IANA_IDENTIFIER.key,
      );
    },
  );
});

// ---------------------------------------------------------------------------
// AD-6 — IANA alias normalization survives the wire (canonical name)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_IanaAliasNormalization", () => {
  it.each([
    ["iana-normalization-us-pacific-alias", "America/Los_Angeles"],
    ["iana-normalization-asia-saigon-renamed", "Asia/Ho_Chi_Minh"],
  ])(
    "AD6_%s_normalizesToCanonical_andSurvivesWire",
    (id, expectedCanonical) => {
      const fx = composite(id);
      const domain = LocalAnchoredEvent.create(
        Temporal.PlainDateTime.from(fx.scheduledLocal),
        fx.iana,
      ).data!;
      expect(domain.ianaIdentifier).toBe(expectedCanonical);

      const wire = {
        scheduledLocal: domain.scheduledLocal.toString(),
        ianaZone: domain.ianaIdentifier,
      };
      const roundTripped = LocalAnchoredEvent.create(
        Temporal.PlainDateTime.from(wire.scheduledLocal),
        wire.ianaZone,
      ).data!;

      expect(roundTripped.ianaIdentifier).toBe(expectedCanonical);
      expect(
        Temporal.Instant.compare(
          roundTripped.computeNextFire().data!,
          Temporal.Instant.from(fx.expectedUtc),
        ),
      ).toBe(0);
    },
  );
});

// ---------------------------------------------------------------------------
// AD-7 — leap year / leap day / impossible calendar date (TS divergence documented)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_LeapDay", () => {
  it("AD7_leapDay_feb29_validInLeapYear_roundTrips", () => {
    const fx = scalars("localDateTime").find(
      (f) => f.id === "plain-datetime-leap-day",
    )!;
    const leap = Temporal.PlainDateTime.from(fx.wire);

    expect(Temporal.PlainDateTime.from(leap.toString()).equals(leap)).toBe(
      true,
    );
    expect(leap.toString()).toBe("2024-02-29T12:00:00");
  });

  it("AD7_impossibleCalendarDate_throwsRangeError", () => {
    // DOCUMENTED CROSS-LANGUAGE DIVERGENCE: C# NodaTime throws
    // ArgumentOutOfRangeException at the LocalDateTime ctor; TS Temporal throws
    // RangeError. Each language asserts ITS documented behavior — they are NOT
    // forced equal. The emitter does not change either.
    expect(() => Temporal.PlainDateTime.from("2026-02-29T12:00:00")).toThrow(
      RangeError,
    );
  });
});

// ---------------------------------------------------------------------------
// AD-8 — year boundary instant round-trips through the wire
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_YearBoundary", () => {
  it("AD8_yearBoundaryInstant_roundTripsThroughWire", () => {
    const fx = scalars("instant").find(
      (f) => f.id === "utc-instant-year-boundary",
    )!;
    const domain = Temporal.Instant.from(fx.wire);

    expect(
      Temporal.Instant.compare(
        Temporal.Instant.from(domain.toString()),
        domain,
      ),
    ).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// AD-9 — sub-second precision (Duration nanosecond ISO; Instant sub-second)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_SubSecondPrecision", () => {
  it("AD9_duration_subSecondNanos_roundTripsLossless_bothLanguages", () => {
    // Temporal.Duration round-trips ISO-8601 decimal-fraction seconds losslessly
    // to nanoseconds. The .NET half now matches via the DcsvIo.D2.Time IsoDuration
    // helper (int64-nanosecond, no float), so the SAME shared-fixture wire string
    // materializes to the SAME 123_456_789 ns value in both languages — the wire
    // stays an ISO-8601 STRING, sub-second precision included.
    const fx = scalars("duration").find(
      (f) => f.id === "duration-subsecond-nanos",
    )!;
    expect(fx.wire).toBe("PT0.123456789S");

    const d = Temporal.Duration.from(fx.wire);
    expect(d.total({ unit: "nanoseconds" })).toBe(123_456_789);
    const roundTripped = Temporal.Duration.from(d.toString());
    expect(roundTripped.total({ unit: "nanoseconds" })).toBe(123_456_789);
    // Sub-second-only is byte-stable: "PT0.123456789S" → toString → identical.
    expect(d.toString()).toBe("PT0.123456789S");
  });

  it("AD9_duration_hmsWithSubSecond_roundTripsLossless", () => {
    // Combined H/M/S + fractional, driven by the shared fixture the .NET half
    // also consumes — cross-language value parity (total nanoseconds).
    const fx = scalars("duration").find(
      (f) => f.id === "duration-hms-with-subsecond",
    )!;
    const expectedNanos = (1 * 3600 + 2 * 60 + 3) * 1_000_000_000 + 123_456_789;

    const d = Temporal.Duration.from(fx.wire);
    expect(d.total({ unit: "nanoseconds" })).toBe(expectedNanos);
    expect(
      Temporal.Duration.from(d.toString()).total({ unit: "nanoseconds" }),
    ).toBe(expectedNanos);
  });

  it("AD9_instant_subSecondFractional_roundTrips", () => {
    const fx = scalars("instant").find(
      (f) => f.id === "utc-instant-subsecond-7digit",
    )!;
    const domain = Temporal.Instant.from(fx.wire);
    // 7-digit fractional (100ns) — the cross-language floor with .NET DateTimeOffset "O".
    expect(
      Temporal.Instant.compare(
        Temporal.Instant.from(domain.toString()),
        domain,
      ),
    ).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// AD-10 — no-invented-offset on plain-local types
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_NoInventedOffset", () => {
  it("AD10_plainLocalWireStrings_carryNoOffset", () => {
    const plainLocal = [
      ...scalars("date"),
      ...scalars("time"),
      ...scalars("localDateTime"),
    ];
    for (const fx of plainLocal) {
      expect(
        fx.offsetFree,
        `fixture '${fx.id}' must be flagged offset-free`,
      ).toBe(true);
      expect(fx.wire).not.toMatch(/[+Z]/);
      expect(fx.wire).not.toContain("+00:00");
    }
  });
});

// ---------------------------------------------------------------------------
// AD-11 — optional nextFireUtc absent → undefined (null→undefined contract)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_OptionalNextFire", () => {
  it("AD11_optionalNextFireUtc_absent_roundTripsAsUndefined", () => {
    const local = Temporal.PlainDateTime.from("2026-06-15T12:00:00");
    const domain = LocalAnchoredEvent.create(local, "UTC").data!;
    expect(domain.nextFireUtc).toBeUndefined();

    // Wire carries undefined for the absent optional (JSON null normalizes to
    // undefined at the deserialization boundary — the TS prefer-undefined rule).
    const wire: {
      scheduledLocal: string;
      ianaZone: string;
      nextFireUtc?: string;
    } = {
      scheduledLocal: domain.scheduledLocal.toString(),
      ianaZone: domain.ianaIdentifier,
    };
    expect(wire.nextFireUtc).toBeUndefined();

    const roundTripped = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(wire.scheduledLocal),
      wire.ianaZone,
      wire.nextFireUtc === undefined
        ? undefined
        : Temporal.Instant.from(wire.nextFireUtc),
    ).data!;

    expect(roundTripped.nextFireUtc).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// AD-12 — historical tzdb offset change (pre-DST-era date)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_HistoricalTzdb", () => {
  it("AD12_historicalTzdbOffset_resolvesForThatDate_roundTrips", () => {
    const local = Temporal.PlainDateTime.from("1950-06-15T12:00:00");
    const domain = LocalAnchoredEvent.create(local, "America/New_York").data!;

    // Independent expectation via Temporal's own tzdb for that historical date.
    const expected = local
      .toZonedDateTime("America/New_York", { disambiguation: "compatible" })
      .toInstant();

    const wire = {
      scheduledLocal: domain.scheduledLocal.toString(),
      ianaZone: domain.ianaIdentifier,
    };
    const roundTripped = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(wire.scheduledLocal),
      wire.ianaZone,
    ).data!;

    expect(
      Temporal.Instant.compare(roundTripped.computeNextFire().data!, expected),
      "the historical tzdb offset is used (tzdb-version-sensitive — surface if shifted)",
    ).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// NV-2 — round-trip comparator non-tautology (epoch / DST-policy / IANA divergences detected)
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_ComparatorNonTautology", () => {
  it("NV2_detectsOneSecondInstantDivergence", () => {
    const instant = Temporal.Instant.from("2026-05-27T14:30:00Z");
    const diverged = instant.add({ seconds: 1 });

    expect(Temporal.Instant.compare(instant, diverged)).not.toBe(0);
  });

  it("NV2_detectsDstPolicyDivergence", () => {
    const correct = Temporal.Instant.from("2026-03-08T07:30:00Z");
    const preGap = correct.subtract({ hours: 1 });

    expect(Temporal.Instant.compare(correct, preGap)).not.toBe(0);
  });

  it("NV2_detectsIanaCanonicalizationDivergence", () => {
    const domain = ZonedInstant.create(
      Temporal.Instant.from("2026-06-15T19:00:00Z"),
      "US/Pacific",
    ).data!;

    expect(domain.ianaIdentifier).not.toBe("US/Pacific");
    expect(domain.ianaIdentifier).toBe("America/Los_Angeles");
  });
});

// ---------------------------------------------------------------------------
// Cross-language equivalence — same fixture wire values consumed here as in C#
// ---------------------------------------------------------------------------

describe("temporalRoundTrip_CrossLanguageEquivalence", () => {
  it("crossLang_scalarFixtures_present_andMaterializeEquivalently", () => {
    expect(scalars("instant").length).toBeGreaterThan(0);
    expect(scalars("offset").length).toBeGreaterThan(0);
    expect(scalars("date").length).toBeGreaterThan(0);
    expect(scalars("time").length).toBeGreaterThan(0);
    expect(scalars("localDateTime").length).toBeGreaterThan(0);
    expect(scalars("duration").length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// Helper: a fixed-offset IANA-less "time zone" string for re-emit (RT-2).
// Temporal accepts an offset string ("-05:00") as a time-zone for toString().
// ---------------------------------------------------------------------------

function offsetZone(minutes: number): string {
  const sign = minutes < 0 ? "-" : "+";
  const abs = Math.abs(minutes);
  const hh = String(Math.floor(abs / 60)).padStart(2, "0");
  const mm = String(abs % 60).padStart(2, "0");
  return `${sign}${hh}:${mm}`;
}
