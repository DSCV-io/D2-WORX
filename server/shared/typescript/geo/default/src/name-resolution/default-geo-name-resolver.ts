// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type {
  Country,
  CountryCode,
  IGeoNameResolver,
  Subdivision,
} from "@d2/geo-abstractions";
import { compare as levenshteinCompare, normalize } from "@d2/geo-abstractions";
import { TK } from "@d2/i18n-keys";
import type { D2Result } from "@d2/result";
import { notFound, ok, validationFailed } from "@d2/result";
import { falsey, truthyOrUndefined } from "@d2/utilities";

import { CountryLookup } from "../countries.js";
import { SubdivisionLookup } from "../subdivisions.js";

/**
 * Default implementation of `IGeoNameResolver` over the codegen-emitted
 * catalog data. Mirrors the .NET `DefaultGeoNameResolver` byte-for-byte
 * on the cascade algorithm + cache-aside discipline + ambiguity-sentinel
 * pattern + DoS-guard length cap. Both runtimes consume the
 * `confusables.fixture.json` parity fixture; any cross-language drift
 * surfaces on first fixture run.
 *
 * Cascade — each public method runs the same pipeline:
 *
 * 1. **Predicate 0 (DoS guard)** — reject input longer than
 *    `MAX_NAME_LENGTH` (256 chars) before normalization.
 * 2. **Predicate 1 (boundary validation)** — `truthyOrUndefined()` short-
 *    circuits null / undefined / empty / whitespace input to
 *    `validationFailed`. Normalize otherwise; if normalization collapses
 *    to empty, return `validationFailed`.
 * 3. **Pass 1 (exact match)** — always runs, no minimum length. Returns
 *    immediately on first non-ambiguous hit; an ambiguity-sentinel hit
 *    returns `notFound`.
 * 4. **Pass 2 (startsWith)** — skipped when normalized input shorter
 *    than 4 chars; ambiguity at any pass returns `notFound`.
 * 5. **Pass 3 (contains)** — skipped when normalized input shorter than
 *    5 chars.
 * 6. **Pass 4 (Levenshtein)** — skipped when normalized input shorter
 *    than 5 chars; `maxDistance = min(2, floor(len / 5))`.
 *
 * **Cache-aside** — the country map (`countryByName`) is built once per
 * process on first call (JS single-thread execution gives the build-once
 * guarantee). Per-country subdivision maps live in
 * `subdivisionByNameByCountry` and build lazily per parent on first
 * lookup for that parent. The catalog is immutable post-`initializeGeoData`
 * so the cache never invalidates.
 *
 * **Ambiguity sentinel** — when two records normalize to the same name
 * the cache stores a single entry tagged `{ kind: "ambiguous" }`. Pass-1
 * lookups hitting the sentinel return `notFound`; Pass-2 / 3 / 4 walks
 * exclude ambiguous entries from the candidate pool. Both record-presence
 * and ambiguity status publish atomically because they live in one map
 * value (single assignment).
 *
 * **No observability instrumentation** — the resolver intentionally ships
 * without spans / counters / metrics. It runs at every third-party text
 * ingestion point; per-call instrumentation overhead is unacceptable.
 *
 * **PII discipline** — the `name` parameter is treated as opaque PII.
 * The resolver never logs the input, never attaches it to a `D2Result`
 * message field, and never emits it on any future telemetry surface.
 *
 * **TraceId flow** — `D2Result` instances carry no `traceId`; callers
 * replay the handler-scoped traceId at the call site via
 * `result.withTraceId(context.traceId)`.
 */
const MAX_NAME_LENGTH = 256;
const MIN_LENGTH_PASS_2 = 4;
const MIN_LENGTH_PASS_3 = 5;
const MIN_LENGTH_PASS_4 = 5;
const PASS_4_MAX_DISTANCE = 2;
const PASS_4_DISTANCE_SCALE = 5;

