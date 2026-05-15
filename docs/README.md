<!--
Copyright (c) DCSV. All rights reserved.
-->

# docs/ — D²-WORX Documentation

> Parent: [`/`](../README.md)

Project documentation that doesn't belong at the repo root.

## Index

### Cross-cutting patterns

| Doc | Purpose |
|---|---|
| [PATTERNS.md](PATTERNS.md) | Distilled tribal knowledge — TLC/2LC/3LC convention, handler, D2Result, utilities, repo, cache, middleware, RedactionSpec, i18n, configuration. The single biggest pattern reference. |

### Process + quality

| Doc | Purpose |
|---|---|
| [TESTS.md](TESTS.md) | Adversarial test discipline — 8-category Case Coverage Checklist, naming conventions, Vitest custom matchers. The canonical reference for what "tested" means in this codebase. |
| [AUDIT_CHECKLIST.md](AUDIT_CHECKLIST.md) | Quality audit checklist — Security / Logic / Code Quality / Conventions / Cross-Service / Test Coverage / Documentation. Run before merging substantial work. |
| [OPERATIONAL-GUARANTEES.md](OPERATIONAL-GUARANTEES.md) | How D²-WORX prevents duplicate actions, ensures idempotency, maintains correct behavior across services, instances, scheduled jobs. |

### Cross-service messaging

| Doc | Purpose |
|---|---|
| [MESSAGING.md](MESSAGING.md) | RabbitMQ patterns — wire format (proto-canonical JSON), exchange + routing key naming, queue topology, AMQP headers, delivery semantics, DLQ inspection. |

### Cross-language tracking

| Doc | Purpose |
|---|---|
| [PARITY.md](PARITY.md) | Template + "Why exclusive?" framework for cross-language additions (.NET ↔ SvelteKit ↔ future). Backend is currently .NET-only; the table is empty + ready to grow. |

### Security

| Doc | Purpose |
|---|---|
| [SECURITY-RUNBOOKS.md](SECURITY-RUNBOOKS.md) | Compromise response runbooks. Stub — see the Status block at the top of that doc for the operational gap. |

### Build-out tracking (under `v2/`, archived as each milestone ships)

| Doc | Purpose |
|---|---|
| [v2/V2.md](v2/V2.md) | Architecture & build plan — internal tracking doc. |
| [v2/PHASE_0.md](v2/PHASE_0.md) | Foundation milestone execution tracking. |
| [v2/PHASE_0_AUTH.md](v2/PHASE_0_AUTH.md) | Authentication architecture reference — JWT shape, session model, key-rotation flow. |
| [v2/PHASE_0_MESSAGING.md](v2/PHASE_0_MESSAGING.md) | Async-messaging architecture reference — exchange/queue topology, encryption framing, DLQ flow. |
| [v2/PHASE_5_REFERENCE.md](v2/PHASE_5_REFERENCE.md) | D2.Courier + D2.Notifications rebuild reference — Universal Message Shape, Comms 6 design principles. |
| [v2/PHASE_6_REFERENCE.md](v2/PHASE_6_REFERENCE.md) | D2.Files (.NET) rebuild reference — 6 design principles, status state machine, smartphone MIME list, GEO_CLIENT log-suppression pattern. |
| [v2/PHASE_8_REFERENCE.md](v2/PHASE_8_REFERENCE.md) | dkron-mgr (.NET) rewrite reference — Reconciler pattern, change-detection field list. |

### Out of scope (until consumers exist)

`JWT-CLAIMS.md` — full custom-claim catalog. Out of scope until the first `d2_`-namespaced claim has a real consumer; until then the spec lives in `contracts/jwt-claims/jwt-claims.spec.json` and the generated `JwtClaimTypes` constants are the canonical reference.
