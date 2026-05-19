// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * `pnpm geo:refresh` — full pipeline run.
 *
 * Orders:
 *   1. Per-catalog src-data writers (in dep order — countries first since others
 *      cross-reference it via in-process loads).
 *   2. Tier 2 builder (reads all Tier 1 src-data + hand-rolled GE → produces codegen-ready
 *      contracts/geo/*.spec.json).
 *   3. Parity tests (verifies cross-catalog integrity + denormalization didn't drift).
 *
 * Idempotent. Cached upstream fetches (24h TTL) keep runs fast after the first pull.
 *
 * Exit codes:
 *   0 = success (all Tier 1 writers + Tier 2 builder + parity tests passed)
 *   non-zero = a step failed; subsequent steps NOT run. Re-run after fixing.
 */

import { spawnSync } from "node:child_process";
import { resolve } from "node:path";
import { REPO_ROOT_PATH } from "../util/cache.js";

const PIPELINE_ROOT = resolve(REPO_ROOT_PATH, "tools", "geo-data-pipeline");

const STEPS: Array<{ name: string; script: string }> = [
  {
    name: "write:countries (depends on nothing)",
    script: "src/spec-writers/write-countries.ts",
  },
  {
    name: "write:subdivisions (reads countries.spec)",
    script: "src/spec-writers/write-subdivisions.ts",
  },
  { name: "write:timezones", script: "src/spec-writers/write-timezones.ts" },
  { name: "write:languages", script: "src/spec-writers/write-languages.ts" },
  { name: "write:locales", script: "src/spec-writers/write-locales.ts" },
  {
    name: "write:currencies (reads countries.spec for country→lang map)",
    script: "src/spec-writers/write-currencies.ts",
  },
  {
    name: "tier-2:build (reads ALL Tier 1 src-data + hand-rolled Tier 2 GE)",
    script: "src/tier-2/build-codegen-specs.ts",
  },
];

let stepIndex = 1;
for (const step of STEPS) {
  console.error(`\n=== [${stepIndex}/${STEPS.length + 1}] ${step.name} ===`);
  const result = spawnSync("node", ["--import", "tsx", resolve(PIPELINE_ROOT, step.script)], {
    cwd: PIPELINE_ROOT,
    stdio: "inherit",
  });
  if (result.status !== 0) {
    console.error(`\n❌ Step failed: ${step.name} (exit ${result.status})`);
    process.exit(result.status ?? 1);
  }
  stepIndex++;
}

console.error(`\n=== [${stepIndex}/${STEPS.length + 1}] parity tests ===`);
const testResult = spawnSync("pnpm", ["test"], {
  cwd: PIPELINE_ROOT,
  stdio: "inherit",
  shell: process.platform === "win32",
});
if (testResult.status !== 0) {
  console.error(`\n❌ Parity tests failed (exit ${testResult.status})`);
  process.exit(testResult.status ?? 1);
}

console.error(
  "\n✅ geo:refresh complete — Tier 1 src-data + Tier 2 output + parity tests all green",
);
