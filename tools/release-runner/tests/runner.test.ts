// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Runner integration tests.
//
// These tests drive the full runner pipeline (bump engine → manifest editor →
// changelog editor) against temp-dir fixtures — no real git, no real manifests.
// All filesystem writes go to a temporary directory that is cleaned up after
// each test.

import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { runRelease } from "../src/runner.js";
import type {
  CommitRecord,
  PackageDescriptor,
  RunnerOptions,
} from "../src/types.js";

// ---------------------------------------------------------------------------
// Temp dir helpers
// ---------------------------------------------------------------------------

let tempDir: string;

beforeEach(() => {
  tempDir = join(tmpdir(), `release-runner-${Date.now().toString()}`);
  mkdirSync(tempDir, { recursive: true });
});

afterEach(() => {
  rmSync(tempDir, { recursive: true, force: true });
});

const SEEDED_CHANGELOG = `# Changelog

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed`;

function createNpmFixture(
  name: string,
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

  // Use the temp dir as the "dir" for path-containment matching.
  // CommitRecord files must start with this relative path.
  const relDir = `${safeName}`;

  return {
    pkg: {
      name,
      ecosystem: "npm",
      dir: relDir,
      manifestPath,
      changelogPath,
      currentVersion: version,
    },
    dir: relDir,
  };
}

function createNugetFixture(
  name: string,
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
    },
    dir: relDir,
  };
}

function makeCommit(message: string, relDirs: string[]): CommitRecord {
  // Build file paths using the relDir (which matches the pkg.dir prefix).
  return {
    message,
    files: relDirs.map((d) => `${d}/src/index.ts`),
  };
}

const opts = (dryRun: boolean, packageFilter?: string): RunnerOptions => ({
  today: "2026-06-24",
  dryRun,
  packageFilter,
});

// ---------------------------------------------------------------------------
// Dry-run mode
// ---------------------------------------------------------------------------

