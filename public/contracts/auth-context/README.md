<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/auth-context/`

`IAuthContext` interface spec — the identity and authorization context fields derived from the bearer JWT and surfaced to every handler and middleware across all transports.

## Consumed by

- **.NET** — [`public/packages/dotnet/context/source-gen/`](../../public/packages/dotnet/context/source-gen/README.md) (Roslyn source-gen → `PropagatedContext` + serializer in `D2.Shared.Context.Abstractions`; the same generator also emits the request-context layer)
- **TypeScript** — [`tools/ts-codegen` › `auth-context-emit.ts`](../../tools/ts-codegen/README.md) (→ `IAuthContext` interface + 4 enums + `ActorEntry` in `@d2/auth-context-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
