// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// CLI entry point for the pre-commit baseline-currency check.
//
// Usage:
//   pnpm --filter release-runner exec tsx src/fingerprint-currency-cli.ts
//
// Recomputes each consumable's source-based fingerprint over the WORKING TREE
// and compares it to the ON-DISK committed .release-fingerprint. Also asserts
// every .NET consumable's PublicAPI.Unshipped.txt is header-only. Fails
// (exit 1) when any baseline is stale; prints a table of stale packages.
// This is the pre-commit gate — it checks "did you forget to re-seed after
// changing the source?", NOT the CI drift-check (which compares against HEAD
// and requires a version bump on any change).
//
// Excluded from the unit-coverage threshold (see vitest.config.ts) — the
// testable logic lives in checkFingerprintCurrency / formatCurrencyReport in
// fingerprint-currency.ts; this shim only resolves the repo root, wires the
// real inventory loader, and maps the result to an exit code.

import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { falsey } from "@d2/utilities";
import {
  checkFingerprintCurrency,
  formatCurrencyReport,
} from "./fingerprint-currency.js";
import { loadAllPackages } from "./manifest-loader.js";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const repoRoot = resolve(__dirname, "../../..");

const packages = loadAllPackages(repoRoot);

if (falsey(packages)) {
  process.stderr.write(
    "[fingerprint-currency] error: no consumable packages found in the repo tree.\n",
  );
  process.exit(1);
}

const result = checkFingerprintCurrency(packages, repoRoot);

process.stdout.write(formatCurrencyReport(result));

process.exit(result.allCurrent ? 0 : 1);
