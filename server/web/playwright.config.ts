import { defineConfig } from "@playwright/test";

export default defineConfig({
  webServer: {
    command: "pnpm dev --port 5174",
    port: 5174,
    timeout: 30_000,
    reuseExistingServer: false,
    env: {
      D2_MOCK_INFRA: "true",
    },
  },
  use: {
    baseURL: "http://localhost:5174",
  },
  testDir: "tests/mocked",
  retries: 1,
});
