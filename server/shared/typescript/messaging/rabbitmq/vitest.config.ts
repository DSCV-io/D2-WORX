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
      // `domain-crypto-map.ts` is types-only (the compile-time publish witness);
      // it is proven by tests/publisher-type-witness.compile.ts under the
      // type-check gate, not by runtime coverage.
      exclude: [
        "src/**/*.g.ts",
        "src/index.ts",
        "src/publishing/domain-crypto-map.ts",
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
