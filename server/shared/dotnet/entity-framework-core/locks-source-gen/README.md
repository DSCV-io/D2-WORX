<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.AdvisoryLocks.SourceGen

> Parent: [`server/shared/dotnet/entity-framework-core/`](../README.md)

**Input contract:** [`contracts/advisory-locks/`](../../../../../contracts/advisory-locks/README.md)

Roslyn incremental source generator that emits the `AdvisoryLocks` static class —
the spec-driven registry of PostgreSQL session advisory lock keys — from
`contracts/advisory-locks/advisory-locks.spec.json`.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` pattern. See
[`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention.

## What this emits

When the consuming assembly is `D2.Shared.EntityFrameworkCore.Postgres`, the generator
emits `AdvisoryLocks.g.cs` containing one nested `public static class` per database,
each holding `public const long` members per declared lock.

```csharp
public static class AdvisoryLocks
{
    /// <summary>Advisory locks owned by keycustodian_db.</summary>
    public static class KeycustodianDb
    {
        /// <summary>Blocking startup-migration lock …</summary>
        public const long MIGRATOR = 1001001001L;

        /// <summary>Try-lock guarding unattended rotation ticks …</summary>
        public const long ROTATION = 2002002002L;
    }
}
```

Consumers reach a lock key as `AdvisoryLocks.KeycustodianDb.MIGRATOR`, which makes the
database affinity visible in the type system.

## Why spec-drive this

PostgreSQL advisory locks share one global 64-bit keyspace **per database**. Two locks
accidentally sharing a key in the same database silently cause one critical section to
skip, believing the other holds it. The generator enforces **per-database key uniqueness
at build time** — a collision (`D2LCK003`) fails the build rather than silently
misbehaving at runtime.

## Per-database uniqueness rule

The uniqueness check is scoped to each database:

- Duplicate `key` **within** the same `database` → `D2LCK003` (build error).
- Duplicate `constName` **within** the same `database` → `D2LCK002` (build error).
- The **same `key` value** in **two different `database` entries** → **no diagnostic**
  (different keyspaces; each database's advisory lock namespace is independent).

## No TypeScript emitter

Advisory locks are a PostgreSQL server-side runtime primitive consumed only by the
.NET migrator and rotation hosted services. No TypeScript process opens an
`NpgsqlConnection` or calls `pg_advisory_lock`, so a TS twin would be dead code. This
is an intentional `.NET-only` carve-out; the absence of a TS emitter is not a parity gap.

## Diagnostics

| ID        | Title                                                 | Severity |
| --------- | ----------------------------------------------------- | -------- |
| `D2LCK001` | Advisory locks spec is malformed                     | Error    |
| `D2LCK002` | Duplicate constName within database                  | Error    |
| `D2LCK003` | Duplicate key within database                        | Error    |
| `D2LCK004` | constName has invalid shape                          | Error    |
| `D2LCK005` | Key value out of signed 64-bit range                 | Error    |
