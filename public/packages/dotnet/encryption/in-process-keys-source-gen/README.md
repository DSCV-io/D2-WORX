<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.InProcessKeys.SourceGen

> Parent: [`public/packages/dotnet/`](../README.md)

**Input contract:** [`contracts/in-process-keys/`](../../../../../contracts/in-process-keys/README.md)

Roslyn incremental source generator that emits binding-specific in-process slot-key catalog classes by reading `contracts/in-process-keys/keys.spec.json` via `<AdditionalFiles>`. Dispatches per consuming assembly to one of two target catalogs.

The spec file is the single source of truth for cross-binding in-process slot keys (HTTP `HttpContext.Items` + gRPC `ServerCallContext.UserState`). Cross-binding entries appear in both .NET catalogs at identical wire values — codegen-guaranteed and verified by `HttpContextItemsVsGrpcUserStateKeysConsistencyTests`.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Catalog dispatch

| Consuming assembly            | Filter                      | Visibility | Emitted source                                                                                 |
| ----------------------------- | --------------------------- | ---------- | ---------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Auth.Abstractions` | `bindings.Contains("http")` | `public`   | `D2HttpContextItems.g.cs` (class `D2HttpContextItems` in `DcsvIo.D2.Auth.Abstractions.Http`)   |
| `DcsvIo.D2.Auth.Grpc`         | `bindings.Contains("grpc")` | `internal` | `D2GrpcUserStateKeys.g.cs` (class `D2GrpcUserStateKeys` in `DcsvIo.D2.Auth.Grpc.Interceptors`) |

The visibility difference (public vs internal) reflects the consumption pattern of each binding — HTTP downstream code reads `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]` directly, whereas gRPC consumers go through typed accessor extensions and the raw key class stays internal.

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                |
| ---------- | -------- | -------------------------------------------------------------------------------------- |
| `D2IPK001` | Error    | Spec file is malformed JSON or violates the schema                                     |
| `D2IPK002` | Error    | An entry's `bindings` array contains an unknown binding (closed enum: `http` / `grpc`) |
| `D2IPK003` | Error    | An entry's `constName` violates UPPER_SNAKE_CASE pattern                               |
| `D2IPK004` | Error    | `keys.spec.json` is missing from `<AdditionalFiles>` for the consuming csproj          |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "keys": [
    {
      "constName": "REQUEST_CONTEXT",
      "value": "D2.RequestContext",
      "purpose": "Slot under which the inbound auth runtime writes IRequestContext.",
      "bindings": ["http", "grpc"]
    }
  ]
}
```

### Field rules

- **`constName`** — UPPER_SNAKE_CASE C# identifier.
- **`value`** — wire value of the slot key. Identical across every binding in `bindings`.
- **`purpose`** — XML `<summary>` text rendered on every emitted catalog the entry appears in.
- **`bindings`** — closed enum `http` / `grpc`. Every key MUST belong to at least one binding.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`contracts/in-process-keys/schema.json`](../../../../../contracts/in-process-keys/schema.json) — JSON Schema for the spec
- [`contracts/in-process-keys/keys.spec.json`](../../../../../contracts/in-process-keys/keys.spec.json) — the source-of-truth catalog
- [`DcsvIo.D2.Headers.SourceGen`](../../headers/source-gen/README.md) — sibling SrcGen for cross-transport wire headers
- [`DcsvIo.D2.Auth.JwtClaims.SourceGen`](../../auth/jwt-claims-source-gen/README.md) — sibling SrcGen for JWT claim names
