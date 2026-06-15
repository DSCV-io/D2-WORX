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
