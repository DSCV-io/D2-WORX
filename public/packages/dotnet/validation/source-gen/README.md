<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Validation.SourceGen

> Parent: [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: D² framework engineers maintaining the shared field-constraints catalog (field-length bounds + closed-list taxonomy enums) consumed by the domain value objects, the FE/BFF Zod schemas, and arbitrary backend modules.

**Input contract:** [`contracts/validation/`](../../../../../contracts/validation/README.md)

Roslyn incremental source generator that emits `FieldConstraints.g.cs` (field-length / digit-count `const int` bounds) and `Taxonomy.g.cs` (the `NamePrefix` / `NameSuffix` / `BiologicalSex` closed-list enums) into `DcsvIo.D2.Validation.Abstractions` by reading `contracts/validation/field-constraints.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `DcsvIo.D2.Validation.Abstractions`.

The spec file is the single source of truth for the platform's shared field bounds + name/sex taxonomy. The bounds gate every value-object `Create(...)` call (contacts + Location); the enums are the closed wire vocabularies for name prefixes/suffixes and biological sex. Same spec drives the TS-side `@dcsv-io/d2-validation-abstractions` catalog via `tools/ts-codegen/src/field-constraints-emit.ts` — cross-language wire-format drift is structurally impossible.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Single-target dispatch

The single `FieldConstraintsGenerator` gates on `compilation.AssemblyName`:

- `DcsvIo.D2.Validation.Abstractions` → emit `FieldConstraints.g.cs` + `Taxonomy.g.cs`.
- Anything else → emit nothing.

Pattern mirrors `DcsvIo.D2.ResultErrorCodes.SourceGen`'s single-target dispatch (one consuming assembly) while emitting two sources via two `AddSource` calls (as the geo generator does for its multi-type emission).

---

## Build-time diagnostics

| ID        | Severity | Trigger                                                                            |
| --------- | -------- | --------------------------------------------------------------------------------- |
| `D2FC001` | Error    | Spec file is malformed JSON or violates the schema                                |
| `D2FC002` | Error    | Two field-length entries share the same `name`                                    |
| `D2FC003` | Error    | A field-length `name` is empty or does not match `^[A-Z][A-Z0-9_]*$`              |
| `D2FC004` | Error    | A field-length `value` is not a positive integer                                  |
| `D2FC005` | Error    | Two taxonomy enums share the same `name`                                          |
| `D2FC006` | Error    | A taxonomy enum `name` is empty or not a valid PascalCase identifier             |
| `D2FC007` | Error    | A taxonomy enum declares an empty `members` list                                  |
| `D2FC008` | Error    | Two members of the same enum share the same `name`                                |
| `D2FC009` | Error    | A taxonomy enum member `name` is empty or not a valid C# identifier              |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "constraints": [
    { "name": "EMAIL_MAX", "value": 254, "doc": "Maximum length of an email address." }
  ],
  "enums": [
    {
      "name": "BiologicalSex",
      "backing": "byte",
      "doc": "Closed list of biological-sex classifications.",
      "members": [{ "name": "Unspecified", "doc": "Not specified / unknown." }]
    }
  ]
}
```

### Field rules

- **`constraints[].name`** — wire-format `^[A-Z][A-Z0-9_]*$`. Unique. Becomes the emitted `public const int` name.
- **`constraints[].value`** — positive integer (`> 0`).
- **`enums[].name`** — PascalCase `^[A-Z][A-Za-z0-9]*$`. Unique. Becomes the emitted `enum` type name (`enum X : byte`).
- **`enums[].backing`** — only `byte` is supported (closed lists are small).
- **`enums[].members[].name`** — identifier-safe (`^[A-Za-z_][A-Za-z0-9_]*$`). Unique within the enum. The member name IS the wire form (string-wire via `JsonStringEnumConverter`).
- **`doc`** (on every node) — XML `<summary>` text on the emitted .NET symbol + JSDoc on the TS-side emitted symbol.

---

## Emitted output

Two `.g.cs` files emitted into the consuming assembly (`DcsvIo.D2.Validation.Abstractions`).
The committed copies land under the `EmitCompilerGeneratedFiles` output path, nested by
analyzer assembly + generator FQN:
`validation/abstractions/Generated/DcsvIo.D2.Validation.SourceGen/DcsvIo.D2.Validation.SourceGen.FieldConstraintsGenerator/{FieldConstraints,Taxonomy}.g.cs`.

- **`FieldConstraints.g.cs`** — `DcsvIo.D2.Validation.Abstractions.FieldConstraints` static class with one `public const int` per `constraints` entry.
- **`Taxonomy.g.cs`** — one `public enum X : byte` carrying `[JsonConverter(typeof(JsonStringEnumConverter))]` per `enums` entry (`NamePrefix`, `NameSuffix`, `BiologicalSex`).

---

## Layering

- TFM: `netstandard2.0` (Roslyn analyzer host requirement).
- Packaged with `IncludeBuildOutput=false` + `PrivateAssets="all"` — the analyzer travels with consumer csprojs but doesn't propagate at runtime.
- Shared scaffolding (`SpecFile`, `LoadResult<TSpec>`, `EmitDiagnostic`, `StringExt` / `IsExternalInit` polyfills) is included via the `$(D2SourceGenSharedRoot)**\*.cs` `<Compile Include>` glob.

## Telemetry

N/A — this is a build-time analyzer with no runtime surface.

## Configuration

N/A — inputs are JSON spec files declared via `<AdditionalFiles>` in the consumer csproj. No runtime configuration.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`contracts/validation/schema.json`](../../../../../contracts/validation/schema.json) — JSON Schema for the spec
- [`contracts/validation/field-constraints.spec.json`](../../../../../contracts/validation/field-constraints.spec.json) — the source-of-truth catalog
- [`tools/ts-codegen/src/field-constraints-emit.ts`](../../../../../tools/ts-codegen/src/field-constraints-emit.ts) — TS-side emitter consuming the same spec
- [`docs/PARITY.md`](../../../../../docs/PARITY.md) — cross-language parity catalog
