// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * `pnpm geo:diff` — entry-level structured diff of every committed
 * `contracts/geo/*.spec.json` against its current on-disk version.
 *
 * Identity per catalog (natural key):
 *   countries              -> iso31661Alpha2Code
 *   subdivisions           -> iso31662Code
 *   currencies             -> iso4217AlphaCode
 *   languages              -> iso6391Code
 *   locales                -> ietfBcp47Tag
 *   timezones              -> ianaIdentifier
 *   geopolitical-entities  -> shortCode
 *
 * Report shape per catalog:
 *   `{ added: [...keys], removed: [...keys], modified: [{ key, changedFields: [...] }] }`
 *
 * Output:
 *   stdout = structured JSON report (machine-consumable; default behavior)
 *   stderr = pretty human-readable summary (counts per catalog + sample changes)
 *
 * Flags:
 *   --json   suppress the human-readable stderr summary (machine-only)
 *
 * Exit codes:
 *   0 = no diffs across any catalog (clean)
 *   1 = at least one diff in at least one catalog (CI gate fails)
 *   2 = error reading or parsing a file (operational failure, not data drift)
 */

import { spawnSync } from "node:child_process";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { REPO_ROOT_PATH } from "../util/cache.js";

interface CatalogConfig {
  filename: string;
  key: string;
}

const CATALOGS: readonly CatalogConfig[] = [
  { filename: "countries.spec.json", key: "iso31661Alpha2Code" },
  { filename: "subdivisions.spec.json", key: "iso31662Code" },
  { filename: "currencies.spec.json", key: "iso4217AlphaCode" },
  { filename: "languages.spec.json", key: "iso6391Code" },
  { filename: "locales.spec.json", key: "ietfBcp47Tag" },
  { filename: "timezones.spec.json", key: "ianaIdentifier" },
  { filename: "geopolitical-entities.spec.json", key: "shortCode" },
];

const GEO_DIR_REL = "contracts/geo";
const GEO_DIR_ABS = resolve(REPO_ROOT_PATH, GEO_DIR_REL);

interface ModifiedEntry {
  key: string;
  changedFields: string[];
}

interface CatalogDiff {
  catalog: string;
  added: string[];
  removed: string[];
  modified: ModifiedEntry[];
}

interface DiffReport {
  summary: {
    totalCatalogs: number;
    catalogsWithDiffs: number;
    totalAdded: number;
    totalRemoved: number;
    totalModified: number;
  };
  catalogs: CatalogDiff[];
}

interface SpecWrapper {
  entries?: unknown[];
  [k: string]: unknown;
}

function parseFlags(argv: readonly string[]): { jsonOnly: boolean } {
  return { jsonOnly: argv.includes("--json") };
}

/**
 * Reads the HEAD-committed version of a file via `git show`. Returns null if file isn't tracked.
 */
function readGitHead(relPath: string): string | null {
  const result = spawnSync("git", ["show", `HEAD:${relPath}`], {
    cwd: REPO_ROOT_PATH,
    encoding: "utf8",
  });
  if (result.status !== 0) return null;
  return result.stdout;
}

async function readWorkingTree(absPath: string): Promise<string | null> {
  try {
    return await readFile(absPath, "utf8");
  } catch {
    return null;
  }
}

function indexByKey(
  entries: unknown[],
  key: string,
): Map<string, Record<string, unknown>> {
  const map = new Map<string, Record<string, unknown>>();
  for (const e of entries) {
    if (e === null || typeof e !== "object") continue;
    const obj = e as Record<string, unknown>;
    const k = obj[key];
    if (typeof k !== "string") continue;
    map.set(k, obj);
  }
  return map;
}

/** Stable deep-equal via canonical JSON stringification (object keys sorted). */
function deepEqual(a: unknown, b: unknown): boolean {
  return canonicalJson(a) === canonicalJson(b);
}

