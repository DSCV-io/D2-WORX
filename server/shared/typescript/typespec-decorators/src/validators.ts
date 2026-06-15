// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Validation helpers called from each $fn body (eager in-decorator checks) and
// from the resilience pipeline-expression parser bridge (validateResilience).
//
// Each helper takes a TypeSpec DecoratorContext (for program + the offending
// target node) and reports diagnostics via $lib.reportDiagnostic.
// Validation reports diagnostics but does NOT block the stateMap().set call
// in the caller — TypeSpec convention is report-and-continue; an error-severity
// diagnostic fails the overall compile but decorators keep storing state so
// downstream passes see consistent program state.

import type { DecoratorContext, Operation } from "@typespec/compiler";
import { $lib } from "./lib.js";
import { loadScopeNames, loadAudienceNames } from "./spec-registry.js";
import { parse } from "./resilience-dsl.js";

// ----------------------------------------------------------------
// Value-set / registry / shape checks (run in each $fn body)
// ----------------------------------------------------------------

const RATE_LIMIT_TIERS = new Set(["Standard", "Elevated", "Restricted"]);
const GRPC_STREAMING_MODES = new Set([
  "unary",
  "serverStream",
  "clientStream",
  "bidiStream",
]);
const PUSH_TARGETS = new Set(["user", "session"]);
const CSRF_POSTURES = new Set(["required", "exempt"]);
const IDEMPOTENT_KEY_SOURCES = new Set(["header", "derived"]);

/** Validate the tier string for @d2RateLimitTier. */
export function validateRateLimitTier(
  context: DecoratorContext,
  target: Operation,
  tier: string,
): void {
  if (!RATE_LIMIT_TIERS.has(tier))
    $lib.reportDiagnostic(context.program, {
      code: "invalid-rate-limit-tier",
      format: { value: tier },
      target,
    });
}

/** Validate the streaming mode string for @d2GrpcMethod. */
export function validateGrpcStreaming(
  context: DecoratorContext,
  target: Operation,
  streaming: string,
): void {
  if (!GRPC_STREAMING_MODES.has(streaming))
    $lib.reportDiagnostic(context.program, {
      code: "invalid-grpc-streaming",
      format: { value: streaming },
      target,
    });
}

/** Validate the pushTarget string for @d2ServerPush. */
export function validatePushTarget(
  context: DecoratorContext,
  target: Operation,
  pushTarget: string,
): void {
  if (!PUSH_TARGETS.has(pushTarget))
    $lib.reportDiagnostic(context.program, {
      code: "invalid-push-target",
      format: { value: pushTarget },
      target,
    });
}

/** Validate the posture string for @d2Csrf. */
export function validateCsrfPosture(
  context: DecoratorContext,
  target: Operation,
  posture: string,
): void {
  if (!CSRF_POSTURES.has(posture))
    $lib.reportDiagnostic(context.program, {
      code: "invalid-csrf-posture",
      format: { value: posture },
      target,
    });
}

/** Validate the @d2Idempotent cross-arg rules. */
export function validateIdempotent(
  context: DecoratorContext,
  target: Operation,
  keySource: string,
  ttlSeconds: number,
  fields: string[],
): void {
  if (!IDEMPOTENT_KEY_SOURCES.has(keySource))
    $lib.reportDiagnostic(context.program, {
      code: "invalid-idempotent-key-source",
      format: { value: keySource },
      target,
    });

  if (ttlSeconds <= 0)
    $lib.reportDiagnostic(context.program, {
      code: "invalid-idempotent-ttl",
      format: { value: String(ttlSeconds) },
      target,
    });

  if (keySource === "derived" && fields.length === 0)
    $lib.reportDiagnostic(context.program, {
      code: "idempotent-derived-requires-fields",
      format: {},
      target,
    });

  if (keySource === "header" && fields.length > 0)
    $lib.reportDiagnostic(context.program, {
      code: "idempotent-header-forbids-fields",
      format: {},
      target,
    });
}

