// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFile, readdir, stat } from "node:fs/promises";
import { resolve } from "node:path";
import { applyCountriesOverlay, loadCountriesOverlay } from "./load-overlays.js";
import type { OverlaysApplied } from "./load-overlays.js";
import type { GeopoliticalEntitySpec } from "./types.js";
import { REPO_ROOT_PATH } from "../util/cache.js";

const SRC_DATA_DIR = resolve(REPO_ROOT_PATH, "contracts", "geo", "src-data");
const GEO_DIR = resolve(REPO_ROOT_PATH, "contracts", "geo");
const MESSAGES_DIR = resolve(REPO_ROOT_PATH, "contracts", "messages");

/** Subset of Tier 1 src-data shapes — only the fields Tier 2 consumes (deliberately narrow). */

export interface SrcDataCountry {
  iso31661Alpha2Code: string;
  iso31661Alpha3Code: string | null;
  iso31661NumericCode: string | null;
  displayName: string;
  officialName: string;
  endonymDisplayName: string | null;
  sovereignCountryISO31661Alpha2Code: string | null;
  phoneNumberPrefix: string | null;
  phoneNumberNationalFormat: string | null;
  phoneNumberMinDigits: number | null;
  phoneNumberMaxDigits: number | null;
  primaryCurrencyISO4217AlphaCode: string | null;
  primaryLanguageISO6391Code: string | null;
  primaryLocaleIETFBCP47Tag: string | null;
  firstDayOfWeek: string | null;
  weekendStart: string | null;
  weekendEnd: string | null;
  measurementSystem: string | null;
  activeLegalTenderCurrencies: Array<{ isoAlphaCode: string; fromDate: string | null }>;
  territoryISO31661Alpha2Codes: string[];
  spokenLanguages: Array<{
    languageCode: string;
    populationPercent: number | null;
    officialStatus: string | null;
    writingPercent: number | null;
  }>;
}

export interface SrcDataSubdivision {
  iso31662Code: string;
  shortCode: string;
  displayName: string;
  officialName: string;
  endonymDisplayName: string | null;
  countryISO31661Alpha2Code: string;
  parentISO31662Code: string | null;
  type: string | null;
  order: number | null;
}

export interface SrcDataCurrency {
  iso4217AlphaCode: string;
  iso4217NumericCode: string | null;
  displayName: string;
  decimalPlaces: number;
  symbol: string | null;
  isActive: boolean;
}

export interface SrcDataLanguage {
  iso6391Code: string;
  displayName: string;
  endonymDisplayName: string | null;
  writingDirection: "LTR" | "RTL" | null;
}

export interface SrcDataLocale {
  ietfBcp47Tag: string;
  languageSubtag: string;
  regionSubtag: string | null;
  displayName: string;
  endonymDisplayName: string | null;
  decimalSeparator: string;
  thousandsSeparator: string;
  dateFormatPattern: "DMY" | "MDY" | "YMD";
}

export interface SrcDataTimezone {
  ianaIdentifier: string;
  displayName: string | null;
  currentStdOffsetMinutes: number;
  currentDstOffsetMinutes: number | null;
  currentStdAbbrev: string | null;
  currentDstAbbrev: string | null;
  countryISO31661Alpha2Code: string | null;
  coApplicableCountryISO31661Alpha2Codes: string[];
  aliases: string[];
}

/** All loaded src-data + the hand-rolled GE catalog, indexed for cross-catalog lookups. */
export interface LoadedSrcData {
  countries: SrcDataCountry[];
  countriesByCode: Map<string, SrcDataCountry>;
  subdivisions: SrcDataSubdivision[];
  currencies: SrcDataCurrency[];
  currenciesByCode: Map<string, SrcDataCurrency>;
  languages: SrcDataLanguage[];
  languagesByCode: Map<string, SrcDataLanguage>;
  locales: SrcDataLocale[];
  localesByTag: Map<string, SrcDataLocale>;
  timezones: SrcDataTimezone[];
  geopoliticalEntities: GeopoliticalEntitySpec[];
  /**
   * BCP 47 tags for which a `contracts/messages/{tag}.json` file exists. Empty when the
   * directory is absent.
   */
  selectableLocaleTags: Set<string>;
  /**
   * Diagnostic record of every overlay entry the Tier 2 build applied — surfaces via
   * the `pnpm geo:overlays` CLI for audit visibility. Empty arrays when no overlay
   * files are present.
   */
  overlaysApplied: OverlaysApplied;
}

export async function loadSrcData(): Promise<LoadedSrcData> {
  const [
    tier1Countries,
    subdivisions,
    currencies,
    languages,
    locales,
    timezones,
    geopoliticalEntities,
    selectableLocaleTags,
    countriesOverlay,
  ] = await Promise.all([
    readSpecEntries<SrcDataCountry>("countries.spec.json"),
    readSpecEntries<SrcDataSubdivision>("subdivisions.spec.json"),
    readSpecEntries<SrcDataCurrency>("currencies.spec.json"),
    readSpecEntries<SrcDataLanguage>("languages.spec.json"),
    readSpecEntries<SrcDataLocale>("locales.spec.json"),
    readSpecEntries<SrcDataTimezone>("timezones.spec.json"),
    readManualGeopoliticalEntities(),
    discoverSelectableLocales(),
    loadCountriesOverlay(),
  ]);

  // Apply countries overlay (additions / overrides / removals) before cross-catalog merge.
  // See contracts/geo/overlays/README.md for the pattern.
  const { countries, applied: countriesApplied } = applyCountriesOverlay(
    tier1Countries,
    countriesOverlay,
  );

  return {
    countries,
    countriesByCode: indexBy(countries, (c) => c.iso31661Alpha2Code),
    subdivisions,
    currencies,
    currenciesByCode: indexBy(currencies, (c) => c.iso4217AlphaCode),
    languages,
    languagesByCode: indexBy(languages, (l) => l.iso6391Code),
    locales,
    localesByTag: indexBy(locales, (l) => l.ietfBcp47Tag),
    timezones,
    geopoliticalEntities,
    selectableLocaleTags,
    overlaysApplied: { countries: countriesApplied },
  };
}

async function readSpecEntries<T>(filename: string): Promise<T[]> {
  const text = await readFile(resolve(SRC_DATA_DIR, filename), "utf8");
  const parsed = JSON.parse(text) as { entries: T[] };
  return parsed.entries;
}

async function readManualGeopoliticalEntities(): Promise<GeopoliticalEntitySpec[]> {
  const text = await readFile(resolve(GEO_DIR, "geopolitical-entities.spec.json"), "utf8");
  const parsed = JSON.parse(text) as { entries: GeopoliticalEntitySpec[] };
  return parsed.entries;
}

async function discoverSelectableLocales(): Promise<Set<string>> {
  const exists = await stat(MESSAGES_DIR).catch(() => null);
  if (!exists?.isDirectory()) return new Set();
  const files = await readdir(MESSAGES_DIR);
  const tags = new Set<string>();
  for (const f of files) {
    if (!f.endsWith(".json")) continue;
    const tag = f.slice(0, -".json".length);
    // Sanity check — only accept BCP 47-looking tags (lang or lang-REGION)
    if (/^[a-z]{2,3}(-[A-Z][a-z]{3})?(-[A-Z]{2}|-\d{3})?$/.test(tag)) {
      tags.add(tag);
    }
  }
  return tags;
}

function indexBy<T, K>(items: readonly T[], key: (item: T) => K): Map<K, T> {
  const map = new Map<K, T>();
  for (const item of items) map.set(key(item), item);
  return map;
}
