<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/headers-grpc

D2 wire-protocol headers applicable to the gRPC transport. Today the catalog holds the gRPC-applicable subset of cross-transport entries (`Authorization`, `x-d2-context`, `traceparent`, `tracestate`) at identical wire values per `headers.spec.json`. Mirrors .NET `D2.Shared.Headers.Grpc.GrpcHeaders`.

## Public API

| Export             | Source              | Mirror                                  |
| ------------------ | ------------------- | --------------------------------------- |
| `GrpcHeaders`      | `grpc-headers.g.ts` | `D2.Shared.Headers.Grpc.GrpcHeaders`    |
| `GrpcHeaderName`   | `grpc-headers.g.ts` | n/a (TS-only union type)                |
| `ALL_GRPC_HEADERS` | `grpc-headers.g.ts` | `D2.Shared.Headers.Grpc.AllGrpcHeaders` |

## Codegen workflow

`prebuild` invokes `tools/ts-codegen/src/headers-emit.ts --target=grpc` before `tsc -b`, so `pnpm -r build` regenerates the catalog from `contracts/headers/headers.spec.json`. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

Use `@d2/headers-grpc` from any gRPC-context consumer — gRPC interceptors, gRPC client wrappers. The catalog includes cross-transport entries (e.g. `TRACEPARENT`) at identical wire values to the other catalogs (codegen-guaranteed and verified by `HeaderCatalogConsistencyTests` on the .NET side).

## Notes on gRPC framework constants

gRPC framework constants like `grpc-encoding`, `grpc-status`, `grpc-message`, `grpc-timeout` come from `Grpc.Core.Metadata` (.NET) or the corresponding gRPC framework symbol on the TS side and are NOT part of `headers.spec.json`. This catalog covers only D2-defined headers and the cross-transport ones that flow through gRPC alongside framework headers.

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains `"grpc"` lives in this catalog.

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`@d2/headers-common`](../headers-common/) — cross-transport subset
- [`@d2/headers-http`](../headers-http/) — HTTP-applicable subset
- [`@d2/headers-amqp`](../headers-amqp/) — AMQP-applicable subset
