<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/headers-common

> Parent: [`server/shared/typescript/`](../README.md)

> **Duplicated from [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — update both in lockstep.** This catalog mirrors its .NET sibling [`D2.Shared.Headers.Common`](../../dotnet/headers-common/README.md) at byte-equal wire values per the cross-language parity contract documented in [`docs/PARITY.md`](../../../../docs/PARITY.md). Both sides emit from the same spec; physical dedup across TS ↔ .NET is not feasible. Parity is asserted by `contract-tests/headers.parity.test.ts` (TS) and `HeaderCatalogConsistencyTests` (.NET).

Cross-transport D2 wire-protocol headers — entries with applicability count >= 2 (i.e. headers that appear identically on multiple transports). Mirrors .NET `D2.Shared.Headers.Common.CommonHeaders`.

## Public API

| Export               | Source                | Mirror                                      |
| -------------------- | --------------------- | ------------------------------------------- |
| `CommonHeaders`      | `common-headers.g.ts` | `D2.Shared.Headers.Common.CommonHeaders`    |
| `CommonHeaderName`   | `common-headers.g.ts` | n/a (TS-only union type)                    |
| `ALL_COMMON_HEADERS` | `common-headers.g.ts` | `D2.Shared.Headers.Common.AllCommonHeaders` |

## Codegen workflow

`prebuild` invokes `tools/ts-codegen/src/headers-emit.ts --target=common` before `tsc -b`, so `pnpm -r build` regenerates the catalog from `contracts/headers/headers.spec.json`. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

Use `@d2/headers-common` when the consumer is transport-agnostic — e.g. a tracing utility that handles `traceparent` / `tracestate` regardless of whether the request arrived over HTTP, gRPC, or AMQP. Transport-specific consumers should reach for `@d2/headers-http`, `@d2/headers-amqp`, or `@d2/headers-grpc` instead — those catalogs include the cross-transport entries inline at identical wire values, so a single `import` covers everything that transport's pipeline can encounter.

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Cross-transport entries appear in `CommonHeaders` AND every per-transport catalog whose `applicability` array contains the relevant transport, all at identical wire values (codegen-guaranteed; verified by `HeaderCatalogConsistencyTests` on the .NET side).

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`@d2/headers-http`](../headers-http/README.md) — HTTP-applicable subset
- [`@d2/headers-amqp`](../headers-amqp/README.md) — AMQP-applicable subset
- [`@d2/headers-grpc`](../headers-grpc/README.md) — gRPC-applicable subset
