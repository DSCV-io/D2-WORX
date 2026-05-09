<!--
Copyright (c) DCSV. All rights reserved.
-->

# OPERATIONAL-GUARANTEES.md — D²-WORX Correctness & Safety

How D²-WORX prevents duplicate actions, ensures idempotency, and maintains correct behavior across services, instances, and scheduled jobs.

---

## Idempotency

### HTTP Idempotency (Edge)

- **Mechanism:** `Idempotency-Key` header middleware on Edge
- **Storage:** Redis `SET NX` with 24-hour TTL — shared across all Edge instances
- **Behavior:** First request with a given key is processed normally and the response is cached. Subsequent requests with the same key return the cached response without re-executing the handler
- **Scope:** External-facing mutations only. Internal service-to-service calls (gRPC, RabbitMQ) use different guarantees
- **Library:** `Idempotency.Default`

### Content-Addressable Entities

- `Location` and `WhoIs` value objects use SHA-256 content-addressable hash IDs (64-char hex)
- Identical input always produces the same ID — app-level deduplication (query existing by hash IDs, insert only new ones) makes creation inherently idempotent
- No duplicate data regardless of how many times the same value is created

### Contacts

- Contacts use UUIDv7 IDs (not content-addressable) but are immutable once created
- "Update" is modeled as create-new → repoint owner reference → delete-old, preserving cache validity across all consumers
- Owner-side junction metadata (label, isPrimary) can be updated in place since it's owner-owned, not contact data

---

## Scheduled Jobs

### No Duplicate Execution

Each maintenance job uses a **Redis distributed lock** (`SET NX PX`) to ensure only one instance processes a job at any given time:

**Execution flow:** Cron trigger → HTTP POST to Edge (service-identity JWT auth) → Edge forwards via gRPC to the owning service → Service handler acquires Redis lock → Batch delete loop → Release lock → Return result

**If the lock is held:** The handler returns early with a success result (no error, no retry). The job is simply skipped on that instance. This is safe because all jobs are periodic cleanup — the next scheduled run will process any remaining records.

### Batch Processing

All purge/cleanup jobs use chunked processing (default 500 records per batch), avoiding long-running transactions and large `IN` clauses. Batch size is configurable via the Options pattern.

### Staggered Scheduling

Jobs are staggered (typically 15 minutes apart) to avoid resource contention.

---

## Rate Limiting

### No Per-Instance Counters

All rate limiting uses **Redis-backed shared counters** — never per-process `Map` / `ConcurrentDictionary` objects.

- **Library:** `RateLimit.Default`
- **Algorithm:** Sliding window approximation (two fixed-window counters + weighted average)
- **Atomicity:** Redis `INCR` + `TTL` — atomic operations, no cross-instance coordination needed
- **Dimensions:** ClientFingerprint (100/min), IP (5,000/min), City (25,000/min), Country (100,000/min)
- **Blocking:** If ANY dimension exceeds threshold → block for 5 minutes
- **Fail-open:** If Redis is down or WhoIs unavailable, requests pass through (availability over strictness)
- **Trusted bypass:** Service-identity authenticated requests bypass all rate limit dimensions

### Country Whitelist

US, CA, GB are exempt from country-level blocking to avoid false positives from CDN/proxy aggregation.

---

## Session Consistency

### Multi-Instance Safety

| Tier                | Storage   | Behavior                                                 |
| ------------------- | --------- | -------------------------------------------------------- |
| Cookie cache (5min) | In cookie | Travels with the request — any instance can decode       |
| Redis               | Shared    | Any instance queries the same Redis — instant revocation |
| PostgreSQL (`auth_db`) | Shared | Dual-write ensures durability + audit trail              |

**No sticky sessions required.** Any instance can handle any request. Session revocation propagates instantly via Redis. The only lag is the cookie cache TTL (~5 minutes max on the device that has the session cached).

### JWT Validation

- JWTs are self-contained — any instance validates with the cached JWKS public key
- JWKS endpoint at the OIDC-canonical **`/.well-known/jwks.json`** — all consumers fetch from the same source; key rotation propagates via JWKS refresh
- Key rotation handled by the KeyCustodian — 90-day cadence, dual-key window during grace
- JWT expiration: 15 minutes — limits the window of a revoked-but-still-valid token

