<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.DataGovernance.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers decorating entity models with GDPR anonymization markers and referencing the engine seam — without pulling in EF Core or DI.

PURE GDPR-anonymization markers, the `[Anonymizable]` attribute, and the engine
seam. Zero EF Core, zero DI, zero Utilities. The EF Core engine implementation
lives in the sibling `data-governance/entity-framework-core/` library.

## Purpose

Every D² service that stores user or org PII needs a uniform, auditable way to
erase that data on demand. This library defines the vocabulary every layer shares:
ownership markers (`IUserOwned`, `IOrgOwned`), an opt-out marker
(`IExemptFromAnonymization`), a provable-completion marker
(`IAnonymizationTrackable`), the field-decoration attribute (`[Anonymizable]`),
the shared rule value object (`AnonymizationRule`), and the engine seam
(`IAnonymizationEngine` + `AnonymizationOutcome`).

Domain and host code take a dependency on this library to reference the seam
and the markers without pulling in EF Core. All implementation details — the
attribute-mapping EF convention, the fluent API, the tiered engine, the startup
guard, and DI registration — live in `DcsvIo.D2.DataGovernance.EntityFrameworkCore`.

Vocabulary throughout is **anonymization** — strictly separate from `[RedactData]`
which governs log-masking only. Do not conflate the two concerns.

## Public API surface

### Ownership and trackable markers

| Interface | Members | Purpose |
|---|---|---|
| `IUserOwned` | `Guid? UserId { get; }` | Entity is owned by a user subject. Engine filters on this id. |
| `IOrgOwned` | `Guid? OrgId { get; }` | Entity is owned by an org subject. Engine filters on this id. |
| `IExemptFromAnonymization` | _(empty marker)_ | Engine skips this entity type entirely — no field is ever overwritten even if decorated. Counted in `AnonymizationOutcome.EntityTypesSkippedExempt`. |
| `IAnonymizationTrackable` | `bool IsAnonymized { get; }` | Engine sets this to `true` via EF Core when overwriting a row; excludes already-anonymized rows on re-run for idempotency. Mandatory on any ownership-marked entity that carries `[Anonymizable]` fields. |

The `UserId` / `OrgId` properties are `Guid?` (nullable) because rows may be
created before ownership is assigned. The engine skips rows where the ownership id
is `null`.

An entity may implement both `IUserOwned` and `IOrgOwned` simultaneously.

### `[Anonymizable]` attribute

Decorates an entity property to declare its per-field anonymization strategy.
Consumed by the EF Core model convention at model-build time.

#### Call-site forms

| Usage | Resulting `AnonymizationRule` |
|---|---|
| `[Anonymizable(AnonymizeKind.SetNull)]` | `Kind = SetNull` |
| `[Anonymizable(AnonymizeKind.SetEmpty)]` | `Kind = SetEmpty` |
| `[Anonymizable("tombstone")]` | `Kind = Constant, ConstantValue = "tombstone"` |
| `[Anonymizable(template: "deletedUser{UserId}@deleted.user.dcsv.io")]` | `Kind = Template, Template = "deletedUser{UserId}@..."` |

The bare positional string is unambiguously a **constant**. The `template:` named
argument selects the template overload — never pass `AnonymizeTemplateMarker`
explicitly; the `template:` named argument selects the ctor and the compiler infers
the discriminator. Constructing the attribute in a contradictory state (e.g.
`AnonymizeKind.Constant` with no value) throws `ArgumentException` at model-build
time — this is a developer configuration error, not a runtime validation failure.

`[Anonymizable("")]` is accepted as `Constant("")`. The engine treats
`Constant("")` and `SetEmpty` identically at apply-time; they remain distinct
rules so the startup divergence guard can detect unintentional double-declarations.

This attribute is strictly separate from `[RedactData]` (log-masking). They govern
independent concerns — applying both to the same property is allowed. When both an
`[Anonymizable]` attribute and a fluent declaration target the same property, fluent
wins (Explicit &gt; DataAnnotation EF Core precedence). Identical attribute + fluent
values are accepted; a divergent pair (different `Kind` or payload) is detected by
the startup guard as a configuration error.

