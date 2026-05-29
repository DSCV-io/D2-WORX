<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Messaging.RabbitMq

> Parent: [`server/shared/dotnet/`](../../README.md)

Default `RabbitMQ.Client 7.x` implementation of the
[`D2.Shared.Messaging.Abstractions`](../abstractions/README.md)
contract. Owns connection lifecycle, channel pooling with idle-eviction,
topology declaration (exchanges + DLX + DLQ + optional retry tiers),
publishing with publisher-confirms + built-in transient retry, payload
encryption via `D2.Shared.Encryption`, full W3C trace-context propagation,
DLQ republish-with-failure-header, and the cross-hop operational-context
propagation (`x-d2-context`).

The wire body is just the serialized message — no envelope wrapper. The
descriptor (`MqMessageDescriptor`, codegen-emitted from
`contracts/mq-messages/mq-messages.spec.json`) drives encryption,
exchange, and default routing key.

---

## Table of contents

- [Cross-hop propagation](#cross-hop-propagation)
- [Quick start](#quick-start)
- [End-to-end architecture](#end-to-end-architecture)
- [Public surface](#public-surface)
- [Wire format](#wire-format)
- [AMQP header contract](#amqp-header-contract)
- [Publisher path](#publisher-path)
- [Consumer path](#consumer-path)
- [DLX + DLQ convention](#dlx--dlq-convention)
- [Optional retry tier topology](#optional-retry-tier-topology)
- [Encryption posture](#encryption-posture)
- [Defaults that apply automatically](#defaults-that-apply-automatically)
- [Architecture](#architecture)
- [Operational anti-patterns](#operational-anti-patterns)
- [Dependencies](#dependencies)
- [References](#references)

---

## Cross-hop propagation

- **Trace correlation** — full W3C `traceparent` (`00-{traceId}-{spanId}-{flags}`)
  - optional `tracestate` AMQP headers. The consumer parses via
    `ActivityContext.TryParse` and starts a `Consumer`-kind span whose parent
    is the publish span — cross-hop trace assembly works in any OTel backend.
- **Operational subset** — `RequestId` / `RequestPath` / fingerprints /
  `WhoIsHashId` ride in the `x-d2-context` AMQP header (base64url-of-JSON
  encoded `PropagatedContext`; same shape on every transport).
  `PropagatedContextSerializer.TryDecode` enforces both a wire-level cap
  (`MAX_HEADER_LENGTH = 2 KiB`) AND per-field length caps (RequestPath ≤
  2048, RequestId ≤ 256, fingerprints ≤ 512, WhoIsHashId ≤ 128). A forged
  header that fits under the wire cap but contains an oversized single
  field is dropped wholesale — propagation is opportunistic, never
  required, so a partial / sanitized context is wrong; a null context is
  right.
- **Identity (UserId / OrgId / Scopes / ActorChain)** — NOT propagated by
  messaging. Each sync hop re-validates a JWT and rebuilds identity from
  scratch; for async events the consumer-side handler doesn't have one and
  shouldn't claim caller identity. Anything the consumer truly needs about
  business identity goes in the typed message body itself.

The lib's surface is intentionally small — almost everything is internal.
Consumers register the stack with one DI call (`AddD2MessagingRabbitMq`),
publish via `IMessageBus`, and register subscribers via
`AddD2SubscribersFromAssembly` (in the abstractions package). This lib's
hosted services pick those up and declare topology accordingly.

---

## Quick start

```csharp
services
    .AddD2EncryptionFor(EncryptionDomains.AUDIT, factory: ...)
    .AddD2MessagingRabbitMq(
        configureConnection: o =>
        {
            o.ConnectionUri = "amqps://audit-svc:" + secrets.RabbitMqPassword
                + "@rabbitmq.internal:5671/d2";
            o.ClientProvidedName = "audit-svc";
        });

services.AddD2Handler();
services.AddD2SubscribersFromAssembly(typeof(MyConsumerAssembly).Assembly);
```

```csharp
public sealed class PublishWidgetCreated(IMessageBus bus)
{
    public ValueTask<D2Result> RunAsync(WidgetCreated evt, CancellationToken ct)
        => bus.PublishAsync(evt, ct: ct);
}
```

The message type carries the spec link:

```csharp
[MqPub(MqMessages.WidgetCreated)]
public sealed class WidgetCreated { /* ... */ }
```

A type without `[MqPub]` throws `InvalidOperationException` from the
publisher's resolver — every publishable type must have a deliberate spec
entry under `contracts/mq-messages/`.

Handlers carry `[MqSub]`:

```csharp
[MqSub(MqSubscriptions.WidgetCreatedAuditing)]
public sealed class WidgetCreatedAuditingHandler
    : BaseHandler<WidgetCreatedAuditingHandler, WidgetCreated, Unit>
{
    /* ExecuteAsync */
}
```

---

## End-to-end architecture

```
┌─────────────────────────┐         ┌─────────────────────────┐
│  Producer service       │         │  Consumer service       │
│                         │         │                         │
│ [MqPub(MqMessages.X)]   │         │ class MyHandler         │
│ class FooEvent { ... }  │         │   : BaseHandler<...>    │
│                         │         │ [MqSub(...)]            │
│         │ IMessageBus   │         │      ▲                  │
│         ▼               │         │      │ dispatch         │
│  RabbitMqMessageBus     │         │  SubscriberChannel      │
│         │               │         │      │ BasicConsume     │
└─────────┼───────────────┘         └──────┼──────────────────┘
          │ publish via channel pool       │ dedicated channel
          ▼                                │
   ┌──────────────────────────────────────────────┐
   │  RabbitMQ broker                             │
   │   exchange: descriptor.Exchange              │
   │     │                                        │
   │     ▼                                        │
   │   queue: descriptor.QueueName                │
   │     ├── DLX → DLQ                            │
   │     └── (optional) tier exchanges + queues   │
   └──────────────────────────────────────────────┘
```

**Spec → codegen → registry → runtime.**

- Two JSON spec files in `contracts/`:
  - `mq-messages/mq-messages.spec.json` — every publishable message type
  - `mq-subscriptions/mq-subscriptions.spec.json` — every subscription contract
- The `D2.Shared.Messaging.SourceGen` Roslyn analyzer reads both at build time
  and emits constants + immutable runtime registries into the
  `D2.Shared.Messaging.Abstractions` assembly:
  - `MqMessages.AuthKeyRotated` (string constant) +
    `MqMessagesRegistry.ByConstant`
    (`Dictionary<string, MqMessageDescriptor>`)
  - `MqSubscriptions.KeyringRefresh` (string constant) +
    `MqSubscriptionsRegistry.ByConstant`
- The producer marks each message class `[MqPub(MqMessages.X)]`; the publisher
  resolves `Type → MqMessageDescriptor` via the cached `MessageWireResolver`
  and gets exchange / encryption / routing-key from the descriptor.
- The consumer marks each handler class `[MqSub(MqSubscriptions.X)]`;
  `AddD2SubscribersFromAssembly(Assembly)` scans, validates the handler's
  `BaseHandler<TSelf, TIn, Unit>` generic argument matches the spec's
  `messageType`, and registers an `ISubscriberRegistration`.

Full spec format + diagnostic catalog for the source-gen lives in
[`messaging/source-gen/README.md`](../source-gen/README.md). The
transport-agnostic public surface (`IMessageBus`, `[MqPub]`, `[MqSub]`,
descriptor records) lives in
[`messaging/abstractions/README.md`](../abstractions/README.md).

---

## Public surface

**`MessagingRabbitMqServiceCollectionExtensions.AddD2MessagingRabbitMq(...)`**
— wires connection, channel pool, bus, topology declarer, idempotency
startup check, and the four hosted services. Idempotent. Validates that
`WaitForConfirm == true` implies `PublisherConfirmsEnabled == true` at
composition time (`ValidateOnStart`). A mismatch is a startup failure, not a
silent fire-and-forget surprise on what the operator believed was a confirmed
publish.

**`RabbitMqConnectionOptions`** — `ConnectionUri` (`amqp://...` or
`amqps://...`, embeds host / port / vhost / credentials / TLS) +
`ClientProvidedName` + consumer-dispatch concurrency + reconnect backoff.

**`ChannelPoolOptions`** — publisher pool size (default 4), acquire
timeout (default 30s), publisher confirms toggle (default on),
**`IdleTtl`** (default 5 min — channels idle longer than this are
disposed and replaced on the next acquire to avoid stale broker-side
state under low-traffic services).

**`RabbitMqPublisherOptions`** — confirm wait toggle, confirm timeout
(default 5s), max attempts (default 5), retry backoff (200ms → 5s cap).

`IMessageBus` itself is registered to `RabbitMqMessageBus` — that type is
internal and not part of the public surface. The bus is a **singleton**
(builds a transient DI scope per `PublishAsync` to resolve the keyed
`IPayloadCrypto` and the calling scope's `IRequestContext` snapshot —
hosted services + other singletons can publish without ceremony).

---

## Wire format

### Envelope

There is no envelope wrapper. The wire body is one of:

- **Plaintext path** — raw `System.Text.Json` serialization of the message
  value (UTF-8 bytes).
- **Encrypted path** — a single AES-256-GCM frame produced by
  `D2.Shared.Encryption.IPayloadCrypto.Encrypt`:
  ```
  [version=1 byte][kid_len=1 byte][kid:UTF-8 bytes][nonce:12 bytes][ciphertext+tag]
  ```
  The kid is also duplicated into the `x-d2-encryption-kid` AMQP header so DLQ
  ops triage can decide whether archive keys are needed without first
  decrypting. The binary frame-layout byte offsets are themselves spec-driven
  via
  [`encryption/frame-source-gen/`](../../encryption/frame-source-gen/README.md).

`EncryptedBodyComposer` chooses the path from the descriptor's `Encryption`
field. Plaintext on a domain that should be encrypted — or vice versa — is a
spec edit, not a code edit; the resolver picks up the new descriptor on the
next build.

### Why JSON not binary protobuf

- Cross-language consumer-friendliness: any language with a JSON parser can
  read a decrypted body without `protoc`-generated code.
- DLQ inspection lands a UTF-8 JSON document after decrypt — `grep`-able,
  diff-able, replay-able.
- The size + parse cost is comfortably bounded for the message rates this
  system runs at.

---

## AMQP header contract

Every published message carries the headers below. Headers stay plaintext in
the broker — they MUST NOT carry user identity, scopes, fingerprints, or any
other sensitive context.

| Header                | Direction           | Purpose                                                                                                                                                                                                                                        |
| --------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `content-type`        | producer → consumer | Always `application/octet-stream`. Body is an opaque byte sequence (encrypted or JSON).                                                                                                                                                        |
| `x-proto-type`        | producer → consumer | The CLR FQN of the message type — fail-fast inspection without body parsing.                                                                                                                                                                   |
| `message-id`          | producer → consumer | UUIDv7 (sortable, includes timestamp). Stable across publish retries — the publisher generates ONCE per `PublishAsync` call so a retry of an already-broker-received-but-unconfirmed publish doesn't bypass the consumer's idempotency window. |
| `timestamp`           | producer → consumer | ISO 8601 UTC at publish time.                                                                                                                                                                                                                  |
| `traceparent`         | producer → consumer | Full W3C trace-context string `00-{traceId}-{spanId}-{flags}`. The consumer parses it via `ActivityContext.TryParse` and starts a `Consumer`-kind span whose parent is the publish span — cross-hop trace assembly works in any OTel backend.  |
| `tracestate`          | producer → consumer | Optional W3C vendor-specific trace state, forwarded as-is.                                                                                                                                                                                     |
| `x-d2-encryption-kid` | producer → consumer | Encryption key id. Set only on encrypted messages.                                                                                                                                                                                             |
| `x-d2-context`        | producer → consumer | Base64url-of-JSON encoded `PropagatedContext` — request id, request path, fingerprints, WhoIs hash. NOT identity (UserId / OrgId / Scopes — those rebuild from the JWT at every hop).                                                          |
| `x-d2-failure-reason` | DLQ-only            | JSON-encoded `DlqFailureMetadata` (cause, errorCode, attemptCount, traceId). Attached by the consumer when republishing to the queue's DLX — see the DLQ section below.                                                                        |

> The runtime header **direction + purpose** semantic catalog lives in this
> doc (it's the operational reference). The **wire-value constants**
> (e.g. `AmqpHeaders.MESSAGE_ID = "message-id"`) are codegen-emitted into
> [`headers/amqp/`](../../headers/amqp/README.md) from
> `contracts/headers/headers.spec.json`.

---

## Publisher path

### Registration

```csharp
services.AddD2MessagingRabbitMq(
    configureConnection:  o => o.ConnectionUri = "amqp://...",
    configureChannelPool: o => o.PublishPoolSize = 8,
    configurePublisher:   o => { o.WaitForConfirm = true; o.MaxAttempts = 5; });

// Encryption (only for encrypted-domain messages):
services.AddD2EncryptionFor(EncryptionDomains.Audit, _ => new PayloadCryptoKeyring(...));
```

`AddD2MessagingRabbitMq`:

- Registers `ID2Connection`, `IChannelPool`, `IMessageBus` as **singletons**.
  The bus builds a transient DI scope per `PublishAsync` to resolve the keyed
  `IPayloadCrypto` and the calling scope's `IRequestContext` snapshot —
  background hosted services can publish without ceremony.
- Validates that `RabbitMqPublisherOptions.WaitForConfirm == true` implies
  `ChannelPoolOptions.PublisherConfirmsEnabled == true` at composition time
  (`ValidateOnStart`). A mismatch is a startup failure, not a silent
  fire-and-forget surprise.

### Calling the bus

```csharp
public sealed class KeyRotatedAnnouncer(IMessageBus bus) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await bus.WaitForReadyAsync(ct);     // see WaitForReadyAsync below
        var result = await bus.PublishAsync(new KeyRotatedEvent { ... }, ct: ct);
        if (result.Failed) { /* log + decide */ }
    }
}
```

### Resolution + composition pipeline

For each `PublishAsync<TMessage>`:

1. `MessageWireResolver.Resolve(typeof(TMessage))` looks up the descriptor via
   the type's `[MqPub]` attribute → `MqMessagesRegistry.ByConstant`. Fails loud
   on missing attribute, unknown constant, or FQN mismatch — these are
   programmer errors, not runtime conditions.
2. `EncryptedBodyComposer.Compose(message, descriptor, sp)` produces the body
   bytes (plaintext JSON or encrypted frame) **once**. Body bytes are reused
   across retry attempts so a retry doesn't re-encrypt with a freshly-generated
   nonce.
3. A stable per-publish `message-id` (UUIDv7) is generated **once**.
4. `BuildPropagatedHeader` snapshots the calling scope's `IRequestContext`
   into `x-d2-context` (or null if no context registered).
5. `RetryHelper.RetryAsync` runs the publish with exponential backoff. Transient
   classification (`TransientPublishClassifier`) treats broker-NACK /
   `OperationInterrupted` / `BrokerUnreachable` / `BrokerUnavailable` /
   `AlreadyClosed` / `TimeoutException` / `PublishException` (when not a
   return) / standard transients as retryable; everything else surfaces as
   `D2Result.ServiceUnavailable`.
6. Each attempt acquires a channel from `BoundedChannelPool.AcquireAsync`. The
   pool evicts channels idle longer than `IdleTtl` (default 5 min) to avoid
   stale broker-side state under low-traffic services.
7. With confirms enabled, `BasicPublishAsync` returns when the broker acks. A
   bounded `ConfirmTimeout` linked-CTS surfaces a hung broker as a
   `TimeoutException` (transient).

### Telemetry

| Metric                                         | Type           | Description                                                                                                                                  |
| ---------------------------------------------- | -------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `d2.messaging.rabbitmq.publishes`              | Counter        | Total publish attempts (including retries).                                                                                                  |
| `d2.messaging.rabbitmq.publish_failures`       | Counter        | Terminal publish failures (after retries exhausted).                                                                                         |
| `d2.messaging.rabbitmq.publish_retries`        | Counter        | Publish retry attempts (transient → backoff → re-attempt).                                                                                   |
| `d2.messaging.rabbitmq.publish_duration`       | Histogram (ms) | Wall-clock duration of a publish operation, end-to-end.                                                                                      |
| `d2.messaging.rabbitmq.dlq_republish_failures` | Counter        | Consumer-side DLQ republish failures — falls back to `BasicNack-no-requeue` so the message still leaves the primary queue.                   |
| `d2.messaging.rabbitmq.ack_failures`           | Counter        | Post-handler-success ack failures (narrow catch around `BasicAckAsync`) — the idempotency mark prevents duplicate work on broker redelivery. |

A `publish {exchange}/{routingKey}` Producer-kind activity wraps the whole
call. Its tags are spec-driven via
`contracts/otel-messaging-tags/otel-messaging-tags.spec.json` — emitted by
`D2.Shared.OtelMessagingTags.SourceGen` into `MessagingActivityTags` consumed
by the publisher AND consumer. The publisher emits:

- `MessagingActivityTags.MESSAGING_SYSTEM` (`messaging.system` = `"rabbitmq"`)
- `MessagingActivityTags.MESSAGING_DESTINATION_NAME`
  (`messaging.destination.name` = exchange)
- `MessagingActivityTags.MESSAGING_RABBITMQ_ROUTING_KEY`
  (`messaging.rabbitmq.routing_key`)
- `MessagingActivityTags.D2_MESSAGE_TYPE` (`d2.message_type` = CLR FullName)
- `MessagingActivityTags.D2_ENCRYPTION_KID` (`d2.encryption_kid`; null/absent
  for plaintext)
- `MessagingActivityTags.MESSAGING_MESSAGE_ID` (`messaging.message.id` =
  UUIDv7)
- `MessagingActivityTags.MESSAGING_OPERATION_TYPE`
  (`messaging.operation.type` = `"publish"`)

The OTel sem-conv canonical attribute is `messaging.operation.type`, NOT
`messaging.operation` — spec-driving the catalog prevents the
publisher / consumer drift class.

### `WaitForReadyAsync`

`IMessageBus.WaitForReadyAsync(CancellationToken)` awaits
`ID2Connection.ReadyTask` — the first connection landing. Use it from
background hosted services that fire off a publish at startup so a
startup-race-with-broker doesn't surface as a confusing `ServiceUnavailable`
on the first call.

---

## Consumer path

### Registration

```csharp
services.AddD2Handler();
services.AddD2MessagingRabbitMq(...);
services.AddD2SubscribersFromAssembly(typeof(MyHandler).Assembly);
```

`AddD2SubscribersFromAssembly` reflects over the assembly looking for classes
with `[MqSub]`; for each match, `SubscriberRegistrar.Register`:

1. Looks up the descriptor in `MqSubscriptionsRegistry.ByConstant`. Missing
   constant → loud throw.
2. Walks the handler's inheritance chain looking for
   `BaseHandler<TSelf, TInput, TOutput>` and validates
   `TInput.FullName == descriptor.MessageTypeName`. Mismatch → loud throw.
3. Calls `ResolveQueueName(descriptor)` which appends a per-process 8-char
   Guid suffix when `descriptor.Pattern == FanoutExclusiveAutoDelete` — keeps
   multi-replica services from racing on the broker's exclusive-queue lock.
4. Registers the handler `Transient`, registers an `ISubscriberRegistration`
   carrying `(HandlerType, MessageType, Descriptor, ResolvedQueueName)`.

### Hosted-service ordering

| Order | Hosted service                   | Responsibility                                                                                                                                                                                                                                                                                         |
| ----- | -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1     | `ConnectionStartupHostedService` | Kicks off background reconnect loop.                                                                                                                                                                                                                                                                   |
| 2     | `IdempotencyStartupCheck`        | Hard-fails startup when any subscription declares `idempotency=true` but no `IDistributedCache` is registered AND no operator-provided `IMessageIdempotencyStore` is registered.                                                                                                                       |
| 3     | `TopologyHostedService`          | Background-declares the topology once `ID2Connection.ReadyTask` completes. A faulted declaration is logged via `TopologyLog.DeclarationFailed` (no more silent loss on `PRECONDITION_FAILED`).                                                                                                         |
| 4     | `ConsumerHostedService`          | Awaits `ReadyTask`, **re-declares topology synchronously** (idempotent), then opens one `SubscriberChannel` per registration. Exposes `ReadyTask` (Task) that completes when every channel has finished `BasicConsume` — integration tests + ordered-startup callers can wait on it before publishing. |

### Per-delivery pipeline

For each delivery on a subscriber channel:

1. **In-flight callback counter** — `Interlocked.Increment` so `DisposeAsync`
   can drain in-flight handlers (bounded 30s) before closing the channel
   mid-ack.
2. **Trace context** — parse `traceparent` / `tracestate` headers via
   `ActivityContext.TryParse`; start a `Consumer`-kind activity
   `receive {queue}` with that as the parent context. Consumer-side activity
   tags (all spec-driven via `MessagingActivityTags`):
   - `MESSAGING_SYSTEM` (`messaging.system` = `"rabbitmq"`)
   - `MESSAGING_DESTINATION_NAME` (`messaging.destination.name` = queue)
   - `MESSAGING_OPERATION_TYPE` (`messaging.operation.type` = `"receive"`)
   - `MESSAGING_MESSAGE_ID` (`messaging.message.id` = producer-assigned
     UUIDv7)
   - `MESSAGING_RABBITMQ_DELIVERY_TAG` (`messaging.rabbitmq.delivery_tag`)
   - `MESSAGING_RABBITMQ_REDELIVERED` (`messaging.rabbitmq.redelivered`)
3. **Per-message DI scope** — `IServiceScopeFactory.CreateAsyncScope`. The
   scope owns the handler instance + a fresh `MutableRequestContext`.
4. **Propagated context** — read `x-d2-context`, decode via
   `PropagatedContextSerializer`, apply onto the scope's
   `MutableRequestContext`. Identity (UserId / OrgId / Scopes) is NEVER in
   this header — it would rebuild from a JWT in a sync hop; for async events
   the consumer-side handler doesn't have one and shouldn't claim caller
   identity.
5. **Idempotency pre-check** (when `descriptor.Idempotency`) —
   `IMessageIdempotencyStore.HasSeenAsync(messageId)`. Hit → `BasicAck`,
   return without invoking the handler. ServiceUnavailable on the **read**
   path → fail-open (process the message; better a duplicate than reject
   during a Redis blip — handlers MUST be at-least-once-safe). The **write**
   path (after handler success) is different: a failed `MarkSeenAsync` would
   silently leave the dedup window unguarded for that message-id, so it
   NACKs to DLQ (cause `RETRIES_EXHAUSTED` ≠ this — it's the same DLQ shape
   as a handler failure) and emits the `IdempotencyMarkFailed` log +
   `ack_failures` counter so the operator sees the store-degradation impact.
6. **Tiered-retry attempt-count check** (when `descriptor.TieredRetry` is
   non-null) — parse the `x-death` header, sum `count` across entries whose
   `reason` is `expired` (retry-tier TTL expiry — our retry path) or
   `rejected` (consumer NACK). Other reasons (`maxlen`, `delivery_limit`) are
   broker-side flow control, not consumer-side retries; counting them would
   trigger `RETRIES_EXHAUSTED` prematurely. If the filtered total ≥
   `MaxAttempts`, route direct to DLQ with cause `RETRIES_EXHAUSTED` (no
   handler invocation). Without this, a permanently-broken payload would
   bounce through tier queues forever.
7. **Dispatch** —
   `HandlerDispatcherFactory.GetForQueue(queue).DispatchAsync(scope.ServiceProvider, ea.Body, ct)`.
8. **Result branch**:
   - `MessageBodyDecodeException` → DLQ with cause `DESERIALIZE_FAILURE` or
     `DECRYPT_FAILURE`.
   - Other handler exception → DLQ with cause `HANDLER_EXCEPTION`.
     (BaseHandler's universal try/catch usually swallows this into
     `D2Result.UnhandledException` → falls into the result-failure arm below.)
   - `result.Failed` → DLQ with cause `HANDLER_RESULT_FAILURE`, errorCode =
     `result.ErrorCode`.
   - Success → idempotency mark (if enabled), then `BasicAck`. Ack failures
     are caught **narrowly** around the `BasicAckAsync` call only — they emit
     a structured log + `d2.messaging.rabbitmq.ack_failures` counter and rely
     on broker redelivery (the idempotency mark prevents duplicate work).
     Without the narrow catch, an ack-after-success failure would falsely
     route the already-processed message to DLQ.

### Idempotency contract (when `idempotency: true` in the spec)

1. The consumer pre-checks
   `IMessageIdempotencyStore.HasSeenAsync(messageId)` before invoking the
   handler. Hit → ack-and-skip.
2. After a successful handler run, the consumer writes the mark via
   `MarkSeenAsync(messageId)` BEFORE `BasicAck`.
3. **Read-path** failure (HasSeen returns ServiceUnavailable) → **fail-open**:
   process the message anyway. Handlers MUST be at-least-once-safe; rejecting
   messages during a transient store outage on the read path is the wrong
   tradeoff.
4. **Write-path** failure (MarkSeen returns ServiceUnavailable) → **NACK to
   DLQ**. Acking without a written mark would silently leave the dedup window
   unguarded; a redelivery of the already-processed message would re-run the
   handler. The `ack_failures` counter + `IdempotencyMarkFailed` log surface
   the store-degradation window so the operator can react.
5. Startup-check: `IdempotencyStartupCheck` hard-fails host startup when any
   subscription declares `idempotency: true` but neither `IDistributedCache`
   nor an operator-provided `IMessageIdempotencyStore` is registered. Silent
   no-op on a safety feature is the worst possible default.

### Delivery semantics

- **At-least-once** is the default and only contract. Handlers MUST be
  idempotent. The opt-in `IMessageIdempotencyStore` (subscription-level
  `idempotency: true`) provides a 24-hour dedup window backed by
  `IDistributedCache`, but the handler's own state machine is the source of
  truth.
- **`message-id` is UUIDv7**, generated ONCE per `PublishAsync` call so a
  publisher retry of an unconfirmed publish doesn't bypass the consumer's
  dedup window.
- **Manual ack only.** `autoAck: false` everywhere. Ack happens AFTER the
  handler's work commits + the idempotency mark is written, not before.
- **Crash between mark + ack** is safe: the next redelivery sees the mark and
  skips the handler. The opposite ordering would let a
  handler-completed-but-pre-mark crash cause a duplicate run, which the
  redelivery couldn't detect.

### Queue patterns

| Pattern                     | When                                                                                            | Durability                                                                                                                                                                                  |
| --------------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CompetingConsumer`         | commands / requests delivered to one consumer in the fleet                                      | durable, non-exclusive, non-autodelete                                                                                                                                                      |
| `DurableShared`             | persistent events (audit, file lifecycle) — survives restart, multiple consumers OK             | durable, non-exclusive, non-autodelete                                                                                                                                                      |
| `FanoutExclusiveAutoDelete` | per-instance broadcast (cache invalidation, keyring refresh) — every replica gets every message | non-durable, exclusive, auto-delete; consumer host auto-suffixes the queue name with a per-process token to avoid the broker's exclusive-queue lock collision in a multi-replica deployment |

### DLQ republish-with-failure-header

`PublishFailureHeaderAsync` republishes the original body to `{queue}.dlx`
with the failure-reason header attached, then `BasicAck`s the original
delivery. A dedicated republish channel (lazy, one per `SubscriberChannel`)
keeps publish state out of the consume channel's delivery queue. On republish
failure: log + `d2.messaging.rabbitmq.dlq_republish_failures` counter + fall
back to `BasicNack-no-requeue` (broker's `x-dead-letter-exchange` argument
routes a header-less copy — better than losing the message).

### `DlqFailureMetadata` (the `x-d2-failure-reason` payload)

```jsonc
{
  "cause":        "HANDLER_EXCEPTION" | "HANDLER_RESULT_FAILURE" | "DECRYPT_FAILURE" | "DESERIALIZE_FAILURE" | "RETRIES_EXHAUSTED",
  "errorCode":    "<exception type FullName | result.ErrorCode>",
  "detail":       null | "<message-keys-joined>",     // see PII-safety below
  "attemptCount": 0,                                  // observed redelivery count
  "traceId":      "<W3C trace id, hex>",
  "nackedBy":     "<service name, optional>"
}
```

The wire shape is spec-driven via
`contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json`. The
`D2.Shared.Messaging.DlqMetadata.SourceGen` multi-target source-gen emits
`DlqFailureMetadataFields` (property names) into
`D2.Shared.Messaging.Abstractions` and `DlqFailureCauses` (closed-enum cause
strings) into `D2.Shared.Messaging.RabbitMq`. The `DlqFailureMetadata` record
applies `[JsonPropertyName(DlqFailureMetadataFields.*)]` attributes
referencing the codegen-emitted constants — drift between the wire shape and
the spec is structurally impossible. The TS sibling `@d2/messaging-abstractions`
emits identical constants, so any TS reader (DLQ ops tooling, RabbitMQ
subscribers) shares byte-equal field-name and cause-string identifiers with
the .NET producers.

**PII-safety discipline**: `detail` is **never** built from
`exception.Message` (handler code can interpolate user input). For
result-failure cases it joins the result's `messages.Select(m => m.Key)` —
translation-token strings, developer-controlled, safe. For exception cases it
stays `null`. All log delegates that take an `Exception` log only
`SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` — no
`ex.Message`, no full stack trace. Handlers MUST NOT include user input in
exception messages, but the broker / log pipeline defends against accidents.

---

## DLX + DLQ convention

Every primary queue `{q}` gets:

- **DLX** `{q}.dlx` — fanout exchange.
- **DLQ** `{q}.dlq` — durable queue bound to the DLX.

The primary queue is declared with
`x-dead-letter-exchange = {q}.dlx` and `x-dead-letter-routing-key = ""`.

On any handler / boundary failure, the consumer **republishes** the original
body to `{q}.dlx` with the `x-d2-failure-reason` header attached, then
`BasicAck`s the original delivery. A dedicated republish channel keeps
publish state out of the consume channel's delivery queue. On republish
failure: emits `d2.messaging.rabbitmq.dlq_republish_failures` counter and
falls back to `BasicNack-no-requeue` (header-less copy lands in DLQ via
`x-dead-letter-exchange`).

---

## Optional retry tier topology

When a subscription declares a `tieredRetry` block in its spec entry, the
topology declarer stands up:

```
{queue}             → primary queue (binds to descriptor.Exchange)
                      x-dead-letter-exchange = {queue}.dlx
                      x-dead-letter-routing-key = ""

{queue}.dlx         → fanout DLX
{queue}.dlq         → bound to {queue}.dlx (the actual DLQ)

{queue}.retry.return → fanout exchange bound BACK to {queue} (routes TTL'd messages back in)

For each tier i in TieredRetry.Tiers:
  {queue}.retry.{i}    → single name used for BOTH the fanout retry-tier exchange AND its bound queue.
                         The exchange's binding routes to the same-name queue; the queue has
                         x-message-ttl = tiers[i] and x-dead-letter-exchange = {queue}.retry.return.
                         (Ops tooling that lists queues vs exchanges in the broker management
                         UI sees the same name in both lists — broker resource type
                         disambiguates which is which.)
```

In normal use, a transient handler failure NACKs to one of the retry-tier
exchanges (the driver is responsible for routing; the framework declares the
topology but the per-handler driver code wires the NACK explicitly — the
framework does not auto-route on the consumer's behalf). The message
TTL-expires onto the retry-return exchange, RabbitMQ re-routes it to the
primary queue. The consumer's `x-death`-driven attempt counter caps the total
cycles via `MaxAttempts`.

---

## Encryption posture

- One keyring per **encryption domain** — registered keyed-singleton via
  `services.AddD2EncryptionFor(domain, factory)`. Domains live in
  `D2.Shared.Encryption.EncryptionDomains` (`Audit`, `Notifications`,
  `Courier`, ...).
- The descriptor's `encryption` field IS the domain string (or the literal
  `"plaintext"`). The publisher resolves `IPayloadCrypto` keyed by that
  string.
- Plaintext entries MUST document `encryptionReason` in the spec. The build
  doesn't enforce a non-empty string, but the field surfaces in code review
  when "why isn't this encrypted?" comes up.
- **AMQP headers stay plaintext at-rest.** Only the body is wrapped in the
  encryption frame. Identity (`UserId` / `OrgId` / scopes) is NEVER in
  headers — the broker stores headers plaintext, and routing semantics need
  them readable.
- The `EncryptionDomains` catalog is itself spec-driven via
  [`encryption/domains-source-gen/`](../../encryption/domains-source-gen/README.md);
  the binary frame layout is spec-driven via
  [`encryption/frame-source-gen/`](../../encryption/frame-source-gen/README.md);
  per-keyring rotation lifecycle lives in
  [`encryption/`](../../encryption/core/README.md).

---

## Defaults that apply automatically

- Publisher confirms: **on**. Disable per-call via
  `PublisherOptions.WaitForConfirm = false` for fire-and-forget.
- Confirm timeout: **5s**. Slow brokers exhibit timeouts as transient and
  retry up to `MaxAttempts` (default 5).
- Retry backoff: **200ms → 2× → cap at 5s**. Worst case before
  `ServiceUnavailable`: 5 attempts × 5s confirm + ~7s of backoff ≈ 32s.
- Persistent message delivery mode (`DeliveryModes.Persistent`).
- AMQP body content-type: `application/octet-stream` — body is opaque
  (encrypted bytes, or JSON bytes; never a structured AMQP type).
- Channel pool idle TTL: **5 min** — bounded broker-side state under
  low-traffic services.
- Subscriber-disposal in-flight drain: **30s** — bounded hold on host
  shutdown so well-behaved handlers ack cleanly.

---

## Architecture

| Concern                    | Owner                                                                                                                                                                                                                                                                                                               |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Connection lifecycle       | `Connection/RabbitMqConnection.cs` — singleton wrapper over `IConnection`; opens lazily; `RabbitMQ.Client` automatic recovery handles reconnects. Host stays up while broker is down (publishers return `ServiceUnavailable`; consumers idle).                                                                      |
| Connection startup         | `Connection/ConnectionStartupHostedService.cs` — non-blocking; kicks off the connection's reconnect loop.                                                                                                                                                                                                           |
| Channel pool               | `Channels/BoundedChannelPool.cs` — semaphore-bounded; recycles healthy channels on lease return; discards faulted channels; evicts channels idle longer than `IdleTtl`.                                                                                                                                             |
| Body composition           | `Encryption/EncryptedBodyComposer.cs` — JSON-serializes the typed message; encrypts via `IPayloadCrypto[domain]` when the descriptor's `Encryption` is non-`plaintext`. Validates encryption-frame version byte on decrypt.                                                                                         |
| Wire resolution            | `Encryption/MessageWireResolver.cs` — `Type → MqMessageDescriptor` lookup via `[MqPub]` + `MqMessagesRegistry.ByConstant`. Per-type cached. Hard-fails on missing attribute / unknown constant / FQN mismatch. Test seam (`RegisterForTesting`) lets integration fixtures bypass the FQN check for synthetic types. |
| Publishing                 | `Publishing/RabbitMqMessageBus.cs` — IMessageBus impl; integrates body composer + channel pool + retry helper + transient classifier; OTel-instrumented; `WaitForReadyAsync` for startup-time publishers.                                                                                                           |
| Transient classification   | `Publishing/TransientPublishClassifier.cs` — what's worth retrying. Includes `PublishException` (when not a return-publish), `BrokerUnavailable`, `BrokerUnreachable`, `OperationInterrupted`, `AlreadyClosed`, `TimeoutException`, plus the standard transients.                                                   |
| Topology                   | `Topology/DefaultTopologyDeclarer.cs` — idempotent declaration of exchanges + queues + DLX + DLQ + optional retry tiers (driven by `SubscriberRegistry`).                                                                                                                                                           |
| Topology startup           | `Topology/TopologyHostedService.cs` — non-blocking; awaits connection ready, then declares once. Logs `TopologyLog.DeclarationFailed` on background-task faults so a `PRECONDITION_FAILED` doesn't vanish into `TaskScheduler.UnobservedTaskException`.                                                             |
| Consumer host              | `Subscribing/ConsumerHostedService.cs` — opens one `SubscriberChannel` per registration after declaring topology synchronously. Exposes `ReadyTask` (Task) that completes when every channel has finished `BasicConsume`.                                                                                           |
| Subscriber channel         | `Subscribing/SubscriberChannel.cs` — owns one consume channel + one lazy republish channel per subscription; per-delivery DI scope; trace-context parsing; tiered-retry attempt-count enforcement; idempotency pre-check; narrow-catch around `BasicAck`; in-flight callback drain on disposal.                     |
| Handler dispatch           | `Subscribing/HandlerDispatcherFactory.cs` — pre-builds typed dispatchers at startup from the registry; one dispatcher per registered queue.                                                                                                                                                                         |
| DLQ failure header         | `Subscribing/DlqFailureHeaderBuilder.cs` — JSON-encodes `DlqFailureMetadata`; PII-safe (drops `exception.Message`).                                                                                                                                                                                                 |
| Sanitized exception render | Consumes `D2.Shared.Utilities.Diagnostics.SanitizedExceptionRender` (`TypeName` + `FirstFrame` only; never `ex.Message`). Used by every consumer-side log site that surfaces exception-derived strings (handler exceptions, ack failures, DLQ-republish failures, boundary failures).                               |
| Idempotency                | `Idempotency/CacheIdempotencyStore.cs` — `IMessageIdempotencyStore` impl backed by `IDistributedCache` with 24h TTL.                                                                                                                                                                                                |
| Idempotency startup check  | `Idempotency/IdempotencyStartupCheck.cs` — hard-fails host startup when any subscription has `idempotency: true` but no `IDistributedCache` AND no operator-provided `IMessageIdempotencyStore`.                                                                                                                    |
| Telemetry                  | `Telemetry/MessagingTelemetry.cs` — static `ActivitySource` + `Meter` named `D2.Shared.Messaging.RabbitMq`; six instruments (publishes, failures, retries, ack-failures, dlq-republish-failures counters; publish-duration histogram).                                                                              |

---

## Operational anti-patterns

- **Auto-ack.** The pipeline always uses manual ack with `autoAck: false`. If
  you find yourself wanting auto-ack, you actually want at-most-once
  semantics, which is a different problem.
- **Sharing the same queue name across competing consumers in a multi-process
  layout when the spec pattern is `FanoutExclusiveAutoDelete`.** Trust the
  per-process suffix — don't override it.
- **Stuffing identity (`UserId` / `OrgId` / scopes) into `x-d2-context`.**
  That field carries propagation-only context (request id, fingerprints).
  Identity rebuilds from the JWT at every hop; consumer-side handlers
  operate without one.
- **Putting user input into exception messages.** The DLQ failure-reason
  header drops `ex.Message` for safety; logs do too. Don't fight it — push
  human-readable detail into result `messages` (translation keys) instead.

> Contract-level anti-patterns (hand-registering `IMessageBus` outside the
> blessed DI helpers, FQN mismatch between `[MqPub]` and the spec entry,
> `[MqSub]` without matching handler type) live in
> [`messaging/abstractions/README.md`](../abstractions/README.md).

---

## Dependencies

- `D2.Shared.Messaging.Abstractions` — interfaces, descriptor records,
  registry, failure helpers, attributes (`MqPub` / `MqSub`),
  `AmqpHeaders`, codegen-emitted `MqMessages.*` / `MqSubscriptions.*`.
- `D2.Shared.Encryption` — `IPayloadCrypto` keyed per encryption domain;
  `EncryptionDomains` constants.
- `D2.Shared.Caching.Abstractions` — `IDistributedCache` consumed by
  `CacheIdempotencyStore`.
- `D2.Shared.Handler` — subscribers are `BaseHandler<TSub, TIn, Unit>`
  instances; the consumer wrapper invokes them via the standard pipeline.
- `D2.Shared.Resilience` — `RetryHelper.RetryAsync` drives the publisher's
  built-in transient retry loop.
- `D2.Shared.Result`, `D2.Shared.Utilities`, `D2.Shared.I18n.Abstractions`
  — standard cross-cutting deps.
- `D2.Shared.Context.Abstractions` + `.Abstractions` — for the consumer-side DI
  registration of `MutableRequestContext` / `IRequestContext` (handlers
  resolve `IRequestContext` through `HandlerContext`); the consumer
  applies the `x-d2-context`-decoded `PropagatedContext` onto the per-
  message scope's `MutableRequestContext`.
- `RabbitMQ.Client 7.x` — only this package references it; abstractions
  stay transport-free.

---

## References

- [`messaging/abstractions/`](../abstractions/README.md) — the
  contract this package implements (transport-agnostic surface, attributes,
  descriptor records, registry, scanner, contract-level anti-patterns).
- [`messaging/source-gen/`](../source-gen/README.md) — codegen
  emitting the `MqMessages` / `MqSubscriptions` registries; full spec
  format + diagnostic catalog + spec evolution rules.
- [`headers/amqp/`](../../headers/amqp/README.md) — codegen-emitted wire-value
  constants for the AMQP header catalog (`AmqpHeaders.MESSAGE_ID`, etc.).
- [`encryption/`](../../encryption/core/README.md) — encryption primitive (keyring,
  `IPayloadCrypto`, frame format, rotation lifecycle).
- [`encryption/domains-source-gen/`](../../encryption/domains-source-gen/README.md)
  — the `EncryptionDomains` catalog spec.
- [`encryption/frame-source-gen/`](../../encryption/frame-source-gen/README.md)
  — the binary frame layout byte-offset spec.
