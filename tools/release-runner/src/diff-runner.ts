// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Artifact-diff release runner — bump flow driven by the artifact diff engine.
//
// This module replaces the commit-type bump source with deriveBump(), which
// derives the semver bump from three orthogonal signals:
//
//   1. ApiDiff       — public API surface changes vs committed baseline.
//   2. FingerprintDiff — compiled-output/manifest fingerprint change.
//   3. BreakingFooter  — author-declared override (escalate-only).
//
// The commit TYPE is demoted to CHANGELOG CATEGORY only (feat → Added,
// fix/perf → Fixed). The diff is the source of truth for the bump magnitude.
//
// Processing order:
//   Packages are processed in topological (leaf-first) order so a dep's new
//   version is resolved before its dependent is fingerprinted. This makes
//   dependency-version-change propagation fall out naturally from the manifest
//   fingerprint: a dep bump rewrites the dep's version in the in-memory
//   resolved-version map → the dependent's manifest-metadata fingerprint input
//   changes → `fingerprintDiff.changed = true` → floors at PATCH. No separate
//   BFS propagation pass is needed.
//
// Missing baseline (no committed .release-fingerprint / .api.md / Shipped.txt):
//   Treated as fingerprintDiff.changed=true, apiDiff all-false → floors at PATCH.
//   The caller should seed the baseline after the first run. The engine never
//   crashes on a missing baseline — it records a PATCH bump and continues.
//
// --no-propagate:
//   Under the diff model, propagation is inherent in the fingerprint. The
//   propagate flag is accepted for API compatibility but is advisory-only: the
//   DiffProvider receives the resolved-version map so it can reflect dep-version
//   changes in the fingerprint. Passing propagate:false suppresses the resolved-
//   version forwarding — dependents are not given the dep's new version when
//   computing their fingerprint, so no propagation occurs.
//
// Injectable design: DiffProvider isolates all real build/extract IO so tests
// can inject synthetic diffs for every transition without spawning child
// processes or requiring baseline files.
//
// Cycle guard: topoSort() throws when a cycle is detected (a cycle makes
// leaf-first processing indeterminate — release over a cyclic graph is an
// error condition that must be surfaced, not silently continued).

import { falsey, truthy } from "@d2/utilities";
import { parseBreakingFooters } from "contract-gate";
import { deriveBump } from "./diff-bump.js";
import { topoSort } from "./dependency-graph.js";
import { writeManifestVersion } from "./manifest-editor.js";
import { promoteChangelog } from "./changelog-editor.js";
import { parseVersion, applyBump } from "./semver.js";
import type { ApiDiff, FingerprintDiff, BreakingFooter } from "./diff-bump.js";
import type { BumpPlan, PackageDescriptor, RunnerOptions } from "./types.js";

// ---------------------------------------------------------------------------
// DiffProvider — injectable seam for per-package artifact extraction
// ---------------------------------------------------------------------------

/**
 * Input context supplied to the DiffProvider for a single package.
 *
 * `resolvedVersions` maps package name → its new version string for any
 * package already processed in the current run. The DiffProvider uses this
 * to compute a manifest fingerprint that reflects dep-version changes so
 * propagation falls out of the fingerprint without a separate BFS pass.
 */
export interface DiffProviderInput {
  readonly pkg: PackageDescriptor;
  /** In-memory resolved versions for packages already bumped this run. */
  readonly resolvedVersions: ReadonlyMap<string, string>;
}

/**
 * The combined diff result for a single package from the DiffProvider.
 *
 * When `baselineMissing` is true, the engine treats the package as a
 * first-run seed (fingerprintDiff.changed=true, apiDiff all-false) and
 * records a PATCH bump so a baseline can be established.
 */
export interface PackageDiff {
  readonly apiDiff: ApiDiff;
  readonly fingerprintDiff: FingerprintDiff;
  /**
   * True when no committed baseline exists for this package yet.
   * The engine handles this gracefully — PATCH bump, no crash.
   */
  readonly baselineMissing: boolean;
}

