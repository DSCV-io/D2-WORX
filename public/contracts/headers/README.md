<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/headers/`

HTTP, AMQP, and gRPC header registry — the closed set of D²-specific headers (`x-d2-trace-id`, `x-d2-correlation-id`, `x-d2-org-id`, etc.) used across all transport layers.

## Consumed by

- **.NET** — [`public/packages/dotnet/headers/source-gen/`](../../public/packages/dotnet/headers/source-gen/README.md) (Roslyn source-gen → per-transport header-name constants in `DcsvIo.D2.Headers.Common` / `.Http` / `.Grpc` / `.Amqp`)
- **TypeScript** — [`tools/ts-codegen` › `headers-emit.ts`](../../tools/ts-codegen/README.md) (→ matching per-transport header-name constants in `@dcsv-io/d2-headers`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
