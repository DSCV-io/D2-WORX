<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.ErrorCodes.SourceGen

> Parent: [`server/shared/dotnet/`](../../README.md)

Roslyn incremental source generator that emits the `AuthErrorCodes` const-string catalog + the `AuthFailures` semantic-factory class into `D2.Shared.Auth` by reading `contracts/auth-error-codes/auth-error-codes.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.Auth`.

The spec file is the single source of truth for the platform's auth-error taxonomy. Every `d2_error_code` constant a transport binding surfaces, every `D2Result` factory the validator picks, and the cross-spec telemetry tag-value enumeration on `d2.auth.problem.emitted` (resolved by `D2.Shared.Telemetry.Tags.SourceGen` via `valuesFromSpec`) all derive from one JSON file — no hand-written parallel constants, no per-feature drift.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                       |
| ---------- | -------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `D2AEC001` | Error    | Spec file is malformed JSON or violates the schema                                                                            |
| `D2AEC002` | Error    | Entry's `category` is not one of `validation_failure` / `infrastructure_unavailable` / `policy_denied`                        |
| `D2AEC003` | Error    | Two entries share the same `code`                                                                                             |
| `D2AEC004` | Error    | Two entries share the same `factoryName`                                                                                      |
| `D2AEC005` | Error    | Entry's `httpStatus` is not in the supported set (`401` / `503`) — expanding the matrix requires updating the codegen mapping |

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
      "doc": "The Authorization header was missing on a protected endpoint."
    },
    {
      "code": "AUTH_JWKS_UNAVAILABLE",
      "httpStatus": 503,
      "category": "infrastructure_unavailable",
      "userMessageKey": "TK.Auth.Errors.TEMPORARILY_UNAVAILABLE",
      "factoryName": "JwksUnavailable",
      "doc": "JWKS upstream is unavailable; no cached snapshot to fall back on."
    }
  ]
}
```

### Field rules

- **`code`** — wire-format `^AUTH_[A-Z][A-Z0-9_]*$`. Unique. Treated as the spec-anchored constant; the literal IS the wire format.
- **`httpStatus`** — supported values today: `401` / `503`. Codegen validates via `D2AEC005` against the supported set.
- **`category`** — closed enum `validation_failure` / `infrastructure_unavailable` / `policy_denied`. Drives factory selection: `validation_failure` / `policy_denied` → `D2Result.Unauthorized`; `infrastructure_unavailable` → `D2Result.ServiceUnavailable` (also gets a typed `<T>` overload).
- **`userMessageKey`** — TK key reference (e.g. `TK.Auth.Errors.UNAUTHORIZED`). Emitted as the `messages` argument on the factory.
- **`factoryName`** — PascalCase symbol for the emitted factory method. Unique.
- **`doc`** — XML `<summary>` text rendered on the constant + factory.

---

## Emitted output (two `.g.cs` files)

Both files emit into the consuming assembly (`D2.Shared.Auth`) from the same spec:

1. **`AuthErrorCodes.g.cs`** — `D2.Shared.Auth.Errors.AuthErrorCodes` static class with one `public const string` per spec entry, `IReadOnlyList<string> AllCodes`, `int GetHttpStatus(string)`, and `string KebabCase(string)` for the ProblemDetails `type` URI helper.
2. **`AuthFailures.g.cs`** — `D2.Shared.Auth.Errors.AuthFailures` static class with one `public static D2Result FactoryName()` per spec entry, plus a typed `D2Result<T> FactoryName<T>()` overload for every `infrastructure_unavailable` entry.

The two-emitter split lives in one sourcegen because both outputs derive from the same spec rows — keeping them co-located ensures any spec edit re-emits both files together and prevents the constants catalog from drifting from the factory surface.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`contracts/auth-error-codes/schema.json`](../../../../../contracts/auth-error-codes/schema.json) — JSON Schema for the spec
- [`contracts/auth-error-codes/auth-error-codes.spec.json`](../../../../../contracts/auth-error-codes/auth-error-codes.spec.json) — the source-of-truth catalog
- [`D2.Shared.Auth.Scopes.SourceGen`](../scopes-source-gen/README.md) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [`D2.Shared.Telemetry.Tags.SourceGen`](../../telemetry/tags-source-gen/README.md) — sibling SrcGen consumes this spec via `valuesFromSpec=auth-error-codes` to drive the `d2.auth.problem.emitted` tag-value enumeration
