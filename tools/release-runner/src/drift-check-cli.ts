// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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

import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { falsey } from "@d2/utilities";
import { checkBaselineDrift, formatDriftReport } from "./drift-check.js";
import { loadAllPackages } from "./manifest-loader.js";
import { makeRealDiffProvider } from "./real-diff-provider.js";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const repoRoot = resolve(__dirname, "../../..");

const packages = loadAllPackages(repoRoot);

if (falsey(packages)) {
  process.stderr.write(
    "[drift-check] error: no consumable packages found in the repo tree.\n",
  );
  process.exit(1);
}

// Production mode: localBuild:false makes api-extractor fail on report drift, a
// second independent guard alongside our own `.api.md` member diff.
const diffProvider = makeRealDiffProvider(repoRoot, { localBuild: false });

const result = checkBaselineDrift(packages, diffProvider);

process.stdout.write(formatDriftReport(result));

process.exit(result.clean ? 0 : 1);
