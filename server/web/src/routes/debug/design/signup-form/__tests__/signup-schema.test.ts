import { describe, it, expect } from "vitest";
import { createSignUpSchema as createSignupSchema } from "$lib/shared/forms/sign-up-schema.js";

describe("createSignupSchema", () => {
  const schema = createSignupSchema();

  const validData = {
    firstName: "Jane",
    lastName: "Doe",
    email: "jane@example.com",
    confirmEmail: "jane@example.com",
    password: "MySecretPass12",
    confirmPassword: "MySecretPass12",
  };

  it("accepts valid form data with all fields filled and matching", () => {
    expect(schema.safeParse(validData).success).toBe(true);
  });

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

  it("rejects mismatched emails (error on confirmEmail path)", () => {
    const result = schema.safeParse({
      ...validData,
      confirmEmail: "different@example.com",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const confirmEmailIssue = result.error.issues.find(
        (i) => i.path[0] === "confirmEmail" && i.message === "Emails do not match.",
      );
      expect(confirmEmailIssue).toBeDefined();
    }
  });

  it("rejects mismatched passwords (error on confirmPassword path)", () => {
    const result = schema.safeParse({
      ...validData,
      confirmPassword: "DifferentPass12",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const confirmPasswordIssue = result.error.issues.find(
        (i) => i.path[0] === "confirmPassword" && i.message === "Passwords do not match.",
      );
      expect(confirmPasswordIssue).toBeDefined();
    }
  });

  it("rejects password too short (inherits passwordField rules)", () => {
    const result = schema.safeParse({
      ...validData,
      password: "short",
      confirmPassword: "short",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const passwordIssue = result.error.issues.find((i) => i.path[0] === "password");
      expect(passwordIssue).toBeDefined();
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
      const passwordIssue = result.error.issues.find(
        (i) => i.path[0] === "password" && i.message === "Password cannot be only numbers.",
      );
      expect(passwordIssue).toBeDefined();
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
      const passwordIssue = result.error.issues.find(
        (i) =>
          i.path[0] === "password" &&
          i.message === "Password cannot be only numbers and date separators.",
      );
      expect(passwordIssue).toBeDefined();
    }
  });

  it("accepts passwords with special characters when they match", () => {
    const result = schema.safeParse({
      ...validData,
      password: "P@$$w0rd!#^&*",
      confirmPassword: "P@$$w0rd!#^&*",
    });
    expect(result.success).toBe(true);
  });

  it("rejects when email is valid but confirmEmail is empty", () => {
    const result = schema.safeParse({
      ...validData,
      confirmEmail: "",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const confirmEmailIssue = result.error.issues.find((i) => i.path[0] === "confirmEmail");
      expect(confirmEmailIssue).toBeDefined();
    }
  });

  it("rejects when confirmEmail has invalid email format", () => {
    const result = schema.safeParse({
      ...validData,
      confirmEmail: "not-an-email",
    });
    expect(result.success).toBe(false);
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
});
