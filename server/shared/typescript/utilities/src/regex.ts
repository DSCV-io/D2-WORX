// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Matches one or more whitespace characters. No backtracking — single greedy
 * quantifier with no following pattern.
 */
export const WHITESPACE_RE = /\s+/g;

/**
 * Matches characters NOT allowed in display names. Allowed: letters from
 * any Unicode script, digits, spaces, hyphens, apostrophes, periods,
 * commas. Single char-class with no quantifier — no backtracking.
 */
export const DISPLAY_NAME_INVALID_RE = /[^\p{L}\p{N}\s\-'.,]/gu;

/**
 * Basic `local@domain.tld` email shape. Bounded backtracking only.
 */
export const EMAIL_RE = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

/**
 * Matches a canonical UUID v1-v8 (or nil). Used by
 * {@link tryParseTruthyNullUuid}. No backtracking — fixed-length anchors.
 */
export const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Matches the canonical "empty" UUID — all zeros. The TS analog of
 * .NET `Guid.Empty`.
 */
export const EMPTY_UUID = "00000000-0000-0000-0000-000000000000";
