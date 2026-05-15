<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Headers.Http

> Parent: [`server/shared/dotnet/`](../README.md)

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

## Build-time diagnostics

The SourceGen surfaces `D2HDR001`–`D2HDR007` for spec violations. See [`D2.Shared.Headers.SourceGen`](../headers-source-gen/README.md) for the full table.

---

## Codegen output

The emitted `HttpHeaders.g.cs` lands at `Generated/D2.Shared.Headers.SourceGen/D2.Shared.Headers.SourceGen.HeadersGenerator/HttpHeaders.g.cs` (tracked in git — committed for inspection, IDE navigation, and PR diff review). Re-emitted on every `dotnet build` from the spec; do not hand-edit. The `*.g.cs` glob is marked `linguist-generated=true` in `.gitattributes` so GitHub PR UI collapses these diffs by default.

---

## Dependencies

- `D2.Shared.Headers.SourceGen` (build-time analyzer)

No runtime dependencies — pure constants.

---

## Reference

- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — source spec
- [`D2.Shared.Headers.SourceGen`](../headers-source-gen/) — emitter
- [`D2.Shared.Headers.Common`](../headers-common/) — cross-transport subset
- [`D2.Shared.Headers.Amqp`](../headers-amqp/) — AMQP-applicable subset
- [`D2.Shared.Headers.Grpc`](../headers-grpc/) — gRPC-applicable subset
