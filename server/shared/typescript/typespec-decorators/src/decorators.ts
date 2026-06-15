// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type {
  DecoratorContext,
  ModelProperty,
  Operation,
} from "@typespec/compiler";
import {
  D2_AUDIENCE_KEY,
  D2_CSRF_KEY,
  D2_GRPC_METHOD_KEY,
  D2_HARMLESS_KEY,
  D2_IDEMPOTENT_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_REDACT_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_RESILIENCE_KEY,
  D2_SERVED_BY_KEY,
  D2_SERVER_PUSH_KEY,
} from "./state-keys.js";

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
  context.program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).set(target, scopes);
}

/**
 * Stores the rate-limit tier string on the operation.
 * Valid tier values (Standard | Elevated | Restricted) are enforced by the
 * validation layer; this decorator stores the raw string.
 */
export function $d2RateLimitTier(
  context: DecoratorContext,
  target: Operation,
  tier: string,
): void {
  context.program.stateMap(D2_RATE_LIMIT_TIER_KEY).set(target, tier);
}

/**
 * Stores the expected JWT audience claim string on the operation.
 * Audience validation against the audiences spec is handled by the validation layer.
 */
export function $d2Audience(
  context: DecoratorContext,
  target: Operation,
  audience: string,
): void {
  context.program.stateMap(D2_AUDIENCE_KEY).set(target, audience);
}

/**
 * Stores the owning module/service string on the operation.
 * Shape-check only — the owner string is not validated against a module registry.
 */
export function $d2ServedBy(
  context: DecoratorContext,
  target: Operation,
  owner: string,
): void {
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
  context.program
    .stateMap(D2_GRPC_METHOD_KEY)
    .set(target, { service, method, streaming: streaming ?? "unary" });
}

/**
 * Marks a model property as PII to be redacted in structured logs.
 * Stores `true` on the property; emitters check for presence.
 */
export function $d2Redact(
  context: DecoratorContext,
  target: ModelProperty,
): void {
  context.program.stateMap(D2_REDACT_KEY).set(target, true);
}

/**
 * Binds an operation to a server-initiated push channel. `pushTarget` selects
 * the channel class; validation of allowed values is deferred to the validation
 * layer. Emitters read back:
 * program.stateMap(D2_SERVER_PUSH_KEY).get(op) → string.
 */
export function $d2ServerPush(
  context: DecoratorContext,
  target: Operation,
  pushTarget: string,
): void {
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
  context.program
    .stateMap(D2_IDEMPOTENT_KEY)
    .set(target, { keySource, ttlSeconds, fields });
}

/**
 * Wraps an operation's outbound call in a resilience pipeline expressed as a
 * composable DSL string (e.g. `"retry(circuitBreaker(singleflight()))"`).
 * Stores the raw expression; parsing and policy-graph construction are handled
 * by the emitter. Emitters read back:
 * program.stateMap(D2_RESILIENCE_KEY).get(op) → string.
 */
export function $d2Resilience(
  context: DecoratorContext,
  target: Operation,
  pipeline: string,
): void {
  context.program.stateMap(D2_RESILIENCE_KEY).set(target, pipeline);
}

/**
 * CSRF posture override for an operation. Stores the raw `posture` string;
 * validation of allowed values is deferred to the validation layer. Emitters
 * read back: program.stateMap(D2_CSRF_KEY).get(op) → string.
 */
export function $d2Csrf(
  context: DecoratorContext,
  target: Operation,
  posture: string,
): void {
  context.program.stateMap(D2_CSRF_KEY).set(target, posture);
}

/**
 * Marks an operation as auth-pipeline-exempt (health probes / discovery only).
 * Stores `true` on the operation; emitters check for presence.
 */
export function $d2Harmless(
  context: DecoratorContext,
  target: Operation,
): void {
  context.program.stateMap(D2_HARMLESS_KEY).set(target, true);
}
