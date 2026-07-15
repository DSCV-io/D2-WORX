// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";

/**
 * Behavior controlling how {@link clean} handles a null/empty input or a
 * post-clean empty result. Mirrors the .NET
 * `D2.Shared.Utilities.Extensions.CleanEnumEmptyBehavior` enum so the two
 * languages reach the same outcome on identical inputs.
 */
export type CleanEnumEmptyBehavior = "ReturnEmpty" | "ReturnNull" | "Throw";

/**
 * Behavior controlling how {@link clean} reacts to a per-element cleaner
 * result of `null` (or `undefined`). Mirrors
 * `D2.Shared.Utilities.Extensions.CleanValueNullBehavior`.
 */
export type CleanValueNullBehavior = "RemoveNulls" | "ThrowOnNull";

/**
 * Per-element cleaner. Returning `null` or `undefined` is a "drop" signal —
 * what happens then is governed by {@link CleanValueNullBehavior}.
 *
 * Cross-language-parity carve-out per rules.md §6.15: this type mirrors
 * the .NET `Cleaner<T>` delegate (which returns `T?`), accepting BOTH
 * `null` (intentional sentinel from .NET interop call sites) and
 * `undefined` (TS-native "absent"). The dual acceptance is the .NET-parity
 * contract — narrowing to only `undefined` would break .NET-bridged
 * call sites that pass `null`-returning lambdas.
 */
export type Cleaner<T> = (item: T) => T | null | undefined;

/**
 * Options bag for {@link clean}. Both behaviors default to the most permissive
 * mode (drop empties / drop nulls) so call sites can pass `clean(items, fn)`
 * for the common case.
 */
export interface CleanOptions {
  readonly enumEmptyBehavior?: CleanEnumEmptyBehavior;
  readonly valueNullBehavior?: CleanValueNullBehavior;
}

/**
 * Applies a cleaner to every element of `items` and reshapes the result
 * according to the supplied empty / null behaviors. Mirrors the .NET
 * `EnumerableExtensions.Clean<T>(...)` extension method 1:1 so the two
 * languages produce the same outcome on identical inputs.
 *
 * Defaults:
 * - `valueNullBehavior = "RemoveNulls"` — cleaner returning null drops the
 *   element silently.
 * - `enumEmptyBehavior = "ReturnNull"` — null/empty input or all-cleaned-to-
 *   null output yields `null`.
 *
 * Accepts any iterable (arrays, sets, map values, generators) — matches
 * `IEnumerable<T>` parity on the .NET side.
 *
 * Cross-language-parity carve-out per rules.md §6.15: parameter accepts
 * `null` and return type emits `null` because the .NET parity contract
 * (named-behavior `ReturnNull`) makes `null` the explicit sentinel; the
 * `ReturnEmpty` behavior is available for callers wanting `undefined`-
 * style "absent" semantics.
 *
 * @throws RangeError when `valueNullBehavior` is `"ThrowOnNull"` and a
 *   cleaner returns null/undefined.
 * @throws RangeError when `enumEmptyBehavior` is `"Throw"` and the input is
 *   empty or post-cleaning yields no elements.
 */
export function clean<T>(
  items: Iterable<T> | null | undefined,
  cleaner: Cleaner<T>,
  options: CleanOptions = {},
): T[] | null {
  const enumEmptyBehavior = options.enumEmptyBehavior ?? "ReturnNull";
  const valueNullBehavior = options.valueNullBehavior ?? "RemoveNulls";

  if (items === null || items === undefined)
    return handleEmpty<T>(enumEmptyBehavior);

  const dirty: T[] = Array.isArray(items) ? items : Array.from(items);
  if (falsey(dirty)) return handleEmpty<T>(enumEmptyBehavior);

  const out: T[] = [];
  for (const item of dirty) {
    const cleaned = cleaner(item);
    if (cleaned !== null && cleaned !== undefined) {
      out.push(cleaned);
      continue;
    }
    if (valueNullBehavior === "ThrowOnNull") {
      throw new RangeError("A cleaned value evaluated to null.");
    }
  }

  if (falsey(out)) return handleEmpty<T>(enumEmptyBehavior);
  return out;
}

// Internal helper for the `clean()` API — return-type `T[] | null` is the
// .NET-parity contract (see `clean()` JSDoc).
function handleEmpty<T>(behavior: CleanEnumEmptyBehavior): T[] | null {
  switch (behavior) {
    case "ReturnEmpty":
      return [];
    case "Throw":
      throw new RangeError("The enumerable is empty after cleaning.");
    default:
      return null;
  }
}
