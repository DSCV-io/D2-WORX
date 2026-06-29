<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/protos/`

Hand-authored Protocol Buffer contracts — the `.proto` source files that define gRPC service interfaces and shared message types used across .NET services and the TypeScript gRPC client.

## Layout

```
contracts/protos/
└── common/
    └── v1/
        ├── d2_result.proto   — D2Result gRPC wire shape (status, errorCode, messages, traceId)
        ├── health.proto      — standard health-check service
        └── ping.proto        — liveness ping
```

## Consumed by

- **.NET** — service projects compile these via `Grpc.Tools` (MSBuild) in each consuming `server/shared/dotnet/*` and `server/services/*` project that references them; C# stubs are generated at build time
- **TypeScript / TypeSpec** — [`server/shared/typescript/typespec-emitters/`](../../server/shared/typescript/typespec-emitters/README.md) references the proto shapes when generating TypeScript gRPC client stubs

These are hand-authored `.proto` files (not generated from a spec), so there is no source-gen project of their own — the consumers above generate FROM them.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
