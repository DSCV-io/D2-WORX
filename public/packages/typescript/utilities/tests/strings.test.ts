// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { cleanDisplayStr, cleanStr, toUndefIfEmpty } from "../src/strings.js";

describe("toUndefIfEmpty", () => {
  it.each([
    [null, undefined],
    [undefined, undefined],
    ["", undefined],
    ["   ", undefined],
    ["  hello  ", "hello"],
    ["hello", "hello"],
  ])("input %j → %j", (input, expected) => {
    expect(toUndefIfEmpty(input)).toBe(expected);
  });
});

describe("cleanStr", () => {
  it("collapses internal whitespace runs and trims", () => {
    expect(cleanStr("  hello   \tworld\n  ")).toBe("hello world");
  });

  it("returns undefined on null/empty/whitespace", () => {
    expect(cleanStr(null)).toBeUndefined();
    expect(cleanStr(undefined)).toBeUndefined();
    expect(cleanStr("")).toBeUndefined();
    expect(cleanStr("   ")).toBeUndefined();
  });

  it("preserves non-whitespace chars", () => {
    expect(cleanStr("a-b'c.d,e")).toBe("a-b'c.d,e");
  });

  it("handles oversized input without truncation", () => {
    const big = `${"x".repeat(1024)}  ${"y".repeat(1024)}`;
    expect(cleanStr(big)).toBe(`${"x".repeat(1024)} ${"y".repeat(1024)}`);
  });
});

describe("cleanDisplayStr", () => {
  it("strips disallowed chars and collapses whitespace", () => {
    expect(cleanDisplayStr("<b>Hello</b> World")).toBe("bHellob World");
  });

  it("returns undefined on null/empty", () => {
    expect(cleanDisplayStr(null)).toBeUndefined();
    expect(cleanDisplayStr("")).toBeUndefined();
    expect(cleanDisplayStr("   ")).toBeUndefined();
  });

  it("preserves Unicode letters from any script", () => {
    expect(cleanDisplayStr("Жанна Иванова")).toBe("Жанна Иванова");
    expect(cleanDisplayStr("田中 太郎")).toBe("田中 太郎");
  });

  it("preserves punctuation chars in allowlist", () => {
    expect(cleanDisplayStr("Mary-Anne O'Neill, Jr.")).toBe(
      "Mary-Anne O'Neill, Jr.",
    );
  });
});
