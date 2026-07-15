// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * `pnpm geo:approve` — interactive per-entry accept/reject of upstream changes detected
 * by `pnpm geo:diff` (working tree vs `git HEAD`). Operator decisions persist to
 * `contracts/geo/.upstream-rejections.json` so future refreshes auto-restore rejected
 * entries from the committed version unless explicitly re-approved.
 *
 * Workflow (per ADDED or MODIFIED entry):
 *   - y / <enter>  -> KEEP working-tree value as-is (the freshly-generated entry)
 *   - n            -> REJECT: restore committed entry into working tree + record
 *                     {catalog, entryKey, reason} in `.upstream-rejections.json`
 *   - s            -> SKIP: leave working tree unchanged, no rejection recorded
 *                     (decide later)
 *
 * Removals are reported but not interactively prompted — explicit removal from upstream
 * is treated as informational; operator handles via direct edit if they want to suppress.
 *
 * Flags:
 *   --catalog X        scope to a single catalog (countries / subdivisions / ...)
 *   --non-interactive  (alias --ci) fail fast with exit 1 if any diff exists; never prompt
 *
 * Exit codes:
 *   0 = clean (no diffs, or all approved interactively)
 *   1 = unresolved diffs remain (non-interactive mode, or operator skipped some)
 *   2 = error reading / writing a spec file or rejections memory file
 */

import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import * as readline from "node:readline/promises";
import { spawnSync } from "node:child_process";
import { stdin as input, stdout as output } from "node:process";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

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
const REJECTIONS_PATH = resolve(GEO_DIR_ABS, ".upstream-rejections.json");

interface SpecWrapper {
  entries?: unknown[];
  [k: string]: unknown;
}

interface RejectionRecord {
  catalog: string;
  entryKey: string;
  reason: string;
  rejectedAt: string;
}

interface RejectionsFile {
  rejections: RejectionRecord[];
}

interface Flags {
  catalog: string | null;
  nonInteractive: boolean;
}

function parseFlags(argv: readonly string[]): Flags {
  const args = argv.slice(2);
  let catalog: string | null = null;
  let nonInteractive = false;
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === "--catalog") {
      const next = args[i + 1];
      if (next) {
        catalog = next.endsWith(".spec.json") ? next : `${next}.spec.json`;
        i++;
      }
    } else if (a === "--non-interactive" || a === "--ci") {
      nonInteractive = true;
    }
  }
  return { catalog, nonInteractive };
}

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

async function loadRejections(): Promise<RejectionsFile> {
  try {
    const raw = await readFile(REJECTIONS_PATH, "utf8");
    const parsed = JSON.parse(raw) as Partial<RejectionsFile>;
    if (Array.isArray(parsed.rejections))
      return { rejections: parsed.rejections };
    return { rejections: [] };
  } catch {
    return { rejections: [] };
  }
}

