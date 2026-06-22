// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Canonical temporal composite wire models.
//
// The single source for zone-bearing temporal wire shapes. A bare scalar
// (DateTimeOffset / ISO string) preserves an OFFSET but DISCARDS the IANA zone
// name — so any value whose IANA identifier must survive the wire uses one of
// these composite records, which carry the canonical IANA string alongside the
// instant / wall-clock.
//
// The wire form is NOT the domain form: the handler body maps each composite to
// and from the D2.Shared.Time smart-constructor (ZonedInstant.Create /
// LocalAnchoredEvent.Create) — and the @d2/time mirror on the TypeScript side —
// at the transport boundary. The emitter only produces the flat wire record.

import "@d2/typespec-decorators";

using D2;

namespace D2.Common.Temporal;

/**
 * Wall-clock local date-time with NO offset and NO zone (the Category-3 LOCAL
 * half). TypeSpec ships `plainDate` and `plainTime` but no plain date-TIME, so
 * D² declares it as a string-shaped scalar: the lossless wire form is an
 * offset-free ISO string (NodaTime `LocalDateTimePattern.ExtendedIso` ↔
 * `Temporal.PlainDateTime`). Inventing a +00:00 offset on this value silently
 * corrupts its wall-clock meaning, so it MUST NOT be an instant-bearing type.
 */
scalar plainDateTime extends string;

/** Wire shape for a Category-1 zoned instant (ZonedInstant): an instant plus the IANA zone that survives the wire. */
model ZonedInstantWire {
  /** UTC instant, ISO-8601 "O" (offset preserved). */
  instant: utcDateTime;

  /** Canonical IANA zone id (e.g. "America/Los_Angeles"). Survives the wire — a bare offset cannot carry it. */
  zoneId: string;
}

/** Wire shape for a Category-3 local-anchored event (LocalAnchoredEvent): wall-clock local + IANA + optional precomputed next-fire UTC. */
model LocalAnchoredEventWire {
  /** Scheduled wall-clock local date-time (offset-FREE — inventing an offset corrupts the wall clock). */
  scheduledLocal: plainDateTime;

  /** Canonical IANA zone id in which scheduledLocal is interpreted. */
  ianaZone: string;

  /** Optional precomputed UTC instant of next firing; absent when not yet computed / canceled. */
  nextFireUtc?: utcDateTime;
}
