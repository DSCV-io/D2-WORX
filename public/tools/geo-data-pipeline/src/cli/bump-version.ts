// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * `pnpm geo:bump-version <version>` — sets `catalogVersion` + `generatedAt` (UTC ISO)
 * across all codegen-consumed spec files in `contracts/geo/` (6 Tier 2 pipeline-derived
 * + 1 Tier 2 hand-rolled GeopoliticalEntity peer).
 *
 * The hand-rolled `geopolitical-entities.spec.json` uses `lastEditedAt` (date-only)
 * rather than `generatedAt` (full ISO) — both get updated per file's schema.
 *
 * The Tier 1 `src-data/*.spec.json` files are NOT touched — those are pipeline-raw,
 * versioned independently by the pipeline writers, not gated by codegen consumers.
 *
 * Usage:
 *   pnpm geo:bump-version 1.0.0
 *   pnpm geo:bump-version --version 1.0.0
 *
 * Exit codes:
 *   0 = success (all spec files updated; per-file summary on stderr)
 *   1 = validation failure (bad version arg, missing files, or write error)
 */

import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

const GEO_DIR = resolve(REPO_ROOT_PATH, "contracts", "geo");

// Pipeline-derived Tier 2 catalogs — use `catalogVersion` + `generatedAt` (full ISO timestamp).
const PIPELINE_DERIVED_FILES: readonly string[] = [
  "countries.spec.json",
  "subdivisions.spec.json",
  "currencies.spec.json",
  "languages.spec.json",
  "locales.spec.json",
  "timezones.spec.json",
];

// Hand-rolled file — uses `catalogVersion` + `lastEditedAt` (date-only).
const HAND_ROLLED_FILES: readonly string[] = [
  "geopolitical-entities.spec.json",
];

// Semver shape: MAJOR.MINOR.PATCH with optional pre-release tag (e.g. `1.0.0-rc1`, `2.3.4-alpha`).
const SEMVER_REGEX = /^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$/;

interface SpecFile {
  catalogVersion?: string;
  generatedAt?: string;
  lastEditedAt?: string;
  [k: string]: unknown;
}

function parseArgs(argv: readonly string[]): string | null {
  const args = argv.slice(2);
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === "--version") {
      const next = args[i + 1];
      if (next) return next;
      return null;
    }
    if (a && !a.startsWith("--")) return a;
  }
  return null;
}

async function bumpFile(
  filename: string,
  newVersion: string,
  isHandRolled: boolean,
): Promise<void> {
  const path = resolve(GEO_DIR, filename);
  const raw = await readFile(path, "utf8");
  const parsed = JSON.parse(raw) as SpecFile;

  const oldVersion = parsed.catalogVersion ?? "<missing>";
  parsed.catalogVersion = newVersion;

  const nowIso = new Date().toISOString();
  if (isHandRolled) {
    // Date-only YYYY-MM-DD form (matches existing `lastEditedAt: "2026-05-18"` shape).
    parsed.lastEditedAt = nowIso.slice(0, 10);
  } else {
    parsed.generatedAt = nowIso;
  }

  await writeSpecJson(path, parsed);
  console.error(`  ${filename}  ${oldVersion}  ->  ${newVersion}`);
}

async function main(): Promise<void> {
  const newVersion = parseArgs(process.argv);
  if (!newVersion) {
    console.error("Error: missing version argument.");
    console.error("Usage: pnpm geo:bump-version <version>");
    console.error("       pnpm geo:bump-version --version <version>");
    console.error("Example: pnpm geo:bump-version 1.0.0");
    process.exit(1);
  }

  if (!SEMVER_REGEX.test(newVersion)) {
    console.error(`Error: invalid version '${newVersion}'.`);
    console.error(
      "Expected semver MAJOR.MINOR.PATCH with optional pre-release tag " +
        "(e.g. 1.0.0 or 1.0.0-rc1).",
    );
    process.exit(1);
  }

  console.error(
    `\n=== Bumping catalogVersion -> ${newVersion} across contracts/geo/ ===\n`,
  );

  try {
    for (const f of PIPELINE_DERIVED_FILES)
      await bumpFile(f, newVersion, false);
    for (const f of HAND_ROLLED_FILES) await bumpFile(f, newVersion, true);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    console.error(`\nError: write failed -- ${message}`);
    process.exit(1);
  }

  const total = PIPELINE_DERIVED_FILES.length + HAND_ROLLED_FILES.length;
  console.error(
    `\nDone. ${total} spec file(s) updated to catalogVersion=${newVersion}.`,
  );
}

await main();
