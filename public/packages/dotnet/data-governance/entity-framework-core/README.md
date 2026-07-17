<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.DataGovernance.EntityFrameworkCore

> Parent: [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers decorating EF Core entity models for GDPR anonymization — wiring the fluent `.Anonymize*` API, registering the engine, and configuring the startup model guard.

EF Core metadata layer for GDPR anonymization. Converges the attribute and fluent
decoration front-ends onto one `AnonymizationRule` stored as the `D2:Anonymize` EF Core
model annotation. The anonymization engine reads only that annotation at runtime.
Includes the `AnonymizationEngine` (erases PII in place on subject erasure),
`AnonymizationModelValidator` (deny-by-default boot guard), and `AddD2DataGovernance`
(DI entry point).

## Purpose

Two decoration paths produce the same annotation on a mapped property:

- **Attribute path** — `[Anonymizable(...)]` on the CLR property, activated by
  `ApplyAnonymizationConventions()` in `ConfigureConventions`. The
  `AnonymizableAttributeConvention` (`IModelFinalizingConvention`) walks every mapped scalar
  and complex sub-property at model-finalization time, reads the attribute, and writes the
  `D2:Anonymize` annotation with DataAnnotation configuration source.
- **Fluent path** — `Anonymize*` extension methods called in `OnModelCreating` on
  `PropertyBuilder<T>`, `OwnedNavigationBuilder<TOwner, TDependent>`,
  `ComplexPropertyBuilder<T>`, or `ComplexTypePropertyBuilder<TProperty>`. Each method
  writes the annotation directly via the public `HasAnnotation` API (Explicit
  configuration source).

When both paths target the same property, the fluent declaration wins. EF Core's
config-source precedence (Explicit > DataAnnotation) enforces this automatically.

## Public API surface

### Annotation key

| Constant | Value | Description |
|---|---|---|
| `AnonymizationAnnotations.ANONYMIZE` | `"D2:Anonymize"` | Key under which `AnonymizationRule` is stored on a mapped property. |

### Fluent extension methods

All overloads are C# 14 block-form extensions. Each writes an `AnonymizationRule` via
`AnonymizationRule.Create` and the public `HasAnnotation` API (Explicit source).

#### On `PropertyBuilder<TProperty>` — entity scalars and directly-reached VO fields

| Method | Resulting rule |
|---|---|
| `.Anonymize(string constant)` | `Create(Constant, constantValue: constant)` |
| `.AnonymizeNull()` | `Create(SetNull)` |
| `.AnonymizeEmpty()` | `Create(SetEmpty)` |
| `.AnonymizeTemplate(string template)` | `Create(Template, template: template)` |

#### On `ComplexTypePropertyBuilder<TProperty>` — complex-type member columns

Use this overload when you already hold the member builder from `cp.Property(lambda)`.
Typical usage: inside a `ComplexProperty(…, cp => { … })` block after `cp.Property(…)`.

| Method | Resulting rule |
|---|---|
| `.Anonymize(string constant)` | `Create(Constant, constantValue: constant)` |
| `.AnonymizeNull()` | `Create(SetNull)` |
| `.AnonymizeEmpty()` | `Create(SetEmpty)` |
| `.AnonymizeTemplate(string template)` | `Create(Template, template: template)` |

This overload is structurally identical to `PropertyBuilder<TProperty>` (same four methods,
same BCL guards, same `HasAnnotation` write with Explicit configuration source). The receiver
type differs: EF Core returns `ComplexTypePropertyBuilder<TProperty>` from
`cp.Property(lambda)`, which is a distinct CLR type from `PropertyBuilder<TProperty>`, so C# 14
extension-member inference requires a separate overload block.

A `[NotMapped]` guard is not needed on this block: `cp.Property(x => x.Member)` itself throws
upstream in EF before this overload is ever reached for an unmapped member, so a
`[NotMapped]` member cannot reach this code. Contrast with the
`ComplexPropertyBuilder<TComplex>` selector-based block below, which DOES check `[NotMapped]`
because it accepts a member-selector lambda and calls `builder.Property(sub)` internally.

#### On `OwnedNavigationBuilder<TOwner, TDependent>` — `OwnsOne`/`OwnsMany` foreign-VO sub-properties

| Method | Resulting rule on the resolved sub-property |
|---|---|
| `.Anonymize<TProp>(Expression<Func<TDependent, TProp>> sub, string constant)` | `Create(Constant, constantValue: constant)` |
| `.AnonymizeNull<TProp>(Expression<Func<TDependent, TProp>> sub)` | `Create(SetNull)` |
| `.AnonymizeEmpty<TProp>(Expression<Func<TDependent, TProp>> sub)` | `Create(SetEmpty)` |
| `.AnonymizeTemplate<TProp>(Expression<Func<TDependent, TProp>> sub, string template)` | `Create(Template, template: template)` |

Each resolves the inner `PropertyBuilder` via `builder.Property(sub)` and writes the
annotation with Explicit source.

#### On `ComplexPropertyBuilder<TComplex>` — `ComplexProperty(...)` foreign-VO sub-properties

Same four-method shape as `OwnedNavigationBuilder`, with `Expression<Func<TComplex, TProp>>`.

### Activation extension

| Method | Target | Description |
|---|---|---|
| `ApplyAnonymizationConventions()` | `ModelConfigurationBuilder` | Registers `AnonymizableAttributeConvention`. Call from `ConfigureConventions`. |

## Decoration paths

### Attribute path — consumer-owned types

Decorate CLR properties with `[Anonymizable(...)]` and call
`builder.ApplyAnonymizationConventions()` in `ConfigureConventions`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    base.ConfigureConventions(builder);
    builder.ApplyAnonymizationConventions();
}
```

The convention walks all entity types (including EF-surfaced owned-entity types) and all
declared complex properties at finalization time. Only mapped properties are reached;
`[NotMapped]` or unmapped members are invisible to the convention.

### Fluent path — foreign VOs and overrides

Decorate in `OnModelCreating` for types the consumer does not own (or when overriding an
attribute):

```csharp
protected override void OnModelCreating(ModelBuilder model)
{
    // Scalar on an entity
    model.Entity<User>()
         .Property(u => u.Email)
         .AnonymizeTemplate("deletedUser{UserId}@deleted.user.dcsv.io");

    // Sub-property on an owned navigation (OwnsOne)
    model.Entity<User>()
         .OwnsOne(u => u.Address, nav =>
         {
             nav.AnonymizeNull<string?>(a => a.Street);
             nav.AnonymizeEmpty<string?>(a => a.PostalCode);
         });

    // Sub-property on a complex property (selector-based — holds ComplexPropertyBuilder<T>)
    model.Entity<Profile>()
         .ComplexProperty(p => p.DisplayName, cp =>
         {
             cp.Anonymize<string>(d => d.Value, "[deleted]");
         });

    // Sub-property on a complex property (receiver-based — holds ComplexTypePropertyBuilder<T>)
    // clearedSentinel is a caller-defined tombstone, e.g. "v1." + new string('0', 64)
    model.Entity<Address>()
         .ComplexProperty(a => a.Location, cp =>
         {
             cp.Property(l => l.City).HasMaxLength(100).AnonymizeNull();
             cp.Property(l => l.PostalCode).HasMaxLength(20).AnonymizeEmpty();
             cp.Property(l => l.HashId).HasMaxLength(67).Anonymize("v1." + new string('0', 64));
         });
}
```

### Precedence

| Scenario | Result |
|---|---|
| Attribute only (convention active) | Attribute rule applied (DataAnnotation source) |
| Fluent only | Fluent rule applied (Explicit source) |
| Both (convention active + fluent) | Fluent rule wins (Explicit > DataAnnotation) |
| Attribute only, convention NOT registered | No annotation — property is untouched by the engine |

Divergent attribute + fluent declarations (different strategies on the same property) are not
an error at this layer — fluent wins. Detection of divergent double-declarations is the
responsibility of the startup model guard.

## Anonymization engine

`AnonymizationEngine` (`IAnonymizationEngine`) erases PII on subject deletion. Register it
via `AddD2DataGovernance`.

```csharp
// In a hosted service or CQRS command handler:
var result = await engine.AnonymizeUserAsync(userId, ct);
```

Returns `D2Result<AnonymizationOutcome>`. Fail-closed: any entity-type failure returns a
non-Ok result — never silent partial success.

### Tiers

| Tier | Shape | Strategy |
|---|---|---|
| A | Scalar, table-split owned, complex (incl. complex-typed JSON columns) | `ExecuteUpdateAsync` — no rows materialized |
| B | Any entity with a Template rule | Materialize → mutate in CLR → `SaveChangesAsync` (chunked, concurrency-aware) |
| C | Owned-JSON, `OwnsMany` child table | Fail-fast at boot — blocked by the startup guard |

### Idempotency

Every query filters on `IsAnonymized == false`. Re-running for the same subject is safe.

## Startup model guard

`AnonymizationModelValidator` runs as an `IHostedService` and validates the host
`DbContext` model at start-up, aborting boot with a PII-safe `InvalidOperationException`
on any misconfiguration. The guard checks:

| Rule | Description |
|---|---|
| V1 | Every decorated entity implements `IUserOwned`/`IOrgOwned` or `IExemptFromAnonymization` |
| V2 | Every decorated non-exempt entity implements `IAnonymizationTrackable` |
| V3 | No decorated entity is Tier-C (owned-JSON or `OwnsMany` child) |
| V4 | Every `Template` rule's `{Token}` names an existing scalar sibling |
| V5 | Every CLR `[Anonymizable]`-decorated property has a `D2:Anonymize` annotation (detects missing `ApplyAnonymizationConventions()`) |
| V6 | No `[Anonymizable]`-decorated property has a surviving annotation rule that differs from the attribute rule (divergent attribute + fluent double-declaration) |
| V7 | No `SetNull` rule targets a non-nullable column |

All findings are collected before throwing so operators see the full list in one boot attempt.
Opt out (test hosts only) by setting `DATA_GOVERNANCE__SKIPMODELVALIDATION=true`.

## DI registration

Register everything via `AddD2DataGovernance`:

```csharp
services.AddD2DataGovernance(configuration);

