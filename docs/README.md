<!--
Copyright (c) DCSV. All rights reserved.
-->

# docs/ — D²-WORX Documentation

> Parent: [`/`](../README.md)

Project documentation that doesn't belong at the repo root.

## How the docs are organized

Docs fall into two kinds: **persistent** (survive forever, each owns one altitude) and **ephemeral** (holding-pens that fold into the durable form on ship, then get pruned).

### Persistent tiers

| Tier | Doc | Owns |
| ---- | --- | ---- |
| 1 — whole project | `docs/v2/V2.md` | Phase map + one-line status per phase + vision |
| 2 — per phase | `docs/v2/PHASE_N.md` | That phase's deliverable DAG + per-deliverable scope/status/deps + build order |
| 3 — per deliverable | `docs/dev/deliverables/NNNN.md` + ADRs | What shipped, decisions, lessons |
| Reference | KEEP docs (`PATTERNS`, `rules`, `process`, `TESTS`, …) + per-lib/service READMEs | Current-truth API and conventions |

Each tier **points** to the tier below — it does not restate what that tier owns. No two docs are a redundant source of truth for the same status or plan.

### Ephemeral holding-pens

- **Research docs** (e.g. `docs/wip/phase-3-edge-planning/`) — distilled into the ship doc + ADRs on ship, then deleted.
- **Design annexes** (`docs/v2/PHASE_N_<concern>.md`) — live until their deliverable is built, then fold into the ship doc + ADRs, then pruned.
- **wip workspaces** (`docs/wip/NNNN/`) — working journals; pruned after ship.

Once work ships, the durable form is the ship doc + ADRs. The forward-looking holding-pen folds in and is pruned.

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
| [dev/rules.md](dev/rules.md) (index) + [dev/rules/](dev/rules/) | Verbose authoritative predicate catalog — security, race conditions, naming, object disposal, D2Result, OOTB shared libs, logging, PII, graceful degradation, UX, DX, observability, idempotency, configuration, conventions. Split into one file per category under `dev/rules/`, with `dev/rules.md` as the index (category table + per-§ anchor stubs); each K=12 audit cluster reads only its category files. Walked every audit round. |
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
| [v2/PHASE_3_AUTH.md](v2/PHASE_3_AUTH.md)           | Authentication architecture reference — JWT shape, session model, key-rotation flow. Edge-auth design annex for A1–A6.                   |
| [v2/PHASE_3_RATE_LIMITING.md](v2/PHASE_3_RATE_LIMITING.md) | Rate-limit design annex — 18-bucket algorithm, Operation Risk Tier classification, kill-switch + FP-detection behavior.           |
| [v2/PHASE_5_REFERENCE.md](v2/PHASE_5_REFERENCE.md) | D2.Courier + D2.Notifications rebuild reference — Universal Message Shape, Comms 6 design principles.                                    |
| [v2/PHASE_6_REFERENCE.md](v2/PHASE_6_REFERENCE.md) | D2.Files (.NET) rebuild reference — 6 design principles, status state machine, smartphone MIME list, GEO_CLIENT log-suppression pattern. |
| [v2/PHASE_8_REFERENCE.md](v2/PHASE_8_REFERENCE.md) | dkron-mgr (.NET) rewrite reference — Reconciler pattern, change-detection field list.                                                    |

### Claim catalog

`JWT-CLAIMS.md` — full custom-claim catalog. The spec lives in `contracts/jwt-claims/jwt-claims.spec.json` and the generated `JwtClaimTypes` constants in `auth/abstractions/` are the canonical reference; a standalone prose catalog may be added alongside them as the claim set stabilizes.
