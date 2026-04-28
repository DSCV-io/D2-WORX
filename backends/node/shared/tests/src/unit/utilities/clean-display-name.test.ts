import { describe, it, expect } from "vitest";
import { cleanDisplayStr, DISPLAY_NAME_INVALID_RE } from "@d2/utilities";

// ---------------------------------------------------------------------------
// cleanDisplayStr
// ---------------------------------------------------------------------------

describe("cleanDisplayStr", () => {
  // ---- 1. Passes through valid names ------------------------------------

  it.each([
    ["John", "John"],
    ["O'Brien", "O'Brien"],
    ["Mary-Jane", "Mary-Jane"],
    ["Dr. Smith", "Dr. Smith"],
    ["Jos\u00e9 Mar\u00eda", "Jos\u00e9 Mar\u00eda"],
    ["\u7530\u4E2D\u592A\u90CE", "\u7530\u4E2D\u592A\u90CE"],
    ["M\u00fcller, Hans", "M\u00fcller, Hans"],
  ])('passes through valid name "%s"', (input: string, expected: string) => {
    expect(cleanDisplayStr(input)).toBe(expected);
  });

  // ---- 2. Strips dangerous characters -----------------------------------

  it.each([
    ["<script>alert(1)</script>", "scriptalert1script"],
    ["**bold** text", "bold text"],
    ["[link](http://evil.com)", "linkhttpevil.com"],
    ["test`backtick`", "testbacktick"],
    ["$100 dollars", "100 dollars"],
    ['he said "hello"', "he said hello"],
  ])('strips dangerous characters from "%s" to "%s"', (input: string, expected: string) => {
    expect(cleanDisplayStr(input)).toBe(expected);
  });

  // ---- 3. Handles whitespace --------------------------------------------

  it("trims and collapses whitespace", () => {
    expect(cleanDisplayStr("  hello  world  ")).toBe("hello world");
  });

  it("returns undefined for whitespace-only input", () => {
    expect(cleanDisplayStr("   ")).toBeUndefined();
  });

  it("returns undefined for empty string", () => {
    expect(cleanDisplayStr("")).toBeUndefined();
  });

  // ---- 4. Handles null/undefined ----------------------------------------

  it("returns undefined for null", () => {
    expect(cleanDisplayStr(null)).toBeUndefined();
  });

  it("returns undefined for undefined", () => {
    expect(cleanDisplayStr(undefined)).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// DISPLAY_NAME_INVALID_RE (frontend validation)
// ---------------------------------------------------------------------------

describe("DISPLAY_NAME_INVALID_RE", () => {
  it("does not match valid display name (no invalid chars)", () => {
    expect(DISPLAY_NAME_INVALID_RE.test("John")).toBe(false);
  });

  it("matches string containing invalid chars", () => {
    expect(DISPLAY_NAME_INVALID_RE.test("<script>")).toBe(true);
  });
});
