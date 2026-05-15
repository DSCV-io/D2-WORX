<!--
Copyright (c) DCSV. All rights reserved.
-->

# MESSAGING.md — D²-WORX Cross-Service Messaging

> Authoritative reference for the D²-WORX async messaging stack: spec-driven publishing, attribute-routed subscriptions, AMQP wire format, queue topology, headers, encryption, retries, DLQ, and delivery semantics. The transport is RabbitMQ; the abstractions can host other transports later without consumer-side changes.

---

## §1. Architecture in one diagram

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
- The `D2.Shared.Messaging.SourceGen` Roslyn analyzer reads both at build time and emits constants + immutable runtime registries into the `D2.Shared.Messaging.Abstractions` assembly:
  - `MqMessages.AuthKeyRotated` (string constant) + `MqMessagesRegistry.ByConstant` (`Dictionary<string, MqMessageDescriptor>`)
  - `MqSubscriptions.KeyringRefresh` (string constant) + `MqSubscriptionsRegistry.ByConstant`
- The producer marks each message class `[MqPub(MqMessages.X)]`; the publisher resolves `Type → MqMessageDescriptor` via the cached `MessageWireResolver` and gets exchange / encryption / routing-key from the descriptor.
- The consumer marks each handler class `[MqSub(MqSubscriptions.X)]`; `AddD2SubscribersFromAssembly(Assembly)` scans, validates the handler's `BaseHandler<TSelf, TIn, Unit>` generic argument matches the spec's `messageType`, and registers an `ISubscriberRegistration`.

---

## §2. Spec files (the source of truth)

### `mq-messages.spec.json`

```jsonc
{
  "$schema": "./schema.json",
  "messages": [
    {
      "constant": "AuthKeyRotated",
      "messageType": "D2.Shared.Auth.Events.KeyRotatedEvent",
      "exchange": "d2.security.key-rotated",
      "exchangeType": "fanout",
      "encryption": "plaintext",
      "encryptionReason": "Rotation events deliver the (domain, kid) tuple consumers need to refresh their keyrings — encrypting them with keys we are rotating creates an unrecoverable chicken-and-egg.",
      "defaultRoutingKey": ""
    }
  ]
}
```

| Field | Required | Notes |
|---|---|---|
| `constant` | yes | PascalCase. Becomes `MqMessages.{constant}` literal. |
| `messageType` | yes | Fully-qualified .NET type name. The class MUST exist and carry `[MqPub(MqMessages.{constant})]`. The resolver hard-fails on FQN mismatch. |
| `exchange` | yes | AMQP exchange name to publish to. |
| `exchangeType` | yes | `fanout`, `topic`, or `direct`. |
| `encryption` | yes | One of: `plaintext`, or an `EncryptionDomains` constant value (e.g. `audit`, `notifications`, `courier`). |
| `encryptionReason` | when `plaintext` | Free-form rationale documented in the spec — surfaces in code review when someone asks "why isn't this encrypted?". |
| `defaultRoutingKey` | optional | Used when the publisher doesn't pass `PublisherOptions.RoutingKey`. Defaults to empty. |

### `mq-subscriptions.spec.json`

```jsonc
{
  "$schema": "./schema.json",
  "subscriptions": [
    {
      "constant": "KeyringRefresh",
      "messageType": "D2.Shared.Auth.Events.KeyRotatedEvent",
      "queueName": "auth.keyring-refresh",
      "pattern": "FanoutExclusiveAutoDelete",
      "routingKeyBinding": "",
      "prefetch": 1,
      "idempotency": false,
      "tieredRetry": null
    }
  ]
}
```

| Field | Required | Notes |
|---|---|---|
| `constant` | yes | PascalCase. Becomes `MqSubscriptions.{constant}` literal — the value passed to `[MqSub(MqSubscriptions.{constant})]`. |
| `messageType` | yes | FQN — must equal the consuming handler's `BaseHandler<TSelf, TIn, Unit>` `TIn` generic argument. The registrar hard-fails on mismatch. |
| `queueName` | yes | For `FanoutExclusiveAutoDelete` this is a PREFIX; the consumer host auto-suffixes a per-process token to keep multi-replica services from racing on the broker's exclusive-queue lock. |
| `pattern` | yes | One of: `CompetingConsumer`, `DurableShared`, `FanoutExclusiveAutoDelete`. Drives the queue's durable / exclusive / auto-delete flags. |
| `routingKeyBinding` | optional | AMQP routing-key pattern bound to the exchange. Empty for fanout, exact key for direct, wildcard pattern (`*`/`#`) for topic. |
| `prefetch` | yes | Per-channel `basic.qos` prefetch count. |
| `idempotency` | yes | `true` enables a pre-handler `IMessageIdempotencyStore` check. Requires `IDistributedCache` registered (or operator-provided `IMessageIdempotencyStore`); enforced at startup. |
| `tieredRetry` | optional | `null` = retries off, handler failures route straight to DLQ. Otherwise: `{ "tiers": ["00:00:05", "00:00:30", "00:05:00"], "maxAttempts": 5 }` declares the broker-level retry-tier topology + the consumer-side `x-death` attempt cap. |

