<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.EntityFrameworkCore

> Parent: [`public/packages/dotnet/`](../README.md)
>
> **Audience**: backend .NET service engineers writing EF Core migrations that need to declare indexes on `ComplexProperty` member columns.

Generic, VO-agnostic EF Core migration helpers. Currently ships one public helper:
`CreateD2Index<TEntity>` — a `MigrationBuilder` extension for declaring indexes on
`ComplexProperty` member columns, working around the EF Core 10 complex-member-index
limitation.

Ships no `DbContext`, no migrations, no DI engine, and no VO-specific mapping. The host
owns all of those.

---

## `CreateD2Index<TEntity>` — complex-member index helper

Call from a migration class's `Up()` method when you need to index a `ComplexProperty`
member column. The helper derives the `{ComplexProp}_{Member}` column name from a typed
member-selector expression and emits a raw `CreateIndexOperation`.

```csharp
// In your Up() migration method:
migrationBuilder.CreateD2Index<Person>(
    table: "Persons",
    member: u => u.HomeLocation.City,
    unique: false);
// Emits: CREATE INDEX IX_Persons_HomeLocation_City ON "Persons" ("HomeLocation_City")

// Custom name + unique:
migrationBuilder.CreateD2Index<Person>(
    table: "Persons",
    member: u => u.HomeLocation.City,
    name: "UX_Persons_HomeLocation_City",
    unique: true);
```

The helper is model-unaware (the migration context has no access to the EF model at
migration-apply time). It derives the column name purely from the expression member chain
using EF Core 10 default complex column naming. If you override complex column prefixes via
`HasColumnName` in your entity configuration, you own the index column name too — but the
D2 toolkit mapping helpers never call `HasColumnName`.

**Null and empty guards.** `ArgumentNullException` is thrown for a null `table` or null
`member` expression. `ArgumentException` is thrown for an empty or whitespace-only `table`.

---

## ⚠️ EF Core 10 limitation — indexing complex-type members

### The limitation

A `ComplexProperty` member column **cannot be indexed model-aware in EF Core 10**.

All three attempted paths fail:

- Fluent `HasIndex(u => u.Vo.Member)` — throws `InvalidOperationException`: "not a valid
  member access expression"
- `HasIndex("Vo_Member")` — throws: shadow property needs a type
- `AddIndex([complexMemberProp])` via metadata + finalizing convention — **silently
  discarded** at finalization: `"index properties … not declared on the entity type"` →
  `IMigrationsModelDiffer` emits **zero** `CreateIndexOperation`

### Proof summary

Spike (EF Core 10.0.7, Npgsql 10.0.1, real Postgres round-trip): all three fluent/metadata
paths were exercised. The model-differ produced zero `CreateIndexOperation`s for every
attempted complex-member index path. The EF team has acknowledged this as a gap in issue
[#31246](https://github.com/dotnet/efcore/issues/31246).

### The `CreateD2Index` workaround

Use `CreateD2Index` in the host's migration class (see the recipe above). The column-name
derivation uses the expression member chain under EF Core 10 default complex column naming
(`{ComplexProp}_{Member}`, joined with `_`).

### EF Core 11 native path

EF Core 11 (issue #31246 closed, milestone 11.0.0; PR
[#38192](https://github.com/dotnet/efcore/pull/38192) merged 2026-05-19) makes
`HasIndex(u => u.Vo.Member)` native inside `IEntityTypeConfiguration`. When the host
upgrades to EF Core 11, move the index declaration to the configuration class and remove
the `CreateD2Index` call from the migration.

### Value-converter and query have no limitation

Value-converted single-value VO columns (`EmailAddress`, `PhoneNumber`) are first-class —
they can be indexed or made unique via `HasIndex(u => u.Email).IsUnique()` in the entity
configuration (no workaround). Complex-member **queries**
(`WHERE HomeLocation_City = 'London'`) also work fine with clean JOIN-free SQL — only the
model-aware **index declaration** on a complex member is limited.

---

## Telemetry

No telemetry surface — migration helpers run at migration-apply time with no runtime span or metric emission.

## Edge cases / gotchas

- **Model-unaware column name derivation** — `CreateD2Index` derives the column name purely from the expression member chain under EF Core 10 default complex column naming (`{ComplexProp}_{Member}`, joined with `_`). If the host overrides complex column prefixes via `HasColumnName` in entity configuration, the host must supply the correct `name:` override argument; the toolkit mapping helpers (`MapPersonal`, `MapStreetAddress`, etc.) never call `HasColumnName`, so the default derivation is always correct when using those helpers.
- **EF 11 supersedes this workaround** — EF Core 11 (issue [#31246](https://github.com/dotnet/efcore/issues/31246), milestone 11.0.0) makes `HasIndex(u => u.Vo.Member)` native. When the host upgrades, move the index declaration to the entity configuration class and remove the `CreateD2Index` migration call. See the README section above.

## Configuration

No configuration — the helper carries no tunable behavior.

## Dependencies

- `Microsoft.EntityFrameworkCore.Relational` — `MigrationBuilder`, `CreateIndexOperation`,
  `OperationBuilder<CreateIndexOperation>`
- `DcsvIo.D2.Utilities` — `ThrowIfFalsey` for a combined null/empty/whitespace guard on
  required string parameters (cycle-free — Utilities does not reference EF or this lib)
