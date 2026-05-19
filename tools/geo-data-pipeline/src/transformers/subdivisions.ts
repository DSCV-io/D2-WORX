// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  fetchCldrSubdivisions,
  type CldrSubdivisionsFetchResult,
} from "../fetchers/cldr-subdivisions.js";
import {
  computeOrder,
  fetchDebianIso31662,
  type DebianSubdivisionEntry,
} from "../fetchers/debian-iso-codes.js";
import { fetchSubdivisionEndonyms } from "../fetchers/wikidata-endonyms.js";
import type { FetchProvenance } from "../util/cache.js";
import { getEndonymLanguageList } from "../util/endonym-languages.js";

/**
 * Final Subdivision spec entry shape. Combines:
 * - debian/iso-codes for authoritative hierarchy: code, type, parent, order
 * - CLDR cldr-subdivisions-full for localized names across 11 supported languages +
 *   the country's primary-language endonym
 */
export interface SubdivisionPartial {
  /** Canonical ISO 3166-2 code (e.g., "US-CA"). PK. */
  iso31662Code: string;
  /** Suffix after the dash, e.g. "CA" for US-CA. Useful for compact display. */
  shortCode: string;
  /** Owning country (ISO 3166-1 alpha-2). FK to Country catalog. */
  countryISO31661Alpha2Code: string;
  /**
   * English display name. From debian iso-codes (authoritative ISO 3166-2 name);
   * falls back to CLDR EN when debian missing.
   */
  displayName: string;
  /**
   * English official name. Same as displayName — debian + CLDR don't distinguish
   * official vs display for subdivisions.
   */
  officialName: string;
  /**
   * Country-primary-language endonym. Looked up from the supported-language CLDR file
   * matching the country's primary language. Null when the country's primary language
   * isn't in the 11 supported set (e.g., RU subdivisions for Russia whose primary lang
   * "ru" isn't supported → null endonym).
   */
  endonymDisplayName: string | null;
  /**
   * Localized names across the 11 supported languages. Sparse where CLDR coverage gaps.
   * Keys are ISO 639-1 codes.
   */
  localizedDisplayNames: Record<string, string>;
  /**
   * ISO 3166-2 subdivision type per the standard. From debian iso-codes.
   * Examples: "State", "Province", "Region", "Prefecture", "Canton", "Parish",
   * "Rayon", "Department", "County", "Governorate", "Autonomous community", etc.
   */
  type: string;
  /**
   * Parent ISO 3166-2 code (e.g., "GB-ENG" for many UK second-order entries).
   * Null for first-order subdivisions directly under the country.
   */
  parentISO31662Code: string | null;
  /**
   * 1 for first-order (no parent), 2 for second-order (parent has no parent), 3+ for deeper.
   * The catalog ships ALL orders (data layer); consumers filter on this field to restrict
   * to a subset (e.g., first-order only for a country picker UI).
   */
  order: number;
}

/**
 * Per-country breakdown of subdivision orders. Generated alongside the spec to
 * answer "which countries have second/third-order data, and how much?"
 */
export interface CountryOrderReport {
  countryISO31661Alpha2Code: string;
  firstOrder: number;
  secondOrder: number;
  thirdPlusOrder: number;
  total: number;
}

const SUPPORTED_LANGUAGE_CODES = [
  "en",
  "es",
  "fr",
  "de",
  "it",
  "ja",
  "nl",
  "ko",
  "zh",
  "pt",
  "pl",
] as const;

export interface SubdivisionsLoadResult {
  entries: SubdivisionPartial[];
  /** Per-country order breakdown for the report section of the spec. */
  countryOrderReports: CountryOrderReport[];
  /** Counts of debian entries with no CLDR match (interesting drift between sources). */
  debianOnly: number;
  cldrOnly: number;
  /** All fetch provenances for the spec's sources[] block. */
  provenance: FetchProvenance[];
  /** Wikidata fill counts (informational; not a regression signal). */
  wikidataEndonymFills: number;
  wikidataLocalizedAdds: number;
}

