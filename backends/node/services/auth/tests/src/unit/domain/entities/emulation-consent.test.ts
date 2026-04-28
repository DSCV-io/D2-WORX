import { describe, it, expect } from "vitest";
import {
  createEmulationConsent,
  revokeEmulationConsent,
  isConsentActive,
  AuthValidationError,
} from "@d2/auth-domain";

describe("EmulationConsent", () => {
  const futureDate = new Date(Date.now() + 24 * 60 * 60 * 1000); // 1 day from now
  const validInput = {
    userId: "user-123",
    grantedToOrgId: "org-456",
    expiresAt: futureDate,
  };

  describe("createEmulationConsent", () => {
    it("should create a consent with valid input", () => {
      const consent = createEmulationConsent(validInput);
      expect(consent.userId).toBe("user-123");
      expect(consent.grantedToOrgId).toBe("org-456");
      expect(consent.expiresAt).toBe(futureDate);
      expect(consent.revokedAt).toBeUndefined();
      expect(consent.id).toHaveLength(36);
      expect(consent.createdAt).toBeInstanceOf(Date);
    });

    it("should accept a pre-generated ID", () => {
      const consent = createEmulationConsent({ ...validInput, id: "custom-id" });
      expect(consent.id).toBe("custom-id");
    });

    it("should throw AuthValidationError for empty userId", () => {
      expect(() => createEmulationConsent({ ...validInput, userId: "" })).toThrow(
        AuthValidationError,
      );
    });

    it("should throw AuthValidationError for empty grantedToOrgId", () => {
      expect(() => createEmulationConsent({ ...validInput, grantedToOrgId: "" })).toThrow(
        AuthValidationError,
      );
    });

    it("should throw AuthValidationError for past expiresAt", () => {
      const pastDate = new Date(Date.now() - 1000);
      expect(() => createEmulationConsent({ ...validInput, expiresAt: pastDate })).toThrow(
        AuthValidationError,
      );
    });

    it("should throw AuthValidationError for invalid date", () => {
      expect(() =>
        createEmulationConsent({ ...validInput, expiresAt: new Date("invalid") }),
      ).toThrow(AuthValidationError);
    });
  });

  describe("revokeEmulationConsent", () => {
    it("should set revokedAt to a date", () => {
      const consent = createEmulationConsent(validInput);
      const revoked = revokeEmulationConsent(consent);
      expect(revoked.revokedAt).toBeInstanceOf(Date);
      expect(revoked.userId).toBe(consent.userId);
      expect(revoked.grantedToOrgId).toBe(consent.grantedToOrgId);
    });

    it("should preserve all other fields", () => {
      const consent = createEmulationConsent(validInput);
      const revoked = revokeEmulationConsent(consent);
      expect(revoked.id).toBe(consent.id);
      expect(revoked.expiresAt).toBe(consent.expiresAt);
      expect(revoked.createdAt).toBe(consent.createdAt);
    });
  });

  describe("isConsentActive", () => {
    it("should return true for non-revoked, non-expired consent", () => {
      const consent = createEmulationConsent(validInput);
      expect(isConsentActive(consent)).toBe(true);
    });

    it("should return false for revoked consent", () => {
      const consent = createEmulationConsent(validInput);
      const revoked = revokeEmulationConsent(consent);
      expect(isConsentActive(revoked)).toBe(false);
    });

    it("should return false for expired consent", () => {
      const consent = createEmulationConsent(validInput);
      // Manually construct an expired consent for testing
      const expired = { ...consent, expiresAt: new Date(Date.now() - 1000) };
      expect(isConsentActive(expired)).toBe(false);
    });
  });
});
