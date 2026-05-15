// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  tryParseTruthyNullEnum,
  tryParseTruthyNullInt,
  tryParseTruthyNullUuid,
} from "../src/parse.js";
import { EMPTY_UUID } from "../src/regex.js";

describe("tryParseTruthyNullUuid", () => {
  it("returns canonical lowercase UUID on success", () => {
    expect(tryParseTruthyNullUuid("550E8400-E29B-41D4-A716-446655440000")).toBe(
      "550e8400-e29b-41d4-a716-446655440000",
    );
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
  ])("%s → null", (_label, input) => {
    expect(tryParseTruthyNullUuid(input)).toBe(null);
  });
});

describe("tryParseTruthyNullInt", () => {
  it.each([
    ["happy positive", "42", 42],
    ["happy negative", "-7", -7],
    ["zero", "0", 0],
    ["padded", "  42  ", 42],
  ])("%s → %s", (_label, input, expected) => {
    expect(tryParseTruthyNullInt(input)).toBe(expected);
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
  ])("%s → null", (_label, input) => {
    expect(tryParseTruthyNullInt(input)).toBe(null);
  });
});

describe("tryParseTruthyNullEnum", () => {
  const Color = { Red: "red", Green: "green", Blue: "blue" } as const;

  it.each([
    ["exact match", "Red", "Red"],
    ["case-insensitive", "red", "Red"],
    ["uppercase", "BLUE", "Blue"],
    ["padded", "  Green  ", "Green"],
  ])("%s → %s", (_label, input, expected) => {
    expect(tryParseTruthyNullEnum(Color, input)).toBe(expected);
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["empty", ""],
    ["whitespace", "   "],
    ["unknown", "Yellow"],
  ])("%s → null", (_label, input) => {
    expect(tryParseTruthyNullEnum(Color, input)).toBe(null);
  });
});
