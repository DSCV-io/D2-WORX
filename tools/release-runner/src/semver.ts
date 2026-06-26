// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Semver utilities — parse, bump, and render version strings.
//
// Intentionally minimal: only the three-part MAJOR.MINOR.PATCH form used
// by this codebase. Prerelease labels (e.g. "1.0.0-alpha.3") are handled by
// `parseVersionLoose`, which strips the label and parses the numeric core.

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
 * For version strings that may carry a prerelease label (e.g. `1.0.0-alpha.3`),
 * use `parseVersionLoose` instead.
 */
export function parseVersion(raw: string): ParsedVersion {
  const trimmed = raw.trim();
  const SEMVER_RE = /^(\d+)\.(\d+)\.(\d+)$/;
  const match = SEMVER_RE.exec(trimmed);

  if (match === null)
    throw new Error(
      `Cannot parse version string "${trimmed}" — expected MAJOR.MINOR.PATCH`,
    );

  // Groups 1–3 are always present when the regex matches (each is a required
  // `\d+` group, not optional). Assert non-null: the regex cannot match without
  // all three groups being non-empty strings.
  const major = parseInt(match[1]!, 10);
  const minor = parseInt(match[2]!, 10);
  const patch = parseInt(match[3]!, 10);

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

/**
 * Parse a version string that may carry an optional prerelease label.
 *
 * Strips any `-<label>` suffix before parsing the `MAJOR.MINOR.PATCH`
 * numeric core, so `"1.0.0-alpha.3"` is treated as `"1.0.0"`. The bump
 * result (`applyBump`) will always be a clean `MAJOR.MINOR.PATCH` string —
 * prerelease labels are not preserved on the output version.
 *
 * Throws if the numeric core is not a valid three-part semver.
 */
export function parseVersionLoose(raw: string): ParsedVersion {
  const trimmed = raw.trim();
  // Strip everything from the first `-` onward (prerelease label + build metadata).
  const hyphenIdx = trimmed.indexOf("-");
  const core = hyphenIdx === -1 ? trimmed : trimmed.slice(0, hyphenIdx);

  return parseVersion(core);
}
