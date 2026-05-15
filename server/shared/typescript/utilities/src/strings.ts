// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";
import { DISPLAY_NAME_INVALID_RE, WHITESPACE_RE } from "./regex.js";

/**
 * Returns null when the input is null/undefined/empty/whitespace; otherwise
 * returns the trimmed string.
 */
export function toNullIfEmpty(input: string | null | undefined): string | null {
  if (falsey(input)) return null;
  return (input as string).trim();
}

/**
 * Trims leading/trailing whitespace and collapses any internal whitespace
 * runs (spaces, tabs, newlines) into a single space. Returns null if the
 * string is empty after cleaning.
 */
export function cleanStr(input: string | null | undefined): string | null {
  const trimmed = input?.trim();
  if (falsey(trimmed)) return null;
  return (trimmed as string).replace(WHITESPACE_RE, " ");
}

/**
 * Strips characters not allowed in display names then applies {@link cleanStr}.
 * Returns null if empty after cleaning.
 */
export function cleanDisplayStr(
  input: string | null | undefined,
): string | null {
  if (falsey(input)) return null;
  const stripped = (input as string).replace(DISPLAY_NAME_INVALID_RE, "");
  return cleanStr(stripped);
}
