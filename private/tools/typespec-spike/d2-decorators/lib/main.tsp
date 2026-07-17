// Spike-local TypeSpec decorator library — declares the @d2* vocabulary.
// These extern decs link to the $-prefixed JS functions exported from dist/index.js.
// The JS is registered by importing it here (mirrors @typespec/http's
// `import "../dist/src/tsp-index.js";` in its main.tsp).

import "../dist/tsp-index.js";

namespace D2;

using TypeSpec.Reflection;

/** Required OAuth 2.0 scope for an operation. */
extern dec d2Scope(target: Operation, scope: valueof string);

/** Rate-limit tier for a REST-bound operation. */
extern dec d2RateLimitTier(target: Operation, tier: valueof string);

/** Expected JWT audience claim. */
extern dec d2Audience(target: Operation, audience: valueof string);

/** Owning module/service for the operation's handler body. */
extern dec d2ServedBy(target: Operation, owner: valueof string);

/** Dual-binding marker: also binds this op to a gRPC service+method. */
extern dec d2GrpcMethod(target: Operation, service: valueof string, method: valueof string);

/** Marks a model property as PII to be redacted in structured logs. */
extern dec d2Redact(target: ModelProperty);

/**
 * Spike-only: marks an operation as a server-initiated push whose terminal hop
 * delivers to `pushTarget` (e.g. "user"). Drives generation of the multi-hop
 * gRPC-receiver -> SSE-emit forwarding pipeline.
 */
extern dec d2ServerPush(target: Operation, pushTarget: valueof string);

/**
 * Spike-only (A4 location transparency): marks a push op's inbound leg as an
 * in-process leaf call rather than a remote gRPC hop. Changing ONLY this knob on
 * the spec re-targets the transport; the downstream SSE hop + payload DTO are
 * unchanged.
 */
extern dec d2InProcess(target: Operation);

/**
 * Spike-only (Spike C — idempotency): marks a mutating operation as idempotent.
 * Drives generation of a dedupe GATE that wraps the handler — a duplicate
 * request with the same idempotency key returns the first result WITHOUT
 * re-invoking the handler body.
 *
 * @param keySource how the idempotency key is obtained:
 *   - `"header"`  — read from the `Idempotency-Key` request header value.
 *   - `"derived"` — a stable hash of the named input `fields` (content key).
 * @param ttlSeconds how long a stored result is replayable, in seconds; after
 *   it expires a replay re-invokes the handler.
 * @param fields when `keySource == "derived"`, the input-model property names
 *   hashed into the key (in declaration order). Ignored for `"header"`.
 */
extern dec d2Idempotent(
  target: Operation,
  keySource: valueof string,
  ttlSeconds: valueof int32,
  ...fields: valueof string[]
);

/**
 * Spike-only (Spike B — resilience): wraps an operation's outbound call in a
 * resilience pipeline composed from the NAMED `profiles` in DECLARATION ORDER.
 *
 * `@d2Resilience("retry-fast", "breaker-standard")` composes retry INSIDE the
 * breaker (first-named is innermost) around the op's outbound dependency call;
 * flipping the order flips the generated composition. Each profile resolves
 * from a single tuning registry (the emitter owns it) so there are NO per-op
 * magic numbers at the call site — the tuning lives in the profile. An UNKNOWN
 * profile name FAILS THE BUILD (the emitter reports an `error` diagnostic so
 * `tsp compile` exits non-zero) rather than surfacing at runtime.
 *
 * @param profiles ordered resilience-profile names to compose. First-named is
 *   the INNERMOST policy (closest to the outbound call); last-named is the
 *   OUTERMOST (the call site enters the last profile first and the first profile
 *   last, wrapping the raw outbound call). Stored in declaration order.
 */
extern dec d2Resilience(target: Operation, ...profiles: valueof string[]);
