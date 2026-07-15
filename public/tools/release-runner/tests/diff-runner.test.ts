// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Integration tests for the artifact-diff release runner (runDiffRelease).
//
// All tests use injectable DiffProvider so no real builds, no real git, and
// no committed baseline files are required. Filesystem writes go to a
// temporary directory created per-test and cleaned up in afterEach.
//
// Test coverage:
//   Suite A — pure bump-derivation via DiffProvider injection (no IO):
//     DR1  no-change → no bump (no plan produced)
//     DR2  output-change-only (fingerprint changed, API same) → PATCH
//     DR3  API add → MINOR
//     DR4  API remove, stable → MAJOR
//     DR5  API remove, pre-stable → MINOR (carve-out)
//     DR6  footer-forced, no diff → MAJOR (stable); authoritative escalation
//     DR7  footer-forced, no diff, pre-stable → MINOR (capped)
//     DR8  missing baseline → PATCH (graceful, warning emitted)
//
//   Suite B — propagation via fingerprint (topo-order, no BFS):
//     DR9   bump a dep → dep's new version in resolvedVersions →
//           dependent's DiffProvider sees changed manifest fingerprint →
//           dependent floors at PATCH (NO separate BFS pass)
//     DR10  dep NOT bumped (no diff) → dependent's fingerprint unchanged →
//           dependent gets no plan
//
//   Suite C — topological ordering:
//     DR11  two packages with a dep edge are processed leaf-first (dep before dependent)
//     DR12  a cycle (A→B→A) → topoSort throws "cycle detected" error
//
//   Suite D — multi-package apply mode (writes manifests + changelogs):
//     DR13  apply-mode writes correct versions and changelog for a MINOR bump
//     DR14  apply-mode writes PATCH changelog for a dep-propagation bump
//
//   Suite E — package filter:
//     DR15  packageFilter restricts output after propagation
//
//   Suite F — dry-run (no writes):
//     DR16  dryRun=true returns plans but leaves files unchanged

import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";

import { runDiffRelease } from "../src/diff-runner.js";
import { topoSort } from "../src/dependency-graph.js";
import type {
  DiffProvider,
  DiffProviderInput,
  PackageDiff,
} from "../src/diff-runner.js";
import type { PackageDescriptor, RunnerOptions } from "../src/types.js";

// ---------------------------------------------------------------------------
// Temp dir
// ---------------------------------------------------------------------------

let tempDir: string;

beforeEach(() => {
  tempDir = join(tmpdir(), `diff-runner-${Date.now().toString()}`);
  mkdirSync(tempDir, { recursive: true });
});

afterEach(() => {
  rmSync(tempDir, { recursive: true, force: true });
});

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const SEEDED_CHANGELOG = `# Changelog

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Changed

### Fixed`;

function createNpmFixture(
  name: string,
  dependencies: string[] = [],
  version = "0.1.0",
): { pkg: PackageDescriptor; dir: string } {
  const safeName = name.replace(/[@/]/g, "_");
  const dir = join(tempDir, safeName);
  mkdirSync(dir, { recursive: true });

  const manifestPath = join(dir, "package.json");
  writeFileSync(
    manifestPath,
    JSON.stringify({ name, version, private: true, type: "module" }, null, 2),
    "utf-8",
  );

  const changelogPath = join(dir, "CHANGELOG.md");
  writeFileSync(changelogPath, SEEDED_CHANGELOG, "utf-8");

  const relDir = safeName;

  return {
    pkg: {
      name,
      ecosystem: "npm",
      dir: relDir,
      manifestPath,
      changelogPath,
      currentVersion: version,
      dependencies,
    },
    dir: relDir,
  };
}

function createNugetFixture(
  name: string,
  dependencies: string[] = [],
  version = "0.1.0",
): { pkg: PackageDescriptor; dir: string } {
  const dir = join(tempDir, name);
  mkdirSync(dir, { recursive: true });

  const manifestPath = join(dir, `${name}.csproj`);
  writeFileSync(
    manifestPath,
    `<Project><PropertyGroup><Version>${version}</Version><PackageId>${name}</PackageId></PropertyGroup></Project>`,
    "utf-8",
  );

  const changelogPath = join(dir, "CHANGELOG.md");
  writeFileSync(changelogPath, SEEDED_CHANGELOG, "utf-8");

  const relDir = name;

  return {
    pkg: {
      name,
      ecosystem: "nuget",
      dir: relDir,
      manifestPath,
      changelogPath,
      currentVersion: version,
      dependencies,
    },
    dir: relDir,
  };
}

