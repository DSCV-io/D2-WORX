// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { truthy } from "../src/truthy.js";

describe("truthy", () => {
  it.each([
    ["null", null, false],
    ["undefined", undefined, false],
    ["empty string", "", false],
    ["whitespace", " \t", false],
    ["non-empty string", "x", true],
    ["empty array", [], false],
    ["non-empty array", [1], true],
    ["empty Set", new Set(), false],
    ["non-empty Map", new Map([["k", 1]]), true],
  ])("%s → %s", (_label, input, expected) => {
    expect(truthy(input)).toBe(expected);
  });
});
