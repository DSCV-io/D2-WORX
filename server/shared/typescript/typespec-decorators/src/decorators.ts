// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type {
  DecoratorContext,
  Model,
  ModelProperty,
  Operation,
} from "@typespec/compiler";
import {
  D2_AUDIENCE_KEY,
  D2_COMMAND_KEY,
  D2_CSRF_KEY,
  D2_FIELD_KEY,
  D2_GRPC_METHOD_KEY,
  D2_HARMLESS_KEY,
  D2_IDEMPOTENT_KEY,
  D2_IN_PROCESS_KEY,
  D2_INTERNAL_KEY,
  D2_QUERY_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_REDACT_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_RESERVED_KEY,
  D2_RESILIENCE_FAIL_WHEN_KEY,
  D2_RESILIENCE_KEY,
  D2_RESILIENCE_RETRY_WHEN_KEY,
  D2_SERVED_BY_KEY,
  D2_SERVER_PUSH_KEY,
} from "./state-keys.js";
import {
  validateAudience,
  validateCsrfPosture,
  validateFieldNumber,
  validateGrpcStreaming,
  validateIdempotent,
  validatePushTarget,
  validateRateLimitTier,
  validateResilience,
  validateResultPredicate,
  validateReservedName,
  validateReservedNumber,
  validateScopes,
  validateServedBy,
} from "./validators.js";

/**
 * Stores the required OAuth 2.0 scopes array on the operation (any-match:
 * at-least-one scope must be present). Mirrors `RequireAnyScope(...)` with
 * `ScopeMatch.Any`. Emitters read back:
 * program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).get(op) → string[].
 */
export function $d2RequireAnyScope(
  context: DecoratorContext,
  target: Operation,
  ...scopes: string[]
): void {
  validateScopes(context, target, scopes);
  context.program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).set(target, scopes);
}

/**
 * Stores the required OAuth 2.0 scopes array on the operation (all-match:
 * every scope must be present). Mirrors `RequireAllScopes(...)` with
 * `ScopeMatch.All`. Emitters read back:
 * program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).get(op) → string[].
 */
export function $d2RequireAllScopes(
  context: DecoratorContext,
  target: Operation,
  ...scopes: string[]
): void {
  validateScopes(context, target, scopes);
  context.program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).set(target, scopes);
}

/**
 * Stores the rate-limit tier string on the operation.
 * Valid tier values: Standard | Elevated | Restricted.
 * Emitters read back: program.stateMap(D2_RATE_LIMIT_TIER_KEY).get(op) → string.
 */
export function $d2RateLimitTier(
  context: DecoratorContext,
  target: Operation,
  tier: string,
): void {
  validateRateLimitTier(context, target, tier);
  context.program.stateMap(D2_RATE_LIMIT_TIER_KEY).set(target, tier);
}

/**
 * Stores the expected JWT audience claim string on the operation.
 * Validated against the spec-driven set in
 * contracts/auth-protocol-audiences/protocol-audiences.spec.json via
 * loadProtocolAudienceValues() — only declared protocol audiences pass.
 * Emitters read back: program.stateMap(D2_AUDIENCE_KEY).get(op) → string.
 */
export function $d2Audience(
  context: DecoratorContext,
  target: Operation,
  audience: string,
): void {
  validateAudience(context, target, audience);
  context.program.stateMap(D2_AUDIENCE_KEY).set(target, audience);
}

/**
 * Stores the owning module/service string on the operation.
 * Shape-check only — the owner string must be non-empty.
 * Emitters read back: program.stateMap(D2_SERVED_BY_KEY).get(op) → string.
 */
export function $d2ServedBy(
  context: DecoratorContext,
  target: Operation,
  owner: string,
): void {
  validateServedBy(context, target, owner);
  context.program.stateMap(D2_SERVED_BY_KEY).set(target, owner);
}

/**
 * Stores the gRPC service+method+streaming-mode tuple on the operation.
 * `streaming` selects the proto `rpc` form; valid values are
 * `unary | serverStream | clientStream | bidiStream`. Defaults to `"unary"`
 * when not provided. Emitters read back:
 * program.stateMap(D2_GRPC_METHOD_KEY).get(op) → { service, method, streaming }.
 */
export function $d2GrpcMethod(
  context: DecoratorContext,
  target: Operation,
  service: string,
  method: string,
  streaming?: string,
): void {
  const mode = streaming ?? "unary";
  validateGrpcStreaming(context, target, mode);
  context.program
    .stateMap(D2_GRPC_METHOD_KEY)
    .set(target, { service, method, streaming: mode });
}

