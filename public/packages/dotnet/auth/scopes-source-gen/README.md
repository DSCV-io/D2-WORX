<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.Scopes.SourceGen

> Parent: [`public/packages/dotnet/`](../../README.md)

**Input contract:** [`public/contracts/auth-scopes/`](../../../../contracts/auth-scopes/README.md)

Roslyn incremental source generator that emits scope catalogs from `public/contracts/auth-scopes/scopes.spec.json` via `<AdditionalFiles>`.

**Dual-target** (assembly-name gate — see [`docs/SRC_GEN.md` §1.5](../../../../../docs/SRC_GEN.md#15-dual-target-dispatch--public-twin--private-extensions)):

| Consuming assembly | Emitted type | Values |
| --- | --- | --- |
| `DcsvIo.D2.Auth.Abstractions` | `Scopes` under `DcsvIo.D2.Auth.Abstractions` | public AdditionalFiles only |
| `DcsvIo.D2.Private.Auth.Abstractions.Extensions` | `ProductScopes` under `DcsvIo.D2.Private.Auth` | public∪private AdditionalFiles |

Any other assembly → no emit. Private host PackageId is 1:1 with the public twin + `.Extensions` (never a multi-concern bag).

The spec file is the single source of truth for the platform's scope catalog. Every scope a handler can require, every scope Edge mints into a token, and every grant-matrix entry lives in one JSON file — no hand-written parallel constants, no per-feature drift.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                                                        |
| ---------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2SCP001` | Error    | Spec file is malformed JSON or violates the schema                                                                                                             |
| `D2SCP002` | Error    | `grantedTo` references unknown `OrgType` / `Role` enum value                                                                                                   |
| `D2SCP003` | Error    | Scope name violates naming convention (lowercase dot-separated; segments must be valid C# identifiers; ≥ 2 segments; no consecutive / leading / trailing dots) |
| `D2SCP004` | Error    | Duplicate scope name                                                                                                                                           |
| `D2SCP005` | Warning  | Anonymous scope marked `impersonationBlocked` (meaningless — anon scopes are pre-auth)                                                                         |
| `D2SCP006` | Error    | `grantedTo` entry has empty role array — invalid config (omit the entry instead)                                                                               |
| `D2SCP007` | Error    | Two scopes collide at the same tree position (one is a strict dot-prefix of the other)                                                                         |
| `D2SCP008` | Error    | Non-anonymous, non-`internal.*` scope omits `grantedTo` (unreachable scope)                                                                                    |
| `D2SCP009` | Error    | No `scopes.spec.json` found in `AdditionalFiles`                                                                                                               |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "scopes": [
    {
      "name": "anon.public.health",
      "description": "Public health-check endpoint.",
      "actionSensitivity": "Routine",
      "impersonationBlocked": false
    },
    {
      "name": "auth.user.impersonate.consent",
      "description": "Initiate consent-based impersonation.",
      "actionSensitivity": "Sensitive",
      "impersonationBlocked": true,
      "grantedTo": { "Support": ["Officer"], "Admin": ["Owner", "Officer"] }
    },
    {
      "name": "billing.payment.charge",
      "description": "Initiate a payment charge.",
      "actionSensitivity": "Critical",
      "impersonationBlocked": true,
      "grantedTo": { "Customer": ["Owner"] }
    }
  ]
}
```

### Field rules

- **`name`**: dot-separated, lowercase, ≥ 2 segments, each segment `^[a-z][a-z0-9]*$`. Anonymous scopes start with `anon.`.
- **`description`**: free-form. Renders as XML `<summary>` on the emitted constant.
- **`actionSensitivity`**: one of `Routine` / `Sensitive` / `Critical`. Drives audit verbosity, OTP step-up, impersonation defaults.
- **`impersonationBlocked`**: `true` → Edge strips this scope from impersonated tokens at mint time (defense in depth — `RequiredScopes` check still rejects naturally). Meaningless on anon scopes (`D2SCP005`).
- **`grantedTo`**: per-(`OrgType`, `Role`) grant matrix. Keys: `OrgType` name (PascalCase) or `"*"`. Values: array of `Role` names (PascalCase) or `["*"]`. **Empty role arrays are forbidden** (`D2SCP006`) — for "no grant," omit the entry. Required for non-anon scopes (`D2SCP008`); omitted on `anon.*` (universal pre-auth grant by namespace convention) and on `internal.*` (internal service-to-service / in-process workload scopes granted by the internal transaction-token mint at the Edge boundary, never by the org-role matrix — no user org-role can ever hold them, which is the intended reachability).

### Wildcard expansion

`*` for org type expands against `DcsvIo.D2.Auth.Abstractions.OrgType` enum members; `*` for role expands against `DcsvIo.D2.Auth.Abstractions.Role` members. Expansion happens at **codegen time**, not runtime — adding a new enum member requires re-running the build to pick it up (the emitted `Scopes.g.cs` carries an `// auto-generated` header listing the enum members it expanded against).

---

## Emitted `Scopes.g.cs` shape

```csharp
public static partial class Scopes
{
    public static class Anon
    {
        public static class Public
        {
            public const string Health = "anon.public.health";
        }
        // ...
    }

    public static class Auth
    {
        public static class User
        {
            public static class Impersonate
            {
                public const string Consent = "auth.user.impersonate.consent";
                public const string Force = "auth.user.impersonate.force";
            }
        }
        // ...
    }

    public static ActionSensitivity GetActionSensitivity(string scope);
    public static bool IsImpersonationBlocked(string scope);
    public static bool IsAnonymous(string scope);
    public static bool IsKnown(string scope);
    public static bool IsGrantedTo(string scope, OrgType orgType, Role role);

    public static IReadOnlySet<string> AllScopes;
    public static IReadOnlySet<string> AllAnonymousScopes;
    public static IReadOnlySet<string> AllImpersonationBlockedScopes;
    public static IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>> GrantedScopes;
}
```

All lookup helpers are O(1) — backed by `HashSet<string>` / `Dictionary<,>`. `GrantedScopes` is the canonical `(OrgType, Role) → scope-set` map Edge consumes at JWT mint time.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`public/contracts/auth-scopes/schema.json`](../../../../contracts/auth-scopes/schema.json) — JSON Schema for the spec
- [`public/contracts/auth-scopes/scopes.spec.json`](../../../../contracts/auth-scopes/scopes.spec.json) — the source-of-truth scope catalog
- [`DcsvIo.D2.I18n.SourceGen`](../../i18n/source-gen/README.md) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- `ActionSensitivity` (this generator's per-scope classification driving audit verbosity, OTP step-up, and impersonation defaults) is orthogonal to `RateLimitTier`, the rate-limit middleware's per-endpoint throttling classification — a different subsystem entirely, neither value derived from the other.
