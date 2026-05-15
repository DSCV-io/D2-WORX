// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { cleanDisplayStr, cleanStr, toNullIfEmpty } from "../src/strings.js";

describe("toNullIfEmpty", () => {
  it.each([
    [null, null],
    [undefined, null],
    ["", null],
    ["   ", null],
    ["  hello  ", "hello"],
    ["hello", "hello"],
  ])("input %j → %j", (input, expected) => {
    expect(toNullIfEmpty(input)).toBe(expected);
  });
});

describe("cleanStr", () => {
  it("collapses internal whitespace runs and trims", () => {
    expect(cleanStr("  hello   \tworld\n  ")).toBe("hello world");
  });

  it("returns null on null/empty/whitespace", () => {
    expect(cleanStr(null)).toBe(null);
    expect(cleanStr(undefined)).toBe(null);
    expect(cleanStr("")).toBe(null);
    expect(cleanStr("   ")).toBe(null);
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

  it("returns null on null/empty", () => {
    expect(cleanDisplayStr(null)).toBe(null);
    expect(cleanDisplayStr("")).toBe(null);
    expect(cleanDisplayStr("   ")).toBe(null);
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
