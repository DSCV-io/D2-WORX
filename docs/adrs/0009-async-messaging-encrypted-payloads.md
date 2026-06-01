<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0009: Async messaging — transport-agnostic `IMessageBus`, default-deny `[MqPub]`/`[MqSub]`, encrypted payload frames, DLQ + idempotency

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: Phase 0 — shared libraries (backfilled)

## Context

D²-WORX needs durable async event delivery between services (audit recording, keyring-refresh broadcast, notification dispatch, courier delivery). Forces:

1. **Transport coupling risk.** Direct `RabbitMQ.Client` use in domain code hard-links business logic to AMQP; a transport swap or test isolation then requires surgery across every publisher and subscriber.
2. **Silent misconfiguration.** Without a registry contract, a mistyped exchange name or unintended plaintext body compiles and surfaces only in production.
3. **PII on the wire.** Audit, notification, and courier payloads carry actor identities, addresses, financial figures, verification codes. TLS in transit is necessary but not sufficient: broker-at-rest, DLQ archives, and ops tooling all see plaintext unless the body itself is encrypted.
4. **At-least-once reality.** AMQP gives at-least-once; exactly-once is an illusion. The practical answers are idempotent handlers + a DLQ with a structured failure envelope.
5. **Cross-language parity.** TS services consume keyring events and ops reads DLQ headers; byte-level specs (frame layout, field names, cause strings, domain ids) must be shared by mechanical generation.

The spec-driven codegen decision (ADR-0002), the abstractions/implementation split (ADR-0006), errors-as-values (ADR-0003), and the caching abstractions (ADR-0008, which backs the idempotency store) are direct predecessors.

## Decision

### 1. Transport-agnostic `IMessageBus` + default-deny `[MqPub]`/`[MqSub]`

`IMessageBus.PublishAsync<TMessage>` (returns `D2Result`) is the only publish surface in domain code; broker, confirms, channel pooling, and retry are hidden behind it. Every publishable type must carry `[MqPub(MqMessages.X)]`; the resolver looks the constant up in the codegen-emitted `MqMessagesRegistry`. A type with no attribute, an unrecognized constant, or a CLR FQN that does not match the spec entry throws at the first publish — the **default-deny** posture (a silently-routed message is the worst configuration-drift failure). Subscriber handlers carry `[MqSub(MqSubscriptions.X)]`; `AddD2SubscribersFromAssembly` validates each derives from `BaseHandler` with the matching input type and registers it — missing constant or FQN mismatch is a loud throw at composition time. Specs `contracts/mq-messages/` + `contracts/mq-subscriptions/` drive `D2.Shared.Messaging.SourceGen` to emit the constants, descriptor records, and registries into `messaging/abstractions`; domain code references only the constants and never imports `RabbitMQ.Client`. Three codegen-declared queue patterns: `CompetingConsumer`, `DurableShared`, `FanoutExclusiveAutoDelete` (per-replica, suffixed with a short per-process token — an 8-char UUIDv7 fragment).

### 2. Sensitive payloads encrypted in a self-describing binary frame; the encryption primitive is domain-agnostic

The wire body is either raw UTF-8 JSON or an AES-256-GCM encrypted frame. The choice lives entirely in `MqMessageDescriptor.Encryption`: an `EncryptionDomains` constant means encrypted; the literal `"plaintext"` means not. The frame (`encryption/core/EncryptionFrame.cs`) is `[version=1][kid_len][kid][nonce:12][ciphertext+16-byte GCM tag]` — self-describing: a receiver needs only the bytes and a keyring containing the kid, with no envelope wrapper or per-message key negotiation. The kid is also copied into an `x-d2-encryption-kid` AMQP header so DLQ ops can identify the archive key without decrypting. Frame byte offsets/constraints are spec-driven (`contracts/encryption-frame/`), with a TS mirror (`@d2/encryption-abstractions`) guaranteeing cross-language byte-offset parity.