---

## Messaging (RabbitMQ)

> Wire-format and topology details live in [docs/MESSAGING.md](MESSAGING.md). This section covers the operational invariants every consumer + producer must respect.

### Spec-Driven Topology

- Every publishable type and every subscription contract is declared in `contracts/mq-messages/mq-messages.spec.json` and `contracts/mq-subscriptions/mq-subscriptions.spec.json`.
- Codegen emits immutable `MqMessagesRegistry` / `MqSubscriptionsRegistry` runtime maps + `MqMessages.X` / `MqSubscriptions.X` constants. The publisher / subscriber sides resolve their wire contract from the codegen'd registries — there is no per-call configuration of exchange / queue / encryption / routing.
- Message classes carry `[MqPub(MqMessages.X)]`; handler classes carry `[MqSub(MqSubscriptions.X)]`. The `MessageWireResolver` and `SubscriberRegistrar` hard-fail on a stale attribute / FQN mismatch — silent misroutes were the entire reason for the spec-driven pivot.

### Queue Patterns

| Pattern | When | Durability |
|---|---|---|
| `CompetingConsumer` | commands / requests delivered to one consumer in the fleet | durable, non-exclusive, non-autodelete |
| `DurableShared` | persistent events (audit, file lifecycle) — survives restart, multiple consumers OK | durable, non-exclusive, non-autodelete |
| `FanoutExclusiveAutoDelete` | per-instance broadcast (cache invalidation, keyring refresh) — every replica gets every message | non-durable, exclusive, auto-delete; consumer host auto-suffixes the queue name with a per-process token to avoid the broker's exclusive-queue lock collision in a multi-replica deployment |

### Delivery Semantics

- **At-least-once** is the default and only contract. Handlers MUST be idempotent. The consumer's `IMessageIdempotencyStore` (opt-in per subscription via `idempotency: true` in the spec) provides a 24-hour dedup window backed by `IDistributedCache`, but the handler's own state machine is the source of truth.
- **`message-id` is UUIDv7**, generated ONCE per `PublishAsync` call so a publisher retry of an unconfirmed publish doesn't bypass the consumer's dedup window.
- **Manual ack only.** `autoAck: false` everywhere. Ack happens AFTER the handler's work commits + the idempotency mark is written, not before.
- **Crash between mark + ack** is safe: the next redelivery sees the mark and skips the handler. The opposite ordering would let a handler-completed-but-pre-mark crash cause a duplicate run, which the redelivery couldn't detect.

### Idempotency Contract

When `idempotency: true` in the subscription spec:

1. The consumer pre-checks `IMessageIdempotencyStore.HasSeenAsync(messageId)` before invoking the handler. Hit → ack-and-skip.
2. After a successful handler run, the consumer writes the mark via `MarkSeenAsync(messageId)` BEFORE `BasicAck`.
3. **Read-path** failure (HasSeen returns ServiceUnavailable) → **fail-open**: process the message anyway. Handlers MUST be at-least-once-safe; rejecting messages during a transient store outage on the read path is the wrong tradeoff.
4. **Write-path** failure (MarkSeen returns ServiceUnavailable) → **NACK to DLQ**. Acking without a written mark would silently leave the dedup window unguarded; a redelivery of the already-processed message would re-run the handler. The `ack_failures` counter + `IdempotencyMarkFailed` log surface the store-degradation window so the operator can react.
5. Startup-check: `IdempotencyStartupCheck` hard-fails host startup when any subscription declares `idempotency: true` but neither `IDistributedCache` nor an operator-provided `IMessageIdempotencyStore` is registered. Silent no-op on a safety feature is the worst possible default.

### DLQ + Failure-Reason Header

Every queue has a paired DLX + DLQ declared by the topology declarer. On any handler / boundary failure:

1. The consumer **republishes** the original body to `{queue}.dlx` with an `x-d2-failure-reason` header attached (JSON-encoded `DlqFailureMetadata`: `cause`, `errorCode`, `attemptCount`, `traceId`).
2. The original delivery is `BasicAck`'d (so the broker's `x-dead-letter-exchange` argument doesn't ALSO route a header-less copy alongside).
3. A dedicated republish channel keeps publish state out of the consume channel's delivery queue.
4. Republish failure → falls back to `BasicNack-no-requeue` (header-less copy lands in DLQ via `x-dead-letter-exchange`) + emits `d2.messaging.rabbitmq.dlq_republish_failures` counter.

