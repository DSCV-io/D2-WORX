// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Unit tests for the graduation engine.
//
// Covers: 0.x → 1.0.0 version + changelog promotion, refuse-when-already-stable,
// unknown-package fail-loud, and dry-run writes-nothing.

import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import {
  graduatePackage,
  buildGraduatedChangelogText,
} from "../src/graduate.js";
import type { PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Temp dir setup
// ---------------------------------------------------------------------------

let tempDir: string;

beforeEach(() => {
  tempDir = join(tmpdir(), `release-runner-graduate-${Date.now().toString()}`);
  mkdirSync(tempDir, { recursive: true });
});

afterEach(() => {
  rmSync(tempDir, { recursive: true, force: true });
});

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const SEEDED_CHANGELOG = `# Changelog — @d2/result

All notable changes to this package are documented here.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed`;

function makeNpmPkg(name: string, version: string): PackageDescriptor {
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

  return {
    name,
    ecosystem: "npm",
    dir: safeName,
    manifestPath,
    changelogPath,
    currentVersion: version,
  };
}

function makeNugetPkg(name: string, version: string): PackageDescriptor {
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

  return {
    name,
    ecosystem: "nuget",
    dir: name,
    manifestPath,
    changelogPath,
    currentVersion: version,
  };
}

// ---------------------------------------------------------------------------
// buildGraduatedChangelogText (pure — no filesystem IO)
// ---------------------------------------------------------------------------

describe("buildGraduatedChangelogText — pure transformation", () => {
  it("promotes [Unreleased] to 1.0.0 versioned heading with hyphen-minus separator", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");
    const result = buildGraduatedChangelogText(
      SEEDED_CHANGELOG,
      pkg,
      "2026-06-24",
    );

    expect(result).toContain("## 1.0.0 - 2026-06-24");
    expect(result).not.toContain("## [Unreleased]\n\n## 1.0.0");
  });

  it("inserts a fresh empty [Unreleased] block above the 1.0.0 section", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");
    const result = buildGraduatedChangelogText(
      SEEDED_CHANGELOG,
      pkg,
      "2026-06-24",
    );

    const unreleasedIdx = result.indexOf("## [Unreleased]");
    const versionedIdx = result.indexOf("## 1.0.0 - 2026-06-24");

    expect(unreleasedIdx).toBeGreaterThanOrEqual(0);
    expect(versionedIdx).toBeGreaterThanOrEqual(0);
    expect(unreleasedIdx).toBeLessThan(versionedIdx);
  });

  it("1.0.0 versioned section has no subsection entries (all empty entries omitted)", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");
    const result = buildGraduatedChangelogText(
      SEEDED_CHANGELOG,
      pkg,
      "2026-06-24",
    );

    const versionedIdx = result.indexOf("## 1.0.0 - 2026-06-24");
    const afterVersioned = result.slice(versionedIdx);
    const nextH2 = afterVersioned.indexOf("\n## ", 1);
    const versionedBody =
      nextH2 === -1 ? afterVersioned : afterVersioned.slice(0, nextH2);

    // Empty subsections should be omitted from the versioned block.
    expect(versionedBody).not.toContain("### Wire-breaking");
    expect(versionedBody).not.toContain("### API-breaking");
    expect(versionedBody).not.toContain("### Added");
    expect(versionedBody).not.toContain("### Fixed");
  });

  it("uses the injected today date", () => {
    const pkg = makeNpmPkg("@d2/result", "0.1.0");
    const result = buildGraduatedChangelogText(
      SEEDED_CHANGELOG,
      pkg,
      "2030-12-31",
    );
    expect(result).toContain("## 1.0.0 - 2030-12-31");
  });
});

// ---------------------------------------------------------------------------
// graduatePackage — unknown-package fail-loud
// ---------------------------------------------------------------------------

describe("graduatePackage — unknown package", () => {
  it("throws with the unknown package name when the inventory does not contain it", () => {
    const pkg = makeNpmPkg("@d2/result", "0.1.0");

    expect(() =>
      graduatePackage("@d2/nonexistent", [pkg], "2026-06-24", true),
    ).toThrow(/@d2\/nonexistent/);
  });

  it("error message lists available packages", () => {
    const pkg = makeNpmPkg("@d2/result", "0.1.0");

    expect(() =>
      graduatePackage("@d2/nonexistent", [pkg], "2026-06-24", true),
    ).toThrow(/@d2\/result/);
  });

  it("throws when the package inventory is empty", () => {
    expect(() => graduatePackage("@d2/result", [], "2026-06-24", true)).toThrow(
      /@d2\/result/,
    );
  });
});

// ---------------------------------------------------------------------------
// graduatePackage — refuse-when-already-stable
// ---------------------------------------------------------------------------

describe("graduatePackage — already stable", () => {
  it("throws when the package MAJOR is already 1", () => {
    const pkg = makeNpmPkg("@d2/result", "1.0.0");

    expect(() =>
      graduatePackage("@d2/result", [pkg], "2026-06-24", true),
    ).toThrow(/already stable/);
  });

  it("throws when the package MAJOR is > 1 (e.g. 2.3.1)", () => {
    const pkg = makeNpmPkg("@d2/result", "2.3.1");

    expect(() =>
      graduatePackage("@d2/result", [pkg], "2026-06-24", true),
    ).toThrow(/already stable/);
  });

  it("error message includes the package name and current version", () => {
    const pkg = makeNpmPkg("@d2/result", "1.2.3");

    expect(() =>
      graduatePackage("@d2/result", [pkg], "2026-06-24", true),
    ).toThrow(/@d2\/result/);
  });
});