// TK constants — sourced from the generated `@d2/i18n-keys` catalog,
// matching the .NET TK SrcGen output from the same spec. Using the
// typed constant (not a raw string) prevents §G render-bug class:
// catalog keys are snake_case, not dot-path.
const TK_NOT_NULL_VIOLATION = TK.common.errors.NOT_NULL_VIOLATION;
const TK_TOO_LONG = TK.common.errors.TOO_LONG;
const TK_NAME_RESOLUTION_NOT_FOUND = TK.geo.errors.NAME_RESOLUTION_NOT_FOUND;
const TK_NAME_RESOLUTION_AMBIGUOUS = TK.geo.errors.NAME_RESOLUTION_AMBIGUOUS;

type CountryCacheEntry =
  | { readonly kind: "record"; readonly record: Country }
  | { readonly kind: "ambiguous" };

type SubdivisionCacheEntry =
  | { readonly kind: "record"; readonly record: Subdivision }
  | { readonly kind: "ambiguous" };

// Module-scoped cache-aside via Map. `undefined` over `null` per the TS
// undefined-over-null convention. JS single-thread execution gives build-
// once semantics under `Promise.all` first-callers — the cache-aside test
// pins this via a build-count spy.
let countryByName: Map<string, CountryCacheEntry> | undefined;
const subdivisionByNameByCountry = new Map<
  CountryCode,
  Map<string, SubdivisionCacheEntry>
>();

// Internal build-count tracker — exported via test-only surface so the
// concurrent-first-caller test can assert the build factory ran exactly
// once. Producers outside test code MUST NOT read this.
let countryBuildCount = 0;
const subdivisionBuildCountByCountry = new Map<CountryCode, number>();

/**
 * @internal
 * Returns the count of cache builds performed for the country name map.
 * Test-only surface — verifies cache-aside semantics.
 */
export function _internalCountryBuildCount(): number {
  return countryBuildCount;
}

/**
 * @internal
 * Returns the count of cache builds performed for a given parent
 * country's subdivision name map. Test-only surface.
 */
export function _internalSubdivisionBuildCount(parent: CountryCode): number {
  return subdivisionBuildCountByCountry.get(parent) ?? 0;
}

/**
 * @internal
 * Resets the cache. Test-only surface — invoked between test runs so
 * cache-aside assertions are deterministic.
 */
export function _internalResetCache(): void {
  countryByName = undefined;
  subdivisionByNameByCountry.clear();
  countryBuildCount = 0;
  subdivisionBuildCountByCountry.clear();
}

/**
 * Resolve a free-form country name. See module-level docstring for the
 * cascade + cache-aside + ambiguity-sentinel semantics.
 */
export function tryResolveCountryByName(name: string): D2Result<Country> {
  // Predicate 0 — DoS guard. Apply before truthy check so an oversized
  // (but technically truthy) input is rejected cheaply.
  if (typeof name === "string" && name.length > MAX_NAME_LENGTH) {
    return validationFailed<Country>({ messages: [TK_TOO_LONG] });
  }

  // Predicate 1 — boundary validation.
  const trimmed = truthyOrUndefined(name);
  if (trimmed === undefined) {
    return validationFailed<Country>({ messages: [TK_NOT_NULL_VIOLATION] });
  }

  const q = normalize(name);
  if (q.length === 0) {
    return validationFailed<Country>({ messages: [TK_NOT_NULL_VIOLATION] });
  }

  const map = getOrBuildCountryMap();
  return runCascadeCountry(q, map);
}

/**
 * Resolve a free-form subdivision name scoped to `parentCountry`. See
 * module-level docstring for the cascade + cache-aside + ambiguity-
 * sentinel semantics.
 */
