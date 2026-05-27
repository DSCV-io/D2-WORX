<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Geo.SourceGen

> Parent: [`server/shared/dotnet/README.md`](../README.md)

Roslyn `IIncrementalGenerator` that turns the seven pipeline-assembled geo spec files
under `contracts/geo/` into typed .NET source — consumed by `D2.Shared.Geo.Abstractions`
(TYPES) and `D2.Shared.Geo.Default` (DATA).

## Who consumes this

Any .NET service that needs typed ISO geo lookups —
`D2.Shared.Geo.Abstractions` pulls this analyzer to receive its enums /
wrapper structs / record shapes / JsonConverters / `GeoCatalog`
constants; `D2.Shared.Geo.Default` pulls the same analyzer to receive
the per-entity static instance data and lookup tables.

## Inputs

Seven spec files under `contracts/geo/`:

| Spec | Source |
|---|---|
| `countries.spec.json` | Pipeline-derived |
| `subdivisions.spec.json` | Pipeline-derived |
| `currencies.spec.json` | Pipeline-derived |
| `languages.spec.json` | Pipeline-derived |
| `locales.spec.json` | Pipeline-derived |
| `timezones.spec.json` | Pipeline-derived |
| `geopolitical-entities.spec.json` | Hand-rolled |

Each spec carries the envelope shape `{ catalogVersion, generatedAt, entries: [...] }`.

## Multi-assembly dispatch

The single `GeoGenerator` inspects `compilation.AssemblyName` and
dispatches per target:

- `D2.Shared.Geo.Abstractions` → emit TYPES.
- `D2.Shared.Geo.Default` → emit DATA.
- Anything else → emit nothing.

Pattern mirrors `D2.Shared.Context.SourceGen`'s assembly-based dispatch.

## Diagnostic IDs

| ID | Trigger |
|---|---|
| `D2GEO001` | Malformed JSON / parse failure of a spec file |
| `D2GEO002` | FK code refers to entity not present in target catalog |
| `D2GEO003` | FK detection ambiguity — field name unmatched by naming convention and no `fkTo` annotation |
| `D2GEO004` | Geo code cannot form a valid C# identifier (reserved for nested-class shell emission) |
| `D2GEO005` | Vocabulary discipline violation — forbidden `region` / `state` / `province` at identifier position |
| `D2GEO006` | Missing or invalid `catalogVersion` / `generatedAt` in a spec |
| `D2GEO007` | Required spec file missing from `AdditionalFiles` (one of the seven canonical names) |
| `D2GEO009` | Structural-parity mismatch — spec field exists but no matching emitted record property |

## Layering

- TFM: `netstandard2.0` (Roslyn analyzer host requirement).
- Build-time data structures inside the generator use plain
  `HashSet<string>` / `Dictionary<TKey, TValue>` —
  `System.Collections.Frozen` is .NET 8+ only and unavailable on
  `netstandard2.0`. The emitted text (compiled on the consumer's `net10`
  target) freely references `FrozenSet` / `FrozenDictionary` as string
  literals; the generator itself never invokes `ToFrozenSet`.
- Packaged with `IncludeBuildOutput=false` + `PrivateAssets="all"` — the
  analyzer travels with consumer csprojs but doesn't propagate at
  runtime.
- Shared scaffolding (`SpecFile`, `LoadResult<TSpec>`, `EmitDiagnostic`,
  `StringExt` / `IsExternalInit` polyfills) is included via the
  `..\source-gen-shared\**\*.cs` `<Compile Include>` glob.

## Telemetry

N/A — this is a build-time analyzer with no runtime surface.

## Configuration

N/A — inputs are JSON spec files declared via `<AdditionalFiles>` in
the consumer csproj. No runtime configuration.
