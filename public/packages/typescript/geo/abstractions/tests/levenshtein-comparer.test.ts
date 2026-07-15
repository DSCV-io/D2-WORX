// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  compare,
  isWithin,
} from "../src/name-resolution/levenshtein-comparer.js";

/**
 * Adversarial coverage for `compare` and `isWithin`. Mirrors the .NET
 * `LevenshteinComparerTests` — inputs and expected outputs MUST agree
 * on both runtimes (cross-language parity invariant).
 */

describe("compare", () => {
  // --------------------------------------------------------------------
  // Identical strings
  // --------------------------------------------------------------------

  it("returns 0 for identical strings", () => {
    expect(compare("france", "france", 3)).toBe(0);
  });

  it("returns 0 for both empty strings", () => {
    expect(compare("", "", 0)).toBe(0);
  });

  // --------------------------------------------------------------------
  // One side empty
  // --------------------------------------------------------------------

  it.each([
    ["abc vs '' (cap 3)", "abc", "", 3, 3],
    ["'' vs abc (cap 3)", "", "abc", 3, 3],
    ["a vs '' (cap 1)", "a", "", 1, 1],
  ])("%s → %i", (_label, a, b, cap, expected) => {
    expect(compare(a, b, cap)).toBe(expected);
  });

  // --------------------------------------------------------------------
  // Single edit operations
  // --------------------------------------------------------------------

  it("returns 1 for single substitution", () => {
    // "france" vs "fraace" — one substitution (n→a)
    expect(compare("france", "fraace", 2)).toBe(1);
  });

  it("returns 1 for single insertion", () => {
    // "abc" vs "abbc" — one insertion
    expect(compare("abc", "abbc", 2)).toBe(1);
  });

  it("returns 1 for single deletion", () => {
    // "france" vs "frnce" — one deletion
    expect(compare("france", "frnce", 2)).toBe(1);
  });

  // --------------------------------------------------------------------
  // Transposition — NOT damerau — two ops
  // --------------------------------------------------------------------

  it("counts transposition as two ops (not damerau)", () => {
    // "ab" vs "ba" — standard Levenshtein = 2 (sub+sub or del+ins)
    expect(compare("ab", "ba", 3)).toBe(2);
  });

  // --------------------------------------------------------------------
  // Case sensitivity
  // --------------------------------------------------------------------

  it("is case-sensitive (callers pre-normalize)", () => {
    // "France" vs "france" — capital F counts as one substitution
    expect(compare("France", "france", 10)).toBe(1);
  });

  // --------------------------------------------------------------------
  // Length-difference shortcut returns ceiling
  // --------------------------------------------------------------------

  it("returns ceiling when length difference exceeds cap", () => {
    // |"france".length - "fr".length| = 4 > cap 2 → ceiling = 3
    expect(compare("france", "fr", 2)).toBe(3);
  });

  // --------------------------------------------------------------------
  // Negative maxDistance sentinel
  // --------------------------------------------------------------------

  it("returns 0 for negative maxDistance on identical strings (cap clamped to 0)", () => {
    // Negative cap clamped to 0 → cap=0, ceiling=1. Identical strings have
    // distance=0; 0 is NOT > cap(0), so the actual distance (0) is returned.
    // .NET parity: Compare("x","x",-1) also returns 0 via the same logic.
    expect(compare("x", "x", -1)).toBe(0);
  });

  it("returns ceiling of 1 for negative maxDistance (different strings)", () => {
    expect(compare("abc", "xyz", -5)).toBe(1);
  });

  // --------------------------------------------------------------------
  // Early-termination cap
  // --------------------------------------------------------------------

  it("returns ceiling for highly different strings with tight cap", () => {
    // "abcdef" vs "xyz" — actual distance >= 4, cap = 1 → ceiling = 2
    expect(compare("abcdef", "xyz", 1)).toBe(2);
  });

  it("returns exact distance when exactly at cap", () => {
    // "abc" vs "xyz" — distance = 3, cap = 3 → returns 3
    expect(compare("abc", "xyz", 3)).toBe(3);
  });

  it("returns ceiling when distance is one above cap", () => {
    // "abcd" vs "xyz " — distance = 4, cap = 3 → ceiling = 4
    expect(compare("abcd", "xyz ", 3)).toBe(4);
  });
});

describe("isWithin", () => {
  // --------------------------------------------------------------------
  // Happy path
  // --------------------------------------------------------------------

  it("returns true for identical strings with cap 0", () => {
    expect(isWithin("france", "france", 0)).toBe(true);
  });

  it("returns true for single-edit pair within cap 1", () => {
    expect(isWithin("france", "fraace", 1)).toBe(true);
  });

  it("returns false when two edits exceed cap of 1", () => {
    expect(isWithin("france", "fraxxe", 1)).toBe(false);
  });

  // --------------------------------------------------------------------
  // Both empty
  // --------------------------------------------------------------------

  it("returns true for both empty strings with cap 0", () => {
    expect(isWithin("", "", 0)).toBe(true);
  });

  // --------------------------------------------------------------------
  // Negative maxDistance always false (including identical strings)
  // --------------------------------------------------------------------

  it("returns false for negative maxDistance even on identical strings", () => {
    // .NET: IsWithin short-circuits to false before calling Compare.
    // TS mirror must do the same to prevent "x" vs "x" → 0 <= -1 false-positive.
    expect(isWithin("x", "x", -1)).toBe(false);
  });

  it("returns false for negative maxDistance on both-empty strings", () => {
    expect(isWithin("", "", -1)).toBe(false);
  });

  // --------------------------------------------------------------------
  // Resolver usage pattern (caps 0 / 1 / 2 / 3)
  // --------------------------------------------------------------------

  it.each([
    ["france vs france, cap 0 → true", "france", "france", 0, true],
    ["france vs frnce, cap 1 → true", "france", "frnce", 1, true],
    ["france vs fnce, cap 2 → true", "france", "fnce", 2, true],
    ["france vs fce, cap 3 → true", "france", "fce", 3, true],
    ["france vs fce, cap 2 → false", "france", "fce", 2, false],
    ["france vs fnce, cap 0 → false", "france", "fnce", 0, false],
  ])("%s", (_label, a, b, cap, expected) => {
    expect(isWithin(a, b, cap)).toBe(expected);
  });
});
