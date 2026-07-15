// ============================================================================
// D2-WORX TypeSpec spike B — resilience pipeline codegen (ADR-0021 de-risk).
//
// ONE op, decorated @d2Resilience with an ORDERED list of profile names. The
// emitter reads the list back and GENERATES a resilience pipeline that resolves
// each profile from a single registry and composes them in DECLARATION ORDER
// around the op's outbound call:
//
//   @d2Resilience("retry-fast", "breaker-standard")
//     -> breaker(retry(outboundCall))  == retry-INSIDE-breaker
//        (first-named = innermost; last-named = outermost)
//
//   request --> [breaker] --> [retry] --> outbound dependency
//                  |             |
//                  |             +-- transient fail within budget -> retried -> success
//                  +-- sustained failures past threshold -> OPEN -> fast-fail (typed)
//
// You wire ONE decorator; the profile resolution + the ordered composition +
// the policy-factory seam are all generated. Zero hand-written pipeline wiring
// per op, and NO magic numbers at the call site — the tuning lives in the
// profile registry (the emitter reads it; the generated pipeline bakes the
// resolved tunables in).
//
// This is the resilience analogue of SC1 / Spike A / Spike C:
//   SC1 proved one op -> dual binding;
//   A   proved one spec -> a multi-hop pipeline;
//   C   proved one decorator -> a generated idempotency gate;
//   B   proves one decorator -> a generated resilience pipeline whose
//       composition ORDER is the declared profile order.
//
// The retry + breaker PRIMITIVES + the controllable outbound dependency are out
// of scope (the spike supplies stand-in primitives + a fake dependency); the
// spike is about codegen completeness of the PIPELINE, not the exact
// D2.Shared.Resilience integration (prod swaps the stand-in for it).
// ============================================================================

import "@d2/typespec-decorators";

using D2;

// ============================================================================
// Input — the command payload for an outbound-call-protected operation.
// ============================================================================

model SubmitPaymentInput {
  /** Opaque payment id (the thing we call the downstream processor about). */
  paymentId: string;

  /** Amount in minor units (cents). */
  amountCents: int32;
}

// ============================================================================
// Output — the command result the outbound dependency returns.
// ============================================================================

model SubmitPaymentOutput {
  /** Downstream-assigned confirmation id. */
  confirmationId: string;

  /** Echoed amount. */
  amountCents: int32;
}

// ============================================================================
// The resilient command.
//
//   @d2Resilience("retry-fast", "breaker-standard")
//     -> the generated pipeline composes the named profiles in declaration
//        order: retry-fast is INNERMOST (closest to the outbound call),
//        breaker-standard is OUTERMOST. So the effective behavior is
//        retry-INSIDE-breaker: transient faults are retried; if failures are
//        SUSTAINED past the breaker threshold the breaker OPENS and fast-fails.
//
// An order-FLIPPED variant (breaker-inside-retry) is generated from
// resilience-flip.tsp — the DELTA is the decorator arg ORDER only, and the
// generated composition flips to match.
// ============================================================================

@d2Resilience("retry-fast", "breaker-standard")
op submitPayment(input: SubmitPaymentInput): SubmitPaymentOutput;
