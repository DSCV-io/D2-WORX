<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.I18n.Abstractions

> Parent: [`server/shared/dotnet/`](../README.md)

Domain-safe slice of the i18n stack: the `TKMessage` primitive, the `TK` constants (Source-Generated from `contracts/messages/en-US.json`), and the `ITranslator` interface. **Zero external deps** (no NuGet packages, no other shared-lib references — only what the .NET runtime ships) so domain layers can reference this without dragging in DI containers, configuration loading, or file IO.

The runtime piece (`Translator`, `SupportedLocales`, `AddD2I18n` DI extension) lives in the sibling [`D2.Shared.I18n`](../i18n/README.md) project. Domain code never references that one.

---

## Why split

`D2Result.Messages`, `InputError.Errors`, and any domain factory message slot use `TKMessage`. Those types must be reachable from domain code, which by convention takes only zero-dep value-typed dependencies. Lumping the runtime translator (which needs `IConfiguration`, `IServiceCollection`, file IO) into the same project as `TKMessage` would force every domain project to transitively pick up DI + Configuration just to spell out an error key. The Abstractions split keeps that strict.

The pattern matches `Microsoft.Extensions.Logging.Abstractions` vs `Microsoft.Extensions.Logging` exactly.

---

## File layout

| Path | Contents |
|---|---|
| `TKMessage.cs` | `TKMessage` sealed record — translation key + optional parameter bindings. Internal ctor; can only be constructed via the SrcGen-emitted `TK.*` constants. |
| `TKMessageJsonConverter.cs` | `JsonConverter<TKMessage>` — wire format `{ "key": "..." }` or `{ "key": "...", "params": { ... } }`. Applied to `TKMessage` via `[JsonConverter]`. JSON property names come from the spec-derived `TkMessageWireShape.KEY` / `.PARAMS` constants — single source of truth shared with the TS-side parser via `contracts/tk-message/tk-message.spec.json`. |
| `ITranslator.cs` | The translation interface. `string T(string locale, TKMessage message)` and `bool HasKey(string key)`. Implementation lives in the runtime lib. |
| `(generated) TK.g.cs` | Emitted by the sibling **`D2.Shared.I18n.SourceGen`** project at [`../i18n-source-gen/`](../i18n-source-gen/README.md) — a Roslyn `IIncrementalGenerator` (netstandard2.0; referenced as Analyzer, not a runtime dll). Output lands at `Generated/D2.Shared.I18n.SourceGen/D2.Shared.I18n.SourceGen.TKGenerator/TK.g.cs` (tracked in git) at every build. Contains nested `static partial class` chains (`TK.Common.Errors.NOT_FOUND` etc.), one `TKMessage` constant per JSON key. |
| `(generated) TkMessageWireShape.g.cs` | Emitted by the sibling **`D2.Shared.WireShapes.SourceGen`** project at [`../wire-shapes-source-gen/`](../wire-shapes-source-gen/README.md) — a Roslyn `IIncrementalGenerator` with multi-target dispatch. Output lands at `Generated/D2.Shared.WireShapes.SourceGen/D2.Shared.WireShapes.SourceGen.WireShapesGenerator/TkMessageWireShape.g.cs` (tracked in git) at every build. Carries the `KEY` and `PARAMS` JSON property-name constants. Cross-language parity-tested against the TS-side `@d2/result` `TkMessageWireShape` catalog. |

---

## TKMessage — the structural primitive

Every translatable string in the codebase is a `TKMessage`:

```csharp
// Common case — no params:
D2Result<T>.ValidationFailed(messages: [TK.Common.Errors.NOT_FOUND]);

// Parameterized — bind via With():
D2Result<T>.ValidationFailed(
    messages: [TK.Auth.Errors.PASSWORD_WEAK.With("minLength", "12")]);

// Per-field input errors — same primitive:
D2Result<T>.ValidationFailed(
    inputErrors: [new InputError("email", [TK.Common.Validation.EMAIL_INVALID])]);
```

