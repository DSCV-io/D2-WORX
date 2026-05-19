// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { REPO_ROOT_PATH } from "./cache.js";

const COUNTRIES_SPEC_PATH = resolve(
  REPO_ROOT_PATH,
  "contracts",
  "geo",
  "src-data",
  "countries.spec.json",
);

/** The 11 supported languages the platform ships UI translations for. */
export const SUPPORTED_LANGUAGE_CODES = [
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

/**
 * Additional major-population languages we want endonyms for even if no country in our
 * 249-country catalog has them as a primary language. Some of these ARE primary langs
 * (and would already be in the 88-derived set); listed explicitly so future hand-edits
 * to the country catalog don't accidentally drop them.
 */
const MAJOR_POPULATION_LANGUAGES = ["hi", "ar", "ru", "tr", "vi", "th", "id", "uk", "fa", "he"];

/**
 * Returns the union of:
 *   - The 11 supported languages
 *   - Primary languages of every country in countries.spec.json
 *   - The explicit major-population list above
 *
 * Used by the Wikidata fetchers to know which languages to pull labels for. The total is
 * typically ~95 languages. Caller passes the result directly to fetchCountryEndonyms or
 * fetchSubdivisionEndonyms as the `languageCodes` argument.
 */
export async function getEndonymLanguageList(): Promise<string[]> {
  const text = await readFile(COUNTRIES_SPEC_PATH, "utf8");
  const spec = JSON.parse(text) as {
    entries: Array<{ primaryLanguageISO6391Code: string | null }>;
  };
  const langs = new Set<string>(SUPPORTED_LANGUAGE_CODES);
  for (const e of spec.entries) {
    if (e.primaryLanguageISO6391Code) langs.add(e.primaryLanguageISO6391Code);
  }
  for (const lang of MAJOR_POPULATION_LANGUAGES) langs.add(lang);
  return [...langs].sort();
}

/**
 * Reads countries.spec.json + returns Map<countryAlpha2, primaryLanguageISO6391Code>.
 * Used by transformers needing per-country primary-language context (e.g., subdivision
 * endonym selection picks the country's primary-language label).
 */
export async function loadCountryPrimaryLanguageMap(): Promise<Map<string, string>> {
  const text = await readFile(COUNTRIES_SPEC_PATH, "utf8");
  const spec = JSON.parse(text) as {
    entries: Array<{ iso31661Alpha2Code: string; primaryLanguageISO6391Code: string | null }>;
  };
  const map = new Map<string, string>();
  for (const e of spec.entries) {
    if (e.primaryLanguageISO6391Code) {
      map.set(e.iso31661Alpha2Code, e.primaryLanguageISO6391Code);
    }
  }
  return map;
}
