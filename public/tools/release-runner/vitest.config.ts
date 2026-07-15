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
      // src/git-adapter.ts — thin IO seam over `git log` and `git diff`;
      //   integration-tested via the dry-run CLI path against the real repo.
      //   Excluded from the unit-coverage threshold; integration coverage
      //   documented in VALIDATION.md.
      //
      // src/cli.ts — CLI entry point; orchestration layer over tested
      //   sub-units. process.exit + process.argv make unit-testing
      //   impractical; the arms themselves are fully covered. Excluded from
      //   the coverage threshold.
      exclude: [
        // src/index.ts — barrel re-export; no testable logic of its own.
        "src/index.ts",
        // src/types.ts — type declarations only; no runtime logic to test.
        "src/types.ts",
        // src/git-adapter.ts — thin IO seam over `git log` and `git diff-tree`;
        //   integration-tested via the dry-run CLI path against the real repo.
        //   Excluded from the unit-coverage threshold; integration coverage
        //   documented in VALIDATION.md.
        "src/git-adapter.ts",
        // src/manifest-loader.ts — filesystem-discovery seam (readdirSync walks);
        //   integration-tested via the dry-run CLI path against the real repo.
        //   The individual readers it delegates to (readNpmVersion, readNugetVersion)
        //   are unit-covered in manifest-editor.test.ts.
        "src/manifest-loader.ts",
        // src/cli.ts — CLI entry point; orchestration layer over tested sub-units.
        //   process.exit + process.argv make unit-testing impractical; the arms
        //   themselves are fully covered. Excluded from the coverage threshold.
        "src/cli.ts",
        // src/drift-check-cli.ts — CLI entry point for the baseline-drift check;
        //   process.exit + real-IO wiring (loadAllPackages + makeRealDiffProvider).
        //   The testable core (checkBaselineDrift, formatDriftReport) lives in
        //   drift-check.ts and is fully unit-covered. Excluded from the threshold.
        "src/drift-check-cli.ts",
        // src/ts-api-adapter.ts — the pure parsing/diff helpers + path resolvers
        //   are unit-covered, but the real seam constructors (makeGitBaselineReader
        //   shells `git show`, makeRealApiExtractorRunner requires + invokes
        //   api-extractor) are IO and exercised by the gated integration lane.
        //   Excluded from the unit-coverage threshold.
        "src/ts-api-adapter.ts",
        // src/fingerprint-currency-cli.ts — CLI entry point for the pre-commit
        //   baseline-currency gate; process.exit + real-IO wiring (loadAllPackages
        //   + makeRealCurrencyFileReader). The testable core (checkFingerprintCurrency,
        //   formatCurrencyReport) lives in fingerprint-currency.ts and is fully
        //   unit-covered. Excluded from the coverage threshold.
        "src/fingerprint-currency-cli.ts",
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
