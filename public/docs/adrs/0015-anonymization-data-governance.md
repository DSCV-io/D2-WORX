<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0015: Anonymization / data-governance architecture — `DcsvIo.D2.DataGovernance`

- **Status**: Accepted
- **Date**: 2026-06-01
- **Deliverable**: `DcsvIo.D2.DataGovernance` libraries (cross-cutting anonymization foundation, a prerequisite to the Contacts library)

## Context

GDPR's right to erasure requires that, when a subject (`UserId` or `OrgId`) is erased, every host service overwrites that subject's PII **in place** with faux/tombstone values — keeping rows, foreign keys, audit trails, and legally-retained records intact — rather than nulling fields or hard-deleting. Nulling forces UIs to special-case absent values for data that conceptually always exists; hard-deleting loses audit trails and breaks foreign-key integrity.

ADR-0001 committed the Contacts library to an "annotation-driven sweep over the host's EF model — anonymize rules declared on the model at build time" for anonymization. This ADR extracts and generalizes that engine into a standalone, cross-cutting foundation that ships as a prerequisite to (and before) the Contacts library. Any D² service storing user or org PII can adopt it without depending on Contacts.

**Anonymization vs. log-masking.** These are separate concerns and must not be conflated. **Anonymization** (this ADR) is an at-rest overwrite operation on database rows — the subject's PII fields are replaced with faux tombstone values. **Log-masking** (`[RedactData]` + `RedactDataDestructuringPolicy`, ADR-0011) is a telemetry concern — PII is structurally scrubbed from log sinks at the Serilog destructuring layer. The vocabulary is deliberately distinct: `[Anonymizable]` for at-rest governance; `[RedactData]` for logging safety. Decoration is independent in both directions — an `[Anonymizable]` field need not carry `[RedactData]`, and vice versa.

## Decision

### 1. Two-library abstractions/implementation split (ADR-0006)

`DcsvIo.D2.DataGovernance.Abstractions` — pure, zero EF Core, zero DI, zero Utilities: ownership markers + the `[Anonymizable]` attribute + engine seam interfaces. Domain and host code reference this slice to declare governance intent without pulling in EF Core.

`DcsvIo.D2.DataGovernance.EntityFrameworkCore` — references Abstractions + EF Core 10: the `[Anonymizable]` EF model convention, fluent decoration API, tiered anonymization engine, startup model-validation guard, `AnonymizationEngineOptions`, and DI registration (`AddD2DataGovernance`).

Both libraries ship zero migrations, zero `DbContext`, and zero database schema.

### 2. Ownership markers reuse existing ids; no duplicate columns

`IUserOwned { Guid? UserId { get; } }` / `IOrgOwned { Guid? OrgId { get; } }` — a record may implement both. The engine builds its `WHERE` clause directly from the marker property; no separate `contactUserId` or duplicate ownership column is introduced.

`IExemptFromAnonymization` (empty marker) — the engine skips this entity type entirely, regardless of any `[Anonymizable]` decoration. Counted in `AnonymizationOutcome.EntityTypesSkippedExempt`.

`IAnonymizationTrackable { bool IsAnonymized { get; } }` — the engine sets this flag when overwriting a row; rows where `IsAnonymized == true` are excluded on re-runs (idempotency). Mandatory on any ownership-marked entity that carries `[Anonymizable]` fields; enforced by the startup guard.

### 3. Single per-field `[Anonymizable]` mechanism with four strategies

`[Anonymizable]` accepts one of four strategies via its constructor:

| Form | Strategy |
|---|---|
| `[Anonymizable(AnonymizeKind.SetNull)]` | `SetNull` — overwrite with `null` |
| `[Anonymizable(AnonymizeKind.SetEmpty)]` | `SetEmpty` — overwrite with `""` |
| `[Anonymizable("tombstone")]` | `Constant` — overwrite with a fixed developer-supplied string |
| `[Anonymizable(template: "deletedUser{UserId}@deleted.user.dcsv.io")]` | `Template` — overwrite with a computed string using `{FieldName}` sibling interpolation; `Guid` values rendered without dashes |

An undecorated field, or any field on an exempt entity, is left untouched. `[Anonymizable(null)]` / `SetNull` is equivalent to a hard-delete-to-null on that column. The earlier notion of separate erasure modes (anonymize / hard-delete / retain) collapses entirely into `[Anonymizable]` presence and the chosen strategy value.

Faux tombstone values are **non-i18n literal strings** — localization does not apply to at-rest anonymized data.

### 4. Two interchangeable front-ends to one EF runtime annotation

The EF Core engine reads only the `D2:Anonymize` model annotation at runtime — it never reflects `[Anonymizable]` directly. Two paths write that annotation; exactly one is needed per field:

