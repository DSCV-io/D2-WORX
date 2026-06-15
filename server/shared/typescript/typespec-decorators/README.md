<!--
  Copyright (c) DCSV. All rights reserved.
-->

# @d2/typespec-decorators

TypeSpec decorator library defining the `@d2*` vocabulary for the D2 Operation Contract IDL.
Authors apply these decorators to TypeSpec `op` and `model` definitions; emitters read the
stored values from the program state map to generate service handlers, gRPC bindings,
Edge routing config, and structured-log redaction markers.

## Public API

| Decorator | Target | Args | State key |
|---|---|---|---|
| `@d2RequireAnyScope` | `op` | `...scopes: string[]` | `D2_REQUIRE_ANY_SCOPE_KEY` |
| `@d2RequireAllScopes` | `op` | `...scopes: string[]` | `D2_REQUIRE_ALL_SCOPES_KEY` |
| `@d2RateLimitTier` | `op` | `tier: string` | `D2_RATE_LIMIT_TIER_KEY` |
| `@d2Audience` | `op` | `audience: string` | `D2_AUDIENCE_KEY` |
| `@d2ServedBy` | `op` | `owner: string` | `D2_SERVED_BY_KEY` |
| `@d2GrpcMethod` | `op` | `service: string, method: string, streaming?: string` | `D2_GRPC_METHOD_KEY` |
| `@d2Redact` | `ModelProperty` | _(none)_ | `D2_REDACT_KEY` |
| `@d2ServerPush` | `op` | `pushTarget: string` | `D2_SERVER_PUSH_KEY` |
| `@d2Idempotent` | `op` | `keySource: string, ttlSeconds: number, ...fields: string[]` | `D2_IDEMPOTENT_KEY` |
| `@d2Resilience` | `op` | `pipeline: string` | `D2_RESILIENCE_KEY` |
| `@d2Csrf` | `op` | `posture: string` | `D2_CSRF_KEY` |
| `@d2Harmless` | `op` | _(none)_ | `D2_HARMLESS_KEY` |

### Scope decorators

`@d2RequireAnyScope` enforces an any-match guard (`ScopeMatch.Any` — at-least-one scope must
be present on the token). `@d2RequireAllScopes` enforces an all-match guard (`ScopeMatch.All` —
every listed scope must be present). Both accept a variadic list of scope strings:

```typespec
@d2RequireAnyScope("orders:read", "orders:admin")
op listOrders(): void;

@d2RequireAllScopes("payments:read", "payments:write")
op processPayment(): void;
```

Emitters read back the stored `string[]`:
```ts
import { D2_REQUIRE_ANY_SCOPE_KEY } from "@d2/typespec-decorators";
const scopes = program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).get(op); // string[]
```

### gRPC streaming modes

`@d2GrpcMethod` accepts an optional `streaming` arg selecting the proto `rpc` form.
Valid values: `unary | serverStream | clientStream | bidiStream`. Omit to default to `unary`.

```typespec
@d2GrpcMethod("Events", "StreamEvents", "serverStream")
op streamEvents(): void;
```

Emitters read back `{ service, method, streaming }`:
```ts
import { D2_GRPC_METHOD_KEY } from "@d2/typespec-decorators";
const payload = program.stateMap(D2_GRPC_METHOD_KEY).get(op);
// { service: "Events", method: "StreamEvents", streaming: "serverStream" }
```

### Idempotency

`@d2Idempotent` accepts a `keySource` (`"header"` reads the idempotency key from a request
header; `"derived"` hashes the listed input-property names), a `ttlSeconds` replay window,
and an optional variadic list of field names (only used when `keySource` is `"derived"`).

```typespec
@d2Idempotent("header", 300)
op createOrder(): void;

@d2Idempotent("derived", 600, "orgId", "userId")
op createPayment(): void;
```

Emitters read back `{ keySource, ttlSeconds, fields }`:

```ts
import { D2_IDEMPOTENT_KEY } from "@d2/typespec-decorators";
import type { IdempotentPayload } from "@d2/typespec-decorators";
const payload = program.stateMap(D2_IDEMPOTENT_KEY).get(op) as IdempotentPayload;
// { keySource: "derived", ttlSeconds: 600, fields: ["orgId", "userId"] }
```

### Resilience pipeline

`@d2Resilience` accepts a single composable pipeline-expression DSL string. The expression is
stored raw; the emitter is responsible for parsing it and constructing the policy graph. Both
bare-defaults and inline-tunables forms are supported:

```typespec
// bare-defaults: every policy uses its built-in defaults
@d2Resilience("retry(circuitBreaker(singleflight()))")
op callExternalService(): void;

// inline-tunables: override specific policy parameters inline
@d2Resilience("retry(3, circuitBreaker(threshold: 5))")
op callCriticalService(): void;
```

Emitters read back the raw string:

```ts
import { D2_RESILIENCE_KEY } from "@d2/typespec-decorators";
const pipeline = program.stateMap(D2_RESILIENCE_KEY).get(op); // string
```

### How `tspMain` / `main` split works

- **`lib/main.tsp`** (via `tspMain`) — declares `extern dec` under `namespace D2`; imported by
  TypeSpec consumers. Imports `dist/tsp-index.js` to register the JS implementations.
- **`dist/index.js`** (via `main`) — emitter-facing barrel: re-exports state-key symbols,
  `GrpcMethodPayload` type, `$lib`, and `$decorators`.

## Build

```bash
pnpm --filter @d2/typespec-decorators build   # tsc -b → dist/
pnpm --filter @d2/typespec-decorators test    # vitest run (requires dist/ from build)
```

## Dependencies

Peer: `@typespec/compiler ^1.13.0`

Dev: `@typespec/compiler 1.13.0`, `@typespec/http 1.13.0`, `typescript 5.9.3`, `vitest 4.0.18`

No telemetry. No runtime configuration.
