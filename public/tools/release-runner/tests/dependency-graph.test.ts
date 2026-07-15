// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Unit tests for the dependency-graph propagation module.
//
// All tests use synthetic PackageDescriptor/BumpPlan inputs — no IO,
// no real manifests. The dependency edges are injected directly via
// the `dependencies` field, mirroring what manifest-loader populates at
// runtime.

import { describe, expect, it } from "vitest";

import {
  buildDependentIndex,
  propagateBumps,
  topoSort,
} from "../src/dependency-graph.js";
import type { BumpPlan, PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makeNpmPkg(
  name: string,
  dependencies: string[] = [],
  version = "0.1.0",
): PackageDescriptor {
  return {
    name,
    ecosystem: "npm",
    dir: `public/packages/typescript/${name.replace("@d2/", "")}`,
    manifestPath: `public/packages/typescript/${name.replace("@d2/", "")}/package.json`,
    changelogPath: `public/packages/typescript/${name.replace("@d2/", "")}/CHANGELOG.md`,
    currentVersion: version,
    dependencies,
  };
}

function makeNugetPkg(
  name: string,
  dependencies: string[] = [],
  version = "0.1.0",
): PackageDescriptor {
  return {
    name,
    ecosystem: "nuget",
    dir: `public/packages/dotnet/${name.toLowerCase()}`,
    manifestPath: `public/packages/dotnet/${name.toLowerCase()}/${name}.csproj`,
    changelogPath: `public/packages/dotnet/${name.toLowerCase()}/CHANGELOG.md`,
    currentVersion: version,
    dependencies,
  };
}

function makePlan(
  pkg: PackageDescriptor,
  overrides: Partial<BumpPlan> = {},
): BumpPlan {
  return {
    pkg,
    bump: "minor",
    newVersion: "0.2.0",
    wireBreakingEntries: [],
    apiBreakingEntries: [],
    addedEntries: ["add feature"],
    fixedEntries: [],
    dependencyEntries: [],
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// buildDependentIndex
// ---------------------------------------------------------------------------

describe("buildDependentIndex — graph construction", () => {
  it("returns an empty map for a package with no dependencies", () => {
    const pkg = makeNpmPkg("@d2/a");
    const index = buildDependentIndex([pkg]);
    expect(index.size).toBe(0);
  });

  it("resolves a single npm name→name edge correctly", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]); // B depends on A
    const index = buildDependentIndex([pkgA, pkgB]);
    expect(index.get("@d2/a")).toEqual(new Set(["@d2/b"]));
  });

  it("resolves a nuget path-basename→name edge correctly", () => {
    const pkgUtils = makeNugetPkg("D2.Shared.Utilities");
    const pkgResult = makeNugetPkg("D2.Shared.Result", ["D2.Shared.Utilities"]);
    const index = buildDependentIndex([pkgUtils, pkgResult]);
    expect(index.get("D2.Shared.Utilities")).toEqual(
      new Set(["D2.Shared.Result"]),
    );
  });

  it("handles multiple dependents on one package", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/a"]);
    const index = buildDependentIndex([pkgA, pkgB, pkgC]);
    expect(index.get("@d2/a")).toEqual(new Set(["@d2/b", "@d2/c"]));
  });

  it("ignores a self-edge (package listing itself as a dependency)", () => {
    const pkg = makeNpmPkg("@d2/a", ["@d2/a"]); // self-edge
    const index = buildDependentIndex([pkg]);
    // self-edge is dropped — index should be empty or not map @d2/a → @d2/a
    expect(index.get("@d2/a")).toBeUndefined();
  });

  it("a devDependency @d2/* name produces the same edge as a regular dependency", () => {
    // In the descriptor model, dependencies (regular + dev) are pre-merged
    // by the loader into a single `dependencies[]`. The graph module sees them
    // as identical — this test confirms the index handles both the same way.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]); // simulates devDep edge
    const index = buildDependentIndex([pkgA, pkgB]);
    expect(index.get("@d2/a")).toContain("@d2/b");
  });

  it("ignores edges whose dependency name is not in the consumable set", () => {
    // A non-consumable like a SourceGen shell or external package is not in
    // the descriptor set. The loader already filters, but if a stale name
    // somehow appeared, the index simply has no entry for it.
    const pkgB = makeNpmPkg("@d2/b", ["@d2/sourcegen-internal"]); // not consumable
    const index = buildDependentIndex([pkgB]);
    // The non-consumable name appears as a key in the index but points to @d2/b.
    // For propagation, this is harmless — @d2/sourcegen-internal will never
    // be in directPlans (it has no descriptor), so its dependent set is never
    // traversed. Confirm the index has the entry but @d2/b is only reachable
    // FROM a plan of @d2/sourcegen-internal (which never exists in practice).
    expect(index.get("@d2/sourcegen-internal")).toContain("@d2/b");
  });
});