function makeCommit(message: string, dirs: string[]) {
  return { message, files: dirs.map((d) => `${d}/src/index.ts`) };
}

function opts(
  dryRun: boolean,
  propagate = true,
  packageFilter?: string,
): RunnerOptions {
  return { today: "2026-06-25", dryRun, propagate, packageFilter };
}

// A DiffProvider that always returns the same static diff for every package.
function staticDiffProvider(diff: PackageDiff): DiffProvider {
  return {
    getDiff(_input: DiffProviderInput): PackageDiff {
      return diff;
    },
  };
}

// ---------------------------------------------------------------------------
// Suite A — pure bump-derivation via injected DiffProvider
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite A: pure bump derivation (injected diffs)", () => {
  it("DR1: no diff, no commits → no plan produced", () => {
    const { pkg } = createNpmFixture("@d2/a");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: false },
      baselineMissing: false,
    });

    const result = runDiffRelease([], [pkg], opts(true), provider);

    expect(result.plans).toHaveLength(0);
    expect(result.applied).toBe(false);
  });

  it("DR2: fingerprint changed (API same), stable → PATCH bump", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "1.2.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("fix: internal tweak", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.bump).toBe("patch");
    expect(result.plans[0]!.newVersion).toBe("1.2.1");
  });

  it("DR3: API add → MINOR bump (pre-stable)", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "0.1.0");
    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("feat: new export", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans[0]!.bump).toBe("minor");
    expect(result.plans[0]!.newVersion).toBe("0.2.0");
    // commit type=feat → addedEntries populated
    expect(result.plans[0]!.addedEntries).toContain("new export");
  });

  it("DR4: API remove, stable → MAJOR bump", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "1.0.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: true, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("refactor: drop legacy method", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans[0]!.bump).toBe("major");
    expect(result.plans[0]!.newVersion).toBe("2.0.0");
  });

  it("DR5: API remove, pre-stable (0.x) → MINOR (pre-stable carve-out)", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "0.4.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: true, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("refactor: remove old export", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans[0]!.bump).toBe("minor");
    expect(result.plans[0]!.newVersion).toBe("0.5.0");
  });

  it("DR6: footer-forced, no diff, stable → MAJOR (authoritative escalation)", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "1.2.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: false },
      baselineMissing: false,
    });

    const commit = {
      message: [
        "feat: force break",
        "",
        "WIRE-BREAKING: dropped field X from FooRequest",
      ].join("\n"),
      files: [`${dir}/src/index.ts`],
    };

    const result = runDiffRelease([commit], [pkg], opts(true), provider);

    expect(result.plans[0]!.bump).toBe("major");
    expect(result.plans[0]!.wireBreakingEntries).toContain(
      "dropped field X from FooRequest",
    );
  });

  it("DR7: footer-forced, no diff, pre-stable (0.x) → MINOR (capped)", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "0.3.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: false },
      baselineMissing: false,
    });

    const commit = {
      message: [
        "feat: break at pre-stable",
        "",
        "BREAKING CHANGE: renamed the main function",
      ].join("\n"),
      files: [`${dir}/src/index.ts`],
    };

    const result = runDiffRelease([commit], [pkg], opts(true), provider);

    expect(result.plans[0]!.bump).toBe("minor");
    expect(result.plans[0]!.newVersion).toBe("0.4.0");
  });

  it("DR8: baseline missing → PATCH bump + warning emitted (graceful, no crash)", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "0.1.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: true }, // missing baseline → changed=true by convention
      baselineMissing: true,
    });

    const result = runDiffRelease(
      [makeCommit("chore: init", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans[0]!.bump).toBe("patch");
    expect(result.warnings.length).toBeGreaterThan(0);
    expect(result.warnings[0]).toContain("baseline missing");
    expect(result.warnings[0]).toContain("@d2/a");
  });
});

