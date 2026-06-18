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
//   InProcess/served-by required: @d2InProcess without @d2ServedBy on the same op → error.
//       An in-process leaf needs a named owner to generate its I<Owner>Api interface.
//   Category required: every op must declare exactly one of @d2Command / @d2Query → error.
//       category-required (neither) or category-exclusive (both).
//   @d2Internal ⊕ exposure: @d2Internal combined with @route / @d2GrpcMethod / @d2InProcess → error.
//       An internal op is not callable across any boundary.
//   Exposure or internal required: every op must carry an exposure decorator or @d2Internal → error.
//       Enforces exposure ⇔ ¬internal as a total, compile-checked invariant.

import type { Operation, Program } from "@typespec/compiler";
import { navigateProgram } from "@typespec/compiler";
import { getRoutePath } from "@typespec/http";
import { $lib } from "./lib.js";
import {
  D2_COMMAND_KEY,
  D2_GRPC_METHOD_KEY,
  D2_HARMLESS_KEY,
  D2_IN_PROCESS_KEY,
  D2_INTERNAL_KEY,
  D2_QUERY_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_SERVED_BY_KEY,
  D2_SERVER_PUSH_KEY,
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

  // ----------------------------------------------------------------
  // @d2InProcess requires @d2ServedBy — a leaf needs a named owner
  // ----------------------------------------------------------------
  for (const [op] of program.stateMap(D2_IN_PROCESS_KEY)) {
    if (!program.stateMap(D2_SERVED_BY_KEY).has(op))
      $lib.reportDiagnostic(program, {
        code: "inprocess-requires-served-by",
        format: { op: (op as Operation).name },
        target: op as Operation,
      });
  }

  // ----------------------------------------------------------------
  // category-required / category-exclusive: every op needs exactly one of
  //     @d2Command / @d2Query.
  // exposure-or-internal-required: every op must carry an exposure
  //     decorator (@route / @d2GrpcMethod / @d2InProcess) or @d2Internal.
  // Both rules walk every operation in the program via navigateProgram.
  // ----------------------------------------------------------------
  navigateProgram(program, {
    operation(op: Operation): void {
      const hasCommand = program.stateMap(D2_COMMAND_KEY).has(op);
      const hasQuery = program.stateMap(D2_QUERY_KEY).has(op);

      if (!hasCommand && !hasQuery)
        $lib.reportDiagnostic(program, {
          code: "category-required",
          format: { op: op.name },
          target: op,
        });
      else if (hasCommand && hasQuery)
        $lib.reportDiagnostic(program, {
          code: "category-exclusive",
          format: { op: op.name },
          target: op,
        });

      // exposure-or-internal-required: exposure or @d2Internal is required on every op.
      const hasRoute = getRoutePath(program, op) !== undefined;
      const hasGrpc = program.stateMap(D2_GRPC_METHOD_KEY).has(op);
      const hasInProcess = program.stateMap(D2_IN_PROCESS_KEY).has(op);
      const hasServerPush = program.stateMap(D2_SERVER_PUSH_KEY).has(op);
      const hasInternal = program.stateMap(D2_INTERNAL_KEY).has(op);
      const hasExposure = hasRoute || hasGrpc || hasInProcess || hasServerPush;

      if (!hasExposure && !hasInternal)
        $lib.reportDiagnostic(program, {
          code: "exposure-or-internal-required",
          format: { op: op.name },
          target: op,
        });
    },
  });

  // ----------------------------------------------------------------
  // internal-op-exposed: @d2Internal ⊕ exposure decorators are mutually exclusive.
  // One internal-op-exposed diagnostic per offending exposure decorator.
  // ----------------------------------------------------------------
  for (const [op] of program.stateMap(D2_INTERNAL_KEY)) {
    const typedOp = op as Operation;

    if (getRoutePath(program, typedOp) !== undefined)
      $lib.reportDiagnostic(program, {
        code: "internal-op-exposed",
        format: { op: typedOp.name, decorator: "@route" },
        target: typedOp,
      });

    if (program.stateMap(D2_GRPC_METHOD_KEY).has(op))
      $lib.reportDiagnostic(program, {
        code: "internal-op-exposed",
        format: { op: typedOp.name, decorator: "@d2GrpcMethod" },
        target: typedOp,
      });

    if (program.stateMap(D2_IN_PROCESS_KEY).has(op))
      $lib.reportDiagnostic(program, {
        code: "internal-op-exposed",
        format: { op: typedOp.name, decorator: "@d2InProcess" },
        target: typedOp,
      });

    if (program.stateMap(D2_SERVER_PUSH_KEY).has(op))
      $lib.reportDiagnostic(program, {
        code: "internal-op-exposed",
        format: { op: typedOp.name, decorator: "@d2ServerPush" },
        target: typedOp,
      });
  }
}
