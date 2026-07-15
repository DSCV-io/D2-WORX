// ============================================================================
// D2-WORX TypeSpec spike A — multi-hop server-push pipeline (ADR-0021 de-risk).
//
// ONE op spec describes the whole push pipeline:
//
//     microservice --(gRPC)--> Edge --(forward)--> SSE module --(--> client)
//
// The op is decorated @d2ServerPush("user") (terminal hop = a user's SSE
// stream) + @d2GrpcMethod (the inbound gRPC leg) + @d2ServedBy (the owning
// module whose generated receiver runs inside Edge). The emitter reads these
// back and generates the wiring for EVERY hop, so the .NET pipeline compiles
// and runs with ZERO hand-written controllers / handlers.
//
// This is the push analogue of the SC1 `sign` op: SC1 proved one op -> dual
// REST+gRPC binding (one hop); this proves one spec -> a fully-generated
// multi-hop push pipeline. The terminal "send to client" is a STUBBED sink;
// the spike is about codegen completeness of the pipeline wiring, not the SSE
// runtime.
// ============================================================================

import "@dcsv-io/d2-typespec-decorators";

using D2;

// ============================================================================
// Payload — the single message that travels every hop unchanged.
//   Kept deliberately small; the A3 claim is that the ONE generated DTO for
//   this model is the type at the gRPC message, the forward arg, AND the SSE
//   emit arg.
// ============================================================================

model NotificationCreated {
  /** Opaque notification id (ULID/GUID string on the wire). */
  id: string;

  /** Human-readable notification body. */
  message: string;
}

// ============================================================================
// The push operation.
//
//   @d2ServedBy("Edge")            -> the generated gRPC receiver is hosted in
//                                     the Edge host (composition root).
//   @d2GrpcMethod("Push", "PushNotificationCreated")
//                                  -> inbound leg: gRPC service `Push`, method
//                                     `PushNotificationCreated`. Drives the
//                                     hand-rolled .proto + Grpc.Tools codegen.
//   @d2ServerPush("user")          -> terminal hop targets the user's stream;
//                                     drives the Edge-receiver -> SSE-emit
//                                     forwarding chain + the SSE EmitAsync.
//
// Shape: the microservice emits `notification`; the call returns a minimal ack
// (gRPC unary RPCs require a response message). No REST/@route binding here —
// a server-push op is gRPC-inbound only, which is itself a knob the emitter
// honours (it does NOT emit a controller for a push op).
// ============================================================================

@d2ServedBy("Edge")
@d2GrpcMethod("Push", "PushNotificationCreated")
@d2ServerPush("user")
op pushNotificationCreated(notification: NotificationCreated): PushAck;

// ============================================================================
// PushAck — the unary RPC's response message (the emitter maps this to the
// generated proto response type; the receiver returns Accepted=true once the
// payload has been handed to the SSE emit binding).
// ============================================================================

model PushAck {
  /** True once the payload was forwarded to the SSE emit binding. */
  accepted: boolean;
}
