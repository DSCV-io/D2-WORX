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
      include: ["src/**/*.ts"],
      // Exclusion scope — what is excluded and WHY:
      //
      // src/index.ts — barrel re-export; no testable logic of its own.
      //
      // src/git.ts — thin IO seam over `git log`. In-process: git.test.ts
      //   (synthetic two-repo cwd honor + empty range + hard-fail). CLI valve
      //   passes repoRoot. Live CI still exercises real-repo valve resolution.
      //   Excluded from the unit-coverage threshold (spawnSync branch surface).
      //
      // src/git-show.ts — thin git IO: fileAtRef (`git show`) +
      //   listTrackedPathsAtRef (`git ls-tree`). Happy path: synthetic e2e
      //   (run-spec-gate.test.ts). Hard-fail throws: git-show.test.ts
      //   (non-git cwd). Still excluded from the unit-coverage threshold
      //   (spawnSync branch surface).
      //
      // src/cli.ts — CLI entry point; orchestration layer over tested sub-units.
      //   Process.exit + process.argv make unit-testing impractical; argv
      //   plumbing is covered by tests/cli-flags.test.ts (subprocess).
      //   The arms themselves are fully covered. Excluded from the coverage
      //   threshold.
      //
      // src/run-spec-gate.ts — orchestration IO seam (git + FS). Integration-
      //   tested by tests/run-spec-gate.test.ts (synthetic git history, all
      //   three JSON arms including whole-file deletion). Pure discovery is
      //   unit-tested under coverage thresholds in discovery.ts. The
      //   spec/i18n/openapi diff engines it calls are individually covered.
      //
      // src/proto-arm.ts — wraps buf binary + filesystem; integration-tested
      //   via proto-arm-integration.test.ts (real buf run). Excluded from the
      //   unit-coverage threshold.
      //
      // discovery.ts is intentionally NOT excluded — pure unit under the
      // package 100/100/100/100 thresholds.
      exclude: [
        "src/index.ts",
        "src/git.ts",
        "src/git-show.ts",
        "src/cli.ts",
        "src/run-spec-gate.ts",
        "src/proto-arm.ts",
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
