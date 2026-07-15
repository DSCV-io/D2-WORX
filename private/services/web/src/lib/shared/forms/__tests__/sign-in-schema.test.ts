// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import { createSignInSchema } from "../sign-in-schema.js";

describe("createSignInSchema", () => {
  const schema = createSignInSchema();

  // --- Happy path ---

  it("accepts valid email and non-empty password", () => {
    const result = schema.safeParse({
      email: "user@example.com",
      password: "anything",
    });
    expect(result.success).toBe(true);
  });

  it("accepts any non-empty password (no complexity rules on sign-in)", () => {
    const result = schema.safeParse({
      email: "user@example.com",
      password: "x",
    });
    expect(result.success).toBe(true);
  });

  it("trims and lowercases email", () => {
    const result = schema.safeParse({
      email: "  User@Example.COM  ",
      password: "anything",
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.email).toBe("user@example.com");
    }
  });

  // --- Email validation ---

  it("rejects empty email", () => {
    const result = schema.safeParse({ email: "", password: "anything" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
    }
  });

  it("rejects invalid email format", () => {
    const result = schema.safeParse({ email: "not-an-email", password: "anything" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "email");
      expect(issue).toBeDefined();
    }
  });

  it("rejects whitespace-only email", () => {
    const result = schema.safeParse({ email: "   ", password: "anything" });
    expect(result.success).toBe(false);
  });

  // --- Password validation ---

  it("rejects empty password", () => {
    const result = schema.safeParse({ email: "user@example.com", password: "" });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "password");
      expect(issue).toBeDefined();
      expect(issue?.message).toBe("Required");
    }
  });

  // --- Missing fields ---

  it("rejects missing email field entirely", () => {
    const result = schema.safeParse({ password: "anything" });
    expect(result.success).toBe(false);
  });

  it("rejects missing password field entirely", () => {
    const result = schema.safeParse({ email: "user@example.com" });
    expect(result.success).toBe(false);
  });

  // --- Boundary: no max length on sign-in password ---

  it("accepts very long password (no max on sign-in)", () => {
    const result = schema.safeParse({
      email: "user@example.com",
      password: "A".repeat(500),
    });
    expect(result.success).toBe(true);
  });

  it("accepts password with special characters", () => {
    const result = schema.safeParse({
      email: "user@example.com",
      password: "P@$$w0rd!#^&*(){}[]|\\:;<>,.?/~`",
    });
    expect(result.success).toBe(true);
  });

  // --- Boundary: email max length ---

  it("accepts email at exactly 254 chars (max boundary)", () => {
    const exactEmail = "a".repeat(245) + "@test.com";
    expect(exactEmail.length).toBe(254);
    const result = schema.safeParse({
      email: exactEmail,
      password: "anything",
    });
    expect(result.success).toBe(true);
  });

  it("rejects email over 254 chars", () => {
    const longEmail = "a".repeat(246) + "@test.com"; // 255 chars
    const result = schema.safeParse({
      email: longEmail,
      password: "anything",
    });
    expect(result.success).toBe(false);
  });

  // --- Whitespace-only password ---

  it("accepts whitespace-only password (no rules on sign-in)", () => {
    const result = schema.safeParse({
      email: "user@example.com",
      password: "   ",
    });
    // Password is min(1) without trim, so whitespace counts as non-empty
    expect(result.success).toBe(true);
  });
});
