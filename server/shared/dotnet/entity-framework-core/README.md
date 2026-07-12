<!--
Copyright (c) DCSV. All rights reserved.
-->

# entity-framework-core cluster

> Parent: [`server/shared/dotnet/`](../README.md)

Cluster of EF Core shared libraries, partitioned by provider specificity:

| Package | Description |
| --- | --- |
| [`core/`](core/README.md) — `D2.Shared.EntityFrameworkCore` | Provider-agnostic EF Core migration helpers. Currently ships `CreateD2Index<TEntity>` — a `MigrationBuilder` extension for declaring indexes on `ComplexProperty` member columns. |
| [`postgres/`](postgres/README.md) — `D2.Shared.EntityFrameworkCore.Postgres` | PostgreSQL-specific EF Core startup **mechanism**: `PgAdvisoryLock`, `AdvisoryLockMigrator<TContext>`, `DesignTimeDbContextFactoryBase<TContext>`, and `NpgsqlContextDefaults.ApplyD2NpgsqlDefaults`. Does **not** host domain lock-key catalogs. |
| [`locks-source-gen/`](locks-source-gen/README.md) — `D2.Shared.AdvisoryLocks.SourceGen` | Roslyn `IIncrementalGenerator` that emits `AdvisoryLocks` into the **owning-module assembly** (currently `D2.Edge.KeyCustodian.Infra`) from `contracts/advisory-locks/advisory-locks.spec.json`. Enforces per-database key uniqueness at build time. Shared owns the tooling; the domain owns the emitted constants. |