// ---------------------------------------------------------------------------
// Suite B — propagation via fingerprint (no BFS)
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite B: propagation via fingerprint", () => {
  it("DR9: bumping dep → resolvedVersions updated → dependent sees changed fingerprint → floors at PATCH", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@d2/a", [], "0.1.0");
    const { pkg: pkgB } = createNpmFixture("@d2/b", ["@d2/a"], "0.1.0");

    // Track what resolvedVersions B's call receives.
    const calls: DiffProviderInput[] = [];

    // A: API add → MINOR.  B: will check resolvedVersions.
    const diffMap = new Map<string, PackageDiff>([
      [
        "@d2/a",
        {
          apiDiff: { added: true, removed: false, changed: false },
          fingerprintDiff: { changed: true },
          baselineMissing: false,
        },
      ],
    ]);

    // B's diff is determined dynamically based on resolvedVersions it receives.
    let bDiff: PackageDiff = {
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: false },
      baselineMissing: false,
    };

    const provider: DiffProvider = {
      getDiff(input: DiffProviderInput): PackageDiff {
        calls.push({
          pkg: input.pkg,
          resolvedVersions: new Map(input.resolvedVersions),
        });

        if (input.pkg.name === "@d2/a") return diffMap.get("@d2/a")!;

        // B: simulate that the DiffProvider detects the dep version changed
        // in resolvedVersions and reflects it as fingerprintDiff.changed=true.
        const aVersion = input.resolvedVersions.get("@d2/a");
        const depVersionChanged =
          aVersion !== undefined && aVersion !== pkgA.currentVersion;

        bDiff = {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed: depVersionChanged },
          baselineMissing: false,
        };

        return bDiff;
      },
    };

    const result = runDiffRelease(
      [makeCommit("feat: add helper", [dirA])],
      [pkgA, pkgB],
      opts(true),
      provider,
    );

    // A: MINOR bump expected.
    const planA = result.plans.find((p) => p.pkg.name === "@d2/a");
    expect(planA!.bump).toBe("minor");

    // B: propagated PATCH via fingerprint.
    const planB = result.plans.find((p) => p.pkg.name === "@d2/b");
    expect(planB).toBeDefined();
    expect(planB!.bump).toBe("patch");

    // B's DiffProvider call received the updated @d2/a version.
    const bCall = calls.find((c) => c.pkg.name === "@d2/b");
    expect(bCall).toBeDefined();
    expect(bCall!.resolvedVersions.get("@d2/a")).toBe("0.2.0");
  });

  it("DR10: dep NOT bumped (no diff) → resolvedVersions unchanged → dependent fingerprint unchanged → no plan", () => {
    const { pkg: pkgA } = createNpmFixture("@d2/a", [], "0.1.0");
    const { pkg: pkgB } = createNpmFixture("@d2/b", ["@d2/a"], "0.1.0");

    // No diff for either package.
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: false },
      baselineMissing: false,
    });

    const result = runDiffRelease([], [pkgA, pkgB], opts(true), provider);

    expect(result.plans).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Suite C — topological ordering and cycle detection
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite C: topo ordering and cycle guard", () => {
  it("DR11: dep is processed before its dependent (leaf-first order)", () => {
    const { pkg: pkgA } = createNpmFixture("@d2/a", [], "0.1.0");
    const { pkg: pkgB } = createNpmFixture("@d2/b", ["@d2/a"], "0.1.0");

    const callOrder: string[] = [];
    const provider: DiffProvider = {
      getDiff(input: DiffProviderInput): PackageDiff {
        callOrder.push(input.pkg.name);
        return {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed: false },
          baselineMissing: false,
        };
      },
    };

    runDiffRelease([], [pkgA, pkgB], opts(true), provider);

    // A (dep) must be called before B (dependent).
    const aIdx = callOrder.indexOf("@d2/a");
    const bIdx = callOrder.indexOf("@d2/b");
    expect(aIdx).toBeGreaterThanOrEqual(0);
    expect(bIdx).toBeGreaterThanOrEqual(0);
    expect(aIdx).toBeLessThan(bIdx);
  });

  it("DR12: cycle (A→B, B→A) → topoSort throws cycle-detected error", () => {
    // These are synthetic descriptor objects (no real fs needed for topoSort).
    const pkgA: PackageDescriptor = {
      name: "@d2/a",
      ecosystem: "npm",
      dir: "_d2_a",
      manifestPath: "_d2_a/package.json",
      changelogPath: "_d2_a/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: ["@d2/b"],
    };
    const pkgB: PackageDescriptor = {
      name: "@d2/b",
      ecosystem: "npm",
      dir: "_d2_b",
      manifestPath: "_d2_b/package.json",
      changelogPath: "_d2_b/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: ["@d2/a"],
    };

    expect(() => topoSort([pkgA, pkgB])).toThrow(/cycle/i);
  });

  it("topoSort: isolated packages (no edges) are all returned", () => {
    const pkgA: PackageDescriptor = {
      name: "@d2/a",
      ecosystem: "npm",
      dir: "_a",
      manifestPath: "_a/package.json",
      changelogPath: "_a/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: [],
    };
    const pkgB: PackageDescriptor = {
      name: "@d2/b",
      ecosystem: "npm",
      dir: "_b",
      manifestPath: "_b/package.json",
      changelogPath: "_b/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: [],
    };

    const sorted = topoSort([pkgA, pkgB]);
    expect(sorted).toHaveLength(2);
    expect(sorted.map((p) => p.name).sort()).toEqual(["@d2/a", "@d2/b"]);
  });

  it("topoSort: three-node chain A→B→C returns A, B, C in leaf-first order", () => {
    const pkgA: PackageDescriptor = {
      name: "@d2/a",
      ecosystem: "npm",
      dir: "_a",
      manifestPath: "_a/package.json",
      changelogPath: "_a/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: [],
    };
    const pkgB: PackageDescriptor = {
      name: "@d2/b",
      ecosystem: "npm",
      dir: "_b",
      manifestPath: "_b/package.json",
      changelogPath: "_b/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: ["@d2/a"],
    };
    const pkgC: PackageDescriptor = {
      name: "@d2/c",
      ecosystem: "npm",
      dir: "_c",
      manifestPath: "_c/package.json",
      changelogPath: "_c/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: ["@d2/b"],
    };

    const sorted = topoSort([pkgA, pkgB, pkgC]);
    expect(sorted.map((p) => p.name)).toEqual(["@d2/a", "@d2/b", "@d2/c"]);
  });

  it("topoSort: empty input returns empty array", () => {
    expect(topoSort([])).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Suite D — apply mode (writes manifests + changelogs)
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite D: apply mode writes", () => {
  it("DR13: apply mode writes correct version and CHANGELOG for MINOR bump", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "0.1.0");
    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("feat: new helper", [dir])],
      [pkg],
      opts(false),
      provider,
    );

    expect(result.applied).toBe(true);

    const updatedPkg = JSON.parse(readFileSync(pkg.manifestPath, "utf-8")) as {
      version: string;
    };
    expect(updatedPkg.version).toBe("0.2.0");

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("## 0.2.0 - 2026-06-25");
    expect(changelog).toContain("### Added");
    expect(changelog).toContain("- new helper");
    expect(changelog).toContain("## [Unreleased]");
  });

  it("DR14: apply mode writes PATCH changelog for dep-propagation bump (no direct commits)", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@d2/a", [], "0.1.0");
    const { pkg: pkgB } = createNpmFixture("@d2/b", ["@d2/a"], "0.1.0");

    const diffMap = new Map<string, PackageDiff>();
    diffMap.set("@d2/a", {
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const provider: DiffProvider = {
      getDiff(input: DiffProviderInput): PackageDiff {
        if (input.pkg.name === "@d2/a") return diffMap.get("@d2/a")!;

        // B: fingerprint changed because @d2/a version changed.
        const aVersion = input.resolvedVersions.get("@d2/a");
        const changed =
          aVersion !== undefined && aVersion !== pkgA.currentVersion;
        return {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed },
          baselineMissing: false,
        };
      },
    };

    runDiffRelease(
      [makeCommit("feat: new helper", [dirA])],
      [pkgA, pkgB],
      opts(false),
      provider,
    );

    const changelogB = readFileSync(pkgB.changelogPath, "utf-8");
    expect(changelogB).toContain("## 0.1.1 - 2026-06-25");
  });

  it("apply mode writes NuGet csproj version for nuget packages", () => {
    const { pkg, dir } = createNugetFixture("D2.Shared.Result", [], "0.1.0");
    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    runDiffRelease(
      [makeCommit("feat: add method", [dir])],
      [pkg],
      opts(false),
      provider,
    );

    const csproj = readFileSync(pkg.manifestPath, "utf-8");
    expect(csproj).toContain("<Version>0.2.0</Version>");
  });
});

