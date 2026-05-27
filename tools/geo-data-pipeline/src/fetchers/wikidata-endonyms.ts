// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchSparqlBatch, type SparqlFetchResult } from "./wikidata-sparql.js";

/**
 * Wikidata properties:
 * - P297: ISO 3166-1 alpha-2 (country)
 * - P300: ISO 3166-2 (country subdivision)
 *
 * Query approach: ask SPARQL to return one row per (isoCode, language, label) triple.
 * The result is "long format" — we pivot client-side into `code -> { lang: label }`.
 *
 * Language filter: pass the 88 languages we want (11 supported + 77 country-primary).
 * Batching: subdivisions × 88 langs is too big for a single query (~50MB / 60s timeout).
 * Split into language-group batches of N languages each — typically 4-5 batches at
 * concurrency 5 covers all 88 langs in <2 minutes wall time.
 */

const WIKIDATA_P297_ISO_3166_1 = "P297"; // ISO 3166-1 alpha-2 (country)
const WIKIDATA_P300_ISO_3166_2 = "P300"; // ISO 3166-2 (subdivision)

export interface WikidataLabelEntry {
  /** ISO code (alpha-2 for countries, "XX-YY" for subdivisions). */
  isoCode: string;
  /** Map: ISO 639-1 lang code -> label in that language. */
  labelsByLang: Record<string, string>;
}

export interface WikidataLabelFetchResult {
  /** Map: ISO code -> language -> label. */
  byCode: Map<string, WikidataLabelEntry>;
  /** All SPARQL fetch results that contributed (for the spec's sources[] block). */
  fetches: SparqlFetchResult[];
}

const BATCH_SIZE = 24; // Languages per batch — sized to keep each SPARQL query ~5-15s wall time

export async function fetchCountryEndonyms(
  languageCodes: readonly string[],
): Promise<WikidataLabelFetchResult> {
  return fetchEndonymsForProperty({
    property: WIKIDATA_P297_ISO_3166_1,
    languageCodes,
    label: "countries",
  });
}

export async function fetchSubdivisionEndonyms(
  languageCodes: readonly string[],
): Promise<WikidataLabelFetchResult> {
  return fetchEndonymsForProperty({
    property: WIKIDATA_P300_ISO_3166_2,
    languageCodes,
    label: "subdivisions",
  });
}

interface FetchEndonymsOptions {
  property: string; // e.g. "P297" or "P300"
  languageCodes: readonly string[];
  label: string; // for cache key + log labels
}

async function fetchEndonymsForProperty(
  options: FetchEndonymsOptions,
): Promise<WikidataLabelFetchResult> {
  const batches = chunkLanguages(options.languageCodes, BATCH_SIZE);
  const sparqlOptions = batches.map((batch, i) => ({
    query: buildEndonymsQuery(options.property, batch),
    cacheLabel: `${options.label}-batch-${i + 1}of${batches.length}`,
  }));

  const fetches = await fetchSparqlBatch(sparqlOptions, 5);

  // Merge across batches
  const byCode = new Map<string, WikidataLabelEntry>();
  for (const fetch of fetches) {
    for (const row of fetch.result.results.bindings) {
      const code = row["isoCode"]?.value;
      const label = row["label"]?.value;
      const lang = row["label"]?.["xml:lang"];
      if (!code || !label || !lang) continue;
      const normalizedCode = code.toUpperCase();
      let entry = byCode.get(normalizedCode);
      if (!entry) {
        entry = { isoCode: normalizedCode, labelsByLang: {} };
        byCode.set(normalizedCode, entry);
      }
      // First-write-wins per language: if two query batches return the same (code, lang),
      // keep the first. This shouldn't happen given non-overlapping language batches.
      // Wikidata community labels sometimes carry a stray leading BOM (U+FEFF) from
      // copy-paste — strip it so downstream string comparisons don't silently fail.
      if (!entry.labelsByLang[lang]) {
        entry.labelsByLang[lang] = stripLeadingBom(label);
      }
    }
  }

  return { byCode, fetches };
}

function chunkLanguages(
  languageCodes: readonly string[],
  chunkSize: number,
): string[][] {
  const chunks: string[][] = [];
  for (let i = 0; i < languageCodes.length; i += chunkSize) {
    chunks.push([...languageCodes.slice(i, i + chunkSize)]);
  }
  return chunks;
}

function stripLeadingBom(value: string): string {
  return value.charCodeAt(0) === 0xfeff ? value.slice(1) : value;
}

/**
 * SPARQL query template — one row per (isoCode, language, label) triple.
 *
 * ```sparql
 * SELECT ?isoCode ?label WHERE {
 *   ?entity wdt:<property> ?isoCode .
 *   ?entity rdfs:label ?label .
 *   FILTER(LANG(?label) IN ("en", "ja", "zh", ...))
 * }
 * ```
 *
 * The `?label` binding carries an `xml:lang` attribute we extract client-side to
 * route into `labelsByLang[lang]`. Filtering by language IN-list keeps the result
 * size bounded.
 */
function buildEndonymsQuery(
  property: string,
  languageCodes: readonly string[],
): string {
  const langListSparql = languageCodes.map((l) => `"${l}"`).join(", ");
  return `SELECT ?isoCode ?label WHERE {
  ?entity wdt:${property} ?isoCode .
  ?entity rdfs:label ?label .
  FILTER(LANG(?label) IN (${langListSparql}))
}`;
}

if (
  process.argv[1]?.endsWith("wikidata-endonyms.ts") ||
  process.argv[1]?.endsWith("wikidata-endonyms.js")
) {
  // Smoke test — pull country endonyms for the 11 supported langs
  const r = await fetchCountryEndonyms([
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
  ]);
  console.log("=== Country smoke test ===");
  console.log("countries with labels:", r.byCode.size);
  console.log("US:", JSON.stringify(r.byCode.get("US")));
  console.log("RU:", JSON.stringify(r.byCode.get("RU")));
  console.log("JP:", JSON.stringify(r.byCode.get("JP")));
}
