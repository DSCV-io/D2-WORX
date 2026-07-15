// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Pure bump-derivation function — artifact-diff versioning engine core.
//
// Derives the semver bump kind from three orthogonal signals:
//   1. ApiDiff    — public API surface changes (added / removed / changed).
//   2. FingerprintDiff — output fingerprint change (compiled bytes / manifest).
//   3. BreakingFooter  — author-declared override via WIRE-BREAKING: / BREAKING CHANGE:.
//
// The footer can only ESCALATE; it never lowers the diff-derived verdict.
// The pre-stable carve-out (MAJOR===0 or any prerelease label) caps break→MINOR.
//
// No IO — all inputs are injected. The full transition matrix is unit-testable
// with synthetic values (see tests/diff-bump.test.ts).
//
// Pre-stable detection:
//   A version is pre-stable when its MAJOR component is 0 (e.g. "0.4.0") OR
//   when it carries a prerelease label (e.g. "1.0.0-alpha.3", "2.0.0-beta.1").
//   Rationale: a prerelease label signals the package is not yet committed to
//   its public API contract even when MAJOR ≥ 1. Both cases apply the carve-out
//   (break → MINOR, not MAJOR). This mirrors SemVer §4 ("Major version zero
//   (0.y.z) is for initial development") and §9 ("A pre-release version
//   indicates that the version is unstable").
//
// `parseVersion` from semver.ts is intentionally NOT used here — it rejects
// prerelease-tagged strings and is scoped to MAJOR.MINOR.PATCH only. This
// module implements its own minimal pre-stability detector that accepts the
// full range of version strings the engine may encounter.

import { falsey } from "@d2/utilities";
import type { BumpKind } from "./types.js";

// ---------------------------------------------------------------------------
// Input types
// ---------------------------------------------------------------------------

/**
 * Describes the diff of the public API surface against the committed baseline.
 *
 * `added`   — one or more public members are NEW since the baseline.
 * `removed` — one or more public members that existed in the baseline are GONE.
 * `changed` — one or more public members had their signature/type changed.
 *
 * Combinations are valid (e.g. a rename is `removed + added`). When both
 * `removed`/`changed` AND `added` are true, the break signal wins.
 */
export interface ApiDiff {
  readonly added: boolean;
  readonly removed: boolean;
  readonly changed: boolean;
}

/**
 * Describes whether the compiled output fingerprint differs from the committed
 * baseline. A `true` value means the publishable artifact would differ — even
 * if the public API surface is identical (internal change, dep-pin bump, etc.).
 */
export interface FingerprintDiff {
  readonly changed: boolean;
}

/**
 * The breaking-change footer signals extracted from commit messages.
 *
 * Mirrors the `BreakingValve` shape from `contract-gate/footer-parser`; typed
 * here independently so `diff-bump` has no runtime dependency on that package
 * (the runner's footer-aggregation adapter wires the two together).
 */
export interface BreakingFooter {
  /** True when at least one WIRE-BREAKING: / BREAKING CHANGE: / type!: was found. */
  readonly forced: boolean;
  /** Descriptions from WIRE-BREAKING: footers (wire axis). */
  readonly wireBreaking: readonly string[];
  /** Descriptions from BREAKING CHANGE: / type!: footers (api axis). */
  readonly apiBreaking: readonly string[];
}

// ---------------------------------------------------------------------------
// Internal — intent levels (pre-carve-out)
// ---------------------------------------------------------------------------

/** Ordered bump-intent levels before the pre-stable carve-out is applied. */
type BumpIntent = "none" | "patch" | "minor" | "break";

const _INTENT_RANK: Record<BumpIntent, number> = {
  none: 0,
  patch: 1,
  minor: 2,
  break: 3,
};

function maxIntent(a: BumpIntent, b: BumpIntent): BumpIntent {
  return _INTENT_RANK[a] >= _INTENT_RANK[b] ? a : b;
}

// ---------------------------------------------------------------------------
// Internal — pre-stability detection
// ---------------------------------------------------------------------------

/**
 * Returns true when `version` is a pre-stable package:
 *   - MAJOR is 0 (e.g. "0.4.0"), OR
 *   - The version string carries a prerelease label after a `-`
 *     (e.g. "1.0.0-alpha.3", "2.0.0-beta.1").
 *
 * Accepts any non-empty version string; malformed strings are treated as
 * pre-stable (fail-safe: a bump caps at MINOR rather than firing a MAJOR
 * on a string whose stability could not be determined).
 */
export function isPreStable(version: string): boolean {
  if (falsey(version)) return true;

  // Extract the MAJOR component (digits before the first dot).
  const dotIdx = version.indexOf(".");
  const majorStr = dotIdx === -1 ? version : version.slice(0, dotIdx);
  const major = parseInt(majorStr, 10);

  if (isNaN(major) || major === 0) return true;

  // A prerelease label is present when the version contains a `-` after the
  // numeric part (e.g. "1.0.0-alpha"). We consider ANY hyphen-delimited suffix
  // a prerelease label regardless of format.
  return version.includes("-");
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Derive the semver bump kind for a package given its artifact diff and the
 * commit footer.
 *
 * @param input.apiDiff         - Public API surface diff vs committed baseline.
 * @param input.fingerprintDiff - Output fingerprint diff vs committed baseline.
 * @param input.currentVersion  - The package's current version string.
 * @param input.footer          - Breaking-change footer signals from commit messages.
 * @returns The bump kind to apply: `"none"` | `"patch"` | `"minor"` | `"major"`.
 */
export function deriveBump(input: {
  readonly apiDiff: ApiDiff;
  readonly fingerprintDiff: FingerprintDiff;
  readonly currentVersion: string;
  readonly footer: BreakingFooter;
}): BumpKind {
  const { apiDiff, fingerprintDiff, currentVersion, footer } = input;

  // --- Step 1: derive intent from the artifact diff -----------------------
  //
  // Priority: break > minor > patch > none.
  // `removed` OR `changed` → break (a public member is gone or incompatibly altered).
  // `added` only         → minor (new public surface, backwards-compatible).
  // fingerprint changed  → patch (internal change; public API intact).
  // nothing changed      → none.

  let diffIntent: BumpIntent = "none";

  if (apiDiff.removed || apiDiff.changed) {
    diffIntent = "break";
  } else if (apiDiff.added) {
    diffIntent = "minor";
  } else if (fingerprintDiff.changed) {
    diffIntent = "patch";
  }

  // --- Step 2: footer escalation (escalate-only, never lower) --------------
  //
  // `footer.forced` means the author declared an explicit breaking change.
  // This forces at least break-intent regardless of the diff result.

  const footerIntent: BumpIntent = footer.forced ? "break" : "none";
  const intent = maxIntent(diffIntent, footerIntent);

  // --- Step 3: apply pre-stable carve-out ----------------------------------
  //
  // If the package is pre-stable (MAJOR === 0 OR prerelease label present),
  // a break is capped at MINOR — the package is not yet committed to stability
  // guarantees and a MAJOR bump would be misleading.

  if (intent === "break") {
    return isPreStable(currentVersion) ? "minor" : "major";
  }

  if (intent === "minor") return "minor";
  if (intent === "patch") return "patch";
  return "none";
}
