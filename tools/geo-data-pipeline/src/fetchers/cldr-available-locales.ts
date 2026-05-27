// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-core";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";
// upstream URL — cannot wrap
const SOURCE_URL =
  "https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-core/availableLocales.json";

/**
 * CLDR's authoritative catalog of locales (`availableLocales.json`):
 *
 * ```
 * {
 *   "availableLocales": {
 *     "modern": ["en", "es", "fr", ...],
 *     "full":   ["aa", "aa-DJ", "aa-ER", "ab", "af", "af-NA", ...]
 *   }
 * }
 * ```
 *
 * - `full` is the comprehensive set (~766 locales). Used as the catalog source.
 * - `modern` is the "actively used in modern computing" subset (often empty for upstream defaults).
 * - Tags follow IETF BCP 47: `<lang>[-<Script>][-<REGION>][-<variant>]`.
 *   Examples: `en-US` (lang-region), `zh-Hans-CN` (lang-script-region),
 *   `en-001` (lang-UN-M49-region: World English).
 */
export interface AvailableLocalesPayload {
  availableLocales: {
    modern: string[];
    full: string[];
  };
}

export interface AvailableLocalesFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  /** All locale tags from `full` — the comprehensive catalog. */
  fullTags: readonly string[];
  /**
   * Locale tags from `modern` — the actively-used subset
   * (informational; may be empty upstream).
   */
  modernTags: readonly string[];
}

export async function fetchCldrAvailableLocales(options?: {
  ttlHours?: number;
}): Promise<AvailableLocalesFetchResult> {
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url: SOURCE_URL,
    license: SOURCE_LICENSE,
    cacheKey: "availableLocales.json",
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(
    fetched.body.toString("utf8"),
  ) as AvailableLocalesPayload;
  return {
    fullTags: payload.availableLocales.full,
    modernTags: payload.availableLocales.modern,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}
