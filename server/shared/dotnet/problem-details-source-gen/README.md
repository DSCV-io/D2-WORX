<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.ProblemDetails.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits the static class `D2.Shared.ProblemDetails.D2ProblemDetailsKeys` carrying the RFC 7807 ProblemDetails wire-format catalog — `TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*` extension-key constants, `TITLE_*` per-HTTP-status title constants, and the `TitleFor` switch — into [`D2.Shared.ProblemDetails.Abstractions`](../problem-details-abstractions/README.md) by reading `contracts/problem-details/problem-details.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.ProblemDetails.Abstractions`.

The spec file is the single source of truth for the RFC 7807 wire shape emitted by every .NET ProblemDetails site:

- `D2.Shared.Auth.Http.ProblemDetails.D2ProblemDetailsExtensions.ToProblemDetails` (auth-middleware emit path A)
- `D2.Shared.AspNetCore.Internal.D2ProblemDetailsCustomizer.Apply` (ASP.NET Core `IProblemDetailsService` pipeline emit path B)

The same spec drives the TS-side `@d2/headers` catalog (via `tools/ts-codegen/src/problem-details-emit.ts`) so cross-language drift on the URI prefix, content type, extension keys, and per-status titles is structurally impossible.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.ProblemDetails.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | Polyfill enabling `init` accessors on `netstandard2.0` records (via the shared `source-gen-shared/` include) |
| `Polyfills/StringExt.cs` | Local `Falsey()` polyfill (via the shared `source-gen-shared/` include) |
| `SpecFile.cs` | Pipeline-boundary record `(Path, Content)` — value-equatable for incremental cache stability (via the shared `source-gen-shared/` include) |
| `ProblemDetailsSpec.cs` | Parsed-shape record for the top-level spec — `(TypeUriPrefix, ContentType, ImmutableArray<ExtensionKeyEntry> ExtensionKeys, ImmutableArray<TitleEntry> Titles)` |
| `ExtensionKeyEntry.cs` | Parsed-shape record per extension-key entry — `(ConstName, Value, Doc)` |
| `TitleEntry.cs` | Parsed-shape record per title entry — `(ConstName, HttpStatus?, Value, Doc)` |
| `ProblemDetailsSpecLoader.cs` | JSON → `ProblemDetailsSpec` parser. Emits `D2PRB001` on parse failure |
| `ProblemDetailsEmitter.cs` | `ProblemDetailsSpec` → `D2ProblemDetailsKeys.g.cs`. Emits the static class with constants + `TitleFor` switch. Emits `D2PRB002`–`D2PRB006` |
| `EmitDiagnostics.cs` | Per-id `EmitDiagnostic` factories |
| `EmitResult.cs` | `(GeneratedSource, ImmutableArray<EmitDiagnostic>)` |
| `DiagnosticIds.cs` | String IDs `D2PRB001`–`D2PRB006` (Roslyn-decoupled — pure-logic tests can reference) |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances (loaded only inside the host) |
| `ProblemDetailsGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `problem-details.spec.json`, gates by assembly name (`D2.Shared.ProblemDetails.Abstractions`), drives loader + emitter |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2PRB001` | Error | Spec file is malformed JSON or violates the schema |
| `D2PRB002` | Error | Two extension keys share the same `constName` |
| `D2PRB003` | Error | Two extension keys share the same wire `value` |
| `D2PRB004` | Error | Two titles share the same `constName` |
| `D2PRB005` | Error | Two titles share the same `httpStatus` (only one entry may map to each status; `null` is the singular fallback) |
| `D2PRB006` | Error | `typeUriPrefix` does not end with a trailing slash (runtime appends the kebab-cased error code directly) |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "typeUriPrefix": "https://problems.d2.dcsv.io/",
  "contentType": "application/problem+json",
  "extensionKeys": [
    {
      "constName": "ERROR_CODE",
      "value": "d2_error_code",
      "doc": "The extension key carrying the machine-readable error code."
    }
  ],
  "titles": [
    {
      "constName": "UNAUTHORIZED",
      "httpStatus": 401,
      "value": "Unauthorized",
      "doc": "The closed-enum coarse Title for 401 responses."
    },
    {
      "constName": "REQUEST_FAILED",
      "httpStatus": null,
      "value": "Request Failed",
      "doc": "The fallback Title used when no httpStatus-specific entry matches."
    }
  ]
}
```

### Field rules

- **`typeUriPrefix`** — base URI for the RFC 7807 `type` field. Runtime appends the kebab-cased error code directly; MUST end with a trailing slash (codegen validates via `D2PRB006`).
- **`contentType`** — MIME type per RFC 7807 §6.1 for responses carrying a ProblemDetails body (e.g. `application/problem+json`). Consumed by `JwtAuthMiddleware.WriteProblemAsync` so the .NET wire Content-Type stays spec-driven (no hand-rolled literal).
- **`extensionKeys[].constName`** — UPPER_SNAKE_CASE. Unique. Becomes the public field name with the `EXTENSION_` prefix (e.g. `ERROR_CODE` → `EXTENSION_ERROR_CODE`).
- **`extensionKeys[].value`** — wire-format extension-key string emitted on the JSON body. Unique. The literal IS the wire format.
- **`titles[].constName`** — UPPER_SNAKE_CASE. Unique. Becomes the public field name with the `TITLE_` prefix (e.g. `UNAUTHORIZED` → `TITLE_UNAUTHORIZED`).
- **`titles[].httpStatus`** — integer status (e.g. 401, 503) OR `null` for the fallback entry. Unique across the catalog (one row per status; exactly one row MAY carry `null`).
- **`titles[].value`** — wire-format coarse Title (locale-NEUTRAL English from a closed enumeration; locale-aware translation is the client's job via the `d2_messages` extension).

---

## Emitted output

One `.g.cs` file emitted into the consuming assembly (`D2.Shared.ProblemDetails.Abstractions`):

**`D2ProblemDetailsKeys.g.cs`** — `D2.Shared.ProblemDetails.D2ProblemDetailsKeys` static class with:
- One `public const string TYPE_URI_PREFIX` declaration.
- One `public const string CONTENT_TYPE` declaration.
- One `public const string EXTENSION_*` per extension-key entry.
- One `public const string TITLE_*` per title entry.
- One `public static string TitleFor(HttpStatusCode statusCode)` switch helper.

The abstractions csproj is referenced by both `D2.Shared.Auth.Http` (path A) and `D2.Shared.AspNetCore` (path B Customizer); both transport-binding csprojs share a single emitted constant set.

---

## Wiring into the consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.ProblemDetails</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="$(CompilerGeneratedFilesOutputPath)\**\*.cs" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\problem-details-source-gen\D2.Shared.ProblemDetails.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\problem-details\problem-details.spec.json" />
  </ItemGroup>
</Project>
```

The single-target dispatch in `ProblemDetailsGenerator` ensures only assemblies named `D2.Shared.ProblemDetails.Abstractions` get the emitted `D2ProblemDetailsKeys.g.cs` — other consumers that reference the analyzer DLL transitively get no emission.

---

## Reference

- [`contracts/problem-details/schema.json`](../../../../contracts/problem-details/schema.json) — JSON Schema for the spec
- [`contracts/problem-details/problem-details.spec.json`](../../../../contracts/problem-details/problem-details.spec.json) — the source-of-truth catalog
- [`D2.Shared.ProblemDetails.Abstractions`](../problem-details-abstractions/README.md) — the consuming csproj (single emit target)
- [`D2.Shared.Auth.ErrorCodes.SourceGen`](../auth-error-codes-source-gen/README.md) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [`tools/ts-codegen/src/problem-details-emit.ts`](../../../../tools/ts-codegen/src/problem-details-emit.ts) — TS-side emitter consuming the same spec
- [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) — Problem Details for HTTP APIs
