// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { resolve } from "node:path";
import { loadSrcData, type LoadedSrcData } from "./load-src-data.js";
import type {
  CatalogFileWrapper,
  CountrySpec,
  CurrencySpec,
  DateFormatPattern,
  DayOfWeek,
  LanguageSpec,
  LocaleSpec,
  MeasurementSystem,
  SubdivisionSpec,
  TimezoneSpec,
  WritingDirection,
} from "./types.js";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

const GEO_DIR = resolve(REPO_ROOT_PATH, "contracts", "geo");
const CATALOG_VERSION = "0.1.0";

const DAY_OF_WEEK_VALUES: readonly DayOfWeek[] = [
  "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
];
const MEASUREMENT_VALUES: readonly MeasurementSystem[] = ["Metric", "Imperial", "Mixed"];
const WRITING_DIRECTION_VALUES: readonly WritingDirection[] = ["LTR", "RTL"];
const DATE_FORMAT_VALUES: readonly DateFormatPattern[] = ["DMY", "MDY", "YMD"];

/** Default region-derived facts when src-data has no value (matches CLDR `001` world defaults). */
const DEFAULT_FIRST_DAY: DayOfWeek = "Monday";
const DEFAULT_WEEKEND_START: DayOfWeek = "Saturday";
const DEFAULT_WEEKEND_END: DayOfWeek = "Sunday";
const DEFAULT_MEASUREMENT: MeasurementSystem = "Metric";

export interface BuildResult {
  countries: CountrySpec[];
  subdivisions: SubdivisionSpec[];
  currencies: CurrencySpec[];
  languages: LanguageSpec[];
  locales: LocaleSpec[];
  timezones: TimezoneSpec[];
}

/**
 * Builds all 6 Tier 2 codegen-ready catalogs from loaded Tier 1 src-data + the
 * hand-rolled Tier 2 GE catalog.
 */
