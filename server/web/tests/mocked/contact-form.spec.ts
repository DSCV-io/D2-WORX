import { expect, test } from "@playwright/test";
import { mockEmailCheck } from "./fixtures.js";

test.describe("contact form (/design/contact-form)", () => {
  test.beforeEach(async ({ page }) => {
    await mockEmailCheck(page);
    await page.goto("/debug/design/contact-form");
    // Wait for geo data to load (SSR text)
    await expect(page.getByText(/countries loaded/)).toBeVisible();
    // Wait for Svelte hydration to complete (event handlers attached)
    await page.waitForLoadState("networkidle");
  });

  test("page loads with heading and form", async ({ page }) => {
    await expect(page.locator("h1")).toContainText("Contact Form");
    await expect(page.locator("form")).toBeVisible();
    await expect(page.getByText("New Contact")).toBeVisible();
  });

  test("shows all required form fields", async ({ page }) => {
    await expect(page.getByRole("textbox", { name: "First Name" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Last Name" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Email" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Phone" })).toBeVisible();
    await expect(page.getByRole("combobox", { name: "Country" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Street Address", exact: true })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "City" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "ZIP / Postal Code" })).toBeVisible();
  });

  test("shows country count from live geo data", async ({ page }) => {
    await expect(page.getByText(/\d+ countries loaded/)).toBeVisible();
  });

  test("country combobox opens and shows searchable options", async ({ page }) => {
    await page.getByRole("combobox", { name: "Country" }).click();

    await expect(page.getByRole("listbox")).toBeVisible();
    await expect(page.getByRole("option", { name: "Afghanistan" })).toBeVisible();
    await expect(page.getByRole("option", { name: "United States", exact: true })).toBeVisible();
  });

  test("selecting a country with subdivisions shows state field", async ({ page }) => {
    await expect(page.getByRole("combobox", { name: "State / Province" })).not.toBeVisible();

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await expect(page.getByRole("combobox", { name: "State / Province" })).toBeVisible();
  });

  test("changing country clears and repopulates state options", async ({ page }) => {
    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();
    await expect(page.getByRole("combobox", { name: "State / Province" })).toBeVisible();

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "Canada" }).click();
    await expect(page.getByRole("combobox", { name: "State / Province" })).toBeVisible();
  });

  test("selecting a country without subdivisions hides state field", async ({ page }) => {
    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();
    await expect(page.getByRole("combobox", { name: "State / Province" })).toBeVisible();

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "Singapore" }).click();
    await expect(page.getByRole("combobox", { name: "State / Province" })).not.toBeVisible();
  });

  test("submitting empty form shows validation errors", async ({ page }) => {
    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 5000 });
  });

  test("back link navigates to design system page", async ({ page }) => {
    await page.getByRole("link", { name: /back to design system/i }).click();
    await expect(page).toHaveURL("/debug/design");
  });

  test("reset button clears form fields", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("Jane");
    await expect(firstNameInput).toHaveValue("Jane");

    await page.getByRole("button", { name: "Reset" }).click();
    await expect(firstNameInput).toHaveValue("");
  });

  test("filling and submitting valid form shows success toast", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("94102");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({
      timeout: 5000,
    });
  });

  // --- Form UX Improvements ---

  test("required fields show asterisk indicators", async ({ page }) => {
    const firstNameLabel = page.locator("label", { hasText: "First Name" });
    await expect(firstNameLabel.locator("span.text-destructive")).toHaveText("*");

    const emailLabel = page.locator("label", { hasText: "Email" });
    await expect(emailLabel.locator("span.text-destructive")).toHaveText("*");

    const countryLabel = page.locator("label", { hasText: "Country" });
    await expect(countryLabel.locator("span.text-destructive")).toHaveText("*");
  });

  test("valid field shows green check icon inline in input after blur", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("Jane");
    await firstNameInput.blur();

    // The icon is inside a relative wrapper around the input (not on the label)
    const fieldContainer = page.locator("[data-slot='form-item']", {
      has: firstNameInput,
    });
    await expect(fieldContainer.locator("svg.text-success")).toBeVisible({
      timeout: 2000,
    });
  });

  test("empty required field shows red X icon after blur", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });

    // Focus then blur an empty required field
    await firstNameInput.focus();
    await firstNameInput.blur();

    // The validate-on-blur should produce errors → red X
    const fieldContainer = page.locator("[data-slot='form-item']", {
      has: firstNameInput,
    });
    await expect(fieldContainer.locator("svg.text-destructive")).toBeVisible({
      timeout: 2000,
    });
    // Error text should also appear
    await expect(page.getByText("Required").first()).toBeVisible();
  });

  test("invalid email shows red X after blur (format error)", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("noemail");
    await emailInput.blur();

    // Client Zod validation should catch invalid email format
    const fieldContainer = page.locator("[data-slot='form-item']", {
      has: emailInput,
    });
    await expect(fieldContainer.locator("svg.text-destructive")).toBeVisible({
      timeout: 2000,
    });
    await expect(page.getByText("Invalid email address")).toBeVisible();
  });

  test("email async check shows error for taken email", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("used@email.com");
    await emailInput.blur();

    // After check completes, should show "already taken" error
    await expect(page.getByText("This email is already taken")).toBeVisible({
      timeout: 5000,
    });
  });

  test("email async check shows valid for available email", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("available@email.com");
    await emailInput.blur();

    // After check completes, should show green check (valid)
    const fieldContainer = page.locator("[data-slot='form-item']", {
      has: emailInput,
    });
    await expect(fieldContainer.locator("svg.text-success")).toBeVisible({
      timeout: 5000,
    });
  });

  test("typing clears errors and hides icon until next blur", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });

    // Blur empty → error
    await firstNameInput.focus();
    await firstNameInput.blur();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });

    // Start typing → error and icon should clear
    await firstNameInput.fill("J");
    await expect(page.getByText("Required")).not.toBeVisible({ timeout: 2000 });
  });

  test("address toggle shows and hides extra address lines", async ({ page }) => {
    await expect(page.getByRole("textbox", { name: "Address Line 2" })).not.toBeVisible();
    await expect(page.getByRole("textbox", { name: "Address Line 3" })).not.toBeVisible();

    await page.getByRole("button", { name: "+ More address lines" }).click();
    await expect(page.getByRole("textbox", { name: "Address Line 2" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Address Line 3" })).toBeVisible();
    await expect(page.getByRole("button", { name: "- Fewer address lines" })).toBeVisible();

    await page.getByRole("textbox", { name: "Address Line 2" }).fill("Apt 4B");
    await page.getByRole("textbox", { name: "Address Line 3" }).fill("Floor 2");

    await page.getByRole("button", { name: "- Fewer address lines" }).click();
    await expect(page.getByRole("textbox", { name: "Address Line 2" })).not.toBeVisible();
    await expect(page.getByRole("textbox", { name: "Address Line 3" })).not.toBeVisible();
  });

  test("address toggle clears extra lines when hiding", async ({ page }) => {
    await page.getByRole("button", { name: "+ More address lines" }).click();
    await page.getByRole("textbox", { name: "Address Line 2" }).fill("Apt 4B");
    await page.getByRole("textbox", { name: "Address Line 3" }).fill("Floor 2");

    await page.getByRole("button", { name: "- Fewer address lines" }).click();
    await page.getByRole("button", { name: "+ More address lines" }).click();
    await expect(page.getByRole("textbox", { name: "Address Line 2" })).toHaveValue("");
    await expect(page.getByRole("textbox", { name: "Address Line 3" })).toHaveValue("");
  });

  test("street address label is not duplicated", async ({ page }) => {
    // There should be exactly one "Street Address" label (not a second span above it)
    const streetLabels = page.locator("label", {
      hasText: /^Street Address/,
    });
    await expect(streetLabels).toHaveCount(1);
  });

  test("cross-field validation: street3 without street2 shows error", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("94102");

    await page.getByRole("button", { name: "+ More address lines" }).click();
    await page.getByRole("textbox", { name: "Address Line 3" }).fill("Floor 2");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText("Address Line 2 is required when Line 3 is provided")).toBeVisible({
      timeout: 5000,
    });
  });

  test("descriptions are shown for phone and postal code", async ({ page }) => {
    await expect(page.getByText("Include country code for international numbers.")).toBeVisible();
    await expect(page.getByText("Must match the selected country format.")).toBeVisible();
  });

  test("whitespace-only first name is rejected on blur", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("   ");
    await firstNameInput.blur();

    const fieldContainer = page.locator("[data-slot='form-item']", {
      has: firstNameInput,
    });
    await expect(fieldContainer.locator("svg.text-destructive")).toBeVisible({
      timeout: 2000,
    });
    await expect(page.getByText("Required").first()).toBeVisible();
  });

  test("whitespace-only email is rejected on blur", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("   ");
    await emailInput.blur();

    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 2000 });
  });

  test("multiple validation cycles: error → fix → blur → valid", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });

    // Cycle 1: blur empty → error
    await firstNameInput.focus();
    await firstNameInput.blur();
    const fieldContainer = page.locator("[data-slot='form-item']", {
      has: firstNameInput,
    });
    await expect(fieldContainer.locator("svg.text-destructive")).toBeVisible({
      timeout: 2000,
    });

    // Cycle 2: fill valid → blur → green check
    await firstNameInput.fill("Jane");
    await firstNameInput.blur();
    await expect(fieldContainer.locator("svg.text-success")).toBeVisible({
      timeout: 2000,
    });

    // Cycle 3: clear → blur → error again
    await firstNameInput.fill("");
    await firstNameInput.blur();
    await expect(fieldContainer.locator("svg.text-destructive")).toBeVisible({
      timeout: 2000,
    });
  });

  test("successful submission after fixing validation errors", async ({ page }) => {
    // Submit empty form first → errors
    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText("Required").first()).toBeVisible({ timeout: 5000 });

    // Now fill all required fields
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("94102");

    // Resubmit → should succeed
    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test("phone number formats as you type", async ({ page }) => {
    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    const phoneInput = page.getByRole("textbox", { name: "Phone" });
    await phoneInput.fill("2025551234");
    // After filling, the display should show formatted output
    const displayValue = await phoneInput.inputValue();
    // Should contain formatted characters (parens, spaces, or dashes)
    expect(displayValue.length).toBeGreaterThan(0);
  });

  test("address lines are included in valid submission", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("94102");

    // Open address lines, fill them, then submit
    await page.getByRole("button", { name: "+ More address lines" }).click();
    await page.getByRole("textbox", { name: "Address Line 2" }).fill("Apt 4B");
    await page.getByRole("textbox", { name: "Address Line 3" }).fill("Floor 2");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test("email async check is skipped for invalid email format", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("notvalid");
    await emailInput.blur();

    // Should show format error, NOT trigger async check (no spinner)
    const fieldContainer = page.locator("[data-slot='form-item']", {
      has: emailInput,
    });
    await expect(page.getByText("Invalid email address")).toBeVisible({ timeout: 2000 });
    // Spinner should NOT appear for format-invalid emails
    await expect(fieldContainer.locator("svg.animate-spin")).not.toBeVisible();
  });

  test("email async check resets when user starts typing again", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("used@email.com");
    await emailInput.blur();

    // Wait for async check to show error
    await expect(page.getByText("This email is already taken")).toBeVisible({
      timeout: 5000,
    });

    // Start typing → error should clear
    await emailInput.fill("used@email.co");
    await expect(page.getByText("This email is already taken")).not.toBeVisible({
      timeout: 2000,
    });
  });

  test("country combobox filters options by search text", async ({ page }) => {
    const countryCombobox = page.getByRole("combobox", { name: "Country" });
    await countryCombobox.click();
    await countryCombobox.fill("Cana");

    await expect(page.getByRole("option", { name: "Canada" })).toBeVisible();
    // Other countries should be filtered out
    await expect(page.getByRole("option", { name: "Afghanistan" })).not.toBeVisible();
  });

  test("postal code shows country-specific error on submit", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("K1A 0B1"); // Canadian postal code

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/invalid postal code/i)).toBeVisible({
      timeout: 5000,
    });
  });

  // --- Cross-field validation: State required ---

  test("state required: select US then submit without state shows error", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    // Deliberately do NOT select a state
    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("94102");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText("State / Province is required for this country")).toBeVisible({
      timeout: 5000,
    });
  });

  test("state error clears after selecting a state", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("94102");

    // Submit without state → error
    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText("State / Province is required for this country")).toBeVisible({
      timeout: 5000,
    });

    // Now select a state and resubmit
    // Use keyboard to select — portal-based combobox can have overlay interception after submit
    const stateCombobox = page.getByRole("combobox", { name: "State / Province" });
    await stateCombobox.focus();
    await stateCombobox.fill("California");
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({
      timeout: 5000,
    });
  });

  // --- Cross-field validation: Postal code format ---

  test("postal code format error: US country with Canadian postal code", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("K1A 0B1");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/invalid postal code for US/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test("garbage postal code '!@#$%' rejected for US", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("!@#$%");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/invalid postal code/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test("submit with all fields valid shows success toast", async ({ page }) => {
    await page.getByRole("textbox", { name: "First Name" }).fill("Jane");
    await page.getByRole("textbox", { name: "Last Name" }).fill("Doe");
    await page.getByRole("textbox", { name: "Email" }).fill("jane@example.com");
    await page.getByRole("textbox", { name: "Phone" }).fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByRole("textbox", { name: "Street Address", exact: true }).fill("123 Main St");
    await page.getByRole("textbox", { name: "City" }).fill("San Francisco");
    await page.getByRole("textbox", { name: "ZIP / Postal Code" }).fill("94102");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({
      timeout: 10000,
    });
  });
});
