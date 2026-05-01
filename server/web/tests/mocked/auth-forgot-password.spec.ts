import { expect, test } from "@playwright/test";

test.describe("forgot-password page (/forgot-password)", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/forgot-password");
    await page.waitForLoadState("networkidle");
  });

  // --- Rendering ---

  test("renders heading and description", async ({ page }) => {
    await expect(page.locator("[data-slot='card-title']")).toContainText("Forgot Password");
    await expect(page.getByText("Enter your email and we'll send you a reset link.")).toBeVisible();
  });

  test("has email input", async ({ page }) => {
    await expect(page.getByRole("textbox", { name: "Email" })).toBeVisible();
  });

  test("has submit button", async ({ page }) => {
    await expect(page.getByRole("button", { name: "Send Reset Link" })).toBeVisible();
  });

  test("has back to sign-in link", async ({ page }) => {
    const backLink = page.getByRole("link", { name: "Back to sign in." });
    await expect(backLink).toBeVisible();
    await expect(backLink).toHaveAttribute("href", "/sign-in");
  });

  // --- SEO ---

  test("has noindex meta tag", async ({ page }) => {
    const robots = page.locator('meta[name="robots"]');
    await expect(robots).toHaveAttribute("content", "noindex");
  });

  test("has OG tags and meta description", async ({ page }) => {
    await expect(page.locator('meta[name="description"]').last()).toHaveAttribute(
      "content",
      "Enter your email and we'll send you a reset link.",
    );
    await expect(page.locator('meta[property="og:title"]')).toHaveAttribute(
      "content",
      /Forgot Password/,
    );
    await expect(page.locator('meta[property="og:description"]')).toHaveAttribute(
      "content",
      "Enter your email and we'll send you a reset link.",
    );
    await expect(page.locator('meta[property="og:type"]')).toHaveAttribute("content", "website");
  });

  // --- Client-side validation ---

  test("blur empty email shows error", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.focus();
    await emailInput.blur();
    await expect(page.getByText("Required")).toBeVisible({ timeout: 2000 });
  });

  test("blur whitespace-only email shows error", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("   ");
    await emailInput.blur();
    await expect(page.getByText("Required")).toBeVisible({ timeout: 2000 });
  });

  test("blur invalid email format shows error", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("notanemail");
    await emailInput.blur();
    await expect(page.getByText("Invalid email address")).toBeVisible({ timeout: 2000 });
  });

  test("blur email exceeding max length shows error", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    // 255 chars total — exceeds 254-char max
    await emailInput.fill("a".repeat(243) + "@example.com");
    await emailInput.blur();
    await expect(page.getByText("Email too long")).toBeVisible({ timeout: 2000 });
  });

  test("blur valid email clears error", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.focus();
    await emailInput.blur();
    await expect(page.getByText("Required")).toBeVisible({ timeout: 2000 });

    await emailInput.fill("valid@example.com");
    await emailInput.blur();
    await expect(page.getByText("Required")).not.toBeVisible({ timeout: 2000 });
  });

  test("fixing invalid email clears error on re-blur", async ({ page }) => {
    const emailInput = page.getByRole("textbox", { name: "Email" });
    await emailInput.fill("not-valid");
    await emailInput.blur();
    await expect(page.getByText("Invalid email address")).toBeVisible({ timeout: 2000 });

    await emailInput.fill("valid@example.com");
    await emailInput.blur();
    await expect(page.getByText("Invalid email address")).not.toBeVisible({ timeout: 2000 });
  });

  // --- Navigation ---

  test("back to sign-in link navigates to /sign-in", async ({ page }) => {
    const backLink = page.getByRole("link", { name: "Back to sign in." });
    await backLink.click();
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL(/\/sign-in$/);
  });
});
