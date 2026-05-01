import { describe, it, expect } from "vitest";
import { getFieldStatus } from "../field-status.js";

describe("getFieldStatus", () => {
  describe("returns 'invalid' when errors are present", () => {
    it("with a single error", () => {
      expect(getFieldStatus({ errors: ["Required"], value: "" })).toBe("invalid");
    });

    it("with multiple errors", () => {
      expect(getFieldStatus({ errors: ["Too short", "Bad format"], value: "x" })).toBe("invalid");
    });

    it("even when value is non-empty", () => {
      expect(getFieldStatus({ errors: ["Invalid"], value: "some value" })).toBe("invalid");
    });
  });

  describe("returns 'valid' when no errors and value is non-empty", () => {
    it("with a string value", () => {
      expect(getFieldStatus({ errors: undefined, value: "Jane" })).toBe("valid");
    });

    it("with an empty errors array (no errors)", () => {
      expect(getFieldStatus({ errors: [], value: "Jane" })).toBe("valid");
    });

    it("with a numeric value", () => {
      expect(getFieldStatus({ errors: undefined, value: 42 })).toBe("valid");
    });

    it("with a boolean true", () => {
      expect(getFieldStatus({ errors: undefined, value: true })).toBe("valid");
    });

    it("with a boolean false", () => {
      expect(getFieldStatus({ errors: undefined, value: false })).toBe("valid");
    });

    it("with a non-empty array", () => {
      expect(getFieldStatus({ errors: undefined, value: ["item"] })).toBe("valid");
    });

    it("with an object", () => {
      expect(getFieldStatus({ errors: undefined, value: { key: "val" } })).toBe("valid");
    });

    it("with zero (falsy but non-empty)", () => {
      expect(getFieldStatus({ errors: undefined, value: 0 })).toBe("valid");
    });
  });

  describe("returns 'idle' when no errors and value is empty", () => {
    it("with empty string", () => {
      expect(getFieldStatus({ errors: undefined, value: "" })).toBe("idle");
    });

    it("with whitespace-only string (treated as empty)", () => {
      expect(getFieldStatus({ errors: undefined, value: "   " })).toBe("idle");
    });

    it("with tab-only string", () => {
      expect(getFieldStatus({ errors: undefined, value: "\t" })).toBe("idle");
    });

    it("with null value", () => {
      expect(getFieldStatus({ errors: undefined, value: null })).toBe("idle");
    });

    it("with undefined value", () => {
      expect(getFieldStatus({ errors: undefined, value: undefined })).toBe("idle");
    });

    it("with empty array", () => {
      expect(getFieldStatus({ errors: undefined, value: [] })).toBe("idle");
    });

    it("with empty errors array and empty value", () => {
      expect(getFieldStatus({ errors: [], value: "" })).toBe("idle");
    });
  });

  describe("errors take priority over value", () => {
    it("errors present + empty value = invalid", () => {
      expect(getFieldStatus({ errors: ["Required"], value: "" })).toBe("invalid");
    });

    it("errors present + null value = invalid", () => {
      expect(getFieldStatus({ errors: ["Required"], value: null })).toBe("invalid");
    });

    it("errors present + non-empty value = invalid (errors always win)", () => {
      expect(getFieldStatus({ errors: ["Too long"], value: "a".repeat(300) })).toBe("invalid");
    });
  });
});
