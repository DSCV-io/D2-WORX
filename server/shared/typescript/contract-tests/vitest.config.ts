// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.parity.test.ts"],
    isolate: true,
    testTimeout: 30000,
    // This is a TEST package — its own source IS test code that asserts
    // against external fixture data. The 100/100/100/100 per-package
    // coverage threshold convention does NOT apply.
  },
});