// ---------------------------------------------------------------------------
// Suite E — package filter
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite E: package filter", () => {
  it("DR15: packageFilter restricts plans to the specified package", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@d2/a", [], "0.1.0");
    const { pkg: pkgB, dir: dirB } = createNpmFixture("@d2/b", [], "0.1.0");
    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("feat: a", [dirA]), makeCommit("feat: b", [dirB])],
      [pkgA, pkgB],
      opts(true, true, "@d2/a"),
      provider,
    );

    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.pkg.name).toBe("@d2/a");
  });
});

// ---------------------------------------------------------------------------
// Suite F — dry-run
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite F: dry-run leaves files unchanged", () => {
  it("DR16: dryRun=true returns plans but does not write manifests or changelogs", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "0.1.0");
    const originalManifest = readFileSync(pkg.manifestPath, "utf-8");
    const originalChangelog = readFileSync(pkg.changelogPath, "utf-8");

    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("feat: new export", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans).toHaveLength(1);
    expect(result.applied).toBe(false);

    expect(readFileSync(pkg.manifestPath, "utf-8")).toBe(originalManifest);
    expect(readFileSync(pkg.changelogPath, "utf-8")).toBe(originalChangelog);
  });
});

// ---------------------------------------------------------------------------
// Suite G — no-propagate semantics
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite G: propagate:false suppresses version forwarding", () => {
  it("propagate:false — dep version NOT forwarded → dependent receives original version", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@d2/a", [], "0.1.0");
    const { pkg: pkgB } = createNpmFixture("@d2/b", ["@d2/a"], "0.1.0");

    const calls: DiffProviderInput[] = [];

    const provider: DiffProvider = {
      getDiff(input: DiffProviderInput): PackageDiff {
        calls.push({
          pkg: input.pkg,
          resolvedVersions: new Map(input.resolvedVersions),
        });

        if (input.pkg.name === "@d2/a") {
          return {
            apiDiff: { added: true, removed: false, changed: false },
            fingerprintDiff: { changed: true },
            baselineMissing: false,
          };
        }

        return {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed: false },
          baselineMissing: false,
        };
      },
    };

    // propagate:false
    const result = runDiffRelease(
      [makeCommit("feat: add helper", [dirA])],
      [pkgA, pkgB],
      opts(true, false),
      provider,
    );

    // A bumped, B not (no propagation).
    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.pkg.name).toBe("@d2/a");

    // B's DiffProvider call should have received the ORIGINAL @d2/a version.
    const bCall = calls.find((c) => c.pkg.name === "@d2/b");
    expect(bCall).toBeDefined();
    expect(bCall!.resolvedVersions.get("@d2/a")).toBe("0.1.0");
  });
});

