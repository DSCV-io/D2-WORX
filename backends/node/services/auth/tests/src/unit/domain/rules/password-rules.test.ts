import { describe, it, expect } from "vitest";
import { validatePassword } from "@d2/auth-domain";

describe("validatePassword", () => {
  describe("numeric-only rejection", () => {
    it("should reject all-digit passwords", () => {
      const result = validatePassword("123456789012");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_NUMERIC_ONLY");
      expect(result.message).toBe("auth_errors_PASSWORD_NUMERIC_ONLY");
    });

    it("should reject long numeric strings", () => {
      const result = validatePassword("000000000000000000");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_NUMERIC_ONLY");
    });
  });

  describe("date-like rejection", () => {
    it("should reject date with dashes", () => {
      const result = validatePassword("2025-10-01");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_DATE_LIKE");
      expect(result.message).toBe("auth_errors_PASSWORD_DATE_LIKE");
    });

    it("should reject date with slashes", () => {
      const result = validatePassword("25/01/1997");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_DATE_LIKE");
    });

    it("should reject date with dots", () => {
      const result = validatePassword("01.25.1997");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_DATE_LIKE");
    });

    it("should reject digits with spaces", () => {
      const result = validatePassword("12 34 56 78");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_DATE_LIKE");
    });

    it("should reject mixed date separators", () => {
      const result = validatePassword("2025/10-01.12");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_DATE_LIKE");
    });
  });

  describe("common password rejection", () => {
    it("should reject a keyboard-pattern common password", () => {
      const result = validatePassword("q1w2e3r4t5y6");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_TOO_COMMON");
      expect(result.message).toBe("auth_errors_PASSWORD_TOO_COMMON");
    });

    it("should reject another keyboard-pattern common password", () => {
      const result = validatePassword("1qaz2wsx3edc");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_TOO_COMMON");
    });

    it("should be case-insensitive", () => {
      const result = validatePassword("Q1W2E3R4T5Y6");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_TOO_COMMON");
    });

    it("should reject mixed-case common passwords", () => {
      const result = validatePassword("Unbelievable");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_TOO_COMMON");
    });

    it("should reject word-based common passwords", () => {
      const result = validatePassword("motherfucker");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_TOO_COMMON");
    });
  });

  describe("valid passwords", () => {
    it("should accept normal password with mixed characters", () => {
      const result = validatePassword("correcthorsebattery");
      expect(result.valid).toBe(true);
      expect(result.code).toBeUndefined();
      expect(result.message).toBeUndefined();
    });

    it("should accept password with special characters", () => {
      const result = validatePassword("myS3cureP@ss!!");
      expect(result.valid).toBe(true);
    });

    it("should accept letters + numbers (not numeric-only)", () => {
      const result = validatePassword("mybirthday1997jan");
      expect(result.valid).toBe(true);
    });

    it("should accept date with letters (not date-like-only)", () => {
      const result = validatePassword("born-1997-01-25-really");
      expect(result.valid).toBe(true);
    });

    it("should accept non-common passwords", () => {
      const result = validatePassword("xk8mQ!purple-fox");
      expect(result.valid).toBe(true);
    });
  });

  describe("priority ordering", () => {
    it("should prioritize numeric-only over common password check", () => {
      // All-digit string — numeric-only check runs before common password check
      const result = validatePassword("123456789012");
      expect(result.valid).toBe(false);
      expect(result.code).toBe("PASSWORD_NUMERIC_ONLY");
    });
  });
});