/**
 * Seam that supplies per-package ApiDiff + FingerprintDiff.
 *
 * The real implementation (`makeRealDiffProvider`) derives the apiDiff from a
 * git-ref text diff of the committed API report and the FingerprintDiff from a
 * source-based fingerprint recompute, dispatching per `pkg.ecosystem`. Tests
 * inject a synthetic implementation that returns pre-canned diffs.
 */
export interface DiffProvider {
  /**
   * Compute the ApiDiff and FingerprintDiff for `input.pkg`.
   *
   * May throw if the build fails for a reason other than API-diff diagnostics.
   * The runner propagates the error (release cannot proceed on a build failure).
   *
   * @param input - Package descriptor and resolved-version context.
   * @returns PackageDiff for the package.
   */
  getDiff(input: DiffProviderInput): PackageDiff;
}

// ---------------------------------------------------------------------------
// Changelog-category derivation from commit type (advisory, not bump source)
// ---------------------------------------------------------------------------

const COMMIT_TYPE_RE = /^([a-zA-Z]+)(?:\([^)]*\))?!?\s*:/;

type CommitKind = "feat" | "fix" | "perf" | "other";

function classifyCommitType(message: string): CommitKind {
  const nlIdx = message.indexOf("\n");
  const subject = (nlIdx === -1 ? message : message.slice(0, nlIdx)).trimEnd();
  const match = COMMIT_TYPE_RE.exec(subject);

  if (match === null) return "other";

  const typeWord = match[1]!.toLowerCase();

  if (typeWord === "feat") return "feat";
  if (typeWord === "fix") return "fix";
  if (typeWord === "perf") return "perf";

  return "other";
}

function extractSubjectDescription(message: string): string {
  const nlIdx = message.indexOf("\n");
  const subject = (nlIdx === -1 ? message : message.slice(0, nlIdx)).trimEnd();
  const colonIdx = subject.indexOf(":");

  if (colonIdx === -1) return subject.trim();

  return subject.slice(colonIdx + 1).trim();
}

// ---------------------------------------------------------------------------
// Path-containment index (reused from bump-engine)
// ---------------------------------------------------------------------------

function buildDirIndex(
  packages: readonly PackageDescriptor[],
): Map<string, PackageDescriptor> {
  const index = new Map<string, PackageDescriptor>();

  for (const pkg of packages) {
    const key = pkg.dir.replace(/\\/g, "/").toLowerCase();
    index.set(key, pkg);
  }

  return index;
}

function findPackageForFile(
  filePath: string,
  dirIndex: Map<string, PackageDescriptor>,
): PackageDescriptor | undefined {
  const norm = filePath.replace(/\\/g, "/").toLowerCase();
  let best: PackageDescriptor | undefined;
  let bestLen = -1;

  for (const [dir, pkg] of dirIndex) {
    if ((norm === dir || norm.startsWith(dir + "/")) && dir.length > bestLen) {
      best = pkg;
      bestLen = dir.length;
    }
  }

  return best;
}

// ---------------------------------------------------------------------------
// Changelog-entry accumulator (per-package)
// ---------------------------------------------------------------------------

interface ChangelogEntries {
  wireBreakingEntries: string[];
  apiBreakingEntries: string[];
  addedEntries: string[];
  fixedEntries: string[];
}

function emptyEntries(): ChangelogEntries {
  return {
    wireBreakingEntries: [],
    apiBreakingEntries: [],
    addedEntries: [],
    fixedEntries: [],
  };
}

// ---------------------------------------------------------------------------
// Footer aggregation for a package's commit set
// ---------------------------------------------------------------------------

function aggregateFooter(messages: readonly string[]): BreakingFooter {
  if (falsey(messages)) {
    return { forced: false, wireBreaking: [], apiBreaking: [] };
  }

  const parsed = parseBreakingFooters([...messages]);
  return {
    forced: parsed.forced,
    wireBreaking: parsed.wireBreaking,
    apiBreaking: parsed.apiBreaking,
  };
}

