/**
 * Tier 2: Full-stack browser E2E — Sign-out flow.
 *
 * Verifies that signing out clears auth state and redirects to sign-in.
 */
import { test, expect } from "@playwright/test";
import { verifyUserEmail } from "./helpers.js";

test.describe("sign-out flow (full stack)", () => {
  const TEST_EMAIL = `signout-${Date.now()}@e2e-test.com`;
  const TEST_PASSWORD = "SecurePass12345";

  test.beforeAll(async ({ request }) => {
    // Create a test user via the Auth API
    const baseUrl = process.env.AUTH_BASE_URL;
    if (!baseUrl) throw new Error("AUTH_BASE_URL not set — global-setup may have failed");

    await request.post(`${baseUrl}/api/auth/sign-up/email`, {
      data: {
        name: "Sign Out Test",
        email: TEST_EMAIL,
        password: TEST_PASSWORD,
      },
    });

    // Mark email as verified so sign-in succeeds (BetterAuth requires verification)
    await verifyUserEmail(TEST_EMAIL);
  });

  test("sign out after sign in clears session and redirects to sign-in", async ({ page }) => {
    // Sign in first
    await page.goto("/sign-in");
    await page.waitForLoadState("networkidle");

    await page.getByRole("textbox", { name: "Email", exact: true }).fill(TEST_EMAIL);
    await page.getByRole("textbox", { name: "Password" }).fill(TEST_PASSWORD);
    await page.getByRole("button", { name: "Sign In" }).click();

    // Wait for redirect away from sign-in (new user without org → /welcome onboarding)
    await expect(page).not.toHaveURL(/\/sign-in$/, { timeout: 15_000 });

    // Sign-out lives inside the user-avatar dropdown (consolidated from the
    // previously-directly-visible button). Open the dropdown first, then
    // click the sign-out menu item.
    const avatarTrigger = page.locator('button:has([data-slot="avatar"])').first();
    await avatarTrigger.click();
    await page.getByRole("menuitem", { name: /sign out/i }).click();

    // Should end up on sign-in page after sign-out clears session
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 15_000 });
  });
});
