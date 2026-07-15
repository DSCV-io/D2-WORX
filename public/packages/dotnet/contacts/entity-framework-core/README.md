<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Contacts.EntityFrameworkCore

> Parent: [`contacts/`](../README.md) · [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers mapping `DcsvIo.D2.Contacts` value objects into EF Core entity models via infra `IEntityTypeConfiguration<T>`.

Per-VO complex-type and value-converter mapping helpers for the DcsvIo.D2.Contacts value objects.
The helpers are called from the host's `IEntityTypeConfiguration<T>` implementation — the domain
aggregate holds plain VO-typed properties and carries zero EF references.

Each helper, in one call:

- Wires member value converters where needed (`Uri`)
- Applies `HasMaxLength` from `FieldConstraints.*` caps
- Writes the per-field anonymize defaults via the fluent `.Anonymize*` API
  from `DcsvIo.D2.DataGovernance.EntityFrameworkCore`

Ships no `DbContext`, no migrations, and no DI engine. The host owns all of those.

---

## Domain purity

Host aggregates hold VO-typed properties as plain CLR properties:

```csharp
// Host domain aggregate — ZERO EF references.
public sealed class Person
{
    public required Personal Name { get; init; }
    public EmailAddress? Email { get; init; }
}
```

All EF mapping (converters, lengths, indexes, anonymize annotations) lives in the host's infra
`IEntityTypeConfiguration<T>` class, which calls the toolkit helpers:

```csharp
// Host infra layer — PersonConfiguration.cs (NOT in the toolkit; illustrative).
internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        // Multi-field VO → complex type
        b.ComplexProperty(p => p.Name, cp => cp.MapPersonal());

        // Single-value VO → value converter, one column, caller-supplied anonymize
        b.MapEmailAddress(p => p.Email)
         .Unique("deletedUser{UserId}@deleted.user.dcsv.io");
    }
}
```

Two CLR-shape concessions (not EF dependencies):
1. Each mapped VO must be EF-materializable. Single-value VOs are handled by the `FromTrusted`
   converter; multi-field VOs are materialized via `required init` properties (EF 10 uses
   `GetUninitializedObject` + sets the `required init` props — no parameterless ctor needed).
2. Every mapped member must be EF-settable: the VOs use `init` (set at materialization). ✓

---

## Multi-field VO helpers (complex types)

Called from inside a `b.ComplexProperty(…, cp => …)` callback. No selector arg — the host
already opened the `ComplexPropertyBuilder<TComplex>`; the helper decorates `cp`'s members.

| Helper | VO | Anonymize defaults |
| --- | --- | --- |
| `cp.MapPersonal()` | `Personal` (FirstName/Middle/Last/Preferred/HashId) | FirstName → `"Deleted"` (constant); Middle/Last/Preferred → SetNull; HashId → cleared sentinel |
| `cp.MapNameAffixes()` | `NameAffixes` (Prefix/PrefixCustom/Suffix/SuffixCustom) | All four → SetNull |
| `cp.MapDemographics()` | `Demographics` (DateOfBirth/BiologicalSex) | Both → SetNull |
| `cp.MapProfessional()` | `Professional` (CompanyName/JobTitle/Department/CompanyWebsite) | CompanyName → `"Deleted"` (constant); Job/Dept/Website → SetNull |

Value converters encapsulated inside the helpers (the host never hand-wires them):
- `Professional.CompanyWebsite` → `Uri ↔ AbsoluteUri string` + `HasMaxLength(COMPANY_WEBSITE_MAX)`

**NameAffixes / Demographics all-nullable constraint.** These VOs have no required scalar
property. EF Core requires a complex type to be REQUIRED (non-nullable) unless it has at least
one required property, so the host entity must declare its `NameAffixes`/`Demographics` member
as non-nullable or EF throws at model build.

**Same-VO-type-twice** (e.g. legal name + maiden name `Personal`) works natively: the host calls
`MapPersonal()` twice via two distinct host-property selectors. EF Core 10 prefixes columns
by the owning-property path automatically (`LegalName_FirstName` vs `MaidenName_FirstName`).
The helpers never call `HasColumnName`, which preserves this default uniquification.

---

## Single-value VO helpers (value converters)

Called on the entity builder directly. Returns a coupling object that requires the caller to
supply the anonymize policy — no toolkit default is written automatically.

```csharp
b.MapEmailAddress(p => p.Email)
 .Anonymize("deleted@deleted.user.dcsv.io");        // non-unique constant

b.MapEmailAddress(p => p.Email)
 .Anonymize("deletedUser{UserId}@deleted.user.dcsv.io"); // non-unique template

b.MapEmailAddress(p => p.Email)
 .Unique("deletedUser{UserId}@deleted.user.dcsv.io");    // unique index + template
```

`MapEmailAddress` / `MapPhoneNumber` each:
1. Apply `HasConversion(EmailAddress/PhoneNumber ↔ string via FromTrusted)`
2. Apply `HasMaxLength(EMAIL_MAX / PHONE_E164_MAX)`
3. Return a coupling object (`EmailMapping` / `PhoneMapping`)

The coupling object exposes:
- `.Anonymize(templateOrConstant)` — writes the anonymize annotation; no index
- `.Unique(uniqueTemplate)` — **the ONLY path to a unique index**; requires a template that
  contains at least one `{Token}` so erased rows produce distinct values and never collide.
  Throws `ArgumentException` at map time if the template has no token.

**"Unique-without-a-uniqueness-template" is unrepresentable.** There is no parameterless
`.Unique()` — the type system removes the footgun.

**Root-scoped anonymize templates.** A value-converted email/phone column lives on the root
entity, so a `{UserId}` template resolves against the root entity's scalar siblings — the
`DcsvIo.D2.DataGovernance` V4 guard is satisfied by construction.

**Example anonymize values** (README examples, not toolkit defaults — the caller supplies the value):
- Non-unique email: `"deleted@deleted.user.dcsv.io"`
- Unique email: `"deletedUser{UserId}@deleted.user.dcsv.io"` (requires a root `UserId` scalar)
- Phone: `"10000000000"`

---

## EF Core 10 complex-member-index limitation

For the EF Core 10 limitation on indexing `ComplexProperty` member columns and the
`CreateD2Index` workaround, see
[`DcsvIo.D2.EntityFrameworkCore`](../../entity-framework-core/README.md).

---

## Per-VO anonymize-default table

| VO | Field | Default | Note |
| --- | --- | --- | --- |
| Personal | FirstName | `"Deleted"` | Non-nullable — constant required |
| Personal | MiddleName/LastName/PreferredName | SetNull | Nullable |
| Personal | HashId | cleared sentinel (`"v1." + 64×'0'`) | |
| NameAffixes | Prefix/PrefixCustom/Suffix/SuffixCustom | SetNull | All nullable |
| Demographics | DateOfBirth/BiologicalSex | SetNull | All nullable |
| Professional | CompanyName | `"Deleted"` | Non-nullable — constant required |
| Professional | JobTitle/Department/CompanyWebsite | SetNull | Nullable |
| EmailAddress | Value | **CALLER-SUPPLIED** | `.Anonymize(…)` or `.Unique(template)` |
| PhoneNumber | Value | **CALLER-SUPPLIED** | `.Anonymize(…)` or `.Unique(template)` |

Tombstone values are non-i18n literals — deliberately stable across locales.

---

## Host responsibilities

1. Register the anonymization engine: `services.AddD2DataGovernance(…)`.
2. Apply anonymization conventions: call `ApplyAnonymizationConventions()` on
   `ModelConfigurationBuilder` in `ConfigureConventions`.
3. Implement `IUserOwned` + `IAnonymizationTrackable` on entities that carry anonymizable contacts.
4. Supply a root scalar `UserId` property when using a `{UserId}` template on email/phone — the
   `DcsvIo.D2.DataGovernance` V4 guard resolves template tokens against root-entity scalar
   siblings.

---

## Telemetry

No telemetry surface — mapping helpers are pure model-build-time calls with no runtime span or metric emission.

## Edge cases / gotchas

- **`NameAffixes` / `Demographics` all-nullable constraint** — EF Core requires a complex type to be REQUIRED (non-nullable) unless it has at least one required property. Declare the host entity's `NameAffixes` / `Demographics` member as non-nullable, or EF throws at model build.
- **Same-VO-type-twice** (e.g. two `Personal` properties for legal name + maiden name) works natively. EF Core 10 prefixes columns by the owning-property path automatically (`LegalName_FirstName` vs `MaidenName_FirstName`). The helpers never call `HasColumnName`, which preserves this default uniquification.
- **Unique email/phone requires a template token** — `.Unique(template)` is the only path to a unique index on a single-value VO column. Passing a template without a `{Token}` throws `ArgumentException` at map time.

## Configuration

No configuration — the helpers carry no tunable behavior. All caps come from the shared `FieldConstraints` codegen catalog.

## Dependencies

- `DcsvIo.D2.Contacts` (`contacts/core/`) — the 6 VO types being mapped
- `DcsvIo.D2.DataGovernance.EntityFrameworkCore` — annotation key + `AnonymizationRule` factory
- `Microsoft.EntityFrameworkCore.Relational` — `EntityTypeBuilder<T>`, `ComplexPropertyBuilder<T>`,
  `PropertyBuilder<T>`, `HasMaxLength`, `HasConversion`