// ---------------------------------------------------------------------------
// graduatePackage — dry-run writes nothing
// ---------------------------------------------------------------------------

describe("graduatePackage — dry-run", () => {
  it("returns applied=false in dry-run mode", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");

    const result = graduatePackage("@d2/result", [pkg], "2026-06-24", true);

    expect(result.applied).toBe(false);
  });

  it("reports newVersion as 1.0.0 even in dry-run", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");

    const result = graduatePackage("@d2/result", [pkg], "2026-06-24", true);

    expect(result.newVersion).toBe("1.0.0");
  });

  it("does not write manifest in dry-run (package.json unchanged)", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");
    const originalManifest = readFileSync(pkg.manifestPath, "utf-8");

    graduatePackage("@d2/result", [pkg], "2026-06-24", true);

    expect(readFileSync(pkg.manifestPath, "utf-8")).toBe(originalManifest);
  });

  it("does not write changelog in dry-run (CHANGELOG.md unchanged)", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");
    const originalChangelog = readFileSync(pkg.changelogPath, "utf-8");

    graduatePackage("@d2/result", [pkg], "2026-06-24", true);

    expect(readFileSync(pkg.changelogPath, "utf-8")).toBe(originalChangelog);
  });
});

// ---------------------------------------------------------------------------
// graduatePackage — npm apply mode
// ---------------------------------------------------------------------------

describe("graduatePackage — npm apply mode", () => {
  it("writes 1.0.0 to package.json version slot", () => {
    const pkg = makeNpmPkg("@d2/result", "0.5.0");

    graduatePackage("@d2/result", [pkg], "2026-06-24", false);

    const updated = JSON.parse(readFileSync(pkg.manifestPath, "utf-8")) as {
      version: string;
    };
    expect(updated.version).toBe("1.0.0");
  });

  it("promotes CHANGELOG [Unreleased] to ## 1.0.0 - <date>", () => {
    const pkg = makeNpmPkg("@d2/result", "0.1.0");

    graduatePackage("@d2/result", [pkg], "2026-06-24", false);

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("## 1.0.0 - 2026-06-24");
  });

  it("re-inserts a fresh [Unreleased] block above the 1.0.0 section", () => {
    const pkg = makeNpmPkg("@d2/result", "0.3.7");

    graduatePackage("@d2/result", [pkg], "2026-06-24", false);

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("## [Unreleased]");

    const unreleasedIdx = changelog.indexOf("## [Unreleased]");
    const versionedIdx = changelog.indexOf("## 1.0.0 - 2026-06-24");
    expect(unreleasedIdx).toBeLessThan(versionedIdx);
  });

  it("returns applied=true when changes are written", () => {
    const pkg = makeNpmPkg("@d2/result", "0.1.0");

    const result = graduatePackage("@d2/result", [pkg], "2026-06-24", false);

    expect(result.applied).toBe(true);
  });

  it("returns the correct pkg descriptor and newVersion", () => {
    const pkg = makeNpmPkg("@d2/result", "0.1.0");

    const result = graduatePackage("@d2/result", [pkg], "2026-06-24", false);

    expect(result.pkg.name).toBe("@d2/result");
    expect(result.newVersion).toBe("1.0.0");
  });

  it("graduates 0.0.1 (minimum valid pre-stable) to 1.0.0", () => {
    const pkg = makeNpmPkg("@d2/result", "0.0.1");

    graduatePackage("@d2/result", [pkg], "2026-06-24", false);

    const updated = JSON.parse(readFileSync(pkg.manifestPath, "utf-8")) as {
      version: string;
    };
    expect(updated.version).toBe("1.0.0");
  });
});

// ---------------------------------------------------------------------------
// graduatePackage — nuget apply mode
// ---------------------------------------------------------------------------

describe("graduatePackage — nuget apply mode", () => {
  it("writes 1.0.0 to <Version> element in .csproj", () => {
    const pkg = makeNugetPkg("D2.Shared.Result", "0.1.0");

    graduatePackage("D2.Shared.Result", [pkg], "2026-06-24", false);

    const updated = readFileSync(pkg.manifestPath, "utf-8");
    expect(updated).toContain("<Version>1.0.0</Version>");
  });

  it("promotes NuGet CHANGELOG [Unreleased] to ## 1.0.0 - <date>", () => {
    const pkg = makeNugetPkg("D2.Shared.Result", "0.1.0");

    graduatePackage("D2.Shared.Result", [pkg], "2026-06-24", false);

    const changelog = readFileSync(pkg.changelogPath, "utf-8");
    expect(changelog).toContain("## 1.0.0 - 2026-06-24");
    expect(changelog).toContain("## [Unreleased]");
  });
});

// ---------------------------------------------------------------------------
// graduatePackage — multi-package inventory
// ---------------------------------------------------------------------------

describe("graduatePackage — multi-package inventory", () => {
  it("graduates only the named package when multiple packages exist", () => {
    const pkgA = makeNpmPkg("@d2/a", "0.1.0");
    const pkgB = makeNpmPkg("@d2/b", "0.2.0");
    const originalB = readFileSync(pkgB.manifestPath, "utf-8");

    graduatePackage("@d2/a", [pkgA, pkgB], "2026-06-24", false);

    // Package B must be untouched.
    expect(readFileSync(pkgB.manifestPath, "utf-8")).toBe(originalB);

    // Package A must be graduated.
    const updatedA = JSON.parse(readFileSync(pkgA.manifestPath, "utf-8")) as {
      version: string;
    };
    expect(updatedA.version).toBe("1.0.0");
  });
});
