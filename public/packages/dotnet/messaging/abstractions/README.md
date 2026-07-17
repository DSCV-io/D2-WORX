<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Messaging.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)

Transport-agnostic abstractions for the D² messaging stack. Domain code
references this package to mark messages with `[MqPub(MqMessages.X)]`, mark
handlers with `[MqSub(MqSubscriptions.X)]`, and depend on `IMessageBus` /
`IMessageIdempotencyStore` — without dragging in `RabbitMQ.Client` or any
specific transport.

The default impl is [`DcsvIo.D2.Messaging.RabbitMq`](../rabbitmq/README.md).
Alternate transports (where they exist) land as sibling csprojs and
use the same surface.

## How publishing + subscribing work end-to-end

1. **Spec files** — `contracts/mq-messages/mq-messages.spec.json` and
   `contracts/mq-subscriptions/mq-subscriptions.spec.json` declare every
   publishable message type and every subscription contract. Spec is the
   source of truth — exchange / encryption / queue topology / prefetch
   live there, not in code.
2. **Codegen** — the analyzer-only csproj `messaging/source-gen/` references this
   package as an analyzer. It emits two generated files into this assembly
   at build time, landing in the tracked `Generated/` directory (committed
   for inspection, IDE navigation, and PR diff review; re-emitted on every
   `dotnet build` from the spec; do not hand-edit):
   - `MqMessages.g.cs` — `public static partial class MqMessages` with one
     string `const` per spec entry, plus `MqMessagesRegistry.ByConstant`
     (`Dictionary<string, MqMessageDescriptor>`).
   - `MqSubscriptions.g.cs` — same shape for subscriptions.
3. **Producer side** — the message class carries `[MqPub(MqMessages.X)]`.
   The transport's resolver looks up the descriptor via the attribute → the
   codegen'd registry → exchange + encryption + default routing key.
4. **Consumer side** — the handler class carries `[MqSub(MqSubscriptions.X)]`.
   `services.AddD2SubscribersFromAssembly(typeof(MyHandler).Assembly)` scans,
   validates the handler's `BaseHandler<TSelf, TIn, Unit>` `TIn` matches the
   spec entry's `messageType`, and registers an `ISubscriberRegistration`
   for the transport's consumer host to pick up.

The runtime / operational details (per-delivery pipeline, DLQ shape, telemetry,
encryption posture, queue topology, channel lifecycle) live in
[`messaging/rabbitmq/README.md`](../rabbitmq/README.md) — the
canonical operational home. This package's job is the transport-agnostic
contract.

## Contract-level anti-patterns

These all fail loud at startup or first call. Listed here so you don't
discover them as a surprise during deploy.

- **Hand-registering `IMessageBus` or `ISubscriberRegistration` outside of
  `AddD2MessagingRabbitMq` / `AddD2SubscribersFromAssembly`.** The codegen +
  scanner are the only blessed paths.
- **Using `[MqPub]` / `[MqSub]` on a class whose CLR FQN doesn't match the
  spec entry's `messageType`.** The resolver / registrar hard-fail at build
  / startup. The spec-driven `[MqPub]` / `[MqSub]` attribute design exists
  specifically to make silent mismatches impossible.