async function saveRejections(rejections: RejectionsFile): Promise<void> {
  await writeSpecJson(REJECTIONS_PATH, rejections);
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

interface CatalogChanges {
  catalog: CatalogConfig;
  committedRaw: string | null;
  workingRaw: string | null;
  committedWrapper: SpecWrapper | null;
  workingWrapper: SpecWrapper | null;
  added: string[];
  removed: string[];
  modified: string[];
}

async function diffCatalog(catalog: CatalogConfig): Promise<CatalogChanges> {
  const relPath = `${GEO_DIR_REL}/${catalog.filename}`;
  const absPath = resolve(GEO_DIR_ABS, catalog.filename);
  const committedRaw = readGitHead(relPath);
  const workingRaw = await readWorkingTree(absPath);

  const committedWrapper = committedRaw
    ? (JSON.parse(committedRaw) as SpecWrapper)
    : null;
  const workingWrapper = workingRaw
    ? (JSON.parse(workingRaw) as SpecWrapper)
    : null;
  const committedEntries = Array.isArray(committedWrapper?.entries)
    ? committedWrapper.entries
    : [];
  const workingEntries = Array.isArray(workingWrapper?.entries)
    ? workingWrapper.entries
    : [];

  const committed = indexByKey(committedEntries, catalog.key);
  const working = indexByKey(workingEntries, catalog.key);

  const added: string[] = [];
  const removed: string[] = [];
  const modified: string[] = [];

  for (const k of working.keys()) if (!committed.has(k)) added.push(k);
  for (const k of committed.keys()) if (!working.has(k)) removed.push(k);
  for (const [k, w] of working) {
    const c = committed.get(k);
    if (!c) continue;
    if (canonicalJson(c) !== canonicalJson(w)) modified.push(k);
  }

  added.sort();
  removed.sort();
  modified.sort();

  return {
    catalog,
    committedRaw,
    workingRaw,
    committedWrapper,
    workingWrapper,
    added,
    removed,
    modified,
  };
}

interface OperatorDecision {
  action: "keep" | "reject" | "skip";
  reason: string;
}

async function promptOperator(
  rl: readline.Interface,
  catalogName: string,
  entryKey: string,
  changeKind: "added" | "modified",
  committedJson: string,
  workingJson: string,
): Promise<OperatorDecision> {
  console.error(`\n--- ${catalogName} :: ${entryKey} (${changeKind}) ---`);
  if (changeKind === "added") {
    console.error("  (new entry in working tree; not present in HEAD)");
    console.error(`  WORKING:\n${indent(workingJson, "    ")}`);
  } else {
    console.error(`  HEAD:\n${indent(committedJson, "    ")}`);
    console.error(`  WORKING:\n${indent(workingJson, "    ")}`);
  }
  while (true) {
    const ans = (await rl.question("Keep working-tree value? [Y/n/s(kip)]: "))
      .trim()
      .toLowerCase();
    if (ans === "" || ans === "y" || ans === "yes")
      return { action: "keep", reason: "" };
    if (ans === "n" || ans === "no") {
      const reason = (await rl.question("Reason for rejection: ")).trim();
      return { action: "reject", reason: reason || "(no reason given)" };
    }
    if (ans === "s" || ans === "skip") return { action: "skip", reason: "" };
    console.error("  Please answer y / n / s.");
  }
}

function indent(text: string, prefix: string): string {
  return text
    .split("\n")
    .map((l) => prefix + l)
    .join("\n");
}

async function applyRejections(
  changes: CatalogChanges,
  rejectedKeys: ReadonlySet<string>,
): Promise<void> {
  if (rejectedKeys.size === 0) return;
  if (!changes.workingWrapper || !Array.isArray(changes.workingWrapper.entries))
    return;
  if (
    !changes.committedWrapper ||
    !Array.isArray(changes.committedWrapper.entries)
  )
    return;

  const committed = indexByKey(
    changes.committedWrapper.entries,
    changes.catalog.key,
  );
  const updated = changes.workingWrapper.entries
    .filter((e) => {
      if (!e || typeof e !== "object") return true;
      const k = (e as Record<string, unknown>)[changes.catalog.key];
      // Drop any ADDED entry the operator rejected (key not in committed).
      if (typeof k === "string" && rejectedKeys.has(k) && !committed.has(k))
        return false;
      return true;
    })
    .map((e) => {
      if (!e || typeof e !== "object") return e;
      const k = (e as Record<string, unknown>)[changes.catalog.key];
      // Replace any MODIFIED entry the operator rejected with the committed version.
      if (typeof k === "string" && rejectedKeys.has(k) && committed.has(k)) {
        return committed.get(k)!;
      }
      return e;
    });

  const newWrapper = { ...changes.workingWrapper, entries: updated };
  const absPath = resolve(GEO_DIR_ABS, changes.catalog.filename);
  await writeSpecJson(absPath, newWrapper);
}

async function main(): Promise<void> {
  const flags = parseFlags(process.argv);

  const targetCatalogs = flags.catalog
    ? CATALOGS.filter((c) => c.filename === flags.catalog)
    : CATALOGS;

  if (flags.catalog && targetCatalogs.length === 0) {
    console.error(`Error: unknown catalog '${flags.catalog}'.`);
    console.error(
      `Known: ${CATALOGS.map((c) => c.filename.replace(".spec.json", "")).join(", ")}`,
    );
    process.exit(2);
  }

  let allChanges: CatalogChanges[];
  try {
    allChanges = await Promise.all(targetCatalogs.map(diffCatalog));
  } catch (err) {
    console.error(
      `Error: failed to diff catalogs -- ${err instanceof Error ? err.message : err}`,
    );
    process.exit(2);
  }

  const totalAdded = allChanges.reduce((acc, c) => acc + c.added.length, 0);
  const totalRemoved = allChanges.reduce((acc, c) => acc + c.removed.length, 0);
  const totalModified = allChanges.reduce(
    (acc, c) => acc + c.modified.length,
    0,
  );

  console.error(
    `\ngeo:approve scope: ${targetCatalogs.length} catalog(s); ` +
      `+${totalAdded} -${totalRemoved} ~${totalModified} pending decisions.\n`,
  );

  if (totalAdded === 0 && totalModified === 0) {
    if (totalRemoved > 0) {
      console.error(
        `Note: ${totalRemoved} removed entr(ies) detected (not interactively prompted).`,
      );
      for (const c of allChanges) {
        if (c.removed.length > 0) {
          const more = c.removed.length > 5 ? ", ..." : "";
          const preview = c.removed.slice(0, 5).join(", ");
          console.error(
            `  ${c.catalog.filename}: removed ${c.removed.length} (${preview}${more})`,
          );
        }
      }
    }
    console.error("No add/modify diffs to approve. Clean.");
    process.exit(0);
  }

  if (flags.nonInteractive) {
    console.error(
      "Non-interactive mode: pending diffs detected. Aborting without prompting.",
    );
    for (const c of allChanges) {
      if (c.added.length > 0) {
        const more = c.added.length > 5 ? ", ..." : "";
        const preview = c.added.slice(0, 5).join(", ");
        console.error(
          `  ${c.catalog.filename}: +${c.added.length} added (${preview}${more})`,
        );
      }
      if (c.modified.length > 0) {
        const more = c.modified.length > 5 ? ", ..." : "";
        const preview = c.modified.slice(0, 5).join(", ");
        console.error(
          `  ${c.catalog.filename}: ~${c.modified.length} modified (${preview}${more})`,
        );
      }
    }
    process.exit(1);
  }

  const rejections = await loadRejections();
  const rl = readline.createInterface({ input, output });

  let skippedCount = 0;
  let keptCount = 0;
  let rejectedCount = 0;

  try {
    for (const change of allChanges) {
      if (change.added.length === 0 && change.modified.length === 0) continue;
      if (
        !change.workingWrapper ||
        !Array.isArray(change.workingWrapper.entries)
      )
        continue;

      const working = indexByKey(
        change.workingWrapper.entries,
        change.catalog.key,
      );
      const committedEntries = Array.isArray(change.committedWrapper?.entries)
        ? change.committedWrapper.entries
        : [];
      const committed = indexByKey(committedEntries, change.catalog.key);

      const rejectedKeysForCatalog = new Set<string>();

      const decide = async (
        key: string,
        kind: "added" | "modified",
      ): Promise<void> => {
        const workingEntry = working.get(key);
        const committedEntry = committed.get(key);
        const workingJson = workingEntry
          ? JSON.stringify(workingEntry, null, 2)
          : "(missing)";
        const committedJson = committedEntry
          ? JSON.stringify(committedEntry, null, 2)
          : "(missing)";
        const decision = await promptOperator(
          rl,
          change.catalog.filename,
          key,
          kind,
          committedJson,
          workingJson,
        );
        if (decision.action === "keep") {
          keptCount++;
        } else if (decision.action === "skip") {
          skippedCount++;
        } else {
          rejectedCount++;
          rejectedKeysForCatalog.add(key);
          rejections.rejections.push({
            catalog: change.catalog.filename,
            entryKey: key,
            reason: decision.reason,
            rejectedAt: new Date().toISOString(),
          });
        }
      };

      for (const key of change.added) await decide(key, "added");
      for (const key of change.modified) await decide(key, "modified");

      await applyRejections(change, rejectedKeysForCatalog);
    }
  } finally {
    rl.close();
  }

  try {
    await saveRejections(rejections);
  } catch (err) {
    const msg = err instanceof Error ? err.message : err;
    console.error(`Error: failed to write ${REJECTIONS_PATH} -- ${msg}`);
    process.exit(2);
  }

  console.error(
    `\nDone. kept=${keptCount} rejected=${rejectedCount} skipped=${skippedCount}.`,
  );
  if (rejectedCount > 0) {
    console.error(`Rejections recorded to: ${REJECTIONS_PATH}`);
  }
  process.exit(skippedCount > 0 ? 1 : 0);
}

await main();
