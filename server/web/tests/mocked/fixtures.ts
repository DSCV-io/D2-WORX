/**
 * Shared mock helpers for mocked Playwright tests.
 *
 * These intercept client-side HTTP requests via page.route() so tests
 * can run against the SvelteKit dev server without real backends.
 *
 * Note: Server-side data loading (e.g. geo ref data via gRPC) is handled
 * by D2_MOCK_INFRA=true which provides mock data at the SSR level.
 * These fixtures only mock client-initiated requests (browser → server).
 */
import type { Page } from "@playwright/test";

/**
 * Mock the async email availability check endpoint.
 *
 * The real endpoint at `/debug/design/api/check-email` already returns mock
 * data (hardcoded `used@email.com` = taken), but this intercepts the request
 * to eliminate the simulated 500-1000ms delay and ensure deterministic behavior.
 */
export async function mockEmailCheck(page: Page): Promise<void> {
  await page.route("**/debug/design/api/check-email*", async (route) => {
    const url = new URL(route.request().url());
    const email = url.searchParams.get("email");
    const available = email?.toLowerCase() !== "used@email.com";
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ available }),
    });
  });
}
