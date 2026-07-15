<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/grpc-trailers/`

gRPC trailer key catalog — the metadata trailer names written by the .NET gRPC handler (`d2_error_code`, `d2_messages`, `traceId`) and read by the TypeScript gRPC client to reconstruct a `D2Result` from a gRPC response.

## Consumed by

- **.NET** — [`public/packages/dotnet/result/grpc-trailers-source-gen/`](../../public/packages/dotnet/result/grpc-trailers-source-gen/README.md) (Roslyn source-gen → `D2GrpcTrailers` constants in `DcsvIo.D2.Auth.Grpc`)
- **TypeScript** — [`tools/ts-codegen` › `grpc-trailers-emit.ts`](../../tools/ts-codegen/README.md) (→ matching trailer-key constants in `@dcsv-io/d2-grpc-client`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
