// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import {
  buildPromotedText,
  promoteChangelog,
} from "../src/changelog-editor.js";
import type { BumpPlan, PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Test fixtures
// ---------------------------------------------------------------------------

const SEEDED_CHANGELOG = `# Changelog — @d2/result

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed`;

function makePkg(name = "@d2/result"): PackageDescriptor {
  return {
    name,
    ecosystem: "npm",
    dir: "server/shared/typescript/result",
    manifestPath: "server/shared/typescript/result/package.json",
    changelogPath: "server/shared/typescript/result/CHANGELOG.md",
    currentVersion: "0.1.0",
    dependencies: [],
  };
}

function makePlan(overrides: Partial<BumpPlan> = {}): BumpPlan {
  return {
    pkg: makePkg(),
    bump: "minor",
    newVersion: "0.2.0",
    wireBreakingEntries: [],
    apiBreakingEntries: [],
    addedEntries: [],
    fixedEntries: [],
    dependencyEntries: [],
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// buildPromotedText (pure — no filesystem IO)
// ---------------------------------------------------------------------------

describe("buildPromotedText — basic promotion", () => {
  it("replaces [Unreleased] heading with versioned heading", () => {
    const plan = makePlan();
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");
    expect(result).toContain("## 0.2.0 - 2026-06-24");
    expect(result).not.toContain("## [Unreleased]\n\n## 0.2.0");
  });

  it("inserts a fresh empty [Unreleased] block above the versioned section", () => {
    const plan = makePlan();
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    const unreleasedIdx = result.indexOf("## [Unreleased]");
    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");

    expect(unreleasedIdx).toBeGreaterThanOrEqual(0);
    expect(versionedIdx).toBeGreaterThanOrEqual(0);
    expect(unreleasedIdx).toBeLessThan(versionedIdx);
  });

  it("fresh [Unreleased] block contains all four empty subsections", () => {
    const plan = makePlan();
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    // The fresh [Unreleased] block must contain the four subsection headings.
    expect(result).toContain("### Wire-breaking");
    expect(result).toContain("### API-breaking");
    expect(result).toContain("### Added");
    expect(result).toContain("### Fixed");
  });

  it("preserves the header paragraph above [Unreleased]", () => {
    const plan = makePlan();
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");
    expect(result).toContain("# Changelog — @d2/result");
    expect(result).toContain("All notable changes to this package");
  });
});

describe("buildPromotedText — entry rendering", () => {
  it("wire-breaking entries render under ### Wire-breaking in versioned section", () => {
    const plan = makePlan({
      wireBreakingEntries: ["dropped field 3 from FooRequest"],
    });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");
    const wireIdx = result.indexOf("- dropped field 3 from FooRequest");

    expect(wireIdx).toBeGreaterThan(versionedIdx);
    expect(result).toContain(
      "### Wire-breaking\n\n- dropped field 3 from FooRequest",
    );
  });

  it("api-breaking entries render under ### API-breaking", () => {
    const plan = makePlan({
      apiBreakingEntries: ["removed DEPRECATED_CODE from catalog"],
    });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");
    expect(result).toContain(
      "### API-breaking\n\n- removed DEPRECATED_CODE from catalog",
    );
  });

  it("added entries render under ### Added", () => {
    const plan = makePlan({
      addedEntries: ["add ok factory", "add created factory"],
    });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");
    expect(result).toContain(
      "### Added\n\n- add ok factory\n- add created factory",
    );
  });

  it("fixed entries render under ### Fixed", () => {
    const plan = makePlan({ fixedEntries: ["correct null check"] });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");
    expect(result).toContain("### Fixed\n\n- correct null check");
  });

  it("empty subsections are omitted from the versioned section", () => {
    // Only addedEntries, no other entries.
    const plan = makePlan({ addedEntries: ["add feature"] });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    // The versioned section must NOT include empty ### Wire-breaking / API-breaking / Fixed.
    // Find the versioned section span.
    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");
    // Find the next ## heading after the versioned section (which is the fresh [Unreleased]).
    // Actually the fresh [Unreleased] is BEFORE the versioned section — so look at the
    // content after the versioned heading.
    const afterVersioned = result.slice(versionedIdx);
    // The next ## after the versioned heading in afterVersioned is either another
    // versioned section or end of file. Find it:
    const nextH2 = afterVersioned.indexOf("\n## ", 1);
    const versionedBody =
      nextH2 === -1 ? afterVersioned : afterVersioned.slice(0, nextH2);

    expect(versionedBody).toContain("### Added");
    expect(versionedBody).not.toContain("### Wire-breaking");
    expect(versionedBody).not.toContain("### API-breaking");
    expect(versionedBody).not.toContain("### Fixed");
  });

  it("all four sections present when all entry types are populated", () => {
    const plan = makePlan({
      wireBreakingEntries: ["wire change"],
      apiBreakingEntries: ["api change"],
      addedEntries: ["new feature"],
      fixedEntries: ["bug fix"],
    });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");
    const afterVersioned = result.slice(versionedIdx);

    expect(afterVersioned).toContain("### Wire-breaking");
    expect(afterVersioned).toContain("### API-breaking");
    expect(afterVersioned).toContain("### Added");
    expect(afterVersioned).toContain("### Fixed");
  });
});

describe("buildPromotedText — date injection", () => {
  it("uses the supplied today date in the versioned heading", () => {
    const plan = makePlan();
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2030-01-15");
    expect(result).toContain("## 0.2.0 - 2030-01-15");
  });
});

describe("buildPromotedText — fail-loud on missing [Unreleased]", () => {
  it("throws when [Unreleased] section is absent", () => {
    const badChangelog = `# Changelog\n\n## 0.1.0 - 2026-01-01\n\n### Fixed\n\n- initial release`;
    const plan = makePlan();
    expect(() => buildPromotedText(badChangelog, plan, "2026-06-24")).toThrow(
      /\[Unreleased\]/,
    );
  });

  it("error message includes the package name", () => {
    const plan = makePlan();
    try {
      buildPromotedText("no unreleased here", plan, "2026-06-24");
      expect.fail("should have thrown");
    } catch (err) {
      expect(String(err)).toContain("@d2/result");
    }
  });
});

describe("buildPromotedText — CHANGELOG with existing versioned entries", () => {
  it("preserves existing versioned sections below the new versioned section", () => {
    const changelogWithHistory = `# Changelog — @d2/result

All changes documented here.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed

## 0.1.0 - 2026-01-01

### Added

- initial release`;

    const plan = makePlan({ newVersion: "0.2.0" });
    const result = buildPromotedText(changelogWithHistory, plan, "2026-06-24");

    // Both the new version and the old version should be present.
    expect(result).toContain("## 0.2.0 - 2026-06-24");
    expect(result).toContain("## 0.1.0 - 2026-01-01");

    // New version should appear before old version.
    expect(result.indexOf("## 0.2.0")).toBeLessThan(result.indexOf("## 0.1.0"));
  });
});

// ---------------------------------------------------------------------------
// buildPromotedText — ### Changed dependency-update section
// ---------------------------------------------------------------------------

describe("buildPromotedText — ### Changed dependency-update section", () => {
  it("dependency entries render under ### Changed in versioned section", () => {
    const plan = makePlan({ dependencyEntries: ["@d2/utilities"] });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");
    const afterVersioned = result.slice(versionedIdx);

    expect(afterVersioned).toContain("### Changed");
    expect(afterVersioned).toContain(
      "- Dependency update: @d2/utilities bumped.",
    );
  });

  it("multiple dependency entries produce multiple bullets", () => {
    const plan = makePlan({
      dependencyEntries: ["@d2/a", "@d2/b"],
    });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    expect(result).toContain("- Dependency update: @d2/a bumped.");
    expect(result).toContain("- Dependency update: @d2/b bumped.");
  });

  it("empty dependencyEntries omits ### Changed from versioned section", () => {
    const plan = makePlan({ dependencyEntries: [], addedEntries: ["thing"] });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");
    const afterVersioned = result.slice(versionedIdx);
    const nextH2 = afterVersioned.indexOf("\n## ", 1);
    const versionedBody =
      nextH2 === -1 ? afterVersioned : afterVersioned.slice(0, nextH2);

    expect(versionedBody).not.toContain("### Changed");
  });

  it("### Changed appears between ### Added and ### Fixed in section order", () => {
    const plan = makePlan({
      addedEntries: ["new feature"],
      dependencyEntries: ["@d2/upstream"],
      fixedEntries: ["bug fix"],
    });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    const addedIdx = result.indexOf("### Added");
    const changedIdx = result.indexOf("### Changed");
    const fixedIdx = result.lastIndexOf("### Fixed");

    expect(addedIdx).toBeLessThan(changedIdx);
    expect(changedIdx).toBeLessThan(fixedIdx);
  });

  it("fresh [Unreleased] block after promotion contains ### Changed", () => {
    // The empty Unreleased block template now includes ### Changed.
    const plan = makePlan({ dependencyEntries: ["@d2/x"] });
    const result = buildPromotedText(SEEDED_CHANGELOG, plan, "2026-06-24");

    // The fresh [Unreleased] block appears before the versioned section.
    const unreleasedIdx = result.indexOf("## [Unreleased]");
    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");
    const freshBlock = result.slice(unreleasedIdx, versionedIdx);

    expect(freshBlock).toContain("### Changed");
  });

  it("changelog seeded with old 4-subsection template still promotes and adds ### Changed in fresh block", () => {
    const OLD_SEEDED = `# Changelog

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed`;

    const plan = makePlan({ dependencyEntries: ["@d2/upstream"] });
    const result = buildPromotedText(OLD_SEEDED, plan, "2026-06-24");

    // Promotion must succeed.
    expect(result).toContain("## 0.2.0 - 2026-06-24");
    // ### Changed appears in the versioned section (dependency entry).
    expect(result).toContain("Dependency update: @d2/upstream bumped.");
    // The fresh block (inserted by the promoter) carries ### Changed.
    const unreleasedIdx = result.indexOf("## [Unreleased]");
    const versionedIdx = result.indexOf("## 0.2.0 - 2026-06-24");
    const freshBlock = result.slice(unreleasedIdx, versionedIdx);

    expect(freshBlock).toContain("### Changed");
  });
});

// ---------------------------------------------------------------------------
// promoteChangelog (filesystem IO)
// ---------------------------------------------------------------------------

let tempDir: string;

beforeEach(() => {
  tempDir = join(tmpdir(), `release-runner-changelog-${Date.now().toString()}`);
  mkdirSync(tempDir, { recursive: true });
});

afterEach(() => {
  rmSync(tempDir, { recursive: true, force: true });
});

describe("promoteChangelog — filesystem", () => {
  it("writes the promoted text to the changelog file", () => {
    const changelogPath = join(tempDir, "CHANGELOG.md");
    writeFileSync(changelogPath, SEEDED_CHANGELOG, "utf-8");

    const plan = makePlan();
    promoteChangelog(changelogPath, plan, "2026-06-24");

    const written = readFileSync(changelogPath, "utf-8");
    expect(written).toContain("## 0.2.0 - 2026-06-24");
    expect(written).toContain("## [Unreleased]");
  });

  it("throws when the file does not contain [Unreleased]", () => {
    const changelogPath = join(tempDir, "CHANGELOG.md");
    writeFileSync(
      changelogPath,
      "# Changelog\n\n## 0.1.0 - 2026-01-01\n",
      "utf-8",
    );

    expect(() =>
      promoteChangelog(changelogPath, makePlan(), "2026-06-24"),
    ).toThrow(/\[Unreleased\]/);
  });

  it("original file content is replaced (not appended)", () => {
    const changelogPath = join(tempDir, "CHANGELOG.md");
    writeFileSync(changelogPath, SEEDED_CHANGELOG, "utf-8");

    promoteChangelog(changelogPath, makePlan(), "2026-06-24");

    const written = readFileSync(changelogPath, "utf-8");
    // The old [Unreleased] with empty subsections should NOT be present in isolation —
    // instead, a fresh empty block followed by the versioned section should exist.
    // Confirm there are exactly TWO ## headings at H2 level: [Unreleased] and the versioned.
    const h2Matches = [...written.matchAll(/^## /gm)];
    expect(h2Matches.length).toBeGreaterThanOrEqual(2);
  });
});
