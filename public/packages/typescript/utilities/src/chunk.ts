// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Splits an array into consecutive chunks of at most `size` elements.
 * Throws on size &lt; 1 (rejects nonsense input rather than silently returning
 * an empty list). Mirrors the .NET `IEnumerable<T>.Chunk(int size)` shape.
 */
export function chunk<T>(arr: readonly T[], size: number): T[][] {
  if (!Number.isInteger(size) || size < 1)
    throw new RangeError(`chunk size must be a positive integer; got ${size}`);
  const out: T[][] = [];
  for (let i = 0; i < arr.length; i += size) out.push(arr.slice(i, i + size));
  return out;
}