// Or with a configuration callback (wins over the bound section value):
services.AddD2DataGovernance(configuration, opts => opts.BatchSize = 200);
```

This registers:
- `IAnonymizationEngine` → `AnonymizationEngine` (scoped — matches the host `DbContext`)
- `AnonymizationEngineOptions` bound from the `DATA_GOVERNANCE` configuration section
- `AnonymizationModelValidator` as a singleton `IHostedService`

**DbContext requirement:** the engine and validator resolve the non-generic `DbContext` from
a created scope. Register the host's concrete context as `DbContext`:

```csharp
// AddDbContext<T> registers both T and DbContext by default.
services.AddDbContext<MyContext>(...);

// Or explicitly, when AddDbContext is not used:
services.AddScoped<DbContext>(sp => sp.GetRequiredService<MyContext>());
```

## Options

`AnonymizationEngineOptions` (section `DATA_GOVERNANCE`):

| Property | Default | Description |
|---|---|---|
| `BatchSize` | 500 | Maximum rows per Tier-B chunk |
| `MaxConcurrencyRetries` | 3 | Reload-retry ceiling on `DbUpdateConcurrencyException` |
| `SkipModelValidation` | false | Disables the startup model guard — test hosts only |

## Dependencies

| Dependency | Role |
|---|---|
| `DcsvIo.D2.DataGovernance.Abstractions` | `AnonymizationRule`, `AnonymizableAttribute`, `AnonymizeKind`, `IAnonymizationEngine`, `IUserOwned`, `IOrgOwned`, `IExemptFromAnonymization`, `IAnonymizationTrackable` |
| `DcsvIo.D2.Utilities` | `.Falsey()` / `.Truthy()` / `.ThrowIfFalsey()` / `SanitizedExceptionRender` |
| `Microsoft.EntityFrameworkCore.Relational` | `GetColumnName()`, `IsMappedToJson()`, `GetTableName()`, `ExecuteUpdateAsync` (Relational API surface) |
| `Microsoft.Extensions.Logging.Abstractions` | `ILogger<T>` / `[LoggerMessage]` |
| `Microsoft.Extensions.Options` | `IOptions<AnonymizationEngineOptions>` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `IServiceCollection`, `TryAddScoped`, `TryAddEnumerable`, `IServiceProvider.CreateScope()` |
| `Microsoft.Extensions.Hosting.Abstractions` | `IHostedService` |
| `Microsoft.Extensions.Configuration.Abstractions` | `IConfiguration` / `GetSection` |
| `Microsoft.Extensions.Configuration.Binder` | `.Bind(opts)` on configuration sections |

## Tests

Unit tests live in `public/packages/dotnet/tests/Unit/DataGovernance/EntityFrameworkCore/`
and `public/packages/dotnet/tests/Unit/DataGovernance/`. Unit-test contexts are
model-build-only — no database connection is ever opened. Most use `UseNpgsql` with a
dummy connection string to exercise Npgsql-specific relational metadata (column types,
JSON mapping) that the in-memory provider does not model; a few use the EF Core in-memory
provider (`UseInMemoryDatabase`) where only generic annotation/model-build logic is under
test. Integration tests live in `public/packages/dotnet/tests/Integration/DataGovernance/`
and use Testcontainers-PostgreSQL for end-to-end Tier-A/B anonymization assertions.

## Telemetry

No telemetry surface — the engine emits no spans or metrics. The `AnonymizationModelValidator` logs a PII-safe `InvalidOperationException` message on boot-guard failure; all other log events are at Debug level. Consumers instrument the anonymization call sites in their own OTel setup.

## Edge cases / gotchas

- **Tier-C entities blocked at boot** — any owned-JSON (`ToJson()`) or `OwnsMany` child-table entity decorated with `[Anonymizable]` causes `AnonymizationModelValidator` to abort host startup. Tier-C shapes require full materialization before overwrite; bulk `ExecuteUpdateAsync` cannot reach them. Restructure as first-class root entities or mark `[ExemptFromAnonymization]`.
- **Fluent wins over attribute** — when both an `[Anonymizable]` attribute and a fluent `.Anonymize*` call target the same property, the fluent declaration wins (EF Core Explicit > DataAnnotation configuration source). Divergent double-declarations are detected by the startup guard (V6 check).
- **`SetNull` on non-nullable column blocked at boot** — V7 check in `AnonymizationModelValidator` detects `SetNull` rules targeting non-nullable columns and aborts startup. Use a `Constant` or `Template` rule instead.

## Configuration

`AnonymizationEngineOptions` (configuration section `DATA_GOVERNANCE`):

| Env var | Default | Description |
|---------|---------|-------------|
| `DATA_GOVERNANCE__BATCHSIZE` | 500 | Maximum rows per Tier-B chunk |
| `DATA_GOVERNANCE__MAXCONCURRENCYRETRIES` | 3 | Reload-retry ceiling on `DbUpdateConcurrencyException` |
| `DATA_GOVERNANCE__SKIPMODELVALIDATION` | false | Disables the startup model guard — test hosts only |

## References

- [`data-governance/abstractions/`](../abstractions/README.md) — marker interfaces, attribute, rule, and engine seam
- [`public/packages/dotnet/README.md`](../../README.md) — shared library index and dependency graph