---

## §3. Wire format

### Envelope

There is no envelope wrapper. The wire body is one of:

- **Plaintext path** — raw `System.Text.Json` serialization of the message value (UTF-8 bytes).
- **Encrypted path** — a single AES-256-GCM frame produced by `D2.Shared.Encryption.IPayloadCrypto.Encrypt`:
  ```
  [version=1 byte][kid_len=1 byte][kid:UTF-8 bytes][nonce:12 bytes][ciphertext+tag]
  ```
  The kid is also duplicated into the `x-d2-encryption-kid` AMQP header so DLQ ops triage can decide whether archive keys are needed without first decrypting.

`EncryptedBodyComposer` chooses the path from the descriptor's `Encryption` field. Plaintext on a domain that should be encrypted — or vice versa — is a spec edit, not a code edit; the resolver picks up the new descriptor on the next build.

### Why JSON not binary protobuf

- Cross-language consumer-friendliness: any language with a JSON parser can read a decrypted body without `protoc`-generated code.
- DLQ inspection lands a UTF-8 JSON document after decrypt — `grep`-able, diff-able, replay-able.
- The size + parse cost is comfortably bounded for our message rates.

---

## §4. AMQP header contract

Every published message carries the headers below. Headers stay plaintext in the broker — they MUST NOT carry user identity, scopes, fingerprints, or any other sensitive context.

| Header | Direction | Purpose |
|---|---|---|
| `content-type` | producer → consumer | Always `application/octet-stream`. Body is an opaque byte sequence (encrypted or JSON). |
| `x-proto-type` | producer → consumer | The CLR FQN of the message type — fail-fast inspection without body parsing. |
| `message-id` | producer → consumer | UUIDv7 (sortable, includes timestamp). Stable across publish retries — the publisher generates ONCE per `PublishAsync` call so a retry of an already-broker-received-but-unconfirmed publish doesn't bypass the consumer's idempotency window. |
| `timestamp` | producer → consumer | ISO 8601 UTC at publish time. |
| `traceparent` | producer → consumer | Full W3C trace-context string `00-{traceId}-{spanId}-{flags}`. The consumer parses it via `ActivityContext.TryParse` and starts a `Consumer`-kind span whose parent is the publish span — cross-hop trace assembly works in any OTel backend. |
| `tracestate` | producer → consumer | Optional W3C vendor-specific trace state, forwarded as-is. |
| `x-d2-encryption-kid` | producer → consumer | Encryption key id. Set only on encrypted messages. |
| `x-d2-context` | producer → consumer | Base64url-of-JSON encoded `PropagatedContext` — request id, request path, fingerprints, WhoIs hash. NOT identity (UserId / OrgId / Scopes — those rebuild from the JWT at every hop). |
| `x-d2-failure-reason` | DLQ-only | JSON-encoded `DlqFailureMetadata` (cause, errorCode, attemptCount, traceId). Attached by the consumer when republishing to the queue's DLX — see §6. |

---

## §5. Publisher path

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
- Registers `ID2Connection`, `IChannelPool`, `IMessageBus` as **singletons**. The bus builds a transient DI scope per `PublishAsync` to resolve the keyed `IPayloadCrypto` and the calling scope's `IRequestContext` snapshot — background hosted services can publish without ceremony.
- Validates that `RabbitMqPublisherOptions.WaitForConfirm == true` implies `ChannelPoolOptions.PublisherConfirmsEnabled == true` at composition time (`ValidateOnStart`). A mismatch is a startup failure, not a silent fire-and-forget surprise.

### Calling the bus

