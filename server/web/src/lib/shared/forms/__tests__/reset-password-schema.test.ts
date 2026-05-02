// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import { createResetPasswordSchema } from "../reset-password-schema.js";

describe("createResetPasswordSchema", () => {
  const schema = createResetPasswordSchema();

  const validData = {
    newPassword: "MySecretPass12",
    confirmNewPassword: "MySecretPass12",
  };

  // --- Happy path ---

  it("accepts valid matching passwords", () => {
    expect(schema.safeParse(validData).success).toBe(true);
  });

  it("accepts passwords with special characters when matching", () => {
    const result = schema.safeParse({
      newPassword: "P@$$w0rd!#^&*",
      confirmNewPassword: "P@$$w0rd!#^&*",
    });
    expect(result.success).toBe(true);
  });

  it("accepts password at exactly 12 chars (min boundary)", () => {
    const result = schema.safeParse({
      newPassword: "Abcdefghij12",
      confirmNewPassword: "Abcdefghij12",
    });
    expect(result.success).toBe(true);
  });

  it("accepts password at exactly 128 chars (max boundary)", () => {
    const longPw = "A".repeat(127) + "1";
    const result = schema.safeParse({
      newPassword: longPw,
      confirmNewPassword: longPw,
    });
    expect(result.success).toBe(true);
  });

  it("accepts password mixing unicode and ASCII characters", () => {
    const result = schema.safeParse({
      newPassword: "MyP@ssw0rd!!99",
      confirmNewPassword: "MyP@ssw0rd!!99",
    });
    expect(result.success).toBe(true);
  });

  // --- Required field validation ---

  it("rejects empty newPassword", () => {
    const result = schema.safeParse({
      ...validData,
      newPassword: "",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "newPassword");
      expect(issue).toBeDefined();
    }
  });

  it("rejects empty confirmNewPassword", () => {
    const result = schema.safeParse({
      ...validData,
      confirmNewPassword: "",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "confirmNewPassword");
      expect(issue).toBeDefined();
    }
  });

  it("rejects both fields empty", () => {
    const result = schema.safeParse({
      newPassword: "",
      confirmNewPassword: "",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const paths = result.error.issues.map((i) => i.path[0]);
      expect(paths).toContain("newPassword");
      expect(paths).toContain("confirmNewPassword");
    }
  });

  // --- Password mismatch ---

  it("rejects mismatched passwords (error on confirmNewPassword path)", () => {
    const result = schema.safeParse({
      newPassword: "MySecretPass12",
      confirmNewPassword: "DifferentPass12",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "confirmNewPassword" && i.message === "Passwords do not match.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("confirmNewPassword min(1) still produces mismatch error (not length error)", () => {
    const result = schema.safeParse({
      newPassword: "MySecretPass12",
      confirmNewPassword: "x",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "confirmNewPassword" && i.message === "Passwords do not match.",
      );
      expect(issue).toBeDefined();
    }
  });

  // --- Password too short ---

  it("rejects password too short (< 12 chars)", () => {
    const result = schema.safeParse({
      newPassword: "short",
      confirmNewPassword: "short",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "newPassword");
      expect(issue).toBeDefined();
    }
  });

  it("rejects password at exactly 11 chars (under min)", () => {
    const result = schema.safeParse({
      newPassword: "Abcdefghi12",
      confirmNewPassword: "Abcdefghi12",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "newPassword");
      expect(issue).toBeDefined();
    }
  });

  // --- Password too long ---

  it("rejects password over 128 chars", () => {
    const longPw = "A".repeat(129);
    const result = schema.safeParse({
      newPassword: longPw,
      confirmNewPassword: longPw,
    });
    expect(result.success).toBe(false);
  });

  // --- Numeric-only password ---

  it("rejects numeric-only password", () => {
    const result = schema.safeParse({
      newPassword: "123456789012",
      confirmNewPassword: "123456789012",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "newPassword" && i.message === "Password cannot be only numbers.",
      );
      expect(issue).toBeDefined();
    }
  });

  // --- Date-like passwords ---

  it("rejects date-like password with dashes", () => {
    const result = schema.safeParse({
      newPassword: "2025-01-01-01",
      confirmNewPassword: "2025-01-01-01",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "newPassword" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects date-like password with slashes", () => {
    const result = schema.safeParse({
      newPassword: "2025/01/01/01",
      confirmNewPassword: "2025/01/01/01",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "newPassword" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects date-like password with dots", () => {
    const result = schema.safeParse({
      newPassword: "2025.01.01.01",
      confirmNewPassword: "2025.01.01.01",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "newPassword" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("rejects date-like password with spaces", () => {
    const result = schema.safeParse({
      newPassword: "01 01 2025 0101",
      confirmNewPassword: "01 01 2025 0101",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) =>
          i.path[0] === "newPassword" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(issue).toBeDefined();
    }
  });

  // --- Whitespace-only confirmNewPassword ---

  it("rejects whitespace-only confirmNewPassword", () => {
    const result = schema.safeParse({
      newPassword: "MySecretPass12",
      confirmNewPassword: "   ",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path[0] === "confirmNewPassword");
      expect(issue).toBeDefined();
    }
  });

  // --- Passwords should NOT be trimmed ---

  it("does not trim passwords (leading/trailing spaces are significant)", () => {
    const result = schema.safeParse({
      newPassword: " MySecretPass12 ",
      confirmNewPassword: "MySecretPass12",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(
        (i) => i.path[0] === "confirmNewPassword" && i.message === "Passwords do not match.",
      );
      expect(issue).toBeDefined();
    }
  });

  it("accepts passwords with leading/trailing spaces when both match", () => {
    const result = schema.safeParse({
      newPassword: " MySecretPass12 ",
      confirmNewPassword: " MySecretPass12 ",
    });
    expect(result.success).toBe(true);
  });
});
