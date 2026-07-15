// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fetchCldrAvailableLocales } from "../fetchers/cldr-available-locales.js";
import { fetchCldrLikelySubtags } from "../fetchers/cldr-likely-subtags.js";
import { fetchCldrTerritories } from "../fetchers/cldr-territories.js";
import { fetchDatasetsCountryCodes } from "../fetchers/datasets-country-codes.js";
import { fetchLibphonenumberMetadata } from "../fetchers/libphonenumber-metadata.js";
import { fetchCountryEndonyms } from "../fetchers/wikidata-endonyms.js";
import { getEndonymLanguageList } from "../util/endonym-languages.js";
import {
  loadCountryEndonyms,
  pickEndonymForCountry,
  pickLocalizedName,
} from "../transformers/country-endonyms.js";
import {
  deriveActiveCurrencies,
  deriveMeasurementEnrichment,
  deriveWeekEnrichment,
  isCurrencyRetiredInCldr,
  loadCountryEnrichments,
  type ActiveCurrencyEntry,
  type CountryMeasurementEnrichment,
  type CountryWeekEnrichment,
} from "../transformers/country-enrichments.js";
import {
  loadCountrySpokenLanguages,
  type CountrySpokenLanguageEntry,
} from "../transformers/country-spoken-languages.js";
import {
  transformPhoneMetadata,
  type CountryPhoneMetadata,
} from "../transformers/country-phone-metadata.js";
import {
  transformCountryRow,
  type CountryPartialFromDatasets,
} from "../transformers/countries.js";
import {
  computeLocaleCatalogTags,
  indexLocaleTagsByRegion,
} from "../transformers/locale-catalog-tags.js";
import { derivePrimaryLocaleTag } from "../transformers/primary-locale-tag.js";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

const SPEC_OUTPUT_PATH = resolve(
  REPO_ROOT_PATH,
  "contracts",
  "geo",
  "src-data",
  "countries.spec.json",
);

/** The 11 locales the platform ships UI translations for. */
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

interface CountryMerged extends CountryPartialFromDatasets {
  /** Native-language display name. */
  endonymDisplayName: string | null;
  /**
   * Localized names across the 11 supported languages
   * (parallel to Timezone.LocalizedDisplayNames).
   */
  localizedDisplayNames: Record<string, string>;
  /** CLDR weekData enrichment. Null on lookup failure (default region fallback used). */
  firstDayOfWeek: CountryWeekEnrichment["firstDayOfWeek"] | null;
  weekendStart: CountryWeekEnrichment["weekendStart"] | null;
  weekendEnd: CountryWeekEnrichment["weekendEnd"] | null;
  /** CLDR measurementData enrichment. Defaults to Metric (world default) when no override. */
  measurementSystem: CountryMeasurementEnrichment["measurementSystem"] | null;
  /**
   * Active legal-tender currencies from CLDR currencyData.json
   * (de-facto overlay applied separately in Tier 2).
   */
  activeLegalTenderCurrencies: ActiveCurrencyEntry[];
  /**
   * Derived via `derivePrimaryLocaleTag` (see transformers/primary-locale-tag.ts)
   * — guaranteed to exist in the locales catalog (CLDR availableLocales OR the
   * country's own per-region locale list). Algorithm walks CLDR likelySubtags
   * for script-subtag canonical expansion (`zh-HK` -> `zh-Hant-HK`,
   * `sr-ME` -> `sr-Latn-ME`), applies a small lang-alias table (`tl` -> `fil`,
   * `no` -> `nb`, `cmn` -> `zh`), then falls back to `en-{region}` then the
   * first entry in the region's locale list when the primary language is
   * 639-3 outside our 639-1 enum. Returns null only when no derivation
   * succeeds; pipeline-time validateLocaleRefs in Tier 2 surfaces nulls /
   * orphans as hard errors.
   */
  primaryLocaleIETFBCP47Tag: string | null;
  /**
   * Inverse nav of SovereignCountry — populated for sovereign countries that have
   * dependent territories.
   */
  territoryISO31661Alpha2Codes: string[];
  /**
   * Phone format + length constraints from libphonenumber. Overrides datasets/country-codes
   * Dial (libphonenumber is authoritative).
   */
  phoneNumberNationalFormat: CountryPhoneMetadata["phoneNumberNationalFormat"];
  phoneNumberMinDigits: CountryPhoneMetadata["phoneNumberMinDigits"];
  phoneNumberMaxDigits: CountryPhoneMetadata["phoneNumberMaxDigits"];
  phoneNumberInternationalPrefix: CountryPhoneMetadata["internationalPrefix"];
  phoneNumberNationalPrefix: CountryPhoneMetadata["nationalPrefix"];
  /**
   * Spoken languages per CLDR territoryInfo.languagePopulation, sorted by population percent
   * descending. CLDR uses ISO 639-1 (`en`, `fr`, `de`) where available, falls back to ISO 639-3
   * (`gsw` Swiss German, `pcm` Nigerian Pidgin) for less-common langs. Carries per-language
   * population percent + official status flag (`official` / `de_facto_official` /
   * `official_regional` / `official_minority` / null). Forward source for Tier 2
   * Country.spokenLanguageISO6391Codes M:M.
   */
  spokenLanguages: CountrySpokenLanguageEntry[];
}

