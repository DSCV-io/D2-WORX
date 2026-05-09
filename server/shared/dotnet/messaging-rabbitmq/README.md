<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Messaging.RabbitMq

> Parent: [`server/shared/dotnet/`](../README.md)

Default `RabbitMQ.Client 7.x` implementation of the
[`D2.Shared.Messaging.Abstractions`](../messaging-abstractions/README.md)
contract. Owns connection lifecycle, channel pooling with idle-eviction,
topology declaration (exchanges + DLX + DLQ + optional retry tiers),
publishing with publisher-confirms + built-in transient retry, payload
encryption via `D2.Shared.Encryption`, full W3C trace-context propagation,
and DLQ republish-with-failure-header.

The wire body is just the serialized message — no envelope wrapper. The
descriptor (`MqMessageDescriptor`, codegen-emitted from
`contracts/mq-messages/mq-messages.spec.json`) drives encryption,
exchange, and default routing key.

## Cross-hop propagation

- **Trace correlation** — full W3C `traceparent` (`00-{traceId}-{spanId}-{flags}`)
  + optional `tracestate` AMQP headers. The consumer parses via
  `ActivityContext.TryParse` and starts a `Consumer`-kind span whose parent
  is the publish span — cross-hop trace assembly works in any OTel backend.
- **Operational subset** — `RequestId` / `RequestPath` / fingerprints /
  `WhoIsHashId` ride in the `x-d2-context` AMQP header (base64url-of-JSON
  encoded `PropagatedContext`; same shape on every transport).
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

## Quick start

