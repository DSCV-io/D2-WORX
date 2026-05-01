import { expect, test } from "@playwright/test";

test("404 page shows for unknown routes", async ({ page }) => {
  const response = await page.goto("/this-route-does-not-exist-12345");
  expect(response?.status()).toBe(404);

  const heading = page.locator("h1");
  await expect(heading).toBeVisible();
  await expect(heading).toContainText("404");

  const homeLink = page.getByRole("link", { name: /go home/i });
  await expect(homeLink).toBeVisible();
});

test("404 page has a working home link", async ({ page }) => {
  await page.goto("/this-route-does-not-exist-12345");

  const homeLink = page.getByRole("link", { name: /go home/i });
  await homeLink.click();

  await expect(page).toHaveURL("/");
});
