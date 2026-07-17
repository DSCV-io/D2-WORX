// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { expect, test } from "@playwright/test";

test.describe("route navigation", () => {
  test("/ renders the public landing page", async ({ page }) => {
    await page.goto("/");

    await expect(page.locator("h1")).toContainText("DCSV WORX");
    await expect(page.getByRole("link", { name: /sign in/i })).toBeVisible();
  });

  test("/dashboard redirects unauthenticated users to /sign-in", async ({ page }) => {
    await page.goto("/dashboard");

    // requireOrg() throws a 303 redirect to /sign-in?returnTo=...
    await expect(page).toHaveURL(/\/sign-in\?returnTo=/);
    await expect(page.locator("[data-slot='card-title']").getByText("Sign In")).toBeVisible();
  });

  test("/settings redirects unauthenticated users to /sign-in", async ({ page }) => {
    await page.goto("/settings");

    await expect(page).toHaveURL(/\/sign-in\?returnTo=/);
    await expect(page.locator("[data-slot='card-title']").getByText("Sign In")).toBeVisible();
  });

  test("/account/profile redirects unauthenticated users to /sign-in", async ({ page }) => {
    await page.goto("/account/profile");

    await expect(page).toHaveURL(/\/sign-in\?returnTo=/);
    await expect(page.locator("[data-slot='card-title']").getByText("Sign In")).toBeVisible();
  });

  test("/sign-in renders centered card layout without sidebar", async ({ page }) => {
    await page.goto("/sign-in");

    await expect(page.locator("[data-slot='card-title']").getByText("Sign In")).toBeVisible();
    // No sidebar wrapper in auth layout
    await expect(page.locator("[data-slot='sidebar-wrapper']")).not.toBeVisible();
  });

  test("/welcome redirects unauthenticated users to /sign-in", async ({ page }) => {
    await page.goto("/welcome");

    // requireAuth() throws a 303 redirect to /sign-in?returnTo=...
    await expect(page).toHaveURL(/\/sign-in\?returnTo=/);
    await expect(page.locator("[data-slot='card-title']").getByText("Sign In")).toBeVisible();
  });

  test("unknown routes still show 404", async ({ page }) => {
    const response = await page.goto("/nonexistent-route-xyz");
    expect(response?.status()).toBe(404);

    await expect(page.locator("h1")).toContainText("404");
  });
});
