<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Telemetry.Tags.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits per-meter `*TelemetryTags.g.cs` typed-constants classes by reading `contracts/telemetry/telemetry.spec.json` via `<AdditionalFiles>`. Per-meter single-target dispatch — emits ONLY when the consuming assembly matches the meter's `consumingAssembly` field.

The spec file is the single source of truth for the platform's OTel meter / instrument / tag enumeration. Every closed-enum tag value emitted by a runtime counter call site is anchored to a generated constant — drift between the spec and runtime tag-write sites is impossible. The same spec is consumable by other platforms (TS, Go) without language-specific format changes.

Untagged instruments and instruments with open-enum tags (e.g. handler-name) are spec-listed for documentation parity but receive no codegen output.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.Telemetry.Tags.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | Polyfill enabling `init` accessors on `netstandard2.0` records |
| `Polyfills/StringExt.cs` | Local `Falsey()` polyfill |
| `SpecFile.cs` | Pipeline-boundary record `(Path, Content)` — value-equatable for incremental cache stability |
| `TelemetrySpec.cs` | Parsed-shape record `(ImmutableArray<MeterEntry> Meters)` |
| `MeterEntry.cs` | `(Meter, ConsumingAssembly, TagsNamespace?, TagsClassName?, Instruments)` |
| `InstrumentEntry.cs` | `(Name, ConstName?, Kind, Description, Unit?, Tags)` |
| `TagEntry.cs` | `(Name, Values, ValuesFromSpec?)` |
| `TelemetrySpecLoader.cs` | JSON → `TelemetrySpec` parser. Emits `D2TEL001` on parse failure |
| `CrossSpecResolver.cs` | Resolves `valuesFromSpec` references against sibling `<AdditionalFiles>` (currently only `auth-error-codes`). Emits `D2TEL006` when references can't be resolved |
| `TelemetryTagsEmitter.cs` | `MeterEntry` → `*TelemetryTags.g.cs`. Emits `D2TEL002`–`D2TEL005` |
| `EmitDiagnostic.cs` | Roslyn-decoupled diagnostic record + per-id factories |
| `EmitResult.cs` | `(GeneratedSource, HintName, Diagnostics)` |
| `LoadResult.cs` | `(TelemetrySpec? Spec, EmitDiagnostic? Diagnostic)` |
| `DiagnosticIds.cs` | String IDs `D2TEL001`–`D2TEL006` |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances |
| `TelemetryTagsGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `telemetry.spec.json` (+ sibling `auth-error-codes.spec.json`), gates per-meter by assembly name, drives loader + emitter |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2TEL001` | Error | Spec file is malformed JSON or violates the schema |
| `D2TEL002` | Error | Duplicate `meter` name across the spec |
| `D2TEL003` | Error | Duplicate `instruments[].name` within a single meter |
| `D2TEL004` | Error | Unknown `kind` value (must be `counter` / `histogram` / `gauge`) |
| `D2TEL005` | Error | Duplicate value within a single tag's `values` array |
| `D2TEL006` | Error | Cross-spec reference (e.g. `valuesFromSpec=auth-error-codes`) cannot be resolved — sibling spec missing from `AdditionalFiles`, malformed, or unknown reference name |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "meters": [
    {
      "meter": "D2.Shared.Auth",
      "consumingAssembly": "D2.Shared.Auth",
      "tagsNamespace": "D2.Shared.Auth.Telemetry",
      "tagsClassName": "AuthTelemetryTags",
      "instruments": [
        {
          "name": "d2.auth.jwt.validations",
          "constName": "JwtValidations",
          "kind": "counter",
          "description": "Total inbound JWT validations.",
          "tags": [
            { "name": "outcome", "values": ["success", "expired", "..."] }
          ]
        },
        {
          "name": "d2.auth.problem.emitted",
          "constName": "ProblemEmitted",
          "kind": "counter",
          "description": "Total auth-failure responses emitted.",
          "tags": [
            { "name": "d2_error_code", "valuesFromSpec": "auth-error-codes" }
          ]
        }
      ]
    }
  ]
}
```

### Field rules

- **`meter`** — OTel meter name. Unique across the spec.
- **`consumingAssembly`** — .NET assembly name; the SrcGen single-targets emission to the matching compilation.
- **`tagsNamespace`** / **`tagsClassName`** — optional overrides for the emitted class location / name.
- **`instruments[].kind`** — closed enum `counter` / `histogram` / `gauge`.
- **`instruments[].tags[]`** — drives codegen of nested typed-constants classes. Untagged instruments / instruments with no tags get NO emitted class (documentation-only spec entry).
- **`tags[].values`** vs **`tags[].valuesFromSpec`** — exactly one is required. `valuesFromSpec` enables cross-spec resolution at codegen time (currently only `"auth-error-codes"` supported).

---

## Emitted output

Per-meter `*TelemetryTags.g.cs` file emitted into the consuming assembly. Example shape:

```csharp
public static class AuthTelemetryTags
{
    public static class JwtValidations
    {
        public const string TAG_OUTCOME = "outcome";

        public static class Outcome
        {
            public const string SUCCESS = "success";
            public const string BEARER_MISSING = "bearer_missing";
            // ...
        }
    }

    public static class ProblemEmitted
    {
        public const string TAG_D2_ERROR_CODE = "d2_error_code";
        // No nested class — d2_error_code uses valuesFromSpec=auth-error-codes;
        // consumers reference AuthErrorCodes.AUTH_* directly.
    }
}
```

---

## Wiring into a consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.Auth</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\telemetry-tags-source-gen\D2.Shared.Telemetry.Tags.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\telemetry\telemetry.spec.json" />
    <!-- Required for any meter whose tags use valuesFromSpec=auth-error-codes -->
    <AdditionalFiles Include="..\..\..\..\contracts\auth-error-codes\auth-error-codes.spec.json" />
  </ItemGroup>
</Project>
```

The per-meter `consumingAssembly` dispatch ensures only the matching assembly receives the emitted file — other consumers get the analyzer DLL but no emission.

---

## Reference

- [`contracts/telemetry/schema.json`](../../../../contracts/telemetry/schema.json) — JSON Schema for the spec
- [`contracts/telemetry/telemetry.spec.json`](../../../../contracts/telemetry/telemetry.spec.json) — the source-of-truth catalog
- [`D2.Shared.Auth.ErrorCodes.SourceGen`](../auth-error-codes-source-gen/) — sibling SrcGen whose spec the cross-spec resolver consumes
- [`D2.Shared.Auth.Scopes.SourceGen`](../auth-scopes-source-gen/) — sibling SrcGen this one mirrors (incremental-generator + diagnostic-split pattern)