interface CountriesSpec {
  $schema: string;
  $note: string;
  catalogVersion: string;
  generatedAt: string;
  sources: Array<{
    name: string;
    url: string;
    license: string;
    fetchedAt: string;
    sha256: string;
  }>;
  fieldCoverage: Record<
    string,
    { populated: number; total: number; pct: string }
  >;
  entries: CountryMerged[];
}

export async function buildCountriesSpec(): Promise<CountriesSpec> {
  const fetched = await fetchDatasetsCountryCodes();

  // Layer A.1 — datasets/country-codes (CSV)
  const partials: CountryPartialFromDatasets[] = [];
  let skipped = 0;
  for (const row of fetched.rows) {
    const partial = transformCountryRow(row);
    if (partial) partials.push(partial);
    else skipped++;
  }

  // Layer A.2 — CLDR territories for all 11 supported languages
  console.error(`[fetch] CLDR territories for 11 supported languages`);
  const endonyms = await loadCountryEndonyms(SUPPORTED_LANGUAGE_CODES);
  const sources = [
    {
      name: fetched.provenance.source,
      url: fetched.provenance.url,
      license: fetched.provenance.license,
      fetchedAt: fetched.provenance.fetchedAt,
      sha256: fetched.provenance.sha256,
    },
  ];
  for (const lang of SUPPORTED_LANGUAGE_CODES) {
    const r = await fetchCldrTerritories(lang);
    sources.push({
      name: `${r.provenance.source}/${lang}`,
      url: r.provenance.url,
      license: r.provenance.license,
      fetchedAt: r.provenance.fetchedAt,
      sha256: r.provenance.sha256,
    });
  }

  // Layer A.3 — CLDR supplemental enrichments (weekData + measurementData + currencyData)
  console.error(
    `[fetch] CLDR supplemental (weekData + measurementData + currencyData)`,
  );
  const enrichments = await loadCountryEnrichments();
  for (const prov of enrichments.provenance) {
    sources.push({
      name: prov.source,
      url: prov.url,
      license: prov.license,
      fetchedAt: prov.fetchedAt,
      sha256: prov.sha256,
    });
  }

  // Layer A.4 — libphonenumber metadata (phone format + length constraints)
  console.error(`[fetch] libphonenumber metadata (XML)`);
  const phone = await fetchLibphonenumberMetadata();
  sources.push({
    name: phone.provenance.source,
    url: phone.provenance.url,
    license: phone.provenance.license,
    fetchedAt: phone.provenance.fetchedAt,
    sha256: phone.provenance.sha256,
  });

  // Layer A.4b — CLDR territoryInfo.json (spoken languages per country)
  console.error(`[fetch] CLDR territoryInfo (spoken languages per country)`);
  const spokenLanguages = await loadCountrySpokenLanguages();
  sources.push({
    name: spokenLanguages.provenance.source,
    url: spokenLanguages.provenance.url,
    license: spokenLanguages.provenance.license,
    fetchedAt: spokenLanguages.provenance.fetchedAt,
    sha256: spokenLanguages.provenance.sha256,
  });

  // Layer A.4c — CLDR availableLocales + likelySubtags. Feeds the principled
  // `derivePrimaryLocaleTag` algorithm (see transformers/primary-locale-tag.ts)
  // which replaces the naive `${lang}-${region}` concatenation that previously
  // produced 19 orphan refs to locales the catalog does not ship. Loaded here
  // so the derivation is pure (no I/O) and the function is unit-testable.
  console.error(
    `[fetch] CLDR availableLocales + likelySubtags (primary-locale derivation)`,
  );
  const cldrAvailableLocales = await fetchCldrAvailableLocales();
  sources.push({
    name: cldrAvailableLocales.provenance.source,
    url: cldrAvailableLocales.provenance.url,
    license: cldrAvailableLocales.provenance.license,
    fetchedAt: cldrAvailableLocales.provenance.fetchedAt,
    sha256: cldrAvailableLocales.provenance.sha256,
  });
  const cldrLikelySubtags = await fetchCldrLikelySubtags();
  sources.push({
    name: cldrLikelySubtags.provenance.source,
    url: cldrLikelySubtags.provenance.url,
    license: cldrLikelySubtags.provenance.license,
    fetchedAt: cldrLikelySubtags.provenance.fetchedAt,
    sha256: cldrLikelySubtags.provenance.sha256,
  });
  // Compute the canonical locale-catalog tags = CLDR availableLocales.full PLUS
  // derived lang-Region tags via likelySubtags (bare `en` -> `en-US`, lang-Script
  // `zh-Hant` -> `zh-Hant-TW`). Mirrors `write-locales.ts` derivation so the
  // primary-locale algorithm sees the SAME tag set the locales catalog will
  // eventually ship. Extracted to `locale-catalog-tags.ts` to keep the two
  // writers' views aligned.
  const cldrAvailableLocaleTags = computeLocaleCatalogTags({
    cldrAvailableLocaleFullTags: cldrAvailableLocales.fullTags,
    cldrLikelySubtags: cldrLikelySubtags.bySourceTag,
  });
  const candidateLocalesByRegion = indexLocaleTagsByRegion(
    cldrAvailableLocaleTags,
  );

  // Merge pass — combine partials + endonyms + enrichments into the final entry shape.
  // `displayName` is sourced from CLDR English territories.json (authoritative), NOT from
  // datasets/country-codes' `CLDR display name` column which contains abbreviations
  // ("US" / "UK") for some entries. The CSV column is misnamed/anomalous; CLDR upstream
  // is the canonical source.
  const merged: CountryMerged[] = partials.map((p) => {
    const localizedDisplayNames: Record<string, string> = {};
    for (const lang of SUPPORTED_LANGUAGE_CODES) {
      const name = pickLocalizedName(endonyms, p.iso31661Alpha2Code, lang);
      if (name) localizedDisplayNames[lang] = name;
    }
    const week = deriveWeekEnrichment(
      enrichments.weekData,
      p.iso31661Alpha2Code,
    );
    const meas = deriveMeasurementEnrichment(
      enrichments.measurementData,
      p.iso31661Alpha2Code,
    );
    const activeCurrencies = deriveActiveCurrencies(
      enrichments.currencyData,
      p.iso31661Alpha2Code,
    );
    // Authoritative displayName: CLDR English territories.json. Fall back to CSV's
    // partial if CLDR has no entry (extremely rare; CLDR covers all ISO 3166-1).
    const cldrEnglishName = pickLocalizedName(
      endonyms,
      p.iso31661Alpha2Code,
      "en",
    );
    const authoritativeDisplayName = cldrEnglishName ?? p.displayName;
    return {
      ...p,
      displayName: authoritativeDisplayName,
      endonymDisplayName: pickEndonymForCountry(
        endonyms,
        p.iso31661Alpha2Code,
        p.primaryLanguageISO6391Code,
      ),
      localizedDisplayNames,
      firstDayOfWeek: week?.firstDayOfWeek ?? null,
      weekendStart: week?.weekendStart ?? null,
      weekendEnd: week?.weekendEnd ?? null,
      measurementSystem: meas?.measurementSystem ?? null,
      activeLegalTenderCurrencies: activeCurrencies,
      primaryLocaleIETFBCP47Tag: derivePrimaryLocaleTag(
        {
          regionAlpha2: p.iso31661Alpha2Code,
          primaryLanguageCode: p.primaryLanguageISO6391Code,
          candidateLocaleTags:
            candidateLocalesByRegion.get(p.iso31661Alpha2Code) ?? [],
        },
        {
          cldrAvailableLocaleTags,
          cldrLikelySubtags: cldrLikelySubtags.bySourceTag,
        },
      ),
      territoryISO31661Alpha2Codes: [], // back-filled below from inverse-nav pass
      phoneNumberNationalFormat: null,
      phoneNumberMinDigits: null,
      phoneNumberMaxDigits: null,
      phoneNumberInternationalPrefix: null,
      phoneNumberNationalPrefix: null,
      spokenLanguages:
        spokenLanguages.byCountry.get(p.iso31661Alpha2Code) ?? [],
    };
  });

  // Layer A.4 merge pass: enrich each merged entry with libphonenumber fields
  for (const m of merged) {
    const territory = phone.territoriesById.get(m.iso31661Alpha2Code);
    if (!territory) continue;
    const meta = transformPhoneMetadata(territory);
    if (!meta) continue;
    m.phoneNumberNationalFormat = meta.phoneNumberNationalFormat;
    m.phoneNumberMinDigits = meta.phoneNumberMinDigits;
    m.phoneNumberMaxDigits = meta.phoneNumberMaxDigits;
    m.phoneNumberInternationalPrefix = meta.internationalPrefix;
    m.phoneNumberNationalPrefix = meta.nationalPrefix;
    // Override datasets/country-codes Dial when libphonenumber disagrees
    // (libphonenumber is authoritative)
    if (
      meta.phoneNumberPrefix &&
      m.phoneNumberPrefix !== meta.phoneNumberPrefix
    ) {
      console.error(
        `  [phone] override ${m.iso31661Alpha2Code}: ` +
          `datasets="${m.phoneNumberPrefix}" -> ` +
          `libphonenumber="${meta.phoneNumberPrefix}"`,
      );
      m.phoneNumberPrefix = meta.phoneNumberPrefix;
    }
  }

  // Layer A.5 — Wikidata SPARQL country endonyms (CC0). Strictly broader coverage than
  // CLDR's 11-locale loading; pulls labels in ~95 languages so any added supported locale
  // has the endonym data already in the spec. Wikidata values OVERWRITE CLDR-derived
  // localizedDisplayNames for the same lang code (Wikidata is more comprehensive +
  // community-maintained at the same authoritative tier; both are Unicode/CC0-licensed).
  const endonymLangs = await getEndonymLanguageList();
  console.error(
    `[fetch] Wikidata country endonyms (${endonymLangs.length} languages)`,
  );
  const wikidataCountries = await fetchCountryEndonyms(endonymLangs);
  for (const fetch of wikidataCountries.fetches) {
    sources.push({
      name: `${fetch.provenance.source}/countries-${fetch.provenance.sha256.substring(0, 8)}`,
      url: fetch.provenance.url,
      license: fetch.provenance.license,
      fetchedAt: fetch.provenance.fetchedAt,
      sha256: fetch.provenance.sha256,
    });
  }

  let wikidataEndonymFills = 0;
  let wikidataLocalizedAdds = 0;
  for (const m of merged) {
    const wEntry = wikidataCountries.byCode.get(m.iso31661Alpha2Code);
    if (!wEntry) continue;
    // Merge Wikidata labels into localizedDisplayNames (Wikidata wins on conflict)
    for (const [lang, label] of Object.entries(wEntry.labelsByLang)) {
      if (m.localizedDisplayNames[lang] !== label) {
        if (m.localizedDisplayNames[lang]) {
          // Existing CLDR value differed from Wikidata; Wikidata authoritative
        } else {
          wikidataLocalizedAdds++;
        }
        m.localizedDisplayNames[lang] = label;
      }
    }
    // Re-derive endonymDisplayName from Wikidata (broader lang coverage than 11-CLDR)
    if (m.primaryLanguageISO6391Code) {
      const wikidataEndonym = wEntry.labelsByLang[m.primaryLanguageISO6391Code];
      if (wikidataEndonym && wikidataEndonym !== m.endonymDisplayName) {
        if (!m.endonymDisplayName) wikidataEndonymFills++;
        m.endonymDisplayName = wikidataEndonym;
      }
    }
  }
  console.error(
    `  [wikidata] country endonym fills: ${wikidataEndonymFills}; ` +
      `localized name adds: ${wikidataLocalizedAdds}`,
  );

  // Primary-currency reconciliation pass:
  //   - datasets/country-codes is the authoritative source for "primary currency" (UN-derived,
  //     curated per-country)
  //   - CLDR currencyData fills gaps where datasets has null (e.g., Türkiye post-2022 rename)
  //   - When datasets has a HISTORICAL/retired code (e.g., SV="SVC" retired 2001), use CLDR's
  //     first active entry instead (CLDR's native array order = primary-first semantics)
  //   - Otherwise keep datasets — even when CLDR's first-active disagrees (LS/NA cases where
  //     a CMA secondary currency appears in CLDR alongside the official primary)
  for (const m of merged) {
    const cldrFirstActive =
      m.activeLegalTenderCurrencies[0]?.isoAlphaCode ?? null;
    const datasetsValue = m.primaryCurrencyISO4217AlphaCode;

    if (!datasetsValue && cldrFirstActive) {
      console.error(
        `  [currency] fill ${m.iso31661Alpha2Code}: ` +
          `datasets=null -> CLDR-active="${cldrFirstActive}"`,
      );
      m.primaryCurrencyISO4217AlphaCode = cldrFirstActive;
    } else if (
      datasetsValue &&
      isCurrencyRetiredInCldr(
        enrichments.currencyData,
        m.iso31661Alpha2Code,
        datasetsValue,
      )
    ) {
      if (cldrFirstActive) {
        console.error(
          `  [currency] override ${m.iso31661Alpha2Code}: ` +
            `datasets="${datasetsValue}" is RETIRED in CLDR -> ` +
            `CLDR-active="${cldrFirstActive}"`,
        );
        m.primaryCurrencyISO4217AlphaCode = cldrFirstActive;
      } else {
        console.error(
          `  [currency] WARN ${m.iso31661Alpha2Code}: ` +
            `datasets="${datasetsValue}" retired but no CLDR active fallback`,
        );
      }
    }
    // else: keep datasets (active in CLDR, or not in CLDR at all = newer than CLDR snapshot)
  }

  // Inverse-nav pass: populate Country.Territories from sovereign FKs
  const territoriesBySovereign = new Map<string, string[]>();
  for (const m of merged) {
    if (m.sovereignCountryISO31661Alpha2Code) {
      const list =
        territoriesBySovereign.get(m.sovereignCountryISO31661Alpha2Code) ?? [];
      list.push(m.iso31661Alpha2Code);
      territoriesBySovereign.set(m.sovereignCountryISO31661Alpha2Code, list);
    }
  }
  for (const m of merged) {
    const territories = territoriesBySovereign.get(m.iso31661Alpha2Code);
    if (territories) {
      territories.sort();
      m.territoryISO31661Alpha2Codes = territories;
    }
  }

  merged.sort((a, b) =>
    a.iso31661Alpha2Code.localeCompare(b.iso31661Alpha2Code),
  );

  // Field coverage report
  const total = merged.length;
  const coverage = {
    iso31661Alpha2Code: countNonNull(merged, (m) => m.iso31661Alpha2Code),
    iso31661Alpha3Code: countNonNull(merged, (m) => m.iso31661Alpha3Code),
    iso31661NumericCode: countNonNull(merged, (m) => m.iso31661NumericCode),
    displayName: countNonNull(merged, (m) => m.displayName),
    officialName: countNonNull(merged, (m) => m.officialName),
    sovereignCountry: countNonNull(
      merged,
      (m) => m.sovereignCountryISO31661Alpha2Code,
    ),
    phoneNumberPrefix: countNonNull(merged, (m) => m.phoneNumberPrefix),
    primaryCurrency: countNonNull(
      merged,
      (m) => m.primaryCurrencyISO4217AlphaCode,
    ),
    primaryLanguage: countNonNull(merged, (m) => m.primaryLanguageISO6391Code),
    endonymDisplayName: countNonNull(merged, (m) => m.endonymDisplayName),
    localizedDisplayNames_en: countNonNull(
      merged,
      (m) => m.localizedDisplayNames["en"] ?? null,
    ),
    localizedDisplayNames_ja: countNonNull(
      merged,
      (m) => m.localizedDisplayNames["ja"] ?? null,
    ),
    localizedDisplayNames_zh: countNonNull(
      merged,
      (m) => m.localizedDisplayNames["zh"] ?? null,
    ),
    firstDayOfWeek: countNonNull(merged, (m) => m.firstDayOfWeek),
    weekendStart: countNonNull(merged, (m) => m.weekendStart),
    weekendEnd: countNonNull(merged, (m) => m.weekendEnd),
    measurementSystem: countNonNull(merged, (m) => m.measurementSystem),
    activeLegalTenderCurrencies: merged.filter(
      (m) => m.activeLegalTenderCurrencies.length > 0,
    ).length,
    primaryLocaleIETFBCP47Tag: countNonNull(
      merged,
      (m) => m.primaryLocaleIETFBCP47Tag,
    ),
    territoriesPopulated: merged.filter(
      (m) => m.territoryISO31661Alpha2Codes.length > 0,
    ).length,
    phoneNumberNationalFormat: countNonNull(
      merged,
      (m) => m.phoneNumberNationalFormat,
    ),
    phoneNumberMinDigits: countNonNull(merged, (m) =>
      m.phoneNumberMinDigits === null ? null : String(m.phoneNumberMinDigits),
    ),
    phoneNumberMaxDigits: countNonNull(merged, (m) =>
      m.phoneNumberMaxDigits === null ? null : String(m.phoneNumberMaxDigits),
    ),
    spokenLanguages: merged.filter((m) => m.spokenLanguages.length > 0).length,
  };
  const fieldCoverage: Record<
    string,
    { populated: number; total: number; pct: string }
  > = {};
  for (const [field, populated] of Object.entries(coverage)) {
    fieldCoverage[field] = {
      populated,
      total,
      pct: `${((populated / total) * 100).toFixed(1)}%`,
    };
  }

  console.error(
    `[transform] countries: ${merged.length} entries (${skipped} rows skipped)`,
  );
  for (const [field, stats] of Object.entries(fieldCoverage)) {
    console.error(
      `  ${field}: ${stats.populated}/${stats.total} (${stats.pct})`,
    );
  }

  return {
    $schema: "./countries.schema.json",
    $note:
      "PIPELINE-RAW spec — produced by tools/geo-data-pipeline. Not directly consumed by " +
      "codegen / DcsvIo.D2.Geo.Default. A clean/transform pass to the sibling " +
      "contracts/geo/*.spec.json (one level up) is a separate step that strips per-entry " +
      "_provenance / build metadata + applies any final hand-curation. Sources: " +
      "datasets/country-codes (PDDL) + CLDR cldr-localenames-full (Unicode-3.0; 11 supported " +
      "langs for displayName authority) + CLDR cldr-core/supplemental (week/measurement/" +
      "currency data — Unicode-3.0) + libphonenumber (Apache-2.0) + Wikidata SPARQL (CC0; " +
      "endonyms across 95 languages). M:M nav fields for Currencies/Locales/Subdivisions/" +
      "Timezones/GeopoliticalEntities are derived in a separate cross-catalog merge pass " +
      "by the Tier 2 builder.",
    catalogVersion: "0.0.2",
    generatedAt: new Date().toISOString(),
    sources,
    fieldCoverage,
    entries: merged,
  };
}

function countNonNull<T>(
  items: readonly T[],
  pick: (item: T) => string | null,
): number {
  let n = 0;
  for (const item of items) if (pick(item) !== null) n++;
  return n;
}

if (
  process.argv[1]?.endsWith("write-countries.ts") ||
  process.argv[1]?.endsWith("write-countries.js")
) {
  const spec = await buildCountriesSpec();
  await mkdir(dirname(SPEC_OUTPUT_PATH), { recursive: true });
  await writeSpecJson(SPEC_OUTPUT_PATH, spec);
  console.error(`[write] ${SPEC_OUTPUT_PATH} (${spec.entries.length} entries)`);
}