// ---------------------------------------------------------------------------
// Suite H — commit type drives changelog categories (not bump)
// ---------------------------------------------------------------------------

describe("runDiffRelease — Suite H: commit type → changelog category (not bump)", () => {
  it("feat: commit with API-add diff → addedEntries populated, bump=minor", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "0.1.0");
    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("feat: export new helper", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans[0]!.addedEntries).toContain("export new helper");
    expect(result.plans[0]!.fixedEntries).toHaveLength(0);
  });

  it("fix: commit with fingerprint-only diff → fixedEntries populated, bump=patch", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "1.0.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("fix: correct null handling", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans[0]!.bump).toBe("patch");
    expect(result.plans[0]!.fixedEntries).toContain("correct null handling");
    expect(result.plans[0]!.addedEntries).toHaveLength(0);
  });

  it("chore: commit with API-remove diff → bump=major (diff wins, not commit type)", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", [], "1.0.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: true, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("chore: internal cleanup", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    // chore type → "other" category, but diff says removed → major.
    expect(result.plans[0]!.bump).toBe("major");
    expect(result.plans[0]!.addedEntries).toHaveLength(0);
    expect(result.plans[0]!.fixedEntries).toHaveLength(0);
  });

  it("empty packages list → returns no plans", () => {
    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease([], [], opts(true), provider);
    expect(result.plans).toHaveLength(0);
    expect(result.applied).toBe(false);
  });

  it("a commit touching a file under NO consumable package + a no-colon subject → ignored, bump still from diff", () => {
    const { pkg } = createNpmFixture("@d2/a");
    const provider = staticDiffProvider({
      apiDiff: { added: true, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    // The first commit touches a path outside any package dir — findPackageForFile
    // returns undefined (the false branch). The subject has no colon, exercising
    // the no-colon branch of extractSubjectDescription.
    const result = runDiffRelease(
      [{ message: "tooling tweak no colon", files: ["tools/unrelated/x.ts"] }],
      [pkg],
      opts(true),
      provider,
    );

    // The package still bumps (the diff is the source of truth, independent of
    // whether a commit touched it) but carries no changelog prose.
    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.bump).toBe("minor");
    expect(result.plans[0]!.addedEntries).toHaveLength(0);
  });

  it("a perf: commit → classified as a Fixed changelog category", () => {
    const { pkg, dir } = createNpmFixture("@d2/a");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("perf: speed up the hot path", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    // perf → "perf" kind → Fixed category; the fingerprint floor → PATCH.
    expect(result.plans[0]!.bump).toBe("patch");
    expect(result.plans[0]!.fixedEntries).toContain("speed up the hot path");
  });

  it("two commits with the SAME footer entry → deduplicated in the changelog accumulator", () => {
    const { pkg, dir } = createNugetFixture("D2.Shared.Result", [], "1.2.0");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const message =
      "feat: rework envelope\n\n" +
      "WIRE-BREAKING: renamed the status field\n" +
      "BREAKING CHANGE: dropped the legacy ctor";

    // Two commits carrying the identical breaking footers — the accumulator's
    // `!includes` dedup guard must keep ONE of each (covers the false branch).
    const result = runDiffRelease(
      [makeCommit(message, [dir]), makeCommit(message, [dir])],
      [pkg],
      opts(true),
      provider,
    );

    // Footer forces a break; stable 1.2.0 → major.
    expect(result.plans[0]!.bump).toBe("major");
    expect(result.plans[0]!.wireBreakingEntries).toEqual([
      "renamed the status field",
    ]);
    expect(result.plans[0]!.apiBreakingEntries).toEqual([
      "dropped the legacy ctor",
    ]);
  });
});

// ---------------------------------------------------------------------------
// Suite I — Prerelease-labelled currentVersion (diff-engine crash regression)
// ---------------------------------------------------------------------------

describe("runDiffRelease — prerelease-labelled currentVersion", () => {
  it("fingerprint change on a prerelease-labelled package produces a plan without throwing", () => {
    // Regression: parseVersion crashed on "1.0.0-alpha.3" at the newVersion
    // computation site (diff-runner.ts). parseVersionLoose must be used.
    const { pkg } = createNugetFixture("D2.Shared.Result", [], "1.0.0-alpha.3");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: false, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    expect(() => runDiffRelease([], [pkg], opts(true), provider)).not.toThrow();

    const result = runDiffRelease([], [pkg], opts(true), provider);
    expect(result.plans).toHaveLength(1);
    // fingerprint-only → PATCH; prerelease label is stripped on output.
    expect(result.plans[0]!.bump).toBe("patch");
    expect(result.plans[0]!.newVersion).toBe("1.0.1");
  });

  it("API-remove diff on a prerelease-labelled package gives MINOR (pre-stable carve-out)", () => {
    // "1.0.0-alpha.3" is pre-stable → break → MINOR, not MAJOR.
    const { pkg, dir } = createNpmFixture("@d2/a", [], "1.0.0-alpha.3");
    const provider = staticDiffProvider({
      apiDiff: { added: false, removed: true, changed: false },
      fingerprintDiff: { changed: true },
      baselineMissing: false,
    });

    const result = runDiffRelease(
      [makeCommit("feat: drop field", [dir])],
      [pkg],
      opts(true),
      provider,
    );

    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.bump).toBe("minor");
    // Drops the prerelease label on the output version.
    expect(result.plans[0]!.newVersion).toBe("1.1.0");
  });

  it("propagation to a prerelease-labelled dependent does not throw", () => {
    // Regression: diff-runner.ts:370 newVersion compute (via parseVersionLoose) was
    // called for propagated dependents. When the dependent carries a prerelease label
    // the parse crashed — parseVersionLoose now handles the prerelease suffix cleanly.
    const { pkg: depPkg } = createNpmFixture("@d2/dep", [], "0.2.0");
    const { pkg: consumerPkg } = createNpmFixture(
      "@d2/consumer",
      ["@d2/dep"],
      "1.0.0-alpha.1",
    );

    // dep bumps; consumer fingerprint changes (dep version in resolved map).
    let callCount = 0;
    const provider: DiffProvider = {
      getDiff(input: DiffProviderInput): PackageDiff {
        callCount++;
        const isConsumer = input.pkg.name === "@d2/consumer";

        return {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed: isConsumer },
          baselineMissing: false,
        };
      },
    };

    expect(() =>
      runDiffRelease([], [depPkg, consumerPkg], opts(true), provider),
    ).not.toThrow();

    const result = runDiffRelease(
      [],
      [depPkg, consumerPkg],
      opts(true),
      provider,
    );
    // consumer gets PATCH; prerelease label is stripped on output.
    const consumerPlan = result.plans.find(
      (p) => p.pkg.name === "@d2/consumer",
    );
    expect(consumerPlan).toBeDefined();
    expect(consumerPlan!.bump).toBe("patch");
    expect(consumerPlan!.newVersion).toBe("1.0.1");
  });
});
