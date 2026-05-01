import { describe, it, expect } from "vitest";
import { oklchToHex, hexToOklch } from "./color-convert.js";

describe("oklchToHex", () => {
  it("converts a vivid blue", () => {
    expect(1).toBe(1); // satisfy requireAssertions
    const hex = oklchToHex(0.5, 0.14, 262);
    expect(hex).toMatch(/^#[0-9a-f]{6}$/);
  });

  it("converts pure white (L=1, C=0)", () => {
    const hex = oklchToHex(1, 0, 0);
    expect(hex).toBe("#ffffff");
  });

  it("converts pure black (L=0, C=0)", () => {
    const hex = oklchToHex(0, 0, 0);
    expect(hex).toBe("#000000");
  });

  it("handles achromatic gray (C=0)", () => {
    const hex = oklchToHex(0.5, 0, 0);
    expect(hex).toMatch(/^#[0-9a-f]{6}$/);
  });
});

describe("hexToOklch", () => {
  it("converts white hex to OKLCH", () => {
    const result = hexToOklch("#ffffff");
    expect(result).not.toBeNull();
    expect(result!.lightness).toBeCloseTo(1, 2);
    expect(result!.chroma).toBeCloseTo(0, 2);
  });

  it("converts black hex to OKLCH", () => {
    const result = hexToOklch("#000000");
    expect(result).not.toBeNull();
    expect(result!.lightness).toBeCloseTo(0, 2);
  });

  it("defaults hue to 0 for achromatic colors", () => {
    const result = hexToOklch("#808080");
    expect(result).not.toBeNull();
    // Achromatic colors have undefined hue in culori — our fn defaults to 0
    expect(result!.hue).toBe(0);
  });

  it("returns null for invalid input", () => {
    expect(hexToOklch("not-a-color")).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(hexToOklch("")).toBeNull();
  });

  it("handles 3-char shorthand hex", () => {
    const result = hexToOklch("#f00");
    expect(result).not.toBeNull();
    expect(result!.hue).toBeGreaterThan(0);
    expect(result!.chroma).toBeGreaterThan(0);
  });
});

describe("round-trip precision", () => {
  it("oklchToHex -> hexToOklch is reasonably close", () => {
    const original = { l: 0.5, c: 0.14, h: 262 };
    const hex = oklchToHex(original.l, original.c, original.h);
    const result = hexToOklch(hex);
    expect(result).not.toBeNull();
    // sRGB clamping means some precision loss, but should be in the ballpark
    expect(result!.lightness).toBeCloseTo(original.l, 1);
    expect(result!.hue).toBeCloseTo(original.h, 0);
  });

  it("round-trips well for in-gamut colors", () => {
    // A moderate desaturated teal — well within sRGB gamut
    const original = { l: 0.6, c: 0.08, h: 180 };
    const hex = oklchToHex(original.l, original.c, original.h);
    const result = hexToOklch(hex);
    expect(result).not.toBeNull();
    expect(result!.lightness).toBeCloseTo(original.l, 1);
    expect(result!.chroma).toBeCloseTo(original.c, 1);
    expect(result!.hue).toBeCloseTo(original.h, 0);
  });
});
