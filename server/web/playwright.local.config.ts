import { defineConfig } from "@playwright/test";

/**
 * Tier 3: Wide local E2E tests.
 *
 * Expects all services running externally (Docker Compose or manual).
 * NOT included in CI — local dev only.
 *
 * Run: pnpm test:e2e:local
 */
export default defineConfig({
  webServer: {
    command: "pnpm dev",
    port: 5173,
    timeout: 120_000,
    reuseExistingServer: true,
  },
  use: {
    baseURL: "http://localhost:5173",
  },
  testDir: "tests/e2e",
});
