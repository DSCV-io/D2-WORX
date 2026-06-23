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

import type {
  DecoratorContext,
  ModelProperty,
  Operation,
} from "@typespec/compiler";
import { $lib } from "./lib.js";
import {
  loadScopeNames,
  loadAudienceNames,
  loadErrorCodeNames,
  loadErrorCategoryNames,
} from "./spec-registry.js";
import { parse } from "./resilience-dsl.js";
import { parseResultPredicate } from "./result-predicate-dsl.js";
import type { LiteralNode, PredicateNode } from "./result-predicate-dsl.js";

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
// @d2Field field-number validation
// ----------------------------------------------------------------

/**
 * Proto3 maximum field number (2^29 - 1).
 * Numbers above this are not valid proto3 field numbers.
 */
const _PROTO_MAX_FIELD_NUMBER = 536870911;

/**
 * Protobuf implementation-reserved range (inclusive). Field numbers in this
 * range are reserved for the Protobuf implementation and must not be used
 * by authors, even though proto3 allows numbers up to 536870911.
 */
const _PROTO_RESERVED_RANGE_LO = 19000;
const _PROTO_RESERVED_RANGE_HI = 19999;

/**
 * Validate the integer field number supplied to @d2Field.
 * Fires `invalid-field-number` when:
 *   - the value is not a safe integer (non-integer float, NaN, Infinity)
 *   - the value is less than 1
 *   - the value exceeds 536870911 (proto3 max)
 *   - the value falls in the protobuf reserved range 19000–19999
 */
export function validateFieldNumber(
  context: DecoratorContext,
  target: ModelProperty,
  number: number,
): void {
  const isValid =
    Number.isInteger(number) &&
    number >= 1 &&
    number <= _PROTO_MAX_FIELD_NUMBER &&
    (number < _PROTO_RESERVED_RANGE_LO || number > _PROTO_RESERVED_RANGE_HI);

  if (!isValid)
    $lib.reportDiagnostic(context.program, {
      code: "invalid-field-number",
      format: { value: String(number) },
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

// ----------------------------------------------------------------
// @d2Resilience result-predicate (retryWhen / failWhen) parse + report
// ----------------------------------------------------------------

/**
 * Parse a retryWhen / failWhen result-predicate string, reporting parser errors
 * (malformed / unknown-field / type-mismatch / shadowed-elem-var) AND the
 * registry-value errors (unknown-error-code / unknown-category) as diagnostics.
 *
 * The model-dependent checks (unknown output / element field, not-a-collection,
 * terminal data type-mismatch) need the op's resolved TOutput graph and run in
 * $onValidate. Report-and-continue per the package convention (this does not
 * block the caller's stateMap().set).
 *
 * @param which - "retryWhen" | "failWhen" — surfaced in every diagnostic so the
 *                author knows which predicate failed.
 */
export function validateResultPredicate(
  context: DecoratorContext,
  target: Operation,
  expr: string,
  which: string,
): void {
  const result = parseResultPredicate(expr);

  if (!result.ok) {
    // The parser emits only `{ which, detail }`-shaped codes (malformed /
    // unknown-field / type-mismatch / shadowed-elem-var). The value-shaped
    // registry codes are produced exclusively by checkRegistryLiterals below.
    for (const err of result.errors)
      $lib.reportDiagnostic(context.program, {
        code: err.code,
        format: { which, detail: err.message },
        target,
      });

    return;
  }

  // Registry-value arm (eager — the registries are available without the model).
  checkRegistryLiterals(context, target, result.root, which);
}

/**
 * Walk the parsed predicate for `result.errorCode` / `result.category` literals
 * and check each against the closed registries. Reports unknown-error-code /
 * unknown-category. (Both checks need only the registries — not the op model —
 * so they run here in the decorator body, matching validateScopes/Audience.)
 */
function checkRegistryLiterals(
  context: DecoratorContext,
  target: Operation,
  node: PredicateNode,
  which: string,
): void {
  if (node.kind === "bool") {
    checkRegistryLiterals(context, target, node.left, which);
    checkRegistryLiterals(context, target, node.right, which);
    return;
  }

  // A standalone boolean data path (any/all/contains) cannot reach an envelope
  // errorCode/category accessor, so there is nothing to check here.
  if (node.kind === "booleanAccess") return;

  if (node.access.kind !== "envelope") return;

  const field = node.access.field;
  if (field !== "errorCode" && field !== "category") return;

  const literals: readonly LiteralNode[] = Array.isArray(node.rhs)
    ? node.rhs
    : [node.rhs];
  const known =
    field === "errorCode" ? loadErrorCodeNames() : loadErrorCategoryNames();
  const code =
    field === "errorCode"
      ? "resilience-predicate-unknown-error-code"
      : "resilience-predicate-unknown-category";

  for (const lit of literals) {
    // Only string literals address the closed registries; a non-string literal
    // is a type-mismatch already caught by the parser, so skip it here.
    if (lit.kind === "string" && !known.has(lit.value))
      $lib.reportDiagnostic(context.program, {
        code,
        format: { which, detail: `'${lit.value}'` },
        target,
      });
  }
}
