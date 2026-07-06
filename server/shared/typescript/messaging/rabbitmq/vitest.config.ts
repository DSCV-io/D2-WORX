// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // Unit suite only. The Testcontainer-backed integration suite lives under
    // tests/integration/ and runs via vitest.integration.config.ts (no
    // coverage gate — real-broker paths are proven, not line-counted).
    include: ["tests/**/*.test.ts"],
    exclude: ["tests/integration/**", "node_modules", "dist"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      exclude: ["src/**/*.g.ts", "src/index.ts"],
      thresholds: {
        lines: 100,
        branches: 100,
        functions: 100,
        statements: 100,
      },
    },
  },
});
