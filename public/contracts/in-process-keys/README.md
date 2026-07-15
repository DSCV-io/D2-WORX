<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/in-process-keys/`

In-process key registry — the closed set of named keys used to stash per-request state inside a single service process (HTTP `HttpContext.Items` slots and gRPC user-state interceptor keys), kept identical across the HTTP and gRPC transports.

## Consumed by

- **.NET** — [`public/packages/dotnet/encryption/in-process-keys-source-gen/`](../../public/packages/dotnet/encryption/in-process-keys-source-gen/README.md) (Roslyn source-gen → `D2HttpContextItems` in `DcsvIo.D2.Auth.Abstractions` for `http`-bound entries + `D2GrpcUserStateKeys` in `DcsvIo.D2.Auth.Grpc` for `grpc`-bound entries)

No `tools/ts-codegen` emitter consumes this catalog — the keys are an in-process .NET concern with no wire representation.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
