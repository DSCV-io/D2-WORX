// ============================================================================
// D2-WORX TypeSpec spike C — idempotency gate, DERIVED key-source variant.
//
// IDENTICAL to idempotent.tsp EXCEPT the decorator args: the key is a stable
// hash of the named input fields (`customerId` + `amountCents`) rather than a
// header value. That one spec change re-targets key EXTRACTION — the gate, the
// store seam, and the replay semantics are otherwise unchanged. Proves the
// key-source is spec-selected: change ONLY the decorator, regenerate, the gate
// still compiles and still dedupes (now content-addressably).
//
// (Kept as a separate file purely so both key sources can be generated in one
// spike run; the DELTA vs idempotent.tsp is the single decorator line.)
// ============================================================================

import "@d2/typespec-decorators";

using D2;

model SubmitOrderInput {
  /** Header-sourced key — UNUSED in the derived variant (kept for shape parity). */
  idempotencyKey: string;

  /** Opaque customer id placing the order. */
  customerId: string;

  /** Order total in minor units (cents). The thing we must not double-charge. */
  amountCents: int32;
}

model SubmitOrderOutput {
  /** Server-assigned order id (proves the handler ran exactly once per key). */
  orderId: string;

  /** Echoed total, for the test to assert the replayed result is identical. */
  amountCents: int32;
}

// keySource "derived": key = stable hash of (customerId, amountCents). THE ONLY
// DELTA vs idempotent.tsp is these decorator args.
@d2Idempotent("derived", 300, "customerId", "amountCents")
op submitOrder(input: SubmitOrderInput): SubmitOrderOutput;
