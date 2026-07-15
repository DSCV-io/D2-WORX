// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vitest/config";

export default defineConfig({
  resolve: {
    alias: {
      // `@d2/encryption-abstractions` — deterministic path to the
      // package's dist/index.js, consistent with the geo/validation
      // aliases below: a Vite `resolve.alias` ALWAYS wins over the
      // pnpm-managed node_modules symlink, so the encryption parity
      // suites load exactly the workspace copy that was just built.
      //
      // NOTE: same caveat as the other aliases — the encryption parity
      // suites REQUIRE a fresh `pnpm --filter @d2/encryption-abstractions
      // build` first; a stale dist/ would be served silently and could
      // mask a regression.
      "@d2/encryption-abstractions": fileURLToPath(
        new URL("../encryption-abstractions/dist/index.js", import.meta.url),
      ),
      // `@d2/encryption` runtime crypto twin — same deterministic-dist
      // rationale as the abstractions alias. The crypto-KAT parity suites
      // REQUIRE a fresh `pnpm --filter @d2/encryption build` first.
      "@d2/encryption": fileURLToPath(
        new URL("../encryption/dist/index.js", import.meta.url),
      ),
      // `@d2/geo-abstractions` is wired into the contract-tests workspace
      // package.json + tsconfig references, but the pnpm-managed
      // node_modules symlink can lag the package.json edit until the next
      // `pnpm install` rotation. This alias gives Vitest a deterministic
      // path to the package's source-of-truth dist/index.js so the parity
      // tests run independently of the symlink state.
      //
      // NOTE: a Vite `resolve.alias` ALWAYS wins over node_modules — even
      // when the workspace symlink is present, this path is what Vitest
      // loads. So the aliased package MUST be freshly built before running
      // the parity suite (`pnpm --filter @d2/geo-abstractions build`); a
      // stale dist/ would be served silently and could mask a regression.
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
      // `@d2/validation` is wired into the contract-tests workspace
      // package.json + tsconfig references, but the pnpm-managed
      // node_modules symlink can lag the package.json edit until the next
      // `pnpm install` rotation. This alias gives Vitest a deterministic
      // path to the package's dist/index.js so the validation parity tests
      // run independently of symlink state — consistent with the geo
      // aliases above.
      //
      // NOTE: same caveat as the geo aliases — a Vite `resolve.alias`
      // ALWAYS wins over the node_modules symlink, so the validation parity
      // suite REQUIRES a fresh `pnpm --filter @d2/validation build` first; a
      // stale dist/ would be served silently and could mask a regression.
      "@d2/validation": fileURLToPath(
        new URL("../validation/default/dist/index.js", import.meta.url),
      ),
      // Caching twin packages — same deterministic-dist rationale as geo /
      // encryption aliases. Caching-twin parity REQUIRES a fresh build of
      // all four packages before the suite runs.
      "@d2/caching-abstractions": fileURLToPath(
        new URL("../caching/abstractions/dist/index.js", import.meta.url),
      ),
      "@d2/caching-local-default": fileURLToPath(
        new URL("../caching/local-default/dist/index.js", import.meta.url),
      ),
      "@d2/caching-distributed-redis": fileURLToPath(
        new URL("../caching/distributed-redis/dist/index.js", import.meta.url),
      ),
      "@d2/caching-tiered": fileURLToPath(
        new URL("../caching/tiered/dist/index.js", import.meta.url),
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
