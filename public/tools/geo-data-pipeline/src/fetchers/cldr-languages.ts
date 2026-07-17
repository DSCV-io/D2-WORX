// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-localenames-full";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";

/**
 * Per-locale CLDR language display names — sibling to `cldr-territories.ts` but for
 * language identifiers.
 *
 * Shape:
 * ```
 * {
 *   "main": {
 *     "en": {
 *       "identity": { "language": "en" },
 *       "localeDisplayNames": {
 *         "languages": {
 *           "aa": "Afar",
 *           "ar": "Arabic",
 *           "ar-001": "Modern Standard Arabic",
 *           "ar-alt-menu": "Arabic, Standard"
 *         }
 *       }
 *     }
 *   }
 * }
 * ```
 *
 * Keys include three flavors:
 *  - bare ISO 639-1/639-3 codes ("aa", "ja")
 *  - region-qualified ("ar-001" = Modern Standard Arabic)
 *  - `-alt-` variants ("ar-alt-menu" = sort-friendly menu form)
 *
 * We consume the BARE 2-letter ISO 639-1 keys only — region/alt variants are filtered out.
 */
export interface CldrLanguagesPayload {
  main: Record<
    string,
    {
      identity: { language: string };
      localeDisplayNames: { languages: Record<string, string> };
    }
  >;
}

export interface CldrLanguagesFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  /** Locale this file is FOR (e.g. "en", "ja") — names are IN this language. */
  locale: string;
  /**
   * Map of foreign-language code → its name in this locale's language.
   * Bare 2-letter codes only.
   */
  namesByLangCode: Map<string, string>;
}

export async function fetchCldrLanguages(
  locale: string,
  options?: {
    ttlHours?: number;
  },
): Promise<CldrLanguagesFetchResult> {
  // upstream URL — cannot wrap
  const url = `https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-localenames-full/main/${locale}/languages.json`;
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url,
    license: SOURCE_LICENSE,
    cacheKey: `${locale}-languages.json`,
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(
    fetched.body.toString("utf8"),
  ) as CldrLanguagesPayload;
  const rawLanguages =
    payload.main[locale]?.localeDisplayNames?.languages ?? {};
  const namesByLangCode = new Map<string, string>();
  for (const [key, value] of Object.entries(rawLanguages)) {
    // Filter to bare 2-letter codes — drop region-qualified ("ar-001") and -alt-* variants
    if (key.length === 2 && /^[a-z]{2}$/.test(key)) {
      namesByLangCode.set(key, value);
    }
  }
  return {
    locale,
    namesByLangCode,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}