describe("runRelease — dry-run mode", () => {
  it("returns plans without writing any files", () => {
    const { pkg, dir } = createNpmFixture("@d2/result");
    const commits = [makeCommit("feat: add helper", [dir])];
    const originalManifest = readFileSync(pkg.manifestPath, "utf-8");
    const originalChangelog = readFileSync(pkg.changelogPath, "utf-8");

    const result = runRelease(commits, [pkg], opts(true));

    expect(result.plans).toHaveLength(1);
    expect(result.applied).toBe(false);

    // Files must be unchanged.
    expect(readFileSync(pkg.manifestPath, "utf-8")).toBe(originalManifest);
    expect(readFileSync(pkg.changelogPath, "utf-8")).toBe(originalChangelog);
  });

  it("dry-run reports the correct bump and new version", () => {
    const { pkg, dir } = createNpmFixture("@d2/result", "0.1.0");
    const commits = [makeCommit("feat: new feature", [dir])];

    const result = runRelease(commits, [pkg], opts(true));

    expect(result.plans[0]?.bump).toBe("minor");
    expect(result.plans[0]?.newVersion).toBe("0.2.0");
  });

  it("dry-run with no changed packages returns empty plans and applied=false", () => {
    const { pkg } = createNpmFixture("@d2/result");
    const commits = [makeCommit("chore: update deps", ["other/path/file.ts"])];

    const result = runRelease(commits, [pkg], opts(true));

    expect(result.plans).toHaveLength(0);
    expect(result.applied).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Apply mode — npm
// ---------------------------------------------------------------------------

describe("runRelease — apply mode (npm)", () => {
  it("writes the new version to package.json", () => {
    const { pkg, dir } = createNpmFixture("@d2/result", "0.1.0");
    const commits = [makeCommit("feat: extend API", [dir])];

    runRelease(commits, [pkg], opts(false));

    const updated = JSON.parse(readFileSync(pkg.manifestPath, "utf-8")) as {
      version: string;
    };
    expect(updated.version).toBe("0.2.0");
  });

  it("promotes CHANGELOG [Unreleased] to versioned section", () => {
    const { pkg, dir } = createNpmFixture("@d2/result", "0.1.0");
    const commits = [makeCommit("feat: new helper", [dir])];

    runRelease(commits, [pkg], opts(false));

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("## 0.2.0 - 2026-06-24");
    expect(changelog).toContain("## [Unreleased]");
  });

  it("CHANGELOG added entries appear in ### Added section", () => {
    const { pkg, dir } = createNpmFixture("@d2/result", "0.1.0");
    const commits = [makeCommit("feat: add ok factory", [dir])];

    runRelease(commits, [pkg], opts(false));

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("- add ok factory");
  });

  it("fix entry appears in ### Fixed section", () => {
    const { pkg, dir } = createNpmFixture("@d2/result", "0.1.0");
    const commits = [makeCommit("fix: correct edge case", [dir])];

    runRelease(commits, [pkg], opts(false));

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("- correct edge case");
    // Patch bump: 0.1.0 → 0.1.1
    expect(changelog).toContain("## 0.1.1 - 2026-06-24");
  });

  it("returns applied=true when changes are written", () => {
    const { pkg, dir } = createNpmFixture("@d2/result", "0.1.0");
    const commits = [makeCommit("feat: add helper", [dir])];

    const result = runRelease(commits, [pkg], opts(false));
    expect(result.applied).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Apply mode — nuget
// ---------------------------------------------------------------------------

describe("runRelease — apply mode (nuget)", () => {
  it("writes the new version to .csproj <Version> element", () => {
    const { pkg, dir } = createNugetFixture("D2.Shared.Result", "0.1.0");
    const commits = [makeCommit("feat: add method", [dir])];

    runRelease(commits, [pkg], opts(false));

    const updated = readFileSync(pkg.manifestPath, "utf-8");
    expect(updated).toContain("<Version>0.2.0</Version>");
  });

  it("promotes NuGet package CHANGELOG correctly", () => {
    const { pkg, dir } = createNugetFixture("D2.Shared.Result", "0.1.0");
    const commits = [makeCommit("fix: null ref", [dir])];

    runRelease(commits, [pkg], opts(false));

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("## 0.1.1 - 2026-06-24");
    expect(changelog).toContain("## [Unreleased]");
  });
});

// ---------------------------------------------------------------------------
// Single-package filter
// ---------------------------------------------------------------------------

describe("runRelease — packageFilter", () => {
  it("only processes the specified package", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@d2/a");
    const { pkg: pkgB, dir: dirB } = createNpmFixture("@d2/b");

    const commits = [
      makeCommit("feat: change in A", [dirA]),
      makeCommit("feat: change in B", [dirB]),
    ];

    const result = runRelease(commits, [pkgA, pkgB], {
      today: "2026-06-24",
      dryRun: true,
      packageFilter: "@d2/a",
    });

    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]?.pkg.name).toBe("@d2/a");
  });

  it("returns empty plans when packageFilter matches no packages", () => {
    const { pkg, dir } = createNpmFixture("@d2/a");
    const commits = [makeCommit("feat: change", [dir])];

    const result = runRelease(commits, [pkg], {
      today: "2026-06-24",
      dryRun: true,
      packageFilter: "@d2/nonexistent",
    });

    expect(result.plans).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Multi-package apply
// ---------------------------------------------------------------------------

describe("runRelease — multi-package apply", () => {
  it("independently bumps each touched package", () => {
    const { pkg: pkgA, dir: dirA } = createNpmFixture("@d2/a", "0.1.0");
    const { pkg: pkgB, dir: dirB } = createNpmFixture("@d2/b", "0.3.0");

    const commits = [
      makeCommit("feat: A new feature", [dirA]),
      makeCommit("fix: B bug fix", [dirB]),
    ];

    runRelease(commits, [pkgA, pkgB], opts(false));

    const updatedA = JSON.parse(readFileSync(pkgA.manifestPath, "utf-8")) as {
      version: string;
    };
    const updatedB = JSON.parse(readFileSync(pkgB.manifestPath, "utf-8")) as {
      version: string;
    };

    expect(updatedA.version).toBe("0.2.0"); // feat → minor
    expect(updatedB.version).toBe("0.3.1"); // fix → patch
  });
});

// ---------------------------------------------------------------------------
// No-op cases
// ---------------------------------------------------------------------------

describe("runRelease — no-op cases", () => {
  it("returns empty plans and applied=false when no commits touch consumable packages", () => {
    const { pkg } = createNpmFixture("@d2/a");
    const commits = [
      makeCommit("ci: update workflow", ["infra/ci/workflow.yml"]),
    ];

    const result = runRelease(commits, [pkg], opts(false));
    expect(result.plans).toHaveLength(0);
    expect(result.applied).toBe(false);
  });

  it("returns applied=false when plans is empty (no writes)", () => {
    const { pkg } = createNpmFixture("@d2/a");
    const result = runRelease([], [pkg], opts(false));
    expect(result.applied).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Wire-breaking entry in CHANGELOG
// ---------------------------------------------------------------------------

describe("runRelease — wire-breaking changelog entry", () => {
  it("wire-breaking entry appears under ### Wire-breaking in versioned block", () => {
    const { pkg, dir } = createNpmFixture("@d2/a", "0.1.0");

    const commits = [
      {
        message: [
          "feat: drop field",
          "",
          "Breaking wire change.",
          "",
          "WIRE-BREAKING: dropped field 3 from FooRequest",
        ].join("\n"),
        files: [`${dir}/src/index.ts`],
      },
    ];

    runRelease(commits, [pkg], opts(false));

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain(
      "### Wire-breaking\n\n- dropped field 3 from FooRequest",
    );
  });
});
