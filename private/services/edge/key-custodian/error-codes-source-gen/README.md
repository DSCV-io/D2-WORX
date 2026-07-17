<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Private.Edge.KeyCustodian.ErrorCodes.SourceGen

> Parent: [`private/services/edge/key-custodian/`](../README.md)

**Input contract:** [`contracts/keycustodian-error-codes/`](../../../../../contracts/keycustodian-error-codes/README.md)

For engineers adding or modifying KeyCustodian error codes, or extending the shared source-gen engine. A thin `[Generator]` shell over the shared unified error-codes engine ([`source-gen-shared/error-codes-emit`](../../../../shared/dotnet/source-gen-shared/error-codes-emit/README.md)). It emits the `KeyCustodianErrorCodes` const-string catalog + the `KeyCustodianFailures` semantic-factory class + the typed `KeyCustodianFailures<T>` twin into `DcsvIo.D2.Private.Edge.KeyCustodian.Domain` by reading `contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `DcsvIo.D2.Private.Edge.KeyCustodian.Domain`. The shell owns only the keycustodian catalog's identity (assembly name + the `ErrorCodesGenerator` type FQN, both load-bearing for the on-disk generated path) + its `CatalogConfig`; all generation logic lives in the shared engine.

The spec file is the single source of truth for the platform's keycustodian error taxonomy. Every `d2_error_code` constant surfaced on a `D2Result` failure, every `KeyCustodianFailures<T>.*` factory the domain calls, and the cross-spec merged registry (`ErrorCodeRegistry`) all derive from one JSON file — no hand-written parallel constants, no per-domain drift.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring) and [`source-gen-shared/error-codes-emit`](../../../../shared/dotnet/source-gen-shared/error-codes-emit/README.md) for the shared engine + the add-a-catalog recipe.

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                                    |
| ---------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `D2KEC001` | Error    | Spec file is malformed JSON or violates the schema                                                                                         |
| `D2KEC002` | Error    | Entry's `category` is not one of the closed enum values (`validation_failure`, `not_found`, `conflict`, `internal_error`)                  |
| `D2KEC003` | Error    | Two entries share the same `code`                                                                                                          |
| `D2KEC004` | Error    | Two entries share the same `factoryName`                                                                                                   |
| `D2KEC005` | Error    | Entry's `httpStatus` is not in the supported set (`400`, `404`, `409`, `500`) — expanding the matrix requires updating the codegen mapping |
| `D2ERC001` | Error    | A `code` does not start with the enforced `KEYCUSTODIAN_` domain prefix (catalog-neutral engine diagnostic)                               |
| `D2ERC002` | Error    | A `userMessageKey` does not inverse-resolve to a key in `contracts/messages/en-US.json` (catalog-neutral engine diagnostic)               |
| `D2ERC003` | Error    | A `factoryShape` is not supported by the delegating emitter (`FactoryHost.Domain` supports the universal `standard` shape and `none` only) |

The `D2ERC*` rows are the shared engine's catalog-neutral diagnostics (they fire for any catalog); the `D2KEC*` rows are this catalog's own validation diagnostics. The `D2ERC002` cross-check requires `contracts/messages/en-US.json` to be surfaced via `<AdditionalFiles>` on the consuming `DcsvIo.D2.Private.Edge.KeyCustodian.Domain.csproj` (the generator reduces it to just its key set so a translation-value edit does not re-run codegen).

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "errorCodes": [
    {
      "code": "KEYCUSTODIAN_KID_INVALID",
      "httpStatus": 400,
      "category": "validation_failure",
      "userMessageKey": "TK.Common.Validation.ID_INVALID",
      "factoryName": "KidInvalid",
      "factoryShape": "standard",
      "doc": "The key identifier is null, empty, whitespace, or contains characters outside the JWKS-safe charset [A-Za-z0-9_-]."
    },
    {
      "code": "KEYCUSTODIAN_SOAK_NOT_ELAPSED",
      "httpStatus": 400,
      "category": "validation_failure",
      "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
      "factoryName": "SoakNotElapsed",
      "factoryShape": "standard",
      "doc": "The smoke-soak window has not yet elapsed; the pending key may not be activated."
    }
  ]
}
```

### Field rules

