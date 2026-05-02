<!--
Copyright (c) DCSV. All rights reserved.
-->

# docs/ — D²-WORX Documentation

Project documentation that doesn't belong at the repo root.

## Index

### Cross-cutting patterns

| Doc | Purpose |
|---|---|
| [PATTERNS.md](PATTERNS.md) | Distilled tribal knowledge — TLC/2LC/3LC convention, handler, D2Result, utilities, repo, cache, middleware, RedactionSpec, i18n, configuration. The single biggest pattern reference. |

### Process + quality

| Doc | Purpose |
|---|---|
| [TESTS.md](TESTS.md) | Adversarial test discipline — 8-category Case Coverage Checklist, naming conventions, Vitest custom matchers. The single highest-value extraction from v1. |
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
| [SECURITY-RUNBOOKS.md](SECURITY-RUNBOOKS.md) | Compromise response runbooks. Stub during wipe; expanded when KeyCustodian ships with the Edge auth module. |

### v2 build-out (tracking docs — under `v2/`, deleted as each phase ships)

| Doc | Used in |
|---|---|
| [v2/V2.md](v2/V2.md) | v2 architecture & build plan. Single source of truth for the v2 rewrite. Archived once v2 ships. |
| [v2/PHASE_0.md](v2/PHASE_0.md) | Wipe + Phase 0 execution tracking. Archived once Phase 0 ships. |
| [v2/PHASE_5_REFERENCE.md](v2/PHASE_5_REFERENCE.md) | Phase 5 — D2.Courier + D2.Notifications rebuild. Universal Message Shape, Comms 6 design principles. |
| [v2/PHASE_6_REFERENCE.md](v2/PHASE_6_REFERENCE.md) | Phase 6 — D2.Files (.NET) rebuild. 6 design principles, status state machine, smartphone MIME list, GEO_CLIENT log-suppression pattern. |
| [v2/PHASE_8_REFERENCE.md](v2/PHASE_8_REFERENCE.md) | Phase 8 — dkron-mgr (.NET) rewrite OR replacement. Reconciler pattern, change-detection field list. |

### Future (TBD per Phase 3)

`JWT-CLAIMS.md` — full custom-claim catalog (created when first `d2:`-namespaced claim ships with the Edge auth module).

---

## See also

- **Code rules + workflow**: [CLAUDE.md](../CLAUDE.md) at repo root
- **Current execution state during the v2 build-out**: [v2/PHASE_0.md](v2/PHASE_0.md) (archived once Phase 0 ships)
