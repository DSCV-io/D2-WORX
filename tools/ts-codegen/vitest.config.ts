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
      // Exclusion scope — what is excluded and WHY:
      //
      // src/orchestrator.ts — top-level CLI entry point (argument parsing +
      //   per-emitter dispatch); no testable exported logic.
      //
      // src/*-emit.ts (except error-codes-registry-emit.ts) — per-topic CLI
      //   runner scripts. Each exports one `run*Emit(force?)` function that
      //   orchestrates mtime-checks, disk writes, and process.exit wiring.
      //   Their snapshot/parity tests call the underlying lib functions
      //   directly; the `isMain` CLI-entry guard and `run*Emit` disk-I/O
      //   wiring are not exercised in unit tests and cannot be without
      //   process/fs mocking. The exported `emit*` / `aggregate*` functions
      //   (the substantive library logic) ARE unit-tested in their respective
      //   test files.
      //
      // src/error-codes-registry-emit.ts IS included (not in this exclude
      //   list) — its exported `discoverCatalogs`, `aggregateAndCheck`, and
      //   `emitErrorCodeRegistry` functions are fully unit-tested by
      //   error-codes-registry-emit.test.ts. The CLI-runner portion
      //   (`loadEnUsKeys`, `runErrorCodesRegistryEmit`, `isMain` guard) is
      //   annotated with /* v8 ignore */ in the source.
      //
      // src/geo-emitter/** — snapshot-tested via integration; excluded from
      //   unit-coverage threshold per the same CLI-runner rationale.
      exclude: [
        "src/orchestrator.ts",
        // top-level emitter CLI runners — isMain guard + run*Emit disk-I/O wiring
        "src/auth-context-emit.ts",
        "src/auth-scopes-emit.ts",
        "src/d2result-envelope-emit.ts",
        "src/dlq-failure-metadata-emit.ts",
        "src/encryption-domains-emit.ts",
        "src/encryption-frame-emit.ts",
        "src/error-category-emit.ts",
        "src/error-codes-emit.ts",
        "src/field-constraints-emit.ts",
        "src/grpc-trailers-emit.ts",
        "src/headers-emit.ts",
        "src/jwt-claims-emit.ts",
        "src/otel-messaging-tags-emit.ts",
        "src/problem-details-emit.ts",
        "src/request-context-emit.ts",
        "src/tk-keys-emit.ts",
        "src/wire-shape-emit.ts",
        // geo-emitter subdir — snapshot-tested via integration; excluded from
        // unit-coverage threshold per the same CLI-runner rationale.
        "src/geo-emitter/**",
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
