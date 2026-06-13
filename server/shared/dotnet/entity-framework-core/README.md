<!--
Copyright (c) DCSV. All rights reserved.
-->

# entity-framework-core cluster

> Parent: [`server/shared/dotnet/`](../README.md)

Cluster of EF Core shared libraries, partitioned by provider specificity:

| Package | Description |
| --- | --- |
| [`core/`](core/README.md) — `D2.Shared.EntityFrameworkCore` | Provider-agnostic EF Core migration helpers. Currently ships `CreateD2Index<TEntity>` — a `MigrationBuilder` extension for declaring indexes on `ComplexProperty` member columns. |
| [`postgres/`](postgres/README.md) — `D2.Shared.EntityFrameworkCore.Postgres` | PostgreSQL-specific EF Core startup machinery: `PgAdvisoryLock`, `AdvisoryLockMigrator<TContext>`, `DesignTimeDbContextFactoryBase<TContext>`, and `NpgsqlContextDefaults.ApplyD2NpgsqlDefaults`. Also hosts the spec-generated `AdvisoryLocks` key registry (emitted by `locks-source-gen`). |
| [`locks-source-gen/`](locks-source-gen/README.md) — `D2.Shared.AdvisoryLocks.SourceGen` | Roslyn `IIncrementalGenerator` that emits the `AdvisoryLocks` static class into `D2.Shared.EntityFrameworkCore.Postgres` from `contracts/advisory-locks/advisory-locks.spec.json`. Enforces per-database key uniqueness at build time. |
