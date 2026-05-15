// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.test.ts"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // The barrel `index.ts` is a pure re-export; `guards/guard-types.ts`
      // is a pure type-declaration module (interface + type aliases) with
      // no runtime exports — coverage thresholds apply to logic-bearing
      // modules only.
      exclude: ["src/**/*.g.ts", "src/index.ts", "src/guards/guard-types.ts"],
      thresholds: {
        lines: 100,
        branches: 100,
        functions: 100,
        statements: 100,
      },
    },
  },
});
