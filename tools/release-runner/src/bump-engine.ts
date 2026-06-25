// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Bump-severity engine — maps commits to per-package bump plans.
//
// Design contract:
//
//   • No git IO — all inputs are supplied by the caller. Inject synthetic
//     CommitRecord arrays in unit tests; the git adapter supplies the real
//     ones in production.
//
//   • One signal drives gate + runner: `parseBreakingFooters` (contract-gate)
//     is the single authority on whether a commit carries a breaking-change
//     footer. The runner and the gate cannot disagree.
//
// Bump-severity table (per ADR-0024 §2.5):
//
//   Change type                | Pre-stable (MAJOR === 0) | Stable (MAJOR ≥ 1)
//   WIRE-BREAKING: footer      | MINOR                    | MAJOR *
//   BREAKING CHANGE: footer    | MINOR                    | MAJOR *
//   feat: commit type          | MINOR                    | MINOR
//   fix: / perf: commit type   | PATCH                    | PATCH
//
//   * Stable break requires the force valve (the footer IS the valve).
//     A breaking footer on a stable package is OK — the gate already
//     required the footer. The runner validates independently: if a caller
//     signals "this commit is breaking" (via valve.forced === true) for a
//     stable package, it's valid. If the caller wants to assert "break
//     without valve" (valve.forced === false), the runner throws via
//     `computeBumpPlans`'s `stableBreakRequired` guard — injectable via
//     the optional `breakingSignalProvider` for tests.
//
// Path-containment: commit → packages via dir-prefix. Longest prefix wins.
// SourceGen shells are non-consumable — their parent consumable is the host.
// Non-consumable paths (services, docs, tooling): no bump attributed.

import { parseBreakingFooters } from "contract-gate";
import { falsey } from "@d2/utilities";
import { parseVersion, applyBump } from "./semver.js";
import type {
  BumpKind,
  BumpPlan,
  CommitRecord,
  PackageDescriptor,
} from "./types.js";

// ---------------------------------------------------------------------------
// Injectable breaking-signal provider (for the stable-break error path)
// ---------------------------------------------------------------------------

/**
 * A breaking-signal result for a single commit. Normally derived from
 * `parseBreakingFooters`; injectable in tests to simulate "forced=false with
 * break intent" (the structurally-impossible-in-production error path).
 */
export interface BreakingSignal {
  readonly forced: boolean;
  readonly wireBreaking: readonly string[];
  readonly apiBreaking: readonly string[];
}

/**
 * Provider that maps a commit message to its breaking signals.
 *
 * Default: `(msg) => parseBreakingFooters([msg])`.
 * Override in tests to inject synthetic forced/signal combinations.
 */
export type BreakingSignalProvider = (message: string) => BreakingSignal;

// ---------------------------------------------------------------------------
// Conventional-commit type extraction
// ---------------------------------------------------------------------------

// Subject-line type prefix: group 1 = type word, optional scope+! before ":".
const COMMIT_TYPE_RE = /^([a-zA-Z]+)(?:\([^)]*\))?!?\s*:/;

type CommitKind = "feat" | "fix" | "perf" | "other";

