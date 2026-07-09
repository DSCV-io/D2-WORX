<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/d2result-envelope/`

`D2Result` wire-envelope field catalog — the JSON property names of the Shape-B response envelope (`success`, `data`, `messages`, `inputErrors`, `errorCode`, `traceId`, `statusCode`, `category`).

## Consumed by

- **.NET** — [`server/shared/dotnet/result/envelope-source-gen/`](../../server/shared/dotnet/result/envelope-source-gen/README.md) (Roslyn source-gen → `D2ResultEnvelopeFieldNames` constants in `D2.Shared.Result`)
- **TypeScript** — [`tools/ts-codegen` › `d2result-envelope-emit.ts`](../../tools/ts-codegen/README.md) (→ matching field-name constants in `@d2/result`, so the BFF gateway parser uses the same identifiers as the .NET serializer)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