export function buildAll(src: LoadedSrcData): BuildResult {
  // --- Pass 1: derive selectable / supported sets (drives IsSelectable + IsSupported flags) ---
  const supportedLanguages = new Set<string>();
  const supportedCurrencies = new Set<string>();
  for (const tag of src.selectableLocaleTags) {
    const locale = src.localesByTag.get(tag);
    if (!locale) continue;
    supportedLanguages.add(locale.languageSubtag);
    if (locale.regionSubtag) {
      const country = src.countriesByCode.get(locale.regionSubtag);
      if (country?.primaryCurrencyISO4217AlphaCode) {
        supportedCurrencies.add(country.primaryCurrencyISO4217AlphaCode);
      }
    }
  }

  // --- Pass 2: build per-catalog inverse-nav lookups ---
  // Subdivisions per country
  const subdivisionCodesByCountry = new Map<string, string[]>();
  for (const s of src.subdivisions) {
    const list = subdivisionCodesByCountry.get(s.countryISO31661Alpha2Code) ?? [];
    list.push(s.iso31662Code);
    subdivisionCodesByCountry.set(s.countryISO31661Alpha2Code, list);
  }
  for (const list of subdivisionCodesByCountry.values()) list.sort();

  // Timezones per country (primary + co-applicable)
  const timezoneIdsByCountry = new Map<string, string[]>();
  for (const t of src.timezones) {
    if (t.countryISO31661Alpha2Code) {
      addToBag(timezoneIdsByCountry, t.countryISO31661Alpha2Code, t.ianaIdentifier);
    }
    for (const co of t.coApplicableCountryISO31661Alpha2Codes) {
      addToBag(timezoneIdsByCountry, co, t.ianaIdentifier);
    }
  }
  for (const list of timezoneIdsByCountry.values()) list.sort();

  // Locales per country (where a Locale resolved its region to a real ISO 3166-1 country code)
  const localeTagsByCountry = new Map<string, string[]>();
  for (const l of src.locales) {
    if (!l.regionSubtag) continue;
    if (!src.countriesByCode.has(l.regionSubtag)) continue;
    addToBag(localeTagsByCountry, l.regionSubtag, l.ietfBcp47Tag);
  }
  for (const list of localeTagsByCountry.values()) list.sort();

  // GeopoliticalEntity codes per country (inverse of the manual M:M)
  const geCodesByCountry = new Map<string, string[]>();
  for (const ge of src.geopoliticalEntities) {
    for (const country of ge.countryISO31661Alpha2Codes) {
      addToBag(geCodesByCountry, country, ge.shortCode);
    }
  }
  for (const list of geCodesByCountry.values()) list.sort();

  // Languages per country (from Country.spokenLanguages + Country.primaryLanguageISO6391Code,
  // restricted to ISO 639-1 two-letter codes since the spec field is "ISO6391Code")
  const spokenLangsByCountry = new Map<string, string[]>();
  for (const c of src.countries) {
    const set = new Set<string>();
    if (c.primaryLanguageISO6391Code) set.add(c.primaryLanguageISO6391Code);
    for (const s of c.spokenLanguages) {
      if (/^[a-z]{2}$/.test(s.languageCode)) set.add(s.languageCode);
    }
    spokenLangsByCountry.set(c.iso31661Alpha2Code, [...set].sort());
  }

  // Country codes per language (inverse of the above)
  const countriesPerLanguage = new Map<string, string[]>();
  for (const [country, langs] of spokenLangsByCountry.entries()) {
    for (const lang of langs) {
      addToBag(countriesPerLanguage, lang, country);
    }
  }
  for (const list of countriesPerLanguage.values()) list.sort();

  // --- Pass 3: per-entity build ---

  const countries: CountrySpec[] = src.countries.map((c) => buildCountry(c, {
    geCodes: geCodesByCountry.get(c.iso31661Alpha2Code) ?? [],
    subdivisionCodes: subdivisionCodesByCountry.get(c.iso31661Alpha2Code) ?? [],
    timezoneIds: timezoneIdsByCountry.get(c.iso31661Alpha2Code) ?? [],
    localeTags: localeTagsByCountry.get(c.iso31661Alpha2Code) ?? [],
    spokenLangs: spokenLangsByCountry.get(c.iso31661Alpha2Code) ?? [],
  }));

  const subdivisions: SubdivisionSpec[] = src.subdivisions.map((s) => ({
    iso31662Code: s.iso31662Code,
    shortCode: s.shortCode,
    displayName: s.displayName,
    officialName: s.officialName,
    endonymDisplayName: s.endonymDisplayName,
    countryISO31661Alpha2Code: s.countryISO31661Alpha2Code,
    parentISO31662Code: s.parentISO31662Code,
    type: s.type,
    order: s.order,
  }));

  const currencies: CurrencySpec[] = src.currencies.map((c) => ({
    iso4217AlphaCode: c.iso4217AlphaCode,
    iso4217NumericCode: c.iso4217NumericCode,
    displayName: c.displayName,
    decimalPlaces: c.decimalPlaces,
    symbol: c.symbol,
    isActive: c.isActive,
    isSupported: supportedCurrencies.has(c.iso4217AlphaCode),
  }));

  const languages: LanguageSpec[] = src.languages.map((l) => ({
    iso6391Code: l.iso6391Code,
    name: l.displayName,
    endonym: l.endonymDisplayName,
    writingDirection: normalizeEnum<WritingDirection>(
      l.writingDirection,
      WRITING_DIRECTION_VALUES,
      "LTR",
    ),
    isSupported: supportedLanguages.has(l.iso6391Code),
    spokenInCountryISO31661Alpha2Codes: countriesPerLanguage.get(l.iso6391Code) ?? [],
  }));

  const locales: LocaleSpec[] = src.locales.map((l) => {
    const country = l.regionSubtag ? src.countriesByCode.get(l.regionSubtag) ?? null : null;
    return {
      ietfBcp47Tag: l.ietfBcp47Tag,
      name: l.displayName,
      endonym: l.endonymDisplayName,
      languageISO6391Code: l.languageSubtag,
      countryISO31661Alpha2Code: country?.iso31661Alpha2Code ?? null,
      isSelectable: src.selectableLocaleTags.has(l.ietfBcp47Tag),
      firstDayOfWeek: normalizeEnum<DayOfWeek>(
        country?.firstDayOfWeek ?? null,
        DAY_OF_WEEK_VALUES,
        DEFAULT_FIRST_DAY,
      ),
      decimalSeparator: l.decimalSeparator,
      thousandsSeparator: l.thousandsSeparator,
      dateFormatPattern: normalizeEnum<DateFormatPattern>(
        l.dateFormatPattern,
        DATE_FORMAT_VALUES,
        "DMY",
      ),
    };
  });

  const timezones: TimezoneSpec[] = src.timezones.map((t) => ({
    ianaIdentifier: t.ianaIdentifier,
    displayName: t.displayName ?? t.ianaIdentifier,
    currentStdOffsetMinutes: t.currentStdOffsetMinutes,
    currentDstOffsetMinutes: t.currentDstOffsetMinutes,
    currentStdAbbrev: t.currentStdAbbrev ?? "",
    currentDstAbbrev: t.currentDstAbbrev,
    countryISO31661Alpha2Code: t.countryISO31661Alpha2Code,
    coApplicableCountryISO31661Alpha2Codes: [...t.coApplicableCountryISO31661Alpha2Codes].sort(),
    aliases: [...t.aliases].sort(),
  }));

  return { countries, subdivisions, currencies, languages, locales, timezones };
}