function classifyCommitType(message: string): CommitKind {
  // Extract subject line without array indexing (avoids noUncheckedIndexedAccess branches).
  const nlIdx = message.indexOf("\n");
  const subject = (nlIdx === -1 ? message : message.slice(0, nlIdx)).trimEnd();
  const match = COMMIT_TYPE_RE.exec(subject);

  if (match === null) return "other";

  // Group 1 is always present when the regex matches (required `[a-zA-Z]+` group).
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
// Path-containment index
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

/**
 * Return the PackageDescriptor whose `dir` is the LONGEST prefix of
 * `filePath`, or `undefined` if no package claims the path.
 */
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
// Bump-level combiner
// ---------------------------------------------------------------------------

function maxBump(a: BumpKind, b: BumpKind): BumpKind {
  const rank: Record<BumpKind, number> = {
    none: 0,
    patch: 1,
    minor: 2,
    major: 3,
  };

  return rank[a] >= rank[b] ? a : b;
}

// ---------------------------------------------------------------------------
// Per-package accumulator (internal)
// ---------------------------------------------------------------------------

interface PackageAccumulator {
  bump: BumpKind;
  wireBreakingEntries: string[];
  apiBreakingEntries: string[];
  addedEntries: string[];
  fixedEntries: string[];
}

function emptyAccumulator(): PackageAccumulator {
  return {
    bump: "none",
    wireBreakingEntries: [],
    apiBreakingEntries: [],
    addedEntries: [],
    fixedEntries: [],
  };
}

// ---------------------------------------------------------------------------
// Default breaking-signal provider
// ---------------------------------------------------------------------------

const defaultSignalProvider: BreakingSignalProvider = (msg) =>
  parseBreakingFooters([msg]);

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Compute per-package bump plans from a set of commit records.
 *
 * Injectable design — no git IO; `commits` and `packages` are supplied by
 * the caller. Inject synthetic values in unit tests.
 *
 * @param commits         - Commit records (message + touched file paths).
 * @param packages        - Consumable package inventory.
 * @param signalProvider  - Optional breaking-signal provider. Defaults to
 *                          `parseBreakingFooters`. Override in tests to
 *                          simulate "forced=false with breaking intent" for
 *                          stable-package error-path coverage.
 * @returns BumpPlan[] — one entry per package with at least one qualifying
 *          commit. Packages with no qualifying commits are omitted.
 * @throws {Error} When a stable (MAJOR ≥ 1) package is touched by a commit
 *                 that signals breaking intent (`wireBreaking` or `apiBreaking`
 *                 non-empty) but `forced === false`. This is the fail-loud
 *                 guard for "break without valve".
 */
export function computeBumpPlans(
  commits: readonly CommitRecord[],
  packages: readonly PackageDescriptor[],
  signalProvider: BreakingSignalProvider = defaultSignalProvider,
): BumpPlan[] {
  if (falsey(commits) || falsey(packages)) return [];

  const dirIndex = buildDirIndex(packages);
  const accs = new Map<string, PackageAccumulator>();

  for (const commit of commits) {
    const signal = signalProvider(commit.message);
    const kind = classifyCommitType(commit.message);
    const desc = extractSubjectDescription(commit.message);

    // Determine which consumable packages this commit touches.
    const touched = new Set<PackageDescriptor>();

    for (const file of commit.files) {
      const pkg = findPackageForFile(file, dirIndex);

      if (pkg !== undefined) touched.add(pkg);
    }

    if (touched.size === 0) continue;

    // Fail-loud check: if the signal claims breaking entries exist but forced
    // is false, that is a contradictory state — fail for every stable package
    // this commit touches.
    const hasBreakEntries =
      signal.wireBreaking.length > 0 || signal.apiBreaking.length > 0;

    if (hasBreakEntries && !signal.forced) {
      for (const pkg of touched) {
        const parsed = parseVersion(pkg.currentVersion);

        if (parsed.major >= 1) {
          throw new Error(
            `Breaking change on stable package "${pkg.name}" (v${pkg.currentVersion}) ` +
              `without the force valve. Add a WIRE-BREAKING: or BREAKING CHANGE: footer.`,
          );
        }
      }
    }

    for (const pkg of touched) {
      const parsed = parseVersion(pkg.currentVersion);
      const isPreStable = parsed.major === 0;

      if (signal.forced) {
        // Breaking-signal commit: bump by break level.
        const bumpLevel: BumpKind = isPreStable ? "minor" : "major";
        const acc = accs.get(pkg.name) ?? emptyAccumulator();

        acc.bump = maxBump(acc.bump, bumpLevel);

        for (const wireDesc of signal.wireBreaking) {
          if (!acc.wireBreakingEntries.includes(wireDesc))
            acc.wireBreakingEntries.push(wireDesc);
        }

        for (const apiDesc of signal.apiBreaking) {
          if (!acc.apiBreakingEntries.includes(apiDesc))
            acc.apiBreakingEntries.push(apiDesc);
        }

        accs.set(pkg.name, acc);
      } else {
        // Non-breaking commit: bump by conventional-commit type.
        const bumpLevel: BumpKind =
          kind === "feat"
            ? "minor"
            : kind === "fix" || kind === "perf"
              ? "patch"
              : "none";

        if (bumpLevel === "none") continue;

        const acc = accs.get(pkg.name) ?? emptyAccumulator();

        acc.bump = maxBump(acc.bump, bumpLevel);

        if (kind === "feat" && desc.length > 0) acc.addedEntries.push(desc);

        if ((kind === "fix" || kind === "perf") && desc.length > 0)
          acc.fixedEntries.push(desc);

        accs.set(pkg.name, acc);
      }
    }
  }

  // Build final BumpPlan list.
  const plans: BumpPlan[] = [];

  for (const pkg of packages) {
    const acc = accs.get(pkg.name);

    if (acc === undefined || acc.bump === "none") continue;

    const parsed = parseVersion(pkg.currentVersion);
    const newVersion = applyBump(parsed, acc.bump);

    plans.push({
      pkg,
      bump: acc.bump,
      newVersion,
      wireBreakingEntries: acc.wireBreakingEntries,
      apiBreakingEntries: acc.apiBreakingEntries,
      addedEntries: acc.addedEntries,
      fixedEntries: acc.fixedEntries,
      dependencyEntries: [],
    });
  }

  return plans;
}
