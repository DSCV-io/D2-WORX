<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Geo.SourceGen

> Parent: [`public/packages/dotnet/README.md`](../../README.md)

> **Audience**: D² framework engineers extending or maintaining the geo data pipeline and the codegen emitting strongly-typed geo records into `DcsvIo.D2.Geo.Abstractions` and `DcsvIo.D2.Geo.Default`.

Roslyn `IIncrementalGenerator` that turns the seven pipeline-assembled geo spec files
under `contracts/geo/` into typed .NET source — consumed by `DcsvIo.D2.Geo.Abstractions`
(TYPES) and `DcsvIo.D2.Geo.Default` (DATA).

## Who consumes this

Any .NET service that needs typed ISO geo lookups —
`DcsvIo.D2.Geo.Abstractions` pulls this analyzer to receive its enums /
wrapper structs / record shapes / JsonConverters / `GeoCatalog`
constants; `DcsvIo.D2.Geo.Default` pulls the same analyzer to receive
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

- `DcsvIo.D2.Geo.Abstractions` → emit TYPES.
- `DcsvIo.D2.Geo.Default` → emit DATA.
- Anything else → emit nothing.

Pattern mirrors `DcsvIo.D2.Context.SourceGen`'s assembly-based dispatch.

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
