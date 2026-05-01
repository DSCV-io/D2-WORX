import { expect, test } from "@playwright/test";

test("home page renders public landing with h1", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator("h1")).toBeVisible();
  await expect(page.locator("h1")).toContainText("DCSV WORX");
});
