<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Audiences.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits the `Audiences` static partial class into `D2.Shared.Auth.Abstractions` by reading `contracts/auth-audiences/audiences.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.Auth.Abstractions`.

The spec file is the single source of truth for the platform's JWT-audience catalog. Every value an inbound JWT's `aud` claim can carry, and every `targetAudience` argument passed to `TokenExchangeClient.ExchangeAsync`, lives in one JSON file — no hand-written parallel constants, no per-feature drift.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.Auth.Audiences.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | Polyfill enabling `init` accessors on `netstandard2.0` records |
| `SpecFile.cs` | Pipeline-boundary record `(Path, Content)` — value-equatable for incremental cache stability |
| `AudiencesSpec.cs` / `AudienceEntry.cs` | Parsed-shape records |
| `AudienceSpecLoader.cs` | JSON → `AudiencesSpec` parser. Emits `D2AUD001` on parse failure |
| `AudiencesEmitter.cs` | `AudiencesSpec` → C# source. Validates names, URLs, duplicates. Emits `D2AUD002`–`D2AUD005` |
| `EmitDiagnostic.cs` | Roslyn-decoupled diagnostic record + factories |
| `EmitResult.cs` | `(GeneratedSource, ImmutableArray<EmitDiagnostic>)` |
| `LoadResult.cs` | `(Spec, Diagnostic)` — exactly one populated |
| `DiagnosticIds.cs` | String IDs `D2AUD001`–`D2AUD006` (Roslyn-decoupled — pure-logic tests can reference) |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances (loaded only inside the host) |
| `AudiencesGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `audiences.spec.json`, gates by assembly name, drives the loader + emitter |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2AUD001` | Error | Spec file is malformed JSON or violates the schema (missing required `audiences` array, missing required per-entry `name` / `url`, wrong types) |
| `D2AUD002` | Error | Audience name violates the C# identifier convention (must match `^[A-Z][A-Za-z0-9]*$`) |
| `D2AUD003` | Error | Two audience entries share the exact same name (would produce a duplicate `const string`) |
| `D2AUD004` | Error | Two audience entries share the exact same URL (would silently alias one to the other at JWT `aud` validation time) |
| `D2AUD005` | Error | Audience URL is not a parseable absolute URI (would never match a real JWT `aud` claim value) |
| `D2AUD006` | Error | No `audiences.spec.json` found in `AdditionalFiles` |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "audiences": [
    {
      "name": "Files",
      "url": "https://files.internal",
      "description": "D² Files service — object/blob CRUD over SeaweedFS."
    }
  ]
}
```

| Field | Required | Description |
|---|---|---|
| `name` | Yes | PascalCase identifier emitted as the `const` name. `^[A-Z][A-Za-z0-9]*$`. |
| `url` | Yes | Absolute URI used as the `aud` claim value. Must parse as an absolute `Uri`. |
| `description` | No | Free-form description; emitted as XML doc on the constant. |

---

## Emitted shape

For each audience the generator emits:

```csharp
public static partial class Audiences
{
    /// <summary>D² Files service — object/blob CRUD over SeaweedFS.</summary>
    public const string Files = "https://files.internal";

    // ... other audiences ...

    public static bool IsKnown(string audience);
    public static string? Resolve(string name);
    public static string? ResolveByUrl(string url);
    public static IReadOnlySet<string> AllUrls { get; }
    public static IReadOnlyDictionary<string, string> ByName { get; }
}
```

`IsKnown(audience)` is the canonical inbound-validation helper — JWT validation calls it on the `aud` claim. `Resolve(name)` is the canonical outbound-call helper — gives outbound code the URL for a name like `"Files"`. `ResolveByUrl(url)` is the inverse for telemetry / logging that wants a friendly name.

---

## Wire-up

Consuming csproj (`D2.Shared.Auth.Abstractions`) declares the analyzer as a project reference with `OutputItemType="Analyzer"` and surfaces the spec via `<AdditionalFiles>`:

```xml
<ItemGroup>
  <ProjectReference Include="..\auth-audiences-source-gen\D2.Shared.Auth.Audiences.SourceGen.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="..\..\..\..\contracts\auth-audiences\audiences.spec.json" />
</ItemGroup>
```

`<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` on the consuming csproj makes the generated `Audiences.g.cs` visible under the consuming csproj's tracked `Generated/` directory for inspection (committed to git per the codegen-output-committed convention; see [`docs/PATTERNS.md`](../../../../docs/PATTERNS.md)).

---

## Why a separate codegen lib (instead of hand-writing Audiences)

Same reasoning as `auth-scopes-source-gen` and `JwtClaimTypes`:

- **Single source of truth.** Audience strings flow through both the inbound JWT validator (`aud` claim check) AND the outbound `TokenExchangeClient.ExchangeAsync` calls. Hand-written parallel constants on each side drift the moment a new service comes online.
- **Cross-language parity for free.** When the SvelteKit BFF or a future Node service ships, it reads the same JSON spec and emits its own `audiences.ts` constants. One spec → N language-specific abstractions libs.
- **Build-time validation.** Duplicate names, duplicate URLs, malformed URLs, and bad identifiers are caught at compile time, not at first JWT validation in production.

---

## References

- [`contracts/auth-audiences/schema.json`](../../../../contracts/auth-audiences/schema.json) — JSON Schema (editor-time gate)
- [`contracts/auth-audiences/audiences.spec.json`](../../../../contracts/auth-audiences/audiences.spec.json) — the catalog
- [`D2.Shared.Auth.Abstractions`](../auth-abstractions/) — the consuming lib (where `Audiences.g.cs` lands)
