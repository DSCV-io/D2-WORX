// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { expect, test } from "@playwright/test";
import { mockEmailCheck } from "./fixtures.js";

test.describe("signup form (/design/signup-form)", () => {
  test.beforeEach(async ({ page }) => {
    await mockEmailCheck(page);
    await page.goto("/debug/design/signup-form");
    await page.waitForLoadState("networkidle");
  });

  // --- Rendering ---

  test("page loads with heading and all fields visible", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Signup Form", level: 1 })).toBeVisible();
    await expect(page.locator("[data-slot='card-title']")).toContainText("Create Account");
    await expect(page.locator("form")).toBeVisible();

    await expect(page.getByRole("textbox", { name: "First Name" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Last Name" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Email", exact: true })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Confirm Email" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Password", exact: true })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Confirm Password" })).toBeVisible();
  });

  test("back link navigates to design index", async ({ page }) => {
    await page.getByRole("link", { name: /back to design system/i }).click();
    await expect(page).toHaveURL("/debug/design");
  });

  test("password fields start with type=password (masked)", async ({ page }) => {
    await expect(page.getByRole("textbox", { name: "Password", exact: true })).toHaveAttribute(
      "type",
      "password",
    );
    await expect(page.getByRole("textbox", { name: "Confirm Password" })).toHaveAttribute(
      "type",
      "password",
    );
  });

  // --- Inline validation ---

  test("blur empty required field shows error", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.focus();
    await firstNameInput.blur();

    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });
  });

  test("type valid input then blur clears error", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });

    // Blur empty → error
    await firstNameInput.focus();
    await firstNameInput.blur();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });

    // Fill valid → error clears
    await firstNameInput.fill("Jane");
    await firstNameInput.blur();
    const fieldContainer = page.locator("[data-slot='form-item']", { has: firstNameInput });
    await expect(fieldContainer.locator("svg.text-success")).toBeVisible({ timeout: 2000 });
  });

  test("invalid email format shows error on blur", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email", exact: true });
    await emailInput.fill("noemail");
    await emailInput.blur();

    await expect(page.getByText("Invalid email address")).toBeVisible({ timeout: 2000 });
  });

  test("password fewer than 12 chars shows error on blur", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("short");
    await passwordInput.blur();

    await expect(page.getByText("Password must be at least 12 characters")).toBeVisible({
      timeout: 2000,
    });
  });

  test("numeric-only password shows error on blur", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("123456789012");
    await passwordInput.blur();

    await expect(page.getByText("Password cannot be only numbers.", { exact: true })).toBeVisible({
      timeout: 2000,
    });
  });

  test("date-like password shows error on blur", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("2025-01-01-01");
    await passwordInput.blur();

    await expect(page.getByText("Password cannot be only numbers and date separators")).toBeVisible(
      { timeout: 2000 },
    );
  });

  // --- Cross-field validation ---

  test("mismatched emails show error on confirmEmail after submit", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("different@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("MySecretPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Emails do not match")).toBeVisible({ timeout: 5000 });
  });

  test("fix mismatched emails clears error on resubmit", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("different@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("MySecretPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Emails do not match")).toBeVisible({ timeout: 5000 });

    // Fix and resubmit — blur first to let client validation clear the error
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).blur();
    await expect(page.getByText("Emails do not match")).not.toBeVisible({ timeout: 2000 });
    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({ timeout: 5000 });
  });

  test("mismatched passwords show error on confirmPassword after submit", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("DifferentPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Passwords do not match")).toBeVisible({ timeout: 5000 });
  });

  test("fix mismatched passwords clears error on resubmit", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("DifferentPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Passwords do not match")).toBeVisible({ timeout: 5000 });

    // Fix and resubmit
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("MySecretPass12");
    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({ timeout: 5000 });
  });

  // --- Password show/hide toggle ---

  test("click eye icon changes password type to text (visible)", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await expect(passwordInput).toHaveAttribute("type", "password");

    await page.getByRole("button", { name: "Show password", exact: true }).click();
    await expect(passwordInput).toHaveAttribute("type", "text");
  });

  test("click eye icon again changes type back to password (masked)", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });

    await page.getByRole("button", { name: "Show password", exact: true }).click();
    await expect(passwordInput).toHaveAttribute("type", "text");

    await page.getByRole("button", { name: "Hide password", exact: true }).click();
    await expect(passwordInput).toHaveAttribute("type", "password");
  });

  test("confirm password has independent toggle", async ({ page }) => {
    const confirmInput = page.getByRole("textbox", { name: "Confirm Password" });
    await expect(confirmInput).toHaveAttribute("type", "password");

    await page.getByRole("button", { name: "Show confirm password" }).click();
    await expect(confirmInput).toHaveAttribute("type", "text");

    await page.getByRole("button", { name: "Hide confirm password" }).click();
    await expect(confirmInput).toHaveAttribute("type", "password");
  });

  test("toggle state does not affect other password field", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    const confirmInput = page.getByRole("textbox", { name: "Confirm Password" });

    // Toggle password → text
    await page.getByRole("button", { name: "Show password", exact: true }).click();
    await expect(passwordInput).toHaveAttribute("type", "text");
    // Confirm password should still be masked
    await expect(confirmInput).toHaveAttribute("type", "password");

    // Toggle confirm → text
    await page.getByRole("button", { name: "Show confirm password" }).click();
    await expect(confirmInput).toHaveAttribute("type", "text");
    // Password should still be text (unchanged by confirm toggle)
    await expect(passwordInput).toHaveAttribute("type", "text");
  });

  // --- Async email check ---

  test("valid email shows green check after async check", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email", exact: true });
    await emailInput.fill("available@email.com");
    await emailInput.blur();

    const fieldContainer = page.locator("[data-slot='form-item']", { has: emailInput });
    await expect(fieldContainer.locator("svg.text-success")).toBeVisible({ timeout: 5000 });
  });

  test("taken email shows error after async check", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email", exact: true });
    await emailInput.fill("used@email.com");
    await emailInput.blur();

    await expect(page.getByText("This email is already taken")).toBeVisible({ timeout: 5000 });
  });

  test("invalid email format does not trigger async check", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email", exact: true });
    await emailInput.fill("notvalid");
    await emailInput.blur();

    await expect(page.getByText("Invalid email address")).toBeVisible({ timeout: 2000 });
    const fieldContainer = page.locator("[data-slot='form-item']", { has: emailInput });
    await expect(fieldContainer.locator("svg.animate-spin")).not.toBeVisible();
  });

  // --- Submission ---

  test("empty form submit shows validation errors on all required fields", async ({ page }) => {
    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 5000 });
  });

  test("valid form submit shows success toast", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("MySecretPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({ timeout: 5000 });
  });

  test("error then fix then resubmit cycle", async ({ page }) => {
    // Submit empty → errors
    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 5000 });

    // Fill all fields correctly
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("MySecretPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({ timeout: 5000 });
  });

  // --- Adversarial ---

  test("whitespace-only name fields rejected", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("   ");
    await firstNameInput.blur();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });
  });

  test("129-char password rejected", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("a".repeat(129));
    await passwordInput.blur();
    await expect(page.getByText("Must be 128 characters or fewer")).toBeVisible({
      timeout: 2000,
    });
  });

  test("numeric-only 12-char password rejected (adversarial)", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("123456789012");
    await passwordInput.blur();
    await expect(page.getByText("Password cannot be only numbers.", { exact: true })).toBeVisible({
      timeout: 2000,
    });
  });

  test("date-like pattern password rejected (adversarial)", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("2025-01-01-01");
    await passwordInput.blur();
    await expect(page.getByText("Password cannot be only numbers and date separators")).toBeVisible(
      { timeout: 2000 },
    );
  });
});
