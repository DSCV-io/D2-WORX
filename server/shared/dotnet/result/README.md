<!--
Copyright (c) DCSV. All rights reserved.
-->

# result/

> Parent: [`server/shared/dotnet/`](../README.md)

The `D2Result` errors-as-values core that every handler, repo, and service returns from — plus the spec-driven source generators that emit its JSON wire envelope and its gRPC trailer envelope. The result core carries semantic factories, the partial-success ladder, `BubbleFail` propagation, and an auto-injected `traceId`; user-facing messages are typed as translation keys so every message is compile-time enforced to be translatable. The envelope and trailer catalogs are spec-driven so the JSON / gRPC wire shapes match the TS-side `@d2/result` catalog byte-for-byte.

## Packages

| Package                                                       | Description                                                                                                                          |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md)                                     | `D2Result<T>` — semantic factories, partial-success ladder, `BubbleFail` propagation, auto-injected `traceId`, translation-key-typed messages. |
| [`grpc/`](grpc/README.md)                                     | Faithful `D2Result` ↔ `D2ResultProto` gRPC response-envelope codec. `ToProto()` (server WRAP) / `ToD2Result<T>()` (client RE-MATERIALIZE) / `HandleAsync<T>()` (call wrapper with transport-fault fail-open) / `IsTransientGrpcException()`. Business failures travel as normal gRPC `OK` responses; `RpcException` is reserved for auth/transport-layer faults (`D2.Shared.Auth.Grpc`). |
| [`envelope-source-gen/`](envelope-source-gen/README.md)       | Roslyn generator emitting the `D2Result` JSON wire-envelope field-name constants into `core/` from `contracts/d2result-envelope/d2result-envelope.spec.json`. |
| [`grpc-trailers-source-gen/`](grpc-trailers-source-gen/README.md) | Roslyn generator emitting the gRPC trailer-key catalog into `auth/grpc/` from `contracts/grpc-trailers/grpc-trailers.spec.json` — the `d2_error_code` / `d2_messages` / `traceId` trailer triple used by the auth interceptor's `RpcException` path (distinct from the business-result `D2ResultProto` envelope). |