/**
 * Marks a model property as PII to be redacted in structured logs.
 * Stores `true` on the property; emitters check for presence.
 * The target type `ModelProperty` is enforced by the `extern dec` declaration
 * in lib/main.tsp — no additional runtime check is needed here.
 */
export function $d2Redact(
  context: DecoratorContext,
  target: ModelProperty,
): void {
  context.program.stateMap(D2_REDACT_KEY).set(target, true);
}

/**
 * Binds an operation to a server-initiated push channel. `pushTarget` selects
 * the channel class; valid values are `user | session`.
 * Emitters read back: program.stateMap(D2_SERVER_PUSH_KEY).get(op) → string.
 */
export function $d2ServerPush(
  context: DecoratorContext,
  target: Operation,
  pushTarget: string,
): void {
  validatePushTarget(context, target, pushTarget);
  context.program.stateMap(D2_SERVER_PUSH_KEY).set(target, pushTarget);
}

/**
 * Marks a mutating operation as idempotent, driving a generated dedupe gate.
 * `keySource` selects key extraction; `ttlSeconds` is the replay window;
 * `fields` (for `"derived"` keySource) are the input property names hashed
 * into the key, in declaration order. Emitters read back:
 * program.stateMap(D2_IDEMPOTENT_KEY).get(op) → { keySource, ttlSeconds, fields }.
 */
export function $d2Idempotent(
  context: DecoratorContext,
  target: Operation,
  keySource: string,
  ttlSeconds: number,
  ...fields: string[]
): void {
  validateIdempotent(context, target, keySource, ttlSeconds, fields);
  context.program
    .stateMap(D2_IDEMPOTENT_KEY)
    .set(target, { keySource, ttlSeconds, fields });
}

/**
 * Wraps an operation's outbound call in a resilience pipeline expressed as a
 * composable DSL string (e.g. `"retry(circuitBreaker(singleflight()))"`).
 * The expression is parsed and validated at compile time; the parsed AST is
 * consumed by the emitter. Emitters read back:
 * program.stateMap(D2_RESILIENCE_KEY).get(op) → string.
 *
 * The optional `predicates` options arg supplies custom `retryWhen` / `failWhen`
 * result-predicate strings. Each is shape- and registry-validated here
 * (`validateResultPredicate`) and model-validated against the op's TOutput in
 * `$onValidate`; the raw strings are stored on the dedicated state keys for the
 * emitter to re-parse. `failWhen` takes precedence over `retryWhen`. The
 * single-positional form (no `predicates`) is unchanged and back-compatible.
 * Emitters read back:
 * program.stateMap(D2_RESILIENCE_RETRY_WHEN_KEY).get(op) → string,
 * program.stateMap(D2_RESILIENCE_FAIL_WHEN_KEY).get(op) → string.
 */
export function $d2Resilience(
  context: DecoratorContext,
  target: Operation,
  pipeline: string,
  predicates?: { readonly retryWhen?: string; readonly failWhen?: string },
): void {
  validateResilience(context, target, pipeline);
  context.program.stateMap(D2_RESILIENCE_KEY).set(target, pipeline);

  if (predicates?.retryWhen !== undefined) {
    validateResultPredicate(context, target, predicates.retryWhen, "retryWhen");
    context.program
      .stateMap(D2_RESILIENCE_RETRY_WHEN_KEY)
      .set(target, predicates.retryWhen);
  }

  if (predicates?.failWhen !== undefined) {
    validateResultPredicate(context, target, predicates.failWhen, "failWhen");
    context.program
      .stateMap(D2_RESILIENCE_FAIL_WHEN_KEY)
      .set(target, predicates.failWhen);
  }
}

/**
 * CSRF posture override for an operation. Valid posture values: required | exempt.
 * Emitters read back: program.stateMap(D2_CSRF_KEY).get(op) → string.
 */
export function $d2Csrf(
  context: DecoratorContext,
  target: Operation,
  posture: string,
): void {
  validateCsrfPosture(context, target, posture);
  context.program.stateMap(D2_CSRF_KEY).set(target, posture);
}

/**
 * Marks an operation as auth-pipeline-exempt (health probes / discovery only).
 * Stores `true` on the operation; emitters check for presence.
 * Mutually exclusive with scope decorators — enforced by $onValidate.
 */
export function $d2Harmless(
  context: DecoratorContext,
  target: Operation,
): void {
  context.program.stateMap(D2_HARMLESS_KEY).set(target, true);
}

