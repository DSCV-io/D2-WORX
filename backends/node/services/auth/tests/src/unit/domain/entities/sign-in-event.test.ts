import { describe, it, expect } from "vitest";
import { createSignInEvent, AuthValidationError } from "@d2/auth-domain";

describe("SignInEvent", () => {
  const validInput = {
    userId: "user-123",
    successful: true,
    ipAddress: "192.168.1.1",
    userAgent: "Mozilla/5.0",
  };

  describe("createSignInEvent", () => {
    it("should create an event with valid input", () => {
      const event = createSignInEvent(validInput);
      expect(event.userId).toBe("user-123");
      expect(event.successful).toBe(true);
      expect(event.ipAddress).toBe("192.168.1.1");
      expect(event.userAgent).toBe("Mozilla/5.0");
      expect(event.whoIsId).toBeUndefined();
      expect(event.id).toHaveLength(36);
      expect(event.createdAt).toBeInstanceOf(Date);
    });

    it("should accept a pre-generated ID", () => {
      const event = createSignInEvent({ ...validInput, id: "custom-id" });
      expect(event.id).toBe("custom-id");
    });

    it("should accept a whoIsId", () => {
      const event = createSignInEvent({ ...validInput, whoIsId: "whois-789" });
      expect(event.whoIsId).toBe("whois-789");
    });

    it("should create a failed sign-in event", () => {
      const event = createSignInEvent({ ...validInput, successful: false });
      expect(event.successful).toBe(false);
    });

    it("should throw AuthValidationError for empty userId", () => {
      expect(() => createSignInEvent({ ...validInput, userId: "" })).toThrow(AuthValidationError);
    });

    it("should throw AuthValidationError for empty ipAddress", () => {
      expect(() => createSignInEvent({ ...validInput, ipAddress: "" })).toThrow(
        AuthValidationError,
      );
    });

    it("should throw AuthValidationError for empty userAgent", () => {
      expect(() => createSignInEvent({ ...validInput, userAgent: "" })).toThrow(
        AuthValidationError,
      );
    });
  });
});