Key facts:

- **Internal constructor.** Producers can ONLY construct a `TKMessage` via the SrcGen-emitted `TK.*` constants. There is no public ctor and no escape hatch — "untranslated literal in `D2Result.Messages`" is structurally unrepresentable.
- **Immutable.** `With(name, value)` and `With(IReadOnlyDictionary<string, string>)` return *new* instances; the original is never mutated. The static-readonly TK constants stay pinned.
- **Record equality with order-independent params.** Two `TKMessage` instances with the same key and same param bindings (regardless of the order `With()` was called in) compare equal.
- **Wire format = code shape.** Same JSON shape in code and on the wire — no separate "in-memory" vs "wire" representation.

---

## Wire format

`TKMessageJsonConverter` controls the wire shape, which is also the in-code shape:

```json
// No params:
{ "key": "common_errors_NOT_FOUND" }

// With params:
{ "key": "auth_errors_PASSWORD_WEAK", "params": { "minLength": "12" } }
```

Used inside `D2Result.Messages` array:
```json
{
  "success": false,
  "statusCode": 422,
  "messages": [
    { "key": "common_errors_VALIDATION_FAILED" }
  ],
  "inputErrors": [
    { "field": "email", "errors": [{ "key": "common_validation_EMAIL_INVALID" }] }
  ]
}
```

**Translation happens client-side.** SvelteKit / Paraglide consumes the wire-format `TKMessage` objects and renders them in the active locale. The server is locale-unaware on the HTTP response path. CDN caching benefits, no `Vary: Accept-Language` fragmentation.

The server-side `Translator` (in the runtime lib) is used only for **outbound notifications** (Courier emails / SMS / push), where the recipient's preferred locale comes from their user profile and the rendered text must be inlined into the notification payload before delivery.

---

## TK Source Generator

`D2.Shared.I18n.SourceGen.TKGenerator` is a Roslyn `[Generator]` that:

1. Reads `contracts/messages/*.json` via `<AdditionalFiles>` declared in this csproj.
2. Treats `en-US.json` as the source of truth.
3. Decomposes each key (`{domain}_{category}_{IDENTIFIER}`) into a TK path (`TK.Domain.Category.IDENTIFIER`).
4. Emits a `TK.g.cs` file containing nested `static partial class` chains with one `static readonly TKMessage` per key.
5. Cross-checks every other locale against en-US to surface translation gaps at build time.

### Decomposition rules

JSON keys follow `{domain}_{category}_{IDENTIFIER}` where:

- Segment 0 → top-level nested class (PascalCase: `common` → `Common`)
- Segment 1 → second-level nested class (PascalCase: `errors` → `Errors`)
- Segments 2..N joined by `_` and uppercased → constant name
- Field value = original JSON key string

| JSON key | Generated path | Field value |
|---|---|---|
| `common_errors_NOT_FOUND` | `TK.Common.Errors.NOT_FOUND` | `"common_errors_NOT_FOUND"` |
| `geo_validation_ip_required` | `TK.Geo.Validation.IP_REQUIRED` | `"geo_validation_ip_required"` |
| `auth_email_signup_subject` | `TK.Auth.Email.SIGNUP_SUBJECT` | `"auth_email_signup_subject"` |
| `geo_validation_address_line1_required` | `TK.Geo.Validation.ADDRESS_LINE1_REQUIRED` | `"geo_validation_address_line1_required"` |

### Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2I18N001` | Warning | A JSON key cannot be decomposed (fewer than 3 segments, invalid C# identifier, etc.). The offending key is skipped. |
| `D2I18N002` | Warning | A key in en-US is missing from another locale catalog. The key is still emitted in TK. |
| `D2I18N003` | Error | Two distinct JSON keys decompose to the same TK path. Build-failing. |
| `D2I18N004` | Warning | A key exists in a non-en-US locale but has no matching entry in en-US. NOT included in TK. |
| `D2I18N005` | Error | The generator can't find `en-US.json` among AdditionalFiles. TK class is empty. |
| `D2I18N006` | Error | A JSON catalog file is malformed (parse failure). The offending file is skipped. |

