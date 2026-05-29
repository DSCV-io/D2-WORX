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
      // Codegen-emitted catalog (`*.g.ts`) is covered by tools/ts-codegen
      // emitter snapshots + per-VALUE pin tests in this package; the
      // hand-written `index.ts` is a pure re-export and excluded from the
      // 100/100/100/100 threshold.
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
