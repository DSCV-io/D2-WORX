// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { formatDiagnostic } from "../lib/diagnostics.js";

import { runGeoEmit } from "./index.js";

/**
 * CLI entry point for the geo emitter. Invoked via
 * `pnpm codegen:geo-abstractions` (or directly via
 * `tsx tools/ts-codegen/src/geo-emitter/cli.ts`).
 *
 * Reads the seven Tier-2 geo spec files under `contracts/geo/`, validates
 * them (catalog uniqueness + vocabulary discipline), and emits the type
 * + closed-set-validation files into
 * `server/shared/typescript/geo-abstractions/src/generated/`.
 *
 * `--force` bypasses the per-output mtime check and re-emits unconditionally.
 */
function main(): void {
  const force = process.argv.includes("--force");
  const diagnostics = runGeoEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}

main();
