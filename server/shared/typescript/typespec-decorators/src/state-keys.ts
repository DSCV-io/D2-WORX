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

/**
 * State key for @d2Redact — stores the RedactReason member-name string on the
 * marked model property.
 */
export const D2_REDACT_KEY = Symbol.for("D2.d2Redact");

/** State key for @d2ServerPush — stores the push channel class string. */
export const D2_SERVER_PUSH_KEY = Symbol.for("D2.d2ServerPush");

/** State key for @d2Idempotent — stores the {@link IdempotentPayload}. */
export const D2_IDEMPOTENT_KEY = Symbol.for("D2.d2Idempotent");

/** State key for @d2Resilience — stores the raw pipeline-expression string. */
export const D2_RESILIENCE_KEY = Symbol.for("D2.d2Resilience");

/**
 * State key for @d2Resilience's `retryWhen` predicate — stores the raw
 * result-predicate string (opts a business result INTO the retry decision).
 * The emitter re-parses it via the exported `parseResultPredicate`.
 */
export const D2_RESILIENCE_RETRY_WHEN_KEY = Symbol.for(
  "D2.d2Resilience.retryWhen",
);

/**
 * State key for @d2Resilience's `failWhen` predicate — stores the raw
 * result-predicate string (forces a terminal fail, suppressing retry).
 * The emitter re-parses it via the exported `parseResultPredicate`.
 */
export const D2_RESILIENCE_FAIL_WHEN_KEY = Symbol.for(
  "D2.d2Resilience.failWhen",
);

/** State key for @d2Csrf — stores the CSRF posture override string. */
export const D2_CSRF_KEY = Symbol.for("D2.d2Csrf");

/** State key for @d2Harmless — stores `true` on the marked operation. */
export const D2_HARMLESS_KEY = Symbol.for("D2.d2Harmless");

/** State key for @d2InProcess — stores `true` on the marked operation. */
export const D2_IN_PROCESS_KEY = Symbol.for("D2.d2InProcess");

/** State key for @d2Command — stores `true` on the marked operation (mutating CQRS op). */
export const D2_COMMAND_KEY = Symbol.for("D2.d2Command");

/** State key for @d2Query — stores `true` on the marked operation (read-only CQRS op). */
export const D2_QUERY_KEY = Symbol.for("D2.d2Query");

/**
 * State key for @d2Internal — stores `true` on the marked operation (no
 * cross-boundary surface).
 */
export const D2_INTERNAL_KEY = Symbol.for("D2.d2Internal");

/**
 * State key for @d2Field — stores the author-pinned proto field number (integer ≥ 1)
 * on the marked model property. Emitters read back:
 * program.stateMap(D2_FIELD_KEY).get(prop) → number.
 */
export const D2_FIELD_KEY = Symbol.for("D2.d2Field");

/**
 * State key for @d2Reserved — stores the author-declared reserved list
 * ({ numbers: number[], names: string[] }) on the marked model. Emitters read back:
 * program.stateMap(D2_RESERVED_KEY).get(model) → ReservedPayload.
 */
export const D2_RESERVED_KEY = Symbol.for("D2.d2Reserved");

/**
 * Payload stored by @d2Reserved: the ordered list of reserved field numbers
 * (removed slots) and reserved field names (removed or renamed fields).
 * Both lists are stored in authoring order; the proto emitter sorts, deduplicates,
 * and range-collapses numbers before emission.
 */
export interface ReservedPayload {
  readonly numbers: number[];
  readonly names: string[];
}

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

/**
 * Payload stored by @d2Idempotent: the key-extraction source, the replay
 * window in seconds, and the ordered list of input-property names hashed into
 * the derived key. `fields` is empty when `keySource` is `"header"`.
 */
export interface IdempotentPayload {
  readonly keySource: string;
  readonly ttlSeconds: number;
  readonly fields: string[];
}
