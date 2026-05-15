<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.InProcessKeys.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits binding-specific in-process slot-key catalog classes by reading `contracts/in-process-keys/keys.spec.json` via `<AdditionalFiles>`. Dispatches per consuming assembly to one of two target catalogs.

The spec file is the single source of truth for cross-binding in-process slot keys (HTTP `HttpContext.Items` + gRPC `ServerCallContext.UserState`). Cross-binding entries appear in both .NET catalogs at identical wire values — codegen-guaranteed and verified by `HttpContextItemsVsGrpcUserStateKeysConsistencyTests`.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.InProcessKeys.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | Polyfill enabling `init` accessors on `netstandard2.0` records |
| `Polyfills/StringExt.cs` | Local `Falsey()` polyfill |
| `SpecFile.cs` | Pipeline-boundary record `(Path, Content)` |
| `InProcessKeysSpec.cs` | Parsed-shape record for the top-level spec |
| `KeyEntry.cs` | Parsed-shape record per spec entry — `(ConstName, Value, Purpose, Bindings)` |
| `InProcessKeysSpecLoader.cs` | JSON → `InProcessKeysSpec` parser. Emits `D2IPK001` on parse failure |
| `InProcessKeysEmitter.cs` | `InProcessKeysSpec` + `BindingFilter` → catalog source. Validates closed-vocabulary binding / constName pattern; emits `D2IPK002`–`D2IPK003` |
| `EmitDiagnostic.cs` | Roslyn-decoupled diagnostic record + per-id factories |
| `EmitResult.cs` | `(GeneratedSource, ImmutableArray<EmitDiagnostic>)` |
| `LoadResult.cs` | `(InProcessKeysSpec? Spec, EmitDiagnostic? Diagnostic)` |
| `DiagnosticIds.cs` | String IDs `D2IPK001`–`D2IPK004` |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances |
| `InProcessKeysGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `keys.spec.json`, dispatches per assembly name to either the HTTP catalog (public class in `D2.Shared.Auth.Abstractions.Http`) or the gRPC catalog (internal class in `D2.Shared.Auth.Grpc.Interceptors`) |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2IPK001` | Error | Spec file is malformed JSON or violates the schema |
| `D2IPK002` | Error | An entry's `bindings` array contains an unknown binding (closed enum: `http` / `grpc`) |
| `D2IPK003` | Error | An entry's `constName` violates UPPER_SNAKE_CASE pattern |
| `D2IPK004` | Error | `keys.spec.json` is missing from `<AdditionalFiles>` for the consuming csproj |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "keys": [
    {
      "constName": "REQUEST_CONTEXT",
      "value": "D2.RequestContext",
      "purpose": "Slot under which the inbound auth runtime writes IRequestContext.",
      "bindings": ["http", "grpc"]
    }
  ]
}
```

### Field rules

- **`constName`** — UPPER_SNAKE_CASE C# identifier.
- **`value`** — wire value of the slot key. Identical across every binding in `bindings`.
- **`purpose`** — XML `<summary>` text rendered on every emitted catalog the entry appears in.
- **`bindings`** — closed enum `http` / `grpc`. Every key MUST belong to at least one binding.

---

## Catalog dispatch

| Consuming assembly | Filter | Visibility | Emitted source |
|---|---|---|---|
| `D2.Shared.Auth.Abstractions` | `bindings.Contains("http")` | `public` | `D2HttpContextItems.g.cs` (class `D2HttpContextItems` in `D2.Shared.Auth.Abstractions.Http`) |
| `D2.Shared.Auth.Grpc` | `bindings.Contains("grpc")` | `internal` | `D2GrpcUserStateKeys.g.cs` (class `D2GrpcUserStateKeys` in `D2.Shared.Auth.Grpc.Interceptors`) |

The visibility difference (public vs internal) reflects the consumption pattern of each binding — HTTP downstream code reads `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]` directly, whereas gRPC consumers go through typed accessor extensions and the raw key class stays internal.

---

## Wiring into a consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.Auth.Abstractions</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\in-process-keys-source-gen\D2.Shared.InProcessKeys.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\in-process-keys\keys.spec.json" />
  </ItemGroup>
</Project>
```

The assembly-name dispatch in `InProcessKeysGenerator` ensures only assemblies in the closed dispatch set get emission.

---

## Reference

- [`contracts/in-process-keys/schema.json`](../../../../contracts/in-process-keys/schema.json) — JSON Schema for the spec
- [`contracts/in-process-keys/keys.spec.json`](../../../../contracts/in-process-keys/keys.spec.json) — the source-of-truth catalog
- [`D2.Shared.Headers.SourceGen`](../headers-source-gen/) — sibling SrcGen for cross-transport wire headers
- [`D2.Shared.Auth.JwtClaims.SourceGen`](../jwt-claims-source-gen/) — sibling SrcGen for JWT claim names
