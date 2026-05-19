// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { createHash } from "node:crypto";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { REPO_ROOT_PATH, type FetchProvenance } from "../util/cache.js";

const SOURCE_NAME = "wikidata";
const SOURCE_LICENSE = "CC0-1.0 (Wikidata)";
const ENDPOINT = "https://query.wikidata.org/sparql";
const USER_AGENT =
  "D2-WORX-geo-data-pipeline/0.1 " +
  "(https://github.com/DCSV/D2-WORX; contact: ops@dcsv) Node/24 (Windows)";

const CACHE_DIR = resolve(REPO_ROOT_PATH, "tools", "geo-data-pipeline", ".cache", SOURCE_NAME);

/**
 * Wikidata Query Service SPARQL endpoint:
 * - Public, no API key
 * - User-Agent REQUIRED (Wikidata returns 403 without an identifying UA per their policy)
 * - 60-second query timeout per request
 * - Returns JSON when Accept: application/sparql-results+json (or ?format=json)
 *
 * Result shape:
 * ```
 * {
 *   "head": { "vars": [...] },
 *   "results": {
 *     "bindings": [
 *       { "<var>": { "type": "literal", "value": "...", "xml:lang"?: "ja" } }
 *     ]
 *   }
 * }
 * ```
 */
export interface SparqlBinding {
  type: string;
  value: string;
  "xml:lang"?: string;
  datatype?: string;
}

export interface SparqlResult {
  head: { vars: string[] };
  results: { bindings: Array<Record<string, SparqlBinding>> };
}

export interface SparqlFetchResult {
  result: SparqlResult;
  provenance: FetchProvenance;
  fromCache: boolean;
  queryWallMs: number;
}

interface SparqlOptions {
  /** SPARQL query string. Cache key derived from sha256 of this. */
  query: string;
  /** Short identifying label for the cache key + log lines (e.g. "subdivisions-batch-1"). */
  cacheLabel: string;
  /** Forces refetch when cached file older than this. Pass 0 to always refetch. Default 24h. */
  ttlHours?: number;
}

/**
 * Executes a SPARQL query against Wikidata's Query Service with caching + provenance.
 * Cache key = sha256(query) so identical queries hit cache; query changes invalidate.
 */
export async function fetchSparql(options: SparqlOptions): Promise<SparqlFetchResult> {
  const ttlHours = options.ttlHours ?? 24;
  const queryHash = createHash("sha256").update(options.query).digest("hex").substring(0, 16);
  const cacheFile = join(CACHE_DIR, `${options.cacheLabel}-${queryHash}.json`);
  const provenanceFile = `${cacheFile}.provenance.json`;

  if (ttlHours > 0 && (await isFresh(cacheFile, ttlHours))) {
    const cachedResult = JSON.parse(await readFile(cacheFile, "utf8")) as SparqlResult;
    const cachedProvenance = JSON.parse(await readFile(provenanceFile, "utf8")) as FetchProvenance;
    return { result: cachedResult, provenance: cachedProvenance, fromCache: true, queryWallMs: 0 };
  }

  console.error(`[fetch] wikidata SPARQL "${options.cacheLabel}" (${queryHash})`);
  const startMs = Date.now();
  const url = `${ENDPOINT}?format=json&query=${encodeURIComponent(options.query)}`;
  let response: Response;
  try {
    response = await fetch(url, {
      headers: { "User-Agent": USER_AGENT, Accept: "application/sparql-results+json" },
      signal: AbortSignal.timeout(120_000),
    });
  } catch (err) {
    if (err instanceof Error && err.name === "TimeoutError") {
      throw new Error(`Wikidata SPARQL fetch timeout (120s) for "${options.cacheLabel}"`);
    }
    throw err;
  }
  const queryWallMs = Date.now() - startMs;

  if (!response.ok) {
    const body = await response.text();
    throw new Error(
      `Wikidata SPARQL fetch failed (${options.cacheLabel}): ` +
        `${response.status} ${response.statusText}\n${body.substring(0, 500)}`,
    );
  }

  const bodyText = await response.text();
  const result = JSON.parse(bodyText) as SparqlResult;
  const sha256 = createHash("sha256").update(bodyText).digest("hex");

  const provenance: FetchProvenance = {
    source: SOURCE_NAME,
    url: ENDPOINT,
    license: SOURCE_LICENSE,
    fetchedAt: new Date().toISOString(),
    sizeBytes: Buffer.byteLength(bodyText, "utf8"),
    sha256,
  };

  await mkdir(CACHE_DIR, { recursive: true });
  await writeFile(cacheFile, bodyText);
  await writeFile(provenanceFile, `${JSON.stringify(provenance, null, 2)}\n`);

  console.error(
    `  [wikidata] "${options.cacheLabel}" -> ${result.results.bindings.length} rows ` +
      `in ${queryWallMs}ms (${(provenance.sizeBytes / 1024).toFixed(1)}KB)`,
  );

  return { result, provenance, fromCache: false, queryWallMs };
}

/**
 * Parallel batch executor with explicit concurrency cap per Wikidata's 5-concurrent limit.
 * Returns results in input order. Aborts on first error to avoid burning quota.
 */
export async function fetchSparqlBatch(
  options: SparqlOptions[],
  concurrency: number = 5,
): Promise<SparqlFetchResult[]> {
  const results: SparqlFetchResult[] = new Array(options.length);
  let nextIndex = 0;

  async function worker(): Promise<void> {
    while (true) {
      const i = nextIndex++;
      if (i >= options.length) return;
      const opt = options[i];
      if (!opt) return;
      results[i] = await fetchSparql(opt);
    }
  }

  await Promise.all(Array.from({ length: Math.min(concurrency, options.length) }, () => worker()));
  return results;
}

async function isFresh(path: string, ttlHours: number): Promise<boolean> {
  try {
    const stats = await stat(path);
    const ageMs = Date.now() - stats.mtimeMs;
    return ageMs < ttlHours * 3_600_000;
  } catch {
    return false;
  }
}
