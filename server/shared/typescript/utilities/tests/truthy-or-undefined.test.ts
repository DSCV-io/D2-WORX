// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { truthyOrUndefined } from "../src/truthy-or-undefined.js";

/**
 * Adversarial coverage for `truthyOrUndefined`. Validates the wire-boundary
 * contract: null / undefined / empty / whitespace-only inputs return
 * `undefined`; truthy inputs return the TRIMMED string (so callers never
 * need a second `.trim()` call).
 */
describe("truthyOrUndefined", () => {
  // --------------------------------------------------------------------
  // Falsey inputs → undefined
  // --------------------------------------------------------------------

  it("returns undefined for null input", () => {
    expect(truthyOrUndefined(null)).toBeUndefined();
  });

  it("returns undefined for undefined input", () => {
    expect(truthyOrUndefined(undefined)).toBeUndefined();
  });

  it("returns undefined for empty string", () => {
    expect(truthyOrUndefined("")).toBeUndefined();
  });

  it.each([
    ["single space", " "],
    ["multiple spaces", "   "],
    ["tab only", "\t"],
    ["newline only", "\n"],
    ["mixed whitespace (tab + newline + space)", "\t\n   "],
    ["carriage-return + newline", "\r\n"],
  ])("returns undefined for whitespace-only input: %s", (_label, input) => {
    expect(truthyOrUndefined(input)).toBeUndefined();
  });

  // --------------------------------------------------------------------
  // Truthy inputs → trimmed string
  // --------------------------------------------------------------------

  it("returns the value unchanged for a plain non-empty string", () => {
    expect(truthyOrUndefined("france")).toBe("france");
  });

  it("returns trimmed string when input has leading whitespace", () => {
    expect(truthyOrUndefined("  france")).toBe("france");
  });

  it("returns trimmed string when input has trailing whitespace", () => {
    expect(truthyOrUndefined("france  ")).toBe("france");
  });

  it("returns trimmed string when input has leading and trailing whitespace", () => {
    expect(truthyOrUndefined("  france  ")).toBe("france");
  });

  it("returns trimmed string when input has tab+newline padding", () => {
    expect(truthyOrUndefined("\tfrance\n")).toBe("france");
  });

  it("preserves internal whitespace (only outer whitespace is trimmed)", () => {
    // Internal whitespace is NOT collapsed — that is NameNormalizer's job.
    expect(truthyOrUndefined("  united states  ")).toBe("united states");
  });

  it("returns a single-character truthy string", () => {
    expect(truthyOrUndefined("x")).toBe("x");
  });

  it("returns a string consisting solely of non-whitespace punctuation", () => {
    expect(truthyOrUndefined("AT&T")).toBe("AT&T");
  });

  // --------------------------------------------------------------------
  // Wire-boundary carve-out — null / undefined from JSON / cookies / DB
  // --------------------------------------------------------------------

  it("accepts null (wire null from JSON) and returns undefined", () => {
    // The parameter type is `string | null | undefined` by design —
    // null is a legitimate wire value that must normalize to undefined.
    const wireNull: string | null = null;
    expect(truthyOrUndefined(wireNull)).toBeUndefined();
  });

  it("accepts undefined (missing header/cookie) and returns undefined", () => {
    const missingHeader: string | undefined = undefined;
    expect(truthyOrUndefined(missingHeader)).toBeUndefined();
  });

  // --------------------------------------------------------------------
  // Idempotency — truthyOrUndefined(truthyOrUndefined(x)) == truthyOrUndefined(x)
  // (when the result is a string, applying again should return the same value)
  // --------------------------------------------------------------------

  it("is idempotent for truthy string results (applying twice is same as once)", () => {
    const once = truthyOrUndefined("  france  ");
    // once is "france" (string). truthyOrUndefined("france") → "france".
    expect(truthyOrUndefined(once)).toBe(once);
  });
});