All diagnostics include the offending key/locale in the message — they appear directly in the build output and IDE error list.

### Why codegen, not hand-maintained constants

**Drift is structurally impossible**: the constant doesn't exist if the JSON key doesn't. Adding a new translation key is a single edit (the JSON file); the TK constant appears at next build, no manual update step.

The SrcGen also surfaces:
- Per-locale coverage gaps (D2I18N002)
- Orphan keys in non-en-US locales (D2I18N004)
- Decomposition rule violations (D2I18N001) — caught at build time rather than as runtime mysteries

### Inspecting generated TK

The emitted file is at `Generated/D2.Shared.I18n.SourceGen/D2.Shared.I18n.SourceGen.TKGenerator/TK.g.cs`. The consuming csproj declares `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` so the output lands in the tracked `Generated/` directory — committed for inspection, IDE navigation, and PR diff review; re-emitted on every `dotnet build` from the spec; do not hand-edit. Rider also surfaces it under `Dependencies → Analyzers → D2.Shared.I18n.SourceGen → TKGenerator`.

---

## ITranslator — the runtime contract (implemented in `D2.Shared.I18n`)

```csharp
public interface ITranslator
{
    string T(string locale, TKMessage message);
    bool HasKey(string key);
}
```

`T()` resolves the locale via `SupportedLocales.Resolve` (canonical match → language fallback → base locale), looks up the key, falls back to base-locale translation, then falls back to the raw key string itself. **Never throws on missing keys** — the raw key is dev-readable and useful as a debugging signal.

`HasKey` is O(1) via a pre-computed `HashSet<string>` populated at catalog-load time.

Implementation in `D2.Shared.I18n.Translator`. Domain code references this interface only when actively rendering for outbound notifications; most domain code just embeds `TKMessage` instances into `D2Result` and lets the boundary translate.

---

## Dependencies

```xml
<!-- Zero external deps. -->
```

The csproj has no `<PackageReference>`s and no `<ProjectReference>`s. The Source Generator is referenced as an Analyzer (its dll doesn't propagate to consumers).

---

## Tests

`server/shared/dotnet/tests/Unit/I18n/` — comprehensive coverage across abstractions surface (every `TKMessage` operation + immutability + JSON roundtrip + adversarial inputs) plus broad coverage of the SrcGen emitter and decomposer pure-logic paths. Categories:

- `TKMessageTests` — equality (incl. order-independent params), JSON roundtrip, malformed JSON handling, immutability of `With()`, integration as part of `D2Result.Messages` arrays.
- `SourceGen/KeyDecomposerTests` — happy path + every invalid edge (leading/trailing/consecutive underscores, identifier-starts-with-digit, unicode rejection, reserved word collision), plus property test against every real key in `en-US.json`.
- `SourceGen/TKEmitterTests` — single-key snapshot, multi-domain emission, alphabetical determinism (cache stability), all six diagnostics (D2I18N001-006), 200-key smoke test.
- `SourceGen/DiagnosticIdsTests` — pins every D2I18N### identifier to its stable value (rename = breaking change).

The Roslyn-host integration (`TKGenerator.Initialize`) is exercised end-to-end via `TKGeneratedTests` — every constant that ships in TK must round-trip back to a key in `en-US.json`.

---

## Out of scope

- **Compile-time call-site safety** — a `T(TKKey)` overload where TKKey is a phantom type that proves the key exists. The current `T(TKMessage)` API is already constructor-locked; the additional safety gap is too small to justify the extra type-system complexity.
- **Generator-emitted XML doc comments on TK constants** — embedding the en-US text into the generated `<summary>` would surface the message in IntelliSense, but would require parsing translation values for XML-doc-safe characters; the build-time complexity isn't justified.
- **Per-message context metadata** — e.g., severity, audience tags. Would require extending `TKMessage` and the wire format; no driving use case justifies the wire-format change.
