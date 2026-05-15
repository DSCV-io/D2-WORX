// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";
import { EMPTY_UUID, UUID_RE } from "./regex.js";

/**
 * Tries to parse a string as a non-empty UUID. Returns the canonical
 * lowercase form on success; null when the input is null/empty/whitespace,
 * not UUID-shaped, OR equals the empty UUID. Mirrors the .NET
 * `string?.TryParseTruthyNull(out Guid? r)` extension semantics — collapsing
 * unparseable AND empty-UUID inputs to a single "absent" signal.
 */
export function tryParseTruthyNullUuid(
  input: string | null | undefined,
): string | null {
  if (falsey(input)) return null;
  const lower = (input as string).trim().toLowerCase();
  if (!UUID_RE.test(lower)) return null;
  if (lower === EMPTY_UUID) return null;
  return lower;
}

/**
 * Tries to parse a string as a finite integer. Returns the parsed number on
 * success; null when the input is null/empty/whitespace OR does not parse
 * cleanly to a finite integer (rejects floats and scientific notation).
 */
export function tryParseTruthyNullInt(
  input: string | null | undefined,
): number | null {
  if (falsey(input)) return null;
  const trimmed = (input as string).trim();
  // Regex guarantees finite integer parse — no further finite-check needed.
  if (!/^-?\d+$/.test(trimmed)) return null;
  return Number.parseInt(trimmed, 10);
}

/**
 * Tries to parse a string as a member of the supplied enum-like object.
 * Case-insensitive on the keys; returns the canonical key on success or null
 * when the input is null/empty or no key matches. Mirrors the .NET
 * `string?.TryParseTruthyNull<TEnum>(out var r)` extension semantics.
 */
export function tryParseTruthyNullEnum<
  T extends Record<string, string | number>,
>(enumObj: T, input: string | null | undefined): keyof T | null {
  if (falsey(input)) return null;
  const lower = (input as string).trim().toLowerCase();
  for (const key of Object.keys(enumObj)) {
    if (key.toLowerCase() === lower) return key as keyof T;
  }
  return null;
}
