// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Meta-record describing the deprecation status of a geo reference-data
 * entity (country, subdivision, currency, language, locale, timezone, or
 * geopolitical entity). Applies uniformly across every entity, so it lives
 * in this hand-written abstractions slice rather than being emitted per
 * entity by the codegen pipeline. Carried on each record as an optional
 * `deprecation` field — `undefined` means the entity is currently active.
 *
 * Deprecation is a real, persistent concern for ISO reference data: codes
 * like `YU` (Yugoslavia) or `SU` (Soviet Union) live forever in historical
 * records and MUST remain resolvable for hash citations and audit replay.
 * The lookup APIs therefore include deprecated entries by default; UI /
 * selector code that wants to filter them out opts in explicitly via the
 * `activeOnly: true` overload.
 *
 * `supersededBy` is plural to cover splits — when one entity deprecates
 * into multiple successors (e.g. `YU` → `RS, ME, HR, SI, MK, BA, XK`).
 * `successorNote` is free-form prose for nuance the structured field can't
 * capture (e.g. "successor list is approximate; political recognition of
 * XK is partial").
 *
 * Mirrors .NET `D2.Shared.Geo.Abstractions.DeprecationInfo` (sealed record)
 * byte-for-byte over the JSON wire: .NET `DateOnly DeprecatedAt` serializes
 * to ISO-8601 calendar-date string (`"2003-06-04"`), `IReadOnlyList<string>?
 * SupersededBy` to a string array or absent property. The TS-side mirror
 * uses `undefined` (not `null`) for optional fields per workspace TS
 * convention; the wire-shape `null` arriving from .NET nullable-value-type
 * JSON normalizes to `undefined` at the Zod deserialization boundary in
 * 2c-2.
 */
export interface DeprecationInfo {
  /**
   * ISO-8601 calendar date (`YYYY-MM-DD`) on which the entity was
   * deprecated by the upstream authority (typically ISO or the IANA TZDB
   * committee). String-encoded rather than `Date` because the deprecation
   * is a calendar event without a meaningful time-of-day component and JS
   * `Date` always carries a UTC instant.
   */
  readonly deprecatedAt: string;

  /**
   * Short human-readable explanation of why the entity was deprecated.
   * Examples: "country dissolved", "currency replaced by EUR", "tzdb rule
   * consolidation". Not a translation key — this is reference-data
   * metadata, not user-facing copy.
   */
  readonly reason: string;

  /**
   * Canonical codes of the successor entities, or `undefined` when no
   * successor is recorded. Plural to cover splits (one entity deprecating
   * into several). The list is in the spec's natural order — callers MUST
   * NOT assume it is sorted alphabetically or by any other criterion.
   */
  readonly supersededBy?: readonly string[];

  /**
   * Free-form note adding context the structured `supersededBy` list
   * cannot capture (partial recognition, approximate mapping, regional
   * politics, etc.), or `undefined` when no additional note is recorded.
   */
  readonly successorNote?: string;
}
