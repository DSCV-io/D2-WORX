// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Baseline-drift checker — proves every committed per-package baseline still
// matches the package's current source, so a stale baseline cannot merge.
//
// The drift check IS the production DiffProvider run with the resolved-version
// map seeded to each package's current version (no-op PR — no dep bumps): drift
// ⇔ any package shows a non-empty diff against its own committed baseline on a
// no-op PR. Re-using `makeRealDiffProvider`
// (not a second extraction code path) keeps ONE extraction implementation —
// the drift check validates the exact provider the CLI now uses for releases.
//
// Coverage by ecosystem (the source-based fingerprint is portable, so a
// recompute on any host matches the committed baseline by construction):
//   - .NET API drift  — owned by the Build lane (RS0016/RS0017 → build error
//                       under TreatWarningsAsErrors); the drift check still
//                       re-derives it via the provider's git-ref report diff,
//                       and it ALSO recomputes the source-based
//                       .release-fingerprint, which the Build lane does not check.
//   - TS API drift    — the `.api.md` report diff (no build-time analog); the CI
//                       lane additionally runs production-mode api-extractor as a
//                       second currency guard.
//   - TS/.NET output  — the source-based fingerprint recompute catches a
//                       source/dep/toolchain change not reflected in the API report.
//
// Failure semantics: collect ALL drifted packages (not first-fail), print a
// table, and exit non-zero if any drifted. Fail-loud per the strict convention.
//
// This module NEVER writes a committed baseline — it is a read-only compare. The
// baselines are regenerated only by the seed script (the generator), never here.

import { falsey, truthy } from "@dcsv-io/d2-utilities";
import type { DiffProvider } from "./diff-runner.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Result types
// ---------------------------------------------------------------------------

/**
 * The drift verdict for a single package.
 */
export interface PackageDriftResult {
  readonly name: string;
  readonly ecosystem: string;
  /** True when the API surface drifted from the committed baseline. */
  readonly apiDrift: boolean;
  /** True when the output fingerprint drifted from the committed baseline. */
  readonly fingerprintDrift: boolean;
  /** True when no committed baseline exists for the package. */
  readonly baselineMissing: boolean;
  /** True when ANY of the above signals a drift. */
  readonly drifted: boolean;
  /** Human-readable detail (which signals drifted). */
  readonly detail: string;
}

/**
 * The aggregate drift-check result across all packages.
 */
export interface DriftCheckResult {
  readonly results: readonly PackageDriftResult[];
  /** The subset of `results` that drifted. */
  readonly drifted: readonly PackageDriftResult[];
  /** True when no package drifted. */
  readonly clean: boolean;
}

// ---------------------------------------------------------------------------
// Core — checkBaselineDrift (pure-ish over an injected DiffProvider)
// ---------------------------------------------------------------------------

/**
 * Check every package's committed baseline against its current source via the
 * injected DiffProvider, with the resolved-version map seeded to each package's
 * current version (no-op PR — no dep bumps in flight) so a fingerprint diff
 * reflects only the package's own output, not a propagated dep bump.
 *
 * A package drifts when the provider reports any of: apiDiff.added/removed/changed,
 * fingerprintDiff.changed, or baselineMissing — on a no-op (no commits, no footer).
 *
 * @param packages     - The consumable package inventory.
 * @param diffProvider - The DiffProvider to run each package through.
 * @returns The aggregate drift result.
 */
export function checkBaselineDrift(
  packages: readonly PackageDescriptor[],
  diffProvider: DiffProvider,
): DriftCheckResult {
  if (falsey(packages)) {
    return { results: [], drifted: [], clean: true };
  }

  // Seed the resolved-version map with each package's CURRENT version (no bump
  // this run — a drift check is a no-op PR). This MUST mirror how the seed
  // computed the baselines (each dep at its committed version) so a no-op
  // recompute matches the committed fingerprint; an empty map would make every
  // dependency version read as "" and false-fail every package with a dep.
  const resolvedVersions = new Map<string, string>(
    packages.map((p) => [p.name, p.currentVersion]),
  );

  const results: PackageDriftResult[] = [];

  for (const pkg of packages) {
    const diff = diffProvider.getDiff({ pkg, resolvedVersions });

    const apiDrift =
      diff.apiDiff.added || diff.apiDiff.removed || diff.apiDiff.changed;
    const fingerprintDrift = diff.fingerprintDiff.changed;
    const baselineMissing = diff.baselineMissing;
    const drifted = apiDrift || fingerprintDrift || baselineMissing;

    const detailParts: string[] = [];

    if (baselineMissing) detailParts.push("baseline missing");
    if (apiDrift) {
      const axes: string[] = [];

      if (diff.apiDiff.added) axes.push("added");
      if (diff.apiDiff.removed) axes.push("removed");
      if (diff.apiDiff.changed) axes.push("changed");

      detailParts.push(`api: ${axes.join("+")}`);
    }

    if (fingerprintDrift) detailParts.push("fingerprint changed");

    results.push({
      name: pkg.name,
      ecosystem: pkg.ecosystem,
      apiDrift,
      fingerprintDrift,
      baselineMissing,
      drifted,
      detail: truthy(detailParts) ? detailParts.join("; ") : "ok",
    });
  }

  const drifted = results.filter((r) => r.drifted);

  return { results, drifted, clean: falsey(drifted) };
}

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

/**
 * Render a drift-check result as a human-readable table for CI logs.
 *
 * @param result - The aggregate drift-check result.
 * @returns A multi-line report string (no trailing process control).
 */
export function formatDriftReport(result: DriftCheckResult): string {
  const lines: string[] = [];

  lines.push("Baseline drift check");
  lines.push("");

  if (result.clean) {
    lines.push(
      `All ${result.results.length.toString()} package baselines are current — no drift.`,
    );

    return lines.join("\n") + "\n";
  }

  lines.push(
    `DRIFT DETECTED in ${result.drifted.length.toString()} package(s):`,
  );
  lines.push("");
  lines.push("  package | ecosystem | detail");
  lines.push("  ------- | --------- | ------");

  for (const r of result.drifted) {
    lines.push(`  ${r.name} | ${r.ecosystem} | ${r.detail}`);
  }

  lines.push("");
  lines.push(
    "A drifted baseline means a package's committed PublicAPI / .api.md / " +
      "fingerprint no longer matches its source. Re-seed the baselines " +
      "(node public/tools/scripts/seed-publicapi-baselines.mjs / " +
      "node public/tools/scripts/seed-apiextractor-baselines.mjs) and bump the package " +
      "version in the same PR.",
  );

  return lines.join("\n") + "\n";
}
