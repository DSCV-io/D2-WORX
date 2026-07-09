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
      // Barrel index + generated wire stubs (any concern folder) + pure-type /
      // pure-options modules excluded; coverage thresholds apply to logic-bearing
      // modules only.
      exclude: [
        "src/index.ts",
        "src/**/*.g.ts",
        "src/issuance/workload-certificate-issuer.ts",
        "src/issuance/workload-leaf-material.ts",
        "src/issuance/leaf-client-options.ts",
        "src/rotation/rotation-subscription.ts",
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
