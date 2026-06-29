<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/request-context/`

`IRequestContext` interface spec — the per-request runtime context that extends `IAuthContext` with transport-level tracing fields (`traceId`, `correlationId`) and network/enrichment information populated by ASP.NET Core and RabbitMQ middleware.

## Consumed by

- **.NET** — [`server/shared/dotnet/context/source-gen/`](../../server/shared/dotnet/context/source-gen/README.md) (Roslyn source-gen → `PropagatedContext` + extensions + serializer in `D2.Shared.Context.Abstractions`; the same generator also emits the auth-context layer this one extends)
- **TypeScript** — [`tools/ts-codegen` › `request-context-emit.ts`](../../tools/ts-codegen/README.md) (→ `IRequestContext` interface + `IPropagatedContext` + `PropagatedContextSerializer` in `@d2/request-context-abstractions`, extending the generated `IAuthContext`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