- **`code`** — wire-format `^KEYCUSTODIAN_[A-Z][A-Z0-9_]*$`. Unique. Treated as the spec-anchored constant; the literal IS the wire format. The engine enforces the `KEYCUSTODIAN_` prefix via `D2ERC001`.
- **`httpStatus`** — supported values: `400` (user-facing input-validation rejections), `404` (key-not-found lookups), `409` (illegal lifecycle transition / duplicate pending key), and `500` (programmer/precondition violations + failed smoke tests surfaced as flagged internal-error results instead of thrown exceptions); codegen validates via `D2KEC005` against the supported set. In the unified engine it selects the base `D2Result` delegation factory (`400 → ValidationFailed`, `404 → NotFound`, `409 → Conflict`, `500 → UnhandledException`).
- **`category`** — closed enum for the keycustodian catalog: `validation_failure` (pairs with the `400` codes), `not_found` (pairs with the `404` codes), `conflict` (pairs with the `409` codes), and `internal_error` (pairs with the `500` codes — the "it's our bug" alert class). Semantic/telemetry classification (NOT the factory selector — `httpStatus` selects the factory). Validated via `D2KEC002`.
- **`userMessageKey`** — TK key reference (e.g. `TK.Keycustodian.Validation.SOAK_NOT_ELAPSED`). Emitted as the `messages` argument on the factory. The engine cross-checks existence against `contracts/messages/en-US.json` via `D2ERC002`.
- **`factoryName`** — PascalCase symbol for the emitted factory method. Unique.
- **`factoryShape`** — must be the universal `standard` shape for all keycustodian entries (`FactoryHost.Domain`'s delegating emitter supports `standard` and `none`; any other value fires `D2ERC003` at build time).
- **`doc`** — XML `<summary>` text rendered on the constant + factory.

---

## Emitted output (three `.g.cs` files)

All three files emit into the consuming assembly (`DcsvIo.D2.Private.Edge.KeyCustodian.Domain`) from the same spec, under `Generated/DcsvIo.D2.Private.Edge.KeyCustodian.ErrorCodes.SourceGen/DcsvIo.D2.Private.Edge.KeyCustodian.ErrorCodes.SourceGen.ErrorCodesGenerator/`:

1. **`KeyCustodianErrorCodes.g.cs`** — `DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors.KeyCustodianErrorCodes` static class with one `public const string` per spec entry, `IReadOnlyList<string> AllCodes`, and `int GetHttpStatus(string)`.
2. **`KeyCustodianFailures.g.cs`** — `DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors.KeyCustodianFailures` static class with one `public static D2Result FactoryName(IReadOnlyList<TKMessage>? messages = null)` per spec entry (delegating to the `httpStatus`-selected base factory — `ValidationFailed` for the `400` codes, `NotFound` for the `404` codes, `Conflict` for the `409` codes, `UnhandledException` for the `500` codes — stamped with the matching `KeyCustodianErrorCodes` constant + the entry's `ErrorCategory`). The optional `messages` override defaults to the entry's `userMessageKey` when omitted and replaces it when supplied, so a lifecycle guard can name the offending argument via `TK.Keycustodian.Internal.PRECONDITION_VIOLATED.With("arg", "<name>")`.
3. **`KeyCustodianFailures.Generic.g.cs`** — `DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors.KeyCustodianFailures<T>`, the typed twin: identical method names, each delegating to the typed `D2Result<T>` base factory so callers can produce a typed domain failure (e.g. `KeyCustodianFailures<Kid>.KidInvalid()`). A distinct sibling file (distinct `AddSource` hint name) so `KeyCustodianFailures.g.cs` stays byte-identical. `KeyCustodianFailures` (non-generic) and `KeyCustodianFailures<T>` (generic) are distinct types — arity differs — exactly as `D2Result` / `D2Result<T>` coexist.

The multi-emitter split lives in one sourcegen because all outputs derive from the same spec rows — keeping them co-located ensures any spec edit re-emits every file together and prevents the constants catalog from drifting from the factory surface.

---

## Telemetry

N/A — compile-time-only Roslyn generator; emits no OTel instruments and runs no runtime code.

## Configuration

N/A — all inputs are spec-file entries and `<AdditionalFiles>` wiring in the consuming `.csproj`; there is no runtime configuration.

## Operations

N/A — the generator produces output at build time; there is no standalone service to run or health-check.

---

## Dependencies

Package references (all `PrivateAssets="all"` — analyzer-only, not propagated to consumers):

- `Microsoft.CodeAnalysis.CSharp` (5.0.0) — Roslyn compilation model (`IIncrementalGenerator`, `SyntaxNode`, `ISymbol`) used by both the shared engine and this shell.
- `Microsoft.CodeAnalysis.Analyzers` (5.3.0) — analyzer-correctness rules (`EnforceExtendedAnalyzerRules`); catches generator API misuse at build time.
- `System.Text.Json` (10.0.7) — spec-file JSON parsing; bundled into the analyzer output via `GeneratePathProperty` so Roslyn's host can load it (`netstandard2.0` does not ship S.T.Json in-box).

Shared source (via `<Compile Include>` — compiled into this shell, not referenced as a project):

- `source-gen-shared/core/**` (`$(D2SourceGenSharedRoot)`) — polyfills + `EmitDiagnostic` record + `LoadResult<TSpec>` + `SpecFile`; the common scaffolding shared across all D² Roslyn generators.
- `source-gen-shared/error-codes-emit/**` (`$(D2ErrorCodesEmitRoot)`) — the unified error-codes engine: entry model, spec loader, constants/failures/generic-twin emitters, `CatalogConfig`, and the `D2ERC*` engine diagnostics. One engine compiled into every error-codes generator shell; this shell supplies only its `CatalogConfig` + analyzer identity.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`source-gen-shared/error-codes-emit`](../../../../shared/dotnet/source-gen-shared/error-codes-emit/README.md) — the shared unified engine this shell drives
- [`contracts/keycustodian-error-codes/schema.json`](../../../../../contracts/keycustodian-error-codes/schema.json) — JSON Schema for the spec (a domain-specialized copy of the canonical schema)
- [`contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json`](../../../../../contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json) — the source-of-truth catalog
- [`DcsvIo.D2.Private.Edge.KeyCustodian.Domain`](../domain/README.md) — the consuming assembly where generated output is emitted
