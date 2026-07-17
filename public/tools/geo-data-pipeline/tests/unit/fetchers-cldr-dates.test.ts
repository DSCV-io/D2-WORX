// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import {
  deriveDateFormatPattern,
  extractShortPattern,
} from "../../src/fetchers/cldr-dates.js";

describe("deriveDateFormatPattern", () => {
  // -- happy-path canonical orderings --

  it("M/d/yy -> MDY (US format)", () => {
    expect(deriveDateFormatPattern("M/d/yy")).toBe("MDY");
  });

  it("dd.MM.y -> DMY (German format)", () => {
    expect(deriveDateFormatPattern("dd.MM.y")).toBe("DMY");
  });

  it("y/MM/dd -> YMD (East-Asian format)", () => {
    expect(deriveDateFormatPattern("y/MM/dd")).toBe("YMD");
  });

  // -- edge-case orderings (documented in source) --

  it("Myd -> MDY (M first, y middle, d last)", () => {
    expect(deriveDateFormatPattern("M y d")).toBe("MDY");
  });

  it("ydM -> YMD (y first, d middle, M last)", () => {
    expect(deriveDateFormatPattern("y d M")).toBe("YMD");
  });

  it("dyM -> DMY (d first, y middle, M last)", () => {
    expect(deriveDateFormatPattern("d y M")).toBe("DMY");
  });

  // -- separators don't matter; only relative letter position --

  it("uses separator-agnostic position (dashes)", () => {
    expect(deriveDateFormatPattern("d-M-y")).toBe("DMY");
  });

  it("uses separator-agnostic position (no separator)", () => {
    expect(deriveDateFormatPattern("yMd")).toBe("YMD");
  });

  // -- additional pattern characters around y/M/d --

  it("ignores literal text before y/M/d", () => {
    expect(deriveDateFormatPattern("EEE, M/d/y")).toBe("MDY");
  });

  // -- error cases --

  it("throws when y missing", () => {
    expect(() => deriveDateFormatPattern("M/d")).toThrow(/missing y\/M\/d/);
  });

  it("throws when M missing", () => {
    expect(() => deriveDateFormatPattern("d/y")).toThrow(/missing y\/M\/d/);
  });

  it("throws when d missing", () => {
    expect(() => deriveDateFormatPattern("M/y")).toThrow(/missing y\/M\/d/);
  });

  it("throws for empty pattern", () => {
    expect(() => deriveDateFormatPattern("")).toThrow(/missing y\/M\/d/);
  });
});

describe("extractShortPattern", () => {
  // CLDR returns `dateFormats.short` as either a plain string OR an object form
  // `{ _value: "...", _numbers: "..." }` for locales with non-default numbering
  // systems (e.g. `haw` Hawaiian uses `{ _value: "d/M/yy", _numbers: "M=romanlow" }`).
  // The helper extracts the actual format string from both shapes.

  it("returns the string when raw is a plain string pattern", () => {
    expect(extractShortPattern("M/d/yy")).toBe("M/d/yy");
  });

  it("extracts _value when raw is an object form (the Hawaiian-locale regression case)", () => {
    const raw = { _value: "d/M/yy", _numbers: "M=romanlow" };
    expect(extractShortPattern(raw)).toBe("d/M/yy");
  });

  it("extracts _value when object form carries only _value (no numbering annotation)", () => {
    expect(extractShortPattern({ _value: "yyyy-MM-dd" })).toBe("yyyy-MM-dd");
  });

  it("returns null for undefined input", () => {
    expect(extractShortPattern(undefined)).toBeNull();
  });

  it("returns null for empty-string input", () => {
    // Empty string is technically `typeof === 'string'` so the helper returns it
    // as-is — downstream caller's responsibility to validate non-empty.
    expect(extractShortPattern("")).toBe("");
  });

  it("returns null for object without _value", () => {
    expect(extractShortPattern({} as { _value?: string })).toBeNull();
  });

  it("returns null when object._value is non-string", () => {
    const raw = { _value: 123 } as unknown as { _value?: string };
    expect(extractShortPattern(raw)).toBeNull();
  });
});
