<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# entity-framework-core cluster

> Parent: [`public/packages/dotnet/`](../README.md)

Cluster of EF Core shared libraries, partitioned by provider specificity:

| Package | Description |
| --- | --- |
| [`core/`](core/README.md) — `DcsvIo.D2.EntityFrameworkCore` | Provider-agnostic EF Core migration helpers. Currently ships `CreateD2Index<TEntity>` — a `MigrationBuilder` extension for declaring indexes on `ComplexProperty` member columns. |
| [`postgres/`](postgres/README.md) — `DcsvIo.D2.EntityFrameworkCore.Postgres` | PostgreSQL-specific EF Core startup **mechanism**: `PgAdvisoryLock`, `AdvisoryLockMigrator<TContext>`, `DesignTimeDbContextFactoryBase<TContext>`, and `NpgsqlContextDefaults.ApplyD2NpgsqlDefaults`. Does **not** host domain lock-key catalogs. |
| [`locks-source-gen/`](locks-source-gen/README.md) — `DcsvIo.D2.AdvisoryLocks.SourceGen` | Roslyn `IIncrementalGenerator` that emits `AdvisoryLocks` into the **owning-module assembly** (currently `DcsvIo.D2.Private.Edge.KeyCustodian.Infra`) from `contracts/advisory-locks/advisory-locks.spec.json`. Enforces per-database key uniqueness at build time. Shared owns the tooling; the domain owns the emitted constants. |
