# OPERATIONAL-GUARANTEES.md — D²-WORX Correctness & Safety

How D²-WORX prevents duplicate actions, ensures idempotency, and maintains correct behavior across services, instances, and scheduled jobs.

---

## Idempotency

### HTTP Idempotency (Gateway)

- **Mechanism:** `Idempotency-Key` header middleware on the .NET REST Gateway
- **Storage:** Redis `SET NX` with 24-hour TTL — shared across all gateway instances
- **Behavior:** First request with a given key is processed normally and the response is cached. Subsequent requests with the same key return the cached response without re-executing the handler
- **Scope:** External-facing mutations only. Internal service-to-service calls (gRPC, RabbitMQ) use different guarantees
- **Packages:** `@d2/idempotency` (Node.js), `Idempotency.Default` (.NET)

### Content-Addressable Entities (Geo)

- `Location` and `WhoIs` entities use SHA-256 content-addressable hash IDs (64-char hex)
- Identical input always produces the same ID — app-level deduplication (query existing by hash IDs, insert only new ones) makes creation inherently idempotent
- No duplicate data regardless of how many times the same entity is created

### Contacts (Auth → Geo)

- Contacts use UUIDv7 IDs (not content-addressable) but are immutable once created
- "Update" is modeled as create-new → repoint junction → delete-old, preserving cache validity across all consumers
- Org contact junction metadata (label, isPrimary) can be updated in place since it's org-owned, not contact data

---

## Scheduled Jobs (Dkron)

### No Duplicate Execution

Each of the 9 daily maintenance jobs uses a **Redis distributed lock** (`SET NX PX`) to ensure only one instance processes a job at any given time:

| Job                          | Service | Lock TTL     | Retention                                |
| ---------------------------- | ------- | ------------ | ---------------------------------------- |
| `purge-stale-whois`          | Geo     | Configurable | 180 days                                 |
| `cleanup-orphaned-locations` | Geo     | Configurable | Zero references                          |
| `purge-sessions`             | Auth    | Configurable | Expired sessions                         |
| `purge-sign-in-events`       | Auth    | Configurable | 90 days                                  |
| `cleanup-invitations`        | Auth    | Configurable | 7 days post-expiry                       |
| `cleanup-emulation-consents` | Auth    | Configurable | Expired/revoked                          |
| `purge-deleted-messages`     | Comms   | Configurable | 90 days                                  |
| `purge-delivery-history`     | Comms   | Configurable | 365 days                                 |
| `cleanup-deleted-users`      | Auth    | Configurable | 30-day soft-delete grace, then anonymize |

**Execution flow:** Dkron (cron trigger) → HTTP POST to REST Gateway (service key auth) → Gateway forwards via gRPC (API key credentials) → Service handler acquires Redis lock → Batch delete loop → Release lock → Return result

**If the lock is held:** The handler returns early with a success result (no error, no retry). The job is simply skipped on that instance. This is safe because all jobs are periodic cleanup — the next scheduled run will process any remaining records.

### Batch Processing

All purge/cleanup jobs use `CHUNK(batch_size)` to process records in batches (default 500), avoiding long-running transactions and large `IN` clauses. Batch size is configurable via the Options pattern.

### Staggered Scheduling

Jobs are staggered 15 minutes apart (2:00–3:45 AM UTC) to avoid resource contention.

---

## Rate Limiting

### No Per-Instance Counters

All rate limiting uses **Redis-backed shared counters** — never per-process `Map` objects.

- **Packages:** `@d2/ratelimit` (Node.js), `RateLimit.Default` (.NET)
- **Algorithm:** Sliding window approximation (two fixed-window counters + weighted average)
- **Atomicity:** Redis `INCR` + `TTL` — atomic operations, no cross-instance coordination needed
- **Dimensions:** ClientFingerprint (100/min), IP (5,000/min), City (25,000/min), Country (100,000/min)
- **Blocking:** If ANY dimension exceeds threshold → block for 5 minutes
- **Fail-open:** If Redis is down or WhoIs unavailable, requests pass through (availability over strictness)
- **Trusted bypass:** Service-key authenticated requests (`X-Api-Key`) bypass all rate limit dimensions

### Country Whitelist

US, CA, GB are exempt from country-level blocking to avoid false positives from CDN/proxy aggregation.

---

## Session Consistency

### Multi-Instance Safety

| Tier                | Storage   | Behavior                                                 |
| ------------------- | --------- | -------------------------------------------------------- |
| Cookie cache (5min) | In cookie | Travels with the request — any instance can decode       |
| Redis               | Shared    | Any instance queries the same Redis — instant revocation |
| PostgreSQL          | Shared    | Dual-write ensures durability + audit trail              |

**No sticky sessions required.** Any instance can handle any request. Session revocation propagates instantly via Redis. The only lag is the cookie cache TTL (~5 minutes max on the device that has the session cached).

### JWT Validation

- JWTs are self-contained — any instance validates with the cached JWKS public key
- JWKS endpoint (`/api/auth/jwks`) is database-backed — all auth instances serve the same keys
- Key rotation (30-day cycle, 30-day grace) is database-driven, not instance-specific
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

