<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.I18n.Keys

> Parent: [`public/packages/dotnet/`](../../README.md)

Foundational slice that exposes the type-safe `TK` constants catalog — one `static readonly TKMessage` per translation key, Source-Generated from `contracts/messages/en-US.json`. Every producer that emits a user-facing message references a `TK.*` constant (e.g. `TK.Common.Errors.NOT_FOUND`) rather than a raw string, so the wire stays language-neutral and the client resolves the final copy.

Its only dependency is the sibling [`DcsvIo.D2.I18n.Abstractions`](../abstractions/README.md) — the project that defines the `TKMessage` type each constant is an instance of. Keeping the constants in this shallow project lets any layer reference them without dragging in the runtime `Translator` (DI / configuration / file IO), which lives in the separate [`DcsvIo.D2.I18n`](../core/README.md) project.

---

## Public API

| Export | Purpose                                                                                                                                                                                          |
| ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TK`   | Nested `static partial class` chains of `TKMessage` constants, each whose `Key` is its snake-case message key (e.g. `TK.Common.Errors.NOT_FOUND.Key == "common_errors_NOT_FOUND"`). One constant per key in `en-US.json`. |

```csharp
// TK.Common.Errors.NOT_FOUND is a TKMessage — drop straight into D2Result:
//   D2Result<T>.NotFound(messages: [TK.Common.Errors.NOT_FOUND]);
// Parameterized — bind via With():
//   TK.Auth.Errors.PASSWORD_WEAK.With("minLength", "12")
```

---

## Dependency edge

```
DcsvIo.D2.I18n.Keys  ──►  DcsvIo.D2.I18n.Abstractions   (TKMessage type)
```

Each generated `TK.*` constant is a `TKMessage` instance — `new("common_errors_NOT_FOUND")` — so this project references Abstractions for the `TKMessage` type. That constructor is internal (producers can only synthesize a `TKMessage` via these constants, never from a raw string), so Abstractions grants this assembly access with `[InternalsVisibleTo("DcsvIo.D2.I18n.Keys")]`.

The TS side mirrors this exactly: `@dcsv-io/d2-i18n-keys → @dcsv-io/d2-i18n-abstractions`, where TS `TK` constants are likewise `TKMessage` instances (not bare strings). Single `contracts/messages/en-US.json` spec, two emitters, drift structurally impossible.

---

## The TK source generator

`DcsvIo.D2.I18n.SourceGen.TKGenerator` (at [`../source-gen/`](../source-gen/README.md), referenced here as a Roslyn Analyzer) emits `TK.g.cs` into this assembly. It:

1. Reads `contracts/messages/*.json` via the `<AdditionalFiles>` declared in this csproj.
2. Treats `en-US.json` as the source of truth.
3. Decomposes each key (`{domain}_{category}_{IDENTIFIER}`) into a TK path (`TK.Domain.Category.IDENTIFIER`).
4. Emits nested `static partial class` chains with one `static readonly TKMessage` per key.
5. Cross-checks every other locale against en-US to surface translation gaps at build time.

### Decomposition rules

JSON keys follow `{domain}_{category}_{IDENTIFIER}` where:

- Segment 0 → top-level nested class (PascalCase: `common` → `Common`)
- Segment 1 → second-level nested class (PascalCase: `errors` → `Errors`)
- Segments 2..N joined by `_` and uppercased → constant name
- Field value = original JSON key string

| JSON key                                | Generated path                             | Field value                               |
| --------------------------------------- | ------------------------------------------ | ----------------------------------------- |
| `common_errors_NOT_FOUND`               | `TK.Common.Errors.NOT_FOUND`               | `"common_errors_NOT_FOUND"`               |
| `geo_validation_ip_required`            | `TK.Geo.Validation.IP_REQUIRED`            | `"geo_validation_ip_required"`            |
| `auth_email_signup_subject`             | `TK.Auth.Email.SIGNUP_SUBJECT`             | `"auth_email_signup_subject"`             |
| `geo_validation_address_line1_required` | `TK.Geo.Validation.ADDRESS_LINE1_REQUIRED` | `"geo_validation_address_line1_required"` |

### Build-time diagnostics

| ID          | Severity | Trigger                                                                                                             |
| ----------- | -------- | ------------------------------------------------------------------------------------------------------------------- |
| `D2I18N001` | Warning  | A JSON key cannot be decomposed (fewer than 3 segments, invalid C# identifier, etc.). The offending key is skipped. |
| `D2I18N002` | Warning  | A key in en-US is missing from another locale catalog. The key is still emitted in TK.                              |
| `D2I18N003` | Error    | Two distinct JSON keys decompose to the same TK path. Build-failing.                                                |
| `D2I18N004` | Warning  | A key exists in a non-en-US locale but has no matching entry in en-US. NOT included in TK.                          |
| `D2I18N005` | Error    | The generator can't find `en-US.json` among AdditionalFiles. TK class is empty.                                     |
| `D2I18N006` | Error    | A JSON catalog file is malformed (parse failure). The offending file is skipped.                                    |

All diagnostics include the offending key/locale in the message — they appear directly in the build output and IDE error list.

### Why codegen, not hand-maintained constants

**Drift is structurally impossible**: the constant doesn't exist if the JSON key doesn't. Adding a new translation key is a single edit (the JSON file); the TK constant appears at next build, no manual update step.

### Inspecting generated TK

The emitted file is at `Generated/DcsvIo.D2.I18n.SourceGen/DcsvIo.D2.I18n.SourceGen.TKGenerator/TK.g.cs`. This csproj declares `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` so the output lands in the tracked `Generated/` directory — committed for inspection, IDE navigation, and PR diff review; re-emitted on every `dotnet build` from the spec; do not hand-edit. Rider also surfaces it under `Dependencies → Analyzers → DcsvIo.D2.I18n.SourceGen → TKGenerator`.

---

## Dependencies

```xml
<ProjectReference Include="..\abstractions\DcsvIo.D2.I18n.Abstractions.csproj" />
```

Runtime reference to Abstractions (for `TKMessage`). The TK generator is referenced as an Analyzer (`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`), so its dll doesn't propagate to consumers.

---

## Tests

The TK catalog is covered by `public/packages/dotnet/tests/Unit/I18n/` — `TKGeneratedTests` round-trips every emitted constant back to a key in `en-US.json`, and the `SourceGen/` tests exercise the generator's emitter and decomposer pure-logic paths (key decomposition, emitter determinism, all six `D2I18N###` diagnostics).