**PII discipline**: `DlqFailureMetadata.detail` is NEVER built from `exception.Message` (handler code can interpolate user input). It's `null` for exception-cause failures and the joined translation-token keys for result-cause failures. Log delegates that take an `Exception` log only the type FullName + first stack frame — no `ex.Message`, no full trace.

### Tiered Retry (optional)

When a subscription declares a `tieredRetry` block, the topology declarer stands up retry-tier exchanges + queues with TTLs that route TTL-expired messages back to the primary queue. The consumer's `x-death`-driven attempt counter caps total cycles via `MaxAttempts` — a permanently-broken payload that exhausts retries routes direct to DLQ with cause `RETRIES_EXHAUSTED` (no further handler invocation).

### Payload Encryption

All sensitive RabbitMQ payloads are encrypted at the publisher and decrypted at the consumer via `D2.Shared.Encryption`:

- AES-256-GCM, JWKS-style multi-key keyring (overlap supported for graceful rotation).
- Per encryption-domain keys (`Audit`, `Notifications`, `Courier`, ...). The descriptor's `encryption` field is the domain string (or the literal `"plaintext"`); the publisher resolves `IPayloadCrypto` keyed by that string.
- Plaintext entries MUST document a `encryptionReason` in the spec — surfaces in code review when the question "why isn't this encrypted?" comes up.
- Routing keys + headers stay cleartext (broker needs them; broker stores them as plaintext at-rest). Only the body is wrapped in the encryption frame.

### Cross-Hop Trace + Context Propagation

- Producer writes the full W3C `traceparent` (`00-{traceId}-{spanId}-{flags}`) + optional `tracestate` headers. Consumer parses via `ActivityContext.TryParse` and starts a `Consumer`-kind span whose parent is the publish span — cross-hop trace assembly works in any OTel backend without bespoke linking.
- `x-d2-context` carries a base64url-of-JSON-encoded `PropagatedContext` (request id, request path, fingerprints, WhoIs hash) — propagation-only, never identity. Identity (UserId / OrgId / Scopes) rebuilds from the JWT at every sync hop; consumer-side handlers operate without one.
- `PropagatedContextSerializer.TryDecode` enforces both a wire-level cap (`MAX_HEADER_LENGTH = 2 KiB`) AND per-field length caps (RequestPath ≤ 2048, RequestId ≤ 256, fingerprints ≤ 512, WhoIsHashId ≤ 128). A forged header that fits under the wire cap but contains an oversized single field is dropped wholesale — propagation is opportunistic, never required, so a partial/sanitized context is wrong; a null context is right.

### Startup Ordering Hooks

- `IMessageBus.WaitForReadyAsync(ct)` — awaits first connection landing. Use from background hosted services that publish at startup (e.g. KeyCustodian rotation announcement) so a startup-race-with-broker doesn't surface as `ServiceUnavailable` on the first call.
- `ConsumerHostedService.ReadyTask` — completes when every subscriber channel has finished `BasicConsume`. Integration tests + ordered-startup callers can wait on it before publishing.
- `TopologyHostedService` logs `TopologyLog.DeclarationFailed` on background-task faults so a `PRECONDITION_FAILED` (e.g. queue declared with mismatched arguments across deploys) is visible in operator logs instead of vanishing into `TaskScheduler.UnobservedTaskException`.

### Channel Lifecycle

- `BoundedChannelPool` evicts publisher channels idle longer than `ChannelPoolOptions.IdleTtl` (default 5 min) on the next `AcquireAsync` — bounds broker-side state (heartbeats, confirm-tracking) under low-traffic services.
- `SubscriberChannel.DisposeAsync` tracks in-flight handler callbacks via `Interlocked` counter; on disposal, unsubscribes + cancels the consumer, then bounded-spin-waits up to 30s for in-flight handlers to drain before closing the channel. Slow handlers don't hold the host shutdown indefinitely; well-behaved ones get a clean ack-and-exit.

### Composition-Time Validation

