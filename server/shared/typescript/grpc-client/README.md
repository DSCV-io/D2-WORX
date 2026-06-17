<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/grpc-client

> Parent: [`server/shared/typescript/`](../README.md)

Singleton-per-process gRPC channel from the SvelteKit BFF to Edge, with
two interceptors:

- **Internal-token interceptor** — OAuth token-endpoint JWT (audience
  `d2.edge`, 15-min TTL), BFF module-singleton cache with proactive
  refresh-ahead, refresh-on-401 cache invalidation.
- **Context-propagation interceptor** — serializes the current request's
  `IPropagatedContext` into the `x-d2-context` gRPC metadata key + forwards
  `traceparent` / `tracestate` for W3C tracing.

Mirrors the .NET `services.AddGrpcClient<T>()` registration shape +
`D2.Shared.Auth.Outbound.ServiceIdentity.IServiceIdentityClient` semantics.

## Public API

### Channel + interceptors

| Export                                      | Purpose                                                                                        |
| ------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `getChannel(opts?)`                         | Singleton-per-process channel accessor; lazy-init; concurrent dedup.                           |
| `closeChannel()`                            | Idempotent shutdown; safe to call from any number of process-shutdown hooks.                   |
| `createInternalTokenInterceptor(opts)`      | Returns a gRPC `Interceptor` that attaches `Authorization: Bearer <jwt>` on every call.        |
| `createContextPropagationInterceptor(opts)` | Returns an `Interceptor` that injects `x-d2-context` + forwards `traceparent` / `tracestate`. |
| `InternalTokenCache`                        | Single-slot cache for the BFF's internal token; three-state read (fresh / aging / expired) with proactive refresh-ahead. |
| `InternalTokenClient` (interface)           | Pluggable contract for OAuth token-endpoint backends. Production wires `HttpInternalTokenClient`. |
| `HttpInternalTokenClient`                   | Node-native `fetch()`-based OAuth client (`grant_type=client_credentials`); Singleflight-deduped. |

### gRPC result codec — `D2Result` ↔ `D2ResultProto` wire round-trip

Mirrors .NET `D2.Shared.Result.Grpc.ProtoExtensions`. Every gRPC response
message carries a `D2ResultProto result = N` envelope field; the typed
payload rides in sibling fields. Business failures return a normal response
(`success=false` in the envelope) — `RpcException` is reserved for
transport/auth-layer faults only.

| Export                                                              | Purpose                                                                                                                                              |
| ------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `d2ResultToProto(result)`                                           | Serialize a `D2Result` to its `D2ResultProto` wire form (server-side WRAP).                                                                          |
| `d2ResultFromProto<TData>(proto, data?)`                            | Reconstruct a `D2Result<TData>` from proto + optional separately-selected payload (client-side RE-MATERIALIZE).                                      |
| `handleGrpcCall(callFn, resultSelector, dataSelector)`              | Execute a unary call, re-materialize the result, and fail-open on transport faults — `ServiceError(CANCELLED)` → `canceled`; `ServiceError(UNAUTHENTICATED)` → `unauthorized`; other `ServiceError` → `serviceUnavailable`; non-`ServiceError` → `unhandledException`. |
| `unaryCall(method, request, opts?)`                                 | Promise-wrapper for `@grpc/grpc-js` callback-style unary methods. Accepts optional `{ deadlineMs }`.                                                 |
| `isTransientGrpcError(err)`                                         | `true` for retry-safe gRPC status codes (DEADLINE_EXCEEDED / RESOURCE_EXHAUSTED / ABORTED / INTERNAL / UNAVAILABLE).                                 |
| `UnaryCallOptions` (interface)                                      | `{ deadlineMs?: number }` — options for `unaryCall`.                                                                                                 |

#### Wire boundary: envelope vs. transport-reject

Two mechanisms coexist — each has a distinct role:

| Mechanism | When | Transport | Reads by |
| --- | --- | --- | --- |
| `D2ResultProto` envelope | Business results (success AND failure) | gRPC `OK` status + response body | `handleGrpcCall` / `d2ResultFromProto` |
| `RpcException` + `D2GrpcTrailers` | Transport/auth rejects (JWT validation failed) | Non-OK gRPC status + trailers | Auth middleware (server-side only) |

A `401` from the JWT middleware is a genuine transport fault → `RpcException(Unauthenticated)`.
A `404` from a handler is a business result → `D2ResultProto{ success=false, status_code=404 }`.

