// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Graduation engine — promotes a pre-stable package from 0.x.y to 1.0.0.
//
// Graduation is the deliberate human act of cutting a pre-stable package
// to its first stable release. It sets the version to exactly 1.0.0 and
// promotes the CHANGELOG [Unreleased] section to a versioned 1.0.0 block.
//
// Unlike the regular bump path, graduation is not inferred from commits —
// it is triggered explicitly via the --graduate <pkg> flag.

import { writeManifestVersion } from "./manifest-editor.js";
import { buildPromotedText, promoteChangelog } from "./changelog-editor.js";
import { parseVersion } from "./semver.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Result type
// ---------------------------------------------------------------------------

/**
 * The outcome of a graduate invocation for one package.
 */
export interface GraduateResult {
  /** The package that was (or would be) graduated. */
  readonly pkg: PackageDescriptor;
  /** The version written (always "1.0.0"). */
  readonly newVersion: string;
  /** True when the version and CHANGELOG were actually written to disk. */
  readonly applied: boolean;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Promote a pre-stable package from its current 0.x.y version to 1.0.0,
 * writing the version slot and promoting the CHANGELOG [Unreleased] section.
 *
 * @param packageName - The name of the package to graduate (must be known).
 * @param packages    - Full consumable package inventory (same shape the
 *                      runner receives from the manifest loader).
 * @param today       - ISO date string `YYYY-MM-DD` stamped into the CHANGELOG.
 * @param dryRun      - When true, compute and report without writing any files.
 *
 * @throws {Error} When the package name is not found in the inventory.
 * @throws {Error} When the package is already at MAJOR >= 1 ("already stable").
 */
export function graduatePackage(
  packageName: string,
  packages: readonly PackageDescriptor[],
  today: string,
  dryRun: boolean,
): GraduateResult {
  const pkg = packages.find((p) => p.name === packageName);

  if (pkg === undefined)
    throw new Error(
      `Unknown package "${packageName}". ` +
        `Available packages: ${packages.map((p) => p.name).join(", ")}`,
    );

  const parsed = parseVersion(pkg.currentVersion);

  if (parsed.major >= 1)
    throw new Error(
      `Package "${packageName}" is already stable at v${pkg.currentVersion}. Nothing to graduate.`,
    );

  const newVersion = "1.0.0";

  if (dryRun) {
    return { pkg, newVersion, applied: false };
  }

  writeManifestVersion(pkg.manifestPath, newVersion);
  promoteChangelog(
    pkg.changelogPath,
    {
      pkg,
      bump: "major",
      newVersion,
      wireBreakingEntries: [],
      apiBreakingEntries: [],
      addedEntries: [],
      fixedEntries: [],
      dependencyEntries: [],
    },
    today,
  );

  return { pkg, newVersion, applied: true };
}

// ---------------------------------------------------------------------------
// Pure changelog transformation (for unit testing without filesystem IO)
// ---------------------------------------------------------------------------

/**
 * Build the graduated CHANGELOG text without writing to disk.
 *
 * Delegates to `buildPromotedText` with a synthetic 1.0.0 plan — exposed
 * for unit testing.
 */
export function buildGraduatedChangelogText(
  changelogText: string,
  pkg: PackageDescriptor,
  today: string,
): string {
  return buildPromotedText(
    changelogText,
    {
      pkg,
      bump: "major",
      newVersion: "1.0.0",
      wireBreakingEntries: [],
      apiBreakingEntries: [],
      addedEntries: [],
      fixedEntries: [],
      dependencyEntries: [],
    },
    today,
  );
}
