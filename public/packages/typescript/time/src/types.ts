// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { TK } from "@dcsv-io/d2-i18n-keys";
import type { Temporal } from "temporal-polyfill";
import {
  type D2Result,
  inputError,
  ok,
  validationFailed,
} from "@dcsv-io/d2-result";

/**
 * Translation key for IANA validation failures. Mirrors
 * `TK.Common.Time.INVALID_IANA_IDENTIFIER` from the .NET TK SrcGen catalog
 * (driven by `contracts/messages/en-US.json`).
 */
const TK_INVALID_IANA = TK.common.time.INVALID_IANA_IDENTIFIER;

/**
 * Translation key for missing required parameters. Mirrors
 * `TK.Common.Errors.NOT_NULL_VIOLATION` from the .NET TK SrcGen catalog.
 */
const TK_NOT_NULL = TK.common.errors.NOT_NULL_VIOLATION;

/**
 * Cross-language canonical-name override map. Node.js's `Intl.DateTimeFormat`
 * canonicalizes some aliases (`US/Pacific` → `America/Los_Angeles`) but
 * leaves others as-is (`Asia/Saigon`, `Asia/Calcutta`) — and prefers the
 * shorter form over the longer for split-zones like
 * `America/Argentina/Buenos_Aires` (Node prefers `America/Buenos_Aires`).
 *
 * The .NET NodaTime path resolves through TZDB's CanonicalIdMap which
 * gives a single canonical name per zone. This map encodes the deltas so
 * BOTH languages produce byte-identical IANA strings for the same input —
 * a hard cross-language wire-format contract.
 *
 * Source of truth: NodaTime's `TzdbDateTimeZoneSource.Default.CanonicalIdMap`
 * (queried at .NET runtime; deltas listed here are the empirically-observed
 * cases that differ from Node.js Intl).
 */
const sr_ianaAliasOverrides: Readonly<Record<string, string>> = {
  "Asia/Saigon": "Asia/Ho_Chi_Minh",
  "Asia/Calcutta": "Asia/Kolkata",
  "America/Buenos_Aires": "America/Argentina/Buenos_Aires",
  "America/Catamarca": "America/Argentina/Catamarca",
  "America/Cordoba": "America/Argentina/Cordoba",
  "America/Jujuy": "America/Argentina/Jujuy",
  "America/Mendoza": "America/Argentina/Mendoza",
  "America/Indianapolis": "America/Indiana/Indianapolis",
  "America/Knox_IN": "America/Indiana/Knox",
  "America/Louisville": "America/Kentucky/Louisville",
};

/**
 * Internal helper: validate + normalize an IANA identifier using the
 * browser/runtime `Intl.DateTimeFormat` API, which is the
 * standards-blessed cross-runtime way to canonicalize IANA names without
 * pulling in a separate tzdb.
 *
 * Returns the canonical IANA name on success, or an error discriminant on
 * failure (undefined / empty / whitespace-only / invalid zone / fixed-offset
 * notation).
 *
 * Behavior contract (mirrors the .NET NodaTime path):
 * - Undefined / empty / whitespace-only -> `{ error: "null" }` (caller maps
 *   to `NOT_NULL_VIOLATION`). Callers normalize JSON `null` to `undefined`
 *   at the deserialization boundary per the TS `prefer-undefined-over-null`
 *   convention; this factory does NOT accept `null` on its public surface.
 * - Valid IANA zone -> canonical name from `resolvedOptions().timeZone`.
 * - Deprecated alias (e.g. `"US/Pacific"`) -> canonical (`"America/Los_Angeles"`).
 * - Fixed-offset notation (`"UTC+5"`, `"+05:00"`) -> `{ error: "invalid" }`.
 *   `Intl` throws `RangeError` on these because they're not real time-zone names.
 * - `"Etc/GMT*"` -> accepted as canonical (they ARE in tzdb).
 */
function normalizeIana(
  iana: string | undefined,
): { canonical: string } | { error: "null" | "invalid" } {
  if (iana === undefined || iana.trim() === "") {
    return { error: "null" };
  }
  // Reject fixed-offset notation up-front to stay parity-aligned with the
  // .NET NodaTime path, which rejects "+05:00" / "-08:00" / "UTC+5". Intl
  // accepts these (it normalizes to "+HH:MM"), but they are NOT IANA
  // identifiers — IANA-only is the cross-language contract.
  if (/^[+-]\d{1,2}(?::\d{2})?$/.test(iana)) {
    return { error: "invalid" };
  }
  if (/^(?:UTC|GMT)[+-]/i.test(iana)) {
    return { error: "invalid" };
  }
  try {
    const resolved = new Intl.DateTimeFormat("en-US", {
      timeZone: iana,
    }).resolvedOptions().timeZone;
    // After Intl resolution, double-check: anything that resolves to a
    // pure offset shape ("+05:00") was a fixed-offset zone that survived
    // our up-front filter (e.g. obscure runtime quirks). Reject for parity.
    /* v8 ignore start — defensive: modern Node Intl never resolves a filter-passing input to a bare +HH:MM offset (the up-front regexes catch every offset form first); kept as belt-and-suspenders for runtime quirks */
    if (/^[+-]\d{2}:\d{2}$/.test(resolved)) {
      return { error: "invalid" };
    }
    /* v8 ignore stop */
    // Apply cross-language canonicalization overrides for the cases where
    // Node Intl differs from .NET NodaTime. See sr_ianaAliasOverrides.
    const overridden = sr_ianaAliasOverrides[resolved] ?? resolved;
    return { canonical: overridden };
  } catch {
    return { error: "invalid" };
  }
}