#### Call-site pattern

```ts
import { getChannel, createInternalTokenInterceptor, handleGrpcCall, unaryCall } from "@d2/grpc-client";

// One-time setup at composition root (unchanged from before).
const channel = await getChannel();
const client = new EdgeServiceClient(channel.getTarget(), undefined, {
  channelOverride: channel,
  interceptors: [ /* … */ ],
});

// Per-call — selectors are compiler-checked against the generated response type.
const result = await handleGrpcCall(
  () => unaryCall(client.doThing.bind(client), req, { deadlineMs: 5_000 }),
  r => r.result,  // D2ResultProto envelope field
  r => r.data,    // typed payload field (undefined on failure shapes)
);
if (result.category === "not_found") { /* … */ }
```

#### Transport fault message safety

Transport faults produce TK-constant-messaged failures; raw transport strings
(`err.details`, `err.message` — broker URIs, host detail, untranslated transport
context) never reach the client. The user-facing `messages[]` array is always
a TK constant (`TK.common.errors.SERVICE_UNAVAILABLE` / `CANCELED` /
`UNAUTHORIZED` / `UNKNOWN`).

## Trust + token model

- **Audience**: `d2.edge` (literal string constant — Edge owns the
  validation; the BFF treats the audience as opaque). NOT a spec entry
  in `audiences.spec.json` (which enumerates BACKEND audiences only).
