import { describe, it, expect } from "vitest";
import {
  generateOtpCode,
  hashOtpCode,
  pendingChangeIdentifier,
  encodePendingValue,
  decodePendingValue,
  type PendingChangeValue,
} from "@d2/auth-domain";

describe("otp-rules", () => {
  describe("generateOtpCode", () => {
    it("returns a 6-character zero-padded numeric string", () => {
      for (let i = 0; i < 50; i++) {
        const code = generateOtpCode();
        expect(code).toMatch(/^\d{6}$/);
        expect(code.length).toBe(6);
      }
    });

    it("produces different codes across calls (probabilistic)", () => {
      const codes = new Set<string>();
      for (let i = 0; i < 100; i++) codes.add(generateOtpCode());
      // 100 draws from 1M-space — collisions theoretically possible but extremely rare.
      expect(codes.size).toBeGreaterThan(95);
    });
  });

  describe("hashOtpCode", () => {
    it("returns a 64-char hex string (sha256)", () => {
      const hash = hashOtpCode("123456");
      expect(hash).toMatch(/^[a-f0-9]{64}$/);
    });

    it("is deterministic for the same input", () => {
      expect(hashOtpCode("123456")).toBe(hashOtpCode("123456"));
    });

    it("produces different hashes for different inputs", () => {
      expect(hashOtpCode("123456")).not.toBe(hashOtpCode("123457"));
    });

    it("hashes empty string consistently", () => {
      // Edge: even empty produces the standard sha256 of "".
      expect(hashOtpCode("")).toMatch(/^[a-f0-9]{64}$/);
    });
  });

  describe("pendingChangeIdentifier", () => {
    it("formats email identifier", () => {
      expect(pendingChangeIdentifier("email", "user-123")).toBe("account-change:email:user-123");
    });

    it("formats phone identifier", () => {
      expect(pendingChangeIdentifier("phone", "user-123")).toBe("account-change:phone:user-123");
    });

    it("does not collide between types for the same user", () => {
      const userId = "01234567-89ab-cdef-0123-456789abcdef";
      expect(pendingChangeIdentifier("email", userId)).not.toBe(
        pendingChangeIdentifier("phone", userId),
      );
    });
  });

  describe("encodePendingValue / decodePendingValue", () => {
    const valid: PendingChangeValue = {
      codeHash: "a".repeat(64),
      pendingValue: "new@example.com",
      attempts: 2,
    };

    it("round-trips a valid PendingChangeValue", () => {
      const encoded = encodePendingValue(valid);
      const decoded = decodePendingValue(encoded);
      expect(decoded).toEqual(valid);
    });

    it("encodes as JSON", () => {
      const encoded = encodePendingValue(valid);
      expect(() => JSON.parse(encoded)).not.toThrow();
    });

    it("decode returns null on malformed JSON", () => {
      expect(decodePendingValue("not json")).toBeNull();
      expect(decodePendingValue("{")).toBeNull();
    });

    it("decode returns null on missing fields", () => {
      expect(decodePendingValue(JSON.stringify({ codeHash: "abc" }))).toBeNull();
      expect(decodePendingValue(JSON.stringify({ codeHash: "abc", pendingValue: "x" }))).toBeNull();
    });

    it("decode returns null on wrong field types", () => {
      expect(
        decodePendingValue(JSON.stringify({ codeHash: 123, pendingValue: "x", attempts: 0 })),
      ).toBeNull();
      expect(
        decodePendingValue(JSON.stringify({ codeHash: "abc", pendingValue: "x", attempts: "0" })),
      ).toBeNull();
    });

    it("decode returns null on null/undefined/non-object", () => {
      expect(decodePendingValue("null")).toBeNull();
      expect(decodePendingValue("[]")).toBeNull();
      expect(decodePendingValue('"string"')).toBeNull();
      expect(decodePendingValue("123")).toBeNull();
    });
  });
});