```csharp
public sealed class KeyRotatedAnnouncer(IMessageBus bus) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await bus.WaitForReadyAsync(ct);     // §5.4 — startup ordering
        var result = await bus.PublishAsync(new KeyRotatedEvent { ... }, ct: ct);
        if (result.Failed) { /* log + decide */ }
    }
}
```

### Resolution + composition pipeline

For each `PublishAsync<TMessage>`:

1. `MessageWireResolver.Resolve(typeof(TMessage))` looks up the descriptor via the type's `[MqPub]` attribute → `MqMessagesRegistry.ByConstant`. Fails loud on missing attribute, unknown constant, or FQN mismatch — these are programmer errors, not runtime conditions.
2. `EncryptedBodyComposer.Compose(message, descriptor, sp)` produces the body bytes (plaintext JSON or encrypted frame) **once**. Body bytes are reused across retry attempts so a retry doesn't re-encrypt with a freshly-generated nonce.
3. A stable per-publish `message-id` (UUIDv7) is generated **once**.
4. `BuildPropagatedHeader` snapshots the calling scope's `IRequestContext` into `x-d2-context` (or null if no context registered).
5. `RetryHelper.RetryAsync` runs the publish with exponential backoff. Transient classification (`TransientPublishClassifier`) treats broker-NACK / `OperationInterrupted` / `BrokerUnreachable` / `BrokerUnavailable` / `AlreadyClosed` / `TimeoutException` / `PublishException` (when not a return) / standard transients as retryable; everything else surfaces as `D2Result.ServiceUnavailable`.
6. Each attempt acquires a channel from `BoundedChannelPool.AcquireAsync`. The pool evicts channels idle longer than `IdleTtl` (default 5 min) to avoid stale broker-side state under low-traffic services.
7. With confirms enabled, `BasicPublishAsync` returns when the broker acks. A bounded `ConfirmTimeout` linked-CTS surfaces a hung broker as a `TimeoutException` (transient).

### Telemetry

| Metric | Type | Description |
|---|---|---|
| `d2.messaging.rabbitmq.publishes` | Counter | Total publish attempts (including retries). |
| `d2.messaging.rabbitmq.publish_failures` | Counter | Terminal publish failures (after retries exhausted). |
| `d2.messaging.rabbitmq.publish_retries` | Counter | Publish retry attempts (transient → backoff → re-attempt). |
| `d2.messaging.rabbitmq.publish_duration` | Histogram (ms) | Wall-clock duration of a publish operation, end-to-end. |

A `publish {exchange}/{routingKey}` Producer-kind activity wraps the whole call; its tags include `messaging.system`, `messaging.destination.name`, `messaging.rabbitmq.routing_key`, `d2.message_type`, `d2.encryption_kid`, `messaging.message.id`.

### `WaitForReadyAsync`

`IMessageBus.WaitForReadyAsync(CancellationToken)` awaits `ID2Connection.ReadyTask` — the first connection landing. Use it from background hosted services that fire off a publish at startup so a startup-race-with-broker doesn't surface as a confusing `ServiceUnavailable` on the first call.

---

## §6. Consumer path

### Registration

```csharp
services.AddD2Handler();
services.AddD2MessagingRabbitMq(...);
services.AddD2SubscribersFromAssembly(typeof(MyHandler).Assembly);
```

`AddD2SubscribersFromAssembly` reflects over the assembly looking for classes with `[MqSub]`; for each match, `SubscriberRegistrar.Register`:

1. Looks up the descriptor in `MqSubscriptionsRegistry.ByConstant`. Missing constant → loud throw.
2. Walks the handler's inheritance chain looking for `BaseHandler<TSelf, TInput, TOutput>` and validates `TInput.FullName == descriptor.MessageTypeName`. Mismatch → loud throw.
3. Calls `ResolveQueueName(descriptor)` which appends a per-process 8-char Guid suffix when `descriptor.Pattern == FanoutExclusiveAutoDelete` — keeps multi-replica services from racing on the broker's exclusive-queue lock.
4. Registers the handler `Transient`, registers an `ISubscriberRegistration` carrying `(HandlerType, MessageType, Descriptor, ResolvedQueueName)`.

### Hosted-service ordering