`AddD2MessagingRabbitMq` validates `RabbitMqPublisherOptions.WaitForConfirm == true` implies `ChannelPoolOptions.PublisherConfirmsEnabled == true` at startup (`ValidateOnStart`). A mismatch is a startup failure, not a silent fire-and-forget surprise on what the operator believed was a confirmed publish.

---

## Cross-Service Updates (SAGA)

For mutations that must touch state in multiple services, D²-WORX uses a **synchronous SAGA helper** rather than choreographed events for foreground writes that need immediate user-visible results.

### Ordering & Compensation

1. **Compensable step first** — write the data that's safest to roll back if a later step fails. If this step fails, abort and surface the error; nothing else has changed.
2. **Subsequent step(s)** — write the data that anchors the cross-service state. If this fails, attempt to **compensate** the earlier step (delete or revert the just-written record) so cross-service state stays consistent.
3. **Compensation failure** is escalated via `logger.fatal` so an operator can manually reconcile. The handler still returns the original failure to the caller — the user's request is not silently "succeeded" when state is inconsistent.

### Why Synchronous SAGA, Not Eventual

These flows return a result the user expects to see immediately (the new phone number, the new contact). Choreographed events would require optimistic UI + eventual consistency, which is the wrong tradeoff for foreground edits. SAGA bounded by a single request gives "all or rolled-back" semantics with bounded latency.

### When NOT a SAGA

Irreversible flows (e.g., user-deletion anonymize) are **not** SAGAs — they are fire-and-forget fanouts. Anonymization has no meaningful compensation (the deletion grace window closed; downstream services own their idempotent consumers rather than coordinating rollback).

---

## Request Enrichment

### Deterministic Output

- Request enrichment is **stateless middleware** — resolves client IP, computes fingerprint, calls the in-process WhoIs cache (Edge only)
- Identical input always produces identical output regardless of which instance processes the request
- No instance affinity required
- Downstream services receive the resolved WhoIs via the `X-D2-WhoIs` header (no per-service WhoIs cache — Edge is the single source)

---

## Multi-Instance Scaling Checklist

When adding a new service or endpoint, verify:

- [ ] **Rate limiting** — Use `RateLimit.Default` (Redis-backed), never per-process counters
- [ ] **Idempotency** — Use Redis-backed `Idempotency.Default` for external-facing mutations
- [ ] **Session/auth** — Validate JWTs via JWKS (no instance affinity), sessions via Redis
- [ ] **Local caches** — In-memory caches are per-instance (fine for read-heavy, TTL-bounded data). Correctness must not depend on cache consistency across instances
- [ ] **Background jobs** — Use Redis distributed locks (`SET NX`). Return early if lock is held
- [ ] **Cache invalidation** — Use fanout exchanges with exclusive auto-delete queues (not competing consumers)
- [ ] **Connection strings** — Externalized via `.env.local` / `.env.secrets`, not hardcoded
- [ ] **DB constraints** — Catch unique violations (PG `23505`) gracefully — return 409, not 500
- [ ] **Migrations** — Never hand-write migration SQL / snapshot / `__EFMigrationsHistory` edits. Always use `dotnet ef migrations add <Name>`. Multi-replica safety via PG advisory lock at the startup migrator
- [ ] **Cross-service mutations** — Use SAGA pattern (per "Cross-Service Updates" above) for foreground multi-service writes. Don't invent new SAGA flows without review
- [ ] **Encryption** — Sensitive payloads on RabbitMQ marked with `[Encrypted(Domain.X)]` (auto-encrypts via `D2.Shared.Encryption`)

---

## Known Limitations

### Cookie Cache Revocation Lag

With cookie cache enabled, a revoked session may remain valid on the device that has it cached for up to 5 minutes (the `cookieCache.maxAge`). This is an acceptable tradeoff for eliminating ~95% of Redis lookups.

### Graceful Shutdown

RabbitMQ consumers should be drained before SIGTERM — if not, in-flight messages may be lost. All scheduled jobs are periodic and safe to re-run, so impact is bounded.

### Fail-Open Rate Limiting

If Redis is unavailable, rate limiting is skipped entirely (fail-open). This prioritizes availability over strictness. For environments requiring strict enforcement, add a circuit breaker that returns 503 when Redis is unreachable.
