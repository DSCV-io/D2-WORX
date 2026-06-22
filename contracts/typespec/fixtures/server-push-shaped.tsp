// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Server-push-shaped fixture TypeSpec contract.
//
// Exercises the SSE dispatch emitter against two PURE @d2ServerPush ops — one on
// the "user" channel class, one on the "session" channel class — so both arms of
// the baked-in channel class are covered. Each op:
//
//   - is a @d2Command — a server-push delivers/publishes an event to a channel,
//     a message-publish SIDE EFFECT, which the binary CQRS rule classifies as a
//     Command (the verb is irrelevant).
//   - is PURE-push (only @d2ServerPush; no @route / @d2GrpcMethod / @d2InProcess;
//     not @d2Internal), so it is the CALLER of an event channel, not a request
//     server. It generates ONLY the dispatcher (a client stub) — no I<Op>Handler
//     (a caller never registers a handler) and no façade entry. The SSE gateway
//     is the server; it implements the one generic emit-sink seam once for every
//     event. Pure-push ops are therefore PARAM-LESS — a push is server-initiated,
//     with no request input.
//   - has an OUTPUT payload carrying a nested model field + a temporal field, so
//     the dispatcher/seam payload type proves walkModel nested + temporal
//     integration end-to-end (the C-1 temporal + nested-model support flows
//     through the dispatch contract byte-identically).
//
// The dispatcher's targetId is a runtime arg (the explicit recipient id), NOT a
// contract field. The event-type is the op name verbatim.
//
// NOT a real KC operation — a fixture mirroring server-push shapes for the
// purpose of exercising the dispatch emitter.

import "@d2/typespec-decorators";

using D2;

namespace D2.Fixtures;

/** A nested model carried in a push payload (proves walkModel nested integration). */
model OrderLine {
  sku: string;
  quantity: int32;
}

/** Output payload for the user-channel push: nested model + temporal field. */
model OrderShippedOutput {
  orderId: string;

  /** Temporal field — proves the C-1 utcDateTime mapping flows through the dispatch payload. */
  shippedAt: utcDateTime;

  /** Nested array-of-model field — proves walkModel nested integration end-to-end. */
  lines: OrderLine[];
}

/** Output payload for the session-channel push: a temporal field on the session channel too. */
model SessionExpiringOutput {
  sessionId: string;

  /** Temporal field on the session-channel payload. */
  expiresAt: utcDateTime;
}

/** user-channel pure-push — output payload is the event; event-type = "orderShipped". */
@d2Command
@d2ServedBy("PushFixtures")
@d2ServerPush("user")
op orderShipped(): OrderShippedOutput;

/** session-channel pure-push — exercises the Session channel class. */
@d2Command
@d2ServedBy("PushFixtures")
@d2ServerPush("session")
op sessionExpiring(): SessionExpiringOutput;
