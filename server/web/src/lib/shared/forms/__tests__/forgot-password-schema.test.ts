import { describe, it, expect } from "vitest";
import { createForgotPasswordSchema } from "../forgot-password-schema.js";

describe("createForgotPasswordSchema", () => {
  const schema = createForgotPasswordSchema();

  // --- Happy path ---

  it("accepts a valid email", () => {
    const result = schema.safeParse({ email: "user@example.com" });
    expect(result.success).toBe(true);
  });

  it("accepts a valid email with special chars (plus tag)", () => {
    const result = schema.safeParse({ email: "user+tag@example.com" });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.email).toBe("user+tag@example.com");
    }
  });

  // --- Trimming and lowercasing ---

  it("trims leading and trailing whitespace", () => {
    const result = schema.safeParse({ email: "  user@example.com  " });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.email).toBe("user@example.com");
    }
  });

  it("lowercases the email", () => {
    const result = schema.safeParse({ email: "User@Example.COM" });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.email).toBe("user@example.com");
    }
  });

  // --- Empty / whitespace ---

  it("rejects empty string", () => {
    const result = schema.safeParse({ email: "" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
      expect(issue?.message).toBe("Required");
    }
  });

  it("rejects whitespace-only (trimmed to empty)", () => {
    const result = schema.safeParse({ email: "   " });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
    }
  });

  // --- Invalid formats ---

  it("rejects missing @ (no domain separator)", () => {
    const result = schema.safeParse({ email: "userexample.com" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
      expect(issue?.message).toBe("Invalid email address");
    }
  });

  it("rejects missing domain", () => {
    const result = schema.safeParse({ email: "user@" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
    }
  });

  it("rejects double @", () => {
    const result = schema.safeParse({ email: "user@@example.com" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
    }
  });

  it("rejects missing TLD", () => {
    const result = schema.safeParse({ email: "user@example" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
    }
  });

  it('rejects just "@"', () => {
    const result = schema.safeParse({ email: "@" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
    }
  });

  // --- Boundary: max length ---

  it("accepts email at exactly 254 chars (max boundary)", () => {
    const exactEmail = "a".repeat(245) + "@test.com";
    expect(exactEmail.length).toBe(254);
    const result = schema.safeParse({ email: exactEmail });
    expect(result.success).toBe(true);
  });

  it("rejects email over 254 chars", () => {
    const longEmail = "a".repeat(246) + "@test.com"; // 255 chars
    const result = schema.safeParse({ email: longEmail });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
      expect(issue?.message).toBe("Email too long");
    }
  });

  // --- Missing field ---

  it("rejects missing email field entirely", () => {
    const result = schema.safeParse({});
    expect(result.success).toBe(false);
  });
});
