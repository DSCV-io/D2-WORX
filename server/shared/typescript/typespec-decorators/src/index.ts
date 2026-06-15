// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Package barrel — the API surface that emitters import.
// Re-exports state-key symbols (the read-back handles emitters need),
// the GrpcMethodPayload type, the $lib descriptor, and the $decorators
// registry. Mirrors the split used by @typespec/http:
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
  type GrpcMethodPayload,
} from "./state-keys.js";

export { $lib } from "./lib.js";

export { $decorators } from "./tsp-index.js";
