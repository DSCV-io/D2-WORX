// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Manifest version-slot editors — read and write the version field in npm
// package.json and NuGet .csproj files.
//
// Both editors operate on the raw file text so that key order, formatting,
// and comments are preserved exactly. They do NOT parse full JSON/XML object
// models — they perform targeted regex replacements on the version slot.

import { readFileSync, writeFileSync } from "node:fs";

// ---------------------------------------------------------------------------
// npm (package.json) adapter
// ---------------------------------------------------------------------------

// Matches `"version": "x.y.z"` with optional surrounding whitespace in values.
// Group 1 = the version string value.
const NPM_VERSION_RE = /("version"\s*:\s*")([^"]+)(")/;

/**
 * Read the `"version"` field from a `package.json` file.
 *
 * @throws {Error} When the version field is absent or unparseable.
 */
export function readNpmVersion(manifestPath: string): string {
  const text = readFileSync(manifestPath, "utf-8");
  const match = NPM_VERSION_RE.exec(text);

  if (match === null)
    throw new Error(
      `Cannot find "version" field in npm manifest: ${manifestPath}`,
    );

  // NPM_VERSION_RE group 2 requires [^"]+ (≥1 char) — non-empty by construction.
  // match[2] is always a non-empty string when NPM_VERSION_RE matches.
  const version = match[2]!;

  return version;
}

/**
 * Write a new version string into the `"version"` field of a `package.json`
 * file, preserving all surrounding content and formatting.
 *
 * @throws {Error} When the version field is absent (can't write what can't be found).
 */
export function writeNpmVersion(
  manifestPath: string,
  newVersion: string,
): void {
  const text = readFileSync(manifestPath, "utf-8");
  const match = NPM_VERSION_RE.exec(text);

  if (match === null)
    throw new Error(
      `Cannot find "version" field to update in npm manifest: ${manifestPath}`,
    );

  // Replace the captured version value (group 2) while preserving the
  // surrounding quote tokens (groups 1 and 3).
  const updated = text.replace(
    NPM_VERSION_RE,
    (_, prefix: string, _oldVersion: string, suffix: string) =>
      `${prefix}${newVersion}${suffix}`,
  );

  writeFileSync(manifestPath, updated, "utf-8");
}

// ---------------------------------------------------------------------------
// NuGet (.csproj) adapter
// ---------------------------------------------------------------------------

// Matches `<Version>x.y.z</Version>` with optional surrounding whitespace.
// Group 1 = the version string value.
const CSPROJ_VERSION_RE = /<Version>([^<]+)<\/Version>/;

/**
 * Read the `<Version>` element from a `.csproj` file.
 *
 * @throws {Error} When the Version element is absent or empty.
 */
export function readNugetVersion(manifestPath: string): string {
  const text = readFileSync(manifestPath, "utf-8");
  const match = CSPROJ_VERSION_RE.exec(text);

  if (match === null)
    throw new Error(
      `Cannot find <Version> element in NuGet manifest: ${manifestPath}`,
    );

  // CSPROJ_VERSION_RE group 1 requires [^<]+ (≥1 char) — non-empty by construction.
  // match[1] is always a non-empty string when CSPROJ_VERSION_RE matches.
  const version = match[1]!;

  return version.trim();
}

/**
 * Write a new version string into the `<Version>` element of a `.csproj`
 * file, preserving all surrounding content and formatting.
 *
 * @throws {Error} When the Version element is absent.
 */
export function writeNugetVersion(
  manifestPath: string,
  newVersion: string,
): void {
  const text = readFileSync(manifestPath, "utf-8");
  const match = CSPROJ_VERSION_RE.exec(text);

  if (match === null)
    throw new Error(
      `Cannot find <Version> element to update in NuGet manifest: ${manifestPath}`,
    );

  const updated = text.replace(
    CSPROJ_VERSION_RE,
    `<Version>${newVersion}</Version>`,
  );

  writeFileSync(manifestPath, updated, "utf-8");
}

// ---------------------------------------------------------------------------
// Unified facade used by the runner
// ---------------------------------------------------------------------------

/**
 * Read the current version from a manifest file.
 *
 * Delegates to the npm or NuGet adapter based on the file extension.
 *
 * @throws {Error} On parse failure or unknown extension.
 */
export function readManifestVersion(manifestPath: string): string {
  if (manifestPath.endsWith(".json")) return readNpmVersion(manifestPath);

  if (manifestPath.endsWith(".csproj")) return readNugetVersion(manifestPath);

  throw new Error(
    `Unknown manifest file extension: ${manifestPath}. Expected .json or .csproj.`,
  );
}

/**
 * Write a new version into a manifest file.
 *
 * Delegates to the npm or NuGet adapter based on the file extension.
 *
 * @throws {Error} On write failure or unknown extension.
 */
export function writeManifestVersion(
  manifestPath: string,
  newVersion: string,
): void {
  if (manifestPath.endsWith(".json")) {
    writeNpmVersion(manifestPath, newVersion);
    return;
  }

  if (manifestPath.endsWith(".csproj")) {
    writeNugetVersion(manifestPath, newVersion);
    return;
  }

  throw new Error(
    `Unknown manifest file extension: ${manifestPath}. Expected .json or .csproj.`,
  );
}
