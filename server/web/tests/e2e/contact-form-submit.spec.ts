/**
 * Tier 3: Wide local E2E — Contact form submission with real Geo validation.
 *
 * Requires all services running (Geo, Redis, etc.).
 * Run with: pnpm test:e2e:local
 */
import { expect, test } from "@playwright/test";

test.describe("contact form submission (real backends)", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/debug/design/contact-form");
    await expect(page.getByText(/countries loaded/)).toBeVisible();
    await page.waitForLoadState("networkidle");
  });

  test("valid submission with real Geo postal code validation", async ({ page }) => {
    await page.getByLabel("First Name").fill("Jane");
    await page.getByLabel("Last Name").fill("Doe");
    await page.getByLabel("Email").fill("jane@example.com");
    await page.getByLabel("Phone").fill("2025551234");

    await page.getByRole("combobox", { name: "Country" }).click();
    await page.getByRole("option", { name: "United States", exact: true }).click();

    await page.getByRole("combobox", { name: "State / Province" }).click();
    await page.getByRole("option", { name: "California" }).click();

    await page.getByLabel("Street Address", { exact: true }).fill("123 Main St");
    await page.getByLabel("City").fill("San Francisco");
    await page.getByLabel("ZIP / Postal Code").fill("94102");

    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText(/validated successfully/i)).toBeVisible({
      timeout: 5000,
    });
  });
});
