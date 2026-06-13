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
| [PATTERNS.md](PATTERNS.md) | Service project structure, handler pipeline, D2Result, spec-driven error codes, utilities, cache, mappers, messaging, SAGA, EF persistence, domain validation, spec-driven codegen, configuration, i18n, and more. The single biggest pattern reference. |

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
| [archive/PHASE_1_GEO_LIBS.md](archive/PHASE_1_GEO_LIBS.md) | Geo-library execution tracking (archived — 0009-geo-libs shipped).                                                             |
| [v2/PHASE_0_AUTH.md](v2/PHASE_0_AUTH.md)           | Authentication architecture reference — JWT shape, session model, key-rotation flow.                                                     |
| [v2/PHASE_0_MESSAGING.md](v2/PHASE_0_MESSAGING.md) | Async-messaging architecture reference — exchange/queue topology, encryption framing, DLQ flow.                                          |
| [v2/PHASE_5_REFERENCE.md](v2/PHASE_5_REFERENCE.md) | D2.Courier + D2.Notifications rebuild reference — Universal Message Shape, Comms 6 design principles.                                    |
| [v2/PHASE_6_REFERENCE.md](v2/PHASE_6_REFERENCE.md) | D2.Files (.NET) rebuild reference — 6 design principles, status state machine, smartphone MIME list, GEO_CLIENT log-suppression pattern. |
| [v2/PHASE_8_REFERENCE.md](v2/PHASE_8_REFERENCE.md) | dkron-mgr (.NET) rewrite reference — Reconciler pattern, change-detection field list.                                                    |

### Claim catalog

`JWT-CLAIMS.md` — full custom-claim catalog. The spec lives in `contracts/jwt-claims/jwt-claims.spec.json` and the generated `JwtClaimTypes` constants in `auth/abstractions/` are the canonical reference; a standalone prose catalog may be added alongside them as the claim set stabilizes.
