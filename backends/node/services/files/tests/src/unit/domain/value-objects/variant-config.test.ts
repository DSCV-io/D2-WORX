import { describe, it, expect } from "vitest";
import { requiresResize } from "@d2/files-domain";
import type { VariantConfig } from "@d2/files-domain";

describe("VariantConfig", () => {
  describe("requiresResize", () => {
    it("should return true when maxDimension is a positive number", () => {
      const config: VariantConfig = { name: "thumb", maxDimension: 64 };
      expect(requiresResize(config)).toBe(true);
    });

    it("should return true for large maxDimension", () => {
      const config: VariantConfig = { name: "large", maxDimension: 2048 };
      expect(requiresResize(config)).toBe(true);
    });

    it("should return false when maxDimension is undefined (original)", () => {
      const config: VariantConfig = { name: "original" };
      expect(requiresResize(config)).toBe(false);
    });

    it("should return false when maxDimension is 0", () => {
      const config: VariantConfig = { name: "raw", maxDimension: 0 };
      expect(requiresResize(config)).toBe(false);
    });

    it("should return false when maxDimension is negative", () => {
      const config: VariantConfig = { name: "bad", maxDimension: -1 };
      expect(requiresResize(config)).toBe(false);
    });

    it.each([
      { name: "thumb", maxDimension: 64, expected: true },
      { name: "small", maxDimension: 128, expected: true },
      { name: "preview", maxDimension: 800, expected: true },
      { name: "original", expected: false },
      { name: "raw", maxDimension: 0, expected: false },
    ] as Array<VariantConfig & { expected: boolean }>)(
      "should return $expected for variant '$name' (maxDim=$maxDimension)",
      ({ expected, ...config }) => {
        expect(requiresResize(config)).toBe(expected);
      },
    );
  });
});
