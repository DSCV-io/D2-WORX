import { expect, test } from "@playwright/test";

test.describe("verify-email page (/verify-email)", () => {
  test("shows default verification message", async ({ page }) => {
    await page.goto("/verify-email");
    await page.waitForLoadState("networkidle");

    await expect(page).toHaveTitle("Verify Email — DCSV WORX");
    await expect(page.getByText("Check Your Email")).toBeVisible();
    await expect(page.getByText("We've sent a verification link to your email.")).toBeVisible();
  });

  test("shows resent message when resent=true", async ({ page }) => {
    await page.goto("/verify-email?resent=true");
    await page.waitForLoadState("networkidle");

    await expect(
      page.getByText("Your email hasn't been verified yet. We've resent the verification link."),
    ).toBeVisible();
  });

  test("shows email address when email param provided", async ({ page }) => {
    await page.goto("/verify-email?email=test@example.com");
    await page.waitForLoadState("networkidle");

    await expect(page.getByText("test@example.com")).toBeVisible();
  });

  test("shows both resent message and email", async ({ page }) => {
    await page.goto("/verify-email?email=test@example.com&resent=true");
    await page.waitForLoadState("networkidle");

    await expect(
      page.getByText("Your email hasn't been verified yet. We've resent the verification link."),
    ).toBeVisible();
    await expect(page.getByText("test@example.com")).toBeVisible();
  });

  test("has back to sign-in link", async ({ page }) => {
    await page.goto("/verify-email");
    await page.waitForLoadState("networkidle");

    const backLink = page.getByRole("link", { name: "Back to Sign In" });
    await expect(backLink).toBeVisible();
    await expect(backLink).toHaveAttribute("href", "/sign-in");
  });

  test("does not show email when param is absent", async ({ page }) => {
    await page.goto("/verify-email");
    await page.waitForLoadState("networkidle");

    // The email display element should not be present when no email param
    await expect(page.getByText("@")).not.toBeVisible();
  });

  test("does not show resent message when resent param is missing", async ({ page }) => {
    await page.goto("/verify-email?email=test@example.com");
    await page.waitForLoadState("networkidle");

    await expect(
      page.getByText("Your email hasn't been verified yet. We've resent the verification link."),
    ).not.toBeVisible();
    await expect(page.getByText("We've sent a verification link to your email.")).toBeVisible();
  });
});
