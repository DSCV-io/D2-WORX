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
  D2_RESILIENCE_RETRY_WHEN_KEY,
  D2_RESILIENCE_FAIL_WHEN_KEY,
  D2_CSRF_KEY,
  D2_HARMLESS_KEY,
  D2_IN_PROCESS_KEY,
  D2_COMMAND_KEY,
  D2_QUERY_KEY,
  D2_INTERNAL_KEY,
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

// Result-predicate (retryWhen / failWhen) parser + AST types + diagnostic
// codes — consumed by the C# / TS predicate emitter.
export {
  parseResultPredicate,
  type ResultPredicateParseResult,
  type ResultPredicateParseError,
  type ResultPredicateDiagnosticCode,
  type PredicateNode,
  type BoolNode,
  type ComparisonNode,
  type BooleanAccessNode,
  type AccessNode,
  type EnvelopeAccessNode,
  type EnvelopeField,
  type DataPathNode,
  type PathSegment,
  type FieldSegment,
  type ArrayAccessorSegment,
  type LiteralNode,
} from "./result-predicate-dsl.js";

// Result-predicate validation surface — the decorator-body validator and the
// native-TypeSpec model-graph walk (the $onValidate model arm).
export { validateResultPredicate } from "./validators.js";
export {
  walkPredicateModel,
  type ModelWalkError,
} from "./predicate-model-walk.js";

// Error-code / error-category registry loaders — used by the result-predicate
// validator and re-exported for emitter / test access.
export { loadErrorCodeNames, loadErrorCategoryNames } from "./spec-registry.js";
