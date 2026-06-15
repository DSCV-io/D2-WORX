// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// State-key constants — one per decorator.
//
// Symbol.for(...) is process-global and value-stable: any module that calls
// Symbol.for("D2.d2RequireAnyScope") gets the identical symbol. Emitters
// import these exact symbols to read back what a decorator wrote via
// program.stateMap(KEY).

/** State key for @d2RequireAnyScope — stores the required scopes array (any-match). */
export const D2_REQUIRE_ANY_SCOPE_KEY = Symbol.for("D2.d2RequireAnyScope");

/** State key for @d2RequireAllScopes — stores the required scopes array (all-match). */
export const D2_REQUIRE_ALL_SCOPES_KEY = Symbol.for("D2.d2RequireAllScopes");

/** State key for @d2RateLimitTier — stores the rate-limit tier string. */
export const D2_RATE_LIMIT_TIER_KEY = Symbol.for("D2.d2RateLimitTier");

/** State key for @d2Audience — stores the expected JWT audience string. */
export const D2_AUDIENCE_KEY = Symbol.for("D2.d2Audience");

/** State key for @d2ServedBy — stores the owning module/service string. */
export const D2_SERVED_BY_KEY = Symbol.for("D2.d2ServedBy");

/** State key for @d2GrpcMethod — stores the {@link GrpcMethodPayload}. */
export const D2_GRPC_METHOD_KEY = Symbol.for("D2.d2GrpcMethod");

/** State key for @d2Redact — stores `true` on the marked model property. */
export const D2_REDACT_KEY = Symbol.for("D2.d2Redact");

/**
 * Payload stored by @d2GrpcMethod: the gRPC service name, method name, and
 * streaming mode. `streaming` is one of `unary | serverStream | clientStream |
 * bidiStream`; defaults to `"unary"` when no arg is provided.
 */
export interface GrpcMethodPayload {
  readonly service: string;
  readonly method: string;
  readonly streaming: string;
}
