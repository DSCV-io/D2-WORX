<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-headers-amqp

> Parent: [`public/packages/typescript/`](../../README.md)

> **Duplicated from [`contracts/headers/headers.spec.json`](../../../../../contracts/headers/headers.spec.json) — update both in lockstep.** This catalog mirrors its .NET sibling [`DcsvIo.D2.Headers.Amqp`](../../../dotnet/headers/amqp/README.md) at byte-equal wire values per the cross-language parity contract documented in [`docs/PARITY.md`](../../../../../docs/PARITY.md). Both sides emit from the same spec; physical dedup across TS ↔ .NET is not feasible. Parity is asserted by `contract-tests/headers.parity.test.ts` (TS) and `HeaderCatalogConsistencyTests` (.NET).

D2 wire-protocol headers applicable to the AMQP transport. Includes the AMQP-specific entries (`content-type`, `x-proto-type`, `message-id`, `timestamp`, `x-d2-encryption-kid`, `x-d2-failure-reason`) AND the cross-transport entries that ride alongside AMQP messages (`x-d2-context`, `traceparent`, `tracestate`) at identical wire values per `headers.spec.json`. Mirrors .NET `DcsvIo.D2.Headers.Amqp.AmqpHeaders`.

## Public API

| Export             | Source              | Mirror                                  |
| ------------------ | ------------------- | --------------------------------------- |
| `AmqpHeaders`      | `amqp-headers.g.ts` | `DcsvIo.D2.Headers.Amqp.AmqpHeaders`    |
| `AmqpHeaderName`   | `amqp-headers.g.ts` | n/a (TS-only union type)                |
| `ALL_AMQP_HEADERS` | `amqp-headers.g.ts` | `DcsvIo.D2.Headers.Amqp.AllAmqpHeaders` |

## Codegen workflow

`prebuild` invokes `tools/ts-codegen/src/headers-emit.ts --target=amqp` before `tsc -b`, so `pnpm -r build` regenerates the catalog from `contracts/headers/headers.spec.json`. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

Use `@dcsv-io/d2-headers-amqp` from any AMQP-context consumer — RabbitMQ publishers, subscribers, DLQ inspection tools. The catalog includes BOTH the AMQP-only entries (e.g. `MESSAGE_ID`) AND the cross-transport entries (e.g. `TRACEPARENT`) that an AMQP pipeline can encounter; one `import` covers everything that transport's pipeline can encounter.

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains `"amqp"` lives in this catalog (cross-transport entries also live in `@dcsv-io/d2-headers-common` AND every other transport catalog they apply to, all at identical wire values; codegen-guaranteed and verified by `HeaderCatalogConsistencyTests` on the .NET side).

## Header categories

- **Routing + observability**: `MESSAGE_ID`, `TIMESTAMP`, `CONTENT_TYPE`, `PROTO_TYPE`
- **Cross-hop tracing**: `TRACEPARENT`, `TRACESTATE`, `PROPAGATED_CONTEXT`
- **Encryption + DLQ ops**: `ENCRYPTION_KID`, `FAILURE_REASON`

Headers MUST NOT carry user identity, scopes, fingerprints, or any other sensitive context — the broker stores headers as plaintext at rest.

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.

## Reference

- [`contracts/headers/headers.spec.json`](../../../../../contracts/headers/headers.spec.json) — source spec
- [`@dcsv-io/d2-headers-common`](../common/README.md) — cross-transport subset
- [`@dcsv-io/d2-headers-http`](../http/README.md) — HTTP-applicable subset
- [`@dcsv-io/d2-headers-grpc`](../grpc/README.md) — gRPC-applicable subset
