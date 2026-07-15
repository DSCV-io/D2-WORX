// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import Papa from "papaparse";
import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "datasets-language-codes";
// upstream URL — cannot wrap
const SOURCE_URL =
  "https://raw.githubusercontent.com/datasets/language-codes/main/data/language-codes.csv";
const SOURCE_LICENSE =
  "PDDL-1.0 (Open Knowledge Foundation Public Domain Dedication)";
const CACHE_KEY = "language-codes.csv";

/**
 * One row of datasets/language-codes — minimal: ISO 639-1 alpha-2 + English name.
 *
 * Sample:
 *   "alpha2","English"
 *   "aa","Afar"
 *   "ab","Abkhazian"
 *   "en","English"
 */
export interface LanguageCodeRow {
  alpha2: string;
  english: string;
}

export interface LanguageCodesFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  rows: LanguageCodeRow[];
}

export async function fetchDatasetsLanguageCodes(options?: {
  ttlHours?: number;
}): Promise<LanguageCodesFetchResult> {
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url: SOURCE_URL,
    license: SOURCE_LICENSE,
    cacheKey: CACHE_KEY,
    ttlHours: options?.ttlHours,
  });
  const csvText = fetched.body.toString("utf8");
  const parsed = Papa.parse<Record<string, string>>(csvText, {
    header: true,
    skipEmptyLines: true,
  });
  const rows: LanguageCodeRow[] = parsed.data
    .map((r) => ({
      alpha2: (r["alpha2"] ?? "").trim().toLowerCase(),
      english: (r["English"] ?? "").trim(),
    }))
    .filter((r) => r.alpha2.length === 2 && r.english);
  return {
    rows,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}
