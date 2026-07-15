// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result } from "@dcsv-io/d2-result";

import type { Country } from "../generated/country.g.js";
import type { Subdivision } from "../generated/subdivision.g.js";

/**
 * Mirror of .NET `DcsvIo.D2.Geo.Abstractions.NameResolution.IGeoNameResolver`
 * — the cascade-based free-form text → entity resolver for countries and
 * subdivisions. Resolves `"United States"` → the `Country` record,
 * `"California"` (within US) → the `Subdivision` record, etc.
 *
 * Cascade order (Pass 1 → Pass 4):
 *
 * 1. **Pass 1** — exact normalized match (always runs, no min input length).
 * 2. **Pass 2** — `startsWith` prefix match (skipped if `q.length < 4`).
 * 3. **Pass 3** — `contains` substring match (skipped if `q.length < 5`).
 * 4. **Pass 4** — Levenshtein fuzzy match (skipped if `q.length < 5`;
 *    per-pass `maxDistance ≤ min(2, floor(q.length / 5))`).
 *
 * Each pass runs over the catalog's matchable name fields (`displayName`,
 * `officialName`, endonym fields, alpha-3 / numeric codes for countries) and
 * short-circuits the cascade as soon as a pass produces ≥1 match. The
 * `normalize()` pipeline at `./name-normalizer.ts` produces the comparison
 * key on both sides.
 *
 * **Fail-closed semantics**: implementations MUST
 * return `D2Result.notFound(...)` rather than guessing when input is
 * empty / whitespace / too short for the cascade pass that would have
 * matched it / ambiguous (multiple matches at the same score within a
 * pass) / no-match at all passes. Rationale: incorrect resolution
 * silently corrupts downstream data; unresolved outcomes are safe
 * because the raw upstream string is preserved in the audit trail.
 *
 * **Cache-aside semantics**: implementations build their normalized-name
 * → record map lazily on first call. First lookup is O(n) over the
 * catalog; subsequent lookups are O(1) against the cached map. Mirrors
 * .NET `DefaultGeoNameResolver` semantics.
 *
 * `D2Result<T>` (from `@dcsv-io/d2-result`) is the cross-language result
 * envelope — mirrors .NET `D2Result<T>` byte-for-byte over the wire.
 *
 * Both .NET and TS resolvers return full records (never bare codes) so
 * callers get the natural fields immediately without a second lookup
 * — codes are available as a property on each returned record.
 */
export interface IGeoNameResolver {
  /**
   * Resolve a free-form country name to the catalog `Country` record.
   *
   * @example
   * `tryResolveCountryByName("United States")` →
   *   `D2Result.ok(Countries.US)`
   * `tryResolveCountryByName("IR")` → `D2Result.notFound(...)` (too short
   *   for Pass 2-4 cascade, no Pass-1 exact hit).
   */
  tryResolveCountryByName(name: string): D2Result<Country>;

  /**
   * Resolve a free-form subdivision name within the scope of
   * `parentCountry` to the catalog `Subdivision` record. Scoping is
   * REQUIRED — `"Western"` is ambiguous globally but unique within most
   * countries; `"Georgia"` is BOTH a country (GE) AND a US state (US-GA),
   * and the resolver MUST NOT silently pick one.
   *
   * @example
   * `tryResolveSubdivisionByName("California", Countries.US)`
   *   → `D2Result.ok(Subdivision{ iso31662Code: "US-CA", ... })`
   * `tryResolveSubdivisionByName("Carolina", Countries.US)`
   *   → `D2Result.notFound(...)` (Pass-3 contains-ambiguity — matches
   *      both North + South Carolina; fail-closed).
   */
  tryResolveSubdivisionByName(
    name: string,
    parentCountry: Country,
  ): D2Result<Subdivision>;
}