/**
 * Marks an operation as eligible for in-process "leaf" invocation — a co-hosted
 * module calling the owning module directly, with no network hop. Drives the
 * generated I<Owner>Api leaf interface; the explicit leaf-vs-gRPC trigger.
 * Stores `true` on the operation; emitters check for presence.
 * Requires @d2ServedBy on the same op — enforced by $onValidate.
 */
export function $d2InProcess(
  context: DecoratorContext,
  target: Operation,
): void {
  context.program.stateMap(D2_IN_PROCESS_KEY).set(target, true);
}

/**
 * Marks an operation as a Command — it mutates persistent or shared state (DB write,
 * distributed-cache write, external write, or message publish). Drives the generated
 * handler into the Commands/ CQRS folder and the matching namespace segment.
 * Stores `true` on the operation; emitters check for presence.
 * Mutually exclusive with @d2Query (exactly one category required) — enforced by $onValidate.
 */
export function $d2Command(context: DecoratorContext, target: Operation): void {
  context.program.stateMap(D2_COMMAND_KEY).set(target, true);
}

/**
 * Marks an operation as a Query — it is read-only (no persistent or shared-state mutation;
 * local/in-memory caching does not make a Query a Command). Drives the generated handler into
 * the Queries/ CQRS folder and the matching namespace segment.
 * Stores `true` on the operation; emitters check for presence.
 * Mutually exclusive with @d2Command (exactly one category required) — enforced by $onValidate.
 */
export function $d2Query(context: DecoratorContext, target: Operation): void {
  context.program.stateMap(D2_QUERY_KEY).set(target, true);
}

/**
 * Marks an operation as in-app-only — NOT callable across any boundary. The emitter suppresses
 * every cross-boundary surface (REST route, gRPC service + proto message, in-process leaf entry,
 * and the I<Module>Client entry), so the op is structurally absent from the module's cross-boundary
 * client. The explicit "not callable from outside" marker.
 * Stores `true` on the operation; emitters check for presence.
 * Mutually exclusive with the exposure decorators (@route / @d2GrpcMethod / @d2InProcess) —
 * enforced by $onValidate.
 */
export function $d2Internal(
  context: DecoratorContext,
  target: Operation,
): void {
  context.program.stateMap(D2_INTERNAL_KEY).set(target, true);
}

/**
 * Pins a model property to an explicit proto3 field number. The number must be
 * a positive integer in the range [1, 536870911] and must NOT fall in the
 * protobuf implementation-reserved range 19000–19999.
 *
 * Author-pinned numbers guarantee wire stability: the proto emitter uses this
 * number verbatim instead of assigning positionally, so reordering, inserting,
 * or removing properties does not silently renumber surviving fields.
 *
 * The proto emitter requires every field on a @d2GrpcMethod-bound model to carry
 * @d2Field; an unpinned field on a proto-bound model is a loud build failure.
 *
 * Emitters read back:
 * program.stateMap(D2_FIELD_KEY).get(prop) → number.
 */
export function $d2Field(
  context: DecoratorContext,
  target: ModelProperty,
  number: number,
): void {
  validateFieldNumber(context, target, number);
  context.program.stateMap(D2_FIELD_KEY).set(target, number);
}

/**
 * Declares author-owned reserved field numbers and names on a model. Reserved
 * entries document proto3 slots that were previously used and must never be
 * reused (prevents silent wire-format collisions with old clients/servers).
 *
 * `numbers` is a variadic list of previously-used field numbers. Each number
 * must be a positive integer ≥ 1, ≤ 536870911 (proto3 max), and must NOT fall
 * in the protobuf implementation-reserved range 19000–19999. An invalid number
 * fires `invalid-reserved-number` (error severity). `names` is stored under the
 * model to emit `reserved "old_name";` lines. Pass numbers through the
 * `...numbers` variadic; for names, supply a separate `@d2Reserved` call or
 * use the names parameter (see lib/main.tsp for the TypeSpec-level signature).
 *
 * Emitters read back:
 * program.stateMap(D2_RESERVED_KEY).get(model) → { numbers: number[], names: string[] }.
 */
export function $d2Reserved(
  context: DecoratorContext,
  target: Model,
  names: string,
  ...numbers: number[]
): void {
  for (const n of numbers) validateReservedNumber(context, target, n);

  const parsed = names
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);

  for (const name of parsed) validateReservedName(context, target, name);

  context.program
    .stateMap(D2_RESERVED_KEY)
    .set(target, { numbers: [...numbers], names: parsed });
}
