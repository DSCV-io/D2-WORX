import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    projects: [
      "backends/node/shared/tests/vitest.config.ts",
      "backends/node/services/*/vitest.config.ts",
      "backends/node/services/*/tests/vitest.config.ts",
      "backends/node/services/*/*/vitest.config.ts",
    ],
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "html"],
      include: ["backends/node/**/src/**/*.ts"],
      exclude: ["**/*.test.ts", "**/*.spec.ts", "**/testing/**", "**/tests/**"],
    },
  },
});
