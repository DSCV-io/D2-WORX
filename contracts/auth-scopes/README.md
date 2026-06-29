<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/auth-scopes/`

OAuth scope catalog — the closed set of permission scopes with their action-sensitivity level and impersonation-blocked flag.

## Consumed by

- **.NET** — [`server/shared/dotnet/auth/scopes-source-gen/`](../../server/shared/dotnet/auth/scopes-source-gen/README.md) (Roslyn source-gen → `Scopes` scope-tree constants in `D2.Shared.Auth.Abstractions`, consumed by per-handler `RequiredScopes` options)
- **TypeScript** — [`tools/ts-codegen` › `auth-scopes-emit.ts`](../../tools/ts-codegen/README.md) (→ `Scopes` const-object tree in `@d2/auth-abstractions`)
- **TypeSpec** — [`server/shared/typescript/typespec-decorators/`](../../server/shared/typescript/typespec-decorators/README.md) reads `scopes.spec.json` to validate scope-referencing decorator arguments at compile time

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
