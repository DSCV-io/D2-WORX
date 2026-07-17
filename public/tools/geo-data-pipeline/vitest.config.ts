// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.test.ts"],
    globals: false,
    // Catalogs are megabytes; parity tests do real file IO + JSON parsing.
    // Generous timeout keeps runs deterministic on slower disks.
    testTimeout: 30_000,
  },
});
