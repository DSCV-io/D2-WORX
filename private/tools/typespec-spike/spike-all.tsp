// ============================================================================
// D2-WORX TypeSpec spike — COMBINED verdict spec (Spike A + Spike B + Spike C in
// one compile). Imports the push pipeline op (@d2ServerPush, drives the A
// block), the resilience op (@d2Resilience, drives the B block), and the
// idempotent command op (@d2Idempotent, drives the C block) so the emitter
// populates the `a`, `b`, and `c` blocks of spike-verdict.json in a single pass
// — i.e. all three spikes' scores coexist.
//
// The .NET artifacts for each spike are unaffected (the emitter writes them
// into their own Spike.*/Generated folders); this file only governs which ops
// are in scope for the shared verdict so all three spikes' scores coexist.
//
// Run through the spike-emitter (NOT the default openapi3 config), e.g.
//   npx tsp compile spike-all.tsp --config resilience.tspconfig.yaml
// ============================================================================

import "@dcsv-io/d2-typespec-decorators";

using D2;

// ---- Spike A: the multi-hop push op (same shape as push.tsp) ---------------

model NotificationCreated {
  /** Opaque notification id (ULID/GUID string on the wire). */
  id: string;

  /** Human-readable notification body. */
  message: string;
}

model PushAck {
  /** True once the payload was forwarded to the SSE emit binding. */
  accepted: boolean;
}

@d2ServedBy("Edge")
@d2GrpcMethod("Push", "PushNotificationCreated")
@d2ServerPush("user")
op pushNotificationCreated(notification: NotificationCreated): PushAck;

// ---- Spike C: the idempotent command op (same shape as idempotent.tsp) -----

model SubmitOrderInput {
  /** Header-sourced idempotency key (the `Idempotency-Key` request header). */
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

@d2Idempotent("header", 300)
op submitOrder(input: SubmitOrderInput): SubmitOrderOutput;

// ---- Spike B: the resilient op (same shape as resilience.tsp) --------------

model SubmitPaymentInput {
  /** Opaque payment id (the thing we call the downstream processor about). */
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

@d2Resilience("retry-fast", "breaker-standard")
op submitPayment(input: SubmitPaymentInput): SubmitPaymentOutput;
