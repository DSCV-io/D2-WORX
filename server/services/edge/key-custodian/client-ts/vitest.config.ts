// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.test.ts"],
    setupFiles: ["tests/setup.ts"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // Barrel index + generated wire stubs + pure-type / pure-options modules
      // excluded; coverage thresholds apply to logic-bearing modules only.
      exclude: [
        "src/index.ts",
        "src/**/*.g.ts",
        "src/generated/**",
        "src/workload-certificate-issuer.ts",
        "src/workload-leaf-material.ts",
        "src/leaf-client-options.ts",
      ],
      thresholds: {
        lines: 100,
        branches: 100,
        functions: 100,
        statements: 100,
      },
    },
  },
});
