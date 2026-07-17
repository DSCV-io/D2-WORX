// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";

/**
 * Boundary helper that returns the trimmed string when `input` carries any
 * non-whitespace content, otherwise returns `undefined`. Mirrors the .NET
 * `name.Falsey()` short-circuit pattern: pipe raw user input through this
 * helper at the boundary so downstream code only sees `string` (the
 * narrowed truthy form) without re-checking null / undefined / empty /
 * whitespace.
 *
 * Use this helper at every public-API boundary where the caller may pass
 * null / undefined / empty / whitespace-only strings. Returns the trimmed
 * form so callers can use it directly without a second `.trim()` call.
 *
 * Wire-boundary carve-out per rules.md §6.15: the `input` parameter is
 * `string | null | undefined` because this helper EXISTS to absorb wire
 * `null` (cookies / headers / DB columns / JSON values) and normalize to
 * `undefined`. Domain code consumes the `string | undefined` return.
 *
 * @param input - the raw string to normalize.
 * @returns the trimmed string when truthy; `undefined` otherwise.
 */
export function truthyOrUndefined(
  input: string | null | undefined,
): string | undefined {
  if (falsey(input)) return undefined;
  return (input as string).trim();
}
