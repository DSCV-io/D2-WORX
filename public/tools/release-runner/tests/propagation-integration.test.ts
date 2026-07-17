// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Integration tests for dependency-graph propagation wired into runRelease.
//
// These tests drive runRelease with propagate:true (default behavior) and
// propagate:false (--no-propagate) to pin the propagation contract and the
// opt-out regression respectively.
//
// All tests use synthetic packages with temp-dir changelogs and manifests.
// No real git IO is involved.

import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { runRelease } from "../src/runner.js";
import type { PackageDescriptor, RunnerOptions } from "../src/types.js";

// ---------------------------------------------------------------------------
// Temp dir
// ---------------------------------------------------------------------------

let tempDir: string;

beforeEach(() => {
  tempDir = join(
    tmpdir(),
    `release-runner-propagation-${Date.now().toString()}`,
  );
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

function makeCommit(message: string, dirs: string[]) {
  return { message, files: dirs.map((d) => `${d}/src/index.ts`) };
}

function opts(
  dryRun: boolean,
  propagate: boolean,
  packageFilter?: string,
): RunnerOptions {
  return { today: "2026-06-24", dryRun, propagate, packageFilter };
}

// ---------------------------------------------------------------------------
// Default propagation (propagate:true)
// ---------------------------------------------------------------------------

describe("runRelease — propagation enabled (default)", () => {
  it("directly-bumped dependency patch-bumps untouched dependents", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture("@dcsv-io/d2-b", ["@dcsv-io/d2-a"]);

    const commits = [makeCommit("feat: add helper", [dirA])];
    const result = runRelease(commits, [pkgA, pkgB], opts(true, true));

    expect(result.plans).toHaveLength(2);

    const planA = result.plans.find((p) => p.pkg.name === "@dcsv-io/d2-a");
    const planB = result.plans.find((p) => p.pkg.name === "@dcsv-io/d2-b");

    expect(planA!.bump).toBe("minor");
    expect(planB!.bump).toBe("patch");
    expect(planB!.dependencyEntries).toContain("@dcsv-io/d2-a");
  });

  it("propagated plan writes PATCH version bump to manifest", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture(
      "@dcsv-io/d2-b",
      ["@dcsv-io/d2-a"],
      "0.2.0",
    );

    const commits = [makeCommit("feat: new thing", [dirA])];
    runRelease(commits, [pkgA, pkgB], opts(false, true));

    const updatedB = JSON.parse(readFileSync(pkgB.manifestPath, "utf-8")) as {
      version: string;
    };
    expect(updatedB.version).toBe("0.2.1"); // patch bump
  });

  it("propagated plan writes ### Changed section to CHANGELOG", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture("@dcsv-io/d2-b", ["@dcsv-io/d2-a"]);

    const commits = [makeCommit("feat: new feature", [dirA])];
    runRelease(commits, [pkgA, pkgB], opts(false, true));

    const changelog = readFileSync(pkgB.changelogPath, "utf-8");
    expect(changelog).toContain("### Changed");
    expect(changelog).toContain("Dependency update: @dcsv-io/d2-a bumped.");
  });

  it("directly-bumped package does NOT get a ### Changed section", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture("@dcsv-io/d2-b", ["@dcsv-io/d2-a"]);

    const commits = [makeCommit("feat: new feature", [dirA])];
    runRelease(commits, [pkgA, pkgB], opts(false, true));

    const changelogA = readFileSync(pkgA.changelogPath, "utf-8");
    // The versioned section for A must have ### Added but NOT a ### Changed section
    // with a dependency-update bullet.
    const versionedIdx = changelogA.indexOf("## 0.2.0 - 2026-06-24");
    const afterVersioned = changelogA.slice(versionedIdx);
    const nextH2 = afterVersioned.indexOf("\n## ", 1);
    const versionedBody =
      nextH2 === -1 ? afterVersioned : afterVersioned.slice(0, nextH2);

    expect(versionedBody).not.toContain("Dependency update:");
  });

  it("transitive chain (A → B → C) results in all three being planned", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture("@dcsv-io/d2-b", ["@dcsv-io/d2-a"]);
    const { pkg: pkgC } = createNpmFixture("@dcsv-io/d2-c", ["@dcsv-io/d2-b"]);

    const commits = [makeCommit("fix: core fix", [dirA])];
    const result = runRelease(commits, [pkgA, pkgB, pkgC], opts(true, true));

    expect(result.plans).toHaveLength(3);

    const bumps = Object.fromEntries(
      result.plans.map((p) => [p.pkg.name, p.bump]),
    );
    expect(bumps["@dcsv-io/d2-a"]).toBe("patch");
    expect(bumps["@dcsv-io/d2-b"]).toBe("patch");
    expect(bumps["@dcsv-io/d2-c"]).toBe("patch");
  });

  it("already-directly-bumped dependent keeps its own higher bump", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB, dir: dirB } = createNpmFixture("@dcsv-io/d2-b", [
      "@dcsv-io/d2-a",
    ]);

    // Both A and B are directly touched.
    const commits = [
      makeCommit("feat: A feature", [dirA]),
      makeCommit("feat: B feature", [dirB]),
    ];
    const result = runRelease(commits, [pkgA, pkgB], opts(true, true));

    expect(result.plans).toHaveLength(2);

    const planB = result.plans.find((p) => p.pkg.name === "@dcsv-io/d2-b");
    expect(planB!.bump).toBe("minor"); // own feat, not overwritten by patch propagation
    expect(planB!.dependencyEntries).toHaveLength(0); // NOT a dep-update plan
  });
});

