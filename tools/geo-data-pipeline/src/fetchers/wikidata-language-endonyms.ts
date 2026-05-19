// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchSparql, type SparqlFetchResult } from "./wikidata-sparql.js";

/**
 * Wikidata's P218 = ISO 639-1 language code. Each language entity (e.g., Q188 German)
 * has labels in many languages; the ENDONYM is the label whose `xml:lang` matches the
 * language's own ISO 639-1 code (e.g., German's label@de = "Deutsch", Japanese's
 * label@ja = "日本語").
 *
 * SPARQL query strategy: a single query that returns one row per (iso639_1, endonym)
 * pair, filtered to where the label's language matches the entity's code.
 */
export interface LanguageEndonymEntry {
  iso639_1: string;
  endonym: string;
}

export interface LanguageEndonymFetchResult {
  byCode: Map<string, string>;
  fetch: SparqlFetchResult;
}

export async function fetchWikidataLanguageEndonyms(): Promise<LanguageEndonymFetchResult> {
  const query = `SELECT ?iso ?label WHERE {
  ?lang wdt:P218 ?iso .
  ?lang rdfs:label ?label .
  FILTER(LANG(?label) = ?iso)
}`;
  const result = await fetchSparql({
    query,
    cacheLabel: "language-endonyms",
  });

  const byCode = new Map<string, string>();
  for (const row of result.result.results.bindings) {
    const code = row["iso"]?.value;
    const label = row["label"]?.value;
    if (!code || !label) continue;
    const norm = code.toLowerCase();
    // First-write-wins per lang (rare duplicates from multiple Wikidata items per code)
    if (!byCode.has(norm)) byCode.set(norm, stripLeadingBom(label));
  }
  return { byCode, fetch: result };
}

function stripLeadingBom(value: string): string {
  return value.charCodeAt(0) === 0xfeff ? value.slice(1) : value;
}