### `AnonymizeKind` enum

Closed set of four overwrite strategies:

| Member | Underlying value | Meaning |
|---|---|---|
| `SetNull` | 0 | Overwrite with `null` (column must be nullable). |
| `SetEmpty` | 1 | Overwrite with `""` (column must be a string type). |
| `Constant` | 2 | Overwrite with a fixed developer-supplied tombstone string. |
| `Template` | 3 | Overwrite with a computed tombstone using `{FieldName}` sibling interpolation (resolved at erasure time). `Guid` values rendered without dashes. |

### `AnonymizationRule` record

Immutable value object carrying `Kind`, `ConstantValue?`, `Template?`. Written
by the EF Core model convention or the fluent mapping API as the `D2:Anonymize`
EF model annotation; read by the anonymization engine at runtime.

Construction via `AnonymizationRule.Create(kind, constantValue, template)` — the
factory enforces the same `Kind ↔ payload` invariant as the attribute ctor.
`Constant("")` ≠ `SetEmpty` by record equality (different `Kind`), even though
the engine treats them identically at apply-time.

### `AnonymizationOutcome` record

Immutable success payload returned by `IAnonymizationEngine`. Four `required init`
counters:

| Property | Meaning |
|---|---|
| `EntityTypesProcessed` | Distinct entity CLR types examined (ownership-marked, not exempt). |
| `RowsAnonymized` | Total rows overwritten in this sweep. |
| `EntityTypesSkippedExempt` | Distinct entity CLR types skipped (`IExemptFromAnonymization`). |
| `AlreadyAnonymizedRows` | Rows excluded because `IsAnonymized` was already `true`. |

A zero-valued outcome (all counters at 0) is valid — no failure. The engine returns
`Ok(outcome-with-zeros)` when no rows matched the subject id.

### `IAnonymizationEngine` — engine seam

```csharp
Task<D2Result<AnonymizationOutcome>> AnonymizeUserAsync(Guid userId, CancellationToken ct = default);
Task<D2Result<AnonymizationOutcome>> AnonymizeOrgAsync(Guid orgId, CancellationToken ct = default);
```

- `Guid.Empty` → `D2Result.ValidationFailed`, no database writes.
- Idempotent — rows where `IsAnonymized == true` are skipped.
- Always `Task` (not `ValueTask`) — the engine always does real async DB I/O.
- Implementation: `DcsvIo.D2.DataGovernance.EntityFrameworkCore`.

## Dependencies

| Dependency | Why |
|---|---|
| `DcsvIo.D2.Result` | `IAnonymizationEngine` returns `Task<D2Result<AnonymizationOutcome>>`. |
| `JetBrains.Annotations` | `[UsedImplicitly]` on `AnonymizableAttribute`'s reflectively-consumed properties (`Kind`, `ConstantValue`, `Template`). Does not flow transitively from `DcsvIo.D2.Result` so an explicit reference is required. |

## Tests

Unit tests covering the full public API surface live in
`public/packages/dotnet/tests/Unit/DataGovernance/Abstractions/`.

Coverage includes every `[Anonymizable]` constructor form and adversarial input
(null/empty constant, whitespace template, undefined enum value, contradictory
Kind/payload combinations), `AnonymizationRule.Create` invariant enforcement,
`AnonymizationOutcome` construction, the four marker interfaces via test
implementations, and `IAnonymizationEngine` contract shape (return type, parameter
names, default `CancellationToken`).

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics. Consumers instrument the anonymization call sites in their own OTel setup.

## Configuration

No configuration — zero-config; the contracts and markers carry no tunable behavior.

## References

- Sibling implementation library: [`data-governance/entity-framework-core/`](../entity-framework-core/README.md)
- Analogous pattern: [`caching/abstractions/`](../caching/abstractions/README.md) — same marker-interface + seam structure
- [ADR-0015](../../../../../public/docs/adrs/0015-anonymization-data-governance.md) — anonymization / data-governance architecture
