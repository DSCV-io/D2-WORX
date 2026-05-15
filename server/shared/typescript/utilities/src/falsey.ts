// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Returns true when the value is null, undefined, an empty string, a
 * whitespace-only string, an empty collection, or a zero/empty UUID.
 *
 * Mirrors the .NET `Falsey()` extension semantics from
 * `D2.Shared.Utilities.Extensions.StringExtensions` /
 * `EnumerableExtensions` / `GuidExtensions`. The runtime kind check is
 * deliberately permissive — strings, arrays, sets, maps, and the canonical
 * empty-uuid string all share a single boundary helper so consumer code
 * does not branch on type.
 */
export function falsey(value: unknown): boolean {
  if (value === null || value === undefined) return true;
  if (typeof value === "string") {
    if (value.length === 0) return true;
    return value.trim().length === 0;
  }
  if (Array.isArray(value)) return value.length === 0;
  if (value instanceof Set || value instanceof Map) return value.size === 0;
  return false;
}