/**
 * Category 1 — Past instant with original wall-clock context.
 *
 * Construct via {@link ZonedInstant.create} — direct construction
 * (`new ZonedInstant(...)`) is private. The factory validates the IANA
 * identifier via the runtime `Intl.DateTimeFormat` API and normalizes
 * deprecated aliases to canonical form (e.g. `"US/Pacific"` →
 * `"America/Los_Angeles"`). Mirrors the .NET `ZonedInstant.Create`
 * smart-constructor.
 *
 * Storage: `event_at TIMESTAMPTZ` (from `instant`) +
 * `event_at_zone TEXT NULL` (from `ianaIdentifier`).
 *
 * Sort/compare: always use `instant` — zone-agnostic and unambiguous.
 */
export class ZonedInstant {
  /** UTC instant of the event. Category 1 — Past instant. */
  readonly instant: Temporal.Instant;
  /** Canonical IANA identifier in effect when the event occurred. */
  readonly ianaIdentifier: string;

  private constructor(instant: Temporal.Instant, canonicalIANA: string) {
    this.instant = instant;
    this.ianaIdentifier = canonicalIANA;
  }

  /**
   * Validates + normalizes the IANA identifier, returning a wrapped
   * {@link D2Result}. Undefined/empty/whitespace → `NOT_NULL_VIOLATION`;
   * unrecognized zone / fixed-offset notation → `INVALID_IANA_IDENTIFIER`.
   * Callers normalize JSON `null` to `undefined` at the deserialization
   * boundary per the TS `prefer-undefined-over-null` convention.
   */
  static create(
    instant: Temporal.Instant,
    ianaIdentifier: string | undefined,
  ): D2Result<ZonedInstant> {
    const result = normalizeIana(ianaIdentifier);
    if ("error" in result) {
      return validationFailed({
        inputErrors: [
          inputError(
            "ianaIdentifier",
            result.error === "null" ? [TK_NOT_NULL] : [TK_INVALID_IANA],
          ),
        ],
      });
    }
    return ok(new ZonedInstant(instant, result.canonical));
  }
}

/**
 * Category 3 — Future local-anchored event.
 *
 * Construct via {@link LocalAnchoredEvent.create}. Use
 * {@link LocalAnchoredEvent.computeNextFire} to (re)derive the UTC firing
 * instant under Temporal's `disambiguation: "compatible"` rule, which
 * matches .NET NodaTime's `Resolvers.LenientResolver` (skipped local
 * times map forward to post-gap; ambiguous local times pick the earlier
 * instant).
 *
 * Storage: `scheduled_local TIMESTAMP` (from `scheduledLocal`) +
 * `scheduled_zone TEXT` (from `ianaIdentifier`) +
 * `next_fire_utc TIMESTAMPTZ NULL` (from `nextFireUtc`).
 *
 * Sort: always use `nextFireUtc`.
 */
export class LocalAnchoredEvent {
  /** Wall-clock date-time of the scheduled event (no zone attached). */
  readonly scheduledLocal: Temporal.PlainDateTime;
  /** Canonical IANA identifier in which `scheduledLocal` is interpreted. */
  readonly ianaIdentifier: string;
  /**
   * Denormalized UTC instant of the next firing, or absent if not yet
   * computed or the event has been canceled. Category 2 — derived from
   * Category 3 source.
   */
  readonly nextFireUtc?: Temporal.Instant;

  private constructor(
    scheduledLocal: Temporal.PlainDateTime,
    canonicalIANA: string,
    nextFireUtc?: Temporal.Instant,
  ) {
    this.scheduledLocal = scheduledLocal;
    this.ianaIdentifier = canonicalIANA;
    this.nextFireUtc = nextFireUtc;
  }

  /**
   * Validates + normalizes the IANA identifier, returning a wrapped
   * {@link D2Result}. Undefined/empty/whitespace → `NOT_NULL_VIOLATION`;
   * unrecognized zone / fixed-offset notation → `INVALID_IANA_IDENTIFIER`.
   * Callers normalize JSON `null` to `undefined` at the deserialization
   * boundary per the TS `prefer-undefined-over-null` convention.
   */
  static create(
    scheduledLocal: Temporal.PlainDateTime,
    ianaIdentifier: string | undefined,
    nextFireUtc?: Temporal.Instant,
  ): D2Result<LocalAnchoredEvent> {
    const result = normalizeIana(ianaIdentifier);
    if ("error" in result) {
      return validationFailed({
        inputErrors: [
          inputError(
            "ianaIdentifier",
            result.error === "null" ? [TK_NOT_NULL] : [TK_INVALID_IANA],
          ),
        ],
      });
    }
    return ok(
      new LocalAnchoredEvent(scheduledLocal, result.canonical, nextFireUtc),
    );
  }

  /**
   * Computes the UTC instant of next firing using Temporal's
   * `disambiguation: "compatible"` (matches .NET NodaTime
   * `Resolvers.LenientResolver` — skipped local times map forward to
   * post-gap; ambiguous local times pick the earlier instant).
   *
   * Returns `D2Result` for API parity with the .NET side. In practice
   * this cannot fail (IANA was pre-validated at construction; Temporal
   * disambiguation handles every DST case without throwing).
   */
  computeNextFire(): D2Result<Temporal.Instant> {
    const zoned = this.scheduledLocal.toZonedDateTime(this.ianaIdentifier, {
      disambiguation: "compatible",
    });
    return ok(zoned.toInstant());
  }
}