`D2.Shared.Encryption` is a standalone library with no dependency on messaging/domains/key-fetching: a `PayloadCryptoKeyring` (JWKS-style, immutable, `IDisposable` zeroes key bytes via `CryptographicOperations.ZeroMemory`), `PayloadCrypto` (per-call `AesGcm` for thread safety), and the frame codec. AAD is bound to the keyring's context bytes, making cross-domain ciphertext replay structurally impossible at the AEAD layer. Keyrings register as keyed singletons (`AddD2EncryptionFor`); the bus resolves `IPayloadCrypto` keyed by the descriptor's domain in a transient per-publish scope. The `EncryptionDomains` catalog (`AUDIT`/`NOTIFICATIONS`/`COURIER` + a `PLAINTEXT` sentinel) is itself codegen'd, and the message source-gen cross-validates each `mq-messages` encryption value against it at build time, so a typo cannot route to a non-existent keyring. AMQP headers stay plaintext but carry only routing metadata + W3C trace context + the propagation-safe `x-d2-context` subset (ADR-0007) — never identity or payload fragments.

### 3. At-least-once: idempotency store + DLQ + tiered retry + `x-death` exhaustion

A stable UUIDv7 `message-id` is generated once per publish and reused across retry attempts; body bytes are composed once and reused (a retry does not re-encrypt with a new nonce). Publisher confirms are on by default (bounded retry via a transient classifier distinguishing retriable broker conditions from terminal ones). Subscriptions declaring `idempotency: true` get an `IMessageIdempotencyStore` pre-check; the default `CacheIdempotencyStore` uses `IDistributedCache` (ADR-0008) with a 24-hour TTL and `SetNx` semantics — check before dispatch, mark after success before `BasicAck`; read-path failure is fail-open (handlers are at-least-once-safe), write-path failure is NACK-to-DLQ. `IdempotencyStartupCheck` hard-fails host startup if a subscription declares idempotency but no store/cache is registered (a silent no-op on a safety feature is the worst default). Every primary queue gets a DLX + DLQ; on any handler-boundary failure the consumer republishes the original body to the DLX with an `x-d2-failure-reason` header carrying a JSON `DlqFailureMetadata` record, then acks the original. `DlqFailureMetadata` property names are codegen-bound (`contracts/dlq-failure-metadata/`), and `detail` is PII-safe by construction (populated only from result message keys, null for exceptions; log delegates emit only `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)`, never `ex.Message` — see ADR-0011). Tiered retry stands up per-tier TTL queues + a return exchange; the consumer enforces a hard total-attempt cap by summing `x-death` counts (filtered to `expired`/`rejected` reasons) and routes straight to DLQ with cause `RETRIES_EXHAUSTED` when the cap is met.

### 4. Spec-driven registries and catalogs — cross-language

All contracts that cross an assembly or language boundary live in `contracts/` and are consumed only through codegen output: `mq-messages` / `mq-subscriptions` (constants + descriptors + registries), `encryption-domains` and `encryption-frame` (both with TS mirrors). Hand-writing any of these constants is forbidden.

## Consequences

**Positive.**

- Domain services publish via `IMessageBus` and subscribe via `[MqSub]` with no AMQP knowledge; swapping transport replaces only the RabbitMQ assembly.
- A message lacking `[MqPub]`, referencing a nonexistent constant, or with an FQN mismatch fails loud at first publish (or composition time for subscribers) — silent misrouting eliminated.
- PII-bearing bodies are encrypted in transit, at-rest in the broker, and in DLQ archives; a broker compromise does not expose payload content. Headers (broker-plaintext) carry only routing + trace context.
- The self-describing frame supports key rotation: in-flight messages under the retiring kid decrypt from the same keyring during overlap; the broker needs no rotation awareness.
- The 24-hour idempotency window covers the realistic redelivery window and is bounded; the startup hard-fail prevents silent safety no-ops.
- `DlqFailureMetadata` gives ops structured triage (cause, errorCode, traceId, archive-key kid) without decrypting the body.

**Negative / risks.**