interface CountryNavData {
  geCodes: string[];
  subdivisionCodes: string[];
  timezoneIds: string[];
  localeTags: string[];
  spokenLangs: string[];
}

function buildCountry(
  c: import("./load-src-data.js").SrcDataCountry,
  nav: CountryNavData,
): CountrySpec {
  return {
    iso31661Alpha2Code: c.iso31661Alpha2Code,
    iso31661Alpha3Code: c.iso31661Alpha3Code ?? "",
    iso31661NumericCode: c.iso31661NumericCode ?? "",
    displayName: c.displayName,
    officialName: c.officialName,
    endonymDisplayName: c.endonymDisplayName,
    phoneNumberPrefix: c.phoneNumberPrefix,
    phoneNumberNationalFormat: c.phoneNumberNationalFormat,
    phoneNumberMinDigits: c.phoneNumberMinDigits,
    phoneNumberMaxDigits: c.phoneNumberMaxDigits,
    firstDayOfWeek: normalizeEnum<DayOfWeek>(
      c.firstDayOfWeek,
      DAY_OF_WEEK_VALUES,
      DEFAULT_FIRST_DAY,
    ),
    weekendStart: normalizeEnum<DayOfWeek>(
      c.weekendStart,
      DAY_OF_WEEK_VALUES,
      DEFAULT_WEEKEND_START,
    ),
    weekendEnd: normalizeEnum<DayOfWeek>(
      c.weekendEnd,
      DAY_OF_WEEK_VALUES,
      DEFAULT_WEEKEND_END,
    ),
    measurementSystem: normalizeEnum<MeasurementSystem>(
      c.measurementSystem,
      MEASUREMENT_VALUES,
      DEFAULT_MEASUREMENT,
    ),
    primaryLanguageISO6391Code: c.primaryLanguageISO6391Code,
    primaryCurrencyISO4217AlphaCode: c.primaryCurrencyISO4217AlphaCode,
    primaryLocaleIETFBCP47Tag: c.primaryLocaleIETFBCP47Tag,
    sovereignCountryISO31661Alpha2Code: c.sovereignCountryISO31661Alpha2Code,
    geopoliticalEntityShortCodes: nav.geCodes,
    subdivisionISO31662Codes: nav.subdivisionCodes,
    timezoneIanaIdentifiers: nav.timezoneIds,
    localeIETFBCP47Tags: nav.localeTags,
    spokenLanguageISO6391Codes: nav.spokenLangs,
    territoryISO31661Alpha2Codes: c.territoryISO31661Alpha2Codes,
    currencies: c.activeLegalTenderCurrencies.map((cur) => ({
      iso4217AlphaCode: cur.isoAlphaCode,
      level: "LegalTender" as const,
    })),
  };
}

function normalizeEnum<T extends string>(
  value: string | null,
  allowed: readonly T[],
  fallback: T,
): T {
  if (value && (allowed as readonly string[]).includes(value)) return value as T;
  return fallback;
}

function addToBag<K, V>(bag: Map<K, V[]>, key: K, value: V): void {
  const list = bag.get(key);
  if (list) list.push(value);
  else bag.set(key, [value]);
}

