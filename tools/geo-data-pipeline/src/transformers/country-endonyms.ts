// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchCldrTerritories } from "../fetchers/cldr-territories.js";

/**
 * Maps locale code → (alpha2 → endonym display name) for a set of CLDR locales.
 * Filters out CLDR `-alt-*` variants (keeping only the canonical name) and UN M49
 * region codes (which CLDR co-mingles with ISO 3166-1 codes in the same file).
 *
 * ISO 3166-1 alpha-2 codes are always 2 ASCII letters; M49 region codes are 3 digits.
 */
const ISO_3166_ALPHA2_PATTERN = /^[A-Z]{2}$/;

export interface CountryEndonyms {
  /** Map: locale code (e.g. "en", "ja") → (alpha2 → endonym). */
  byLocale: Map<string, Map<string, string>>;
  /** Map: locale code → CLDR canonical territory count (for sanity-check reporting). */
  canonicalCountByLocale: Map<string, number>;
}

export async function loadCountryEndonyms(
  locales: readonly string[],
): Promise<CountryEndonyms> {
  const byLocale = new Map<string, Map<string, string>>();
  const canonicalCountByLocale = new Map<string, number>();

  for (const locale of locales) {
    const fetched = await fetchCldrTerritories(locale);
    const alpha2ToName = new Map<string, string>();
    for (const [key, value] of Object.entries(fetched.territories)) {
      if (key.includes("-alt-")) continue;
      if (!ISO_3166_ALPHA2_PATTERN.test(key)) continue;
      alpha2ToName.set(key, value);
    }
    byLocale.set(locale, alpha2ToName);
    canonicalCountByLocale.set(locale, alpha2ToName.size);
  }

  return { byLocale, canonicalCountByLocale };
}

/**
 * Picks the endonym for a country in its own primary language. For multi-language
 * supported countries (Belgium, Switzerland, Canada, etc.) the caller passes the
 * country's primary language code. Returns null if no endonym is available in that
 * language (e.g., country's primary language isn't one of our supported locales).
 */
export function pickEndonymForCountry(
  endonyms: CountryEndonyms,
  alpha2: string,
  primaryLanguageISO6391Code: string | null,
): string | null {
  if (!primaryLanguageISO6391Code) return null;
  const localeMap = endonyms.byLocale.get(primaryLanguageISO6391Code);
  if (!localeMap) return null;
  return localeMap.get(alpha2) ?? null;
}

/**
 * Picks the country's name in a different LOCALE — used when displaying to a user
 * whose preferred locale is NOT the country's primary language. Cross-language country
 * names live in the same CLDR territories.json files.
 */
export function pickLocalizedName(
  endonyms: CountryEndonyms,
  alpha2: string,
  viewerLocaleLanguageCode: string,
): string | null {
  const localeMap = endonyms.byLocale.get(viewerLocaleLanguageCode);
  if (!localeMap) return null;
  return localeMap.get(alpha2) ?? null;
}
