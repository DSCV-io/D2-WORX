// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-subdivisions-full";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";
// upstream URL — cannot wrap
const URL_TEMPLATE =
  "https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-subdivisions-full/subdivisions/{locale}/{locale}.json";

/**
 * CLDR `cldr-subdivisions-full/subdivisions/{locale}/{locale}.json` shape:
 *
 * ```
 * {
 *   "subdivisions": {
 *     "identity": { "language": "<locale>" },
 *     "localeDisplayNames": {
 *       "subdivisions": {
 *         "usca": "California",
 *         "usny": "New York",
 *         "gben": "England",
 *         "afbds": "Badakhshan",
 *         "jp13": "Tokyo",
 *         ...
 *       }
 *     }
 *   }
 * }
 * ```
 *
 * **NOTE on the top-level key**: it's `"subdivisions"` NOT `"main"` — different from
 * cldr-localenames-full's territories.json which uses `"main": { "<locale>": ... }`.
 * IDs are LOWERCASE concatenated form: the first 2 chars are the ISO 3166-1 alpha-2
 * country code; the rest is the subdivision suffix. To get standard ISO 3166-2 form,
 * split at index 2 and join with "-" after uppercasing. E.g.:
 *   "usca" → "US-CA"
 *   "afbds" → "AF-BDS"
 *   "jp13" → "JP-13"
 *   "frara" → "FR-ARA"
 */
export interface CldrSubdivisionsPayload {
  subdivisions: {
    identity: { language: string; [k: string]: unknown };
    localeDisplayNames: {
      subdivisions: Record<string, string>;
    };
  };
}

export interface CldrSubdivisionsFetchResult extends Pick<CachedFetch, "provenance" | "fromCache"> {
  locale: string;
  /** Raw CLDR concatenated-id → display name (e.g. `usca` → `California`). */
  rawIdToName: Record<string, string>;
  entryCount: number;
}

export async function fetchCldrSubdivisions(
  locale: string,
  options?: { ttlHours?: number },
): Promise<CldrSubdivisionsFetchResult> {
  const url = URL_TEMPLATE.replaceAll("{locale}", locale);
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url,
    license: SOURCE_LICENSE,
    cacheKey: `${locale}-subdivisions.json`,
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(fetched.body.toString("utf8")) as CldrSubdivisionsPayload;
  const rawIdToName = payload.subdivisions.localeDisplayNames.subdivisions;
  return {
    locale,
    rawIdToName,
    entryCount: Object.keys(rawIdToName).length,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

/**
 * Converts CLDR's lowercase concatenated subdivision id into standard ISO 3166-2 form.
 * Example: "usca" → "US-CA"; "afbds" → "AF-BDS"; "jp13" → "JP-13"; "frara" → "FR-ARA".
 * Returns null when the id is too short to split (< 3 chars).
 */
export function cldrIdToIso31662(rawId: string): string | null {
  if (rawId.length < 3) return null;
  const countryPart = rawId.substring(0, 2).toUpperCase();
  const subdivisionPart = rawId.substring(2).toUpperCase();
  if (!/^[A-Z]{2}$/.test(countryPart)) return null;
  if (!/^[A-Z0-9]+$/.test(subdivisionPart)) return null;
  return `${countryPart}-${subdivisionPart}`;
}

if (
  process.argv[1]?.endsWith("cldr-subdivisions.ts") ||
  process.argv[1]?.endsWith("cldr-subdivisions.js")
) {
  const targetLocale = process.argv[2] ?? "en";
  const result = await fetchCldrSubdivisions(targetLocale);
  // Sample
  const samples: Record<string, string> = {};
  let n = 0;
  for (const [k, v] of Object.entries(result.rawIdToName)) {
    samples[`${k} → ${cldrIdToIso31662(k)}`] = v;
    if (++n >= 10) break;
  }
  // Distribution
  const byCountry: Record<string, number> = {};
  for (const k of Object.keys(result.rawIdToName)) {
    const cc = k.substring(0, 2).toUpperCase();
    byCountry[cc] = (byCountry[cc] ?? 0) + 1;
  }
  const top10 = Object.entries(byCountry)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10);
  console.log(
    JSON.stringify(
      {
        fromCache: result.fromCache,
        locale: result.locale,
        entryCount: result.entryCount,
        provenance: result.provenance,
        sampleEntries: samples,
        topCountriesBySubdivCount: top10,
      },
      null,
      2,
    ),
  );
}