// ---------------------------------------------------------------------------
// Runner result
// ---------------------------------------------------------------------------

/**
 * The outcome of a single diff-driven runner invocation.
 */
export interface DiffRunnerResult {
  /** Plans computed (packages with a non-none bump). */
  readonly plans: readonly BumpPlan[];
  /** True when changes were written to disk (not dry-run). */
  readonly applied: boolean;
  /** Warning messages for non-fatal conditions (e.g. missing baseline). */
  readonly warnings: readonly string[];
}

// ---------------------------------------------------------------------------
// Public API — runDiffRelease
// ---------------------------------------------------------------------------

/**
 * Run the artifact-diff release pass for a set of commits and packages.
 *
 * Derives per-package semver bumps from artifact diffs (via DiffProvider) in
 * topological (leaf-first) order. The commit TYPE drives changelog categories
 * only; the diff is the bump source of truth.
 *
 * Propagation via fingerprint: when a dep bumps, its new version is written
 * into the in-memory resolved-version map passed to subsequent DiffProvider
 * calls. The DiffProvider is responsible for reflecting that version change in
 * the FingerprintDiff (manifest-metadata fingerprint input). If the dep version
 * changed, the dependent's manifest fingerprint changes → floors at PATCH.
 * This is the propagation mechanism — no separate BFS pass.
 *
 * When `options.propagate` is false, the resolved-version map is NOT forwarded
 * to dependent DiffProvider calls, so no propagation occurs.
 *
 * @param commits      - Commit records supplying messages + touched file paths.
 * @param packages     - Full consumable package inventory.
 * @param options      - Runner options (dryRun, packageFilter, today, propagate).
 * @param diffProvider - Injectable seam for per-package diff extraction.
 * @returns DiffRunnerResult with plans, applied flag, and any warnings.
 * @throws {Error} When a dependency cycle is detected (topoSort fails loud).
 * @throws {Error} When a build fails for a reason other than API diagnostics.
 */