// ---------------------------------------------------------------------------
// --no-propagate reproduces direct-only behavior
// ---------------------------------------------------------------------------

describe("runRelease — --no-propagate reproduces direct-only bumping", () => {
  it("with propagate:false, untouched dependents receive no plan", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture("@dcsv-io/d2-b", ["@dcsv-io/d2-a"]);

    const commits = [makeCommit("feat: add feature", [dirA])];
    const result = runRelease(commits, [pkgA, pkgB], opts(true, false));

    // Only A is bumped — B is NOT in the plan.
    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.pkg.name).toBe("@dcsv-io/d2-a");
  });

  it("with propagate:false, B's manifest is untouched in apply mode", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture(
      "@dcsv-io/d2-b",
      ["@dcsv-io/d2-a"],
      "0.5.0",
    );

    const commits = [makeCommit("feat: add feature", [dirA])];
    runRelease(commits, [pkgA, pkgB], opts(false, false));

    const updatedB = JSON.parse(readFileSync(pkgB.manifestPath, "utf-8")) as {
      version: string;
    };
    // B version unchanged at 0.5.0.
    expect(updatedB.version).toBe("0.5.0");
  });
});

// ---------------------------------------------------------------------------
// --package filter applied AFTER propagation
// ---------------------------------------------------------------------------

describe("runRelease — packageFilter applied after propagation", () => {
  it("--package B shows propagated PATCH plan for B even though A was directly bumped", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture("@dcsv-io/d2-b", ["@dcsv-io/d2-a"]);

    const commits = [makeCommit("feat: A feature", [dirA])];
    const result = runRelease(
      commits,
      [pkgA, pkgB],
      opts(true, true, "@dcsv-io/d2-b"),
    );

    // The filter restricts to @dcsv-io/d2-b. B was reachable via propagation.
    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.pkg.name).toBe("@dcsv-io/d2-b");
    expect(result.plans[0]!.bump).toBe("patch");
  });
});

// ---------------------------------------------------------------------------
// Changelog back-compat: seeded with OLD 4-subsection template
// ---------------------------------------------------------------------------

describe("runRelease — CHANGELOG back-compat (4-subsection seed)", () => {
  it("old 4-subsection seeded CHANGELOG still promotes correctly", () => {
    const OLD_CHANGELOG = `# Changelog

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed`;

    const { pkg: pkgA, dir: dirA } = createNpmFixture("@dcsv-io/d2-a");
    const { pkg: pkgB } = createNpmFixture("@dcsv-io/d2-b", ["@dcsv-io/d2-a"]);

    // Override B's changelog with the OLD 4-section template.
    writeFileSync(pkgB.changelogPath, OLD_CHANGELOG, "utf-8");

    const commits = [makeCommit("feat: A feature", [dirA])];

    // Should not throw — the promoter rewrites the whole Unreleased block.
    expect(() =>
      runRelease(commits, [pkgA, pkgB], opts(false, true)),
    ).not.toThrow();

    const changelog = readFileSync(pkgB.changelogPath, "utf-8");
    expect(changelog).toContain("## 0.1.1 - 2026-06-24"); // patch bump applied
    expect(changelog).toContain("### Changed"); // fresh block has 5 sections now
  });
});
