// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "debian-iso-codes";
const SOURCE_LICENSE = "LGPL-2.1+ (Debian iso-codes package)";
const URL_ISO_3166_2 =
  "https://salsa.debian.org/iso-codes-team/iso-codes/-/raw/main/data/iso_3166-2.json";

/**
 * Shape of debian iso-codes' `data/iso_3166-2.json`:
 *
 * ```
 * {
 *   "3166-2": [
 *     { "code": "AD-02", "name": "Canillo", "type": "Parish" },
 *     { "code": "AZ-BAB", "name": "Babək", "parent": "AZ-NX", "type": "Rayon" },
 *     ...
 *   ]
 * }
 * ```
 *
 * - `code`: canonical ISO 3166-2 form with dash (e.g., "US-CA", "GB-ENG")
 * - `name`: English name
 * - `type`: subdivision type per the ISO 3166-2 standard (Parish / State / Province /
 *   Region / Canton / Prefecture / Rayon / Autonomous community / Federal district / etc.)
 * - `parent` (optional): ISO 3166-2 code of the parent subdivision when this entry is
 *   second-or-lower-order. Missing => first-order (directly under the country).
 *
 * Hierarchy reconstruction: walk the `parent` chain — entries with no parent are
 * first-order; entries whose parent has no parent are second-order; etc.
 */
export interface DebianSubdivisionEntry {
  code: string;
  name: string;
  type: string;
  parent?: string;
}

export interface DebianIsoCodesFetchResult extends Pick<CachedFetch, "provenance" | "fromCache"> {
  entries: DebianSubdivisionEntry[];
  /** Indexed by ISO 3166-2 code for fast lookup. */
  byCode: Map<string, DebianSubdivisionEntry>;
}

export async function fetchDebianIso31662(options?: {
  ttlHours?: number;
}): Promise<DebianIsoCodesFetchResult> {
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url: URL_ISO_3166_2,
    license: SOURCE_LICENSE,
    cacheKey: "iso_3166-2.json",
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(fetched.body.toString("utf8")) as {
    "3166-2": DebianSubdivisionEntry[];
  };
  const entries = payload["3166-2"];
  const byCode = new Map<string, DebianSubdivisionEntry>();
  for (const entry of entries) byCode.set(entry.code.toUpperCase(), entry);
  return {
    entries,
    byCode,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

/**
 * Walks the `parent` chain to determine an entry's order (depth).
 * Returns 1 for first-order (no parent), 2 for second-order (parent has no parent), etc.
 * Returns null when a parent code can't be resolved (broken hierarchy — flagged as a warning).
 */
export function computeOrder(
  entry: DebianSubdivisionEntry,
  byCode: Map<string, DebianSubdivisionEntry>,
): number | null {
  let order = 1;
  let current = entry;
  const visited = new Set<string>();
  while (current.parent) {
    if (visited.has(current.code)) return null; // cycle guard
    visited.add(current.code);
    const parent = byCode.get(current.parent.toUpperCase());
    if (!parent) return null; // unresolvable parent
    order++;
    current = parent;
    if (order > 10) return null; // depth guard
  }
  return order;
}

if (
  process.argv[1]?.endsWith("debian-iso-codes.ts") ||
  process.argv[1]?.endsWith("debian-iso-codes.js")
) {
  const result = await fetchDebianIso31662();
  const orderBreakdown = { 1: 0, 2: 0, 3: 0, 4: 0, unresolved: 0 };
  for (const e of result.entries) {
    const order = computeOrder(e, result.byCode);
    if (order === null) orderBreakdown.unresolved++;
    else if (order in orderBreakdown) orderBreakdown[order as 1 | 2 | 3 | 4]++;
    else orderBreakdown[4]++; // bucket all deep entries together for sanity
  }
  // Distinct types
  const types: Record<string, number> = {};
  for (const e of result.entries) types[e.type] = (types[e.type] ?? 0) + 1;
  const topTypes = Object.entries(types)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 15);
  console.log(
    JSON.stringify(
      {
        fromCache: result.fromCache,
        provenance: result.provenance,
        totalEntries: result.entries.length,
        orderBreakdown,
        top15Types: topTypes,
      },
      null,
      2,
    ),
  );
}
