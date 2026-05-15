// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { runAuthContextEmit } from "./auth-context-emit.js";
import { runAuthErrorCodesEmit } from "./auth-error-codes-emit.js";
import { runAuthFailuresEmit } from "./auth-failures-emit.js";
import { runAuthScopesEmit } from "./auth-scopes-emit.js";
import { runHeadersEmit } from "./headers-emit.js";
import { runJwtClaimsEmit } from "./jwt-claims-emit.js";
import { formatDiagnostic } from "./lib/diagnostics.js";
import { runRequestContextEmit } from "./request-context-emit.js";

/**
 * Top-level orchestrator. Runs every per-topic emitter in dep-graph
 * order. `--force` bypasses the per-spec mtime check and re-emits every
 * artifact unconditionally (used for verification + spec-edit sweeps).
 */
function main(): void {
  const force = process.argv.includes("--force");
  const allDiagnostics = [
    ...runAuthContextEmit(force),
    // request-context extends auth-context — emit auth-context first.
    ...runRequestContextEmit(force),
    ...runAuthScopesEmit(force),
    ...runAuthErrorCodesEmit(force),
    // auth-failures depends on auth-error-codes constants — emit after.
    ...runAuthFailuresEmit(force),
    // headers + jwt-claims emit independent catalogs from their own specs.
    ...runHeadersEmit(force),
    ...runJwtClaimsEmit(force),
  ];
  for (const d of allDiagnostics) console.error(formatDiagnostic(d));
  if (allDiagnostics.some((d) => d.severity === "error")) {
    process.exit(1);
  }
}

main();
