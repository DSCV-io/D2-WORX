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

### Competing Consumers

- RabbitMQ queues use **competing consumers** — each message is delivered to exactly one consumer instance
- No duplicate processing of queue messages under normal operation
- If a consumer crashes mid-processing, the message is requeued and picked up by another instance

### Fanout for Cache Invalidation

- Cache invalidation signals use **fanout exchanges** with exclusive auto-delete queues per instance
- Every instance receives every invalidation signal — ensures all local caches are refreshed
- This is the correct pattern: invalidation must reach all instances, not just one

### At-Least-Once Fanouts Require Idempotent Consumers

For any fanout exchange (e.g., audit events, future user-anonymize fanouts):

- **Delivery semantics: at-least-once.** A producer retry or a consumer crash can result in the same payload being delivered more than once.
- **Consumer requirement: idempotent processing.** Handlers must treat duplicate IDs as no-ops, not failures.
- Producers do not wait for downstream confirmation — fire-and-forget by design.

### Payload Encryption

All sensitive RabbitMQ payloads are encrypted at the publisher and decrypted at the consumer via `D2.Shared.Encryption`:

- AES-256-GCM, JWKS-style multi-key keyring (overlap supported for graceful rotation)
- Per encryption-domain keys (audit, notifications, courier at launch)
- Routing keys + headers stay cleartext for routing; only message body is encrypted
- Two-tier keyring: production loads active + retiring kids; ops CLI loads archived kids on demand for forensic decryption

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
