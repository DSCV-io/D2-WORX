// ============================================================================
// D2-WORX TypeSpec spike B — UNKNOWN-PROFILE variant (the B3 build-failure proof).
//
// IDENTICAL to resilience.tsp EXCEPT one named profile does NOT exist in the
// registry. Compiling this spec MUST FAIL `tsp compile`: the emitter resolves
// each @d2Resilience name against the registry and, on an unknown name, reports
// an `error`-severity diagnostic (@dcsv-io/d2-spike-emitter/unknown-resilience-profile)
// which makes the compile exit non-zero. The failure is COMPILE-TIME (a
// reported diagnostic), NOT a runtime error — an unknown profile can never reach
// a running service.
//
// Expected: `npx tsp compile resilience-unknown.tsp ...` fails with the
// unknown-resilience-profile diagnostic naming "retry-WARP-SPEED". No .NET
// artifacts are emitted for this spec (the pipeline is only emitted when every
// profile resolves).
// ============================================================================

import "@dcsv-io/d2-typespec-decorators";

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

// "retry-WARP-SPEED" is NOT in the registry (known: retry-fast, retry-standard,
// breaker-standard) -> the emitter reports an error diagnostic -> tsp compile FAILS.
@d2Resilience("retry-WARP-SPEED", "breaker-standard")
op submitPayment(input: SubmitPaymentInput): SubmitPaymentOutput;
