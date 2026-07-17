// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Recursively canonicalize a JSON-shaped value: object keys are sorted
 * lexicographically; arrays preserve order; primitives are returned
 * as-is. Used by parity tests to compare two JSON values for structural
 * equivalence regardless of key insertion order or whitespace.
 *
 * Both sides of the parity assertion (TS-side and the .NET-emitted
 * fixture file's `data` payload) flow through this same helper so the
 * comparison is "shape and contents," not "literal byte sequence."
 *
 * @example
 *   canonicalize({ b: 1, a: [3, 2] })
 *   // → { a: [3, 2], b: 1 }
 */
export function canonicalize(value: unknown): unknown {
  if (value === null || value === undefined) return value;
  if (Array.isArray(value)) return value.map(canonicalize);
  if (typeof value !== "object") return value;
  const obj = value as Record<string, unknown>;
  const sortedKeys = Object.keys(obj).sort();
  const result: Record<string, unknown> = {};
  for (const k of sortedKeys) result[k] = canonicalize(obj[k]);
  return result;
}

/**
 * Canonicalize a value AND serialize it to a stable JSON string.
 * Convenient for byte-equal comparisons of two values.
 */
export function canonicalJson(value: unknown): string {
  return JSON.stringify(canonicalize(value));
}
