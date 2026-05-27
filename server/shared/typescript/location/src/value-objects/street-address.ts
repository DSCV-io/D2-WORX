// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { tk } from "@d2/result";
import type { D2Result } from "@d2/result";
import { validationFailed, ok } from "@d2/result";
import { falsey } from "@d2/utilities";

import { sha256Hex } from "../internal/sha256-hex.js";

const TK_LINE1_REQUIRED = tk("geo_validation_address_line1_required");

/**
 * Immutable 5-line postal address value object. `line1` is required;
 * `line2..line5` are optional and may be populated in any combination
 * (no gap rule). Mirrors
 * `D2.Shared.Location.ValueObjects.StreetAddress`.
 *
 * Two-stage normalization: the stored form preserves case + strips
 * decorative punctuation; the hash form upper-cases + NFD-strips
 * combining marks + applies a Unicode-category filter so dedup-equivalent
 * inputs across scripts produce byte-identical `hashId` values.
 *
 * **PII** — postal-address PII per GDPR. Consumers MUST scrub fields of
 * this type at the logger / serializer sink.
 */
export interface StreetAddress {
  readonly line1: string;
  readonly line2?: string;
  readonly line3?: string;
  readonly line4?: string;
  readonly line5?: string;
  readonly hashId: string;
}

// Decorative punctuation regex — stripped from the stored form.
// Anchored character-class match — no backtracking — B1 shape, no timeout needed.
const DECORATIVE_PUNCT_RE = /[.,;:!?]/g;

// Hash-form filter: keep only Letter / Decimal-digit / ASCII space.
// /u flag MANDATORY — without /u, \p{L}/\p{Nd} Unicode-property escapes are
// unsupported and surrogate-pair emoji silently bypass the filter, producing
// WRONG hash output. The /u flag ensures non-Latin scripts are treated as
// first-class characters (preserved in hash form) while the negation pattern
// strips everything else (control chars, format chars like BiDi overrides /
// zero-width joiners, punctuation, symbols, emoji).
const HASH_FILTER_KEEP_RE = /[\p{L}\p{Nd} ]/gu;

/**
 * Two-stage normalization stage 2 — produces the hash-input form
 * (UPPERCASE + NFD-stripped combining marks + Unicode-category filter
 * keeping only Letter / Decimal-digit / ASCII space). Internal but
 * exported so the cross-language parity fixture can pin its behavior.
 *
 * @param cleaned A line value already passed through stage 1 (stored
 *                form via `cleanStored`), or undefined.
 * @returns Empty string when input is undefined / empty; the hash-form
 *          canonical string otherwise.
 */
export function normalizeForHash(cleaned: string | undefined): string {
  if (cleaned === undefined || cleaned.length === 0) return "";

  // Stage 2a — case-fold (no-op on caseless scripts).
  const upper = cleaned.toUpperCase();

  // Stage 2b — NFD decompose so Latin diacritics split into base + combining mark.
  const nfd = upper.normalize("NFD");

  // Stage 2c — Unicode-category-aware filter: match-and-keep (combining
  // marks fall outside \p{L}+\p{Nd}+space, so they drop here). The /u
  // flag is mandatory — without it, surrogate-pair emoji silently survive.
  const matches = nfd.match(HASH_FILTER_KEEP_RE);
  return matches !== null ? matches.join("") : "";
}

/**
 * Two-stage normalization stage 1 — produces the stored form (trim →
 * collapse internal whitespace → strip decorative punctuation; CASE
 * PRESERVED). Whitespace-only / undefined input → undefined.
 */
function cleanStored(line: string | undefined): string | undefined {
  if (line === undefined || line.length === 0) return undefined;

  // Pass 1 — strip Unicode format chars (BiDi overrides, ZWJ, etc.) and
  // collapse whitespace + control chars to single spaces (matches .NET
  // `Rune.IsWhiteSpace` + `Rune.IsControl` + UnicodeCategory.Format
  // logic). The /u flag is mandatory for \p{} matching.
  const noFormatChars = line.replace(/\p{Cf}/gu, "");
  const collapsed = noFormatChars
    .replace(/[\s\p{Cc}]+/gu, " ") // any whitespace or control char → single space
    .trim();

  if (collapsed.length === 0) return undefined;

  // Pass 2 — strip decorative punctuation.
  const stripped = collapsed.replace(DECORATIVE_PUNCT_RE, "");

  // Pass 3 — re-trim + re-collapse (stripping punctuation may leave
  // "St ." → "St " or "  " runs from the removed punctuation neighbors).
  const finalForm = stripped.replace(/\s+/g, " ").trim();

  return finalForm.length === 0 ? undefined : finalForm;
}

/**
 * Creates a `StreetAddress` from up to 5 free-text lines. `line1` is
 * required (post-clean); the others are optional and may be supplied
 * in any combination (no gap rule).
 */
export function createStreetAddress(
  line1: string | undefined,
  line2?: string | undefined,
  line3?: string | undefined,
  line4?: string | undefined,
  line5?: string | undefined,
): D2Result<StreetAddress> {
  const cleanedLine1 = cleanStored(line1);
  if (falsey(cleanedLine1)) {
    return validationFailed<StreetAddress>({
      messages: [TK_LINE1_REQUIRED],
    });
  }

  const cleanedLine2 = cleanStored(line2);
  const cleanedLine3 = cleanStored(line3);
  const cleanedLine4 = cleanStored(line4);
  const cleanedLine5 = cleanStored(line5);

  const hashInput =
    normalizeForHash(cleanedLine1) +
    "|" +
    normalizeForHash(cleanedLine2) +
    "|" +
    normalizeForHash(cleanedLine3) +
    "|" +
    normalizeForHash(cleanedLine4) +
    "|" +
    normalizeForHash(cleanedLine5);

  const hashId = "v1." + sha256Hex(hashInput);

  const result: StreetAddress = {
    line1: cleanedLine1!,
    hashId,
    ...(cleanedLine2 !== undefined ? { line2: cleanedLine2 } : {}),
    ...(cleanedLine3 !== undefined ? { line3: cleanedLine3 } : {}),
    ...(cleanedLine4 !== undefined ? { line4: cleanedLine4 } : {}),
    ...(cleanedLine5 !== undefined ? { line5: cleanedLine5 } : {}),
  };
  return ok<StreetAddress>(result);
}