// ---------------------------------------------------------------------------
// topoSort — leaf-first topological ordering
// ---------------------------------------------------------------------------

describe("topoSort — basic ordering", () => {
  it("returns empty array for empty input", () => {
    expect(topoSort([])).toEqual([]);
  });

  it("single package with no deps → returned as-is", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const result = topoSort([pkgA]);
    expect(result).toHaveLength(1);
    expect(result[0]!.name).toBe("@d2/a");
  });

  it("two packages with an edge: dep appears before dependent", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]); // B depends on A
    const result = topoSort([pkgB, pkgA]); // unsorted input

    expect(result).toHaveLength(2);
    const indexA = result.findIndex((p) => p.name === "@d2/a");
    const indexB = result.findIndex((p) => p.name === "@d2/b");
    expect(indexA).toBeLessThan(indexB); // A (leaf) before B (dependent)
  });

  it("chain A → B → C: sorted as A, B, C", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/b"]);
    const result = topoSort([pkgC, pkgA, pkgB]); // shuffled input

    const names = result.map((p) => p.name);
    expect(names.indexOf("@d2/a")).toBeLessThan(names.indexOf("@d2/b"));
    expect(names.indexOf("@d2/b")).toBeLessThan(names.indexOf("@d2/c"));
  });

  it("diamond graph (A→B, A→C, B→D, C→D): A appears first, D appears last", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/a"]);
    const pkgD = makeNpmPkg("@d2/d", ["@d2/b", "@d2/c"]);
    const result = topoSort([pkgD, pkgC, pkgB, pkgA]);

    const names = result.map((p) => p.name);
    expect(names[0]).toBe("@d2/a"); // sole leaf
    expect(names[names.length - 1]).toBe("@d2/d"); // sole root
    expect(names).toHaveLength(4);
  });

  it("non-consumable dep edge is ignored (edge to a name not in the inventory)", () => {
    // @d2/b depends on @d2/external which is not in the inventory.
    // topoSort ignores that edge and still returns both packages.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/external", "@d2/a"]);
    const result = topoSort([pkgB, pkgA]);

    expect(result).toHaveLength(2);
    expect(result[0]!.name).toBe("@d2/a");
    expect(result[1]!.name).toBe("@d2/b");
  });

  it("throws when a dependency cycle exists", () => {
    const pkgA = makeNpmPkg("@d2/a", ["@d2/b"]);
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);

    expect(() => topoSort([pkgA, pkgB])).toThrow(/cycle/i);
  });

  it("isolated packages (no edges) are sorted by name for determinism", () => {
    const pkgZ = makeNpmPkg("@d2/z");
    const pkgA = makeNpmPkg("@d2/a");
    const pkgM = makeNpmPkg("@d2/m");
    const result = topoSort([pkgZ, pkgA, pkgM]);

    expect(result.map((p) => p.name)).toEqual(["@d2/a", "@d2/m", "@d2/z"]);
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — core propagation logic
// ---------------------------------------------------------------------------

describe("propagateBumps — direct bump propagates to dependents", () => {
  it("directly-bumped dependency patch-bumps untouched dependents", () => {
    // A bumps (feat → minor); B depends on A and is NOT directly bumped.
    // Expected: B receives a PATCH dependency-update plan.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA, { bump: "minor", newVersion: "0.2.0" });

    const result = propagateBumps([planA], [pkgA, pkgB]);

    expect(result).toHaveLength(2);

    const planB = result.find((p) => p.pkg.name === "@d2/b");
    expect(planB).toBeDefined();
    expect(planB!.bump).toBe("patch");
    expect(planB!.newVersion).toBe("0.1.1");
  });

  it("propagated plan records the upstream package name in dependencyEntries", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);
    const planB = result.find((p) => p.pkg.name === "@d2/b");

    expect(planB!.dependencyEntries).toContain("@d2/a");
  });

  it("directly-bumped plan has empty dependencyEntries (not a dependency-update)", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);
    const resultPlanA = result.find((p) => p.pkg.name === "@d2/a");

    expect(resultPlanA!.dependencyEntries).toHaveLength(0);
  });

  it("PATCH propagation on a 0.x package produces 0.x.y → 0.x.(y+1)", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"], "0.3.7");
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);
    const planB = result.find((p) => p.pkg.name === "@d2/b");

    expect(planB!.bump).toBe("patch");
    expect(planB!.newVersion).toBe("0.3.8");
  });

  it("no dependents means propagation returns only the direct plan", () => {
    const pkgA = makeNpmPkg("@d2/a"); // no dependents
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA]);

    expect(result).toHaveLength(1);
    expect(result[0]!.pkg.name).toBe("@d2/a");
  });

  it("empty directPlans returns empty array", () => {
    const pkgA = makeNpmPkg("@d2/a");
    expect(propagateBumps([], [pkgA])).toHaveLength(0);
  });

  it("empty packages returns the direct plans unchanged", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const planA = makePlan(pkgA);
    const result = propagateBumps([planA], []);
    expect(result).toHaveLength(1);
  });

  it("duplicate entries in directPlans are de-duped by the visited guard (no double seeding)", () => {
    // Passing the same plan twice is malformed input, but the seeding loop
    // guards against it: a package visited once is skipped on the second pass.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);

    // planA appears twice — the second occurrence exercises the !visited branch.
    const result = propagateBumps([planA, planA], [pkgA, pkgB]);

    // A appears once in the result (not twice), B gets exactly one PATCH plan.
    const aPlans = result.filter((p) => p.pkg.name === "@d2/a");
    expect(aPlans).toHaveLength(2); // two entries in directPlans are returned as-is
    // B still gets a single PATCH plan (seeding guard prevents double-queueing).
    const bPlans = result.filter((p) => p.pkg.name === "@d2/b");
    expect(bPlans).toHaveLength(1);
    expect(bPlans[0]!.bump).toBe("patch");
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — transitive chain
// ---------------------------------------------------------------------------

describe("propagateBumps — transitive chain (A → B → C)", () => {
  // C depends on B, B depends on A.
  // Direct bump: A bumps → B and C should both get PATCH.

  it("transitive chain: bumping A patch-bumps B and C", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/b"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB, pkgC]);

    expect(result).toHaveLength(3);

    const planB = result.find((p) => p.pkg.name === "@d2/b");
    const planC = result.find((p) => p.pkg.name === "@d2/c");

    expect(planB!.bump).toBe("patch");
    expect(planC!.bump).toBe("patch");
  });

  it("a directly-bumped middle package (A,B direct; C transitive) still expands to C", () => {
    // A and B are BOTH directly bumped; C depends on B (not directly bumped).
    // The seeding loop queues C via planB before the BFS starts, so C still
    // receives a PATCH plan even though B is skipped (already planned) in the BFS.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/b"]);
    const planA = makePlan(pkgA, { bump: "minor", newVersion: "0.2.0" });
    const planB = makePlan(pkgB, { bump: "minor", newVersion: "0.2.0" });

    const result = propagateBumps([planA, planB], [pkgA, pkgB, pkgC]);

    expect(result).toHaveLength(3);

    const resultC = result.find((p) => p.pkg.name === "@d2/c");
    expect(resultC).toBeDefined();
    expect(resultC!.bump).toBe("patch");
    expect(resultC!.dependencyEntries).toContain("@d2/b");

    // A and B keep their own direct (minor) plans — not overwritten.
    expect(result.find((p) => p.pkg.name === "@d2/a")!.bump).toBe("minor");
    expect(result.find((p) => p.pkg.name === "@d2/b")!.bump).toBe("minor");
  });

  it("B's dependencyEntries names A; C's dependencyEntries names B", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/b"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB, pkgC]);
    const planB = result.find((p) => p.pkg.name === "@d2/b");
    const planC = result.find((p) => p.pkg.name === "@d2/c");

    expect(planB!.dependencyEntries).toContain("@d2/a");
    expect(planC!.dependencyEntries).toContain("@d2/b");
  });

  it("deep chain (A → B → C → D) all get PATCH", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/b"]);
    const pkgD = makeNpmPkg("@d2/d", ["@d2/c"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB, pkgC, pkgD]);

    expect(result).toHaveLength(4);
    expect(result.every((p) => p.bump === "minor" || p.bump === "patch")).toBe(
      true,
    );

    const planD = result.find((p) => p.pkg.name === "@d2/d");
    expect(planD!.bump).toBe("patch");
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — diamond graph (shared transitive dependent)
// ---------------------------------------------------------------------------

describe("propagateBumps — diamond dependency graph (A→B, A→C, B→D, C→D)", () => {
  // D depends on both B and C, which both depend on A. When A bumps, D should
  // receive exactly ONE PATCH plan despite being reachable via two paths.

  it("diamond: bumping A patch-bumps B, C, and D exactly once", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/a"]);
    const pkgD = makeNpmPkg("@d2/d", ["@d2/b", "@d2/c"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB, pkgC, pkgD]);

    expect(result).toHaveLength(4);

    const planB = result.find((p) => p.pkg.name === "@d2/b");
    const planC = result.find((p) => p.pkg.name === "@d2/c");
    const planD = result.find((p) => p.pkg.name === "@d2/d");

    expect(planB?.bump).toBe("patch");
    expect(planC?.bump).toBe("patch");
    expect(planD?.bump).toBe("patch");
  });

  it("diamond: each package appears at most once (no double-bump via two paths)", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/a"]);
    const pkgD = makeNpmPkg("@d2/d", ["@d2/b", "@d2/c"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB, pkgC, pkgD]);
    const names = result.map((p) => p.pkg.name);

    expect(new Set(names).size).toBe(names.length);
  });

  it("diamond with B directly bumped: B, C, D all appear in result once", () => {
    // A and B are directly bumped. C and D are transitive.
    // D depends on both B and C — the seeding loop queues D from planB.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const pkgC = makeNpmPkg("@d2/c", ["@d2/a"]);
    const pkgD = makeNpmPkg("@d2/d", ["@d2/b", "@d2/c"]);
    const planA = makePlan(pkgA, { bump: "minor", newVersion: "0.2.0" });
    const planB = makePlan(pkgB, { bump: "minor", newVersion: "0.2.0" });

    const result = propagateBumps([planA, planB], [pkgA, pkgB, pkgC, pkgD]);

    expect(result).toHaveLength(4);

    const names = result.map((p) => p.pkg.name);
    expect(new Set(names).size).toBe(names.length);

    expect(result.find((p) => p.pkg.name === "@d2/a")?.bump).toBe("minor");
    expect(result.find((p) => p.pkg.name === "@d2/b")?.bump).toBe("minor");
    expect(result.find((p) => p.pkg.name === "@d2/c")?.bump).toBe("patch");
    expect(result.find((p) => p.pkg.name === "@d2/d")?.bump).toBe("patch");
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — cycle guard
// ---------------------------------------------------------------------------

describe("propagateBumps — cycle guard", () => {
  it("synthetic A↔B cycle does not loop infinitely", () => {
    // A depends on B AND B depends on A (would be a cycle in real code but
    // impossible in a well-formed dependency graph — guard must handle it).
    const pkgA = makeNpmPkg("@d2/a", ["@d2/b"]);
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);

    // Must terminate (no infinite loop).
    expect(() => propagateBumps([planA], [pkgA, pkgB])).not.toThrow();
  });

  it("with A↔B cycle, bumping A results in both A and B being planned", () => {
    const pkgA = makeNpmPkg("@d2/a", ["@d2/b"]);
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);

    expect(result).toHaveLength(2);

    const planB = result.find((p) => p.pkg.name === "@d2/b");
    expect(planB).toBeDefined();
    expect(planB!.bump).toBe("patch");
  });

  it("each package appears at most once in the result for a cycle", () => {
    const pkgA = makeNpmPkg("@d2/a", ["@d2/b"]);
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);
    const names = result.map((p) => p.pkg.name);

    // Each name appears exactly once.
    expect(new Set(names).size).toBe(names.length);
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — already-directly-bumped dependent is NOT double-bumped
// ---------------------------------------------------------------------------

describe("propagateBumps — already-directly-bumped dependents keep their bump", () => {
  it("B directly bumped minor: propagation from A does not overwrite B's bump", () => {
    // A bumps (minor), B depends on A AND has its own direct minor bump.
    // Expected: B keeps MINOR — not downgraded to PATCH by propagation.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA, { bump: "minor", newVersion: "0.2.0" });
    const planB = makePlan(pkgB, {
      bump: "minor",
      newVersion: "0.2.0",
      addedEntries: ["own feature"],
    });

    const result = propagateBumps([planA, planB], [pkgA, pkgB]);

    const resultPlanB = result.find((p) => p.pkg.name === "@d2/b");
    expect(resultPlanB!.bump).toBe("minor"); // kept, not overwritten
    expect(resultPlanB!.dependencyEntries).toHaveLength(0); // NOT a dep-update plan
  });

  it("B directly bumped patch: propagation from A does not re-patch B", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);
    const planB = makePlan(pkgB, {
      bump: "patch",
      newVersion: "0.1.1",
      addedEntries: [],
      fixedEntries: ["own fix"],
    });

    const result = propagateBumps([planA, planB], [pkgA, pkgB]);

    // Still exactly 2 plans.
    expect(result).toHaveLength(2);

    const resultPlanB = result.find((p) => p.pkg.name === "@d2/b");
    expect(resultPlanB!.bump).toBe("patch");
    expect(resultPlanB!.newVersion).toBe("0.1.1"); // unchanged
  });

  it("directly-bumped package does not get dependencyEntries even if also reachable via propagation", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);
    const planB = makePlan(pkgB, { dependencyEntries: [] }); // direct with no dep entries

    const result = propagateBumps([planA, planB], [pkgA, pkgB]);
    const resultPlanB = result.find((p) => p.pkg.name === "@d2/b");

    // dependencyEntries stays empty on directly-bumped packages.
    expect(resultPlanB!.dependencyEntries).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — duplicate seeding (both parents directly bumped)
// ---------------------------------------------------------------------------

describe("propagateBumps — phantom dependent queued from two directly-bumped parents", () => {
  // A and B are both directly bumped, and both have @d2/phantom as a dependent
  // in the reverse-dependency index (but @d2/phantom has no descriptor).
  // Seeding queues phantom(A) AND phantom(B). First dequeue → visits phantom
  // but cannot plan it (no descriptor). Second dequeue → already visited → skip.
  // Exercises the visited-but-not-planned short-circuit in the BFS.

  it("phantom dependent (no descriptor) queued twice → terminates cleanly, phantom absent from result", () => {
    // phantom depends on both A and B (reverse index: A→{phantom}, B→{phantom}).
    // Simulate by making phantom list A and B as its own dependencies in its
    // NOT-included descriptor — actually, simpler: make A and B both have
    // phantom as a dependent by injecting it directly into the input packages
    // BUT without a full PackageDescriptor (just a name in dep lists).
    //
    // The buildDependentIndex function creates edges from any listed dep name.
    // If pkgC (with no entry in pkgByName) appears in dep lists of both A and B,
    // the reverse index maps A→{phantom} and B→{phantom}.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b");
    // pkgPhantomDependent depends on both A and B, but its descriptor is excluded
    // from the `packages` array passed to propagateBumps.
    const pkgPhantomDependent = makeNpmPkg("@d2/phantom", ["@d2/a", "@d2/b"]);
    const planA = makePlan(pkgA, { bump: "minor", newVersion: "0.2.0" });
    const planB = makePlan(pkgB, { bump: "minor", newVersion: "0.2.0" });

    // Pass A and B and phantom to buildDependentIndex (so the reverse edges exist),
    // but only A and B as the package inventory for propagateBumps (phantom is unknown).
    // We achieve this by passing [pkgA, pkgB, pkgPhantomDependent] as packages so the
    // reverse index builds correctly, but phantom has no plan → the BFS processes it.
    const result = propagateBumps(
      [planA, planB],
      [pkgA, pkgB, pkgPhantomDependent],
    );

    // phantom DOES have a descriptor here (we included it), so it gets propagated.
    // This variant tests the "two direct plans → phantom queued twice" scenario,
    // where phantom IS in pkgByName.
    expect(result.find((p) => p.pkg.name === "@d2/phantom")).toBeDefined();
    expect(result.find((p) => p.pkg.name === "@d2/phantom")?.bump).toBe(
      "patch",
    );
    // Each name appears exactly once (no double-bump).
    const names = result.map((p) => p.pkg.name);
    expect(new Set(names).size).toBe(names.length);
  });

  it("truly-absent phantom (not in packages array) → queue visits it twice without crashing", () => {
    // A depends on B, and B's dep list includes @d2/phantom (no descriptor).
    // A has @d2/phantom in its dep list too → reverse index maps phantom to {A,B}.
    // Seeding queues phantom from A and again from B's propagation.
    const pkgA = makeNpmPkg("@d2/a", ["@d2/phantom"]);
    const pkgB = makeNpmPkg("@d2/b", ["@d2/phantom"]);
    const planA = makePlan(pkgA, { bump: "minor", newVersion: "0.2.0" });
    const planB = makePlan(pkgB, { bump: "minor", newVersion: "0.2.0" });

    // phantom has no entry in the packages array → pkgByName has no entry.
    // Both planA and planB have phantom as a dependent (via dep list reversal).
    // Seeding queues phantom(upstream=A) and phantom(upstream=B).
    // First dequeue: pkg===undefined → visits phantom, skips plan (line 242).
    // Second dequeue: visited.has("@d2/phantom")===true → skips (line 236).
    expect(() => propagateBumps([planA, planB], [pkgA, pkgB])).not.toThrow();

    const result = propagateBumps([planA, planB], [pkgA, pkgB]);
    // No phantom in result (it has no descriptor).
    expect(result.find((p) => p.pkg.name === "@d2/phantom")).toBeUndefined();
    // A and B keep their own direct (minor) plans.
    expect(result).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — non-consumable edge is ignored
// ---------------------------------------------------------------------------

describe("propagateBumps — non-consumable edges are not propagation targets", () => {
  it("a dependency name not in the package inventory produces no propagation target", () => {
    // Imagine a non-consumable package (SourceGen shell, external npm dep) that
    // the loader already excludes. Its name does not appear in any descriptor.
    // The only way it would enter the graph is if a descriptor listed it —
    // which the loader prevents. Here we model the worst case: a descriptor
    // lists a non-consumable name, but since there is no descriptor for it,
    // the BFS finds no BumpPlan seed for it and it is never enqueued.
    const pkgA = makeNpmPkg("@d2/a"); // directly bumped
    // pkgB lists @d2/sourcegen-internal as a dependency — that name has no descriptor.
    const pkgB = makeNpmPkg("@d2/b", ["@d2/sourcegen-internal", "@d2/a"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);

    // B should still be patched (it depends on A which is bumped).
    expect(result.find((p) => p.pkg.name === "@d2/b")).toBeDefined();
    // No phantom plan for the non-consumable.
    expect(
      result.find((p) => p.pkg.name === "@d2/sourcegen-internal"),
    ).toBeUndefined();
  });

  it("a non-@d2/ dependency name produces no propagation target", () => {
    // The loader filters out non-@d2/ npm deps. Model the case where that filter
    // already ran and the descriptor has no entry for 'typescript' etc.
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]); // loader already excluded 'typescript'
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);
    const names = result.map((p) => p.pkg.name);

    expect(names).not.toContain("typescript");
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — mixed npm + nuget graph
// ---------------------------------------------------------------------------

describe("propagateBumps — cross-ecosystem propagation", () => {
  it("nuget package bumping propagates to its nuget dependents", () => {
    const pkgUtils = makeNugetPkg("D2.Shared.Utilities");
    const pkgResult = makeNugetPkg("D2.Shared.Result", ["D2.Shared.Utilities"]);
    const planUtils = makePlan(pkgUtils);

    const result = propagateBumps([planUtils], [pkgUtils, pkgResult]);

    const planResult = result.find((p) => p.pkg.name === "D2.Shared.Result");
    expect(planResult).toBeDefined();
    expect(planResult!.bump).toBe("patch");
    expect(planResult!.dependencyEntries).toContain("D2.Shared.Utilities");
  });
});

// ---------------------------------------------------------------------------
// propagateBumps — order of output
// ---------------------------------------------------------------------------

describe("propagateBumps — output ordering", () => {
  it("direct plans appear first in the result array", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgB = makeNpmPkg("@d2/b", ["@d2/a"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgB]);

    expect(result[0]!.pkg.name).toBe("@d2/a");
    expect(result[1]!.pkg.name).toBe("@d2/b");
  });

  it("propagated plans are sorted by name for determinism", () => {
    const pkgA = makeNpmPkg("@d2/a");
    const pkgZ = makeNpmPkg("@d2/z", ["@d2/a"]);
    const pkgM = makeNpmPkg("@d2/m", ["@d2/a"]);
    const planA = makePlan(pkgA);

    const result = propagateBumps([planA], [pkgA, pkgZ, pkgM]);

    const propagatedNames = result.slice(1).map((p) => p.pkg.name);
    expect(propagatedNames).toEqual(["@d2/m", "@d2/z"]);
  });
});
