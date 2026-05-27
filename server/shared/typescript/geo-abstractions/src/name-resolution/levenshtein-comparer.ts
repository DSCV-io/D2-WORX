// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Classic Wagner-Fischer Levenshtein edit-distance with early-termination
 * at `maxDistance + 1`. Pure function — no I/O, no shared state, thread
 * safe by design.
 *
 * Mirrors .NET `D2.Shared.Geo.Abstractions.NameResolution.LevenshteinComparer`
 * byte-for-byte; the same input pair + `maxDistance` MUST produce the
 * same numeric output on both runtimes. Cross-language parity is pinned
 * via the byte-equivalent fixture exercised by the parity test suite.
 *
 * Operates on UTF-16 code units (TS `string.length` and `charCodeAt`
 * semantics), which matches .NET `string` indexing. Inputs that contain
 * surrogate pairs are compared at the UTF-16 unit level — a code point
 * outside the BMP counts as two distance units. This is intentional for
 * parity; supplementary-plane characters are exceedingly rare in geo
 * reference data (no entity name in the seeded catalog contains one).
 *
 * Boundary handling (matches .NET):
 *
 * - **Negative `maxDistance`**: clamped to `cap = 0`, so the returned
 *   `ceiling` is `1`. Any non-trivial difference returns the sentinel.
 *   `isWithin` short-circuits to `false` before calling `compare` when
 *   `maxDistance < 0` (matches .NET `IsWithin`).
 * - **Length-difference shortcut**: if `|len(a) - len(b)| > cap`, no
 *   alignment can fit inside the cap, so we return `ceiling` without
 *   allocating DP buffers.
 * - **Row-min early termination**: when every cell in a row of the DP
 *   table exceeds `cap`, no remaining cell can ever drop below it (the
 *   recurrence is monotonic), so the function returns `ceiling`
 *   immediately. Callers MUST treat any value greater than
 *   `maxDistance` as "exceeds bound" and never as a legitimate edit
 *   distance.
 *
 * @param a - the first string to compare.
 * @param b - the second string to compare.
 * @param maxDistance - the maximum distance to compute. Distances at or
 *   below this bound are returned exactly; distances above the bound
 *   return the sentinel `cap + 1`. Negative values are clamped to `0`.
 * @returns the edit distance, or `cap + 1` when the distance exceeds
 *   the (clamped) cap.
 */
export function compare(a: string, b: string, maxDistance: number): number {
  // Clamp negative cap to 0 to match .NET semantics (`var cap = maxDistance < 0 ? 0 : maxDistance`).
  const cap = maxDistance < 0 ? 0 : maxDistance;
  const ceiling = cap + 1;

  // Order so the SHORTER string is on the inner DP axis — O(min) memory.
  // Matches .NET's `(first, second) = (second, first)` swap.
  let first = a;
  let second = b;
  if (first.length < second.length) {
    const swap = first;
    first = second;
    second = swap;
  }

  // Length-difference shortcut — distance is bounded below by the delta
  // and cannot fit inside the cap.
  if (first.length - second.length > cap) return ceiling;

  // Trivial cases — second is empty, distance equals first.length.
  if (second.length === 0) return first.length > cap ? ceiling : first.length;

  // Two-row DP — previous row + current row over `second` (the shorter
  // string). Initialize previous row to the empty-prefix distances
  // 0..second.length.
  let previous = new Array<number>(second.length + 1);
  let current = new Array<number>(second.length + 1);
  for (let j = 0; j <= second.length; j++) previous[j] = j;

  for (let i = 1; i <= first.length; i++) {
    current[0] = i;
    let rowMin = i;
    const firstChar = first.charCodeAt(i - 1);

    for (let j = 1; j <= second.length; j++) {
      const cost = firstChar === second.charCodeAt(j - 1) ? 0 : 1;
      // previous[]! / current[]! safe — both arrays allocated with second.length+1 entries.
      const deletion = previous[j]! + 1;
      const insertion = current[j - 1]! + 1;
      const substitution = previous[j - 1]! + cost;
      let cell = deletion;
      if (insertion < cell) cell = insertion;
      if (substitution < cell) cell = substitution;
      current[j] = cell;
      if (cell < rowMin) rowMin = cell;
    }

    // Early termination — entire row is above the cap, no later cell
    // can reduce below it (DP recurrence is monotonic non-decreasing in
    // the row minimum).
    if (rowMin > cap) return ceiling;

    // Swap rows for next iteration.
    const swap = previous;
    previous = current;
    current = swap;
  }

  const distance = previous[second.length]!;
  return distance > cap ? ceiling : distance;
}

/**
 * Convenience predicate — returns `true` when the bounded Levenshtein
 * edit distance between `a` and `b` is at most `maxDistance`.
 *
 * Matches .NET `LevenshteinComparer.IsWithin`: short-circuits to `false`
 * when `maxDistance < 0` (without calling `compare`), so a negative cap
 * always returns `false` regardless of input pair — preventing the
 * `a === b → 0 <= -1` false-positive that a naive
 * `compare(...) <= maxDistance` would produce.
 *
 * @param a - the first string to compare.
 * @param b - the second string to compare.
 * @param maxDistance - the inclusive distance threshold. Negative values
 *   always return `false`.
 * @returns `true` iff `a` and `b` are within `maxDistance` edits AND
 *   `maxDistance >= 0`.
 */
export function isWithin(a: string, b: string, maxDistance: number): boolean {
  if (maxDistance < 0) return false;
  return compare(a, b, maxDistance) <= maxDistance;
}
