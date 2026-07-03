// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Temporal-shaped fixture TypeSpec contract.
//
// This fixture carries every temporal scalar plus both zone-bearing composite
// wire records so the DTO emitter walks the full temporal surface:
//   utcDateTime / offsetDateTime          → DateTimeOffset (ISO-8601 "O")
//   plainDate / plainTime / plainDateTime → string (offset-free ISO)
//   duration                              → string (ISO-8601 "P…T…")
//   ZonedInstantWire / LocalAnchoredEventWire → composite records (IANA survives)
//
// The composites flow through the existing nested-model walk — there is no
// temporal special-case in the walker. The output model mirrors the input so
// both walk directions are exercised. The wire form is NOT the domain form; the
// round-trip tests exercise the handler-side Create mapping, the emitter only
// produces the flat wire DTO.
//
// This is a test fixture, not a real operation. The fixture-owned leaf symbols
// carry a `Fixture` marker; the shared zone-bearing composites
// (ZonedInstantWire / LocalAnchoredEventWire) are real canonical wire models the
// fixture merely consumes, so they keep their real names.

import "@d2/typespec-decorators";
import "../common/temporal.tsp";

using D2;
using D2.Common.Temporal;

namespace D2.Fixtures;

/** Input carrying every temporal type — scalars plus both zone-bearing composites. */
model TemporalFixtureInput {
  /** Bare past instant (Category 1/2). */
  pastInstant: utcDateTime;

  /** Future fixed instant (Category 2). */
  deadline: utcDateTime;

  /** Instant with a preserved offset (offset survives; IANA does NOT — use the composite for IANA). */
  withOffset: offsetDateTime;

  /** Date-only value (no time, no offset). */
  birthday: plainDate;

  /** Time-only value (no date, no offset). */
  alarmTime: plainTime;

  /** Wall-clock local date-time (no offset). */
  wallClock: plainDateTime;

  /** Elapsed interval, ISO-8601. */
  elapsed: duration;

  /** Optional instant — exercises the optional-scalar path (DateTimeOffset? / string with ?:). */
  optionalInstant?: utcDateTime;

  /** Zoned instant composite — the canonical IANA name must survive the wire. */
  zoned: ZonedInstantWire;

  /** Local-anchored event composite — the optional nextFireUtc rides optional handling. */
  schedule: LocalAnchoredEventWire;
}

/** Output mirrors the input so the DTO emitter walks every temporal type in both directions. */
model TemporalFixtureOutput {
  /** Bare past instant (Category 1/2). */
  pastInstant: utcDateTime;

  /** Future fixed instant (Category 2). */
  deadline: utcDateTime;

  /** Instant with a preserved offset. */
  withOffset: offsetDateTime;

  /** Date-only value. */
  birthday: plainDate;

  /** Time-only value. */
  alarmTime: plainTime;

  /** Wall-clock local date-time. */
  wallClock: plainDateTime;

  /** Elapsed interval. */
  elapsed: duration;

  /** Optional instant. */
  optionalInstant?: utcDateTime;

  /** Zoned instant composite. */
  zoned: ZonedInstantWire;

  /** Local-anchored event composite. */
  schedule: LocalAnchoredEventWire;
}

/**
 * Echo every temporal type back. Fixture operation — exercises the DTO emitter's
 * temporal scalar mapping and the composite nested-model walk.
 */
@d2Query
@d2InProcess
@d2ServedBy("TemporalFixtures")
@d2Concern("TemporalFixture")
op temporalFixture(input: TemporalFixtureInput): TemporalFixtureOutput;