- **Stuffing identity (`UserId` / `OrgId` / scopes) into the
  `x-d2-context` propagated header.** That header carries propagation-only
  context (request id, fingerprints, WhoIs hash). Identity rebuilds from
  the JWT at every sync hop; consumer-side handlers operate without one.
  **Headers stay plaintext at-rest — identity NEVER in headers.**

  > **Duplicated from [`messaging/rabbitmq/README.md` § Encryption posture](../rabbitmq/README.md#encryption-posture) for at-a-glance contract-side visibility. The canonical runtime-enforcement reference lives there — update both in lockstep.**

## Public surface

**`IMessageBus`** — `PublishAsync<TMessage>(message, options?, ct)` +
`WaitForReadyAsync(ct)`. The publish path resolves the type's descriptor
(throws on missing `[MqPub]` / unknown constant / FQN mismatch),
encrypts the body when the descriptor's encryption is non-`plaintext`,
attaches the canonical AMQP headers, and waits for publisher confirm
when configured. `WaitForReadyAsync` lets startup-time publishers (e.g.
KeyCustodian rotation announcements) gate on first connection landing.

**`MqPubAttribute`** — `[MqPub(MqMessages.X)]` on the message class.
Single-field attribute carrying the codegen'd constant. Default-deny: a
class without `[MqPub]` throws `InvalidOperationException` from the
publisher's resolver — every publishable type must have a deliberate spec
entry.

**`MqSubAttribute`** — `[MqSub(MqSubscriptions.X)]` on the handler class
(must derive from `BaseHandler<TSelf, TIn, Unit>`). Picked up by
`AddD2SubscribersFromAssembly`.

**`MqMessageDescriptor`** — codegen-emitted record carrying
`(Constant, MessageTypeName, Exchange, ExchangeType, Encryption,
EncryptionReason?, DefaultRoutingKey?)`. Sentinel `MqMessageDescriptor.PLAINTEXT`
constant for the encryption field; `IsPlaintext` convenience predicate.

**`MqSubscriptionDescriptor`** — codegen-emitted record carrying
`(Constant, MessageTypeName, QueueName, Pattern, RoutingKeyBinding,
Prefetch, Idempotency, TieredRetry?)`.

**`TieredRetryDescriptor`** — `(TimeSpan[] Tiers, int MaxAttempts)` for
the optional broker-level retry topology. Carried inside an
`MqSubscriptionDescriptor` when the spec entry has a `tieredRetry` block.

AMQP wire-protocol header constants live in
[`DcsvIo.D2.Headers.Amqp`](../../headers/amqp/README.md) (codegen-emitted
from `contracts/headers/headers.spec.json`). Cross-transport entries
(e.g. `traceparent`, `tracestate`, `x-d2-context`) appear at identical
wire values in [`DcsvIo.D2.Headers.Common`](../../headers/common/README.md).
Messages MUST NOT carry identity / raw PII in plaintext headers — only
routing, observability, and the small operational propagation subset
(`x-d2-context` is base64url-of-JSON of the hand-written `PropagatedContext`
record in `DcsvIo.D2.Context.Abstractions`). See
[`messaging/rabbitmq/README.md`](../rabbitmq/README.md) for the
full runtime + wire-format contract.

**`QueuePattern`** — enum: `CompetingConsumer` / `FanoutExclusiveAutoDelete`
/ `DurableShared`. Selects topology declared per subscriber. The transport's
host auto-suffixes `FanoutExclusiveAutoDelete` queue names with a per-process
token so multi-replica services don't race on the broker's exclusive-queue
lock.

**`PublisherOptions`** — per-publish overrides (confirm wait, routing key
override, exchange override, max attempts).

**`IMessageIdempotencyStore`** — opt-in dedup helper for subscribers. Default
impl in the RabbitMQ package backs onto `IDistributedCache`. Operators can
register their own (e.g. tests with an in-memory fake) — the startup-check
recognizes the operator-provided implementation.

**`SubscriberRegistry`** + **`ISubscriberRegistration`** — DI-singleton
aggregating every `AddD2Subscriber` / scanner-discovered registration.
Read once at startup by the transport's consumer host. Carries
`(HandlerType, MessageType, Descriptor, ResolvedQueueName)`.

**`SubscriberRegistrar`** — internal helper used by both the assembly
scanner and the explicit programmatic `AddD2Subscriber<,>` helper. Resolves
descriptor by constant, validates handler-message pairing, applies the
per-process queue-name suffix for `FanoutExclusiveAutoDelete`, and adds
the registration to DI.

**`MessagingFailures`** — `D2Result` validation-failure helpers (mirrors
`InputFailures` in the cache abstractions).

**`MessagingJsonOptions`** — shared `JsonSerializerOptions` used to
(de)serialize the wire body and the `x-d2-failure-reason` DLQ header
payload. CamelCase, omits null fields on write, no pretty-printing.

**`DlqFailureMetadata`** — JSON shape attached to dead-lettered messages via
the `x-d2-failure-reason` header. Five well-known causes:
`HANDLER_RESULT_FAILURE` / `HANDLER_EXCEPTION` / `DECRYPT_FAILURE` /
`DESERIALIZE_FAILURE` / `RETRIES_EXHAUSTED`.

### DI helpers

**`services.AddD2SubscribersFromAssembly(params Assembly[] assemblies)`** —
canonical registration path. Reflects over each assembly looking for
classes carrying `[MqSub]`; for each match, validates + registers via
`SubscriberRegistrar`.

**`services.AddD2Subscriber<THandler, TMessage>(MqSubscriptionDescriptor descriptor)`** —
programmatic escape hatch. Useful for integration tests that need
per-test queue names without polluting the production spec. Production
code should prefer the scanner.

## Dependencies

- `DcsvIo.D2.Result` — every op returns `D2Result<T>` / `D2Result`.
- `DcsvIo.D2.I18n.Abstractions` — typed `TKMessage` for failure surfaces.
- `DcsvIo.D2.Handler` — `BaseHandler<THandler, TIn, Unit>` constraint on
  subscribers.
- `DcsvIo.D2.Encryption` — `EncryptionDomains` constants are the legal
  values for `MqMessageDescriptor.Encryption` (alongside the `"plaintext"`
  literal).
- `DcsvIo.D2.Utilities` — `Falsey()` / `Truthy()` extensions.
- Build-time analyzer ref to `DcsvIo.D2.Messaging.SourceGen` (zero runtime cost).

## References

- [`messaging/rabbitmq/`](../rabbitmq/README.md) — default RabbitMQ
  impl + canonical runtime / wire-format / header / queue / encryption /
  delivery-semantics / DLQ / startup-ordering reference.
- [`messaging/source-gen/`](../source-gen/README.md) — codegen that
  emits the registries from the spec files; full spec format + diagnostic
  catalog + spec evolution rules.
