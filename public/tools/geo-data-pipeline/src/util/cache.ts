// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { createHash } from "node:crypto";
import { existsSync } from "node:fs";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Walk upward from `start` until a monorepo-root sentinel is found.
 * Sentinels: `D2.slnx` or `pnpm-workspace.yaml` (not a fixed `..` count alone).
 */
function findMonorepoRoot(start: string): string {
  let dir = start;

  for (let i = 0; i < 24; i++) {
    if (
      existsSync(join(dir, "D2.slnx")) ||
      existsSync(join(dir, "pnpm-workspace.yaml"))
    ) {
      return dir;
    }

    const parent = resolve(dir, "..");

    if (parent === dir) {
      break;
    }

    dir = parent;
  }

  throw new Error(
    `geo-data-pipeline: could not locate monorepo root from ${start} ` +
      `(expected D2.slnx or pnpm-workspace.yaml)`,
  );
}

const thisDir = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = findMonorepoRoot(thisDir);
const CACHE_DIR = resolve(
  REPO_ROOT,
  "public",
  "tools",
  "geo-data-pipeline",
  ".cache",
);

export interface FetchProvenance {
  source: string;
  url: string;
  license: string;
  fetchedAt: string;
  sizeBytes: number;
  sha256: string;
}

export interface CachedFetch<TBody = Buffer> {
  body: TBody;
  cachedPath: string;
  provenance: FetchProvenance;
  fromCache: boolean;
}

interface FetchAndCacheOptions {
  source: string;
  url: string;
  license: string;
  cacheKey: string;
  ttlHours?: number;
}

/**
 * Fetches a URL and caches the body under .cache/<source>/<cacheKey>.
 * Skips the fetch if a fresh cached copy exists (default TTL: 24h; pass 0 to always refetch).
 * Always writes a sibling .provenance.json with URL + license + sha256 + timestamp.
 */
export async function fetchAndCache(
  options: FetchAndCacheOptions,
): Promise<CachedFetch> {
  const ttlHours = options.ttlHours ?? 24;
  const cacheSubdir = join(CACHE_DIR, options.source);
  const cachedBodyPath = join(cacheSubdir, options.cacheKey);
  const cachedProvenancePath = `${cachedBodyPath}.provenance.json`;

  if (ttlHours > 0) {
    const fresh = await isFresh(cachedBodyPath, ttlHours);
    if (fresh) {
      const body = await readFile(cachedBodyPath);
      const provenanceRaw = await readFile(cachedProvenancePath, "utf8");
      const provenance = JSON.parse(provenanceRaw) as FetchProvenance;
      return { body, cachedPath: cachedBodyPath, provenance, fromCache: true };
    }
  }

  console.error(`[fetch] ${options.source} <- ${options.url}`);
  let response: Response;
  try {
    response = await fetch(options.url, {
      signal: AbortSignal.timeout(60_000),
    });
  } catch (err) {
    if (err instanceof Error && err.name === "TimeoutError") {
      throw new Error(`Fetch timeout (60s): ${options.url}`, { cause: err });
    }
    throw err;
  }
  if (!response.ok) {
    throw new Error(
      `Fetch failed: ${options.url} -> ${response.status} ${response.statusText}`,
    );
  }
  const arrayBuffer = await response.arrayBuffer();
  const body = Buffer.from(arrayBuffer);

  const sha256 = createHash("sha256").update(body).digest("hex");
  const provenance: FetchProvenance = {
    source: options.source,
    url: options.url,
    license: options.license,
    fetchedAt: new Date().toISOString(),
    sizeBytes: body.byteLength,
    sha256,
  };

  await mkdir(cacheSubdir, { recursive: true });
  await writeFile(cachedBodyPath, body);
  await writeFile(
    cachedProvenancePath,
    `${JSON.stringify(provenance, null, 2)}\n`,
  );

  return { body, cachedPath: cachedBodyPath, provenance, fromCache: false };
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

export const REPO_ROOT_PATH = REPO_ROOT;
