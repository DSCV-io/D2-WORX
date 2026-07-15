// ============================================================================
// D2-WORX TypeSpec spike A — A4 LOCATION TRANSPARENCY variant.
//
// IDENTICAL to push.tsp EXCEPT for one added decorator: @d2InProcess. That one
// spec knob re-targets the op's INBOUND leg from a remote gRPC hop to an
// in-process leaf call. The emitter then generates an in-process invoker
// (Spike.InProc) instead of a gRPC receiver — same downstream SSE-emit hop,
// same payload shape. Proves the transport leg is spec-selected: change ONLY
// the spec, regenerate, it still compiles.
//
// (Kept as a separate file purely so both transports can be generated in one
// spike run; the DELTA vs push.tsp is the single `@d2InProcess` line.)
// ============================================================================

import "@dcsv-io/d2-typespec-decorators";

using D2;

model NotificationCreated {
  /** Opaque notification id (ULID/GUID string on the wire). */
  id: string;

  /** Human-readable notification body. */
  message: string;
}

@d2ServedBy("Edge")
@d2GrpcMethod("Push", "PushNotificationCreated")
@d2ServerPush("user")
@d2InProcess // <-- THE ONLY DELTA vs push.tsp: re-targets the inbound leg in-process.
op pushNotificationCreated(notification: NotificationCreated): PushAck;

model PushAck {
  /** True once the payload was forwarded to the SSE emit binding. */
  accepted: boolean;
}
