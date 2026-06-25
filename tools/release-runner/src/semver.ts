// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Semver utilities — parse, bump, and render version strings.
//
// Intentionally minimal: only the three-part MAJOR.MINOR.PATCH form used
// by this codebase. No pre-release / build-metadata handling required.

import type { BumpKind } from "./types.js";

// ---------------------------------------------------------------------------
// Parsed version
// ---------------------------------------------------------------------------

export interface ParsedVersion {
  readonly major: number;
  readonly minor: number;
  readonly patch: number;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Parse a `MAJOR.MINOR.PATCH` version string.
 *
 * Throws if the string is not a valid three-part semver.
 */
export function parseVersion(raw: string): ParsedVersion {
  const trimmed = raw.trim();
  const SEMVER_RE = /^(\d+)\.(\d+)\.(\d+)$/;
  const match = SEMVER_RE.exec(trimmed);

  if (match === null)
    throw new Error(
      `Cannot parse version string "${trimmed}" — expected MAJOR.MINOR.PATCH`,
    );

  // Destructure — groups 1–3 are always present when the regex matches
  // (each is a required `\d+` group, not optional). TypeScript types them as
  // `string | undefined` due to noUncheckedIndexedAccess; we assert non-null
  // because the regex cannot match without all three groups being non-empty.
  const [, rawMajor, rawMinor, rawPatch] = match as [
    string,
    string,
    string,
    string,
  ];

  const major = parseInt(rawMajor, 10);
  const minor = parseInt(rawMinor, 10);
  const patch = parseInt(rawPatch, 10);

  return { major, minor, patch };
}

/**
 * Apply a bump level to a parsed version and return the new version string.
 *
 * Bump rules:
 * - `major`: increment MAJOR, reset MINOR + PATCH to 0.
 * - `minor`: increment MINOR, reset PATCH to 0.
 * - `patch`: increment PATCH.
 * - `none`:  return the current version string unchanged.
 */
export function applyBump(parsed: ParsedVersion, bump: BumpKind): string {
  if (bump === "none") return renderVersion(parsed);

  if (bump === "major")
    return renderVersion({ major: parsed.major + 1, minor: 0, patch: 0 });

  if (bump === "minor")
    return renderVersion({
      major: parsed.major,
      minor: parsed.minor + 1,
      patch: 0,
    });

  return renderVersion({
    major: parsed.major,
    minor: parsed.minor,
    patch: parsed.patch + 1,
  });
}

/** Format a `ParsedVersion` back to a `MAJOR.MINOR.PATCH` string. */
export function renderVersion(v: ParsedVersion): string {
  return `${v.major.toString()}.${v.minor.toString()}.${v.patch.toString()}`;
}