| Order | Hosted service | Responsibility |
|---|---|---|
| 1 | `ConnectionStartupHostedService` | Kicks off background reconnect loop. |
| 2 | `IdempotencyStartupCheck` | Hard-fails startup when any subscription declares `idempotency=true` but no `IDistributedCache` is registered AND no operator-provided `IMessageIdempotencyStore` is registered. |
| 3 | `TopologyHostedService` | Background-declares the topology once `ID2Connection.ReadyTask` completes. A faulted declaration is logged via `TopologyLog.DeclarationFailed` (no more silent loss on PRECONDITION_FAILED). |
| 4 | `ConsumerHostedService` | Awaits `ReadyTask`, **re-declares topology synchronously** (idempotent), then opens one `SubscriberChannel` per registration. Exposes `ReadyTask` (Task) that completes when every channel has finished `BasicConsume` — integration tests + ordered-startup callers can wait on it before publishing. |

### Per-delivery pipeline

For each delivery on a subscriber channel:

1. **In-flight callback counter** — `Interlocked.Increment` so `DisposeAsync` can drain in-flight handlers (bounded 30s) before closing the channel mid-ack.
2. **Trace context** — parse `traceparent` / `tracestate` headers via `ActivityContext.TryParse`; start a `Consumer`-kind activity `receive {queue}` with that as the parent context. Activity tags: `messaging.system`, `messaging.destination.name`, `messaging.message.id`, `messaging.rabbitmq.delivery_tag`, `messaging.rabbitmq.redelivered`.
3. **Per-message DI scope** — `IServiceScopeFactory.CreateAsyncScope`. The scope owns the handler instance + a fresh `MutableRequestContext`.
4. **Propagated context** — read `x-d2-context`, decode via `PropagatedContextSerializer`, apply onto the scope's `MutableRequestContext`. Identity (UserId / OrgId / Scopes) is NEVER in this header — it would rebuild from a JWT in a sync hop; for async events the consumer-side handler doesn't have one and shouldn't claim caller identity.
5. **Idempotency pre-check** (when `descriptor.Idempotency`) — `IMessageIdempotencyStore.HasSeenAsync(messageId)`. Hit → `BasicAck`, return without invoking the handler. ServiceUnavailable on the **read** path → fail-open (process the message; better a duplicate than reject during a Redis blip — handlers MUST be at-least-once-safe). The **write** path (after handler success) is different: a failed `MarkSeenAsync` would silently leave the dedup window unguarded for that message-id, so it NACKs to DLQ (cause `RETRIES_EXHAUSTED` ≠ this — it's the same DLQ shape as a handler failure) and emits the `IdempotencyMarkFailed` log + `ack_failures` counter so the operator sees the store-degradation impact.
6. **Tiered-retry attempt-count check** (when `descriptor.TieredRetry` is non-null) — parse the `x-death` header, sum `count` across entries whose `reason` is `expired` (retry-tier TTL expiry — our retry path) or `rejected` (consumer NACK). Other reasons (`maxlen`, `delivery_limit`) are broker-side flow control, not consumer-side retries; counting them would trigger `RETRIES_EXHAUSTED` prematurely. If the filtered total ≥ `MaxAttempts`, route direct to DLQ with cause `RETRIES_EXHAUSTED` (no handler invocation). Without this, a permanently-broken payload would bounce through tier queues forever.
7. **Dispatch** — `HandlerDispatcherFactory.GetForQueue(queue).DispatchAsync(scope.ServiceProvider, ea.Body, ct)`.
8. **Result branch**:
   - `MessageBodyDecodeException` → DLQ with cause `DESERIALIZE_FAILURE` or `DECRYPT_FAILURE`.
   - Other handler exception → DLQ with cause `HANDLER_EXCEPTION`. (BaseHandler's universal try/catch usually swallows this into `D2Result.UnhandledException` → falls into the result-failure arm below.)
   - `result.Failed` → DLQ with cause `HANDLER_RESULT_FAILURE`, errorCode = `result.ErrorCode`.
   - Success → idempotency mark (if enabled), then `BasicAck`. Ack failures are caught **narrowly** around the `BasicAckAsync` call only — they emit a structured log + `d2.messaging.rabbitmq.ack_failures` counter and rely on broker redelivery (the idempotency mark prevents duplicate work). Without the narrow catch, an ack-after-success failure would falsely route the already-processed message to DLQ.

### DLQ republish-with-failure-header

`PublishFailureHeaderAsync` republishes the original body to `{queue}.dlx` with the failure-reason header attached, then `BasicAck`s the original delivery. A dedicated republish channel (lazy, one per `SubscriberChannel`) keeps publish state out of the consume channel's delivery queue. On republish failure: log + `d2.messaging.rabbitmq.dlq_republish_failures` counter + fall back to `BasicNack-no-requeue` (broker's `x-dead-letter-exchange` argument routes a header-less copy — better than losing the message).

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

**PII-safety discipline** (H7): `detail` is **never** built from `exception.Message` (handler code can interpolate user input). For result-failure cases it joins the result's `messages.Select(m => m.Key)` — translation-token strings, developer-controlled, safe. For exception cases it stays `null`. All log delegates that take an `Exception` log only `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` — no `ex.Message`, no full stack trace. Handlers MUST NOT include user input in exception messages, but the broker / log pipeline defends against accidents.

---

## §7. Tiered retry topology (optional)

When `descriptor.TieredRetry` is non-null, the topology declarer stands up:

```
{queue}             → primary queue (binds to descriptor.Exchange)
                      x-dead-letter-exchange = {queue}.dlx
                      x-dead-letter-routing-key = ""

{queue}.dlx         → fanout DLX
{queue}.dlq         → bound to {queue}.dlx (the actual DLQ)

{queue}.retry-return → fanout exchange bound BACK to {queue} (route TTL'd messages back in)

For each tier i in TieredRetry.Tiers:
  {queue}.retry-{i}.x  → fanout retry-tier exchange
  {queue}.retry-{i}.q  → queue with x-message-ttl = tiers[i] and x-dead-letter-exchange = retry-return
```

In normal use, a transient handler failure NACKs to one of the retry-tier exchanges (the driver is responsible for routing; the framework declares the topology but the per-handler driver code wires the NACK explicitly — the framework does not auto-route on the consumer's behalf). The message TTL-expires onto the retry-return exchange, RabbitMQ re-routes it to the primary queue. The consumer's `x-death`-driven attempt counter caps the total cycles via `MaxAttempts`.

---

## §8. Encryption posture

- One keyring per **encryption domain** — registered keyed-singleton via `services.AddD2EncryptionFor(domain, factory)`. Domains live in `D2.Shared.Encryption.EncryptionDomains` (`Audit`, `Notifications`, `Courier`, ...).
- The descriptor's `encryption` field IS the domain string (or the literal `"plaintext"`). The publisher resolves `IPayloadCrypto` keyed by that string.
- Plaintext entries MUST document `encryptionReason` in the spec. The build doesn't enforce a non-empty string, but the field surfaces in code review when "why isn't this encrypted?" comes up.
- AMQP headers stay plaintext at-rest. Only the body is wrapped in the encryption frame.

---

## §9. Versioning + spec evolution

- The spec files ARE the contract. Adding a message: add a `messages[]` entry, mark the new class `[MqPub(MqMessages.X)]`, rebuild — codegen surfaces the constant.
- Renaming a constant: edit the spec, update every `[MqPub("...")]` reference. The resolver hard-fails on stale references; CI catches it.
- Changing the wire shape of an existing message: the message class itself is the source of truth. JSON serialization tolerates additive changes (new optional fields). Removing or renaming a field is a breaking change — bump to a new constant + new class until every consumer has migrated.
- Encryption-domain change: edit the spec. The publisher picks up the new descriptor on the next build; rolling-upgrade safety needs both keyrings registered during the cutover window.

---

## §10. Anti-patterns to avoid

- **Hand-registering `IMessageBus` / `ISubscriberRegistration` outside of `AddD2MessagingRabbitMq` / `AddD2SubscribersFromAssembly`.** The codegen + scanner are the only blessed paths.
- **Stuffing identity (`UserId` / `OrgId` / scopes) into `x-d2-context`.** That field carries propagation-only context (request id, fingerprints). Identity rebuilds from the JWT at every hop; consumer-side handlers operate without one.
- **Putting user input into exception messages.** The DLQ failure-reason header drops `ex.Message` for safety; logs do too. Don't fight it — push human-readable detail into result `messages` (translation keys) instead.
- **Using `[MqPub]` / `[MqSub]` on a class whose CLR FQN doesn't match the spec entry's `messageType`.** The resolver / registrar hard-fail at build / startup. Silent mismatches were the entire point of the pivot.
- **Sharing the same queue name across competing consumers in a multi-process layout when the spec pattern is `FanoutExclusiveAutoDelete`.** Trust the per-process suffix — don't override.
- **Auto-ack.** The pipeline always uses manual ack with `autoAck: false`. If you find yourself wanting auto-ack, you actually want at-most-once semantics, which is a different problem.
