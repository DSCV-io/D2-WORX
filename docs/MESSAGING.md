<!--
Copyright (c) DCSV. All rights reserved.
-->

# MESSAGING.md — D²-WORX Cross-Service Messaging

> Wire format, naming, queue topology, headers, and delivery semantics for RabbitMQ-based async messaging in D²-WORX.

---

## Wire Format — Proto-Canonical JSON

All RabbitMQ message bodies use **proto-canonical JSON** (per `Google.Protobuf.JsonFormatter`).

**Why JSON not binary protobuf:**
- Cross-language friendly — any consumer can parse the decrypted payload without protoc-generated code
- After decryption, ops/DLQ tooling sees text JSON — easier to grep, diff, replay than binary
- Tradeoff (size + parse cost) is acceptable for our message rates

**Why proto-canonical (not arbitrary JSON):**
- Field names match `.proto` definitions
- Numeric enums emit as strings (forward-compatible with renames if old enum value preserved)
- Default values omitted (consumer applies proto3 defaults — saves bytes, matches binary semantics)
- Cross-language consumer libraries (`google.protobuf.json_format` Python, `Google.Protobuf.JsonParser` C#, `protobufjs` JS) all interpret identically

**All message bodies are encrypted.** AES-256-GCM wraps the proto-canonical JSON. Routing keys + headers stay cleartext (RMQ needs them for routing). Encrypt-all is the default to avoid per-exchange categorization mistakes — most async traffic carries PII or business-sensitive data (audit events, notification payloads, file lifecycle, user-anonymize fanouts), and AES-256-GCM is cheap per-message. The tradeoff is no plaintext browsing in RMQ Management UI; the `d2 msg decrypt` ops CLI covers DLQ inspection.

---

## Exchange Naming

Pattern: **`d2.{producer}.{purpose}`**.

| Example | Producer | Purpose |
|---|---|---|
| `d2.audit.events` | `audit` (D2.Audit) | Audit events from any service |
| `d2.notifications.requests` | `notifications` (D2.Notifications) | Notification delivery requests |
| `d2.files.events` | `files` (D2.Files) | File lifecycle events (uploaded, processed) |
| `d2.courier.delivery-status` | `courier` (D2.Courier) | Email/SMS delivery status updates |
| `d2.security.key-rotated` | `auth` (KeyCustodian) | Key rotation notifications |

**Service-namespaced** to prevent name collisions and make ownership explicit. The producer service owns the exchange schema + lifecycle.

---

## Routing Keys

Within an exchange, routing keys are **lowercase, dot-separated** for hierarchy.

Examples:
- `sign-in.success`
- `sign-in.failed`
- `payment.charged`
- `file.uploaded`
- `file.processed`

For **fanout** exchanges (cache invalidation, audit), routing key is empty (`""`) — every binding receives every message.

For **topic** exchanges, routing key supports wildcards (`#` = multi-segment, `*` = single-segment).

---

## Proto Package Naming

Event message schemas live in `contracts/protos/events/v{N}/{producer}.proto`.

Pattern:
- Package: `d2.events.{producer}.v{N}` — e.g., `d2.events.audit.v1`, `d2.events.files.v1`
- Message types: `{Producer}{EventType}Event` — e.g., `FileUploadedEvent`, `NotificationRequestedEvent`
  - **Naming exception**: when producer + event-type would create awkward double-naming (e.g., D2.Audit's only event is "an audit event"), drop the redundant suffix → `AuditEvent`. Same for any future case.

Each `.proto` file documents in comments:
```
// Published by: D2.Files
// Consumed by:  D2.Audit, D2.Notifications
```

So readers can trace producers + consumers without grep.

---

## Queue Patterns

### Competing consumers (most common)

For commands / requests where each message should be processed exactly once across the consumer fleet:

- **Durable shared queue** bound to the exchange
- N consumer instances pull from the same queue
- RabbitMQ guarantees each message is delivered to exactly one consumer
- If a consumer crashes mid-processing, the message is requeued

Example: `d2.notifications.requests` → `notifications.deliver` queue (shared) → 3 D2.Notifications replicas compete.

### Fanout — exclusive auto-delete (cache invalidation)

For broadcast notifications where every instance must receive every message:

- **Exclusive auto-delete queue per consumer instance** (created on connect, destroyed on disconnect)
- Bound to a fanout exchange
- Every instance gets every message (full broadcast)

Example: cache invalidation events. If "user X's profile changed", every Edge replica needs to evict its local cache. Competing consumers would deliver to one replica only — the rest would serve stale data.

### Durable shared (audit, persistent events)

For events that must be persisted regardless of consumer state:

- Durable queue bound to the exchange (survives broker restart)
- One or more consumers pull
- Unconsumed messages persist on disk

Example: `d2.audit.events` → durable `audit.events` queue → D2.Audit consumes and writes to `audit_db`.

---

## AMQP Headers Contract

Every message carries these headers:

| Header | Purpose |
|---|---|
| `content-type` | Always `application/octet-stream` (bodies are AES-256-GCM-encrypted — see "Wire Format" above). |
| `x-proto-type` | Fully-qualified proto message name (e.g., `d2.events.files.v1.FileUploadedEvent`). Lets consumers route by type without inspecting body. |
| `message-id` | UUIDv7 (sortable, includes timestamp). Per-message unique identifier. |
| `timestamp` | Producer's send timestamp (ISO 8601, UTC). |
| `x-d2-trace-id` | OTel trace context (W3C traceparent format). Propagates the calling request's trace through the async hop. |
| `x-d2-correlation-id` | If applicable (HTTP request had `Idempotency-Key`, etc.). For cross-service tracking. |
| `x-d2-encryption-kid` | Key ID — also embedded in the body, but headers help ops inspect without decrypting. |

---

## Delivery Semantics

### At-least-once (default for fanouts)

A producer retry or consumer crash can result in the same payload being delivered more than once. **Consumers MUST be idempotent** — duplicate IDs are no-ops, not failures.

Producers do not wait for downstream confirmation — fire-and-forget by design.

Examples of at-least-once: audit events, key-rotated notifications, future user-anonymize fanouts.

### Exactly-once (competing consumers, with cooperation)

Competing-consumer queues deliver each message to exactly ONE consumer. But "exactly-once processing" requires the consumer to be transactionally idempotent — ACK after the work commits, not before.

Pattern:
1. Consumer pulls message
2. Consumer's handler does its work in a single DB transaction (writes + ACK in one commit)
3. If handler fails before commit → no ACK → RMQ requeues → next pull retries
4. If handler succeeds → ACK → message removed

This is "effectively-once" in practice — true exactly-once requires distributed transactions, which we don't use.

### Acknowledgment timing

- **Manual ACK** — never auto-ACK. Auto-ACK at receive time loses messages on consumer crash.
- **ACK only after work commits** — see above.
- **NACK with requeue** for transient failures (rate limits, timeouts).
- **NACK without requeue** (= DLQ) for permanent failures (validation errors, schema mismatches).

---

## Dead Letter Queue (DLQ)

Every queue has a DLQ binding. Messages NACK-without-requeue land in `{queue-name}.dlq`.

DLQ inspection:
```bash
# Browse DLQ messages (encrypted-domain inspection)
d2 msg decrypt --domain audit --queue audit-events.dlq --count 10

# Browse including archived keys (for stale messages older than rotation grace)
d2 msg decrypt --message-id <id> --include-archive
```

The ops CLI loads keys from `auth_db.encryption_key` (active + archived) and decrypts on demand.

---

## Versioning

Message schemas are proto-defined under `contracts/protos/events/v{N}/{producer}.proto` — protos are the single source of truth for every async message shape. Versioning follows the same rules as gRPC RPC contracts:

- Breaking changes require a new `vN+1/` package; never modify a `vN` schema in place
- Old `vN/` packages stay supported until explicit decommission
- Producers can publish multiple versions simultaneously during a transition
- Consumers can subscribe to multiple versions during a transition
- Field-number renumbering is forbidden (wire-format identifier)
- Deprecate before removing: `[deprecated = true]` field option

(`reserved` keyword for removed fields is deferred — adopt only when first multi-team incident makes the value concrete; pre-prod single-team adds line noise without value.)

---

## Anti-Patterns to Avoid

- **Producing ad-hoc JSON without a proto schema** — every event has a `.proto` definition under `contracts/protos/events/`
- **Auto-ACK** — loses messages on crash. Always manual ACK.
- **ACK before work commits** — same problem as auto-ACK.
- **Sharing one queue across exchanges** — coupling. One queue per (exchange, consumer-purpose).
- **Skipping idempotent processing** — at-least-once means duplicates WILL happen. Plan for it.
- **Using fanout for commands** — fanout broadcasts. Commands should be competing-consumer.
- **Encrypting routing keys / headers** — RMQ needs them in cleartext for routing. Only the body is encrypted.