function canonicalJson(value: unknown): string {
  if (value === null) return "null";
  if (typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(",")}]`;
  const obj = value as Record<string, unknown>;
  const keys = Object.keys(obj).sort();
  const parts = keys.map(
    (k) => `${JSON.stringify(k)}:${canonicalJson(obj[k])}`,
  );
  return `{${parts.join(",")}}`;
}

function diffEntries(
  catalog: string,
  key: string,
  committedEntries: unknown[],
  workingEntries: unknown[],
): CatalogDiff {
  const committed = indexByKey(committedEntries, key);
  const working = indexByKey(workingEntries, key);

  const added: string[] = [];
  const removed: string[] = [];
  const modified: ModifiedEntry[] = [];

  for (const k of working.keys()) {
    if (!committed.has(k)) added.push(k);
  }
  for (const k of committed.keys()) {
    if (!working.has(k)) removed.push(k);
  }
  for (const [k, workingEntry] of working) {
    const committedEntry = committed.get(k);
    if (!committedEntry) continue;
    const changedFields: string[] = [];
    const fieldNames = new Set<string>([
      ...Object.keys(committedEntry),
      ...Object.keys(workingEntry),
    ]);
    for (const f of fieldNames) {
      if (!deepEqual(committedEntry[f], workingEntry[f])) changedFields.push(f);
    }
    if (changedFields.length > 0) {
      changedFields.sort();
      modified.push({ key: k, changedFields });
    }
  }

  added.sort();
  removed.sort();
  modified.sort((a, b) => a.key.localeCompare(b.key));

  return { catalog, added, removed, modified };
}

async function diffCatalog(
  catalog: CatalogConfig,
): Promise<CatalogDiff | { error: string; catalog: string }> {
  const relPath = `${GEO_DIR_REL}/${catalog.filename}`;
  const absPath = resolve(GEO_DIR_ABS, catalog.filename);

  const committedRaw = readGitHead(relPath);
  const workingRaw = await readWorkingTree(absPath);

  if (committedRaw === null && workingRaw === null) {
    return {
      error: `Neither HEAD nor working tree has ${relPath}`,
      catalog: catalog.filename,
    };
  }

  let committedEntries: unknown[] = [];
  let workingEntries: unknown[] = [];

  if (committedRaw !== null) {
    try {
      const parsed = JSON.parse(committedRaw) as SpecWrapper;
      committedEntries = Array.isArray(parsed.entries) ? parsed.entries : [];
    } catch (err) {
      const msg = err instanceof Error ? err.message : err;
      return {
        error: `Failed to parse HEAD:${relPath} -- ${msg}`,
        catalog: catalog.filename,
      };
    }
  }

  if (workingRaw !== null) {
    try {
      const parsed = JSON.parse(workingRaw) as SpecWrapper;
      workingEntries = Array.isArray(parsed.entries) ? parsed.entries : [];
    } catch (err) {
      const msg = err instanceof Error ? err.message : err;
      return {
        error: `Failed to parse working ${relPath} -- ${msg}`,
        catalog: catalog.filename,
      };
    }
  }

  return diffEntries(
    catalog.filename,
    catalog.key,
    committedEntries,
    workingEntries,
  );
}

function printHumanSummary(report: DiffReport): void {
  console.error("\n=== geo:diff summary ===\n");
  for (const c of report.catalogs) {
    const hasAny =
      c.added.length > 0 || c.removed.length > 0 || c.modified.length > 0;
    if (!hasAny) {
      console.error(`  ${c.catalog.padEnd(34)}  (no changes)`);
      continue;
    }
    console.error(`  ${c.catalog}`);
    if (c.added.length > 0) {
      const more = c.added.length > 5 ? ", ..." : "";
      const preview = c.added.slice(0, 5).join(", ");
      console.error(`    + added    (${c.added.length}): ${preview}${more}`);
    }
    if (c.removed.length > 0) {
      const more = c.removed.length > 5 ? ", ..." : "";
      const preview = c.removed.slice(0, 5).join(", ");
      console.error(`    - removed  (${c.removed.length}): ${preview}${more}`);
    }
    if (c.modified.length > 0) {
      console.error(`    ~ modified (${c.modified.length}):`);
      for (const m of c.modified.slice(0, 5)) {
        console.error(`        ${m.key}  [${m.changedFields.join(", ")}]`);
      }
      if (c.modified.length > 5) console.error("        ...");
    }
  }
  const s = report.summary;
  console.error(
    `\nTotals: ${s.catalogsWithDiffs}/${s.totalCatalogs} catalogs changed; ` +
      `+${s.totalAdded} -${s.totalRemoved} ~${s.totalModified}\n`,
  );
}

async function main(): Promise<void> {
  const flags = parseFlags(process.argv);

  const catalogResults: CatalogDiff[] = [];
  let hadError = false;
  for (const c of CATALOGS) {
    const r = await diffCatalog(c);
    if ("error" in r) {
      console.error(`Error: ${r.error}`);
      hadError = true;
      continue;
    }
    catalogResults.push(r);
  }
  if (hadError) process.exit(2);

  const summary = {
    totalCatalogs: catalogResults.length,
    catalogsWithDiffs: catalogResults.filter(
      (c) =>
        c.added.length > 0 || c.removed.length > 0 || c.modified.length > 0,
    ).length,
    totalAdded: catalogResults.reduce((acc, c) => acc + c.added.length, 0),
    totalRemoved: catalogResults.reduce((acc, c) => acc + c.removed.length, 0),
    totalModified: catalogResults.reduce(
      (acc, c) => acc + c.modified.length,
      0,
    ),
  };

  const report: DiffReport = { summary, catalogs: catalogResults };

  // stdout = structured JSON (always, machine-consumable).
  console.log(JSON.stringify(report, null, 2));

  // stderr = pretty summary (unless --json suppresses).
  if (!flags.jsonOnly) printHumanSummary(report);

  const hasDiffs =
    summary.totalAdded > 0 ||
    summary.totalRemoved > 0 ||
    summary.totalModified > 0;
  process.exit(hasDiffs ? 1 : 0);
}

await main();
