// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  decodePlusCode,
  encodePlusCode,
  isValidPlusCode,
} from "../src/index.js";

describe("PlusCodeEncoder", () => {
  describe("encode", () => {
    it("(40.7128, -74.006) length 10 → 11-char string", () => {
      const out = encodePlusCode(40.7128, -74.006, 10);
      expect(out).toHaveLength(11);
      expect(out.charAt(8)).toBe("+");
    });

    it("(0, 0) length 10 → 11-char string", () => {
      expect(encodePlusCode(0.0, 0.0, 10).charAt(8)).toBe("+");
    });

    it("near-north-pole — does not throw", () => {
      const out = encodePlusCode(89.9999, 0.0, 10);
      expect(out).toHaveLength(11);
    });
  });

  describe("encode/decode round-trip", () => {
    it("round-trip within cell bounds", () => {
      const lat = 40.7128;
      const lon = -74.006;
      const code = encodePlusCode(lat, lon, 10);
      const { latitude, longitude, latError, lonError } = decodePlusCode(code);
      expect(Math.abs(latitude - lat)).toBeLessThan(latError * 2 + 1e-6);
      expect(Math.abs(longitude - lon)).toBeLessThan(lonError * 2 + 1e-6);
    });
  });

  describe("isValidPlusCode", () => {
    it.each(["87G7MQ8V+RG", "87G7MQ8V+RG52"])("%s → true", (code) => {
      expect(isValidPlusCode(code)).toBe(true);
    });

    it.each([
      undefined,
      "",
      "   ",
      "NOPLUS",
      "87G7MQ8VRG",
      "87G7MQ8V+", // empty suffix — rejected per OLC spec
      "7FG49Q00+", // empty suffix — rejected per OLC spec
      "+87G7MQ8V",
      "AI+BC",
    ])("%j → false", (code) => {
      expect(isValidPlusCode(code as string | undefined)).toBe(false);
    });
  });

  // ---------------------------------------------------------------------
  // §2.1 Regression tests — F-A2-1 closure.
  // Pins the FULL_PAIRS = 4 invariant + Decode pair-count derivation.
  // ---------------------------------------------------------------------
  describe("regression — FULL_PAIRS invariant + Decode pair count", () => {
    it("encode_HasExactly8DigitsBeforePlusSeparator_PinsFullPairsCount", () => {
      // Regression: prior bug computed fullPairs = pairDigits / 2 → 5 pairs (10 chars).
      // OLC spec mandates 4 full pairs = 8 prefix digits before '+'.
      const code = encodePlusCode(40.7128, -74.006, 10);
      const plusIdx = code.indexOf("+");
      expect(plusIdx).toBe(8);
      expect(code.substring(0, plusIdx)).toHaveLength(8);
      expect(code.substring(plusIdx + 1)).toHaveLength(2);
    });

    it("decode_FullPairCountDerivedFromSeparatorPosition_NotStrippedLength", () => {
      // Regression: prior bug derived pair count from stripped-+ length → wrong fullPairs.
      // Re-encoding the decoded center must be byte-identical (proves pair-count invariant).
      const code = encodePlusCode(40.7128, -74.006, 10);
      const { latitude, longitude, latError, lonError } = decodePlusCode(code);
      expect(Math.abs(latitude - 40.7128)).toBeLessThanOrEqual(latError * 4);
      expect(Math.abs(longitude - -74.006)).toBeLessThanOrEqual(lonError * 4);
      const reEncoded = encodePlusCode(latitude, longitude, 10);
      expect(reEncoded).toBe(code);
    });
  });
});
