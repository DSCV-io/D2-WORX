<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Headers.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits per-transport header catalog classes by reading `contracts/headers/headers.spec.json` via `<AdditionalFiles>`. Dispatches per consuming assembly to one of four target catalogs.

The spec file is the single source of truth for every D2 wire header (HTTP / gRPC / AMQP). Cross-transport entries appear in multiple per-transport catalogs at identical wire values — codegen-guaranteed and verified by `HeaderCatalogConsistencyTests`.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.Headers.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | Polyfill enabling `init` accessors on `netstandard2.0` records |
| `Polyfills/StringExt.cs` | Local `Falsey()` polyfill (utilities lib targets `net10`; can't be referenced from `netstandard2.0`) |
| `SpecFile.cs` | Pipeline-boundary record `(Path, Content)` — value-equatable for incremental cache stability |
| `HeadersSpec.cs` | Parsed-shape record for the top-level spec — `(ImmutableArray<HeaderEntry> Headers)` |
| `HeaderEntry.cs` | Parsed-shape record per spec entry — `(Name, ConstName, Applicability, Convention, Description)` |
| `HeadersSpecLoader.cs` | JSON → `HeadersSpec` parser. Emits `D2HDR001` on parse failure |
| `HeadersEmitter.cs` | `HeadersSpec` + `CatalogFilter` → catalog source. Validates closed-vocabulary transport / constName pattern / applicability non-empty / per-catalog uniqueness; emits `D2HDR002`–`D2HDR006` |
| `EmitDiagnostic.cs` | Roslyn-decoupled diagnostic record + per-id factories |
| `EmitResult.cs` | `(GeneratedSource, ImmutableArray<EmitDiagnostic>)` |
| `LoadResult.cs` | `(HeadersSpec? Spec, EmitDiagnostic? Diagnostic)` |
| `DiagnosticIds.cs` | String IDs `D2HDR001`–`D2HDR007` (Roslyn-decoupled — pure-logic tests can reference) |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances (loaded only inside the host) |
| `HeadersGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `headers.spec.json`, dispatches per assembly name to one of four target catalogs |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2HDR001` | Error | Spec file is malformed JSON or violates the schema |
| `D2HDR002` | Error | An entry's `applicability` array contains an unknown transport (closed enum: `http` / `grpc` / `amqp`) |
| `D2HDR003` | Error | An entry's `constName` violates UPPER_SNAKE_CASE pattern |
| `D2HDR004` | Error | An entry's `constName` collides with another entry within the same catalog |
| `D2HDR005` | Error | An entry's `applicability` array is empty (every header must belong to at least one transport) |
| `D2HDR006` | Warning | An entry's `convention` is outside the recognized set (typo guard — emitter still emits, just flags) |
| `D2HDR007` | Error | `headers.spec.json` is missing from `<AdditionalFiles>` for the consuming csproj |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "headers": [
    {
      "name": "Idempotency-Key",
      "constName": "IDEMPOTENCY_KEY",
      "applicability": ["http"],
      "convention": "stripe",
      "description": "Idempotency key for request deduplication."
    },
    {
      "name": "x-d2-context",
      "constName": "PROPAGATED_CONTEXT",
      "applicability": ["http", "grpc", "amqp"],
      "convention": "d2",
      "description": "Base64url-of-JSON encoded PropagatedContext."
    }
  ]
}
```

### Field rules

- **`name`** — wire-format header name. Identical across every transport listed in `applicability`.
- **`constName`** — UPPER_SNAKE_CASE C# identifier. Unique within each catalog the entry belongs to.
- **`applicability`** — closed enum `http` / `grpc` / `amqp`. Every header MUST belong to at least one transport.
- **`convention`** — provenance hint (recognized: `d2` / `rfc` / `w3c` / `stripe` / `amqp` / `amqp-x` / `oauth`). Surfaced in xmldoc.
- **`description`** — XML `<summary>` text rendered on every emitted catalog the entry appears in.

---

## Catalog dispatch

| Consuming assembly | Filter | Emitted source |
|---|---|---|
| `D2.Shared.Headers.Common` | `applicability.Length >= 2` | `CommonHeaders.g.cs` (class `CommonHeaders`) |
| `D2.Shared.Headers.Http` | `applicability.Contains("http")` | `HttpHeaders.g.cs` (class `HttpHeaders`) |
| `D2.Shared.Headers.Amqp` | `applicability.Contains("amqp")` | `AmqpHeaders.g.cs` (class `AmqpHeaders`) |
| `D2.Shared.Headers.Grpc` | `applicability.Contains("grpc")` | `GrpcHeaders.g.cs` (class `GrpcHeaders`) |

Cross-transport entries appear in multiple catalogs at identical wire values, codegen-guaranteed (verified by `HeaderCatalogConsistencyTests`).

---

## Wiring into a consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.Headers.Http</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\headers-source-gen\D2.Shared.Headers.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\headers\headers.spec.json" />
  </ItemGroup>
</Project>
```

The assembly-name dispatch in `HeadersGenerator` ensures only assemblies in the closed dispatch set get emission — other consumers (test projects, transport-binding csprojs that reference `auth`) get the analyzer DLL but no emission.

---

## Reference

- [`contracts/headers/schema.json`](../../../../contracts/headers/schema.json) — JSON Schema for the spec
- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — the source-of-truth catalog
- [`D2.Shared.Auth.ErrorCodes.SourceGen`](../auth-error-codes-source-gen/) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [`D2.Shared.InProcessKeys.SourceGen`](../in-process-keys-source-gen/) — sibling SrcGen for cross-binding in-process slot keys
