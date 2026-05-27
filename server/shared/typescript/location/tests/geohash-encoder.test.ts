// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  decodeGeohash,
  encodeGeohash,
  isValidGeohash,
  truncateOrPadGeohash,
} from "../src/index.js";

describe("GeohashEncoder", () => {
  describe("encode — reference vectors", () => {
    it("(40.7128, -74.006) at precision 10", () => {
      const out = encodeGeohash(40.7128, -74.006, 10);
      expect(out).toHaveLength(10);
      expect(out).toMatch(/^[0-9b-hjkmnp-z]+$/);
    });

    it("(0, 0) — equator + meridian at precision 12", () => {
      const out = encodeGeohash(0.0, 0.0, 12);
      expect(out).toHaveLength(12);
    });

    it("(90, 0) — north pole", () => {
      expect(encodeGeohash(90.0, 0.0, 10)).toHaveLength(10);
    });

    it("(-90, 0) — south pole", () => {
      expect(encodeGeohash(-90.0, 0.0, 10)).toHaveLength(10);
    });

    it("(0, 180) — dateline east", () => {
      expect(encodeGeohash(0.0, 180.0, 10)).toHaveLength(10);
    });

    it("(0, -180) — dateline west", () => {
      expect(encodeGeohash(0.0, -180.0, 10)).toHaveLength(10);
    });
  });

  describe("encode/decode round-trip", () => {
    it("round-trip within cell bounds", () => {
      const lat = 40.7128;
      const lon = -74.006;
      const g = encodeGeohash(lat, lon, 10);
      const { latitude, longitude, latError, lonError } = decodeGeohash(g);
      expect(Math.abs(latitude - lat)).toBeLessThan(latError * 2);
      expect(Math.abs(longitude - lon)).toBeLessThan(lonError * 2);
    });
  });

  describe("truncateOrPad", () => {
    it("same length → unchanged", () => {
      expect(truncateOrPadGeohash("dr5regw3pp", 10)).toBe("dr5regw3pp");
    });

    it("longer → truncated", () => {
      expect(truncateOrPadGeohash("dr5regw3ppzz", 10)).toBe("dr5regw3pp");
    });

    it("shorter → padded via decode+re-encode", () => {
      const padded = truncateOrPadGeohash("dr5re", 10);
      expect(padded).toHaveLength(10);
    });
  });

  describe("isValidGeohash", () => {
    it.each(["dr5regw3pp", "u4pruy0k85", "s0000"])("%s → true", (g) => {
      expect(isValidGeohash(g)).toBe(true);
    });

    it.each(["", "a", "i", "l", "o", "INVALID!", "DR5"])("%j → false", (g) => {
      expect(isValidGeohash(g)).toBe(false);
    });
  });
});