```csharp
services
    .AddD2EncryptionFor(EncryptionDomains.Audit, factory: ...)
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

## Public surface

**`MessagingRabbitMqServiceCollectionExtensions.AddD2MessagingRabbitMq(...)`**
— wires connection, channel pool, bus, topology declarer, idempotency
startup check, and the four hosted services. Idempotent. Validates that
`WaitForConfirm == true` implies `PublisherConfirmsEnabled == true` at
composition time (`ValidateOnStart`).

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

## Architecture

| Concern | Owner |
|---|---|
| Connection lifecycle | `Connection/RabbitMqConnection.cs` — singleton wrapper over `IConnection`; opens lazily; `RabbitMQ.Client` automatic recovery handles reconnects. Host stays up while broker is down (publishers return `ServiceUnavailable`; consumers idle). |
| Connection startup | `Connection/ConnectionStartupHostedService.cs` — non-blocking; kicks off the connection's reconnect loop. |
| Channel pool | `Channels/BoundedChannelPool.cs` — semaphore-bounded; recycles healthy channels on lease return; discards faulted channels; evicts channels idle longer than `IdleTtl`. |
| Body composition | `Encryption/EncryptedBodyComposer.cs` — JSON-serializes the typed message; encrypts via `IPayloadCrypto[domain]` when the descriptor's `Encryption` is non-`plaintext`. Validates encryption-frame version byte on decrypt. |
| Wire resolution | `Encryption/MessageWireResolver.cs` — `Type → MqMessageDescriptor` lookup via `[MqPub]` + `MqMessagesRegistry.ByConstant`. Per-type cached. Hard-fails on missing attribute / unknown constant / FQN mismatch. Test seam (`RegisterForTesting`) lets integration fixtures bypass the FQN check for synthetic types. |
| Publishing | `Publishing/RabbitMqMessageBus.cs` — IMessageBus impl; integrates body composer + channel pool + retry helper + transient classifier; OTel-instrumented; `WaitForReadyAsync` for startup-time publishers. |
| Transient classification | `Publishing/TransientPublishClassifier.cs` — what's worth retrying. Includes `PublishException` (when not a return-publish), `BrokerUnavailable`, `BrokerUnreachable`, `OperationInterrupted`, `AlreadyClosed`, `TimeoutException`, plus the standard transients. |
| Topology | `Topology/DefaultTopologyDeclarer.cs` — idempotent declaration of exchanges + queues + DLX + DLQ + optional retry tiers (driven by `SubscriberRegistry`). |
| Topology startup | `Topology/TopologyHostedService.cs` — non-blocking; awaits connection ready, then declares once. Logs `TopologyLog.DeclarationFailed` on background-task faults so a `PRECONDITION_FAILED` doesn't vanish into `TaskScheduler.UnobservedTaskException`. |
| Consumer host | `Subscribing/ConsumerHostedService.cs` — opens one `SubscriberChannel` per registration after declaring topology synchronously. Exposes `ReadyTask` (Task) that completes when every channel has finished `BasicConsume`. |
| Subscriber channel | `Subscribing/SubscriberChannel.cs` — owns one consume channel + one lazy republish channel per subscription; per-delivery DI scope; trace-context parsing; tiered-retry attempt-count enforcement; idempotency pre-check; narrow-catch around `BasicAck`; in-flight callback drain on disposal. |
| Handler dispatch | `Subscribing/HandlerDispatcherFactory.cs` — pre-builds typed dispatchers at startup from the registry; one dispatcher per registered queue. |
| DLQ failure header | `Subscribing/DlqFailureHeaderBuilder.cs` — JSON-encodes `DlqFailureMetadata`; PII-safe (drops `exception.Message`). |
| Sanitized exception render | `Subscribing/SanitizedExceptionRender.cs` — `TypeName` + `FirstFrame` only; never `ex.Message`. Used by every log delegate that takes an `Exception`. |
| Idempotency | `Idempotency/CacheIdempotencyStore.cs` — `IMessageIdempotencyStore` impl backed by `IDistributedCache` with 24h TTL. |
| Idempotency startup check | `Idempotency/IdempotencyStartupCheck.cs` — hard-fails host startup when any subscription has `idempotency: true` but no `IDistributedCache` AND no operator-provided `IMessageIdempotencyStore`. |
| Telemetry | `Telemetry/MessagingTelemetry.cs` — static `ActivitySource` + `Meter` named `D2.Shared.Messaging.RabbitMq`; six instruments (publishes, failures, retries, ack-failures, dlq-republish-failures counters; publish-duration histogram). |

## DLX + DLQ convention

Every primary queue `{q}` gets:

- **DLX** `{q}.dlx` — fanout exchange.
- **DLQ** `{q}.dlq` — durable queue bound to the DLX.

The primary queue is declared with
`x-dead-letter-exchange = {q}.dlx` and `x-dead-letter-routing-key = ""`.

On any handler / boundary failure, the consumer **republishes** the
original body to `{q}.dlx` with the `x-d2-failure-reason` header attached,
then `BasicAck`s the original delivery. A dedicated republish channel
keeps publish state out of the consume channel's delivery queue. On
republish failure: emits `d2.messaging.rabbitmq.dlq_republish_failures`
counter and falls back to `BasicNack-no-requeue` (header-less copy lands
in DLQ via `x-dead-letter-exchange`).

## Optional retry tier topology

When a subscription's `MqSubscriptionDescriptor.TieredRetry` is non-null
(via the spec entry's `tieredRetry` block), the declarer adds:

- **Return exchange** `{q}.retry-return` (fanout) bound to the primary queue.
- **Tier exchanges/queues** `{q}.retry-{0..N-1}.x` and `.q` — durable
  queues with `x-message-ttl = tiers[i].TotalMilliseconds` and
  `x-dead-letter-exchange = {q}.retry-return`.

A failed message can be republished to a tier; on TTL expiry the broker
dead-letters it onto the return exchange, which routes back to the
primary queue for another attempt. The consumer's `x-death`-driven
attempt counter caps total cycles via `TieredRetryDescriptor.MaxAttempts`
— exhaustion routes direct to DLQ with cause `RETRIES_EXHAUSTED` (no
further handler invocation).

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

## References

- [`messaging-abstractions/`](../messaging-abstractions/README.md) — the
  contract this package implements
- [`messaging-source-gen/`](../messaging-source-gen/README.md) — codegen emitting the
  `MqMessages` / `MqSubscriptions` registries
- [MESSAGING.md](../../../../docs/MESSAGING.md) — full wire format / header /
  topology / encryption contract
- [OPERATIONAL-GUARANTEES.md](../../../../docs/OPERATIONAL-GUARANTEES.md) —
  delivery semantics, idempotency contract, DLQ, startup ordering
