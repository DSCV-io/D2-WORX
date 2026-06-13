<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Result.ErrorCodes.SourceGen

> Parent: [`server/shared/dotnet/`](../../README.md)

A thin `[Generator]` shell over the shared unified error-codes engine ([`error-codes-emit`](../error-codes-emit/README.md)). It emits the `ErrorCodes` const-string catalog AND the constructing semantic failure factories + per-code booleans into `D2.Shared.Result` by reading `contracts/error-codes/error-codes.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.Result`. The shell owns only the generic catalog's identity (assembly name + the `ErrorCodesGenerator` type FQN) + its `CatalogConfig`; all generation logic lives in the shared engine.

The spec file is the single source of truth for the platform's generic error-code taxonomy. Every `d2_error_code` constant a `D2Result` failure carries, every constructing semantic failure factory on `D2Result` / `D2Result<TData>` (e.g. `NotFound`, `ValidationFailed`), and every per-code boolean discriminator (e.g. `IsNotFound`, `IsConflict`) is generated from this spec. Same spec drives the TS-side `@d2/result` `ErrorCodes` catalog + factories via `tools/ts-codegen/src/error-codes-emit.ts` — cross-language wire-format drift is structurally impossible.

The generic catalog owns the reserved unprefixed namespace (`NOT_FOUND`, `CONFLICT`, …) and runs in the engine's `FactoryHost.Base` mode — the factories ARE the base, so they CONSTRUCT a `D2Result` directly and land as members ONTO the `D2Result` / `D2Result<TData>` partial classes (not a separate `<Domain>Failures` class). Per-domain catalogs (e.g. the auth `AUTH_*` taxonomy at `contracts/auth-error-codes/`, driven by `D2.Shared.Auth.ErrorCodes.SourceGen`) run in `FactoryHost.Domain` mode — their factories DELEGATE to the `httpStatus`-selected base factory and live in a separate `<Domain>Failures` (+ `<Domain>Failures<T>`) class. The SAME engine drives both via per-catalog config.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring) and [`error-codes-emit`](../error-codes-emit/README.md) for the shared engine + the add-a-catalog recipe.

---

## Build-time diagnostics

| ID        | Severity | Trigger                                                                                                                                                                                                       |
| --------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2EC001` | Error    | Spec file is malformed JSON or violates the schema                                                                                                                                                            |
| `D2EC002` | Error    | Two entries share the same `code`                                                                                                                                                                             |
| `D2EC003` | Error    | Entry's `httpStatus` is not in the supported set (`200` / `206` / `207` / `400` / `401` / `403` / `404` / `409` / `413` / `429` / `500` / `503`) — expanding the matrix requires updating the codegen mapping |
| `D2EC004` | Error    | Entry's `code` is empty or does not match `^[A-Z][A-Z0-9_]*$`                                                                                                                                                 |
| `D2EC005` | Error    | Entry's `doc` summary text is missing or whitespace-only                                                                                                                                                      |

The shared engine's `D2ERC001` (domain-prefix) does NOT apply — the generic catalog owns the reserved unprefixed namespace (no domain prefix to enforce). `D2ERC002` (TK-existence) DOES apply now that the catalog is factory-bearing — every `userMessageKey` is inverse-transformed (e.g. `TK.Common.Errors.NOT_FOUND` → `common_errors_NOT_FOUND`) and cross-checked against `contracts/messages/en-US.json` (surfaced via `<AdditionalFiles>`). `D2ERC003` (unsupported delegating `factoryShape`) never fires here — `FactoryHost.Base` implements the universal `standard` shape (and `none`).

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "errorCodes": [
    {
      "code": "NOT_FOUND",
      "httpStatus": 404,
      "category": "not_found",
      "userMessageKey": "TK.Common.Errors.NOT_FOUND",
      "factoryName": "NotFound",
      "factoryShape": "standard",
      "doc": "Indicates that the requested resource was not found."
    },
    {
      "code": "SERVICE_UNAVAILABLE",
      "httpStatus": 503,
      "category": "infrastructure_unavailable",
      "userMessageKey": "TK.Common.Errors.SERVICE_UNAVAILABLE",
      "factoryName": "ServiceUnavailable",
      "factoryShape": "standard",
      "doc": "Indicates that the service is currently unavailable."
    }
  ]
}
```

### Field rules

- **`code`** — wire-format `^[A-Z][A-Z0-9_]*$`. Unique. Treated as the spec-anchored constant; the literal IS the wire format.
- **`httpStatus`** — supported values today: the 12 entries in the supported set (see `D2EC003`). The JSON-Schema `enum` mirrors the codegen matrix; expanding both is a coordinated edit.
- **`category`** — closed semantic/telemetry classification (9 values incl. `partial_success` for the 206/207 codes). A wire field for generic class-based consumer handling.
- **`userMessageKey`** — TK symbol-path reference (e.g. `TK.Common.Errors.NOT_FOUND`) used as the factory's default message. Cross-checked against `en-US.json` by `D2ERC002`. The `code` and `userMessageKey` constant may legitimately differ (e.g. `UNHANDLED_EXCEPTION` → `TK.Common.Errors.UNKNOWN`; `RATE_LIMITED` → `TK.Common.Errors.TOO_MANY_REQUESTS`).
- **`factoryName`** — PascalCase factory method name (e.g. `NotFound`, `TooManyRequests`). May differ from `code`.
- **`factoryShape`** — `standard` (the one universal error-factory shape: `messages?, inputErrors?, errorCode?, category?, traceId?` — all optional) or `none` (constant + boolean only, no factory). Drives the factory signature.
- **`doc`** — XML `<summary>` text rendered on the emitted constant + factory + JSDoc on the TS-side emitted constant.

---

## Emitted output

Four `.g.cs` files emitted into the consuming assembly (`D2.Shared.Result`):

- **`ErrorCodes.g.cs`** — `D2.Shared.Result.ErrorCodes` static class with one `public const string` per spec entry, `IReadOnlyList<string> AllCodes`, and `int GetHttpStatus(string)`.
- **`D2Result.Factories.g.cs`** — the constructing non-generic semantic failure factories on `partial class D2Result` (one per `factoryShape != none` entry).
- **`D2Result.Generic.Factories.g.cs`** — the `<TData>` typed twins on `partial class D2Result<TData>` (carry `new` + `default` data).
- **`D2Result.Booleans.g.cs`** — the per-error-code boolean discriminators on `partial class D2Result` (one per ErrorCode-keyed code; serialization codes key none).

---

## Reference

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`error-codes-emit`](../error-codes-emit/README.md) — the shared unified engine this shell drives
- [`contracts/error-codes/schema.json`](../../../../../contracts/error-codes/schema.json) — JSON Schema for the spec
- [`contracts/error-codes/error-codes.spec.json`](../../../../../contracts/error-codes/error-codes.spec.json) — the source-of-truth catalog
- [`D2.Shared.Auth.ErrorCodes.SourceGen`](../../auth/error-codes-source-gen/README.md) — auth-domain SrcGen for the auth-specific `AUTH_*` taxonomy
- [`tools/ts-codegen/src/error-codes-emit.ts`](../../../../../tools/ts-codegen/src/error-codes-emit.ts) — TS-side emitter consuming the same spec
- [`docs/PARITY.md`](../../../../../docs/PARITY.md) — cross-language parity catalog (lists this spec)
