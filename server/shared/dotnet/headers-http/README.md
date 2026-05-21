<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Headers.Http

> Parent: [`server/shared/dotnet/`](../README.md)

> **Duplicated from [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — update both in lockstep.** This catalog mirrors its TS sibling [`@d2/headers-http`](../../typescript/headers-http/README.md) at byte-equal wire values per the cross-language parity contract documented in [`docs/PARITY.md`](../../../../docs/PARITY.md). Both sides emit from the same spec; physical dedup across .NET ↔ TS is not feasible. Parity is asserted by `HeaderCatalogConsistencyTests` (.NET) and `contract-tests/headers.parity.test.ts` (TS).

D2 wire-protocol headers applicable to the HTTP transport. Includes HTTP-only entries (`Authorization`, `Idempotency-Key`, `X-D2-Client-Fingerprint`, `X-D2-Internal-Token`) AND cross-transport entries (`x-d2-context`, `traceparent`, `tracestate`) at identical wire values. Codegen-emitted from `contracts/headers/headers.spec.json` via `D2.Shared.Headers.SourceGen` (filtered with `applicability.Contains("http")`). Mirrors TS `@d2/headers-http`.

---

## Public API

| Member | Type | Purpose |
|---|---|---|
| `HttpHeaders.AUTHORIZATION` | `const string "Authorization"` | RFC 6750 bearer token header |
| `HttpHeaders.CLIENT_FINGERPRINT` | `const string "X-D2-Client-Fingerprint"` | Client-computed device fingerprint |
| `HttpHeaders.IDEMPOTENCY_KEY` | `const string "Idempotency-Key"` | Stripe-style request-deduplication key |
| `HttpHeaders.INTERNAL_TOKEN` | `const string "X-D2-Internal-Token"` | BFF↔Edge service-identity JWT |
| `HttpHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"` | Base64url-of-JSON propagated context envelope (cross-transport) |
| `HttpHeaders.TRACEPARENT` | `const string "traceparent"` | W3C Trace Context (cross-transport) |
| `HttpHeaders.TRACESTATE` | `const string "tracestate"` | W3C tracestate (cross-transport) |
| `HttpHeaders.AllHttpHeaders` | `IReadOnlyList<string>` | All wire values in `constName` order |

---

## When to reach for this catalog

Use `D2.Shared.Headers.Http` from any HTTP-context consumer — `auth-http` middleware, `auth-outbound` HTTP token-exchange client, ASP.NET CORS configuration, idempotency middleware. The catalog includes BOTH the HTTP-only entries AND the cross-transport entries that an HTTP pipeline can encounter; one `using` covers everything.

---

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains `"http"` lives in this catalog. Cross-transport entries also live in `D2.Shared.Headers.Common` AND every other transport catalog they apply to, all at identical wire values (codegen-guaranteed and verified by `HeaderCatalogConsistencyTests`).

---

## Build-time diagnostics + generated output

> Diagnostic IDs `D2HDR001`–`D2HDR007` and the generated-file path convention (`Generated/D2.Shared.Headers.SourceGen/.../<Catalog>Headers.g.cs`) are documented at [`../headers-source-gen/README.md` § Build-time diagnostics](../headers-source-gen/README.md#build-time-diagnostics) and [§ Generated output convention](../headers-source-gen/README.md#generated-output-convention).

---

## Dependencies

- `D2.Shared.Headers.SourceGen` (build-time analyzer)

No runtime dependencies — pure constants.

---

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`D2.Shared.Headers.SourceGen`](../headers-source-gen/README.md) — emitter
- [`D2.Shared.Headers.Common`](../headers-common/README.md) — cross-transport subset
- [`D2.Shared.Headers.Amqp`](../headers-amqp/README.md) — AMQP-applicable subset
- [`D2.Shared.Headers.Grpc`](../headers-grpc/README.md) — gRPC-applicable subset
