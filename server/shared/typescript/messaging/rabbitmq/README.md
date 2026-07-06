<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/messaging-rabbitmq

> Parent: [`server/shared/typescript/`](../../README.md)

The TypeScript **CONSUMER** runtime twin of the .NET
[`D2.Shared.Messaging.RabbitMq`](../../../../dotnet/messaging/rabbitmq/README.md)
consumer path. A service author building a Node service that consumes messages
a .NET service publishes uses this package: a service-agnostic RabbitMQ
subscriber with the same topology, same DLQ convention, and same cross-hop
context and trace propagation — so a Node consumer and a .NET consumer are
interchangeable on the wire.

Built on [`rabbitmq-client`](https://www.npmjs.com/package/rabbitmq-client)
(zero-dep, auto-reconnecting), pinned exact.

---

## Consumer-only by design

There is deliberately **no publisher API**. In .NET, publishing to an
encrypted domain and encrypting the payload are one inseparable composed path,
so "plaintext body on an encrypted domain" is unrepresentable. A TS publisher
without that structural fusion would break the guarantee the .NET side is built
on, so this package exposes no publisher: composing an encrypted publish path is
out of scope here and lives on the .NET side. The DLQ republish inside the
consume pipeline is an internal detail of dead-lettering — not a general publish
surface.

---

## Quick start

```ts
import {
  createConnection,
  subscribe,
  QueuePattern,
  InMemoryMessageIdempotencyStore,
} from "@d2/messaging-rabbitmq";

const connection = createConnection({
  connectionUri: process.env.D2_RABBITMQ_URI!, // secret — never logged whole
  clientProvidedName: "audit-svc",
});

const sub = subscribe({
  connection,
  logger,
  store: new InMemoryMessageIdempotencyStore(),
  descriptor: {
    queueName: "audit.key-rotated",
    exchange: "d2.security.key-rotated",
    exchangeType: "fanout",
    pattern: QueuePattern.DurableShared,
    routingKeyBinding: "",
    prefetch: 8,
    idempotency: true,
    nackedBy: "audit-svc",
  },
  handler: (message, ctx) => {
    // `message` = the decoded body; `ctx.propagated` = the per-message
    // operational context (requestId / fingerprints / callPath — never identity).
    return doWork(message);
  },
});

await sub.ready;
```

The handler returns a `D2Result`. A failed result dead-letters the message
(`HANDLER_RESULT_FAILURE`); a thrown error dead-letters it
(`HANDLER_EXCEPTION`). Success acks — after writing the idempotency mark.

---

## What the runtime does (per delivery)

1. **Trace linkage** — parses the producer's `traceparent` and starts a
   `Consumer`-kind span `receive {queue}` whose parent is the publish span, so
   the trace assembles across runtimes. A missing / malformed header starts a
   root span (never a reject). Span tags come from the spec-emitted
   `MessagingActivityTags` closed set (same values the .NET consumer emits).
2. **Per-message context** — decodes the `x-d2-context` header
   (base64url-of-JSON, exactly what the .NET `PropagatedContextSerializer.Encode`
   and the gRPC interceptor produce) via the shared
   `@d2/request-context-abstractions` serializer and applies the operational
   subset (request id / path / fingerprints / WhoIs hash / locale-tier fields /
   `callPath`) onto a fresh per-message context. **Identity is never taken from
   the wire**, and **`RequestOrigin` is never wire-reconstructed** — those
   slots do not exist on the applied shape (§9.41). A malformed header is
   fail-safe (empty context, message still processed).
3. **Idempotency** (opt-in) — a precise 5-point contract mirroring .NET: a
   seen `message-id` is **ack-and-skipped, never dead-lettered**; a read-path
   store outage fails **open** (process anyway); the mark is written only on
   the success path **before** the ack; a mark-write failure NACKs to the DLQ
   (never leave the dedup window unguarded); failure paths never mark.
4. **Body decompose** — an injectable opener seam. The default handles
   plaintext (raw UTF-8 JSON) and **fail-louds** any body whose first byte is a
   known encryption-frame version (1 or 2) → `DECRYPT_FAILURE` → DLQ, never a
   silent mis-parse.
5. **Dead-lettering** — on failure the original body is republished to
   `{queue}.dlx` with an `x-d2-failure-reason` header (`DlqFailureMetadata`:
   `cause` / `errorCode` / `detail` / `attemptCount` / `traceId` / `nackedBy`,
   PII-safe) then the original is acked; a republish failure falls back to
   NACK-no-requeue. Producer headers (`traceparent`, `x-d2-context`, ...) ride
   forward on the DLQ copy.

---

## Topology

`subscribe` declares the exact .NET topology (see
[`DlqNaming`](../../../../dotnet/messaging/rabbitmq/Topology/DlqNaming.cs)):

- primary queue with `x-dead-letter-exchange = {queue}.dlx`
- `{queue}.dlx` fanout DLX → `{queue}.dlq` durable DLQ
- optional retry tiers (`{queue}.retry.{i}` + `{queue}.retry.return`)

Queue patterns: `CompetingConsumer`, `DurableShared`,
`FanoutExclusiveAutoDelete` (auto-suffixed per process to avoid the exclusive
queue lock).

---

## Testing

- **Unit** (`pnpm test`) — the full delivery matrix against injected seams;
  100% `src/**` coverage.
- **Integration** (`pnpm test:integration`) — a Testcontainer RabbitMQ replaying
  **real .NET-emitted golden messages** (emitted by
  `D2.Shared.Tests` `Integration/ContractFixtures/MqGoldenMessageFixtureEmitter`
  into `contract-tests/fixtures/mq-messages-golden/`): wire-contract consume,
  encrypted-frame → DLQ, handler-failure DLQ metadata, idempotency dedup, and
  competing consumers.
- **Descriptor mirror** — `MqMessages` / `MqMessagesRegistry` (in
  `@d2/messaging-abstractions`, emitted by `tools/ts-codegen/src/mq-messages-emit.ts`)
  is asserted byte-equal to the .NET `MqMessagesRegistry` by
  `contract-tests/tests/mq-messages.parity.test.ts`.

---

## Dependencies

- `rabbitmq-client` — the only vendor dep (transport).
- `@d2/headers-amqp` — AMQP header wire-value constants.
- `@d2/messaging-abstractions` — `DlqFailureMetadataFields` / `DlqFailureCauses`
  + the `MqMessages` descriptor mirror.
- `@d2/request-context-abstractions` — `PropagatedContextSerializer` +
  `IPropagatedContext`.
- `@d2/encryption-abstractions` — frame-version constants (fail-loud guard).
- `@d2/telemetry` — the `MessagingActivityTags` span-tag catalog.
- `@d2/result`, `@d2/logging`, `@d2/utilities` — cross-cutting.