export function tryResolveSubdivisionByName(
  name: string,
  parentCountry: Country,
): D2Result<Subdivision> {
  // Parent-country precondition.
  if (parentCountry === null || parentCountry === undefined) {
    return validationFailed<Subdivision>({
      messages: [TK_NOT_NULL_VIOLATION],
    });
  }

  if (typeof name === "string" && name.length > MAX_NAME_LENGTH) {
    return validationFailed<Subdivision>({ messages: [TK_TOO_LONG] });
  }

  const trimmed = truthyOrUndefined(name);
  if (trimmed === undefined) {
    return validationFailed<Subdivision>({
      messages: [TK_NOT_NULL_VIOLATION],
    });
  }

  const q = normalize(name);
  if (q.length === 0) {
    return validationFailed<Subdivision>({
      messages: [TK_NOT_NULL_VIOLATION],
    });
  }

  const map = getOrBuildSubdivisionMap(parentCountry.iso31661Alpha2Code);
  return runCascadeSubdivision(q, map);
}

function getOrBuildCountryMap(): Map<string, CountryCacheEntry> {
  let map = countryByName;
  if (map === undefined) {
    countryBuildCount += 1;
    map = buildCountryByName();
    countryByName = map;
  }

  return map;
}

function getOrBuildSubdivisionMap(
  parent: CountryCode,
): Map<string, SubdivisionCacheEntry> {
  let map = subdivisionByNameByCountry.get(parent);
  if (map === undefined) {
    subdivisionBuildCountByCountry.set(
      parent,
      (subdivisionBuildCountByCountry.get(parent) ?? 0) + 1,
    );
    map = buildSubdivisionByName(parent);
    subdivisionByNameByCountry.set(parent, map);
  }

  return map;
}

function buildCountryByName(): Map<string, CountryCacheEntry> {
  const map = new Map<string, CountryCacheEntry>();

  // Deterministic ordering — cross-process / cross-runtime agreement on
  // which entries become ambiguity sentinels. The .NET side orders by the
  // alpha-2 string via `StringComparer.Ordinal`; the JS string comparison
  // matches that exactly because alpha-2 is ASCII.
  const ordered = Object.values(CountryLookup.byCode).sort((a, b) =>
    a.iso31661Alpha2Code < b.iso31661Alpha2Code
      ? -1
      : a.iso31661Alpha2Code > b.iso31661Alpha2Code
        ? 1
        : 0,
  );

  for (const country of ordered) {
    addCountryKey(map, country.displayName, country);
    addCountryKey(map, country.officialName, country);
    addCountryKey(map, country.endonymDisplayName, country);
    addCountryKey(map, country.endonymOfficialName, country);
    addCountryKey(map, country.iso31661Alpha3Code, country);
    addCountryKey(map, country.iso31661NumericCode, country);
  }

  return map;
}

function addCountryKey(
  map: Map<string, CountryCacheEntry>,
  rawName: string | undefined,
  country: Country,
): void {
  const trimmed = truthyOrUndefined(rawName);
  if (trimmed === undefined) return;

  const key = normalize(trimmed);
  if (key.length === 0) return;

  const existing = map.get(key);
  if (existing !== undefined) {
    if (existing.kind === "ambiguous") return;
    if (existing.record !== country) {
      map.set(key, { kind: "ambiguous" });
    }
    return;
  }

  map.set(key, { kind: "record", record: country });
}

function buildSubdivisionByName(
  parent: CountryCode,
): Map<string, SubdivisionCacheEntry> {
  const map = new Map<string, SubdivisionCacheEntry>();
  const subdivisions = SubdivisionLookup.byCountry[parent];
  if (falsey(subdivisions)) return map;

  const ordered = [...subdivisions!].sort((a, b) =>
    a.iso31662Code < b.iso31662Code
      ? -1
      : a.iso31662Code > b.iso31662Code
        ? 1
        : 0,
  );

  for (const sub of ordered) {
    addSubdivisionKey(map, sub.displayName, sub);
    addSubdivisionKey(map, sub.officialName, sub);
    addSubdivisionKey(map, sub.endonymDisplayName, sub);
    addSubdivisionKey(map, sub.endonymOfficialName, sub);
    addSubdivisionKey(map, sub.shortCode, sub);
  }

  return map;
}

