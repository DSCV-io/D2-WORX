<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Headers.Common

> Parent: [`server/shared/dotnet/`](../README.md)

Cross-transport D2 wire-protocol headers — entries with applicability count >= 2 (i.e. headers that appear identically on multiple transports). Codegen-emitted from `contracts/headers/headers.spec.json` via `D2.Shared.Headers.SourceGen` (filtered with `applicability.Length >= 2`). Mirrors TS `@d2/headers-common`.

---

## Public API

| Member | Type | Purpose |
|---|---|---|
| `CommonHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"` | Base64url-of-JSON propagated context envelope (HTTP + gRPC + AMQP) |
| `CommonHeaders.TRACEPARENT` | `const string "traceparent"` | W3C Trace Context (HTTP + gRPC + AMQP) |
| `CommonHeaders.TRACESTATE` | `const string "tracestate"` | W3C tracestate (HTTP + gRPC + AMQP) |
| `CommonHeaders.AllCommonHeaders` | `IReadOnlyList<string>` | All wire values in `constName` order — useful for cross-spec consistency tests |

(Catalog is codegen-emitted; the table above lists today's three cross-transport entries. New cross-transport entries appear here automatically when added to the spec.)

---

## When to reach for this catalog

Use `D2.Shared.Headers.Common` when the consumer is transport-agnostic — e.g. a tracing utility that handles `traceparent` / `tracestate` regardless of whether the request arrived over HTTP, gRPC, or AMQP. Transport-specific consumers should reach for `D2.Shared.Headers.Http`, `D2.Shared.Headers.Amqp`, or `D2.Shared.Headers.Grpc` instead — those catalogs include the cross-transport entries inline at identical wire values, so a single `using` covers everything that transport's pipeline can encounter.

---

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Cross-transport entries appear in `CommonHeaders` AND every per-transport catalog whose `applicability` array contains the relevant transport, all at identical wire values (codegen-guaranteed and verified by `HeaderCatalogConsistencyTests`).

---

## Build-time diagnostics

The SourceGen surfaces `D2HDR001`–`D2HDR007` for spec violations. See [`D2.Shared.Headers.SourceGen`](../headers-source-gen/README.md) for the full table.

---

## Codegen output

The emitted `CommonHeaders.g.cs` lands at `Generated/D2.Shared.Headers.SourceGen/D2.Shared.Headers.SourceGen.HeadersGenerator/CommonHeaders.g.cs` (tracked in git — committed for inspection, IDE navigation, and PR diff review). Re-emitted on every `dotnet build` from the spec; do not hand-edit. The `*.g.cs` glob is marked `linguist-generated=true` in `.gitattributes` so GitHub PR UI collapses these diffs by default.

---

## Dependencies

- `D2.Shared.Headers.SourceGen` (build-time analyzer; `OutputItemType="Analyzer"` + `ReferenceOutputAssembly="false"`)

No runtime dependencies — pure constants.

---

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`D2.Shared.Headers.SourceGen`](../headers-source-gen/) — emitter
- [`D2.Shared.Headers.Http`](../headers-http/) — HTTP-applicable subset
- [`D2.Shared.Headers.Amqp`](../headers-amqp/) — AMQP-applicable subset
- [`D2.Shared.Headers.Grpc`](../headers-grpc/) — gRPC-applicable subset
