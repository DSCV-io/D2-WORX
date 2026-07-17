<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0009: Async messaging — transport-agnostic `IMessageBus`, default-deny `[MqPub]`/`[MqSub]`, encrypted payload frames, DLQ + idempotency

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: D2 shared libraries (backfilled)

## Context

D2 needs durable async event delivery between services (audit recording, keyring-refresh broadcast, notification dispatch, courier delivery). Forces:

1. **Transport coupling risk.** Direct `RabbitMQ.Client` use in domain code hard-links business logic to AMQP; a transport swap or test isolation then requires surgery across every publisher and subscriber.
2. **Silent misconfiguration.** Without a registry contract, a mistyped exchange name or unintended plaintext body compiles and surfaces only in production.
3. **PII on the wire.** Audit, notification, and courier payloads carry actor identities, addresses, financial figures, verification codes. TLS in transit is necessary but not sufficient: broker-at-rest, DLQ archives, and ops tooling all see plaintext unless the body itself is encrypted.
4. **At-least-once reality.** AMQP gives at-least-once; exactly-once is an illusion. The practical answers are idempotent handlers + a DLQ with a structured failure envelope.
5. **Cross-language parity.** TS services consume keyring events and ops reads DLQ headers; byte-level specs (frame layout, field names, cause strings, domain ids) must be shared by mechanical generation.

The spec-driven codegen decision (ADR-0002), the abstractions/implementation split (ADR-0006), errors-as-values (ADR-0003), and the caching abstractions (ADR-0008, which backs the idempotency store) are direct predecessors.

## Decision

### 1. Transport-agnostic `IMessageBus` + default-deny `[MqPub]`/`[MqSub]`

`IMessageBus.PublishAsync<TMessage>` (returns `D2Result`) is the only publish surface in domain code; broker, confirms, channel pooling, and retry are hidden behind it. Every publishable type must carry `[MqPub(MqMessages.X)]`; the resolver looks the constant up in the codegen-emitted `MqMessagesRegistry`. A type with no attribute, an unrecognized constant, or a CLR FQN that does not match the spec entry throws at the first publish — the **default-deny** posture (a silently-routed message is the worst configuration-drift failure). Subscriber handlers carry `[MqSub(MqSubscriptions.X)]`; `AddD2SubscribersFromAssembly` validates each derives from `BaseHandler` with the matching input type and registers it — missing constant or FQN mismatch is a loud throw at composition time. Specs `public/contracts/mq-messages/` + `public/contracts/mq-subscriptions/` drive `DcsvIo.D2.Messaging.SourceGen` to emit the constants, descriptor records, and registries into `messaging/abstractions`; domain code references only the constants and never imports `RabbitMQ.Client`. Three codegen-declared queue patterns: `CompetingConsumer`, `DurableShared`, `FanoutExclusiveAutoDelete` (per-replica, suffixed with a short per-process token — an 8-char UUIDv7 fragment).

### 2. Sensitive payloads encrypted in a self-describing binary frame; the encryption primitive is domain-agnostic

The wire body is either raw UTF-8 JSON or an AES-256-GCM encrypted frame. The choice lives entirely in `MqMessageDescriptor.Encryption`: an `EncryptionDomains` constant means encrypted; the literal `"plaintext"` means not. The frame (`encryption/core/EncryptionFrame.cs`) is `[version=1][kid_len][kid][nonce:12][ciphertext+16-byte GCM tag]` — self-describing: a receiver needs only the bytes and a keyring containing the kid, with no envelope wrapper or per-message key negotiation. The kid is also copied into an `x-d2-encryption-kid` AMQP header so DLQ ops can identify the archive key without decrypting. Frame byte offsets/constraints are spec-driven (`public/contracts/encryption-frame/`), with a TS mirror (`@dcsv-io/d2-encryption-abstractions`) guaranteeing cross-language byte-offset parity.

`DcsvIo.D2.Encryption` is a standalone library with no dependency on messaging/domains/key-fetching: a `PayloadCryptoKeyring` (JWKS-style, immutable, `IDisposable` zeroes key bytes via `CryptographicOperations.ZeroMemory`), `PayloadCrypto` (per-call `AesGcm` for thread safety), and the frame codec. AAD is bound to the keyring's context bytes, making cross-domain ciphertext replay structurally impossible at the AEAD layer. Keyrings register as keyed singletons (`AddD2EncryptionFor`); the bus resolves `IPayloadCrypto` keyed by the descriptor's domain in a transient per-publish scope. The `EncryptionDomains` catalog (`AUDIT`/`NOTIFICATIONS`/`COURIER` + a `PLAINTEXT` sentinel) is itself codegen'd, and the message source-gen cross-validates each `mq-messages` encryption value against it at build time, so a typo cannot route to a non-existent keyring. AMQP headers stay plaintext but carry only routing metadata + W3C trace context + the propagation-safe `x-d2-context` subset (ADR-0007) — never identity or payload fragments.

