<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.I18n.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)

Domain-safe slice of the i18n stack: the `TKMessage` primitive and the `ITranslator` interface. **Zero external deps** (no NuGet packages, no other shared-lib references — only what the .NET runtime ships) so domain layers can reference this without dragging in DI containers, configuration loading, or file IO.

The `TK` constants (one `TKMessage` instance per translation key, Source-Generated from `contracts/messages/en-US.json`) live in the sibling [`DcsvIo.D2.I18n.Keys`](../keys/README.md) project, which references this one for the `TKMessage` type. The runtime piece (`Translator`, `SupportedLocales`, `AddD2I18n` DI extension) lives in the sibling [`DcsvIo.D2.I18n`](../core/README.md) project. Domain code never references that one.

---

## Why split

`D2Result.Messages`, `InputError.Errors`, and any domain factory message slot use `TKMessage`. Those types must be reachable from domain code, which by convention takes only zero-dep value-typed dependencies. Lumping the runtime translator (which needs `IConfiguration`, `IServiceCollection`, file IO) into the same project as `TKMessage` would force every domain project to transitively pick up DI + Configuration just to spell out an error key. The Abstractions split keeps that strict.

The pattern matches `Microsoft.Extensions.Logging.Abstractions` vs `Microsoft.Extensions.Logging` exactly.

---

## File layout

| Path                                  | Contents                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TKMessage.cs`                        | `TKMessage` sealed record — translation key + optional parameter bindings. Internal ctor; can only be constructed via the SrcGen-emitted `TK.*` constants in the sibling [`DcsvIo.D2.I18n.Keys`](../keys/README.md) (granted access via `[InternalsVisibleTo]`).                                                                                                                                                                                                                                                                            |
| `TKMessageJsonConverter.cs`           | `JsonConverter<TKMessage>` — wire format `{ "key": "..." }` or `{ "key": "...", "params": { ... } }`. Applied to `TKMessage` via `[JsonConverter]`. JSON property names come from the spec-derived `TkMessageWireShape.KEY` / `.PARAMS` constants — single source of truth shared with the TS-side parser via `contracts/tk-message/tk-message.spec.json`.                                                                                                                                                                                |
| `ITranslator.cs`                      | The translation interface. `string T(string locale, TKMessage message)` and `bool HasKey(string key)`. Implementation lives in the runtime lib.                                                                                                                                                                                                                                                                                                                                                                                           |
| `(generated) TkMessageWireShape.g.cs` | Emitted by the **`DcsvIo.D2.WireShapes.SourceGen`** project at [`../../source-gen-shared/wire-shapes-source-gen/`](../../source-gen-shared/wire-shapes-source-gen/README.md) — a Roslyn `IIncrementalGenerator` with multi-target dispatch. Output lands at `Generated/DcsvIo.D2.WireShapes.SourceGen/DcsvIo.D2.WireShapes.SourceGen.WireShapesGenerator/TkMessageWireShape.g.cs` (tracked in git) at every build. Carries the `KEY` and `PARAMS` JSON property-name constants. Cross-language parity-tested against the TS-side `@dcsv-io/d2-result` `TkMessageWireShape` catalog. |

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

- **Internal constructor.** Producers can ONLY construct a `TKMessage` via the SrcGen-emitted `TK.*` constants in the sibling [`DcsvIo.D2.I18n.Keys`](../keys/README.md). There is no public ctor and no escape hatch — "untranslated literal in `D2Result.Messages`" is structurally unrepresentable.
- **Immutable.** `With(name, value)` and `With(IReadOnlyDictionary<string, string>)` return _new_ instances; the original is never mutated. The static-readonly TK constants stay pinned.
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
  "messages": [{ "key": "common_errors_VALIDATION_FAILED" }],
  "inputErrors": [
    {
      "field": "email",
      "errors": [{ "key": "common_validation_EMAIL_INVALID" }]
    }
  ]
}
```

**Translation happens client-side.** SvelteKit / Paraglide consumes the wire-format `TKMessage` objects and renders them in the active locale. The server is locale-unaware on the HTTP response path. CDN caching benefits, no `Vary: Accept-Language` fragmentation.

The server-side `Translator` (in the runtime lib) is used only for **outbound notifications** (Courier emails / SMS / push), where the recipient's preferred locale comes from their user profile and the rendered text must be inlined into the notification payload before delivery.

---

## TK constants

The `TK.*` constants every producer references (e.g. `TK.Common.Errors.NOT_FOUND`) are Source-Generated `TKMessage` instances. They live in the sibling [`DcsvIo.D2.I18n.Keys`](../keys/README.md) project, which hosts the `DcsvIo.D2.I18n.SourceGen.TKGenerator` and references this project for the `TKMessage` type. The decomposition rules, build-time diagnostics, and codegen rationale are documented there.

---

## ITranslator — the runtime contract (implemented in `DcsvIo.D2.I18n`)

```csharp
public interface ITranslator
{
    string T(string locale, TKMessage message);
    bool HasKey(string key);
}
```

`T()` resolves the locale via `SupportedLocales.Resolve` (canonical match → language fallback → base locale), looks up the key, falls back to base-locale translation, then falls back to the raw key string itself. **Never throws on missing keys** — the raw key is dev-readable and useful as a debugging signal.

`HasKey` is O(1) via a pre-computed `HashSet<string>` populated at catalog-load time.

Implementation in `DcsvIo.D2.I18n.Translator`. Domain code references this interface only when actively rendering for outbound notifications; most domain code just embeds `TKMessage` instances into `D2Result` and lets the boundary translate.

---

## Dependencies

```xml
<!-- Zero external deps. -->
```

The csproj has no `<PackageReference>`s and no runtime `<ProjectReference>`s. The `DcsvIo.D2.WireShapes.SourceGen` generator (which emits `TkMessageWireShape.g.cs`) is referenced as an Analyzer, so its dll doesn't propagate to consumers.

---

## Tests

`public/packages/dotnet/tests/Unit/I18n/` — comprehensive coverage across abstractions surface (every `TKMessage` operation + immutability + JSON roundtrip + adversarial inputs) plus broad coverage of the SrcGen emitter and decomposer pure-logic paths. Categories:

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
