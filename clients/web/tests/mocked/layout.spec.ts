import { expect, test } from "@playwright/test";

test.describe("layout rendering", () => {
  // --- App layout (requires authenticated session + active org) ---
  // These tests need a real or mocked authenticated session to reach the app shell.
  // With D2_MOCK_INFRA=true the session resolver returns unauthenticated,
  // so /dashboard redirects to /sign-in. Deferring to Tier 2 E2E with auth fixtures.

  test.skip("app layout has sidebar with nav links", async ({ page }) => {
    await page.goto("/dashboard");

    const sidebar = page.locator("[data-slot='sidebar']");
    await expect(sidebar.getByRole("link", { name: "Dashboard" })).toBeVisible();
    await expect(sidebar.getByRole("link", { name: "Settings" })).toBeVisible();
    await expect(sidebar.getByRole("link", { name: "Profile" })).toBeVisible();
  });

  test.skip("sidebar nav links navigate correctly", async ({ page }) => {
    await page.goto("/dashboard");

    const sidebar = page.locator("[data-slot='sidebar']");
    await sidebar.getByRole("link", { name: "Settings" }).click();
    await expect(page).toHaveURL("/settings");
    await expect(page.locator("h1")).toContainText("Settings");

    await sidebar.getByRole("link", { name: "Profile" }).click();
    await expect(page).toHaveURL("/profile");
    await expect(page.locator("h1")).toContainText("Profile");

    await sidebar.getByRole("link", { name: "Dashboard" }).click();
    await expect(page).toHaveURL("/dashboard");
    await expect(page.locator("h1")).toContainText("Dashboard");
  });

  test.skip("app header shows theme toggle", async ({ page }) => {
    await page.goto("/dashboard");

    await expect(page.getByRole("button", { name: /toggle theme/i })).toBeVisible();
  });

  test.skip("app header shows sidebar trigger", async ({ page }) => {
    await page.goto("/dashboard");

    await expect(page.getByRole("button", { name: /toggle sidebar/i }).first()).toBeVisible();
  });

  // --- Auth layout (accessible without authentication) ---

  test("auth layout is centered with no sidebar", async ({ page }) => {
    await page.goto("/sign-in");

    // Should show DCSV WORX branding
    await expect(page.getByText("DCSV WORX")).toBeVisible();
    // Should show the Preferences dropdown trigger (which contains the
    // theme segmented control + locale picker). The standalone "toggle
    // theme" button this test originally expected was consolidated into
    // the Preferences popover.
    await expect(page.getByRole("button", { name: /preferences/i })).toBeVisible();
    // No sidebar
    await expect(page.locator("[data-slot='sidebar']")).not.toBeVisible();
  });

  // --- Onboarding layout (requires authenticated session) ---
  // /welcome uses requireAuth() which redirects unauthenticated users to /sign-in.
  // Deferring to Tier 2 E2E with auth fixtures.

  test.skip("onboarding layout is centered with no sidebar", async ({ page }) => {
    await page.goto("/welcome");

    await expect(page.getByText("DCSV WORX").first()).toBeVisible();
    await expect(page.getByRole("button", { name: /toggle theme/i })).toBeVisible();
    await expect(page.locator("[data-slot='sidebar']")).not.toBeVisible();
  });

  // --- Public layout (no authentication required) ---

  test("public layout has nav and footer", async ({ page }) => {
    await page.goto("/");

    // Nav with branding
    await expect(page.locator("nav").getByText("DCSV WORX")).toBeVisible();
    // Footer with copyright
    await expect(page.locator("footer").getByText(/DCSV/)).toBeVisible();
  });
});
