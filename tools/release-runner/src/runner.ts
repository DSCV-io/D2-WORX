// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Release runner orchestrator.
//
// Coordinates: git adapter → bump engine → manifest editor → changelog editor.
//
// Injectable design: the git adapter and today-date are supplied as parameters
// so the unit tests drive the runner with synthetic commit histories and fixed
// dates — no real git or filesystem IO required in tests.

import { falsey } from "@d2/utilities";
import { computeBumpPlans } from "./bump-engine.js";
import { propagateBumps } from "./dependency-graph.js";
import { writeManifestVersion } from "./manifest-editor.js";
import { promoteChangelog } from "./changelog-editor.js";
import type {
  BumpPlan,
  CommitRecord,
  PackageDescriptor,
  RunnerOptions,
} from "./types.js";

// ---------------------------------------------------------------------------
// Runner result
// ---------------------------------------------------------------------------

/**
 * The outcome of a single runner invocation.
 *
 * In dry-run mode, `applied` is always false and no files are written.
 */
export interface RunnerResult {
  /** Plans that were computed (all packages with qualifying commits). */
  readonly plans: readonly BumpPlan[];
  /** True when changes were actually written to disk (not dry-run). */
  readonly applied: boolean;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Run the release runner for a set of commits and packages.
 *
 * @param commits  - Commit records supplying messages + touched file paths.
 *                   Injectable for unit tests; the real git adapter provides
 *                   these in production.
 * @param packages - Consumable package inventory. May be pre-filtered by the
 *                   caller (e.g. single-package run).
 * @param options  - Runner options (dryRun, packageFilter, today).
 * @returns RunnerResult with the computed plans and applied flag.
 */
export function runRelease(
  commits: readonly CommitRecord[],
  packages: readonly PackageDescriptor[],
  options: RunnerOptions,
): RunnerResult {
  // Compute direct plans over the FULL package set so the dependency graph
  // can resolve all edges. The package filter is applied AFTER propagation.
  const directPlans = computeBumpPlans(commits, packages);

  // Propagate bumps to transitive dependents (default-on; opt-out via
  // RunnerOptions.propagate = false / CLI --no-propagate).
  const allPlans =
    options.propagate && directPlans.length > 0
      ? propagateBumps(directPlans, packages)
      : directPlans;

  // Apply the single-package filter AFTER propagation so --package X still
  // shows X even when X was only reached via propagation.
  const plans =
    options.packageFilter !== undefined
      ? allPlans.filter((p) => p.pkg.name === options.packageFilter)
      : allPlans;

  if (options.dryRun || falsey(plans)) {
    return { plans, applied: false };
  }

  // Apply mode: write each plan's version + changelog.
  for (const plan of plans) {
    writeManifestVersion(plan.pkg.manifestPath, plan.newVersion);
    promoteChangelog(plan.pkg.changelogPath, plan, options.today);
  }

  return { plans, applied: true };
}
