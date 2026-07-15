<!--
Copyright (c) DCSV. All rights reserved.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0017: EF-as-DDD persistence — retire the per-op Repository TLC; persist sum-type aggregates as a flat non-polymorphic Record + pure mapper (Shape B), NOT TPH

- **Status**: Accepted
- **Date**: 2026-06-06 (persistence-mechanism revision: 2026-06-09)
- **Deliverable**: `0016-keycustodian`

## Context

V1 and early V2 work used a per-operation Repository TLC (`Repository/Handlers/{C,R,U,D}/`) with one interface and one implementation per CRUD verb. The intent was to isolate DB access behind a stable interface and reduce coupling. In practice, the tax was significant:

- **No ad-hoc LINQ**: every non-trivial query required a new repo handler file; EF's composable `IQueryable` pipeline was unused.
- **No aggregate `Include` loads**: loading a key + its audit entries across the repository boundary required multiple round-trips or hand-rolled join logic.
- **No in-DB projections**: result-set projections (selecting only the columns needed) required new repo variants or pulling full entities.
- **Transactions as ceremony**: a multi-row write (state change + append audit entry in one `SaveChangesAsync`) required coordinating separate repo handlers, making atomicity fragile.
- **Boilerplate inflation**: every new domain object produced 4+ repo interfaces + 4+ implementations before any real logic was written.

The cross-cutting machinery the Repository TLC was supposed to protect — telemetry, metrics, `D2Result` DB-exception translation, cancellation, request scope — ALL lives at `BaseHandler` / `BaseRepoHandler`, not at the Repository TLC. The per-op repository layer provided only a naming convention with no real isolation benefit.

Entity Framework Core, used correctly, already provides:
- A working Unit-of-Work pattern (`SaveChangesAsync`)
- A clean aggregate-root pattern via `DbSet<T>` and navigation properties
- OTel instrumentation at the SQL level (via `Microsoft.EntityFrameworkCore.Diagnostics`)
- DB-exception classification via `IDbExceptionClassifier` (already in `BaseRepoHandler`)

KeyCustodian is the first service pilot for the new pattern.

### The persistence-mechanism problem: immutable sum-type aggregates do not map to a polymorphic EF entity

KeyCustodian's domain (ADR-0016) is an **immutable sum-type state machine**: a sealed base record (`EncryptionKey`) + one sealed per-state type (`PendingKey` / `ActiveKey` / `RetiringKey` / `RetiredKey` / `CompromisedKey`) + total transitions that return a *new* instance of the next state. This is the right domain model — illegal transitions are uncompilable (§9.31 at the type level). The question this ADR settles is how that domain is persisted.

The original TPH-entity plan was to make the aggregate IS the EF entity and map it TPH (`TABLE PER HIERARCHY`): one table, a `KeyStatus` discriminator column mapped to the CLR type, and "transitions = delete-old-state row + insert-new-state row at the same PK in one `SaveChangesAsync`." A throwaway Testcontainers-Postgres spike (EF Core 10.0.7 / Npgsql.EFC 10.0.1 / Postgres 17, 2026-06-09) **falsified that plan**. The plan rested on the premise that "a discriminator mapped to a read-only `Status` since EF Core 5 is not a tracked mutation, so morphing the entity's runtime type is fine." That premise is FALSE. Three confirmed EF behaviors converge into a wall — EF Core **will not morph a tracked entity's runtime CLR type** (Zoran Horvat's "Wall #1"):