export function runDiffRelease(
  commits: readonly import("./types.js").CommitRecord[],
  packages: readonly PackageDescriptor[],
  options: RunnerOptions,
  diffProvider: DiffProvider,
): DiffRunnerResult {
  if (falsey(packages)) return { plans: [], applied: false, warnings: [] };

  // --- Step 1: build commit→package index ---------------------------------

  const dirIndex = buildDirIndex(packages);

  // Map: package name → array of commit messages touching that package.
  const pkgMessages = new Map<string, string[]>();
  // Map: package name → changelog entries from those commits.
  const pkgEntries = new Map<string, ChangelogEntries>();

  for (const commit of commits) {
    const kind = classifyCommitType(commit.message);
    const desc = extractSubjectDescription(commit.message);

    const touched = new Set<PackageDescriptor>();

    for (const file of commit.files) {
      const pkg = findPackageForFile(file, dirIndex);

      if (pkg !== undefined) touched.add(pkg);
    }

    for (const pkg of touched) {
      const msgs = pkgMessages.get(pkg.name) ?? [];
      msgs.push(commit.message);
      pkgMessages.set(pkg.name, msgs);

      const entries = pkgEntries.get(pkg.name) ?? emptyEntries();

      if (kind === "feat" && truthy(desc)) entries.addedEntries.push(desc);
      if ((kind === "fix" || kind === "perf") && truthy(desc))
        entries.fixedEntries.push(desc);

      pkgEntries.set(pkg.name, entries);
    }
  }

  // --- Step 2: topological sort (leaf-first) — throws on cycle ------------

  const orderedPackages = topoSort(packages);

  // --- Step 3: per-package diff → bump, in topo order -------------------

  // Resolved-version map: updated as each package is bumped.
  // Forwarded to the DiffProvider when propagate:true so dep-version changes
  // are reflected in the dependent's manifest fingerprint.
  const resolvedVersions = new Map<string, string>(
    packages.map((p) => [p.name, p.currentVersion]),
  );

  const plans: BumpPlan[] = [];
  const warnings: string[] = [];

  for (const pkg of orderedPackages) {
    const messages = pkgMessages.get(pkg.name) ?? [];
    const entries = pkgEntries.get(pkg.name) ?? emptyEntries();

    // Aggregate breaking footer from all commits touching this package.
    const footer = aggregateFooter(messages);

    // Wire the footer's breaking entries into the changelog accumulator.
    for (const wireDesc of footer.wireBreaking) {
      if (!entries.wireBreakingEntries.includes(wireDesc))
        entries.wireBreakingEntries.push(wireDesc);
    }

    for (const apiDesc of footer.apiBreaking) {
      if (!entries.apiBreakingEntries.includes(apiDesc))
        entries.apiBreakingEntries.push(apiDesc);
    }

    // Extract the diff for this package.
    // Pass the current resolved-version map only when propagate:true.
    const diffInput: DiffProviderInput = {
      pkg,
      resolvedVersions: options.propagate
        ? new Map(resolvedVersions)
        : new Map(packages.map((p) => [p.name, p.currentVersion])),
    };

    const diff = diffProvider.getDiff(diffInput);

    if (diff.baselineMissing) {
      warnings.push(
        `[release-runner] baseline missing for ${pkg.name} — ` +
          `treating as first run (PATCH floor). Seed baselines with the seed command.`,
      );
    }

    // Derive the bump from the diff + footer.
    const bump = deriveBump({
      apiDiff: diff.apiDiff,
      fingerprintDiff: diff.fingerprintDiff,
      currentVersion: pkg.currentVersion,
      footer,
    });

    if (bump === "none") continue;

    const parsed = parseVersion(pkg.currentVersion);
    const newVersion = applyBump(parsed, bump);

    // Update the resolved-version map so dependents see the new version
    // in their fingerprint computation (propagation-via-fingerprint).
    resolvedVersions.set(pkg.name, newVersion);

    // Is this purely a dependency-update bump (no direct commits, no footer)?
    // Determine by checking whether the bump came ONLY from propagation
    // (fingerprintDiff.changed=true driven by a dep-version change, no msgs).
    const hasDirect = messages.length > 0 || footer.forced;

    const plan: BumpPlan = {
      pkg,
      bump,
      newVersion,
      wireBreakingEntries: entries.wireBreakingEntries,
      apiBreakingEntries: entries.apiBreakingEntries,
      addedEntries: entries.addedEntries,
      fixedEntries: entries.fixedEntries,
      // dependencyEntries carries the names of direct dep bumps when this
      // is a pure propagation bump (no direct commits to this package).
      dependencyEntries: hasDirect
        ? []
        : [...resolvedVersions.entries()]
            .filter(([name, ver]) => {
              const depPkg = packages.find((p) => p.name === name);
              return (
                depPkg !== undefined &&
                pkg.dependencies.includes(name) &&
                ver !== depPkg.currentVersion
              );
            })
            .map(([name]) => name),
    };

    plans.push(plan);
  }

  // --- Step 4: apply package filter ---------------------------------------

  const filteredPlans =
    options.packageFilter !== undefined
      ? plans.filter((p) => p.pkg.name === options.packageFilter)
      : plans;

  if (options.dryRun || falsey(filteredPlans)) {
    return { plans: filteredPlans, applied: false, warnings };
  }

  // --- Step 5: apply mode — write manifests + changelogs -----------------

  for (const plan of filteredPlans) {
    writeManifestVersion(plan.pkg.manifestPath, plan.newVersion);
    promoteChangelog(plan.pkg.changelogPath, plan, options.today);
  }

  return { plans: filteredPlans, applied: true, warnings };
}
