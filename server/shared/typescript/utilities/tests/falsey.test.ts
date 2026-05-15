// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { falsey } from "../src/falsey.js";
import { EMPTY_UUID } from "../src/regex.js";

describe("falsey", () => {
  it.each([
    ["null", null, true],
    ["undefined", undefined, true],
    ["empty string", "", true],
    ["whitespace string", "   ", true],
    ["tab+newline string", "\t\n", true],
    ["truthy string", "x", false],
    ["empty array", [], true],
    ["one-element array", [0], false],
    ["empty Set", new Set(), true],
    ["non-empty Set", new Set([1]), false],
    ["empty Map", new Map(), true],
    ["non-empty Map", new Map([["k", "v"]]), false],
    ["zero number", 0, false],
    ["false bool", false, false],
    ["object", {}, false],
  ])("%s → %s", (_label, input, expected) => {
    expect(falsey(input)).toBe(expected);
  });

  it("treats empty UUID string as a regular non-empty string", () => {
    // EMPTY_UUID is non-empty AS A STRING — falsey only checks string-emptiness.
    // tryParseTruthyNullUuid is the helper that collapses empty UUID → null.
    expect(falsey(EMPTY_UUID)).toBe(false);
  });
});
