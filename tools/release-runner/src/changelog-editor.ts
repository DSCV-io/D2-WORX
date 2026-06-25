// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// CHANGELOG.md promotion engine.
//
// Given a seeded CHANGELOG with an `## [Unreleased]` section followed by
// the four empty subsections:
//
//   ## [Unreleased]
//
//   ### Wire-breaking
//
//   ### API-breaking
//
//   ### Added
//
//   ### Fixed
//
// The promoter:
//   1. Replaces `## [Unreleased]` with `## <newVersion> - <YYYY-MM-DD>`.
//   2. Populates the four subsections with the supplied entries (empty
//      subsections are DROPPED from the versioned block — keep the output
//      clean).
//   3. Inserts a fresh empty `## [Unreleased]` block (with all four empty
//      subsections) above the new versioned section.
//
// The result is written to the changelog file. All surrounding content
// (the header paragraph, any older versioned blocks) is preserved exactly.

import { readFileSync, writeFileSync } from "node:fs";
import type { BumpPlan } from "./types.js";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const UNRELEASED_HEADING = "## [Unreleased]";

const EMPTY_UNRELEASED_BLOCK = `## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Changed

### Fixed`;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Render a bullet list from an array of entry strings.
 *
 * Callers guard `entries.length > 0` before calling — this function always
 * receives a non-empty array.
 */
function renderBullets(entries: readonly string[]): string {
  return entries.map((e) => `- ${e}`).join("\n");
}

/**
 * Build the versioned section text for a bump plan.
 *
 * Only includes subsections that have entries — empty subsections are omitted.
 */
function buildVersionedSection(plan: BumpPlan, today: string): string {
  const heading = `## ${plan.newVersion} - ${today}`;
  const parts: string[] = [heading];

  if (plan.wireBreakingEntries.length > 0) {
    parts.push(
      `\n### Wire-breaking\n\n${renderBullets(plan.wireBreakingEntries)}`,
    );
  }

  if (plan.apiBreakingEntries.length > 0) {
    parts.push(
      `\n### API-breaking\n\n${renderBullets(plan.apiBreakingEntries)}`,
    );
  }

  if (plan.addedEntries.length > 0) {
    parts.push(`\n### Added\n\n${renderBullets(plan.addedEntries)}`);
  }

  if (plan.dependencyEntries.length > 0) {
    const bullets = plan.dependencyEntries.map(
      (upstream) => `Dependency update: ${upstream} bumped.`,
    );

    parts.push(`\n### Changed\n\n${renderBullets(bullets)}`);
  }

  if (plan.fixedEntries.length > 0) {
    parts.push(`\n### Fixed\n\n${renderBullets(plan.fixedEntries)}`);
  }

  return parts.join("");
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Promote the `## [Unreleased]` section of a CHANGELOG.md file to a
 * versioned release block and insert a fresh empty `## [Unreleased]` above it.
 *
 * @param changelogPath - Absolute path to the CHANGELOG.md file.
 * @param plan          - BumpPlan supplying newVersion + entry lists.
 * @param today         - Date string `YYYY-MM-DD` to stamp in the heading.
 *
 * @throws {Error} When the CHANGELOG does not contain `## [Unreleased]` —
 *                 fail-loud: a missing Unreleased section means the CHANGELOG
 *                 was not seeded correctly and the runner must not silently
 *                 corrupt it.
 */
export function promoteChangelog(
  changelogPath: string,
  plan: BumpPlan,
  today: string,
): void {
  const text = readFileSync(changelogPath, "utf-8");
  const updated = buildPromotedText(text, plan, today);

  writeFileSync(changelogPath, updated, "utf-8");
}

/**
 * Pure transformation of CHANGELOG text — exposed for unit testing without
 * filesystem IO.
 *
 * @throws {Error} When the text does not contain `## [Unreleased]`.
 */
export function buildPromotedText(
  text: string,
  plan: BumpPlan,
  today: string,
): string {
  const idx = text.indexOf(UNRELEASED_HEADING);

  if (idx === -1)
    throw new Error(
      `CHANGELOG for "${plan.pkg.name}" does not contain "${UNRELEASED_HEADING}". ` +
        `Ensure the CHANGELOG was seeded correctly before running the release runner.`,
    );

  // Everything before (and including) any content up to the Unreleased heading.
  const beforeUnreleased = text.slice(0, idx);

  // Find where the NEXT `## ` heading starts after the Unreleased heading, or
  // the end of file — this defines the old Unreleased block's extent.
  const afterHeadingStart = idx + UNRELEASED_HEADING.length;
  const nextH2 = text.indexOf("\n## ", afterHeadingStart);
  const afterOldBlock = nextH2 === -1 ? text.length : nextH2 + 1; // +1 to keep the "\n"

  // Everything from the next versioned block onwards (may be empty).
  const tail = text.slice(afterOldBlock);

  const versionedSection = buildVersionedSection(plan, today);

  // Assemble: header + fresh Unreleased block + versioned section + old tail.
  const assembled = [
    beforeUnreleased,
    EMPTY_UNRELEASED_BLOCK,
    "\n\n",
    versionedSection,
    tail.length > 0 && !tail.startsWith("\n") ? "\n" : "",
    tail,
  ].join("");

  return assembled;
}
