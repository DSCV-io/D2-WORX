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

## Publish/encrypt fusion

Publishing and encryption are **structurally fused** — the TS twin of .NET's
spec-driven composer + DI. `createPublisher({ crypto })` binds a compile-time
type witness: `publish(key, message)` accepts only a message whose encryption
domain is `plaintext` or was wired into `crypto`. Publishing to an unwired
encrypted domain is a **compile error**, and there is no raw-bytes publish
overload — the composer for a domain is the only path to the socket for that
domain. A sealed domain's slot only accepts an `IPayloadSealer`; a symmetric
slot only an `IPayloadCrypto` (mode-branded by the generated
`EncryptionDomainModes` literals). The compile witness is pinned by
`tests/publisher-type-witness.compile.ts` (`@ts-expect-error` proofs under the
type-check gate).

A **runtime default-deny** second lock (`composeBody`) covers dynamic / fixture
paths: the descriptor's domain mode is consulted unconditionally, and a missing
composer for an encrypted domain, or an unknown domain, fails loud before any
socket write. The body is composed once (a resend reuses the exact bytes — no
re-encrypt under a fresh nonce). The KC-backed composer instances are wired by
the host via `@d2/key-custodian-client`'s `createSealedCryptoViaKeyCustodian`.

On the consume side, `CryptoBodyOpener` (sealed / symmetric) plugs the real
crypto into the body-decompose seam: a wrong-version frame, a plaintext body on
an encrypted domain, tampering, or an unknown kid all DLQ with `DECRYPT_FAILURE`
(never a silent mis-decode); `assertOpenerMatchesDomain` is the consumer-side
subscriber-vs-opener cross-check.

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
