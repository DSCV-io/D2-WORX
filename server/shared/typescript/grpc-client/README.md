<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/grpc-client

Singleton-per-process gRPC channel from the SvelteKit BFF to Edge, with
two interceptors:

- **Internal-token interceptor** — KeyCustodian-issued JWT (audience
  `d2.edge`, 15-min TTL), BFF module-singleton cache, refresh-on-401
  cache invalidation.
- **Context-propagation interceptor** — serializes the current request's
  `IPropagatedContext` into the `x-d2-context` gRPC metadata key + forwards
  `traceparent` / `tracestate` for W3C tracing.

Mirrors the .NET `services.AddGrpcClient<T>()` registration shape +
`D2.Shared.Auth.Outbound.ServiceIdentity.IServiceIdentityClient` semantics.

## Public API

| Export                                      | Purpose                                                                                       |
| ------------------------------------------- | --------------------------------------------------------------------------------------------- |
| `getChannel(opts?)`                         | Singleton-per-process channel accessor; lazy-init; concurrent dedup.                          |
| `closeChannel()`                            | Idempotent shutdown; safe to call from any number of process-shutdown hooks.                  |
| `createInternalTokenInterceptor(opts)`      | Returns a gRPC `Interceptor` that attaches `Authorization: Bearer <jwt>` on every call.       |
| `createContextPropagationInterceptor(opts)` | Returns an `Interceptor` that injects `x-d2-context` + forwards `traceparent` / `tracestate`. |
| `InternalTokenCache`                        | Single-slot atomic-style cache for the BFF's internal token.                                  |
| `KeyCustodianClient` (interface)            | Pluggable contract for token-acquire backends. Production wires `HttpKeyCustodianClient`.     |
| `HttpKeyCustodianClient`                    | Node-native `fetch()`-based KeyCustodian client; Singleflight-deduped.                        |

## Trust + token model

- **Audience**: `d2.edge` (literal string constant — Edge owns the
  validation; the BFF treats the audience as opaque). NOT a spec entry
  in `audiences.spec.json` (which enumerates BACKEND audiences only).
- **TTL**: 15-min (matches the platform's standard JWT TTL).
- **Caching**: BFF module-singleton — one cached token per Node process;
  thread-safety is JS event-loop's single-threaded property.
- **Refresh**: lazy-only. On `UNAUTHENTICATED` response the interceptor
  CLEARS the cache so the NEXT call re-acquires fresh; the
  `@grpc/grpc-js` interceptor SPI does not support truly re-issuing the
  same call from inside the interceptor — the retry layer
  (e.g. `@d2/resilience`'s `RetryHelper`) is responsible for the
  second attempt.
- **Singleflight dedup**: 100 concurrent gRPC calls all triggering
  KeyCustodian acquire result in ONE upstream call thanks to
  `@d2/resilience`'s `Singleflight<TKey, TValue>`.

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
values. The KeyCustodian client logs `{ httpStatus, endpoint, errorName }`
on failure paths; the actual response body never lands in any
log binding.

## Dependencies

- `@d2/auth-abstractions` — `AuthFailures.jwksUnavailable` factory for
  KeyCustodian-unreachable failures.
- `@d2/headers-common` — `PROPAGATED_CONTEXT` / `TRACEPARENT` /
  `TRACESTATE` metadata keys (gRPC-applicable wire constants live in
  `@d2/headers-common` and `@d2/headers-http`; the dedicated
  `@d2/headers-grpc` package contains the same cross-transport entries
  inline and is not needed at this layer).
- `@d2/headers-http` — `AUTHORIZATION` constant for the internal-token
  attach.
- `@d2/logging` — `ILogger` interface for redaction-respecting diagnostic
  logs.
- `@d2/protos` — gRPC stubs (transitively pulls `@grpc/grpc-js@1.14.3`).
- `@d2/request-context-abstractions` — `IPropagatedContext` shape +
  `PropagatedContextSerializer.serialize()`.
- `@d2/resilience` — `Singleflight` for concurrent token-refresh dedup.
- `@d2/result` — `D2Result` envelope.
- `@d2/utilities` — `falsey()` for input shape checks.
- `@grpc/grpc-js@1.14.3` — runtime gRPC implementation; pinned.

## Usage

```ts
import {
  getChannel,
  closeChannel,
  createInternalTokenInterceptor,
  createContextPropagationInterceptor,
  HttpKeyCustodianClient,
  InternalTokenCache,
} from "@d2/grpc-client";

// One-time setup at composition root.
const cache = new InternalTokenCache();
const keyCustodian = new HttpKeyCustodianClient({
  tokenEndpoint: process.env.D2_KEY_CUSTODIAN_TOKEN_ENDPOINT!,
  clientId: process.env.D2_BFF_CLIENT_ID!,
  clientSecret: process.env.D2_BFF_CLIENT_SECRET!,
});

const interceptors = [
  createInternalTokenInterceptor({ cache, keyCustodian }),
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

- KeyCustodian unreachable / 5xx / malformed response → `acquireToken`
  returns `D2Result.serviceUnavailable()` via `AuthFailures.jwksUnavailable`.
- Concurrent calls all triggering the same acquire share ONE upstream
  fetch via Singleflight.
- `getChannel()` with no env var + no opts.endpoint throws with a
  diagnostic message.
- `closeChannel()` is idempotent — safe before any `getChannel()`,
  safe twice in a row.
- Interceptor logs SHAPE not VALUES — JWT bytes never reach Pino.
- The `@grpc/grpc-js` interceptor SPI does NOT support re-issuing a
  call from inside an interceptor; refresh-on-401 clears the cache so
  the NEXT call (e.g. via `RetryHelper`) acquires fresh.

## Tests

Adversarial coverage per platform discipline — every public function has
happy-path + every-failure-branch (KeyCustodian 5xx / 4xx / non-JSON
/ shape-violating / oversized / network-throw). Per-VALUE pin tests
on metadata keys for cross-language parity. 100/100/100/100 coverage
threshold.