function addSubdivisionKey(
  map: Map<string, SubdivisionCacheEntry>,
  rawName: string | undefined,
  sub: Subdivision,
): void {
  const trimmed = truthyOrUndefined(rawName);
  if (trimmed === undefined) return;

  const key = normalize(trimmed);
  if (key.length === 0) return;

  const existing = map.get(key);
  if (existing !== undefined) {
    if (existing.kind === "ambiguous") return;
    if (existing.record !== sub) {
      map.set(key, { kind: "ambiguous" });
    }
    return;
  }

  map.set(key, { kind: "record", record: sub });
}

function runCascadeCountry(
  q: string,
  map: Map<string, CountryCacheEntry>,
): D2Result<Country> {
  // Pass 1 — exact.
  const hit = map.get(q);
  if (hit !== undefined) {
    if (hit.kind === "ambiguous") {
      return notFound<Country>({ messages: [TK_NAME_RESOLUTION_AMBIGUOUS] });
    }
    return ok<Country>(hit.record);
  }

  // Pass 2 — startsWith.
  if (q.length >= MIN_LENGTH_PASS_2) {
    const result = scanCountry(map, q, "startsWith");
    if (result.ambiguous) {
      return notFound<Country>({ messages: [TK_NAME_RESOLUTION_AMBIGUOUS] });
    }
    if (result.record !== undefined) return ok<Country>(result.record);
  }

  // Pass 3 — contains.
  if (q.length >= MIN_LENGTH_PASS_3) {
    const result = scanCountry(map, q, "contains");
    if (result.ambiguous) {
      return notFound<Country>({ messages: [TK_NAME_RESOLUTION_AMBIGUOUS] });
    }
    if (result.record !== undefined) return ok<Country>(result.record);
  }

  // Pass 4 — bounded Levenshtein.
  if (q.length >= MIN_LENGTH_PASS_4) {
    const maxDistance = Math.min(
      PASS_4_MAX_DISTANCE,
      Math.floor(q.length / PASS_4_DISTANCE_SCALE),
    );
    const result = scanLevenshteinCountry(map, q, maxDistance);
    if (result.ambiguous) {
      return notFound<Country>({ messages: [TK_NAME_RESOLUTION_AMBIGUOUS] });
    }
    if (result.record !== undefined) return ok<Country>(result.record);
  }

  return notFound<Country>({ messages: [TK_NAME_RESOLUTION_NOT_FOUND] });
}

function runCascadeSubdivision(
  q: string,
  map: Map<string, SubdivisionCacheEntry>,
): D2Result<Subdivision> {
  const hit = map.get(q);
  if (hit !== undefined) {
    if (hit.kind === "ambiguous") {
      return notFound<Subdivision>({
        messages: [TK_NAME_RESOLUTION_AMBIGUOUS],
      });
    }
    return ok<Subdivision>(hit.record);
  }

  if (q.length >= MIN_LENGTH_PASS_2) {
    const result = scanSubdivision(map, q, "startsWith");
    if (result.ambiguous) {
      return notFound<Subdivision>({
        messages: [TK_NAME_RESOLUTION_AMBIGUOUS],
      });
    }
    if (result.record !== undefined) return ok<Subdivision>(result.record);
  }

  if (q.length >= MIN_LENGTH_PASS_3) {
    const result = scanSubdivision(map, q, "contains");
    if (result.ambiguous) {
      return notFound<Subdivision>({
        messages: [TK_NAME_RESOLUTION_AMBIGUOUS],
      });
    }
    if (result.record !== undefined) return ok<Subdivision>(result.record);
  }

  if (q.length >= MIN_LENGTH_PASS_4) {
    const maxDistance = Math.min(
      PASS_4_MAX_DISTANCE,
      Math.floor(q.length / PASS_4_DISTANCE_SCALE),
    );
    const result = scanLevenshteinSubdivision(map, q, maxDistance);
    if (result.ambiguous) {
      return notFound<Subdivision>({
        messages: [TK_NAME_RESOLUTION_AMBIGUOUS],
      });
    }
    if (result.record !== undefined) return ok<Subdivision>(result.record);
  }

  return notFound<Subdivision>({ messages: [TK_NAME_RESOLUTION_NOT_FOUND] });
}

