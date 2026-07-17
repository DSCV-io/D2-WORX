// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// CLI entry point for the baseline-drift check.
//
// Usage:
//   pnpm --filter release-runner exec tsx src/drift-check-cli.ts
//
// Re-extracts every consumable's API surface + recomputes its output fingerprint
// and compares to the committed baselines, failing (exit 1) on any drift. Used by
// the `versioning-integration` CI lane.
//
// Excluded from the unit-coverage threshold (see vitest.config.ts) — the testable
// logic lives in `checkBaselineDrift` / `formatDriftReport` in drift-check.ts;
// this shim only resolves the repo root, wires the real DiffProvider + the
// inventory loader (both real-IO seams), and maps the result to an exit code.

import { falsey } from "@dcsv-io/d2-utilities";
import { checkBaselineDrift, formatDriftReport } from "./drift-check.js";
import { loadAllPackages } from "./manifest-loader.js";
import { makeRealDiffProvider } from "./real-diff-provider.js";
import { repoRoot } from "./repo-root.js";

const packages = loadAllPackages(repoRoot);

if (falsey(packages)) {
  process.stderr.write(
    "[drift-check] error: no consumable packages found in the repo tree.\n",
  );
  process.exit(1);
}

// The drift check is build-free: it diffs the committed API reports (git-ref
// text diff) + recomputes the source-based fingerprint, both against HEAD. The
// `.api.md` CURRENCY (committed report vs the built dist) is enforced separately
// in the CI lane by a production-mode api-extractor run.
const diffProvider = makeRealDiffProvider(repoRoot);

const result = checkBaselineDrift(packages, diffProvider);

process.stdout.write(formatDriftReport(result));

process.exit(result.clean ? 0 : 1);
