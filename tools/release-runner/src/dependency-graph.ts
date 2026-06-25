// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Dependency-graph propagation for the release runner.
//
// Given a set of directly-bumped packages, propagates a PATCH
// dependency-update bump to every consumable that transitively depends on
// any directly-bumped package — unless it is already in the plan set
// (direct-bump packages keep their own higher bump level).
//
// Design:
//   - Pure module: no IO, no git. Inputs are the BumpPlan[] from the bump
//     engine and the full PackageDescriptor[] inventory.
//   - buildDependentIndex: constructs the REVERSE graph (dep name →
//     Set<dependent name>) from the descriptor dependency lists.
//   - propagateBumps: BFS closure over the reverse graph, cycle-guarded by a
//     visited set. A package is enqueued for expansion at most once.
//
// Magnitude decision: propagated dependents receive a PATCH bump regardless
// of the upstream's bump level. A dependent that re-exposes an upstream break
// must declare that via its OWN footer (the author's call). PATCH on a 0.x
// package is 0.1.0 → 0.1.1, consistent with the direct-fix path.

import { falsey } from "@d2/utilities";
import { parseVersion, applyBump } from "./semver.js";
import type { BumpPlan, PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Topological sort — leaf-first ordering for the diff-runner
// ---------------------------------------------------------------------------

/**
 * Return the consumable package list in topological (leaf-first) order so
 * that when the artifact-diff engine processes packages in sequence, each
 * dep's new version is resolved before its dependent is fingerprinted.
 *
 * Uses iterative Kahn's algorithm (in-degree driven). Throws when a
 * dependency cycle is detected — a cycle makes leaf-first processing
 * indeterminate and a release over a cyclic graph is an error condition
 * that must be surfaced, not silently continued.
 *
 * Packages with no inter-consumable edges (isolated nodes) are included in
 * an arbitrary but deterministic order (sorted by name).
 *
 * @param packages - Full consumable package inventory.
 * @returns The same packages in leaf-first topological order.
 * @throws {Error} When a dependency cycle is detected among consumable packages.
 */
export function topoSort(
  packages: readonly PackageDescriptor[],
): PackageDescriptor[] {
  if (falsey(packages)) return [];

  const nameSet = new Set(packages.map((p) => p.name));
  const pkgByName = new Map(packages.map((p) => [p.name, p]));

  // Build in-degree map and adjacency list (dep → dependents) within the
  // consumable set only; edges to non-consumable packages are ignored.
  const inDegree = new Map<string, number>();
  // forward edges: pkg → its consumable deps
  const deps = new Map<string, string[]>();

  for (const pkg of packages) {
    inDegree.set(pkg.name, inDegree.get(pkg.name) ?? 0);
    deps.set(pkg.name, []);
  }

  for (const pkg of packages) {
    for (const dep of pkg.dependencies) {
      if (!nameSet.has(dep)) continue; // non-consumable edge — ignore

      // pkg depends on dep; dep must be processed before pkg.
      // Kahn's: increment in-degree of pkg for each dep edge.
      inDegree.set(pkg.name, (inDegree.get(pkg.name) ?? 0) + 1);

      // adjacency: dep → its dependents (packages that depend on dep)
      const dependents = deps.get(dep) ?? [];
      dependents.push(pkg.name);
      deps.set(dep, dependents);
    }
  }

  // Seed the queue with all zero-in-degree nodes (true leaves), sorted for
  // determinism.
  const queue: string[] = [...packages]
    .filter((p) => (inDegree.get(p.name) ?? 0) === 0)
    .map((p) => p.name)
    .sort();

  const sorted: PackageDescriptor[] = [];

  while (queue.length > 0) {
    // Sort queue each iteration for determinism across multiple zero-in-degree nodes.
    queue.sort();
    const name = queue.shift()!;
    const pkg = pkgByName.get(name);

    if (pkg !== undefined) sorted.push(pkg);

    for (const dependent of deps.get(name) ?? []) {
      const newDegree = (inDegree.get(dependent) ?? 0) - 1;
      inDegree.set(dependent, newDegree);

      if (newDegree === 0) queue.push(dependent);
    }
  }

  if (sorted.length !== packages.length) {
    // Some nodes were not reached — they form a cycle.
    const cycleNodes = packages
      .filter((p) => !sorted.some((s) => s.name === p.name))
      .map((p) => p.name)
      .sort()
      .join(", ");

    throw new Error(
      `Dependency cycle detected among consumable packages: [${cycleNodes}]. ` +
        `Release cannot proceed over a cyclic dependency graph. ` +
        `Resolve the cycle before running the release runner.`,
    );
  }

  return sorted;
}

// ---------------------------------------------------------------------------
// Reverse-graph builder
// ---------------------------------------------------------------------------

/**
 * Build a reverse dependency index: for each package name D, map it to the
 * set of consumable package names that depend on D.
 *
 * Self-edges are silently ignored. Unknown dependency names (those not in the
 * consumable name set) are dropped — non-consumable edges are not propagation
 * targets.
 *
 * @param packages - Full consumable package inventory.
 * @returns Map from dependency name → Set of dependent names.
 */
export function buildDependentIndex(
  packages: readonly PackageDescriptor[],
): Map<string, Set<string>> {
  const index = new Map<string, Set<string>>();

  for (const pkg of packages) {
    for (const dep of pkg.dependencies) {
      if (dep === pkg.name) continue; // guard self-edge

      const dependents = index.get(dep) ?? new Set<string>();

      dependents.add(pkg.name);
      index.set(dep, dependents);
    }
  }

  return index;
}

// ---------------------------------------------------------------------------
// Propagation pass
// ---------------------------------------------------------------------------

/**
 * Propagate bump plans to transitive dependents of directly-bumped packages.
 *
 * For every package already in `directPlans`, every consumable that
 * transitively depends on it (not already in any plan) receives a PATCH
 * dependency-update bump. The `dependencyEntries` field on a propagated plan
 * names the immediate upstream package(s) that triggered the bump.
 *
 * A package that is already bumped (directly or via earlier propagation) is
 * never double-bumped — its existing plan is left untouched.
 *
 * The BFS is cycle-guarded: a package name is enqueued for expansion at most
 * once, so a synthetic A↔B cycle terminates after both are resolved.
 *
 * @param directPlans - Bump plans from `computeBumpPlans` (directly-bumped).
 * @param packages    - Full consumable package inventory (for descriptor lookup
 *                      and reverse-graph construction).
 * @returns Combined array: the original direct plans unchanged, plus one new
 *          PATCH plan per propagated dependent (in BFS order).
 */
export function propagateBumps(
  directPlans: readonly BumpPlan[],
  packages: readonly PackageDescriptor[],
): BumpPlan[] {
  if (falsey(directPlans) || falsey(packages)) return [...directPlans];

  const dependentIndex = buildDependentIndex(packages);
  const pkgByName = new Map<string, PackageDescriptor>(
    packages.map((p) => [p.name, p]),
  );

  // Track which names already have a plan (direct or propagated).
  const planned = new Map<string, BumpPlan>(
    directPlans.map((p) => [p.pkg.name, p]),
  );

  // BFS worklist: [dependentName, triggeredByUpstreamName]
  // Seed with all direct-plan package names as the first wave of "bumped"
  // packages whose dependents we need to visit.
  const queue: Array<{ name: string; upstream: string }> = [];
  const visited = new Set<string>();

  for (const plan of directPlans) {
    if (!visited.has(plan.pkg.name)) {
      visited.add(plan.pkg.name);
      const deps = dependentIndex.get(plan.pkg.name);

      if (deps !== undefined) {
        for (const depName of deps) {
          queue.push({ name: depName, upstream: plan.pkg.name });
        }
      }
    }
  }

  // BFS expansion.
  while (queue.length > 0) {
    const entry = queue.shift();

    if (entry === undefined) break;

    const { name, upstream } = entry;

    // Already has a plan (direct or earlier propagation) — don't overwrite.
    if (planned.has(name)) {
      // Already planned: skip but do NOT block its own dependents (they may
      // still need expansion). The visited guard handles re-queueing.
      if (!visited.has(name)) {
        visited.add(name);
        const deps = dependentIndex.get(name);

        if (deps !== undefined) {
          for (const depName of deps) {
            queue.push({ name: depName, upstream: name });
          }
        }
      }

      continue;
    }

    // Cycle guard: if already visited (queued from another path), skip.
    if (visited.has(name)) continue;

    visited.add(name);

    const pkg = pkgByName.get(name);

    if (pkg === undefined) continue; // should not happen; defensive

    const parsed = parseVersion(pkg.currentVersion);
    const newVersion = applyBump(parsed, "patch");

    const propagatedPlan: BumpPlan = {
      pkg,
      bump: "patch",
      newVersion,
      wireBreakingEntries: [],
      apiBreakingEntries: [],
      addedEntries: [],
      fixedEntries: [],
      dependencyEntries: [upstream],
    };

    planned.set(name, propagatedPlan);

    // Enqueue this package's own dependents for transitive propagation.
    const deps = dependentIndex.get(name);

    if (deps !== undefined) {
      for (const depName of deps) {
        queue.push({ name: depName, upstream: name });
      }
    }
  }

  // Return direct plans first (order preserved), then propagated plans in BFS
  // order (sorted by name for determinism).
  const propagated = [...planned.values()].filter(
    (p) => !directPlans.some((d) => d.pkg.name === p.pkg.name),
  );

  propagated.sort((a, b) => a.pkg.name.localeCompare(b.pkg.name));

  return [...directPlans, ...propagated];
}