1. A same-PK `Remove` + `Add` is silently **merged into a single `UPDATE`**, not a `DELETE` + `INSERT` ([efcore#16355](https://github.com/dotnet/efcore/issues/16355) / [#30705](https://github.com/dotnet/efcore/issues/30705) — Open/Backlog, unfixed in EF Core 10). The intended "swap the row's CLR type" never happens.
2. That merged `UPDATE` leaves the **old state's columns un-nulled** → the discriminator and the data disagree (the row says `Active` but `RetiringAt` is still populated) ([efcore#36308](https://github.com/dotnet/efcore/issues/36308)).
3. A discriminator mapped to a get-only `Status` override fails model-build ("no backing field / no setter") ([efcore#4650](https://github.com/dotnet/efcore/issues/4650) — won't-fix).

The morph wall is structural and unfixed. (Horvat's "Wall #2" — polymorphic value types — does **not** bite here: the hierarchy is the `EncryptionKey` *entity*, and the read-side materialization of immutable records is fine; the VOs are flat.) C# 15's `closed`-hierarchy / discriminated-union proposal does not rescue the TPH approach either: the C# 15 union is a struct boxing an `object?` with no EF mapping, so discriminated unions stay domain-side regardless. The conclusion: the aggregate must NOT be the polymorphic EF entity.

The persistence-separation literature backs this directly — Vladimir Khorikov, Jimmy Bogard, and Kamil Grzybek all separate a rich immutable domain from a thin persistence shape via a mapper rather than forcing the ORM to model the domain's polymorphism; Oskar Dudycz's guidance is that event-sourcing is justified only when the history *is* the product, not as a default.

## Decision

**Retire the Repository TLC. CQRS handlers access the database directly through the module `DbContext` contract + aggregates + LINQ.** Two persistence shapes apply, selected by whether the aggregate is a state machine:

### Shape A (simple aggregates) — aggregate-as-EF-entity, direct mapping

For a non-state-machine aggregate (a single CLR type with VO members, no sealed per-state hierarchy), the aggregate IS the EF entity, mapped directly via an `IEntityTypeConfiguration<T>` (value converters / complex types per the EF VO mapping pattern). State changes, when any, are ordinary tracked-property `UPDATE`s. This is unchanged from prior EF-as-DDD work.

### Shape B (state-machine / sum-type aggregates) — flat non-polymorphic Record + pure mapper

For an immutable sum-type state machine (sealed base + per-state types + total transitions returning new instances — ADR-0016's `EncryptionKey`), **the aggregate is NOT the EF entity.** Instead:

- **The domain stays pure and EF-free.** The sealed sum-type + transitions are unchanged — no EF attributes, no EF references on Domain. (When C# 15 `closed` hierarchies ship, the base may be annotated; that is purely additive and changes nothing here.)
- **Persistence is ONE flat, non-polymorphic EF Record** per aggregate, whose CLR type never changes:
  - a settable `Status` **value** column (a string/enum value, NOT a TPH type discriminator),
  - the always-present core fields (PK + identity + the fields every state carries),
  - NULLABLE per-state columns (or a JSON payload column for data-heavy states) for the fields only some states carry.
  Because the Record's CLR type is fixed, the EF morph wall is **structurally unreachable** — a transition is an ordinary `UPDATE` of the `Status` value + the relevant columns.
- **A PURE static mapper bridges domain ↔ Record:**
  - `Record.ToDomain()` — switch on the `Status` value and rehydrate the correct sealed state from the columns, via the states' `required init` properties and the VOs' `FromTrusted` factories (trusted store value, no re-validation).
  - `aggregate.ProjectOnto(record)` — set `Status`, then **NULL ALL per-state columns first and set only the new state's** afterward. The null-all-then-set discipline is what prevents stale columns (the exact corruption EF's merged-UPDATE would have caused).
  The mapper is a static function, NOT a DI'd `IRepository` interface. Re-introducing an interface here would reconstitute the per-op repository layer this ADR retires.
- **`IQueryable` query extensions** (`Pending()` / `Active()` / `Signing()` / …) filter on the `Status` value column → server-side SQL `WHERE` (indexed); `.ToDomainListAsync()` materializes the rows then maps to the sum-type shape.
- **Concurrency** is a Postgres `xmin` optimistic-concurrency token (`.IsConcurrencyToken()…HasColumnType("xid")`), giving an exactly-one-winner invariant for free. It complements (does not replace) the `pg_try_advisory_lock` rotation coordination from ADR-0016.
- **Audit** rows (the append-only `EncryptionKeyAudit`-style entity) are written in the SAME `SaveChangesAsync` / transaction as the state change. EF orders principal-before-dependent writes; the audit FK uses `OnDelete(Restrict)`.

### Layering

- **App** references EF Core and owns: the flat `Record`, the pure mapper, the `IQueryable` query extensions, and the `IXxxDbContext` contract (`DbSet<…>` + `SaveChangesAsync`).
- **Infra** owns: the concrete `DbContext` implementation and the `IEntityTypeConfiguration<Record>`.
- **Domain** stays EF-free.
- **No repository.** Handlers use the `IXxxDbContext` contract + the mapper + LINQ directly.

### Command handlers

Command handlers (mutations — rotate, activate, compromise, generate) inherit `BaseRepoHandler` (which keeps the DB-exception → `D2Result` translation via `IDbExceptionClassifier`). They load the tracked Record, call the domain transition on the rehydrated aggregate, `ProjectOnto` the tracked Record, append the audit entity, and `SaveChangesAsync` once. For state-machine aggregates the write is an ordinary `UPDATE` (+ audit `INSERT`); the `xmin` token makes a concurrent transition fail the second writer. The `rowsAffected`/concurrency check follows §9.32.

### Query handlers

Query handlers (read-only — JWKS assembly, key status lookups) inherit `BaseHandler` and use `AsNoTracking()` LINQ via the query extensions → `ToDomainListAsync()` or lightweight projections. No mutations, no `SaveChangesAsync`.

### Boilerplate at scale → source-gen amortization

The Record + mapper + EF config + query extensions for a state-machine aggregate are mechanical (~120–180 lines per aggregate, derivable from the sealed-state shapes). **HAND-WRITE the first pilot (KeyCustodian) by hand.** Once the shape is proven across 2–3 aggregates, extract a Roslyn source generator that emits them per aggregate from the sealed-state hierarchy. Do not build the generator before the shape is proven.

### What is unchanged

- `BaseHandler` and `BaseRepoHandler` are unchanged — all cross-cutting concerns (telemetry, metrics, cancellation, `D2Result` translation) remain there.
- EF Core OTel instrumentation replaces per-op repo spans with SQL-level spans, which are more precise.
- The handler pipeline, DI registration pattern, and `D2Result` semantic factories are unchanged.
- The EF VO mapping pattern (complex types + value converters, ADR-0001) is unchanged — it applies to the Record's VO-typed columns.

### Convention shift (ratified at 0016-keycustodian SHIP)

The Repository TLC has been retired from new code; `PATTERNS.md` and `rules.md` have been updated to reflect that CQRS handlers use `DbContext` directly. A state-machine-persistence predicate (flat non-polymorphic Record + pure mapper + query extensions + no-repo + `xmin`; event-sourcing deviation; source-gen amortization) is codified alongside §9.31/§9.32.

## Consequences

**Positive:**
- The EF morph wall is structurally unreachable for state-machine aggregates: the Record's CLR type never changes, so the silent-merge / stale-columns / discriminator-build failures cannot occur. Validated 6/6 by the spike (model builds; transition emits one `UPDATE` + one audit `INSERT` — no delete+insert, no morph; full lifecycle round-trips to the correct sealed type; server-side status filtering; `xmin` exactly-one concurrency; exact temporal boundaries). No domain changes were needed.
- The domain stays pure: the sum-type + total transitions carry zero persistence concern, so the make-illegal-states-unrepresentable guarantee (§9.31) is uncompromised by the ORM.
- Full EF power for queries: LINQ, `Include`, projections, bulk ops, single-transaction multi-row writes.
- Less boilerplate: no per-op repo interface + implementation per CRUD verb; the Record/mapper/config/query-extensions are themselves source-gen-able once proven.
- SQL-level OTel spans: more precise than per-op repo spans for debugging.
- Cross-cutting unchanged: all pipeline machinery stays at `BaseHandler` / `BaseRepoHandler`.

**Negative / trade-offs:**
- Two representations of the same aggregate (the sealed domain shape and the flat Record), bridged by the mapper. The mapper's `ProjectOnto` null-all-then-set discipline is load-bearing — a missed null leaves a stale column. This is the cost of keeping the domain pure; it is mechanical and (once proven) source-gen-able, but until the generator exists it is hand-written and must be tested per-state (round-trip every state + assert no stale columns survive a transition).
- A new representation to learn: teams familiar with aggregate-as-EF-entity must learn the Record + mapper split for state-machine aggregates (Shape B); simple aggregates (Shape A) are unchanged.
- The DB-exception → `D2Result` translation must be explicitly invoked via `BaseRepoHandler` — a handler that forgets to inherit it loses the translation. `BaseHandler` API guidance mitigates this.
- §9.24 TLC table and related docs have been updated at 0016-keycustodian SHIP.

### Deviation (per-aggregate, NOT the default): event-sourcing via Marten

For an aggregate that is BOTH **audit-defining** (the history IS the product, not a side table) AND **workflow-complex** (Dudycz's good-fit criteria), event-sourcing via Marten is the justified persistence. Marten coexists with EF on the same Postgres instance. This is a deliberate per-aggregate deviation — NOT the system-wide default. The default for state-machine aggregates is Shape B (flat Record + mapper); event-sourcing is reached for only when an aggregate clears both bars.

## Alternatives considered

- **Keep per-op Repository handlers**: rejected. The tax above outweighs any isolation benefit; all cross-cutting concerns are already at `BaseHandler`.
- **Aggregate-as-TPH-entity with delete+insert transitions** (the original TPH-entity plan): rejected as **unsound** on EF Core 10. The morph wall ([efcore#30705](https://github.com/dotnet/efcore/issues/30705) Open/Backlog, [#36308](https://github.com/dotnet/efcore/issues/36308), [#4650](https://github.com/dotnet/efcore/issues/4650) won't-fix) makes "morph a tracked entity's CLR type via a discriminator" silently merge into a stale-column UPDATE, and the get-only-`Status` discriminator fails model-build. Falsified by the spike. ADR-0016's "read-only discriminator since EF Core 5 → not a tracked mutation" premise is FALSE.
- **Generic `IRepository<T>`**: rejected — a leaky abstraction over EF that loses EF's query power AND, for state-machine aggregates, would re-introduce the per-op repository layer the pure static mapper deliberately avoids.
- **MediatR-style separate repository + Unit-of-Work**: rejected — `BaseHandler` already IS the pipeline; `SaveChangesAsync` already IS the Unit-of-Work. Adding a separate UoW layer adds ceremony with no benefit.
- **C# 15 discriminated unions mapped by EF**: rejected — the C# 15 union is a struct boxing an `object?` with no EF mapping; DUs stay domain-side. The flat-Record + mapper approach is independent of the union proposal.
- **Event-sourcing as the system-wide default**: rejected — per Dudycz, event-sourcing is justified only when the history IS the product AND the workflow is complex. It is a per-aggregate deviation (see Consequences), not the default; most aggregates are well-served by the flat Record + mapper.
- **Dapper / raw SQL**: out of scope for this service. EF Core's ORM capabilities are well-matched to the key lifecycle domain; the flat Record maps cleanly with value converters + the `xmin` token.

## References

- [ADR-0016](0016-keycustodian-lifecycle-store.md) — the KeyCustodian sum-type lifecycle + the concrete `KeyRecord` schema this convention persists; the `pg_try_advisory_lock` rotation coordination the `xmin` token complements.
- [ADR-0001](0001-contacts-folded-owned-component.md) — the EF VO mapping pattern (complex types + value converters) the Record's VO columns reuse.
- [ADR-0018](0018-spec-driven-error-codes.md) / [ADR-0019](0019-wrapped-result-wire-model.md) — the error-code + wrapped-result conventions KeyCustodian's handlers surface failures through.
- Persistence-strategy spike (EF Core 10.0.7 / Npgsql.EFC 10.0.1 / Postgres 17) — 6/6 validated; the throwaway Testcontainers spike falsified TPH delete+insert (morph-wall, stale-column UPDATE, get-only discriminator) and confirmed flat-record Shape B as the decision rationale for this ADR.
