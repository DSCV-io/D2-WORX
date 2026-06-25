// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// CLI entry point for the release runner.
//
// Usage:
//   pnpm --filter release-runner exec tsx src/cli.ts [options]
//
// Options:
//   --against <ref>     Baseline git ref — the integration baseline branch.
//                       Resolution order: --against arg, then D2_RELEASE_BASELINE
//                       env var. Error if neither is provided.
//   --package <name>    Restrict to a single package
//   --dry-run           Compute and report without writing (default: true)
//   --apply             Write bumps + changelogs (disables dry-run)
//   --graduate <name>   Graduate a pre-stable package from 0.x.y to 1.0.0
//
// Excluded from the unit-coverage threshold (see vitest.config.ts).

import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { resolveBaseline } from "./baseline.js";
import { commitsInRange } from "./git-adapter.js";
import { loadAllPackages } from "./manifest-loader.js";
import { runRelease } from "./runner.js";
import { graduatePackage } from "./graduate.js";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const repoRoot = resolve(__dirname, "../../..");

// ---------------------------------------------------------------------------
// Argument parsing (minimal — no third-party parser needed)
// ---------------------------------------------------------------------------

const args = process.argv.slice(2);

function flag(name: string): boolean {
  return args.includes(name);
}

function option(name: string, defaultValue: string): string {
  const idx = args.indexOf(name);

  return idx !== -1 && idx + 1 < args.length
    ? (args[idx + 1] ?? defaultValue)
    : defaultValue;
}

// ---------------------------------------------------------------------------
// Baseline resolution: --against arg > D2_RELEASE_BASELINE env var
// ---------------------------------------------------------------------------

const argAgainst = (() => {
  const idx = args.indexOf("--against");

  return idx !== -1 && idx + 1 < args.length ? args[idx + 1] : undefined;
})();

const against = resolveBaseline(argAgainst, process.env["D2_RELEASE_BASELINE"]);

if (against === undefined) {
  console.error(
    "error: no baseline ref supplied. Pass --against <ref> or set D2_RELEASE_BASELINE.",
  );
  process.exit(1);
}
const packageFilter = flag("--package") ? option("--package", "") : undefined;
const dryRun = !flag("--apply");
const graduateTarget = flag("--graduate")
  ? option("--graduate", "")
  : undefined;

// ---------------------------------------------------------------------------
// Today date (injectable via --today for reproducible output in tests)
// ---------------------------------------------------------------------------

function isoToday(): string {
  return new Date().toISOString().slice(0, 10);
}

const today = option("--today", isoToday());

// ---------------------------------------------------------------------------
// Run
// ---------------------------------------------------------------------------

const packages = loadAllPackages(repoRoot);

// ---------------------------------------------------------------------------
// Graduate mode — mutually exclusive with the commit-range bump path
// ---------------------------------------------------------------------------

if (graduateTarget !== undefined && graduateTarget.length > 0) {
  const result = graduatePackage(graduateTarget, packages, today, dryRun);
  const mode = result.applied ? "APPLIED" : "DRY-RUN";

  console.log(
    `\nGraduate — ${mode}: ${result.pkg.name} ${result.pkg.currentVersion} → ${result.newVersion}`,
  );

  if (!result.applied) {
    console.log(`(dry-run — pass --apply to write)`);
  }
} else {
  const commits = commitsInRange(against, "HEAD");

  const result = runRelease(commits, packages, {
    today,
    dryRun,
    packageFilter:
      packageFilter !== undefined && packageFilter.length > 0
        ? packageFilter
        : undefined,
  });

  // -------------------------------------------------------------------------
  // Report
  // -------------------------------------------------------------------------

  if (result.plans.length === 0) {
    console.log(
      `No consumable packages have qualifying commits in ${against}..HEAD.`,
    );
  } else {
    const mode = result.applied ? "APPLIED" : "DRY-RUN";
    console.log(`\nRelease runner — ${mode} (baseline: ${against})\n`);

    for (const plan of result.plans) {
      console.log(
        `  ${plan.pkg.name} (${plan.pkg.ecosystem})  ${plan.pkg.currentVersion} → ${plan.newVersion}  [${plan.bump}]`,
      );

      if (plan.wireBreakingEntries.length > 0) {
        for (const e of plan.wireBreakingEntries)
          console.log(`    Wire-breaking: ${e}`);
      }

      if (plan.apiBreakingEntries.length > 0) {
        for (const e of plan.apiBreakingEntries)
          console.log(`    API-breaking: ${e}`);
      }

      if (plan.addedEntries.length > 0) {
        for (const e of plan.addedEntries) console.log(`    Added: ${e}`);
      }

      if (plan.fixedEntries.length > 0) {
        for (const e of plan.fixedEntries) console.log(`    Fixed: ${e}`);
      }
    }

    console.log(
      result.applied
        ? `\n${result.plans.length.toString()} package(s) bumped.`
        : `\n${result.plans.length.toString()} package(s) would be bumped (dry-run — pass --apply to write).`,
    );
  }
}
