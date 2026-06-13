<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.EntityFrameworkCore.Postgres

> Parent: [`server/shared/dotnet/entity-framework-core/`](../README.md)
>
> **Audience**: backend .NET service engineers wiring a PostgreSQL-backed EF Core
> DbContext with advisory-lock-guarded migrations and startup validation.

PostgreSQL-specific EF Core startup machinery shared across all D2 services.
Each service that owns a per-domain database registers
`AdvisoryLockMigrator<TContext>` to safely bootstrap migrations across multiple
replicas, and calls `ApplyD2NpgsqlDefaults` from both the DI registration and the
design-time factory so the two paths can never drift.

---

## `PgAdvisoryLock`

Session-scoped PostgreSQL advisory lock helper. Opens a **dedicated**
`NpgsqlConnection`; each acquired lock scope owns its connection lifecycle.

```csharp
// Try-acquire (non-blocking — skip if held):
await using var rotLock = await PgAdvisoryLock.TryAcquireSessionAsync(
    connStr, AdvisoryLocks.KeycustodianDb.ROTATION, ct);
if (!rotLock.IsHeld)
    return; // another instance is rotating — skip this tick

// Blocking acquire (migrator):
await using var migLock = await PgAdvisoryLock.AcquireSessionBlockingAsync(
    connStr, AdvisoryLocks.KeycustodianDb.MIGRATOR, ct);
// migLock.IsHeld is always true after this returns
```

`PgAdvisoryLock` is `[MustDisposeResource]` — always use `await using`. `DisposeAsync`
sends an explicit `pg_advisory_unlock` and closes the connection.

**No `EnableRetryOnFailure`**: an execution-strategy reconnect silently drops a session
lock. Services using advisory locks must handle transient failures at the application
level.

---

## `AdvisoryLockMigrator<TContext>`

`IHostedService` that runs at host startup: ensure-database → blocking lock → migrate
→ release.

Register it before any hosted services that require the schema:

```csharp
services.AddSingleton<AdvisoryLockMigrator<MyDbContext>>(sp =>
    new AdvisoryLockMigrator<MyDbContext>(
        sp.GetRequiredService<IServiceScopeFactory>(),
        connectionString,
        AdvisoryLocks.MyDb.MIGRATOR,
        sp.GetRequiredService<ILogger<AdvisoryLockMigrator<MyDbContext>>>()));
services.AddHostedService<AdvisoryLockMigrator<MyDbContext>>();
```

Alternatively, use `AddD2AdvisoryLockMigrator<TContext>` if available from a higher-level
DI registration helper.

The migrator is **fail-fast**: a bad migration throws, crash-looping the host so the
problem surfaces immediately rather than starting with a corrupt schema.

---

## `DesignTimeDbContextFactoryBase<TContext>`

Abstract base for EF Core design-time factories in module-within-host services (no
`Sdk.Web` startup project for `dotnet ef`). Subclass in the `infra/` project:

```csharp
public sealed class MyDbContextFactory
    : DesignTimeDbContextFactoryBase<MyDbContext>
{
    protected override string ConnectionStringEnvVar => "MY_DATABASE_URL";
    protected override string MigrationsAssemblyName =>
        typeof(MyDbContextFactory).Assembly.GetName().Name!;
    protected override MyDbContext CreateContext(DbContextOptions<MyDbContext> opts)
        => new(opts);
}
```

Set `MY_DATABASE_URL` before running `dotnet ef migrations add <Name>`.

---

## `NpgsqlContextDefaults.ApplyD2NpgsqlDefaults`

Canonical `DbContextOptionsBuilder` extension that applies `UseNpgsql` with
`AddD2NodaTime()`, `CommandTimeout`, and `MigrationsAssembly`. Call from BOTH the
runtime DI lambda AND the design-time factory so neither path can drift.

`EnableRetryOnFailure` is intentionally absent — see `PgAdvisoryLock` remarks above.

---

## `AdvisoryLocks` (generated)

The `AdvisoryLocks` static class is emitted by
`D2.Shared.AdvisoryLocks.SourceGen` from
`contracts/advisory-locks/advisory-locks.spec.json` into this assembly at build time.
It provides `public const long` lock keys partitioned by database:

```csharp
AdvisoryLocks.KeycustodianDb.MIGRATOR  // 1001001001L
AdvisoryLocks.KeycustodianDb.ROTATION  // 2002002002L
```

The source generator enforces per-database key uniqueness at build time — a duplicate key
within the same database is a build error. See the
[locks-source-gen README](../locks-source-gen/README.md) for the diagnostic table.

---

## Configuration

No configuration of its own. Consumers supply:
- `connectionString` — the Npgsql connection string (e.g. from `MY_DATABASE_URL`).
- `commandTimeoutSeconds` — per-command timeout (seconds).
- `migrationsAssemblyName` — assembly holding the EF Core migrations.
- `migratorLockKey` — advisory lock bigint key for the migrator (from `AdvisoryLocks`).

---

## Dependencies

- `Microsoft.EntityFrameworkCore` — `DbContext`, `Database.MigrateAsync`, design-time
  `IDesignTimeDbContextFactory`.
- `Npgsql.EntityFrameworkCore.PostgreSQL` — `UseNpgsql`, `NpgsqlDbContextOptionsBuilder`.
- `Npgsql` — raw `NpgsqlConnection` for advisory locks and ensure-db maintenance.
- `D2.Shared.Time` — `AddD2NodaTime()` (NodaTime ↔ `TIMESTAMPTZ` value converters).
- `D2.Shared.Utilities` — `ThrowIfFalsey` / `Falsey` guards.
- `JetBrains.Annotations` — `[MustDisposeResource]` on `PgAdvisoryLock`.