/** Validate each scope name for @d2RequireAnyScope and @d2RequireAllScopes. */
export function validateScopes(
  context: DecoratorContext,
  target: Operation,
  scopes: string[],
): void {
  const known = loadScopeNames();
  for (const scope of scopes) {
    if (!known.has(scope))
      $lib.reportDiagnostic(context.program, {
        code: "unknown-scope",
        format: { value: scope },
        target,
      });
  }
}

/** Validate the audience string for @d2Audience. */
export function validateAudience(
  context: DecoratorContext,
  target: Operation,
  audience: string,
): void {
  // "d2-edge" is the self-audience — always valid without a spec entry
  if (audience === "d2-edge") return;
  const known = loadAudienceNames();
  if (!known.has(audience))
    $lib.reportDiagnostic(context.program, {
      code: "unknown-audience",
      format: { value: audience },
      target,
    });
}

/** Validate the owner string for @d2ServedBy. */
export function validateServedBy(
  context: DecoratorContext,
  target: Operation,
  owner: string,
): void {
  if (owner.trim().length === 0)
    $lib.reportDiagnostic(context.program, {
      code: "empty-served-by",
      format: {},
      target,
    });
}

// ----------------------------------------------------------------
// @d2Resilience pipeline-expression parse + report
// ----------------------------------------------------------------

/** Parse the pipeline-expression string and report any parse errors as diagnostics. */
export function validateResilience(
  context: DecoratorContext,
  target: Operation,
  pipeline: string,
): void {
  const result = parse(pipeline);
  if (result.ok) return;
  for (const err of result.errors) {
    switch (err.code) {
      case "resilience-malformed":
        $lib.reportDiagnostic(context.program, {
          code: "resilience-malformed",
          format: { detail: err.message },
          target,
        });
        break;
      case "resilience-unknown-policy":
        // Extract the policy name from the error message for the format param
        $lib.reportDiagnostic(context.program, {
          code: "resilience-unknown-policy",
          format: { policy: extractPolicy(err.message) },
          target,
        });
        break;
      case "resilience-unknown-arg": {
        const [p, a] = extractPolicyArg(err.message);
        $lib.reportDiagnostic(context.program, {
          code: "resilience-unknown-arg",
          format: { policy: p, arg: a },
          target,
        });
        break;
      }
      case "resilience-bad-arg": {
        const [p2, a2, detail2] = extractPolicyArgDetail(err.message);
        $lib.reportDiagnostic(context.program, {
          code: "resilience-bad-arg",
          format: { policy: p2, arg: a2, detail: detail2 },
          target,
        });
        break;
      }
      case "resilience-multiple-inner":
        $lib.reportDiagnostic(context.program, {
          code: "resilience-multiple-inner",
          format: { policy: extractPolicy(err.message) },
          target,
        });
        break;
      case "resilience-positional-after-named":
        $lib.reportDiagnostic(context.program, {
          code: "resilience-positional-after-named",
          format: { policy: extractPolicy(err.message) },
          target,
        });
        break;
    }
  }
}

// ----------------------------------------------------------------
// Internal helpers for extracting format params from parser messages
// ----------------------------------------------------------------

function extractPolicy(msg: string): string {
  // Pattern: "'<policy>' ..." — all policy-level parser messages contain the policy
  // name in single quotes; the non-null assertion is safe by construction.
  return /'([^']+)'/.exec(msg)![1]!;
}

function extractPolicyArg(msg: string): [string, string] {
  // Pattern: "'<policy>' has no tunable '<arg>'" or "'<policy>' has no positional slot N"
  // The resilience-dsl parser always emits messages that match this pattern for
  // resilience-unknown-arg errors; the non-null assertion is safe by construction.
  const m = /'([^']+)' has no (?:tunable|positional slot) '?([^']+)'?/.exec(
    msg,
  )!;
  return [m[1]!, m[2]!];
}

function extractPolicyArgDetail(msg: string): [string, string, string] {
  // Pattern: "'<policy>.<arg>' is invalid: <detail>"
  // The resilience-dsl parser always emits messages that match this pattern for
  // resilience-bad-arg errors; the non-null assertion is safe by construction.
  const m = /'([^.]+)\.([^']+)' is invalid: (.+)/.exec(msg)!;
  return [m[1]!, m[2]!, m[3]!];
}
