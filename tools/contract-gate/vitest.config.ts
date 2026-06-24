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
      // src/index.ts — barrel re-export; no testable logic of its own.
      //
      // src/git.ts — thin IO seam over `git log`; tested via integration
      //   (a real `git log` call) rather than unit mocks so the seam
      //   signature is verified against the real binary. Excluded from the
      //   unit-coverage threshold; integration coverage documented in
      //   VALIDATION.md.
      //
      // src/git-show.ts — thin IO seam over `git show`; same rationale as
      //   git.ts. Exercised via run-spec-gate integration tests.
      //
      // src/cli.ts — CLI entry point; orchestration layer over tested sub-units.
      //   Process.exit + process.argv make unit-testing impractical; the arms
      //   themselves are fully covered. Excluded from the coverage threshold.
      //
      // src/run-spec-gate.ts — filesystem + git IO orchestrator; excludes
      //   file-system globbing from coverage (tested via integration). The
      //   spec/i18n/openapi diff engines it calls are individually covered.
      //
      // src/proto-arm.ts — wraps buf binary + filesystem; integration-tested
      //   via proto-arm-integration.test.ts (real buf run). Excluded from the
      //   unit-coverage threshold.
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
