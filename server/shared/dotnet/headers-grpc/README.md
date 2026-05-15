<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Headers.Grpc

> Parent: [`server/shared/dotnet/`](../README.md)

D2 wire-protocol headers applicable to the gRPC transport. Today the catalog holds the gRPC-applicable subset of cross-transport entries (`Authorization`, `x-d2-context`, `traceparent`, `tracestate`) at identical wire values. Codegen-emitted from `contracts/headers/headers.spec.json` via `D2.Shared.Headers.SourceGen` (filtered with `applicability.Contains("grpc")`). Mirrors TS `@d2/headers-grpc`.

---

## Public API

| Member | Type | Purpose |
|---|---|---|
| `GrpcHeaders.AUTHORIZATION` | `const string "Authorization"` | RFC 6750 bearer token header |
| `GrpcHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"` | Base64url-of-JSON propagated context envelope (cross-transport) |
| `GrpcHeaders.TRACEPARENT` | `const string "traceparent"` | W3C Trace Context (cross-transport) |
| `GrpcHeaders.TRACESTATE` | `const string "tracestate"` | W3C tracestate (cross-transport) |
| `GrpcHeaders.AllGrpcHeaders` | `IReadOnlyList<string>` | All wire values in `constName` order |

---

## Notes on gRPC framework constants

gRPC framework constants like `grpc-encoding`, `grpc-status`, `grpc-message`, `grpc-timeout` come from `Grpc.Core.Metadata` and are NOT part of `headers.spec.json`. This catalog covers only D2-defined headers and the cross-transport ones that flow through gRPC alongside framework headers.

---

## When to reach for this catalog

Use `D2.Shared.Headers.Grpc` from any gRPC-context consumer — gRPC interceptors, gRPC client wrappers in `auth-grpc` and `auth-outbound`. The catalog values are identical to the corresponding entries in `D2.Shared.Headers.Common` / `D2.Shared.Headers.Http` (codegen-guaranteed and verified by `HeaderCatalogConsistencyTests`).

---

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains `"grpc"` lives in this catalog.

---

## Build-time diagnostics

The SourceGen surfaces `D2HDR001`–`D2HDR007` for spec violations. See [`D2.Shared.Headers.SourceGen`](../headers-source-gen/README.md) for the full table.

---

## Codegen output

The emitted `GrpcHeaders.g.cs` lands at `Generated/D2.Shared.Headers.SourceGen/D2.Shared.Headers.SourceGen.HeadersGenerator/GrpcHeaders.g.cs` (tracked in git — committed for inspection, IDE navigation, and PR diff review). Re-emitted on every `dotnet build` from the spec; do not hand-edit. The `*.g.cs` glob is marked `linguist-generated=true` in `.gitattributes` so GitHub PR UI collapses these diffs by default.

---

## Dependencies

- `D2.Shared.Headers.SourceGen` (build-time analyzer)

No runtime dependencies — pure constants.

---

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`D2.Shared.Headers.SourceGen`](../headers-source-gen/) — emitter
- [`D2.Shared.Headers.Common`](../headers-common/) — cross-transport subset
- [`D2.Shared.Headers.Http`](../headers-http/) — HTTP-applicable subset
- [`D2.Shared.Headers.Amqp`](../headers-amqp/) — AMQP-applicable subset
