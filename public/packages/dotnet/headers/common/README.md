<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Headers.Common

> Parent: [`public/packages/dotnet/`](../README.md)

> **Duplicated from [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — update both in lockstep.** This catalog mirrors its TS sibling [`@dcsv-io/d2-headers-common`](../../typescript/headers/common/README.md) at byte-equal wire values per the cross-language parity contract documented in [`docs/PARITY.md`](../../../../docs/PARITY.md). Both sides emit from the same spec; physical dedup across .NET ↔ TS is not feasible. Parity is asserted by `HeaderCatalogConsistencyTests` (.NET) and `contract-tests/headers.parity.test.ts` (TS).

Cross-transport D2 wire-protocol headers — entries with applicability count >= 2 (i.e. headers that appear identically on multiple transports). Codegen-emitted from `contracts/headers/headers.spec.json` via `DcsvIo.D2.Headers.SourceGen` (filtered with `applicability.Length >= 2`). Mirrors TS `@dcsv-io/d2-headers-common`.

---

## Public API

| Member                             | Type                          | Purpose                                                                        |
| ---------------------------------- | ----------------------------- | ------------------------------------------------------------------------------ |
| `CommonHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"` | Base64url-of-JSON propagated context envelope (HTTP + gRPC + AMQP)             |
| `CommonHeaders.TRACEPARENT`        | `const string "traceparent"`  | W3C Trace Context (HTTP + gRPC + AMQP)                                         |
| `CommonHeaders.TRACESTATE`         | `const string "tracestate"`   | W3C tracestate (HTTP + gRPC + AMQP)                                            |
| `CommonHeaders.AllCommonHeaders`   | `IReadOnlyList<string>`       | All wire values in `constName` order — useful for cross-spec consistency tests |

(Catalog is codegen-emitted; the table above lists today's three cross-transport entries. New cross-transport entries appear here automatically when added to the spec.)

---

## When to reach for this catalog

Use `DcsvIo.D2.Headers.Common` when the consumer is transport-agnostic — e.g. a tracing utility that handles `traceparent` / `tracestate` regardless of whether the request arrived over HTTP, gRPC, or AMQP. Transport-specific consumers should reach for `DcsvIo.D2.Headers.Http`, `DcsvIo.D2.Headers.Amqp`, or `DcsvIo.D2.Headers.Grpc` instead — those catalogs include the cross-transport entries inline at identical wire values, so a single `using` covers everything that transport's pipeline can encounter.

---

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Cross-transport entries appear in `CommonHeaders` AND every per-transport catalog whose `applicability` array contains the relevant transport, all at identical wire values (codegen-guaranteed and verified by `HeaderCatalogConsistencyTests`).

---

## Build-time diagnostics + generated output

> Diagnostic IDs `D2HDR001`–`D2HDR007` and the generated-file path convention (`Generated/DcsvIo.D2.Headers.SourceGen/.../<Catalog>Headers.g.cs`) are documented at [`../source-gen/README.md` § Build-time diagnostics](../source-gen/README.md#build-time-diagnostics) and [§ Generated output convention](../source-gen/README.md#generated-output-convention).

---

## Dependencies

- `DcsvIo.D2.Headers.SourceGen` (build-time analyzer; `OutputItemType="Analyzer"` + `ReferenceOutputAssembly="false"`)

No runtime dependencies — pure constants.

---

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`DcsvIo.D2.Headers.SourceGen`](../source-gen/README.md) — emitter
- [`DcsvIo.D2.Headers.Http`](../http/README.md) — HTTP-applicable subset
- [`DcsvIo.D2.Headers.Amqp`](../amqp/README.md) — AMQP-applicable subset
- [`DcsvIo.D2.Headers.Grpc`](../grpc/README.md) — gRPC-applicable subset
