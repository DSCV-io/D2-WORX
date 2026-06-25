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
//                       env var. Error if neither is provided (required for all
//                       modes except --list).
//   --package <name>    Restrict to a single package
//   --dry-run           Compute and report without writing (default: true)
//   --apply             Write bumps + changelogs (disables dry-run). Mutually
//                       exclusive with --list.
//   --graduate <name>   Graduate a pre-stable package from 0.x.y to 1.0.0.
//                       Mutually exclusive with --list.
//   --list              Print the full consumable package inventory as JSON and
//                       exit. Read-only — writes nothing. Mutually exclusive
//                       with --apply / --graduate. Does not require --against.
//   --help, -h          Print this help message and exit.
//
// Excluded from the unit-coverage threshold (see vitest.config.ts).

import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { falsey, truthy } from "@d2/utilities";
import { resolveBaseline } from "./baseline.js";
import { validateGitRef } from "contract-gate";
import { commitsInRange } from "./git-adapter.js";
import { loadAllPackages } from "./manifest-loader.js";
import { formatPackageList } from "./list-formatter.js";
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
// Help text
// ---------------------------------------------------------------------------

const HELP_TEXT = `\
release-runner — per-package semver release automation

Usage:
  pnpm --filter release-runner exec tsx src/cli.ts [options]

Options:
  --against <ref>    Baseline git ref (required unless --list; or set
                     D2_RELEASE_BASELINE env var)
  --package <name>   Restrict bump computation to a single package.
                     Filter is applied after propagation so --package X
                     shows X even when reached only via dependency-update.
  --dry-run          Compute and print plans without writing (default)
  --apply            Write version bumps and changelogs to disk
  --graduate <name>  Graduate a pre-stable package from 0.x.y to 1.0.0
  --list             Print consumable package inventory as JSON and exit.
                     Read-only; mutually exclusive with --apply / --graduate.
  --no-propagate     Disable dependency-update propagation. By default a bump
                     to any package also PATCH-bumps its transitive consumable
                     dependents. Pass this flag to restrict to direct bumps only.
  --help, -h         Print this help message and exit

Exit codes:
  0  Success.
  1  No packages found (--list), or invalid arguments / runtime error.
`;

// ---------------------------------------------------------------------------
// --help mode
// ---------------------------------------------------------------------------

if (flag("--help") || flag("-h")) {
  process.stdout.write(HELP_TEXT);
  process.exit(0);
}

// ---------------------------------------------------------------------------
// Mutual exclusion: --list is incompatible with --apply / --graduate
// ---------------------------------------------------------------------------

if (flag("--list") && flag("--apply")) {
  process.stderr.write(
    "[release-runner] error: --list and --apply are mutually exclusive" +
      " — --list is read-only and writes nothing.\n",
  );
  process.exit(1);
}

if (flag("--list") && flag("--graduate")) {
  process.stderr.write(
    "[release-runner] error: --list and --graduate are mutually exclusive" +
      " — --list is read-only and writes nothing.\n",
  );
  process.exit(1);
}

// ---------------------------------------------------------------------------
// --list mode — read-only inventory emit
// ---------------------------------------------------------------------------

if (flag("--list")) {
  const packages = loadAllPackages(repoRoot);

  if (falsey(packages)) {
    console.error(
      "error: --list found no consumable packages in the repo tree.",
    );
    process.exit(1);
  }

  process.stdout.write(formatPackageList(packages));
  process.exit(0);
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

try {
  validateGitRef(against);
} catch (err) {
  console.error(
    `[release-runner] error: invalid baseline ref — ${String(err)}`,
  );
  process.exit(1);
}

const packageFilter = flag("--package") ? option("--package", "") : undefined;
const dryRun = !flag("--apply");
const propagate = !flag("--no-propagate");
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

if (truthy(graduateTarget)) {
  const result = graduatePackage(graduateTarget!, packages, today, dryRun);
  const mode = result.applied ? "APPLIED" : "DRY-RUN";

  console.log(
    `\nGraduate — ${mode}: ${result.pkg.name}` +
      ` ${result.pkg.currentVersion} → ${result.newVersion}`,
  );

  if (!result.applied) {
    console.log(`(dry-run — pass --apply to write)`);
  }
} else {
  const commits = commitsInRange(against, "HEAD");

  const result = runRelease(commits, packages, {
    today,
    dryRun,
    packageFilter: truthy(packageFilter) ? packageFilter : undefined,
    propagate,
  });

  // -------------------------------------------------------------------------
  // Report
  // -------------------------------------------------------------------------

  if (falsey(result.plans)) {
    console.log(
      `No consumable packages have qualifying commits in ${against}..HEAD.`,
    );
  } else {
    const mode = result.applied ? "APPLIED" : "DRY-RUN";
    console.log(`\nRelease runner — ${mode} (baseline: ${against})\n`);

    for (const plan of result.plans) {
      const isDependencyUpdate =
        plan.dependencyEntries.length > 0 &&
        plan.wireBreakingEntries.length === 0 &&
        plan.apiBreakingEntries.length === 0 &&
        plan.addedEntries.length === 0 &&
        plan.fixedEntries.length === 0;

      const tag = isDependencyUpdate
        ? `[${plan.bump}][dependency-update]`
        : `[${plan.bump}]`;

      console.log(
        `  ${plan.pkg.name} (${plan.pkg.ecosystem})` +
          `  ${plan.pkg.currentVersion} → ${plan.newVersion}  ${tag}`,
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

      if (plan.dependencyEntries.length > 0) {
        for (const e of plan.dependencyEntries)
          console.log(`    Dependency update: ${e}`);
      }

      if (plan.fixedEntries.length > 0) {
        for (const e of plan.fixedEntries) console.log(`    Fixed: ${e}`);
      }
    }

    console.log(
      result.applied
        ? `\n${result.plans.length.toString()} package(s) bumped.`
        : `\n${result.plans.length.toString()} package(s) would be bumped` +
            ` (dry-run — pass --apply to write).`,
    );
  }
}
