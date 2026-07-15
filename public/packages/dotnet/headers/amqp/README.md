<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Headers.Amqp

> Parent: [`public/packages/dotnet/`](../README.md)

> **Duplicated from [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — update both in lockstep.** This catalog mirrors its TS sibling [`@dcsv-io/d2-headers-amqp`](../../typescript/headers/amqp/README.md) at byte-equal wire values per the cross-language parity contract documented in [`docs/PARITY.md`](../../../../docs/PARITY.md). Both sides emit from the same spec; physical dedup across .NET ↔ TS is not feasible. Parity is asserted by `HeaderCatalogConsistencyTests` (.NET) and `contract-tests/headers.parity.test.ts` (TS).

D2 wire-protocol headers applicable to the AMQP transport. Includes AMQP-only entries (`content-type`, `x-proto-type`, `message-id`, `timestamp`, `x-d2-encryption-kid`, `x-d2-failure-reason`) AND cross-transport entries (`x-d2-context`, `traceparent`, `tracestate`) at identical wire values. Codegen-emitted from `contracts/headers/headers.spec.json` via `DcsvIo.D2.Headers.SourceGen` (filtered with `applicability.Contains("amqp")`). Mirrors TS `@dcsv-io/d2-headers-amqp`.

---

## Public API

| Member                           | Type                                 | Purpose                                                           |
| -------------------------------- | ------------------------------------ | ----------------------------------------------------------------- |
| `AmqpHeaders.CONTENT_TYPE`       | `const string "content-type"`        | Always `application/octet-stream` for D2 messages                 |
| `AmqpHeaders.ENCRYPTION_KID`     | `const string "x-d2-encryption-kid"` | Encryption key id duplicated from the encrypted frame for DLQ ops |
| `AmqpHeaders.FAILURE_REASON`     | `const string "x-d2-failure-reason"` | DLQ failure metadata attached by the consumer on nack             |
| `AmqpHeaders.MESSAGE_ID`         | `const string "message-id"`          | UUIDv7 message identifier                                         |
| `AmqpHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"`        | Base64url-of-JSON propagated context envelope (cross-transport)   |
| `AmqpHeaders.PROTO_TYPE`         | `const string "x-proto-type"`        | Fully-qualified proto type name                                   |
| `AmqpHeaders.TIMESTAMP`          | `const string "timestamp"`           | Producer-set ISO 8601 UTC timestamp                               |
| `AmqpHeaders.TRACEPARENT`        | `const string "traceparent"`         | W3C Trace Context (cross-transport)                               |
| `AmqpHeaders.TRACESTATE`         | `const string "tracestate"`          | W3C tracestate (cross-transport)                                  |
| `AmqpHeaders.AllAmqpHeaders`     | `IReadOnlyList<string>`              | All wire values in `constName` order                              |

---

## Header categories

- **Routing + observability**: `MESSAGE_ID`, `TIMESTAMP`, `CONTENT_TYPE`, `PROTO_TYPE`
- **Cross-hop tracing**: `TRACEPARENT`, `TRACESTATE`, `PROPAGATED_CONTEXT`
- **Encryption + DLQ ops**: `ENCRYPTION_KID`, `FAILURE_REASON`

Headers MUST NOT carry user identity, scopes, fingerprints, or any other sensitive context — the broker stores headers as plaintext at rest. Sensitive identity that a consumer needs goes in the typed message body (encrypted via the descriptor's `encryption` domain when the type carries PII).

---

## When to reach for this catalog

Use `DcsvIo.D2.Headers.Amqp` from any AMQP-context consumer — `messaging/rabbitmq` publishers, subscribers, DLQ inspection tools. One `using` covers AMQP-only entries AND cross-transport entries.

---

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains `"amqp"` lives in this catalog.

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
- [`DcsvIo.D2.Headers.Grpc`](../grpc/README.md) — gRPC-applicable subset
