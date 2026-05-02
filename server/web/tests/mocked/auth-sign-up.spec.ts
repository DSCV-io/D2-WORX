// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { expect, test } from "@playwright/test";

test.describe("sign-up page (/sign-up)", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/sign-up");
    await page.waitForLoadState("networkidle");
  });

  // --- Rendering ---

  test("page title and heading visible", async ({ page }) => {
    await expect(page).toHaveTitle("Create Account — DCSV WORX");
    await expect(
      page.locator("[data-slot='card-title']").getByText("Create Account"),
    ).toBeVisible();
    await expect(page.getByText("Fill in your details to get started.")).toBeVisible();
  });

  test("all six fields visible", async ({ page }) => {
    await expect(page.getByRole("textbox", { name: "First Name" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Last Name" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Email", exact: true })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Confirm Email" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Password", exact: true })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Confirm Password" })).toBeVisible();
  });

  test("password fields start masked", async ({ page }) => {
    await expect(page.getByRole("textbox", { name: "Password", exact: true })).toHaveAttribute(
      "type",
      "password",
    );
    await expect(page.getByRole("textbox", { name: "Confirm Password" })).toHaveAttribute(
      "type",
      "password",
    );
  });

  test("has sign-in link", async ({ page }) => {
    // Footer link in the form body — disambiguated from the nav-bar's
    // "Sign In" button by the trailing period and exact-name match.
    const signInLink = page.getByRole("link", { name: "Sign in.", exact: true });
    await expect(signInLink).toBeVisible();
    await expect(signInLink).toHaveAttribute("href", "/sign-in");
  });

  // --- Client-side validation ---

  test("blur empty required field shows error", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.focus();
    await firstNameInput.blur();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });
  });

  test("invalid email shows error on blur", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email", exact: true });
    await emailInput.fill("not-valid");
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

  test("mismatched emails show error after submit", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("different@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("MySecretPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Emails do not match")).toBeVisible({ timeout: 5000 });
  });

  test("mismatched passwords show error after submit", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email", exact: true }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Confirm Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Password", exact: true }).fill("MySecretPass12");
    await page.getByRole("textbox", { name: "Confirm Password" }).fill("DifferentPass12");

    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Passwords do not match")).toBeVisible({ timeout: 5000 });
  });

  // --- Password toggle ---

  test("click eye toggles password visibility", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await expect(passwordInput).toHaveAttribute("type", "password");

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

    // Main password should still be masked
    await expect(page.getByRole("textbox", { name: "Password", exact: true })).toHaveAttribute(
      "type",
      "password",
    );
  });

  // --- Adversarial ---

  test("whitespace-only name rejected", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("   ");
    await firstNameInput.blur();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });
  });

  test("numeric-only password rejected", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("123456789012");
    await passwordInput.blur();
    await expect(page.getByText("Password cannot be only numbers.", { exact: true })).toBeVisible({
      timeout: 2000,
    });
  });

  test("date-like password rejected", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("2025-01-01-01");
    await passwordInput.blur();
    await expect(page.getByText("Password cannot be only numbers and date separators")).toBeVisible(
      { timeout: 2000 },
    );
  });

  test("129-char password rejected", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("a".repeat(129));
    await passwordInput.blur();
    await expect(page.getByText("Must be 128 characters or fewer")).toBeVisible({
      timeout: 2000,
    });
  });

  test("empty form submit shows validation errors", async ({ page }) => {
    await page.getByRole("button", { name: "Sign Up" }).click();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 5000 });
  });

  // --- Error clearing ---

  test("fixing invalid email clears error on blur", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email", exact: true });
    await emailInput.fill("not-valid");
    await emailInput.blur();
    await expect(page.getByText("Invalid email address")).toBeVisible({ timeout: 2000 });

    await emailInput.fill("valid@example.com");
    await emailInput.blur();
    await expect(page.getByText("Invalid email address")).not.toBeVisible({ timeout: 2000 });
  });

  test("fixing short password clears error on blur", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("short");
    await passwordInput.blur();
    await expect(page.getByText("Password must be at least 12 characters")).toBeVisible({
      timeout: 2000,
    });

    await passwordInput.fill("ValidPassword12");
    await passwordInput.blur();
    await expect(page.getByText("Password must be at least 12 characters")).not.toBeVisible({
      timeout: 2000,
    });
  });

  test("fixing empty name clears error on blur", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.focus();
    await firstNameInput.blur();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });

    await firstNameInput.fill("Jane");
    await firstNameInput.blur();
    // After filling valid name, the "Required" for firstName should clear
    // (other fields may still show "Required" but the firstName one should be gone)
    const firstNameField = firstNameInput.locator("..");
    await expect(firstNameField.getByText("Required")).not.toBeVisible({ timeout: 2000 });
  });

  // --- Boundary values ---

  test("name at exactly 256 chars rejected on blur", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("A".repeat(256));
    await firstNameInput.blur();
    await expect(page.getByText("Must be 255 characters or fewer")).toBeVisible({ timeout: 2000 });
  });

  test("date-like password with slashes rejected", async ({ page }) => {
    const passwordInput = page.getByRole("textbox", { name: "Password", exact: true });
    await passwordInput.fill("2025/01/01/01");
    await passwordInput.blur();
    await expect(page.getByText("Password cannot be only numbers and date separators")).toBeVisible(
      { timeout: 2000 },
    );
  });
});