### Delivery Deduplication (Comms)

- Each `delivery_request` gets a unique `correlationId` (unique index) for cross-service tracking
- `delivery_attempt` rows are linked to their parent request via `delivery_request_id` — deduplication is at the request level
- A unique constraint on `(request_id, channel, attempt_number)` enforces attempt-level deduplication (`uq_delivery_attempt_request_channel_attempt`)

### Anonymize Fanout (`auth.user-anonymize`)

- Producer: Auth `FinalizeDeletedUser` (called from the `cleanup-deleted-users` Dkron job).
- Topology: fanout exchange — every interested service binds its own queue.
- Delivery semantics: **at-least-once.** A Dkron retry or a consumer crash can result in the same userId being delivered more than once.
- **Consumer requirement: idempotent processing.** Anonymization handlers must treat a duplicate `userId` as a no-op (already-anonymized rows are skipped, not failed).
- Auth does not wait for downstream confirmation — fire-and-forget by design.

---

## Cross-Service Updates (SAGA)

For mutations that must touch state in multiple services (e.g., updating a user's profile contact data: Geo contact + Auth user row), D²-WORX uses a **synchronous SAGA helper** rather than choreographed events. The canonical helper lives at
`backends/node/services/auth/app/src/implementations/cqrs/handlers/x/cross-service-update.ts` (`runCrossServiceUpdate`).

### Ordering & Compensation

1. **Geo first** — write the contact (or other Geo-owned data). If this fails, abort and surface the error; nothing else has changed.
2. **Auth second** — write the BetterAuth-owned row that references the new Geo data. If this fails, attempt to **compensate Geo** (delete or revert the just-written record) so the cross-service state stays consistent.
3. **Compensation failure** is escalated via `logger.fatal` so an operator can manually reconcile. The handler still returns the original Auth failure to the caller — the user's request is not silently "succeeded" when state is inconsistent.

### Why Synchronous SAGA, Not Eventual

These flows return a result the user expects to see immediately (the new phone number, the new contact). Choreographed events would require optimistic UI + eventual consistency, which is the wrong tradeoff for foreground profile edits. SAGA bounded by a single request gives "all or rolled-back" semantics with bounded latency.

### Anonymize Is Not a SAGA

The user-deletion anonymize flow is **not** a SAGA — it is a fire-and-forget fanout. Anonymization is irreversible by design (the deletion grace window closed; no compensation is meaningful), so each downstream service owns its own idempotent consumer rather than coordinating rollback.

---

## Request Enrichment

### Deterministic Output

- Request enrichment is **stateless middleware** — resolves client IP, computes fingerprint, calls Geo WhoIs cache
- Identical input always produces identical output regardless of which instance processes the request
- No instance affinity required

---

## Multi-Instance Scaling Checklist

When adding a new service or endpoint, verify:

- [ ] **Rate limiting** — Use `@d2/ratelimit` (Redis-backed) or `.NET RateLimit.Default`, never per-process Maps
- [ ] **Idempotency** — Use Redis-backed `@d2/idempotency` for external-facing mutations
- [ ] **Session/auth** — Validate JWTs via JWKS (no instance affinity), sessions via Redis
- [ ] **Local caches** — Memory caches are per-instance (fine for read-heavy, TTL-bounded data). Correctness must not depend on cache consistency across instances
- [ ] **Background jobs** — Use Dkron + Redis distributed locks (`SET NX`). Return early if lock is held
- [ ] **Cache invalidation** — Use fanout exchanges with exclusive auto-delete queues (not competing consumers)
- [ ] **Connection strings** — Externalized via config/environment, not hardcoded
- [ ] **DB constraints** — Catch unique violations (PG `23505`) gracefully — return 409, not 500
- [ ] **Migrations** — Never hand-write migration SQL / journal / snapshot files. Always use the framework generator (`pnpm db:generate` / `dotnet ef migrations add`). Hand-written migrations desync the framework's model snapshot and silently break the runtime migrator (Drizzle skips on `when` mismatch; EF Core compares `__EFMigrationsHistory`).
- [ ] **Cross-service mutations** — Use SAGA helper (`runCrossServiceUpdate`) for foreground multi-service writes (Geo-first → Auth-second → compensate Geo on Auth failure → `logger.fatal` on rollback failure). Don't invent new SAGA flows without review (see BACKENDS.md § SAGA Pattern)

---

## Known Limitations

### Cookie Cache Revocation Lag

With cookie cache enabled, a revoked session may remain valid on the device that has it cached for up to 5 minutes (the `cookieCache.maxAge`). This is an acceptable tradeoff for eliminating ~95% of Redis lookups.

### Graceful Shutdown

RabbitMQ consumer is not drained before SIGTERM — in-flight messages may be lost. Tracked as outstanding item (requires `MessageBus.drain()` API). Low impact: all jobs are periodic and safe to re-run.

### Fail-Open Rate Limiting

If Redis is unavailable, rate limiting is skipped entirely (fail-open). This prioritizes availability over strictness. For environments requiring strict enforcement, add a circuit breaker that returns 503 when Redis is unreachable.
