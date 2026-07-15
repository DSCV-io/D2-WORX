// ============================================================================
// D2-WORX TypeSpec spike C — idempotency gate codegen (ADR-0021 de-risk).
//
// ONE mutating op, decorated @d2Idempotent, describes an idempotent command.
// The emitter reads the decorator back and GENERATES a dedupe GATE that wraps
// the handler:
//
//   request --> [generated gate] --(cache miss)--> handler --> store result
//                       |
//                       +--(cache hit, same key)--> return stored result
//                                                   WITHOUT calling the handler
//
// You wire ONE decorator on the op; the key extraction + dedupe gate + store
// seam are all generated. Zero hand-written idempotency plumbing.
//
// This is the command analogue of the SC1 `sign` op and the Spike A push op:
// SC1 proved one op -> dual binding; A proved one spec -> a multi-hop pipeline;
// C proves one decorator -> a generated idempotency gate around the handler.
// The handler body + the store impl are out of scope (the spike supplies a
// counting handler + an in-memory store fake); the spike is about codegen
// completeness of the GATE, not the business logic or the distributed cache.
// ============================================================================

import "@dcsv-io/d2-typespec-decorators";

using D2;

// ============================================================================
// Input — the command payload. `idempotencyKey` carries the header-sourced key
// (e.g. an `Idempotency-Key` request header) on the wire; the generated gate
// reads it to dedupe. The remaining fields are the command's actual inputs.
// ============================================================================

model SubmitOrderInput {
  /** Header-sourced idempotency key (the `Idempotency-Key` request header). */
  idempotencyKey: string;

  /** Opaque customer id placing the order. */
  customerId: string;

  /** Order total in minor units (cents). The thing we must not double-charge. */
  amountCents: int32;
}

// ============================================================================
// Output — the command result. The generated gate stores THIS under the key on
// first execution and replays it verbatim on a duplicate request.
// ============================================================================

model SubmitOrderOutput {
  /** Server-assigned order id (proves the handler ran exactly once per key). */
  orderId: string;

  /** Echoed total, for the test to assert the replayed result is identical. */
  amountCents: int32;
}

// ============================================================================
// The idempotent command.
//
//   @d2Idempotent("header", 300)
//     -> keySource "header": the gate reads the `Idempotency-Key` value
//        (surfaced on the input as `idempotencyKey`).
//     -> ttlSeconds 300: a stored result is replayable for 5 minutes; after
//        that a replay re-invokes the handler.
//
// A `"derived"` variant (key = hash of named fields) is generated from
// idempotent-derived.tsp — the DELTA is the decorator args only.
// ============================================================================

@d2Idempotent("header", 300)
op submitOrder(input: SubmitOrderInput): SubmitOrderOutput;