interface ScanResult<TRecord> {
  readonly record?: TRecord;
  readonly ambiguous: boolean;
}

function scanCountry(
  map: Map<string, CountryCacheEntry>,
  q: string,
  mode: "startsWith" | "contains",
): ScanResult<Country> {
  let winner: Country | undefined;
  for (const [key, entry] of map) {
    if (entry.kind === "ambiguous") continue;
    const match = mode === "startsWith" ? key.startsWith(q) : key.includes(q);
    if (!match) continue;
    if (winner === undefined) {
      winner = entry.record;
      continue;
    }
    if (winner !== entry.record) return { ambiguous: true };
  }
  return { record: winner, ambiguous: false };
}

function scanSubdivision(
  map: Map<string, SubdivisionCacheEntry>,
  q: string,
  mode: "startsWith" | "contains",
): ScanResult<Subdivision> {
  let winner: Subdivision | undefined;
  for (const [key, entry] of map) {
    if (entry.kind === "ambiguous") continue;
    const match = mode === "startsWith" ? key.startsWith(q) : key.includes(q);
    if (!match) continue;
    if (winner === undefined) {
      winner = entry.record;
      continue;
    }
    if (winner !== entry.record) return { ambiguous: true };
  }
  return { record: winner, ambiguous: false };
}

function scanLevenshteinCountry(
  map: Map<string, CountryCacheEntry>,
  q: string,
  maxDistance: number,
): ScanResult<Country> {
  let bestDistance = Number.MAX_SAFE_INTEGER;
  let winner: Country | undefined;
  let ambiguousAtBest = false;

  for (const [key, entry] of map) {
    if (entry.kind === "ambiguous") continue;
    const distance = levenshteinCompare(q, key, maxDistance);
    if (distance > maxDistance) continue;
    if (distance < bestDistance) {
      bestDistance = distance;
      winner = entry.record;
      ambiguousAtBest = false;
    } else if (distance === bestDistance) {
      if (winner === undefined || winner !== entry.record) {
        ambiguousAtBest = true;
      }
    }
  }

  if (ambiguousAtBest) return { ambiguous: true };
  return { record: winner, ambiguous: false };
}

function scanLevenshteinSubdivision(
  map: Map<string, SubdivisionCacheEntry>,
  q: string,
  maxDistance: number,
): ScanResult<Subdivision> {
  let bestDistance = Number.MAX_SAFE_INTEGER;
  let winner: Subdivision | undefined;
  let ambiguousAtBest = false;

  for (const [key, entry] of map) {
    if (entry.kind === "ambiguous") continue;
    const distance = levenshteinCompare(q, key, maxDistance);
    if (distance > maxDistance) continue;
    if (distance < bestDistance) {
      bestDistance = distance;
      winner = entry.record;
      ambiguousAtBest = false;
    } else if (distance === bestDistance) {
      if (winner === undefined || winner !== entry.record) {
        ambiguousAtBest = true;
      }
    }
  }

  if (ambiguousAtBest) return { ambiguous: true };
  return { record: winner, ambiguous: false };
}

/**
 * Class wrapper around the free-function resolvers, conforming to the
 * `IGeoNameResolver` interface for DI parity with the .NET side.
 */
export class DefaultGeoNameResolver implements IGeoNameResolver {
  tryResolveCountryByName(name: string): D2Result<Country> {
    return tryResolveCountryByName(name);
  }

  tryResolveSubdivisionByName(
    name: string,
    parentCountry: Country,
  ): D2Result<Subdivision> {
    return tryResolveSubdivisionByName(name, parentCountry);
  }
}
