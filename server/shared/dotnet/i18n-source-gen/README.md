<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.I18n.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits the `TK` static class — a hierarchical catalog of `TKMessage` constants — into [`D2.Shared.I18n.Abstractions`](../i18n-abstractions/) by reading `contracts/messages/*.json` translation catalogs via `<AdditionalFiles>`. The en-US.json file is the source-of-truth for the catalog; other locales contribute coverage diagnostics (`D2I18N002` per missing key, `D2I18N004` per orphan key). The generator is the original SrcGen pattern in this codebase — sibling generators (`auth-scopes-source-gen`, `auth-audiences-source-gen`, `context-source-gen`, `messaging-source-gen`) all mirror its file layout + diagnostic-decoupling design.

The catalog is the single source of truth for translation keys. Every `TK.Common.Errors.NOT_FOUND` reference compiles against an emitted constant — adding a key is a one-line edit to `en-US.json` (the SrcGen picks it up next build); renaming a key breaks every consumer at compile time.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.I18n.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | `init`-accessor polyfill for `netstandard2.0` records |
| `Polyfills/StringExt.cs` | netstandard2.0 polyfill of `D2.Shared.Utilities.Extensions.Falsey()` (the analyzer can't reference `D2.Shared.Utilities` because that lib targets `net10`) |
| `LocaleFile.cs` | Pipeline-boundary record `(Path, LocaleCode, Content)` — value-equatable for incremental cache stability |
| `KeyDecomposer.cs` | Pure logic — translation key (`"common_errors_NOT_FOUND"`) → TK constant path (`TK.Common.Errors.NOT_FOUND`). Validates segment count, identifier shape, reserved-word avoidance |
| `DecomposedKey.cs` | `(IsValid, Domain, Category, Identifier, OriginalKey, InvalidReason?)` |
| `TKEmitter.cs` | `(enUsContent, otherLocales)` → `TK.g.cs`. Also emits `D2I18N003` (collision), `D2I18N002` (missing-in-locale), `D2I18N004` (orphan-in-locale), `D2I18N006` (malformed JSON) |
| `EmitDiagnostic.cs` | Roslyn-decoupled diagnostic record + factories |
| `EmitResult.cs` | `(GeneratedSource, ImmutableArray<EmitDiagnostic>)` |
| `DiagnosticIds.cs` | String IDs `D2I18N001`–`D2I18N006` (Roslyn-decoupled — pure-logic tests can reference) |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances (loaded only inside the host) |
| `TKGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `messages/*.json`, gates by assembly name, drives the loader + emitter |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2I18N001` | Error | Translation key violates the segment / identifier / reserved-word rules (see Key decomposition) |
| `D2I18N002` | Warning | Key present in en-US is missing from another locale file (per-locale coverage gap) |
| `D2I18N003` | Error | Two source keys decompose to the same TK constant path (e.g. `common_errors_NOT_FOUND` + `Common_Errors_NOT_FOUND`) |
| `D2I18N004` | Warning | Key present in another locale is orphaned (no en-US equivalent) |
| `D2I18N005` | Error | No `en-US.json` found in `AdditionalFiles` for a target assembly |
| `D2I18N006` | Error | A locale file failed to parse (malformed JSON) |

---

## Key decomposition

Translation keys follow the `<domain>_<category>_<NAME>` shape. The `KeyDecomposer` enforces:

- ≥ 2 underscore-separated segments (a single `flat` key is rejected).
- Each segment is a valid C# identifier (`^[A-Za-z_][A-Za-z0-9_]*$`).
- No leading / trailing / consecutive underscores in a segment.
- Decomposed `Domain` and `Category` are PascalCase'd from the segment text. `Identifier` is the raw segment (preserves original case — `NOT_FOUND` stays `NOT_FOUND`, `ip_required` stays `ip_required`).
- Decomposed identifiers don't collide with C# reserved words (the static `sr_csharpReservedWords` set covers contextual + new-since-Roslyn lowercase tokens).

A decomposition failure produces `D2I18N001` and the offending key is dropped from the emitted catalog.

---

## Spec format (translation catalog)

```json
{
  "common_errors_NOT_FOUND": "The requested resource was not found.",
  "common_errors_UNAUTHORIZED": "You are not authorized.",
  "auth_errors_INVALID_ROLE": "The role '{role}' is not valid for org '{org}'."
}
```

- **Keys**: snake_case, `<domain>_<category>_<NAME>` shape.
- **Values**: free-form translation strings. Embedded `{name}` placeholders correspond to `TKMessage.With(name, value)` parameter substitution at render time.
- **One JSON file per locale** in `contracts/messages/` (e.g. `en-US.json`, `fr-FR.json`).

---

## Emitted `TK.g.cs` shape

```csharp
public static partial class TK
{
    public static class Common
    {
        public static class Errors
        {
            public static readonly TKMessage NOT_FOUND = new("common_errors_NOT_FOUND");
            public static readonly TKMessage UNAUTHORIZED = new("common_errors_UNAUTHORIZED");
        }
    }

    public static class Auth
    {
        public static class Errors
        {
            public static readonly TKMessage INVALID_ROLE = new("auth_errors_INVALID_ROLE");
        }
    }
    // ... etc.
}
```

Each constant is a `static readonly TKMessage` carrying just the key — parameter substitution happens lazily at render time via `TKMessage.With(...)`. The `internal`-ctor design of `TKMessage` (per `D2.Shared.I18n.Abstractions`) means raw-string `D2Result.messages = ["untranslated literal"]` is structurally unrepresentable; every consumer is forced through `TK.*`.

---

## Wiring into a consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.I18n.Abstractions</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\i18n-source-gen\D2.Shared.I18n.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\messages\*.json" />
  </ItemGroup>
</Project>
```

The single-target dispatch in `TKGenerator` ensures only the `D2.Shared.I18n.Abstractions` assembly gets the emitted `TK.g.cs`. Other consumers (test projects, runtime libs) get the analyzer DLL but no emission.

---

## Cross-platform parity

The Node side uses [Paraglide](https://inlang.com/m/gerre34r) for translation key compilation (different toolchain rooted in the SvelteKit ecosystem). Per [docs/dev/rules.md §9.30](../../../docs/dev/rules.md#9-architectural-layer-hygiene), this is an intentional "Why exclusive?" carve-out — the .NET SrcGen + Node Paraglide both consume the same `contracts/messages/*.json` catalogs but produce platform-native artifacts. No `@d2/i18n-source-gen` Node mirror exists or is planned.

---

## Reference

- [`contracts/messages/en-US.json`](../../../../contracts/messages/en-US.json) — source-of-truth catalog
- [`D2.Shared.I18n.Abstractions`](../i18n-abstractions/) — emission target (defines `TKMessage`)
- [`D2.Shared.Auth.Scopes.SourceGen`](../auth-scopes-source-gen/) — sibling SrcGen modeled on this one
