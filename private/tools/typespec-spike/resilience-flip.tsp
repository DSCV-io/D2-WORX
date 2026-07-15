// ============================================================================
// D2-WORX TypeSpec spike B — resilience pipeline, ORDER-FLIPPED variant.
//
// IDENTICAL to resilience.tsp EXCEPT the decorator arg ORDER is reversed:
//
//   resilience.tsp       @d2Resilience("retry-fast", "breaker-standard")
//                          -> breaker(retry(call))  == retry-INSIDE-breaker
//
//   resilience-flip.tsp  @d2Resilience("breaker-standard", "retry-fast")
//                          -> retry(breaker(call))  == breaker-INSIDE-retry
//
// That one DELTA (the profile ORDER) must flip the GENERATED composition: the
// emitted pipeline nests the policies in the new declaration order, and the
// generated `ComposedOrder` constant reflects it. Proves the composition order
// is spec-driven, not hard-coded (B1).
//
// (Kept as a separate file purely so both orders can be generated/compiled in
// one spike run; the DELTA vs resilience.tsp is the single decorator line.)
// ============================================================================

import "@d2/typespec-decorators";

using D2;

model SubmitPaymentInput {
  /** Opaque payment id. */
  paymentId: string;

  /** Amount in minor units (cents). */
  amountCents: int32;
}

model SubmitPaymentOutput {
  /** Downstream-assigned confirmation id. */
  confirmationId: string;

  /** Echoed amount. */
  amountCents: int32;
}

// FLIPPED order: breaker OUTERMOST -> INNERMOST, retry INNERMOST -> OUTERMOST.
// THE ONLY DELTA vs resilience.tsp is these decorator arg positions.
@d2Resilience("breaker-standard", "retry-fast")
op submitPayment(input: SubmitPaymentInput): SubmitPaymentOutput;
