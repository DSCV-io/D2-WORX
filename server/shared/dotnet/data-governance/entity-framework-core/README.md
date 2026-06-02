<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.DataGovernance.EntityFrameworkCore

> Parent: [`server/shared/dotnet/`](../../README.md)

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
  `PropertyBuilder<T>`, `OwnedNavigationBuilder<TOwner, TDependent>`, or
  `ComplexPropertyBuilder<T>`. Each method writes the annotation directly via the public
  `HasAnnotation` API (Explicit configuration source).

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

    // Sub-property on a complex property
    model.Entity<Profile>()
         .ComplexProperty(p => p.DisplayName, cp =>
         {
             cp.Anonymize<string>(d => d.Value, "[deleted]");
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
| `D2.Shared.DataGovernance.Abstractions` | `AnonymizationRule`, `AnonymizableAttribute`, `AnonymizeKind`, `IAnonymizationEngine`, `IUserOwned`, `IOrgOwned`, `IExemptFromAnonymization`, `IAnonymizationTrackable` |
| `D2.Shared.Utilities` | `.Falsey()` / `.Truthy()` / `.ThrowIfFalsey()` / `SanitizedExceptionRender` |
| `Microsoft.EntityFrameworkCore.Relational` | `GetColumnName()`, `IsMappedToJson()`, `GetTableName()`, `ExecuteUpdateAsync` (Relational API surface) |
| `Microsoft.Extensions.Logging.Abstractions` | `ILogger<T>` / `[LoggerMessage]` |
| `Microsoft.Extensions.Options` | `IOptions<AnonymizationEngineOptions>` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `IServiceCollection`, `TryAddScoped`, `TryAddEnumerable`, `IServiceProvider.CreateScope()` |
| `Microsoft.Extensions.Hosting.Abstractions` | `IHostedService` |
| `Microsoft.Extensions.Configuration.Abstractions` | `IConfiguration` / `GetSection` |
| `Microsoft.Extensions.Configuration.Binder` | `.Bind(opts)` on configuration sections |

## Tests

Unit tests live in `server/shared/dotnet/tests/Unit/DataGovernance/EntityFrameworkCore/`
and `server/shared/dotnet/tests/Unit/DataGovernance/`. All contexts use the Npgsql provider
with a dummy connection string (model-build-only — the connection is never opened). Integration
tests live in `server/shared/dotnet/tests/Integration/DataGovernance/` and use
Testcontainers-PostgreSQL for end-to-end Tier-A/B anonymization assertions.

## References

- [`data-governance/abstractions/`](../abstractions/README.md) — marker interfaces, attribute, rule, and engine seam
- [`server/shared/dotnet/README.md`](../../README.md) — shared library index and dependency graph
