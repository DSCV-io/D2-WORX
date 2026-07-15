// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Package-list formatter for the --list CLI mode.
//
// Pure, side-effect-free module: receives PackageDescriptor[] and returns
// the JSON string to write to stdout. Extracted from cli.ts so it can be
// imported and tested directly without triggering CLI side effects.

import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// ListEntry — the shape written to stdout / shipped in manifest.json
// ---------------------------------------------------------------------------

/** A single entry in the --list JSON output. */
export interface ListEntry {
  /** Package name (e.g. "@dcsv-io/d2-result", "DcsvIo.D2.Result"). */
  readonly name: string;
  /** Ecosystem discriminator. */
  readonly ecosystem: "npm" | "nuget";
  /** Repo-root-relative directory that owns the manifest file. */
  readonly dir: string;
  /** Absolute path to the manifest file (package.json / .csproj). */
  readonly manifestPath: string;
  /** Current version string parsed from the manifest (e.g. "0.1.0"). */
  readonly currentVersion: string;
}

// ---------------------------------------------------------------------------
// formatPackageList
// ---------------------------------------------------------------------------

/**
 * Format the consumable package inventory as a JSON string suitable for
 * writing to stdout or saving as manifest.json.
 *
 * The output is an array of {@link ListEntry} objects — one per package,
 * in the order supplied (the manifest-loader already sorts by name).
 *
 * @param packages - Non-empty array of PackageDescriptors from loadAllPackages.
 * @returns JSON string (with trailing newline) representing the inventory.
 */
export function formatPackageList(
  packages: readonly PackageDescriptor[],
): string {
  const entries: ListEntry[] = packages.map((p) => ({
    name: p.name,
    ecosystem: p.ecosystem,
    dir: p.dir,
    manifestPath: p.manifestPath,
    currentVersion: p.currentVersion,
  }));

  return JSON.stringify(entries, undefined, 2) + "\n";
}
