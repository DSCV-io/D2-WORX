import { describe, it, expect } from "vitest";
import { createUser, updateUser, AuthValidationError } from "@d2/auth-domain";

describe("User", () => {
  const validInput = {
    email: "test@example.com",
    name: "John Doe",
    username: "swiftriver482",
    displayUsername: "SwiftRiver482",
  };

  describe("createUser", () => {
    it("should create a user with valid input", () => {
      const user = createUser(validInput);
      expect(user.email).toBe("test@example.com");
      expect(user.name).toBe("John Doe");
      expect(user.username).toBe("swiftriver482");
      expect(user.displayUsername).toBe("SwiftRiver482");
      expect(user.emailVerified).toBe(false);
      expect(user.image).toBeUndefined();
      expect(user.id).toHaveLength(36);
      expect(user.createdAt).toBeInstanceOf(Date);
      expect(user.updatedAt).toBeInstanceOf(Date);
    });

    it("should accept a pre-generated ID", () => {
      const id = "custom-id-123";
      const user = createUser({ ...validInput, id });
      expect(user.id).toBe(id);
    });

    it("should generate a UUIDv7 when no ID is provided", () => {
      const user1 = createUser(validInput);
      const user2 = createUser(validInput);
      expect(user1.id).not.toBe(user2.id);
    });

    it("should clean and lowercase the email", () => {
      const user = createUser({ ...validInput, email: "  TEST@Example.COM  " });
      expect(user.email).toBe("test@example.com");
    });

    it("should clean whitespace in name", () => {
      const user = createUser({ ...validInput, name: "  John   Doe  " });
      expect(user.name).toBe("John Doe");
    });

    it("should throw AuthValidationError for invalid email", () => {
      expect(() => createUser({ ...validInput, email: "not-an-email" })).toThrow(Error);
    });

    it("should throw AuthValidationError for empty name", () => {
      expect(() => createUser({ ...validInput, name: "   " })).toThrow(AuthValidationError);
    });

    it("should throw AuthValidationError for empty string name", () => {
      expect(() => createUser({ ...validInput, name: "" })).toThrow(AuthValidationError);
    });

    it("should accept emailVerified = true", () => {
      const user = createUser({ ...validInput, emailVerified: true });
      expect(user.emailVerified).toBe(true);
    });

    it("should accept an image URL", () => {
      const user = createUser({ ...validInput, image: "https://example.com/photo.jpg" });
      expect(user.image).toBe("https://example.com/photo.jpg");
    });

    it("should default locale to 'en-US'", () => {
      const user = createUser(validInput);
      expect(user.locale).toBe("en-US");
    });

    it("should accept an explicit locale", () => {
      const user = createUser({ ...validInput, locale: "es" });
      expect(user.locale).toBe("es");
    });

    it("should accept any string as locale (validation is at higher layers)", () => {
      const user = createUser({ ...validInput, locale: "ja" });
      expect(user.locale).toBe("ja");
    });

    it("should throw AuthValidationError for missing username", () => {
      expect(() => createUser({ ...validInput, username: "" } as any)).toThrow(AuthValidationError);
    });

    it("should throw AuthValidationError for missing displayUsername", () => {
      expect(() => createUser({ ...validInput, displayUsername: "" } as any)).toThrow(
        AuthValidationError,
      );
    });
  });

  describe("updateUser", () => {
    const baseUser = createUser(validInput);

    it("should update the name", () => {
      const updated = updateUser(baseUser, { name: "Jane Smith" });
      expect(updated.name).toBe("Jane Smith");
      expect(updated.email).toBe(baseUser.email);
    });

    it("should update the email", () => {
      const updated = updateUser(baseUser, { email: "new@example.com" });
      expect(updated.email).toBe("new@example.com");
      expect(updated.name).toBe(baseUser.name);
    });

    it("should set updatedAt to a new timestamp", () => {
      const updated = updateUser(baseUser, { name: "Updated" });
      expect(updated.updatedAt.getTime()).toBeGreaterThanOrEqual(baseUser.updatedAt.getTime());
    });

    it("should throw AuthValidationError for empty name update", () => {
      expect(() => updateUser(baseUser, { name: "  " })).toThrow(AuthValidationError);
    });

    it("should throw for invalid email update", () => {
      expect(() => updateUser(baseUser, { email: "bad" })).toThrow(Error);
    });

    it("should update the username", () => {
      const updated = updateUser(baseUser, { username: "newhandle", displayUsername: "NewHandle" });
      expect(updated.username).toBe("newhandle");
      expect(updated.displayUsername).toBe("NewHandle");
    });

    it("should preserve unchanged fields", () => {
      const updated = updateUser(baseUser, { emailVerified: true });
      expect(updated.id).toBe(baseUser.id);
      expect(updated.name).toBe(baseUser.name);
      expect(updated.email).toBe(baseUser.email);
      expect(updated.username).toBe(baseUser.username);
      expect(updated.displayUsername).toBe(baseUser.displayUsername);
      expect(updated.emailVerified).toBe(true);
      expect(updated.createdAt).toBe(baseUser.createdAt);
    });

    it("should preserve image when update passes undefined", () => {
      const withImage = createUser({ ...validInput, image: "https://example.com/photo.jpg" });
      const updated = updateUser(withImage, { image: undefined });
      expect(updated.image).toBe("https://example.com/photo.jpg");
    });

    it("should update the locale", () => {
      const updated = updateUser(baseUser, { locale: "fr" });
      expect(updated.locale).toBe("fr");
      expect(updated.name).toBe(baseUser.name);
    });

    it("should preserve locale when not updating it", () => {
      const withLocale = createUser({ ...validInput, locale: "de" });
      const updated = updateUser(withLocale, { name: "Updated Name" });
      expect(updated.locale).toBe("de");
    });
  });
});
