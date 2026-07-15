<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Headers.Grpc

> Parent: [`public/packages/dotnet/`](../README.md)

> **Duplicated from [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — update both in lockstep.** This catalog mirrors its TS sibling [`@dcsv-io/d2-headers-grpc`](../../typescript/headers/grpc/README.md) at byte-equal wire values per the cross-language parity contract documented in [`docs/PARITY.md`](../../../../docs/PARITY.md). Both sides emit from the same spec; physical dedup across .NET ↔ TS is not feasible. Parity is asserted by `HeaderCatalogConsistencyTests` (.NET) and `contract-tests/headers.parity.test.ts` (TS).

D2 wire-protocol headers applicable to the gRPC transport. Today the catalog holds the gRPC-applicable subset of cross-transport entries (`Authorization`, `x-d2-context`, `traceparent`, `tracestate`) at identical wire values. Codegen-emitted from `contracts/headers/headers.spec.json` via `DcsvIo.D2.Headers.SourceGen` (filtered with `applicability.Contains("grpc")`). Mirrors TS `@dcsv-io/d2-headers-grpc`.

---

## Public API

| Member                           | Type                           | Purpose                                                         |
| -------------------------------- | ------------------------------ | --------------------------------------------------------------- |
| `GrpcHeaders.AUTHORIZATION`      | `const string "Authorization"` | RFC 6750 bearer token header                                    |
| `GrpcHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"`  | Base64url-of-JSON propagated context envelope (cross-transport) |
| `GrpcHeaders.TRACEPARENT`        | `const string "traceparent"`   | W3C Trace Context (cross-transport)                             |
| `GrpcHeaders.TRACESTATE`         | `const string "tracestate"`    | W3C tracestate (cross-transport)                                |
| `GrpcHeaders.AllGrpcHeaders`     | `IReadOnlyList<string>`        | All wire values in `constName` order                            |

---

## Notes on gRPC framework constants

gRPC framework constants like `grpc-encoding`, `grpc-status`, `grpc-message`, `grpc-timeout` come from `Grpc.Core.Metadata` and are NOT part of `headers.spec.json`. This catalog covers only D2-defined headers and the cross-transport ones that flow through gRPC alongside framework headers.

---

## When to reach for this catalog

Use `DcsvIo.D2.Headers.Grpc` from any gRPC-context consumer — gRPC interceptors and the gRPC client wrappers in `auth/grpc` and `auth/outbound`. On a cross-process hop the gRPC client forwards the once-minted internal transaction-token unchanged in the `Authorization` header over mTLS, which establishes workload identity ([ADR-0023](../../../../../public/docs/adrs/0023-mtls-workload-identity.md)); the prior `client_credentials` service-identity layer is superseded by that mTLS workload identity. The catalog values are identical to the corresponding entries in `DcsvIo.D2.Headers.Common` / `DcsvIo.D2.Headers.Http` (codegen-guaranteed and verified by `HeaderCatalogConsistencyTests`).

---

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains `"grpc"` lives in this catalog.

---

## Build-time diagnostics + generated output

> Diagnostic IDs `D2HDR001`–`D2HDR007` and the generated-file path convention (`Generated/DcsvIo.D2.Headers.SourceGen/.../<Catalog>Headers.g.cs`) are documented at [`../source-gen/README.md` § Build-time diagnostics](../source-gen/README.md#build-time-diagnostics) and [§ Generated output convention](../source-gen/README.md#generated-output-convention).

---

## Dependencies

- `DcsvIo.D2.Headers.SourceGen` (build-time analyzer)

No runtime dependencies — pure constants.

---

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`DcsvIo.D2.Headers.SourceGen`](../source-gen/README.md) — emitter
- [`DcsvIo.D2.Headers.Common`](../common/README.md) — cross-transport subset
- [`DcsvIo.D2.Headers.Http`](../http/README.md) — HTTP-applicable subset
- [`DcsvIo.D2.Headers.Amqp`](../amqp/README.md) — AMQP-applicable subset
