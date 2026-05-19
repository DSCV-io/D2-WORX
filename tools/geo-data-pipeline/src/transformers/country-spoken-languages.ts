// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  fetchCldrSupplemental,
  type TerritoryInfoPayload,
} from "../fetchers/cldr-supplemental.js";
import type { FetchProvenance } from "../util/cache.js";

/**
 * Spoken-language entry per country, derived from CLDR territoryInfo.languagePopulation.
 * Sample (US):
 *   { iso6391Code: "en", populationPercent: 78.6, officialStatus: "de_facto_official" }
 *   { iso6391Code: "es", populationPercent: 13.4, officialStatus: null }
 *   ...
 */
export interface CountrySpokenLanguageEntry {
  /**
   * ISO 639-1 (2-letter) when available; CLDR may use ISO 639-3 (3-letter) for less-common langs.
   */
  languageCode: string;
  /**
   * Percent of country population that speaks this language (decimal, e.g. 78.6 for 78.6%).
   * Null when CLDR omits.
   */
  populationPercent: number | null;
  /**
   * CLDR `_officialStatus` value when present:
   *   - "official" — constitutionally official
   *   - "de_facto_official" — official in practice (e.g., en in US)
   *   - "official_regional" — official at sub-national level
   *   - "official_minority" — official-minority status
   * Null when language has no official designation.
   */
  officialStatus: string | null;
  /** Percent that writes the language (CLDR `_writingPercent`). Null when CLDR omits. */
  writingPercent: number | null;
}

export interface SpokenLanguagesLoadResult {
  /** Map of ISO 3166-1 alpha-2 → ordered list of spoken languages (population desc). */
  byCountry: Map<string, CountrySpokenLanguageEntry[]>;
  provenance: FetchProvenance;
}

/**
 * Loads CLDR territoryInfo + inverts to per-country spoken-language entries sorted by
 * population percent descending. Only includes regions whose key is a 2-char ISO 3166-1
 * alpha-2 code (skips UN M49 numeric regions like "001"=World).
 */
export async function loadCountrySpokenLanguages(): Promise<SpokenLanguagesLoadResult> {
  const fetched = await fetchCldrSupplemental<TerritoryInfoPayload>("territoryInfo");
  const territoryInfo = fetched.payload.supplemental.territoryInfo;

  const byCountry = new Map<string, CountrySpokenLanguageEntry[]>();
  for (const [region, info] of Object.entries(territoryInfo)) {
    if (region.length !== 2 || !/^[A-Z]{2}$/.test(region)) continue;
    const langPop = info.languagePopulation;
    if (!langPop) continue;

    const entries: CountrySpokenLanguageEntry[] = [];
    for (const [lang, stats] of Object.entries(langPop)) {
      entries.push({
        languageCode: lang,
        populationPercent: parseFloatStrict(stats._populationPercent),
        officialStatus: stats._officialStatus ?? null,
        writingPercent: parseFloatStrict(stats._writingPercent),
      });
    }
    entries.sort((a, b) => (b.populationPercent ?? 0) - (a.populationPercent ?? 0));
    byCountry.set(region, entries);
  }

  return {
    byCountry,
    provenance: fetched.provenance,
  };
}

function parseFloatStrict(value: string | undefined): number | null {
  if (!value) return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}
