<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/auth-scopes/`

OAuth scope catalog — the closed set of permission scopes with their action-sensitivity level and impersonation-blocked flag.

## Namespace conventions (grant model)

- `anon.*` — anonymous (pre-auth) scopes, universally granted by namespace convention; they omit `grantedTo`.
- `internal.*` — internal service-to-service / in-process workload scopes (e.g. `internal.kc.sign`), granted by the internal transaction-token mint at the Edge boundary, never by the per-(`OrgType`, `Role`) grant matrix; they omit `grantedTo` and no user org-role can ever hold them (the intended reachability for a workload scope).
- Every other scope requires a `grantedTo` matrix — a non-`anon.*`, non-`internal.*` scope that omits it is an unreachable-scope error (`D2SCP008`).

## Consumed by

- **.NET** — [`server/shared/dotnet/auth/scopes-source-gen/`](../../server/shared/dotnet/auth/scopes-source-gen/README.md) (Roslyn source-gen → `Scopes` scope-tree constants in `D2.Shared.Auth.Abstractions`, consumed by per-handler `RequiredScopes` options)
- **TypeScript** — [`tools/ts-codegen` › `auth-scopes-emit.ts`](../../tools/ts-codegen/README.md) (→ `Scopes` const-object tree in `@d2/auth-abstractions`)
- **TypeSpec** — [`server/shared/typescript/typespec-decorators/`](../../server/shared/typescript/typespec-decorators/README.md) reads `scopes.spec.json` to validate scope-referencing decorator arguments at compile time

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
