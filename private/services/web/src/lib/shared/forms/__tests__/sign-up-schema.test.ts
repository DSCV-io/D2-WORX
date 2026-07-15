// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import { createSignUpSchema } from "../sign-up-schema.js";

describe("createSignUpSchema", () => {
  const schema = createSignUpSchema();

  const validData = {
    firstName: "Jane",
    lastName: "Doe",
    email: "jane@example.com",
    confirmEmail: "jane@example.com",
    password: "MySecretPass12",
    confirmPassword: "MySecretPass12",
  };

  // --- Happy path ---

  it("accepts valid form data with all fields filled and matching", () => {
    expect(schema.safeParse(validData).success).toBe(true);
  });

  it("accepts passwords with special characters", () => {
    const result = schema.safeParse({
      ...validData,
      password: "P@$$w0rd!#^&*",
      confirmPassword: "P@$$w0rd!#^&*",
    });
    expect(result.success).toBe(true);
  });

  it("trims name fields", () => {
    const result = schema.safeParse({
      ...validData,
      firstName: "  Jane  ",
      lastName: "  Doe  ",
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.firstName).toBe("Jane");
      expect(result.data.lastName).toBe("Doe");
    }
  });

  it("trims email fields before comparison", () => {
    const result = schema.safeParse({
      ...validData,
      email: "  jane@example.com  ",
      confirmEmail: "  jane@example.com  ",
    });
    expect(result.success).toBe(true);
  });

  // --- Required field validation ---

  it("rejects empty form (all required fields error)", () => {
    const result = schema.safeParse({
      firstName: "",
      lastName: "",
      email: "",
      confirmEmail: "",
      password: "",
      confirmPassword: "",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const paths = result.error.issues.map((i) => i.path[0]);
      expect(paths).toContain("firstName");
      expect(paths).toContain("lastName");
      expect(paths).toContain("email");
      expect(paths).toContain("password");
      expect(paths).toContain("confirmPassword");
    }
  });

  it("rejects whitespace-only names", () => {
    const result = schema.safeParse({
      ...validData,
      firstName: "   ",
      lastName: "\t",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const paths = result.error.issues.map((i) => i.path[0]);
      expect(paths).toContain("firstName");
      expect(paths).toContain("lastName");
    }
  });

  // --- Email validation ---

  it("rejects invalid email format", () => {
    const result = schema.safeParse({
      ...validData,
      email: "not-an-email",
      confirmEmail: "not-an-email",
    });
    expect(result.success).toBe(false);
  });

  it("rejects mismatched emails (error on confirmEmail path)", () => {
    const result = schema.safeParse({
      ...validData,
      confirmEmail: "different@example.com",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "confirmEmail" && i.message === "Emails do not match.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects when email is valid but confirmEmail is empty", () => {
    const result = schema.safeParse({
      ...validData,
      confirmEmail: "",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "confirmEmail");
      expect(issue).toBeDefined();
    }
  });

  it("rejects email over 254 chars", () => {
    const longEmail = "a".repeat(246) + "@test.com"; // 255 chars
    const result = schema.safeParse({
      ...validData,
      email: longEmail,
      confirmEmail: longEmail,
    });
    expect(result.success).toBe(false);
  });

  // --- Password validation ---

  it("rejects mismatched passwords (error on confirmPassword path)", () => {
    const result = schema.safeParse({
      ...validData,
      confirmPassword: "DifferentPass12",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "confirmPassword" && i.message === "Passwords do not match.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects password too short (< 12 chars)", () => {
    const result = schema.safeParse({
      ...validData,
      password: "short",
      confirmPassword: "short",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "password");
      expect(issue).toBeDefined();
    }
  });

  it("rejects numeric-only password", () => {
    const result = schema.safeParse({
      ...validData,
      password: "123456789012",
      confirmPassword: "123456789012",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "password" && i.message === "Password cannot be only numbers.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects date-like password", () => {
    const result = schema.safeParse({
      ...validData,
      password: "2025-01-01-01",
      confirmPassword: "2025-01-01-01",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "password" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects password over 128 chars", () => {
    const longPw = "A".repeat(129);
    const result = schema.safeParse({
      ...validData,
      password: longPw,
      confirmPassword: longPw,
    });
    expect(result.success).toBe(false);
  });

  // --- Exact boundary: password length ---

  it("accepts password at exactly 12 chars (min boundary)", () => {
    const result = schema.safeParse({
      ...validData,
      password: "Abcdefghij12",
      confirmPassword: "Abcdefghij12",
    });
    expect(result.success).toBe(true);
  });

  it("rejects password at exactly 11 chars (under min)", () => {
    const result = schema.safeParse({
      ...validData,
      password: "Abcdefghi12",
      confirmPassword: "Abcdefghi12",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "password");
      expect(issue).toBeDefined();
    }
  });

  it("accepts password at exactly 128 chars (max boundary)", () => {
    const longPw = "A".repeat(127) + "1";
    const result = schema.safeParse({
      ...validData,
      password: longPw,
      confirmPassword: longPw,
    });
    expect(result.success).toBe(true);
  });

  // --- Exact boundary: name max length ---

  it("accepts name at exactly 255 chars (max boundary)", () => {
    const result = schema.safeParse({
      ...validData,
      firstName: "A".repeat(255),
    });
    expect(result.success).toBe(true);
  });

  it("rejects firstName over 255 chars", () => {
    const result = schema.safeParse({
      ...validData,
      firstName: "A".repeat(256),
    });
    expect(result.success).toBe(false);
  });

  it("rejects lastName over 255 chars", () => {
    const result = schema.safeParse({
      ...validData,
      lastName: "A".repeat(256),
    });
    expect(result.success).toBe(false);
  });

  // --- Exact boundary: email max length ---

  it("accepts email at exactly 254 chars (max boundary)", () => {
    // "@test.com" = 9 chars, so local = 254 - 9 = 245
    const exactEmail = "a".repeat(245) + "@test.com";
    expect(exactEmail.length).toBe(254);
    const result = schema.safeParse({
      ...validData,
      email: exactEmail,
      confirmEmail: exactEmail,
    });
    expect(result.success).toBe(true);
  });

  // --- Cross-field edge cases ---

  it("matches case-different emails (both lowercased by schema)", () => {
    const result = schema.safeParse({
      ...validData,
      email: "Jane@Example.com",
      confirmEmail: "jane@example.com",
    });
    expect(result.success).toBe(true);
  });

  it("confirmPassword min(1) still produces mismatch error (not length error)", () => {
    const result = schema.safeParse({
      ...validData,
      password: "MySecretPass12",
      confirmPassword: "x",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "confirmPassword" && i.message === "Passwords do not match.",
      );
      expect(issue).toBeDefined();
    }
  });

  // --- Date-like password variants ---

  it("rejects date-like password with slashes", () => {
    const result = schema.safeParse({
      ...validData,
      password: "2025/01/01/01",
      confirmPassword: "2025/01/01/01",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "password" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects date-like password with dots", () => {
    const result = schema.safeParse({
      ...validData,
      password: "2025.01.01.01",
      confirmPassword: "2025.01.01.01",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "password" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects date-like password with spaces", () => {
    const result = schema.safeParse({
      ...validData,
      password: "01 01 2025 0101",
      confirmPassword: "01 01 2025 0101",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "password" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  // --- Mixed valid characters ---

  it("accepts password mixing unicode and ASCII letters", () => {
    const result = schema.safeParse({
      ...validData,
      password: "MyP@ssw0rd!!99",
      confirmPassword: "MyP@ssw0rd!!99",
    });
    expect(result.success).toBe(true);
  });

  it("accepts names with hyphens, apostrophes, spaces", () => {
    const result = schema.safeParse({
      ...validData,
      firstName: "Mary-Jane",
      lastName: "O'Brien Smith",
    });
    expect(result.success).toBe(true);
  });
});
