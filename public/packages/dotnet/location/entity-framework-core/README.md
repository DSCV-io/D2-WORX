<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Location.EntityFrameworkCore

> Parent: [`location/`](../README.md) · [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers mapping `DcsvIo.D2.Location` value objects into EF Core entity models via infra `IEntityTypeConfiguration<T>`.

Per-VO complex-type and value-converter mapping helpers for the DcsvIo.D2.Location value
objects (`StreetAddress`, `AdminLocation`, `Coordinates`). The helpers are called from the
host's `IEntityTypeConfiguration<T>` implementation — the domain aggregate holds plain
VO-typed properties and carries zero EF references.

Each helper, in one call:

- Wires member value converters where needed (`CountryCode`, `SubdivisionCode`)
- Applies `HasMaxLength` from `FieldConstraints.*` caps (plus the encoder-intrinsic
  geohash / plus-code caps)
- Writes the per-field anonymize defaults via the fluent `.Anonymize*` API
  (`DcsvIo.D2.DataGovernance.EntityFrameworkCore`)

Ships no `DbContext`, no migrations, and no DI engine. The host owns all of those.

---

## Domain purity

Host aggregates hold VO-typed properties as plain CLR properties:

```csharp
// Host domain aggregate — ZERO EF references.
public sealed class Sighting
{
    public required StreetAddress Where { get; init; }
    public AdminLocation? Admin { get; init; }
    public Coordinates? Coords { get; init; }
}
```

All EF mapping (converters, lengths, anonymize annotations) lives in the host's infra
`IEntityTypeConfiguration<T>` class, which calls the toolkit helpers:

```csharp
// Host infra layer — SightingConfiguration.cs (NOT in the toolkit; illustrative).
internal sealed class SightingConfiguration : IEntityTypeConfiguration<Sighting>
{
    public void Configure(EntityTypeBuilder<Sighting> b)
    {
        b.ComplexProperty(s => s.Where, cp => cp.MapStreetAddress());
        b.ComplexProperty(s => s.Admin, cp => cp.MapAdminLocation());
        b.ComplexProperty(s => s.Coords, cp => cp.MapCoordinates());
    }
}
```

---

## Multi-field VO helpers (complex types)

Called from inside a `b.ComplexProperty(…, cp => …)` callback. No selector arg — the host
already opened the `ComplexPropertyBuilder<TComplex>`; the helper decorates `cp`'s members.

| Helper | VO | Anonymize defaults |
| --- | --- | --- |
| `cp.MapStreetAddress()` | `StreetAddress` (Line1–5/HashId) | Line1 → `"[deleted]"` (constant); Line2–5 → SetNull; HashId → cleared sentinel |
| `cp.MapAdminLocation()` | `AdminLocation` (City/PostalCode/Subdivision/Country/HashId) | City/PostalCode/Subdivision → SetNull; **Country KEPT** (coarse, no annotation); HashId → cleared sentinel |
| `cp.MapCoordinates()` | `Coordinates` (Latitude/Longitude/Geohash/PlusCode/AccuracyMeters/HashId) | Latitude/Longitude → `"0"` (constant, coerced to `0.0`); Geohash/PlusCode → SetEmpty; AccuracyMeters → SetNull; HashId → cleared sentinel |

Value converters encapsulated inside the helpers (the host never hand-wires them):

- `AdminLocation.SubdivisionIso31662Code` → `SubdivisionCode ↔ .Value string` + `HasMaxLength(8)`
  (an empty stored value reads back as `null`, never `SubdivisionCode("")`)
- `AdminLocation.CountryIso31661Alpha2Code` → `CountryCode enum ↔ alpha-2 name string`

**Required-numeric anonymize coercion.** `Coordinates.Latitude` / `Longitude` are required
non-nullable `double` columns, so the V7 nullable guard forbids `SetNull`. They take a
constant `"0"` string; the anonymization engine coerces it to `0.0` through the column's
type mapping at erasure time. The required geohash / plus-code strings clear to empty
(`SetEmpty`); only the nullable `AccuracyMeters` clears to null.

**Same-VO-type-twice** (e.g. home + work `AdminLocation`) works natively: the host calls
`MapAdminLocation()` twice via two distinct host-property selectors. EF Core 10 prefixes
columns by the owning-property path automatically (`HomeLocation_City` vs
`WorkLocation_City`). The helpers never call `HasColumnName`, which preserves this default
uniquification.

---

## Per-VO anonymize-default table

| VO | Field | Default | Note |
| --- | --- | --- | --- |
| StreetAddress | Line1 | `"[deleted]"` | Non-nullable — constant required |
| StreetAddress | Line2–Line5 | SetNull | Nullable |
| StreetAddress | HashId | cleared sentinel (`"v1." + 64×'0'`) | |
| AdminLocation | City/PostalCode/SubdivisionIso31662Code | SetNull | Nullable |
| AdminLocation | CountryIso31661Alpha2Code | **KEPT** | Coarse-grained, not anonymized |
| AdminLocation | HashId | cleared sentinel | |
| Coordinates | Latitude/Longitude | `"0"` (constant) | Non-nullable numeric — coerced to `0.0` |
| Coordinates | Geohash/PlusCode | SetEmpty | Non-nullable string |
| Coordinates | AccuracyMeters | SetNull | Nullable |
| Coordinates | HashId | cleared sentinel | |

Tombstone values are non-i18n literals — deliberately stable across locales.

---

## Host responsibilities

1. Register the anonymization engine: `services.AddD2DataGovernance(…)`.
2. Apply anonymization conventions: call `ApplyAnonymizationConventions()` on
   `ModelConfigurationBuilder` in `ConfigureConventions`.
3. Implement `IUserOwned` + `IAnonymizationTrackable` on entities that carry anonymizable
   location data.

---

## EF Core 10 complex-member-index limitation

For the EF Core 10 limitation on indexing `ComplexProperty` member columns (e.g.
`AdminLocation.City`) and the `CreateD2Index` workaround, see
[`DcsvIo.D2.EntityFrameworkCore`](../../entity-framework-core/README.md).

---

## Telemetry

No telemetry surface — mapping helpers are pure model-build-time calls with no runtime span or metric emission.

## Edge cases / gotchas

- **`SetNull` on required numeric columns blocked** — `Coordinates.Latitude` / `Longitude` are non-nullable `double` columns. The V7 startup guard in `DcsvIo.D2.DataGovernance` blocks `SetNull` on non-nullable columns. Both fields take a constant `"0"` anonymization rule; the engine coerces `"0"` to `0.0` through the column's type mapping at erasure time.
- **Country field intentionally kept** — `AdminLocation.CountryIso31661Alpha2Code` carries no anonymization annotation. Country is coarse-grained and deliberately retained for analytics post-erasure.
- **Same-VO-type-twice** (e.g. home + work `AdminLocation`) works natively. EF Core 10 prefixes columns by the owning-property path automatically. The helpers never call `HasColumnName`.

## Configuration

No configuration — the helpers carry no tunable behavior. All caps come from the shared `FieldConstraints` codegen catalog.

## Dependencies

- `DcsvIo.D2.Location` (`location/core/`) — the `StreetAddress` / `AdminLocation` /
  `Coordinates` VO types being mapped
- `DcsvIo.D2.Validation.Abstractions` — `FieldConstraints.*` length caps
- `DcsvIo.D2.DataGovernance.EntityFrameworkCore` — the fluent `.Anonymize*` API
- `Microsoft.EntityFrameworkCore.Relational` — `ComplexPropertyBuilder<T>`,
  `ComplexTypePropertyBuilder<T>`, `HasMaxLength`, `HasConversion`
