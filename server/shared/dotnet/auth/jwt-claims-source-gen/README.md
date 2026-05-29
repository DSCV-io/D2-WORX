<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.JwtClaims.SourceGen

> Parent: [`server/shared/dotnet/`](../../README.md)

Roslyn incremental source generator that emits the `JwtClaimTypes` static-class catalog into `D2.Shared.Auth.Abstractions` by reading `contracts/jwt-claims/jwt-claims.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.Auth.Abstractions`.

The spec file is the single source of truth for every JWT claim D2 reads or writes — standard OAuth/OIDC vocabulary (`sub`, `aud`, ...), D2-specific top-level claims (`d2_session_id`, `d2_org_id`, ...), and nested inside-act claims (`d2_kind` under the act object per RFC 8693 §2.1). A separate parity test (`JwtClaimsVsIAuthContextConsistencyTests`) asserts every `claim:` annotation in `IAuthContext.spec.json` references a valid entry here.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                      |
| ---------- | -------- | -------------------------------------------------------------------------------------------- |
| `D2JWT001` | Error    | Spec file is malformed JSON or violates the schema                                           |
| `D2JWT002` | Error    | A claim's `kind` is not in the closed enum (`standard` / `d2-custom` / `inside-act`)         |
| `D2JWT003` | Error    | A claim's `constName` violates UPPER_SNAKE_CASE pattern                                      |
| `D2JWT004` | Error    | Two claims share the same `constName`                                                        |
| `D2JWT005` | Error    | `jwt-claims.spec.json` is missing from `<AdditionalFiles>` for `D2.Shared.Auth.Abstractions` |
| `D2JWT006` | Error    | A claim's `value` is empty / whitespace-only                                                 |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "claims": [
    {
      "constName": "SUB",
      "value": "sub",
      "kind": "standard",
      "description": "Subject — the user's identifier."
    },
    {
      "constName": "SESSION_ID",
      "value": "d2_session_id",
      "kind": "d2-custom",
      "description": "User session identifier — links the token to a session record in auth_db."
    },
    {
      "constName": "ACT_KIND",
      "value": "d2_kind",
      "kind": "inside-act",
      "description": "Flavor of impersonation. Lookup path: act.d2_kind."
    }
  ]
}
```

### Field rules

- **`constName`** — UPPER_SNAKE_CASE C# identifier. Unique across the spec.
- **`value`** — wire-format claim name. Non-empty. Note: collisions across kinds are allowed (e.g. `SESSION_ID` and `ACT_SESSION_ID` both have value `d2_session_id` — distinct lookup paths).
- **`kind`** — closed enum `standard` / `d2-custom` / `inside-act`.
- **`description`** — XML `<summary>` text rendered on the emitted constant.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`contracts/jwt-claims/schema.json`](../../../../contracts/jwt-claims/schema.json) — JSON Schema for the spec
- [`contracts/jwt-claims/jwt-claims.spec.json`](../../../../contracts/jwt-claims/jwt-claims.spec.json) — the source-of-truth catalog
- [`D2.Shared.Headers.SourceGen`](../../headers/source-gen/README.md) — sibling SrcGen for cross-transport wire headers
- [`D2.Shared.InProcessKeys.SourceGen`](../../encryption/in-process-keys-source-gen/README.md) — sibling SrcGen for cross-binding in-process slot keys
