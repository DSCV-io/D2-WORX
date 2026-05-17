<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Result.ErrorCodes.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits the `ErrorCodes` const-string catalog into `D2.Shared.Result` by reading `contracts/error-codes/error-codes.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.Result`.

The spec file is the single source of truth for the platform's generic error-code taxonomy. Every `d2_error_code` constant a `D2Result` failure carries and every per-code boolean discriminator on `D2Result` (e.g. `IsNotFound`, `IsConflict`) ultimately resolves through one of these constants. Same spec drives the TS-side `@d2/result` `ErrorCodes` catalog via `tools/ts-codegen/src/error-codes-emit.ts` — cross-language wire-format drift is structurally impossible.

The auth-specific taxonomy (`AUTH_*` codes) lives in a separate spec at `contracts/auth-error-codes/` driven by `D2.Shared.Auth.ErrorCodes.SourceGen` — that catalog carries additional fields (`factoryName`, `userMessageKey`, `category`) that the generic taxonomy doesn't need.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.Result.ErrorCodes.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `ErrorCodesSpec.cs` | Parsed-shape record for the top-level spec — `(ImmutableArray<ErrorCodeEntry> ErrorCodes)` |
| `ErrorCodeEntry.cs` | Parsed-shape record per spec entry — `(Code, HttpStatus, Doc)` |
| `ErrorCodesSpecLoader.cs` | JSON → `ErrorCodesSpec` parser. Emits `D2EC001` on parse failure |
| `ErrorCodesEmitter.cs` | `ErrorCodesSpec` → `ErrorCodes.g.cs`. Emits SCREAMING_SNAKE constants + `AllCodes` set + `GetHttpStatus` switch. Emits `D2EC002`–`D2EC005` |
| `EmitDiagnostics.cs` | Per-id factories on top of the shared `EmitDiagnostic` record |
| `EmitResult.cs` | `(GeneratedSource, ImmutableArray<EmitDiagnostic>)` |
| `DiagnosticIds.cs` | String IDs `D2EC001`–`D2EC005` (Roslyn-decoupled — pure-logic tests can reference) |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances (loaded only inside the host) |
| `ErrorCodesGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `error-codes.spec.json`, gates by assembly name, drives loader + emitter |

The shared polyfills + scaffolding (`Polyfills/IsExternalInit`, `Polyfills/StringExt`, `EmitDiagnostic`, `LoadResult<TSpec>`, `SpecFile`) come from `../source-gen-shared/` via a `<Compile Include>` glob.

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2EC001` | Error | Spec file is malformed JSON or violates the schema |
| `D2EC002` | Error | Two entries share the same `code` |
| `D2EC003` | Error | Entry's `httpStatus` is not in the supported set (`200` / `206` / `207` / `400` / `401` / `403` / `404` / `409` / `413` / `429` / `500` / `503`) — expanding the matrix requires updating the codegen mapping |
| `D2EC004` | Error | Entry's `code` is empty or does not match `^[A-Z][A-Z0-9_]*$` |
| `D2EC005` | Error | Entry's `doc` summary text is missing or whitespace-only |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "errorCodes": [
    {
      "code": "NOT_FOUND",
      "httpStatus": 404,
      "doc": "Indicates that the requested resource was not found."
    },
    {
      "code": "SERVICE_UNAVAILABLE",
      "httpStatus": 503,
      "doc": "Indicates that the service is currently unavailable."
    }
  ]
}
```

### Field rules

- **`code`** — wire-format `^[A-Z][A-Z0-9_]*$`. Unique. Treated as the spec-anchored constant; the literal IS the wire format.
- **`httpStatus`** — supported values today: the 12 entries in the supported set (see `D2EC003`). The JSON-Schema `enum` mirrors the codegen matrix; expanding both is a coordinated edit.
- **`doc`** — XML `<summary>` text rendered on the emitted constant + JSDoc on the TS-side emitted constant.

---

## Emitted output

One `.g.cs` file emitted into the consuming assembly (`D2.Shared.Result`):

- **`ErrorCodes.g.cs`** — `D2.Shared.Result.ErrorCodes` static class with one `public const string` per spec entry, `IReadOnlyList<string> AllCodes`, and `int GetHttpStatus(string)`.

---

## Wiring into a consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.Result</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="$(CompilerGeneratedFilesOutputPath)\**\*.cs" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\error-codes-source-gen\D2.Shared.Result.ErrorCodes.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\error-codes\error-codes.spec.json" />
  </ItemGroup>
</Project>
```

The single-target dispatch in `ErrorCodesGenerator` ensures only assemblies named `D2.Shared.Result` get the emitted `ErrorCodes.g.cs` — other consumers (test projects that reference the SrcGen for unit-testing the loader / emitter) get the analyzer dll but no emission.

---

## Reference

- [`contracts/error-codes/schema.json`](../../../../contracts/error-codes/schema.json) — JSON Schema for the spec
- [`contracts/error-codes/error-codes.spec.json`](../../../../contracts/error-codes/error-codes.spec.json) — the source-of-truth catalog
- [`D2.Shared.Auth.ErrorCodes.SourceGen`](../auth-error-codes-source-gen/README.md) — sibling SrcGen for the auth-specific `AUTH_*` taxonomy
- [`tools/ts-codegen/src/error-codes-emit.ts`](../../../../tools/ts-codegen/src/error-codes-emit.ts) — TS-side emitter consuming the same spec
- [`docs/PARITY.md`](../../../../docs/PARITY.md) — cross-language parity catalog (lists this spec)
