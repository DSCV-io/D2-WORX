// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Package barrel — the API surface that emitters import.
// Re-exports state-key symbols (the read-back handles emitters need),
// the GrpcMethodPayload type, the $lib descriptor, the $decorators
// registry, and the resilience-DSL parser + AST types.
// Mirrors the split used by @typespec/http:
//   index.js  = emitter API
//   tsp-index.js = what main.tsp imports

export {
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_AUDIENCE_KEY,
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_REDACT_KEY,
  D2_SERVER_PUSH_KEY,
  D2_IDEMPOTENT_KEY,
  D2_RESILIENCE_KEY,
  D2_CSRF_KEY,
  D2_HARMLESS_KEY,
  type GrpcMethodPayload,
  type IdempotentPayload,
} from "./state-keys.js";

export { $lib } from "./lib.js";

export { $decorators } from "./tsp-index.js";

// Resilience DSL parser + AST types — consumed by the emitter fleet.
export {
  parse,
  type ResiliencePolicyNode,
  type ResilienceParseResult,
  type ResilienceParseError,
  type ResilienceDiagnosticCode,
} from "./resilience-dsl.js";
