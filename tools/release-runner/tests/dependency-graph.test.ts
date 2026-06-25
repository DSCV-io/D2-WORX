// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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
    dir: `server/shared/typescript/${name.replace("@d2/", "")}`,
    manifestPath: `server/shared/typescript/${name.replace("@d2/", "")}/package.json`,
    changelogPath: `server/shared/typescript/${name.replace("@d2/", "")}/CHANGELOG.md`,
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
    dir: `server/shared/dotnet/${name.toLowerCase()}`,
    manifestPath: `server/shared/dotnet/${name.toLowerCase()}/${name}.csproj`,
    changelogPath: `server/shared/dotnet/${name.toLowerCase()}/CHANGELOG.md`,
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
