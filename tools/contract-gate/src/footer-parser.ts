// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Shared breaking-change footer parser.
//
// Scans commit-message bodies for the Conventional Commits footer tokens
// that declare a breaking contract change:
//
//   WIRE-BREAKING: <description>        — wire-axis (proto / OpenAPI)
//   BREAKING CHANGE: <description>      — api-axis  (spec catalogs / i18n)
//   BREAKING-CHANGE: <description>      — api-axis  (alternate Conventional-Commits spelling)
//   <type>!: <subject>                  — type!-shorthand (subject line only)
//
// The result drives TWO consumers:
//   • The breaking-change gate  — suppresses gate RED when forced is true.
//   • The release runner        — maps wireBreaking / apiBreaking descriptions
//                                 into per-package CHANGELOG blocks + version bumps.
//
// A single breaking footer of EITHER axis opens ALL gate arms for the PR (the
// simplest correct rule pre-external; axis attribution is still recorded in the
// `wireBreaking` / `apiBreaking` arrays for the release runner's changelog use).
//
// Regex discipline (per regex-redos-discipline, Bucket 2):
//   All patterns in this module operate on a single, bounded-length commit-message
//   line. No nested quantifiers, no super-linear backtracking. matchTimeout and
//   JIT pre-warm are NOT needed — the input cannot grow unboundedly and neither
//   pattern uses alternation inside a quantifier. Matches the rationale comment in
//   wire-channel.ts:20-24.

import { falsey } from "@d2/utilities";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/**
 * Structured result of parsing the breaking-change footers across a set of
 * commit messages.
 *
 * Consumed by:
 *   - The breaking-change gate: `forced` suppresses RED → GREEN.
 *   - The release runner: `wireBreaking` / `apiBreaking` drive changelog + bump.
 */
export interface BreakingValve {
  /** True when at least one breaking-change signal was found in any commit. */
  readonly forced: boolean;
  /**
   * Descriptions extracted from `WIRE-BREAKING:` footers (wire axis —
   * proto, gRPC, OpenAPI).
   */
  readonly wireBreaking: readonly string[];
  /**
   * Descriptions extracted from `BREAKING CHANGE:` / `BREAKING-CHANGE:` footers
   * and `type!:` subject-line shorthands (api axis — spec catalogs, i18n keys).
   */
  readonly apiBreaking: readonly string[];
}

// ---------------------------------------------------------------------------
// Internal regex constants (Bucket 2 — bounded input, no super-linear backtracking)
// ---------------------------------------------------------------------------

/** Matches a `type!: subject` conventional-commit subject line (the breaking shorthand). */
const TYPE_BANG_RE = /^[a-zA-Z]+(\([^)]*\))?!:\s*(.+)$/;

/** Matches a `WIRE-BREAKING: description` footer token. */
const WIRE_BREAKING_RE = /^WIRE-BREAKING:\s*(.+)$/;

/** Matches `BREAKING CHANGE: description` OR `BREAKING-CHANGE: description`. */
const API_BREAKING_RE = /^BREAKING[ -]CHANGE:\s*(.+)$/;

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Scan an array of full commit messages (each containing a subject line,
 * optional body paragraphs, and an optional Conventional Commits footer block)
 * for breaking-change signals.
 *
 * Parsing rules:
 *  - The footer block is the contiguous run of `Token: value` lines after the
 *    LAST blank line in the message. Only lines in the footer block are
 *    eligible for `WIRE-BREAKING:` / `BREAKING CHANGE:` / `BREAKING-CHANGE:`
 *    detection (a footer token appearing in the prose body is NOT a valid footer).
 *  - The `type!:` shorthand is detected on the SUBJECT LINE only (first line).
 *  - Parsing is case-sensitive: `WIRE-BREAKING:` fires; `wire-breaking:` does not
 *    (Conventional Commits tokens are uppercase by convention, and case-insensitive
 *    matching would let accidental prose sentences trigger the valve).
 *  - Trailing whitespace and CRLF line endings are stripped before matching.
 *  - A message that is entirely blank or malformed produces no findings (no throw).
 *
 * @param commitMessages - Array of raw commit-message strings (multi-line, one
 *   message per element). Safe to pass an empty array — returns `forced: false`.
 * @returns A {@link BreakingValve} describing the aggregate breaking signals.
 */
export function parseBreakingFooters(
  commitMessages: readonly string[],
): BreakingValve {
  const wireBreaking: string[] = [];
  const apiBreaking: string[] = [];

  for (const message of commitMessages) {
    if (falsey(message)) continue;

    // Normalize line endings (CRLF → LF) and split into lines.
    const lines = message
      .replace(/\r\n/g, "\n")
      .replace(/\r/g, "\n")
      .split("\n");

    if (lines.length === 0) continue;

    // ----- Subject-line type!: shorthand -----
    const subjectLine = (lines[0] ?? "").trimEnd();
    const bangMatch = TYPE_BANG_RE.exec(subjectLine);

    if (bangMatch !== null) {
      const description = bangMatch[2] ?? "";

      if (!falsey(description)) apiBreaking.push(description.trim());
    }

    // ----- Footer block detection -----
    // The footer block starts after the LAST blank line in the message.
    // Walk backwards, skipping trailing empty lines (from a trailing newline),
    // to find the blank line that separates the footer from the body.
    let footerStartIndex = -1;

    // Find the index of the last non-empty line so we don't treat a trailing
    // newline as the blank-line footer separator.
    let lastNonEmptyIndex = lines.length - 1;

    while (lastNonEmptyIndex >= 0 && falsey(lines[lastNonEmptyIndex] ?? "")) {
      lastNonEmptyIndex--;
    }

    for (let i = lastNonEmptyIndex - 1; i >= 1; i--) {
      if (falsey(lines[i] ?? "")) {
        footerStartIndex = i + 1;
        break;
      }
    }

    if (footerStartIndex === -1 || footerStartIndex > lastNonEmptyIndex)
      continue;

    const footerLines = lines.slice(footerStartIndex);

    for (const raw of footerLines) {
      const line = raw.trimEnd();

      const wireMatch = WIRE_BREAKING_RE.exec(line);

      if (wireMatch !== null) {
        const desc = (wireMatch[1] ?? "").trim();

        if (!falsey(desc)) wireBreaking.push(desc);
        continue;
      }

      const apiMatch = API_BREAKING_RE.exec(line);

      if (apiMatch !== null) {
        const desc = (apiMatch[1] ?? "").trim();

        if (!falsey(desc)) apiBreaking.push(desc);
      }
    }
  }

  return {
    forced: wireBreaking.length + apiBreaking.length > 0,
    wireBreaking,
    apiBreaking,
  };
}
