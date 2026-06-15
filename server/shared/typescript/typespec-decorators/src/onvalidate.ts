// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Program-level cross-decorator validation hook.
//
// Exported as $onValidate — the TypeSpec compiler discovers this named export
// from the tsp-index module and runs it once after all decorators have applied.
// Mirrors the @typespec/http pattern (tsp-index.ts re-exports $onValidate from
// validate.ts).
//
// Checks:
//   D4: @d2RateLimitTier on an op with no public HTTP @route → error.
//       Internal operations bypass Edge rate-limiting and must not carry a tier.
//   Harmless/scope conflict: @d2Harmless + a scope decorator on the same op → error.
//       An auth-exempt operation cannot also require scopes.

import type { Operation, Program } from "@typespec/compiler";
import { getRoutePath } from "@typespec/http";
import { $lib } from "./lib.js";
import {
  D2_HARMLESS_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_REQUIRE_ANY_SCOPE_KEY,
} from "./state-keys.js";

/** Program-level cross-decorator validation. Runs after all decorators apply. */
export function $onValidate(program: Program): void {
  // ----------------------------------------------------------------
  // D4: @d2RateLimitTier requires a public HTTP @route
  // ----------------------------------------------------------------
  for (const [op] of program.stateMap(D2_RATE_LIMIT_TIER_KEY)) {
    const routePath = getRoutePath(program, op as Operation);
    if (routePath === undefined)
      $lib.reportDiagnostic(program, {
        code: "rate-tier-requires-route",
        format: { op: (op as Operation).name },
        target: op as Operation,
      });
  }

  // ----------------------------------------------------------------
  // @d2Harmless ⊕ scope decorators are mutually exclusive
  // ----------------------------------------------------------------
  for (const [op] of program.stateMap(D2_HARMLESS_KEY)) {
    const hasAnyScope = program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).has(op);
    const hasAllScopes = program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).has(op);
    if (hasAnyScope || hasAllScopes)
      $lib.reportDiagnostic(program, {
        code: "harmless-scope-conflict",
        format: { op: (op as Operation).name },
        target: op as Operation,
      });
  }
}
