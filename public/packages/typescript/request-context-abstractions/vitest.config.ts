// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.test.ts"],
    coverage: {
      provider: "v8",
      // Codegen-emitted; coverage from tools/ts-codegen emitter tests.
      include: ["src/**/*.ts"],
      exclude: ["src/**/*.g.ts", "src/index.ts"],
    },
  },
});