- **TTL**: 15-min (matches the platform's standard JWT TTL).
- **Caching**: BFF module-singleton — one cached token per Node process;
  thread-safety is JS event-loop's single-threaded property.
- **Refresh**: proactive refresh-ahead + reactive on-401. `InternalTokenCache`
  distinguishes three token states:
  - **Fresh** (`now < expiresAtMs − refreshLeadMs`, default 60 s): served
    with no refresh signal.
  - **Aging** (`expiresAtMs − refreshLeadMs ≤ now < expiresAtMs − skewMs`):
    still valid — served immediately AND the interceptor fires a
    **fire-and-forget background re-mint** so the next call finds a fresh
    token. Errors in the background mint are silently swallowed (the next
    call mints synchronously if needed).
  - **Expired** (`now ≥ expiresAtMs − skewMs`, default 5 s): cache miss →
    caller mints synchronously.
  On `UNAUTHENTICATED` response the interceptor CLEARS the cache so the NEXT
  call re-acquires fresh; the `@grpc/grpc-js` interceptor SPI does not
  support truly re-issuing the same call from inside the interceptor — the
  retry layer (e.g. `@d2/resilience`'s `RetryHelper`) is responsible for the
  second attempt.
- **Singleflight dedup**: 100 concurrent gRPC calls all triggering a token
  acquire result in ONE upstream OAuth call thanks to `@d2/resilience`'s
  `Singleflight<TKey, TValue>` (inside `HttpInternalTokenClient`).

## Channel model

- **Singleton-per-process** — mirrors .NET `AddGrpcClient<T>()` (single
  channel cached for the lifetime of the process).
- **Lazy init**: created on first `getChannel()` call.
- **Concurrent first-call dedup**: via a module-level Promise — the Nth
  caller awaits the same Promise as the 1st.
- **Endpoint resolution**: `opts.endpoint` first, then
  `D2_EDGE_GRPC_ENDPOINT` env var. Throws if neither resolves.
- **TLS-by-default** — Edge's `aud=d2.edge` JWT validation requires
  TLS to ship credentials; tests pass `insecure: true` to point at
  in-process gRPC fixtures.

## Channel options

| Option                            | Value | Source                                                  |
| --------------------------------- | ----- | ------------------------------------------------------- |
| `grpc.max_send_message_length`    | 4 MB  | gRPC default (explicit pin so future change is visible) |
| `grpc.max_receive_message_length` | 4 MB  | same                                                    |
| `grpc.keepalive_time_ms`          | 10 s  | matches platform retry defaults from `@d2/resilience`   |
| `grpc.keepalive_timeout_ms`       | 5 s   | gRPC HTTP/2 idle-tolerance defaults                     |

## Security: PII redaction in interceptor logs

Token bytes never reach Pino. Diagnostic logs only record metadata
SHAPE — `{ method, hasContext, hasTraceparent, hasTracestate }` — never
values. The `HttpInternalTokenClient` logs `{ httpStatus, endpoint, errorName }`
on failure paths; the actual response body never lands in any
log binding.

## Dependencies

- `@d2/auth-abstractions` — `AuthFailures.jwksUnavailable` factory for
  token-endpoint-unreachable failures.
- `@d2/error-category` — `ErrorCategory` union + `ALL_ERROR_CATEGORIES` for
  safe category parse on `d2ResultFromProto`.
- `@d2/headers-common` — `PROPAGATED_CONTEXT` / `TRACEPARENT` /
  `TRACESTATE` metadata keys (gRPC-applicable wire constants live in
  `@d2/headers-common` and `@d2/headers-http`; the dedicated
  `@d2/headers-grpc` package contains the same cross-transport entries
  inline and is not needed at this layer).
- `@d2/headers-http` — `AUTHORIZATION` constant for the internal-token
  attach.
- `@d2/i18n-abstractions` — `tk(key, params?)` factory used in
  `d2ResultFromProto` to reconstruct `TKMessage` from proto fields.
- `@d2/i18n-keys` — `TK.*` constants for transport-fault messages in
  `handleGrpcCall` (no raw error strings ever enter user-facing messages).
- `@d2/logging` — `ILogger` interface for redaction-respecting diagnostic
  logs.
- `@d2/protos` — `D2ResultProto` / `TKMessageProto` / `InputErrorProto`
  generated stubs (transitively pulls `@grpc/grpc-js@1.14.3`).
- `@d2/request-context-abstractions` — `IPropagatedContext` shape +
  `PropagatedContextSerializer.serialize()`.
- `@d2/resilience` — `Singleflight` for concurrent token-refresh dedup.
- `@d2/result` — `D2Result` + semantic factory functions.
- `@d2/utilities` — `falsey()` for input shape checks; `truthyOrUndefined()`
  for proto optional-string rehydration.
- `@grpc/grpc-js@1.14.3` — runtime gRPC implementation; pinned.

## Usage

```ts
import {
  getChannel,
  closeChannel,
  createInternalTokenInterceptor,
  createContextPropagationInterceptor,
  HttpInternalTokenClient,
  InternalTokenCache,
} from "@d2/grpc-client";

// One-time setup at composition root.
const cache = new InternalTokenCache();
const tokenClient = new HttpInternalTokenClient({
  tokenEndpoint: process.env.D2_TOKEN_ENDPOINT!,
  clientId: process.env.D2_BFF_CLIENT_ID!,
  clientSecret: process.env.D2_BFF_CLIENT_SECRET!,
});

const interceptors = [
  createInternalTokenInterceptor({ cache, tokenClient }),
  createContextPropagationInterceptor({
    getCurrentContext: () => /* read from AsyncLocalStorage */ undefined,
    getCurrentTraceparent: () => /* read from OTel context */ undefined,
  }),
];

// Per-call.
const channel = await getChannel();
const client = new EdgeServiceClient(channel.getTarget(), undefined, {
  channelOverride: channel,
  interceptors,
});

// On shutdown.
process.on("SIGTERM", async () => {
  await closeChannel();
});
```

## Edge cases

- Token endpoint unreachable / 5xx / malformed response → `acquireToken`
  returns `D2Result.serviceUnavailable()` via `AuthFailures.jwksUnavailable`.
- Concurrent calls all triggering the same acquire share ONE upstream
  fetch via Singleflight (inside `HttpInternalTokenClient`).
- Tokens in the aging window trigger a fire-and-forget background re-mint;
  a failed background refresh is silently swallowed — the next call mints
  synchronously.
- `getChannel()` with no env var + no opts.endpoint throws with a
  diagnostic message.
- `closeChannel()` is idempotent — safe before any `getChannel()`,
  safe twice in a row.
- Interceptor logs SHAPE not VALUES — JWT bytes never reach Pino.
- The `@grpc/grpc-js` interceptor SPI does NOT support re-issuing a
  call from inside an interceptor; refresh-on-401 clears the cache so
  the NEXT call (e.g. via `RetryHelper`) acquires fresh.
- Background refresh-ahead errors are silently caught — never an
  unhandled promise rejection; a debug/warn log is emitted with
  `{ errorName }` only (no token bytes).

## Tests

Adversarial coverage per platform discipline — every public function has
happy-path + every-failure-branch (token endpoint 5xx / 4xx / non-JSON
/ shape-violating / oversized / network-throw). Per-VALUE pin tests
on metadata keys for cross-language parity. 100/100/100/100 coverage
threshold.
