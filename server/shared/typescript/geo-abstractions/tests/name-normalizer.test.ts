// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { normalize } from "../src/name-resolution/name-normalizer.js";

describe("normalize", () => {
  it("preserves unspaced ampersand (AT&T stays at&t)", () => {
    // Spaced-form-only substitution — `&` without surrounding whitespace
    // is preserved literally. .NET parity: AT&T → at&t.
    expect(normalize("AT&T")).toBe("at&t");
  });

  it("substitutes spaced ampersand token (' & ' → ' and ')", () => {
    expect(normalize("Foo & Bar")).toBe("foo and bar");
  });

  it("returns empty string for empty input", () => {
    expect(normalize("")).toBe("");
  });

  it("returns empty string for whitespace-only input", () => {
    // Matches .NET Falsey() short-circuit on whitespace-only strings.
    expect(normalize("   ")).toBe("");
  });

  it("returns empty string for mixed-whitespace input (tabs / newlines)", () => {
    expect(normalize("\t\n\r ")).toBe("");
  });

  it("strips diacritics via NFD decomposition (São Paulo → sao paulo)", () => {
    expect(normalize("São Paulo")).toBe("sao paulo");
  });

  it("collapses internal whitespace runs to a single space", () => {
    expect(normalize("United   States")).toBe("united states");
  });

  it("trims leading and trailing whitespace", () => {
    expect(normalize("  Foo  ")).toBe("foo");
  });
});