**Attribute path.** An `IModelFinalizingConvention` reads `[Anonymizable]` off CLR properties — entity scalars and the properties of consumer-owned value objects / owned types / complex types — and writes the annotation. Activated by `modelBuilder.ApplyAnonymizationConventions()`. An `[Anonymizable]` on a CLR property without that call is detected by the startup guard as a configuration error.

**Fluent path (universal).** C# 14 block-form extensions on `PropertyBuilder<T>`, `OwnedNavigationBuilder<,>`, and `ComplexPropertyBuilder<>` — `.Anonymize(value)` / `.AnonymizeNull()` / `.AnonymizeTemplate("...")` / `ownedB.Anonymize(o => o.SubField, value)` — write the same annotation. This is the only path for **foreign value objects** the consumer composes but does not own (e.g. `DcsvIo.D2.Contacts` VOs, `DcsvIo.D2.Location` types) — those types carry no `[Anonymizable]` by design.

On the same property, fluent wins over the attribute (standard EF Core Fluent > Data-Annotation precedence). **Decoration is purely opt-in per field.** A PII field with no rule is not anonymized; the startup guard does not cross-check `[RedactData(PersonalInformation)]` for completeness — governance is fully decoupled from log-masking.

### 5. Tiered EF Core 10 engine

Per-entity type, classified at first use and cached (mirroring `RedactDataDestructuringPolicy`'s `ConcurrentDictionary`):

**Tier A — `ExecuteUpdateAsync` (default).** Entities whose anonymizable fields are all `SetNull` / `SetEmpty` / `Constant` on columns reachable by EF 10 `ExecuteUpdate` (scalars, table-split owned columns, complex types including JSON-serialized): one chained `ExecuteUpdateAsync` per entity type, filtered `WHERE UserId = X` (or `OrgId`), setting `IsAnonymized = true` in the same statement. One round-trip, no materialization, value converters applied automatically.

**Tier B — materialize → mutate → `SaveChanges` fallback.** Entities with any `Template`-strategy field (CLR-side `{FieldName}` interpolation does not translate to portable SQL) or any non-Tier-A column shape: chunk-load matching rows (batch size from `AnonymizationEngineOptions`, default 500), set targets in CLR, `SaveChangesAsync` per chunk in a transaction; `DbUpdateConcurrencyException` → reload-and-retry (overwrites are idempotent). Template fields force Tier B — accepted trade-off, since erasure operations are rare and typically run asynchronously.

**Tier C — fail-fast at startup.** Entity shapes that are neither Tier A nor Tier B (e.g. owned-JSON entities mapped with `.ToJson()` on an owned navigation) are surfaced by the startup guard as hard configuration errors before any traffic is served.

`OwnsMany` child-table anonymization is deferred — the startup guard detects `OwnsMany`-decorated fields and fails fast, so the limitation is never silent.

The engine is provider-agnostic: it speaks only EF Core abstractions (`IModel`, `IEntityType`, `IProperty.GetColumnName()`, `ExecuteUpdateAsync`, `SaveChangesAsync`). Column names are always read from `IProperty.GetColumnName()` — never guessed from owned-type naming conventions.

### 6. `IAnonymizationEngine` seam

```csharp
Task<D2Result<AnonymizationOutcome>> AnonymizeUserAsync(Guid userId, CancellationToken ct = default);
Task<D2Result<AnonymizationOutcome>> AnonymizeOrgAsync(Guid orgId, CancellationToken ct = default);
```

- `Guid.Empty` → `D2Result.ValidationFailed`, no database writes.
- Idempotent — rows where `IsAnonymized == true` are excluded.
- `AnonymizationOutcome` reports `EntityTypesProcessed`, `RowsAnonymized`, `EntityTypesSkippedExempt`, `AlreadyAnonymizedRows`.
- Engine is non-generic; it resolves the host's single scoped `DbContext`. A generic-`TContext` variant is a future extension if a multi-context consumer appears.

### 7. Deny-by-default startup model-validation guard

A boot-time validator walks `DbContext.Model` and the mapped CLR types and asserts:

- Every entity carrying a `D2:Anonymize` annotation implements an ownership marker (or is `IExemptFromAnonymization`) and `IAnonymizationTrackable`.
- Every annotated property classifies as Tier A or Tier B — no Tier C shapes reach runtime.
- Template fields reference an existing, supported sibling scalar.
- Every CLR property carrying `[Anonymizable]` actually produced a `D2:Anonymize` annotation (catches the attribute-present-but-`ApplyAnonymizationConventions()`-not-called footgun).
- No property carries a divergent attribute + fluent double-declaration (identical is allowed; conflicting values fail).
- `OwnsMany`-decorated fields are detected and fail fast.

Violations produce PII-safe `[LoggerMessage]` output (entity name / property name / column name — never data values) followed by `throw InvalidOperationException` before traffic is served. Decoration completeness is NOT enforced — an undecorated PII field is the consumer's responsibility.

No crypto-shred: per-subject encryption-at-rest is deferred to its own future work.

## Consequences

**Positive.**

- **Uniform subject-keyed anonymization without a central PII store.** Any service storing user or org PII marks its entities, decorates its fields, and calls `AnonymizeUserAsync` / `AnonymizeOrgAsync` — no knowledge of other services' schemas is required.
- **Provider-agnostic.** The engine speaks EF Core abstractions throughout; swapping the underlying database provider requires no engine changes.
- **Host owns nothing beyond markers and decoration.** No `DbContext`, no migrations, no extra tables.
- **One annotation read at runtime.** The engine never reflects CLR attributes in the hot path; all governance metadata is resolved into EF model annotations at model-build time.
- **Deny-by-default startup guard** converts silent misconfiguration (an ownership-marked entity with no `IsAnonymized` flag; a Tier-C shape that would throw at erasure time) into a deterministic startup failure before any traffic is served.

**Negative / risks.**

- **Template fields force the slower Tier-B path** (CLR-side formatting cannot be expressed in portable SQL). Acceptable: erasure is rare and typically runs asynchronously; the trade-off is documented.
- **`OwnsMany` child-table anonymization is deferred.** The fail-fast guard ensures the limitation is never silent, but a service using `OwnsMany` must wait for the follow-on work in the Contacts library.
- **Opt-in decoration means a forgotten PII field is the consumer's responsibility.** This is an accepted trade-off for keeping governance fully decoupled from log-masking. The guard enforces integrity of the rules that are declared; it does not enforce completeness.
- **Reflection- and metadata-driven engine complexity** is bounded by the startup guard — misconfigurations surface early rather than as runtime panics during an erasure operation.

## Alternatives considered

**NULL-wipe instead of in-place anonymization.** Setting PII columns to `NULL` is simpler but forces every UI and downstream consumer to special-case the absent value. A contact name that "disappeared" is visually distinct from a tombstone `"Deleted"` — the former breaks UX invariants. `[Anonymizable(AnonymizeKind.SetNull)]` is still available per-field where nullability is explicitly desired.

**One global pseudonym vs. per-field faux values.** Replacing all PII with a single sentinel (e.g. `"[ANONYMIZED]"`) is simpler but collapses semantics — an email address and a display name become indistinguishable tombstones. Per-field strategies allow type-appropriate faux values (a tombstone email that passes email-format validation; a tombstone name that reads naturally).

**Attribute-only decoration (no universal fluent path).** The attribute covers consumer-owned types cleanly. However, foreign/shared value objects (Contacts VOs, Location types) are authored in other libraries and carry no `[Anonymizable]` by design — without the fluent path there is no way to annotate their properties in the host's model. The fluent path is the only viable option for foreign VOs.

**Raw provider SQL `UPDATE` vs. EF tracked `SaveChanges`.** Provider-specific SQL strings (e.g. `UPDATE user_profiles SET display_name = 'Deleted' WHERE user_id = $1`) would perform well for Tier-A shapes but break provider-agnosticism and cannot apply value converters. EF's `ExecuteUpdateAsync` is now capable enough for the Tier-A case (verified against EF Core 10) and applies converters automatically.

**Mandatory `[RedactData(PersonalInformation)]` completeness enforcement.** Anchoring the startup guard to `[RedactData]` completeness would create a structural coupling between governance and logging safety — the two concerns diverge in scope (at-rest overwrite vs. telemetry masking), deployment cadence, and consumer base. A service author can correctly annotate all logging-safety concerns without wanting erasure on every field, and vice versa. Opt-in per-field decoration keeps the two concerns independent.

**Crypto-shred (encryption-at-rest per subject).** Discarding the subject's encryption key in lieu of field-overwrite is a more thorough erasure mechanism, but it requires per-subject key management infrastructure (key derivation, KMS, rotation) that does not exist yet and would be a larger, cross-cutting concern. Deferred to its own future work.

## References

- [ADR-0001](0001-contacts-folded-owned-component.md) — the Contacts library's "annotation-driven sweep over the host EF model — anonymize rules declared on the model at build time"; this ADR extracts and generalizes that engine into a standalone foundation.
- [ADR-0006](0006-abstractions-implementation-split.md) — the abstractions/implementation split applied here (`Abstractions` + `EntityFrameworkCore`).
- [ADR-0011](0011-pii-redaction-logging-safety.md) — `[RedactData]` + `SanitizedExceptionRender` + LOG-OK/NOT-LOGGED split; the separate log-masking concern that anonymization deliberately does not replace or cross-check.
- [Michael Nygard's ADR essay](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) — the format this record follows.