- The default-deny `[MqPub]` check is runtime, not compile-time: a missing attribute fails at first publish if the path is not exercised by integration tests.
- The cache-backed idempotency store couples idempotency to the cache tier: a Redis outage during the write path causes NACK-to-DLQ for already-processed messages — ops must monitor ack-failure metrics.
- Per-call `AesGcm` is concurrency-safe but allocates/frees native memory per op; at high sustained throughput this may add GC pressure — benchmark before scaling a high-volume encrypted domain.
- Encryption is domain-agnostic: every publisher/consumer of a domain shares its keyring; there is no encrypt-only/decrypt-only capability split within a domain (payload minimization is the compensating control).
- `FanoutExclusiveAutoDelete` queues are non-durable: a broker restart drops in-flight messages for those subscriptions (intentional for keyring-refresh, which re-syncs on reconnect, but must be documented per subscription).

## Alternatives considered

**Direct `RabbitMQ.Client` use in domain code.** Domain services would import AMQP types, requiring a live/mock broker for every publisher/subscriber test, and transport evolution would touch every service. The `IMessageBus` + attribute contract keeps transport behind the library boundary.

**Envelope wrapper format.** A typed envelope (`{type, correlationId, payload}`) co-locates routing metadata and business data but adds parse overhead, inflates size, and forces every decoder to understand the envelope before deserializing. The chosen approach puts routing metadata in AMQP headers (broker reads natively) and keeps the body as the serialized message — or, when encrypted, the opaque frame.

**Encrypting at rest only, or TLS only.** Storage-layer or TLS-only encryption does not protect broker memory, DLQ archive blobs, log captures, or management-API observers. Body-level AES-256-GCM means plaintext is never visible to the broker; TLS remains required for defense in depth but is not the primary payload-confidentiality control.

**No idempotency store / exactly-once illusion.** Exactly-once is unachievable on AMQP without a transactional log broker. The alternative to a store is requiring every handler to implement transactional dedup (a DB UNIQUE on `message_id`) — the right answer for handlers that already transact, and the store's docs note this. The cache-backed store exists for the idempotent-side-effect case (external API calls, email sends) where a DB transaction does not help; offering nothing would force every subscriber to re-implement a 24-hour keyed dedup cache.

## References

- `server/shared/dotnet/messaging/abstractions/` — `IMessageBus.cs`, `MqPubAttribute.cs`/`MqSubAttribute.cs`, descriptor records, `SubscriberRegistrar.cs`/`SubscriberRegistry.cs`, `IMessageIdempotencyStore.cs`, `DlqFailureMetadata.cs`, `TieredRetryDescriptor.cs`, `QueuePattern.cs`.
- `server/shared/dotnet/messaging/rabbitmq/` — the README (canonical async-messaging reference) + `Encryption/EncryptedBodyComposer.cs`, `MessageWireResolver.cs`, `Publishing/RabbitMqMessageBus.cs`, `TransientPublishClassifier.cs`, `Idempotency/CacheIdempotencyStore.cs` + `IdempotencyStartupCheck.cs`, `Subscribing/SubscriberChannel.cs` (`x-death` enforcement + DLQ republish).
- `server/shared/dotnet/encryption/core/` — `IPayloadCrypto.cs`, `PayloadCrypto.cs`, `PayloadCryptoKeyring.cs`, `EncryptionFrame.cs`, `EncryptionServiceCollectionExtensions.cs`, README.
- `contracts/mq-messages/`, `contracts/mq-subscriptions/`, `contracts/encryption-frame/`, `contracts/encryption-domains/`.
- [ADR-0002](0002-spec-driven-codegen.md) (codegen), [ADR-0006](0006-abstractions-implementation-split.md) (abstractions split + keyed DI), [ADR-0003](0003-d2result-errors-as-values.md) (`IMessageBus` returns `D2Result`), [ADR-0008](0008-caching-marker-interfaces.md) (`IDistributedCache` backs `CacheIdempotencyStore`), [ADR-0007](0007-request-context-propagation.md) (`x-d2-context` header), [ADR-0011](0011-pii-redaction-logging-safety.md) (PII-safe log delegates).