/** Wraps each catalog with the standard header and writes to contracts/geo/{name}.spec.json. */
export async function writeAll(result: BuildResult, src: LoadedSrcData): Promise<void> {
  const generatedAt = new Date().toISOString();
  const baseNote = [
    "TIER 2 codegen-ready spec — produced by tools/geo-data-pipeline from",
    "contracts/geo/src-data/*.spec.json (Tier 1). Matches the canonical entity",
    "record shape; consumed directly by codegen (.NET SourceGen + TS emitter — Tier 3)",
    "to produce concrete entity types. Do NOT hand-edit — re-run `pnpm geo:refresh`",
    "to regenerate.",
  ].join(" ");

  const supportedCurrencyCount = result.currencies.filter(
    (c) => (c as { isSupported: boolean }).isSupported,
  ).length;
  const currenciesNote =
    `${baseNote} IsSupported derived from contracts/messages/*.json file presence — ` +
    `selectable locales' primary countries' primary currencies. Active set: ` +
    `${src.selectableLocaleTags.size} selectable locales -> derived ${supportedCurrencyCount} ` +
    `supported currencies.`;
  const languagesNote =
    `${baseNote} IsSupported derived from contracts/messages/*.json file presence — ` +
    `languages of selectable locales.`;
  const localesNote =
    `${baseNote} IsSelectable derived from contracts/messages/{tag}.json file presence ` +
    `(currently ${src.selectableLocaleTags.size} selectable). FirstDayOfWeek denormalized ` +
    `from Country[locale.country]; matches via parity test.`;

  const writes: Array<[string, CatalogFileWrapper<unknown>]> = [
    [
      "countries.spec.json",
      wrap("countries.schema.json", baseNote, result.countries, generatedAt),
    ],
    [
      "subdivisions.spec.json",
      wrap("subdivisions.schema.json", baseNote, result.subdivisions, generatedAt),
    ],
    [
      "currencies.spec.json",
      wrap("currencies.schema.json", currenciesNote, result.currencies, generatedAt),
    ],
    [
      "languages.spec.json",
      wrap("languages.schema.json", languagesNote, result.languages, generatedAt),
    ],
    [
      "locales.spec.json",
      wrap("locales.schema.json", localesNote, result.locales, generatedAt),
    ],
    [
      "timezones.spec.json",
      wrap("timezones.schema.json", baseNote, result.timezones, generatedAt),
    ],
  ];

  for (const [filename, payload] of writes) {
    await writeSpecJson(resolve(GEO_DIR, filename), payload);
    const entryCount = (payload as { entries: unknown[] }).entries.length;
    console.error(`[tier-2] write ${filename} (${entryCount} entries)`);
  }
}

function wrap<T>(
  schema: string,
  note: string,
  entries: T[],
  generatedAt: string,
): CatalogFileWrapper<T> {
  return {
    $generated: true,
    $source: "pipeline-derived",
    $schema: `./${schema}`,
    $note: note,
    catalogVersion: CATALOG_VERSION,
    generatedAt,
    entries,
  };
}

if (
  process.argv[1]?.endsWith("build-codegen-specs.ts") ||
  process.argv[1]?.endsWith("build-codegen-specs.js")
) {
  console.error("[tier-2] loading Tier 1 src-data + overlays + hand-rolled GE catalog");
  const src = await loadSrcData();
  console.error(
    `  countries=${src.countries.length} ` +
      `subdivisions=${src.subdivisions.length} ` +
      `currencies=${src.currencies.length} ` +
      `languages=${src.languages.length} ` +
      `locales=${src.locales.length} ` +
      `timezones=${src.timezones.length} ` +
      `ge=${src.geopoliticalEntities.length} ` +
      `selectableLocales=${src.selectableLocaleTags.size}`,
  );
  const c = src.overlaysApplied.countries;
  if (c.additions.length + c.overrides.length + c.removals.length > 0) {
    console.error(
      `  [overlays] countries: +${c.additions.length} additions, ` +
        `~${c.overrides.length} overrides, -${c.removals.length} removals ` +
        `(run \`pnpm geo:overlays\` for reasons)`,
    );
  }
  const result = buildAll(src);
  await writeAll(result, src);
  console.error("[tier-2] done");
}
