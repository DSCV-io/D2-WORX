// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { applyBump, parseVersion, renderVersion } from "../src/semver.js";

// ---------------------------------------------------------------------------
// parseVersion
// ---------------------------------------------------------------------------

describe("parseVersion — valid strings", () => {
  it("parses 0.1.0 correctly", () => {
    const v = parseVersion("0.1.0");
    expect(v.major).toBe(0);
    expect(v.minor).toBe(1);
    expect(v.patch).toBe(0);
  });

  it("parses 1.2.3 correctly", () => {
    const v = parseVersion("1.2.3");
    expect(v.major).toBe(1);
    expect(v.minor).toBe(2);
    expect(v.patch).toBe(3);
  });

  it("parses 10.0.0 correctly", () => {
    const v = parseVersion("10.0.0");
    expect(v.major).toBe(10);
    expect(v.minor).toBe(0);
    expect(v.patch).toBe(0);
  });

  it("trims surrounding whitespace before parsing", () => {
    const v = parseVersion("  2.3.4  ");
    expect(v.major).toBe(2);
    expect(v.minor).toBe(3);
    expect(v.patch).toBe(4);
  });
});

describe("parseVersion — invalid strings (fail-loud)", () => {
  it("throws on empty string", () => {
    expect(() => parseVersion("")).toThrow();
  });

  it("throws on two-part semver", () => {
    expect(() => parseVersion("1.2")).toThrow();
  });

  it("throws on pre-release suffix", () => {
    expect(() => parseVersion("1.2.3-alpha")).toThrow();
  });

  it("throws on non-numeric component", () => {
    expect(() => parseVersion("a.b.c")).toThrow();
  });

  it("throws on missing-patch", () => {
    expect(() => parseVersion("1.")).toThrow();
  });
});

// ---------------------------------------------------------------------------
// applyBump
// ---------------------------------------------------------------------------

describe("applyBump — pre-stable (0.x)", () => {
  it("patch bump increments patch", () => {
    expect(applyBump({ major: 0, minor: 1, patch: 0 }, "patch")).toBe("0.1.1");
  });

  it("minor bump increments minor and resets patch", () => {
    expect(applyBump({ major: 0, minor: 1, patch: 3 }, "minor")).toBe("0.2.0");
  });

  it("major bump increments major and resets minor + patch", () => {
    expect(applyBump({ major: 0, minor: 5, patch: 2 }, "major")).toBe("1.0.0");
  });

  it("none bump returns version unchanged", () => {
    expect(applyBump({ major: 0, minor: 1, patch: 0 }, "none")).toBe("0.1.0");
  });
});

describe("applyBump — stable (1.x)", () => {
  it("patch bump on stable version increments patch", () => {
    expect(applyBump({ major: 1, minor: 2, patch: 3 }, "patch")).toBe("1.2.4");
  });

  it("minor bump on stable version increments minor and resets patch", () => {
    expect(applyBump({ major: 1, minor: 2, patch: 3 }, "minor")).toBe("1.3.0");
  });

  it("major bump on stable version increments major and resets minor + patch", () => {
    expect(applyBump({ major: 1, minor: 2, patch: 3 }, "major")).toBe("2.0.0");
  });

  it("none bump on stable version returns version unchanged", () => {
    expect(applyBump({ major: 2, minor: 0, patch: 0 }, "none")).toBe("2.0.0");
  });
});

// ---------------------------------------------------------------------------
// renderVersion
// ---------------------------------------------------------------------------

describe("renderVersion", () => {
  it("renders 0.0.0", () => {
    expect(renderVersion({ major: 0, minor: 0, patch: 0 })).toBe("0.0.0");
  });

  it("renders double-digit components", () => {
    expect(renderVersion({ major: 10, minor: 20, patch: 30 })).toBe("10.20.30");
  });
});