> #### Amendment (2026-07-06) — per-domain **sealed** mode: a per-consumer-service capability split
>
> A second per-domain encryption **mode** is added alongside the original (now called **symmetric**): **`sealed`**. A domain declares its mode in `public/contracts/encryption-domains` (`mode: symmetric | sealed`, default `symmetric`, back-compatible; a sealed domain also declares `consumerService`, the single decryptor's ServiceId). `audit`, `notifications`, and `courier` flip to `sealed`; `plaintext` stays a no-encrypt sentinel. The mode is a single-source domain fact read from the generated catalog (`EncryptionDomainModes` / `MqMessageDescriptor.IsSealed` + `.ConsumerService`) — never a second generated surface.
>
> - **Construction.** Ephemeral-static ECDH-ES over P-256 → HKDF-SHA256 (service-bound, length-delimited `info`; salt = AAD = `UTF-8(consumerServiceId)`; frozen values) → per-message AES-256-GCM. Forward secrecy comes from the per-message ephemeral keypair. One auto-provisioned ECDH keypair per consumer SERVICE (distinct from its mTLS leaf — key separation), rotated on the KeyCustodian lifecycle.
> - **Wire.** A **version-2** frame (`[version=2][recipient_kid_len][recipient_kid][eph_pub_len:2 BE][eph_pub:SPKI][nonce:12][ciphertext+tag]`), version-dispatching decoders (a v1 frame arriving on a sealed domain is a hard version-mismatch → DLQ). `x-d2-encryption-kid` carries the recipient kid for DLQ triage. The v2 layout is spec-driven (`public/contracts/encryption-frame`) with a byte-identical TS mirror, KAT-pinned both directions.
> - **Capability split.** `IPayloadSealer` (public-key, **encrypt-only** — no `Open` member) vs `IPayloadOpener` (private-key, **decrypt-only** — no `Seal` member). A producer holding only a sealer **cannot open ANY sealed frame, including its own output**. The private key leaves KeyCustodian only to its owning service, selected purely by authenticated mTLS peer identity (a targetless op) — the hard wall is KeyCustodian-side, not the DI shape.
> - **Auto-wiring.** The composer resolves domain → `consumerService` → the keyed sealer (publish) / opener (consume), keyed by the recipient SERVICE (two sealed domains sharing a consumer share one sealer/opener). Zero per-message hand-code. One spec-driven registration call per service (`AddD2SealedEncryptionViaKeyCustodian`) wires a sealer for every sealed-domain consumer (lazy public-key fetch) and the private-key opener only when the service is itself a sealed consumer; a rabbitmq-lib boot check crashes a host whose sealed subscriber has no matching opener (the forgotten-call net). The TS twin enforces the same fusion structurally (a compile-time publisher type-witness + a runtime default-deny double lock — referenced here, not co-specified).

### 3. At-least-once: idempotency store + DLQ + tiered retry + `x-death` exhaustion

A stable UUIDv7 `message-id` is generated once per publish and reused across retry attempts; body bytes are composed once and reused (a retry does not re-encrypt with a new nonce). Publisher confirms are on by default (bounded retry via a transient classifier distinguishing retriable broker conditions from terminal ones). Subscriptions declaring `idempotency: true` get an `IMessageIdempotencyStore` pre-check; the default `CacheIdempotencyStore` uses `IDistributedCache` (ADR-0008) with a 24-hour TTL and `SetNx` semantics — check before dispatch, mark after success before `BasicAck`; read-path failure is fail-open (handlers are at-least-once-safe), write-path failure is NACK-to-DLQ. `IdempotencyStartupCheck` hard-fails host startup if a subscription declares idempotency but no store/cache is registered (a silent no-op on a safety feature is the worst default). Every primary queue gets a DLX + DLQ; on any handler-boundary failure the consumer republishes the original body to the DLX with an `x-d2-failure-reason` header carrying a JSON `DlqFailureMetadata` record, then acks the original. `DlqFailureMetadata` property names are codegen-bound (`public/contracts/dlq-failure-metadata/`), and `detail` is PII-safe by construction (populated only from result message keys, null for exceptions; log delegates emit only `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)`, never `ex.Message` — see ADR-0011). Tiered retry stands up per-tier TTL queues + a return exchange; the consumer enforces a hard total-attempt cap by summing `x-death` counts (filtered to `expired`/`rejected` reasons) and routes straight to DLQ with cause `RETRIES_EXHAUSTED` when the cap is met.

### 4. Spec-driven registries and catalogs — cross-language

All contracts that cross an assembly or language boundary live in `public/contracts/` and are consumed only through codegen output: `mq-messages` / `mq-subscriptions` (constants + descriptors + registries), `encryption-domains` and `encryption-frame` (both with TS mirrors). Hand-writing any of these constants is forbidden.

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
- For a **symmetric** domain, encryption is domain-agnostic: every publisher/consumer of the domain shares its keyring; there is no encrypt-only/decrypt-only capability split within the domain (payload minimization is the compensating control). This bullet is now SCOPED to symmetric domains — a **sealed** domain (2026-07-06 amendment) DOES carry a per-consumer-service capability split (sealer vs opener), so it may carry fully denormalized content (no consumer-side DB lookup / callback). Added sealed negatives: a per-message ECDH agreement (CPU cost — benchmark a high-volume sealed domain); a single-point decrypt authority per consumer service (the key-rotation / compromise runbook applies); first-seal lazy-provision latency; and a second frame version every decoder must handle.
- A **sealed**-mode domain has NO symmetric keyring/lifecycle existence (2026-07-06 amendment): `audit` / `notifications` / `courier` leave the KeyCustodian symmetric payload catalog entirely (one-way by construction — their per-service sealing keys live under the `seal:<serviceId>` family instead). The symmetric machinery (`getKeyring` op, keyring authority, boot validator, consumer runtime) is preserved domain-generically for a future symmetric-mode domain; the payload catalog is empty until one is declared.
- `FanoutExclusiveAutoDelete` queues are non-durable: a broker restart drops in-flight messages for those subscriptions (intentional for keyring-refresh, which re-syncs on reconnect, but must be documented per subscription).

## Alternatives considered

**Direct `RabbitMQ.Client` use in domain code.** Domain services would import AMQP types, requiring a live/mock broker for every publisher/subscriber test, and transport evolution would touch every service. The `IMessageBus` + attribute contract keeps transport behind the library boundary.

**Envelope wrapper format.** A typed envelope (`{type, correlationId, payload}`) co-locates routing metadata and business data but adds parse overhead, inflates size, and forces every decoder to understand the envelope before deserializing. The chosen approach puts routing metadata in AMQP headers (broker reads natively) and keeps the body as the serialized message — or, when encrypted, the opaque frame.

**Encrypting at rest only, or TLS only.** Storage-layer or TLS-only encryption does not protect broker memory, DLQ archive blobs, log captures, or management-API observers. Body-level AES-256-GCM means plaintext is never visible to the broker; TLS remains required for defense in depth but is not the primary payload-confidentiality control.

**No idempotency store / exactly-once illusion.** Exactly-once is unachievable on AMQP without a transactional log broker. The alternative to a store is requiring every handler to implement transactional dedup (a DB UNIQUE on `message_id`) — the right answer for handlers that already transact, and the store's docs note this. The cache-backed store exists for the idempotent-side-effect case (external API calls, email sends) where a DB transaction does not help; offering nothing would force every subscriber to re-implement a 24-hour keyed dedup cache.

## References

- `public/packages/dotnet/messaging/abstractions/` — `IMessageBus.cs`, `MqPubAttribute.cs`/`MqSubAttribute.cs`, descriptor records, `SubscriberRegistrar.cs`/`SubscriberRegistry.cs`, `IMessageIdempotencyStore.cs`, `DlqFailureMetadata.cs`, `TieredRetryDescriptor.cs`, `QueuePattern.cs`.
- `public/packages/dotnet/messaging/rabbitmq/` — the README (canonical async-messaging reference) + `Encryption/EncryptedBodyComposer.cs`, `MessageWireResolver.cs`, `Publishing/RabbitMqMessageBus.cs`, `TransientPublishClassifier.cs`, `Idempotency/CacheIdempotencyStore.cs` + `IdempotencyStartupCheck.cs`, `Subscribing/SubscriberChannel.cs` (`x-death` enforcement + DLQ republish).
- `public/packages/dotnet/encryption/core/` — `IPayloadCrypto.cs`, `PayloadCrypto.cs`, `PayloadCryptoKeyring.cs`, `EncryptionFrame.cs`, `EncryptionServiceCollectionExtensions.cs`, README.
- `public/contracts/mq-messages/`, `public/contracts/mq-subscriptions/`, `public/contracts/encryption-frame/`, `public/contracts/encryption-domains/`.
- [ADR-0002](0002-spec-driven-codegen.md) (codegen), [ADR-0006](0006-abstractions-implementation-split.md) (abstractions split + keyed DI), [ADR-0003](0003-d2result-errors-as-values.md) (`IMessageBus` returns `D2Result`), [ADR-0008](0008-caching-marker-interfaces.md) (`IDistributedCache` backs `CacheIdempotencyStore`), [ADR-0007](0007-request-context-propagation.md) (`x-d2-context` header), [ADR-0011](0011-pii-redaction-logging-safety.md) (PII-safe log delegates).
