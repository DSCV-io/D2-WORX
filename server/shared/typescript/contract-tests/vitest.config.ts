// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vitest/config";

export default defineConfig({
  resolve: {
    alias: {
      // `@d2/geo-abstractions` is wired into the contract-tests workspace
      // package.json + tsconfig references, but the pnpm-managed
      // node_modules symlink can lag the package.json edit until the next
      // `pnpm install` rotation. This alias gives Vitest a deterministic
      // path to the package's source-of-truth dist/index.js so the parity
      // tests run independently of the symlink state. The alias is a no-op
      // when the symlink IS present — Vitest prefers the explicit alias.
      "@d2/geo-abstractions": fileURLToPath(
        new URL("../geo/abstractions/dist/index.js", import.meta.url),
      ),
      // Records-meta sub-export of @d2/geo-default — same rationale as
      // the @d2/geo-abstractions alias above (package.json sub-export
      // resolution requires the pnpm symlink layer to be current; this
      // alias gives Vitest a deterministic path).
      "@d2/geo-default/_records-meta.g": fileURLToPath(
        new URL(
          "../geo/default/dist/generated/_records-meta.g.js",
          import.meta.url,
        ),
      ),
    },
  },
  test: {
    include: ["tests/**/*.parity.test.ts"],
    isolate: true,
    testTimeout: 30000,
    // This is a TEST package — its own source IS test code that asserts
    // against external fixture data. The 100/100/100/100 per-package
    // coverage threshold convention does NOT apply.
  },
});
