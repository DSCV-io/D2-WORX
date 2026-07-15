// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";
import { EMPTY_UUID, UUID_RE } from "./regex.js";

/**
 * Tries to parse a string as a non-empty UUID. Returns the canonical
 * lowercase form on success; `undefined` when the input is
 * null/undefined/empty/whitespace, not UUID-shaped, OR equals the empty UUID.
 * Mirrors the .NET `string?.TryParseTruthyNull(out Guid? r)` extension
 * semantics — collapsing unparseable AND empty-UUID inputs to a single
 * "absent" signal.
 *
 * TS naming uses `*Undef*` (rules.md §6.15 — `undefined` for absent). The
 * .NET mirror is `TryParseTruthyNull(out Guid? r)` — same behavior, C#
 * convention. See PARITY.md "Utility helper naming divergence".
 */
export function tryParseTruthyUndefUuid(
  input: string | null | undefined,
): string | undefined {
  if (falsey(input)) return undefined;
  const lower = (input as string).trim().toLowerCase();
  if (!UUID_RE.test(lower)) return undefined;
  if (lower === EMPTY_UUID) return undefined;
  return lower;
}

/**
 * Tries to parse a string as a finite integer. Returns the parsed number on
 * success; `undefined` when the input is null/undefined/empty/whitespace OR
 * does not parse cleanly to a finite integer (rejects floats and scientific
 * notation).
 *
 * TS naming uses `*Undef*` (rules.md §6.15). See {@link tryParseTruthyUndefUuid}.
 */
export function tryParseTruthyUndefInt(
  input: string | null | undefined,
): number | undefined {
  if (falsey(input)) return undefined;
  const trimmed = (input as string).trim();
  // Regex guarantees finite integer parse — no further finite-check needed.
  if (!/^-?\d+$/.test(trimmed)) return undefined;
  return Number.parseInt(trimmed, 10);
}

/**
 * Tries to parse a string as a member of the supplied enum-like object.
 * Case-insensitive on the keys; returns the canonical key on success or
 * `undefined` when the input is null/undefined/empty or no key matches.
 * Mirrors the .NET `string?.TryParseTruthyNull<TEnum>(out var r)` extension
 * semantics.
 *
 * TS naming uses `*Undef*` (rules.md §6.15). See {@link tryParseTruthyUndefUuid}.
 */
export function tryParseTruthyUndefEnum<
  T extends Record<string, string | number>,
>(enumObj: T, input: string | null | undefined): keyof T | undefined {
  if (falsey(input)) return undefined;
  const lower = (input as string).trim().toLowerCase();
  for (const key of Object.keys(enumObj)) {
    if (key.toLowerCase() === lower) return key as keyof T;
  }
  return undefined;
}
