import { expect, test } from "@playwright/test";

test.describe("Display name input masking", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/sign-up");
    await page.waitForLoadState("networkidle");
  });

  test("should strip HTML tags from firstName field", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("<script>alert(1)</script>");
    await expect(firstNameInput).toHaveValue("scriptalert1script");
  });

  test("should allow valid name characters", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("O'Brien-Smith");
    await expect(firstNameInput).toHaveValue("O'Brien-Smith");
  });

  test("should strip markdown syntax", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("**bold** [link](url)");
    await expect(firstNameInput).toHaveValue("bold linkurl");
  });

  test("should preserve Unicode letters", async ({ page }) => {
    const firstNameInput = page.getByRole("textbox", { name: "First Name" });
    await firstNameInput.fill("José María");
    await expect(firstNameInput).toHaveValue("José María");
  });
});
