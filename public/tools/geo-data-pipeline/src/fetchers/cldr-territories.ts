// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-localenames-full";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";
// upstream URL — cannot wrap
const URL_TEMPLATE =
  "https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-localenames-full/main/{locale}/territories.json";

/**
 * CLDR `territories.json` shape (per locale):
 *
 * ```
 * {
 *   "main": {
 *     "<locale>": {
 *       "identity": { ... },
 *       "localeDisplayNames": {
 *         "territories": {
 *           "US": "United States",
 *           "US-alt-short": "US",
 *           "US-alt-variant": "United States of America",
 *           "GB": "United Kingdom",
 *           "GB-alt-short": "UK",
 *           ...
 *         }
 *       }
 *     }
 *   }
 * }
 * ```
 *
 * - Keys are ISO 3166-1 alpha-2 codes (some also numeric UN M49 codes like "001" for World)
 * - `<code>` = canonical/long-form territory name (what we want for endonym)
 * - `<code>-alt-short` = abbreviated form (e.g., "US", "UK")
 * - `<code>-alt-variant` = alternate name (e.g., "USA")
 * - Some locales have `-alt-menu` or `-alt-biot` variants too
 */
export interface CldrTerritoriesPayload {
  main: Record<
    string,
    {
      identity: {
        language: string;
        version?: { _cldrVersion?: string };
        [k: string]: unknown;
      };
      localeDisplayNames: {
        territories: Record<string, string>;
      };
    }
  >;
}

export interface CldrTerritoriesFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  locale: string;
  territories: Record<string, string>;
  identity: { language: string; cldrVersion: string | null };
  /**
   * Count of canonical (non-alt) territory entries.
   * Excludes `-alt-short`, `-alt-variant`, `-alt-menu`, etc.
   */
  canonicalCount: number;
  /**
   * Count of `-alt-*` variants in the payload (alongside canonical entries).
   */
  altVariantCount: number;
}

export async function fetchCldrTerritories(
  locale: string,
  options?: { ttlHours?: number },
): Promise<CldrTerritoriesFetchResult> {
  const url = URL_TEMPLATE.replace("{locale}", locale);
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url,
    license: SOURCE_LICENSE,
    cacheKey: `${locale}-territories.json`,
    ttlHours: options?.ttlHours,
  });

  const payload = JSON.parse(
    fetched.body.toString("utf8"),
  ) as CldrTerritoriesPayload;
  const mainKeys = Object.keys(payload.main);
  if (mainKeys.length === 0 || !mainKeys[0]) {
    throw new Error(
      `CLDR territories payload for ${locale} has empty 'main' object`,
    );
  }
  const inner = payload.main[mainKeys[0]];
  if (!inner) {
    throw new Error(
      `CLDR territories payload for ${locale} missing inner locale block`,
    );
  }
  const territories = inner.localeDisplayNames.territories;

  let canonicalCount = 0;
  let altVariantCount = 0;
  for (const key of Object.keys(territories)) {
    if (key.includes("-alt-")) altVariantCount++;
    else canonicalCount++;
  }

  return {
    locale: mainKeys[0],
    territories,
    identity: {
      language: inner.identity.language,
      cldrVersion: inner.identity.version?._cldrVersion ?? null,
    },
    canonicalCount,
    altVariantCount,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

if (
  process.argv[1]?.endsWith("cldr-territories.ts") ||
  process.argv[1]?.endsWith("cldr-territories.js")
) {
  const targetLocale = process.argv[2] ?? "en";
  const result = await fetchCldrTerritories(targetLocale);

  // Sample 10 keys
  const sample: Record<string, string> = {};
  let n = 0;
  for (const [k, v] of Object.entries(result.territories)) {
    if (k.includes("-alt-")) continue;
    sample[k] = v;
    if (++n >= 10) break;
  }
  // Find US specifically (interesting variants)
  const usVariants: Record<string, string> = {};
  for (const [k, v] of Object.entries(result.territories)) {
    if (k.startsWith("US")) usVariants[k] = v;
  }

  console.log(
    JSON.stringify(
      {
        fromCache: result.fromCache,
        locale: result.locale,
        identity: result.identity,
        canonicalCount: result.canonicalCount,
        altVariantCount: result.altVariantCount,
        provenance: result.provenance,
        sampleCanonical: sample,
        usVariants,
      },
      null,
      2,
    ),
  );
}
