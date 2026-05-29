<!--
Copyright (c) DCSV. All rights reserved.
-->

# docs/ — D²-WORX Documentation

> Parent: [`/`](../README.md)

Project documentation that doesn't belong at the repo root.

## Index

### Cross-cutting patterns

| Doc                        | Purpose                                                                                                                                                                               |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [PATTERNS.md](PATTERNS.md) | Distilled tribal knowledge — TLC/2LC/3LC convention, handler, D2Result, utilities, repo, cache, middleware, RedactionSpec, i18n, configuration. The single biggest pattern reference. |

### Process + quality

| Doc                              | Purpose                                                                                                                                                                                                                                                 |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [COMMANDS.md](COMMANDS.md)       | Build / test / lint / versioning command catalog — full Docker Compose lifecycle, single-project builds, test filters, inspection commands.                                                                                                              |
| [TESTS.md](TESTS.md)             | Adversarial test discipline — 8-category Case Coverage Checklist, naming conventions, Vitest custom matchers. The canonical reference for what "tested" means in this codebase.                                                                         |
| [SRC_GEN.md](SRC_GEN.md)         | Spec-driven codegen reference — .NET Roslyn `IIncrementalGenerator` + TypeScript `tools/ts-codegen` emitter patterns.                                                                                                                                   |
| [dev/process.md](dev/process.md) | Workflow + audit-loop architecture — phase lifecycle (PLAN / EXECUTE / FINAL-REVIEW / SHIP / REVIEW), permission gates, sub-agent orchestrator-worker model, K=12 audit-cluster dispatch protocol, self-improvement loop.                               |
| [dev/rules.md](dev/rules.md)     | Verbose authoritative predicate catalog — security, race conditions, naming, object disposal, D2Result, OOTB shared libs, logging, PII, graceful degradation, UX, DX, observability, idempotency, configuration, conventions. Walked every audit round. |
| [ADRs](adrs/README.md)           | Architectural Decision Records (Nygard format + Deliverable cross-link field).                                                                                                                                                                          |

### Cross-language tracking

| Doc                    | Purpose                                                                                                                                                                                |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [PARITY.md](PARITY.md) | Template + "Why exclusive?" framework for cross-language additions (.NET ↔ SvelteKit ↔ other languages). Cross-language parity template — populated as cross-language components ship. |

### Build-out tracking (under `v2/`, archived as each milestone ships)

| Doc                                                | Purpose                                                                                                                                  |
| -------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| [v2/V2.md](v2/V2.md)                               | Architecture & build plan — internal tracking doc.                                                                                       |
| [v2/PHASE_1.md](v2/PHASE_1.md)                     | Geo-library execution tracking.                                                                                                          |
| [v2/PHASE_0_AUTH.md](v2/PHASE_0_AUTH.md)           | Authentication architecture reference — JWT shape, session model, key-rotation flow.                                                     |
| [v2/PHASE_0_MESSAGING.md](v2/PHASE_0_MESSAGING.md) | Async-messaging architecture reference — exchange/queue topology, encryption framing, DLQ flow.                                          |
| [v2/PHASE_5_REFERENCE.md](v2/PHASE_5_REFERENCE.md) | D2.Courier + D2.Notifications rebuild reference — Universal Message Shape, Comms 6 design principles.                                    |
| [v2/PHASE_6_REFERENCE.md](v2/PHASE_6_REFERENCE.md) | D2.Files (.NET) rebuild reference — 6 design principles, status state machine, smartphone MIME list, GEO_CLIENT log-suppression pattern. |
| [v2/PHASE_8_REFERENCE.md](v2/PHASE_8_REFERENCE.md) | dkron-mgr (.NET) rewrite reference — Reconciler pattern, change-detection field list.                                                    |

### Not yet written

`JWT-CLAIMS.md` — full custom-claim catalog. Not yet written; the spec lives in `contracts/jwt-claims/jwt-claims.spec.json` and the generated `JwtClaimTypes` constants are the canonical reference.
