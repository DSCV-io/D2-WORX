<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.ErrorCodes.SourceGen

> Parent: [`public/packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/auth-error-codes/`](../../../../../contracts/auth-error-codes/README.md)

A thin `[Generator]` shell over the shared unified error-codes engine ([`source-gen-shared/error-codes-emit`](../../source-gen-shared/error-codes-emit/README.md)). It emits the `AuthErrorCodes` const-string catalog + the `AuthFailures` semantic-factory class + the typed `AuthFailures<T>` twin into `D2.Shared.Auth` by reading `contracts/auth-error-codes/auth-error-codes.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.Auth`. The shell owns only the auth catalog's identity (assembly name + the `ErrorCodesGenerator` type FQN, both load-bearing for the on-disk generated path) + its `CatalogConfig`; all generation logic lives in the shared engine.

The spec file is the single source of truth for the platform's auth-error taxonomy. Every `d2_error_code` constant a transport binding surfaces, every `D2Result` factory the validator picks, and the cross-spec telemetry tag-value enumeration on `d2.auth.problem.emitted` (resolved by `D2.Shared.Telemetry.Tags.SourceGen` via `valuesFromSpec`) all derive from one JSON file — no hand-written parallel constants, no per-feature drift.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring) and [`source-gen-shared/error-codes-emit`](../../source-gen-shared/error-codes-emit/README.md) for the shared engine + the add-a-catalog recipe.

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                       |
| ---------- | -------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `D2AEC001` | Error    | Spec file is malformed JSON or violates the schema                                                                            |
| `D2AEC002` | Error    | Entry's `category` is not one of `validation_failure` / `infrastructure_unavailable` / `policy_denied`                        |
| `D2AEC003` | Error    | Two entries share the same `code`                                                                                             |
| `D2AEC004` | Error    | Two entries share the same `factoryName`                                                                                      |
| `D2AEC005` | Error    | Entry's `httpStatus` is not in the supported set (`401` / `503`) — expanding the matrix requires updating the codegen mapping |
| `D2ERC001` | Error    | A `code` does not start with the enforced `AUTH_` domain prefix (catalog-neutral engine diagnostic)                          |
| `D2ERC002` | Error    | A `userMessageKey` does not inverse-resolve to a key in `contracts/messages/en-US.json` (catalog-neutral engine diagnostic)  |

The `D2ERC*` rows are the shared engine's catalog-neutral diagnostics (they fire for any catalog); the `D2AEC*` rows are this catalog's own validation diagnostics. The `D2ERC002` cross-check requires `contracts/messages/en-US.json` to be surfaced via `<AdditionalFiles>` on the consuming `D2.Shared.Auth.csproj` (the generator reduces it to just its key set so a translation-value edit does not re-run codegen).

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "errorCodes": [
    {
      "code": "AUTH_BEARER_MISSING",
      "httpStatus": 401,
      "category": "validation_failure",
      "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
      "factoryName": "BearerMissing",
      "factoryShape": "standard",
      "doc": "The Authorization header was missing on a protected endpoint."
    },
    {
      "code": "AUTH_JWKS_UNAVAILABLE",
      "httpStatus": 503,
      "category": "infrastructure_unavailable",
      "userMessageKey": "TK.Auth.Errors.TEMPORARILY_UNAVAILABLE",
      "factoryName": "JwksUnavailable",
      "factoryShape": "standard",
      "doc": "JWKS upstream is unavailable; no cached snapshot to fall back on."
    }
  ]
}
```

### Field rules

- **`code`** — wire-format `^AUTH_[A-Z][A-Z0-9_]*$`. Unique. Treated as the spec-anchored constant; the literal IS the wire format. The engine enforces the `AUTH_` prefix via `D2ERC001`.
- **`httpStatus`** — supported values today: `401` / `503`. Codegen validates via `D2AEC005` against the supported set. In the unified engine it selects the base `D2Result` delegation factory (`401 → Unauthorized`; `503 → ServiceUnavailable`) and gates the typed `<T>` overload (emitted for `503`).
- **`category`** — closed enum `validation_failure` / `infrastructure_unavailable` / `policy_denied`. Semantic/telemetry classification (NOT the factory selector — `httpStatus` selects the factory). Validated via `D2AEC002`.
- **`userMessageKey`** — TK key reference (e.g. `TK.Auth.Errors.UNAUTHORIZED`). Emitted as the `messages` argument on the factory. The engine cross-checks existence against `contracts/messages/en-US.json` via `D2ERC002`.
- **`factoryName`** — PascalCase symbol for the emitted factory method. Unique.
- **`factoryShape`** — closed enum (`standard` / `none`) driving the generated factory's signature variant. Every auth entry is the universal `standard` shape — both `401 → Unauthorized` and `503 → ServiceUnavailable` delegate through it; the typed `<T>` overload on the `503` factories is an `httpStatus`-driven emitter rule, not a `factoryShape` value.
- **`doc`** — XML `<summary>` text rendered on the constant + factory.

---

## Emitted output (three `.g.cs` files)

All three files emit into the consuming assembly (`D2.Shared.Auth`) from the same spec:

1. **`AuthErrorCodes.g.cs`** — `D2.Shared.Auth.Errors.AuthErrorCodes` static class with one `public const string` per spec entry, `IReadOnlyList<string> AllCodes`, `int GetHttpStatus(string)`, and `string KebabCase(string)` for the ProblemDetails `type` URI helper.
2. **`AuthFailures.g.cs`** — `D2.Shared.Auth.Errors.AuthFailures` static class with one `public static D2Result FactoryName()` per spec entry (delegating to the `httpStatus`-selected base factory — `401 → Unauthorized`, `503 → ServiceUnavailable`), plus a typed `D2Result<T> FactoryName<T>()` overload for every `503` entry.
3. **`AuthFailures.Generic.g.cs`** — `D2.Shared.Auth.Errors.AuthFailures<T>`, the typed twin: identical method names, each delegating to the typed `D2Result<T>` base factory so callers can produce a typed domain failure (e.g. `AuthFailures<Session>.BearerMissing()`). A distinct sibling file (distinct `AddSource` hint name) so `AuthFailures.g.cs` stays byte-identical. `AuthFailures` (non-generic) and `AuthFailures<T>` (generic) are distinct types — arity differs — exactly as `D2Result` / `D2Result<T>` coexist.

The multi-emitter split lives in one sourcegen because all outputs derive from the same spec rows — keeping them co-located ensures any spec edit re-emits every file together and prevents the constants catalog from drifting from the factory surface.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`source-gen-shared/error-codes-emit`](../../source-gen-shared/error-codes-emit/README.md) — the shared unified engine this shell drives
- [`contracts/auth-error-codes/schema.json`](../../../../../contracts/auth-error-codes/schema.json) — JSON Schema for the spec (a domain-specialized copy of the canonical schema)
- [`contracts/auth-error-codes/auth-error-codes.spec.json`](../../../../../contracts/auth-error-codes/auth-error-codes.spec.json) — the source-of-truth catalog
- [`D2.Shared.Auth.Scopes.SourceGen`](../scopes-source-gen/README.md) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [`D2.Shared.Telemetry.Tags.SourceGen`](../../telemetry/tags-source-gen/README.md) — sibling SrcGen consumes this spec via `valuesFromSpec=auth-error-codes` to drive the `d2.auth.problem.emitted` tag-value enumeration
