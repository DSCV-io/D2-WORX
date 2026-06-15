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
  D2_GRPC_METHOD_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_REDACT_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_SERVED_BY_KEY,
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
 * Shape-check only — registry validation is deferred until the service/module
 * catalog stabilizes.
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
