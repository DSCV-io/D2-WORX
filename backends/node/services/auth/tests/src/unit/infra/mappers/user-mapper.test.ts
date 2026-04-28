import { describe, it, expect } from "vitest";
import { toDomainUser } from "@d2/auth-infra";

describe("toDomainUser", () => {
  it("should map camelCase BetterAuth user to domain User", () => {
    const raw = {
      id: "user-123",
      email: "test@example.com",
      name: "Test User",
      username: "swiftriver482",
      displayUsername: "SwiftRiver482",
      emailVerified: true,
      image: "https://example.com/avatar.png",
      createdAt: new Date("2026-01-01"),
      updatedAt: new Date("2026-01-02"),
    };

    const user = toDomainUser(raw);

    expect(user.id).toBe("user-123");
    expect(user.email).toBe("test@example.com");
    expect(user.name).toBe("Test User");
    expect(user.username).toBe("swiftriver482");
    expect(user.displayUsername).toBe("SwiftRiver482");
    expect(user.emailVerified).toBe(true);
    expect(user.image).toBe("https://example.com/avatar.png");
    expect(user.createdAt).toEqual(new Date("2026-01-01"));
    expect(user.updatedAt).toEqual(new Date("2026-01-02"));
  });

  it("should map snake_case fields", () => {
    const raw = {
      id: "user-456",
      email: "snake@example.com",
      name: "Snake User",
      username: "brightfalcon123",
      display_username: "BrightFalcon123",
      email_verified: false,
      image: null,
      created_at: "2026-01-01T00:00:00Z",
      updated_at: "2026-01-02T00:00:00Z",
    };

    const user = toDomainUser(raw);

    expect(user.emailVerified).toBe(false);
    expect(user.image).toBeUndefined();
    expect(user.username).toBe("brightfalcon123");
    expect(user.displayUsername).toBe("BrightFalcon123");
  });

  it("should default image to undefined when missing", () => {
    const raw = {
      id: "user-789",
      email: "noimg@example.com",
      name: "No Image",
      username: "keenowl55",
      displayUsername: "KeenOwl55",
      emailVerified: false,
      createdAt: new Date(),
      updatedAt: new Date(),
    };

    const user = toDomainUser(raw);
    expect(user.image).toBeUndefined();
  });

  it("should map locale field when present", () => {
    const raw = {
      id: "user-loc",
      email: "loc@example.com",
      name: "Locale User",
      username: "localeuser",
      displayUsername: "LocaleUser",
      emailVerified: true,
      image: undefined,
      locale: "de",
      createdAt: new Date(),
      updatedAt: new Date(),
    };

    const user = toDomainUser(raw);
    expect(user.locale).toBe("de");
  });

  it("should default locale to 'en' when missing", () => {
    const raw = {
      id: "user-noloc",
      email: "noloc@example.com",
      name: "No Locale",
      username: "nolocale",
      displayUsername: "NoLocale",
      emailVerified: false,
      createdAt: new Date(),
      updatedAt: new Date(),
    };

    const user = toDomainUser(raw);
    expect(user.locale).toBe("en-US");
  });
});