export async function loadSubdivisions(
  countryPrimaryLanguageMap: Map<string, string>,
): Promise<SubdivisionsLoadResult> {
  console.error(`[fetch] debian/iso-codes iso_3166-2.json`);
  const debian = await fetchDebianIso31662();

  console.error(`[fetch] CLDR subdivisions for 11 supported languages`);
  const cldrByLocale = new Map<string, CldrSubdivisionsFetchResult>();
  const provenance: FetchProvenance[] = [debian.provenance];
  for (const locale of SUPPORTED_LANGUAGE_CODES) {
    const r = await fetchCldrSubdivisions(locale);
    cldrByLocale.set(locale, r);
    provenance.push(r.provenance);
  }

  // Build per-debian-entry spec entries
  const entries: SubdivisionPartial[] = [];
  const allCldrCodes = new Set<string>();
  for (const r of cldrByLocale.values()) {
    for (const k of Object.keys(r.rawIdToName)) {
      allCldrCodes.add(k); // CLDR concatenated form e.g. "usca"
    }
  }
  const seenCodes = new Set<string>();
  let unresolvedOrder = 0;

  for (const debEntry of debian.entries) {
    const isoCode = debEntry.code.toUpperCase();
    if (seenCodes.has(isoCode)) continue;
    seenCodes.add(isoCode);

    const parts = isoCode.split("-");
    const countryCode = parts[0];
    const shortCode = parts.slice(1).join("-"); // preserves multi-dash second-order codes
    if (!countryCode || !shortCode) continue;

    const order = computeOrder(debEntry, debian.byCode);
    if (order === null) {
      unresolvedOrder++;
      continue;
    }

    // CLDR lookups
    const cldrConcatenated = isoCode.toLowerCase().split("-").join("");
    const localizedDisplayNames: Record<string, string> = {};
    for (const locale of SUPPORTED_LANGUAGE_CODES) {
      const name = cldrByLocale.get(locale)?.rawIdToName[cldrConcatenated];
      if (name) localizedDisplayNames[locale] = name;
    }

    const primaryLang = countryPrimaryLanguageMap.get(countryCode) ?? null;
    const endonymDisplayName = primaryLang ? (localizedDisplayNames[primaryLang] ?? null) : null;

    // Display name: prefer CLDR EN (consistent with Country pattern); fall back to debian
    const cldrEn = localizedDisplayNames["en"];
    const displayName = cldrEn ?? debEntry.name;
    const officialName = displayName; // no source distinguishes official from display

    entries.push({
      iso31662Code: isoCode,
      shortCode,
      countryISO31661Alpha2Code: countryCode,
      displayName,
      officialName,
      endonymDisplayName,
      localizedDisplayNames,
      type: debEntry.type,
      parentISO31662Code: debEntry.parent ? debEntry.parent.toUpperCase() : null,
      order,
    });
  }

  if (unresolvedOrder > 0) {
    console.error(
      `  [WARN] ${unresolvedOrder} debian entries dropped due to unresolvable parent chain`,
    );
  }

  // Tally entries CLDR has but debian doesn't (and vice versa)
  const debianCodes = new Set(
    debian.entries.map((e) => e.code.toUpperCase().toLowerCase().split("-").join("")),
  );
  let cldrOnly = 0;
  for (const c of allCldrCodes) if (!debianCodes.has(c)) cldrOnly++;
  let debianOnly = 0;
  for (const c of debianCodes) if (!allCldrCodes.has(c)) debianOnly++;

  // Per-country order breakdown
  const reportMap = new Map<string, CountryOrderReport>();
  for (const e of entries) {
    let row = reportMap.get(e.countryISO31661Alpha2Code);
    if (!row) {
      row = {
        countryISO31661Alpha2Code: e.countryISO31661Alpha2Code,
        firstOrder: 0,
        secondOrder: 0,
        thirdPlusOrder: 0,
        total: 0,
      };
      reportMap.set(e.countryISO31661Alpha2Code, row);
    }
    if (e.order === 1) row.firstOrder++;
    else if (e.order === 2) row.secondOrder++;
    else row.thirdPlusOrder++;
    row.total++;
  }
  const countryOrderReports = [...reportMap.values()].sort((a, b) => b.total - a.total);

  // Wikidata SPARQL endonym layer (CC0). Replaces CLDR's sparse non-English subdivision
  // names with Wikidata's much broader coverage (89-97% per language vs CLDR's near-zero
  // for ja/zh/ko). For our specific endonym use case (per-subdivision name in country's
  // primary language), Wikidata covers ~98%+ of major-country subdivisions. CLDR remains
  // the source for English displayName (already locked above).
  const endonymLangs = await getEndonymLanguageList();
  console.error(`[fetch] Wikidata subdivision endonyms (${endonymLangs.length} languages)`);
  const wikidata = await fetchSubdivisionEndonyms(endonymLangs);
  for (const fetch of wikidata.fetches) {
    provenance.push(fetch.provenance);
  }

  let wikidataEndonymFills = 0;
  let wikidataLocalizedAdds = 0;
  for (const entry of entries) {
    const wEntry = wikidata.byCode.get(entry.iso31662Code);
    if (!wEntry) continue;
    // Merge Wikidata labels into localizedDisplayNames (Wikidata wins on conflict;
    // CLDR's near-empty non-English data is replaced with Wikidata's comprehensive set).
    for (const [lang, label] of Object.entries(wEntry.labelsByLang)) {
      if (entry.localizedDisplayNames[lang] !== label) {
        if (!entry.localizedDisplayNames[lang]) wikidataLocalizedAdds++;
        entry.localizedDisplayNames[lang] = label;
      }
    }
    // Re-derive endonymDisplayName from Wikidata for the country's primary language
    const primaryLang = countryPrimaryLanguageMap.get(entry.countryISO31661Alpha2Code);
    if (primaryLang) {
      const wikidataEndonym = wEntry.labelsByLang[primaryLang];
      if (wikidataEndonym && wikidataEndonym !== entry.endonymDisplayName) {
        if (!entry.endonymDisplayName) wikidataEndonymFills++;
        entry.endonymDisplayName = wikidataEndonym;
      }
    }
  }
  console.error(
    `  [wikidata] subdivision endonym fills: ${wikidataEndonymFills}; ` +
      `localized name adds: ${wikidataLocalizedAdds}`,
  );

  entries.sort((a, b) => a.iso31662Code.localeCompare(b.iso31662Code));

  return {
    entries,
    countryOrderReports,
    debianOnly,
    cldrOnly,
    provenance,
    wikidataEndonymFills,
    wikidataLocalizedAdds,
  };
}
