<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0017: EF-as-DDD persistence — retire the per-op Repository TLC; CQRS handlers use DbContext + aggregates + LINQ directly

- **Status**: Accepted (draft — finalized at SHIP of deliverable 0016; CLAUDE.md/rules.md/PATTERNS.md edits require explicit user approval at SHIP)
- **Date**: 2026-06-06
- **Deliverable**: `0016-keycustodian`

## Context

V1 and early V2 work used a per-operation Repository TLC (`Repository/Handlers/{C,R,U,D}/`) with one interface and one implementation per CRUD verb. The intent was to isolate DB access behind a stable interface and reduce coupling. In practice, the tax was significant:

- **No ad-hoc LINQ**: every non-trivial query required a new repo handler file; EF's composable `IQueryable` pipeline was unused.
- **No aggregate `Include` loads**: loading a key + its audit entries across the repository boundary required multiple round-trips or hand-rolled join logic.
- **No in-DB projections**: result-set projections (selecting only the columns needed) required new repo variants or pulling full entities.
- **Transactions as ceremony**: a rotation (delete retiring row + insert active + append audit entry in one `SaveChangesAsync`) required coordinating three separate repo handlers, making atomicity fragile.
- **Boilerplate inflation**: every new domain object produced 4+ repo interfaces + 4+ implementations before any real logic was written.

The cross-cutting machinery the Repository TLC was supposed to protect — telemetry, metrics, `D2Result` DB-exception translation, cancellation, request scope — ALL lives at `BaseHandler` / `BaseRepoHandler`, not at the Repository TLC. The per-op repository layer provided only a naming convention with no real isolation benefit.

Entity Framework Core, used correctly, already provides:
- A working Unit-of-Work pattern (`SaveChangesAsync`)
- A clean aggregate-root pattern via `DbSet<T>` and navigation properties
- OTel instrumentation at the SQL level (via `Microsoft.EntityFrameworkCore.Diagnostics`)
- DB-exception classification via `IDbExceptionClassifier` (already in `BaseRepoHandler`)

KeyCustodian is the first service pilot for the new pattern.

## Decision

**Retire the Repository TLC.** CQRS handlers access the database directly through the module `DbContext` + aggregates + LINQ.

### Command handlers

Command handlers (mutations — rotate, activate, compromise, generate) inherit `BaseRepoHandler` (which keeps the DB-exception → `D2Result` translation via `IDbExceptionClassifier`). They use the module `DbContext` directly:

- State transitions are implemented as **delete-old-row + insert-new-row** (required by EF TPH — the discriminator column maps to the CLR type, so changing state is a structural change, not an UPDATE).
- Multi-row writes (retiring + successor + audit entry) happen in a single `SaveChangesAsync` call for atomicity.
- PG advisory lock acquisition and release bracket the rotation transaction.

### Query handlers

Query handlers (read-only — JWKS assembly, key status lookups) inherit `BaseHandler` and use `AsNoTracking()` LINQ → lightweight DTOs. No mutations, no `SaveChangesAsync`.

### What is unchanged

- `BaseHandler` and `BaseRepoHandler` are unchanged — all cross-cutting concerns (telemetry, metrics, cancellation, `D2Result` translation) remain there.
- EF Core OTel instrumentation replaces per-op repo spans with SQL-level spans, which are more precise.
- The handler pipeline, DI registration pattern, and `D2Result` semantic factories are unchanged.

### Convention shift (at SHIP, user-approved)

The CLAUDE.md / rules.md / PATTERNS.md TLC table currently lists Repository as a first-class TLC. At SHIP:
- Repository TLC is retired from new code; the table is updated to reflect that CQRS handlers use DbContext directly.
- §9.31/§9.32 EF predicates are updated to document the DbContext-direct handler shape.
- This change requires explicit user approval at SHIP because it affects a convention documented across multiple files.

## Consequences

**Positive:**
- Full EF power: LINQ, `Include`, projections, bulk ops, single-transaction multi-row writes.
- Less boilerplate: no per-op repo interface + implementation per CRUD verb.
- SQL-level OTel spans: more precise than per-op repo spans for debugging.
- Cross-cutting unchanged: all pipeline machinery stays at `BaseHandler` / `BaseRepoHandler`.

**Negative / trade-offs:**
- Convention shift: teams familiar with the Repository TLC pattern must learn the DbContext-direct handler shape.
- §9.24 TLC table and related docs require updates at SHIP.
- The DB-exception → `D2Result` translation (previously implicit in repo handlers) must be explicitly invoked via `BaseRepoHandler` — handlers that forget to inherit `BaseRepoHandler` lose this translation. A linting check or `BaseHandler` API guidance mitigates this.

## Alternatives considered

- **Keep per-op Repository handlers**: rejected. The tax above outweighs any isolation benefit; all cross-cutting concerns are already at `BaseHandler`.
- **Generic `IRepository<T>`**: rejected — a leaky abstraction over EF that loses the same power as the per-op approach without the interface-per-verb overhead.
- **MediatR-style separate repository + Unit-of-Work**: rejected — `BaseHandler` already IS the pipeline; `SaveChangesAsync` already IS the Unit-of-Work. Adding a separate UoW layer adds ceremony with no benefit.
- **Dapper / raw SQL**: out of scope for this service. EF Core's ORM capabilities are well-matched to the key lifecycle domain.
