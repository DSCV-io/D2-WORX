<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3_EDGE.md — Edge service forward-looking design

> **Status**: design only. Implementation tracked under Phase 3 in [V2.md](V2.md).

Design intent for the Edge service (Phase 3 of the v2 roadmap) — module authors,
operators, and reviewers preparing for Edge implementation will find here the canonical
references for HTTP idempotency, request enrichment, scheduled-jobs receiver, session
3-tier storage, multi-instance scaling, and the cross-service SAGA pattern that Edge
coordinates.

---

## Table of contents

- [§1. HTTP idempotency contract](#1-http-idempotency-contract)
- [§2. Request enrichment](#2-request-enrichment)
- [§3. Scheduled jobs — Edge as cron-trigger receiver](#3-scheduled-jobs--edge-as-cron-trigger-receiver)
- [§4. Session storage layers (3-tier)](#4-session-storage-layers-3-tier)
- [§5. Multi-instance scaling — service onboarding checklist](#5-multi-instance-scaling--service-onboarding-checklist)
- [§6. Cross-service SAGA pattern](#6-cross-service-saga-pattern)

---

## §1. HTTP idempotency contract

Edge implements `Idempotency-Key` header middleware on every external-facing mutation.

- **Mechanism**: `Idempotency-Key` HTTP header middleware on Edge.
- **Storage**: Redis `SET NX` with 24-hour TTL — shared across all Edge instances.
- **Behavior**: the first request with a given key is processed normally and the
  response is cached. Subsequent requests with the same key return the cached
  response without re-executing the handler.
- **Scope**: external-facing mutations only. Internal service-to-service calls
  (gRPC, RabbitMQ) use different guarantees — messaging-side idempotency lives in
  [`server/shared/dotnet/messaging-rabbitmq/README.md`](../../server/shared/dotnet/messaging-rabbitmq/README.md).
- **Library home**: the Edge-side `Idempotency.*` middleware csproj lands when Edge
  ships; key shape, TTL, and the cached-response envelope are pinned in spec for
  cross-language parity once the spec is authored.

---

## §2. Request enrichment

Stateless middleware that runs early in the Edge pipeline and produces the canonical
enrichment claims every downstream service consumes.

- **Stateless** — resolves client IP, computes the 10-slot composite fingerprint,
  calls the in-process WhoIs cache (Edge is the single source — there is no
  per-service WhoIs cache).
- **Deterministic** — identical input always produces identical output regardless
  of which Edge instance processes the request.
- **No instance affinity required** — replicas don't need to pin a session to a
  specific Edge box; the resolved enrichment flows through the JWT claim set Edge
  mints (per the anon-visitor authentication pattern in
  [PHASE_0_AUTH.md §3.8](PHASE_0_AUTH.md#38-anon-visitor-authentication-pattern--pattern-a-locked-mint-anon-jwt-at-edge)).
- **Downstream propagation** — services receive the resolved WhoIs via the
  `X-D2-WhoIs` header AND via the JWT's `d2_whois_id` tamper-evident claim
  binding (anon JWTs only; authed JWTs carry the WhoIs ID the same way).

---

## §3. Scheduled jobs — Edge as cron-trigger receiver

Edge is the HTTP entry-point for scheduler-triggered work; the scheduler itself
(dkron-mgr) is a separate Phase 8 deliverable tracked in
[PHASE_8_REFERENCE.md](PHASE_8_REFERENCE.md). This section covers the Edge half only.

### Execution flow

```
Cron trigger
  → HTTP POST to Edge (service-identity JWT auth)
  → Edge forwards via gRPC to the owning service
  → Service handler acquires Redis lock
  → Batch delete / cleanup loop
  → Release lock
  → Return result to Edge → return result to scheduler
```

### No duplicate execution

Each maintenance job uses a **Redis distributed lock** (`SET NX PX`) to ensure only
one instance processes a job at any given time.

**If the lock is held**: the handler returns early with a success result (no error,
no retry). The job is simply skipped on that instance. This is safe because all jobs
are periodic cleanup — the next scheduled run will process any remaining records.

### Batch processing

All purge / cleanup jobs use chunked processing (default 500 records per batch),
avoiding long-running transactions and large `IN` clauses. Batch size is configurable
via the Options pattern in the owning service.

### Staggered scheduling

Jobs are staggered (typically 15 minutes apart) to avoid resource contention on the
shared Redis lock surface.

> Cross-reference: the scheduler side (Dkron + cron config + retry policy) lives in
> [PHASE_8_REFERENCE.md](PHASE_8_REFERENCE.md). The split is deliberate — Edge owns
> the HTTP entry-point + service-identity auth + gRPC forwarding; the scheduler owns
> what fires when.

---

## §4. Session storage layers (3-tier)

| Tier                   | Storage   | Behavior                                                 |
| ---------------------- | --------- | -------------------------------------------------------- |
| Cookie cache (5 min)   | In cookie | Travels with the request — any instance can decode       |
| Redis                  | Shared    | Any instance queries the same Redis — instant revocation |
| PostgreSQL (`auth_db`) | Shared    | Dual-write ensures durability + audit trail              |

**No sticky sessions required.** Any instance can handle any request. Session
revocation propagates instantly via Redis. The only lag is the cookie cache TTL
(~5 minutes max on the device that has the session cached).

**Cookie-cache revocation lag** is the documented tradeoff: a revoked session may
remain valid on the device that has it cached for up to 5 minutes (the cookie
`maxAge`). This is the accepted tradeoff for eliminating ~95% of Redis lookups.

JWT validation across all three tiers reads from the shared JWKS — any Edge or
backend instance validates with the cached JWKS public key. JWKS rotation propagates
via the canonical `/.well-known/jwks.json` endpoint; key rotation cadence + dual-key
window during grace live in [V2.md §5.4](V2.md) and [PHASE_0_AUTH.md](PHASE_0_AUTH.md).

---

## §5. Multi-instance scaling — service onboarding checklist

Every new Edge service (Phase 3+) verifies the following at registration time:

- **Rate limiting** — uses the Edge rate-limit middleware (Redis-backed buckets),
  never per-process counters. Design lives in [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md).
- **HTTP idempotency** — uses the Edge idempotency middleware (§1) for
  external-facing mutations. Messaging-side idempotency uses
  `IMessageIdempotencyStore` per
  [messaging-rabbitmq/README.md](../../server/shared/dotnet/messaging-rabbitmq/README.md).
- **Session / auth** — validates JWTs via JWKS (no instance affinity), sessions
  via Redis (§4).
- **Local caches** — in-memory caches are per-instance (fine for read-heavy,
  TTL-bounded data). Correctness must not depend on cache consistency across
  instances. Cluster-wide L1 coherence uses `ICacheInvalidationBackplane` (Redis
  pub/sub).
- **Background jobs** — Redis distributed locks (`SET NX`) per §3. Return early if
  lock is held.
- **Cache invalidation** — fanout exchanges with exclusive auto-delete queues (not
  competing consumers) for cluster-wide invalidation events.
- **Connection strings** — externalized via `.env.local` / `.env.secrets`, never
  hardcoded.
- **DB constraints** — unique violations (PG `23505`) caught and mapped via
  `BaseRepoHandler` + `IDbExceptionClassifier` → returns 409, not 500.
- **Migrations** — never hand-written. Always `dotnet ef migrations add <Name>`.
  Multi-replica safety via PG advisory lock at the startup migrator.
- **Cross-service mutations** — uses the SAGA pattern (§6) for foreground
  multi-service writes.
- **Encryption** — sensitive payloads on RabbitMQ marked via the spec's
  `encryption` field per
  [messaging-source-gen/README.md](../../server/shared/dotnet/messaging-source-gen/README.md);
  auto-resolves the keyring via `D2.Shared.Encryption`.

---

## §6. Cross-service SAGA pattern

For mutations that must touch state in multiple services, Edge coordinates a
**synchronous SAGA** rather than choreographed events when the user expects an
immediate, visible result.

### Ordering + compensation

1. **Compensable step first** — write the data that's safest to roll back if a
   later step fails. If this step fails, abort and surface the error; nothing
   else has changed.
2. **Subsequent step(s)** — write the data that anchors the cross-service state.
   If this fails, attempt to **compensate** the earlier step (delete or revert
   the just-written record) so cross-service state stays consistent.
3. **Compensation failure** is escalated via `logger.fatal` so an operator can
   manually reconcile. The handler still returns the original failure to the
   caller — the user's request is never silently "succeeded" when state is
   inconsistent.

### Why synchronous, not eventual

These flows return a result the user expects to see immediately (the new phone
number, the new contact). Choreographed events would require optimistic UI +
eventual consistency, which is the wrong tradeoff for foreground edits. SAGA
bounded by a single request gives "all or rolled-back" semantics with bounded
latency.

### When NOT a SAGA

Irreversible flows (e.g., user-deletion anonymize) are **not** SAGAs — they are
fire-and-forget fanouts. Anonymization has no meaningful compensation (the
deletion grace window has closed; downstream services own their idempotent
consumers rather than coordinating rollback).

---

## References

- [V2.md §5.2 Edge — Unified Gateway](V2.md#52-edge--unified-gateway) — top-level
  roadmap entry for the Edge service.
- [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) — sister doc, rate-limit
  middleware design.
- [PHASE_0_AUTH.md](PHASE_0_AUTH.md) — Auth runtime + anon-visitor
  authentication pattern (every request reaches Edge with a JWT).
- [PHASE_8_REFERENCE.md](PHASE_8_REFERENCE.md) — scheduler half of the
  scheduled-jobs flow.
- [server/shared/dotnet/messaging-rabbitmq/README.md](../../server/shared/dotnet/messaging-rabbitmq/README.md)
  — messaging-side idempotency contract + DLQ + tiered retries.
