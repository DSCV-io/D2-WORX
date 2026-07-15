// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  tryParseTruthyUndefEnum,
  tryParseTruthyUndefInt,
  tryParseTruthyUndefUuid,
} from "../src/parse.js";
import { EMPTY_UUID } from "../src/regex.js";

describe("tryParseTruthyUndefUuid", () => {
  it("returns canonical lowercase UUID on success", () => {
    expect(
      tryParseTruthyUndefUuid("550E8400-E29B-41D4-A716-446655440000"),
    ).toBe("550e8400-e29b-41d4-a716-446655440000");
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["empty", ""],
    ["whitespace", "   "],
    ["empty UUID", EMPTY_UUID],
    ["malformed", "not-a-uuid"],
    ["wrong shape", "550e8400-e29b-41d4-a716"],
    ["wrong type chars", "550e8400-e29b-41d4-a716-44665544000Z"],
  ])("%s → undefined", (_label, input) => {
    expect(tryParseTruthyUndefUuid(input)).toBeUndefined();
  });
});

describe("tryParseTruthyUndefInt", () => {
  it.each([
    ["happy positive", "42", 42],
    ["happy negative", "-7", -7],
    ["zero", "0", 0],
    ["padded", "  42  ", 42],
  ])("%s → %s", (_label, input, expected) => {
    expect(tryParseTruthyUndefInt(input)).toBe(expected);
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["empty", ""],
    ["whitespace", "   "],
    ["float", "3.14"],
    ["scientific", "1e3"],
    ["alpha", "abc"],
    ["mixed", "42a"],
    ["sign-only", "-"],
  ])("%s → undefined", (_label, input) => {
    expect(tryParseTruthyUndefInt(input)).toBeUndefined();
  });
});

describe("tryParseTruthyUndefEnum", () => {
  const Color = { Red: "red", Green: "green", Blue: "blue" } as const;

  it.each([
    ["exact match", "Red", "Red"],
    ["case-insensitive", "red", "Red"],
    ["uppercase", "BLUE", "Blue"],
    ["padded", "  Green  ", "Green"],
  ])("%s → %s", (_label, input, expected) => {
    expect(tryParseTruthyUndefEnum(Color, input)).toBe(expected);
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["empty", ""],
    ["whitespace", "   "],
    ["unknown", "Yellow"],
  ])("%s → undefined", (_label, input) => {
    expect(tryParseTruthyUndefEnum(Color, input)).toBeUndefined();
  });
});
