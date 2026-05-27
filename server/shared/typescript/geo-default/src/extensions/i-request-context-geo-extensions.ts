// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { Country, Subdivision } from "@d2/geo-abstractions";
import {
  ALL_COUNTRY_CODE_SET,
  SUBDIVISION_CODE_SET,
} from "@d2/geo-abstractions";
import type { IRequestContext } from "@d2/request-context-abstractions";
import { truthyOrUndefined } from "@d2/utilities";

import { CountryLookup } from "../countries.js";
import { SubdivisionLookup } from "../subdivisions.js";

/**
 * Default-layer record-returning helpers over `IRequestContext` geo
 * fields. Mirrors the .NET `D2.Shared.Geo.Default.Extensions.IRequestContextGeoExtensions`
 * surface. Returns the full typed record (e.g. `Country`) instead of the
 * raw alpha-2 / `US-NY`-style string so callers can read nested data
 * (`countryFor(request)?.primaryLanguage?.displayName`) without a second
 * catalog lookup.
 *
 * **Naming.** TypeScript has no equivalent of .NET's namespace-shadowed
 * extension methods, so the TS helpers carry distinct `*For()` names
 * (rather than reusing the field name `country` / `subdivision`) to make
 * the call site unambiguous when both record-form and code-form
 * accessors might coexist in future. The .NET surface uses
 * `request.Country()` because C# extension members can shadow safely
 * via `using`-directive disambiguation; the TS surface uses
 * `countryFor(request)` because no such language facility exists.
 *
 * **Boundary contract.** Each helper:
 *
 * 1. Reads the raw `countryIso31661Alpha2Code` / `subdivisionIso31662Code`
 *    field from the request context. The field type is `string | undefined`
 *    per spec-derived `IRequestContext`.
 * 2. Treats undefined / empty / whitespace as "geo signal absent" and
 *    returns `undefined`.
 * 3. Validates the raw string against the spec-derived closed-set
 *    validation table (`ALL_COUNTRY_CODE_SET` / `SUBDIVISION_CODE_SET`)
 *    — a JWT claim could carry an out-of-date code from a session
 *    minted before a catalog change.
 * 4. Looks up the record in `CountryLookup.byCode` /
 *    `SubdivisionLookup.byCode`. A defensive miss (catalog entry
 *    pruned between validation table and lookup table) returns
 *    `undefined`.
 *
 * **Logging guidance for callers.** The fields these helpers return
 * derive from upstream IP-geolocation enrichment. Display strings such
 * as `displayName` are not PII themselves but their context (a session
 * resolving to a specific country) can be. When logging from a request
 * context prefer the canonical `iso31661Alpha2Code` / `iso31662Code`
 * (short, stable, audit-friendly) over the free-form `displayName` to
 * keep log shapes stable and reduce locale coupling.
 */

/**
 * Returns the full `Country` record for the request context's raw
 * `countryIso31661Alpha2Code` field, or `undefined` when the raw value is
 * absent / unparseable / unknown to the catalog. The raw string is uppercased
 * before validation so lowercase / mixed-case values (e.g. "us", "Us")
 * resolve to the canonical record — matching the .NET parser's
 * `ignoreCase: true` contract.
 */
export function countryFor(context: IRequestContext): Country | undefined {
  const trimmed = truthyOrUndefined(context.countryIso31661Alpha2Code);
  if (trimmed === undefined) return undefined;
  const normalized = trimmed.toUpperCase();
  if (!ALL_COUNTRY_CODE_SET.has(normalized)) return undefined;
  return CountryLookup.byCode[normalized];
}

/**
 * Returns the full `Subdivision` record for the request context's raw
 * `subdivisionIso31662Code` field, or `undefined` when the raw value is absent
 * / unparseable / unknown to the catalog. The raw string is uppercased before
 * validation so lowercase / mixed-case values (e.g. "us-ny", "Us-Ny") resolve
 * to the canonical record — matching the cross-language lenient parser contract.
 */
export function subdivisionFor(
  context: IRequestContext,
): Subdivision | undefined {
  const trimmed = truthyOrUndefined(context.subdivisionIso31662Code);
  if (trimmed === undefined) return undefined;
  const normalized = trimmed.toUpperCase();
  if (!SUBDIVISION_CODE_SET.has(normalized)) return undefined;
  return SubdivisionLookup.byCode[normalized];
}
