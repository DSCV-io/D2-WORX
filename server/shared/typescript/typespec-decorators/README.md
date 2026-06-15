<!--
  Copyright (c) DCSV. All rights reserved.
-->

# @d2/typespec-decorators

TypeSpec decorator library defining the `@d2*` vocabulary for the D2 Operation Contract IDL.
Authors apply these decorators to TypeSpec `op` and `model` definitions; emitters read the
stored values from the program state map to generate service handlers, gRPC bindings,
Edge routing config, and structured-log redaction markers.

## Public API

| Decorator             | Target          | Args                                                         | State key                   |
| --------------------- | --------------- | ------------------------------------------------------------ | --------------------------- |
| `@d2RequireAnyScope`  | `op`            | `...scopes: string[]`                                        | `D2_REQUIRE_ANY_SCOPE_KEY`  |
| `@d2RequireAllScopes` | `op`            | `...scopes: string[]`                                        | `D2_REQUIRE_ALL_SCOPES_KEY` |
| `@d2RateLimitTier`    | `op`            | `tier: string`                                               | `D2_RATE_LIMIT_TIER_KEY`    |
| `@d2Audience`         | `op`            | `audience: string`                                           | `D2_AUDIENCE_KEY`           |
| `@d2ServedBy`         | `op`            | `owner: string`                                              | `D2_SERVED_BY_KEY`          |
| `@d2GrpcMethod`       | `op`            | `service: string, method: string, streaming?: string`        | `D2_GRPC_METHOD_KEY`        |
| `@d2Redact`           | `ModelProperty` | _(none)_                                                     | `D2_REDACT_KEY`             |
| `@d2ServerPush`       | `op`            | `pushTarget: string`                                         | `D2_SERVER_PUSH_KEY`        |
| `@d2Idempotent`       | `op`            | `keySource: string, ttlSeconds: number, ...fields: string[]` | `D2_IDEMPOTENT_KEY`         |
| `@d2Resilience`       | `op`            | `pipeline: string`                                           | `D2_RESILIENCE_KEY`         |
| `@d2Csrf`             | `op`            | `posture: string`                                            | `D2_CSRF_KEY`               |
| `@d2Harmless`         | `op`            | _(none)_                                                     | `D2_HARMLESS_KEY`           |
| `@d2InProcess`        | `op`            | _(none)_                                                     | `D2_IN_PROCESS_KEY`         |
| `@d2Command`          | `op`            | _(none)_                                                     | `D2_COMMAND_KEY`            |
| `@d2Query`            | `op`            | _(none)_                                                     | `D2_QUERY_KEY`              |
| `@d2Internal`         | `op`            | _(none)_                                                     | `D2_INTERNAL_KEY`           |

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
const payload = program
  .stateMap(D2_IDEMPOTENT_KEY)
  .get(op) as IdempotentPayload;
// { keySource: "derived", ttlSeconds: 600, fields: ["orgId", "userId"] }
```

### Resilience pipeline

`@d2Resilience` accepts a single composable pipeline-expression DSL string. The DSL is validated
at compile time; any invalid expression is an **error-severity diagnostic** that fails the build.
Both bare-defaults and inline-tunables forms are supported:

```typespec
// bare-defaults: every policy uses its built-in defaults
@d2Resilience("retry(circuitBreaker(singleflight()))")
op callExternalService(): void;

// inline-tunables: override specific policy parameters inline
@d2Resilience("retry(3, circuitBreaker(threshold: 5))")
op callCriticalService(): void;
```

Emitters read back the raw string or use `parse()` directly to walk the AST:

```ts
import { D2_RESILIENCE_KEY, parse } from "@d2/typespec-decorators";
const raw = program.stateMap(D2_RESILIENCE_KEY).get(op); // string
const result = parse(raw);
if (result.ok) {
  // result.root: ResiliencePolicyNode — policy / tunables / inner chain
}
```

## Validation

All 16 decorators carry build-time validation. Every diagnostic is **severity "error"** —
invalid configurations fail the TypeSpec compile rather than emitting a warning.

### Eager value-set checks (run in each `$fn` body)

| Code                                 | Decorator                                   | Trigger                                                                            |
| ------------------------------------ | ------------------------------------------- | ---------------------------------------------------------------------------------- |
| `invalid-rate-limit-tier`            | `@d2RateLimitTier`                          | tier not in `Standard \| Elevated \| Restricted`                                   |
| `invalid-grpc-streaming`             | `@d2GrpcMethod`                             | streaming not in `unary \| serverStream \| clientStream \| bidiStream`             |
| `invalid-push-target`                | `@d2ServerPush`                             | pushTarget not in `user \| session`                                                |
| `invalid-csrf-posture`               | `@d2Csrf`                                   | posture not in `required \| exempt`                                                |
| `invalid-idempotent-key-source`      | `@d2Idempotent`                             | keySource not in `header \| derived`                                               |
| `invalid-idempotent-ttl`             | `@d2Idempotent`                             | ttlSeconds ≤ 0                                                                     |
| `idempotent-derived-requires-fields` | `@d2Idempotent`                             | keySource "derived" with no field names                                            |
| `idempotent-header-forbids-fields`   | `@d2Idempotent`                             | keySource "header" with field names                                                |
| `unknown-scope`                      | `@d2RequireAnyScope`, `@d2RequireAllScopes` | scope not in `contracts/auth-scopes/scopes.spec.json`                              |
| `unknown-audience`                   | `@d2Audience`                               | audience not in `contracts/auth-audiences/audiences.spec.json` (and not `d2-edge`) |
| `empty-served-by`                    | `@d2ServedBy`                               | owner is empty or whitespace-only                                                  |

### `$onValidate` cross-decorator checks (run after all decorators apply)

| Code                           | Trigger                                                                                                           |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `rate-tier-requires-route`     | `@d2RateLimitTier` on an op with no `@route` — internal ops bypass Edge rate-limiting                             |
| `harmless-scope-conflict`      | `@d2Harmless` combined with `@d2RequireAnyScope` or `@d2RequireAllScopes` — auth-exempt ops cannot require scopes |
| `inprocess-requires-served-by` | `@d2InProcess` without `@d2ServedBy` — an in-process leaf needs a named owner to generate its interface           |
| `category-required`            | Operation declares neither `@d2Command` nor `@d2Query` — exactly one CQRS category is required on every op        |
| `category-exclusive`           | Operation declares both `@d2Command` and `@d2Query` — categories are mutually exclusive                           |
| `internal-op-exposed`          | `@d2Internal` combined with `@route`, `@d2GrpcMethod`, `@d2InProcess`, or `@d2ServerPush` — an internal op is not callable across any boundary |
| `exposure-or-internal-required`| Operation has no transport exposure (`@route` / `@d2GrpcMethod` / `@d2InProcess` / `@d2ServerPush`) and is not `@d2Internal` |

### CQRS category + internal marker

`@d2Command` marks a mutating CQRS operation (mutates persistent/shared state — DB write, distributed-cache write,
external write, message publish). `@d2Query` marks a read-only operation. Exactly one must appear on every operation;
the category drives the `Commands/` vs `Queries/` folder placement in generated handler scaffolding.

`@d2Internal` marks an operation as in-app-only — no cross-boundary surface is generated. An internal op may not
carry any exposure decorator (`@route`, `@d2GrpcMethod`, `@d2InProcess`, `@d2ServerPush`). Every operation must
either carry an exposure decorator or be marked `@d2Internal`.

```typespec
@d2Command
op createOrder(): void;   // mutating — goes in Commands/

@d2Query
op listOrders(): void;    // read-only — goes in Queries/

@d2Command
@d2Internal
op recomputeCache(): void; // in-app-only mutating op — no cross-boundary surface
```

Emitters read these as presence checks:

```ts
import { D2_COMMAND_KEY, D2_QUERY_KEY, D2_INTERNAL_KEY } from "@d2/typespec-decorators";
const isCommand  = program.stateMap(D2_COMMAND_KEY).has(op);   // → Commands/ folder
const isQuery    = program.stateMap(D2_QUERY_KEY).has(op);     // → Queries/ folder
const isInternal = program.stateMap(D2_INTERNAL_KEY).has(op);  // suppress cross-boundary surface
```

### `@d2Resilience` DSL parser

The DSL is parsed via a pure recursive-descent parser. Grammar:

```
expression   := policyCall
policyCall   := policyName "(" argList? ")"
policyName   := "retry" | "circuitBreaker" | "singleflight"
argList      := arg ("," arg)*
arg          := namedArg | positionalArg | policyCall
namedArg     := identifier ":" literal
positionalArg:= literal
literal      := number | duration | boolean
number       := [0-9]+
duration     := [0-9]+(ms|s)      (normalized: ms stays ms; s is converted per-tunable)
boolean      := "true" | "false"
```

Rules:

- A resilience pipeline is a **linear stack**: each policy wraps at most one inner policy.
- Positional arguments must come **before** named arguments.
- `singleflight` accepts **no** tunables (it is always a leaf with zero configuration).
- `retry` tunables: `maxAttempts` (int ≥ 1), `baseDelayMs`/`baseDelay` (duration-ms), `backoffMultiplier` (int ≥ 1), `maxDelayMs`/`maxDelay` (duration-ms), `jitter` (bool).
- `circuitBreaker` tunables: `failureThreshold`/`threshold` (int ≥ 1), `cooldownSeconds`/`cooldown` (duration-s).

DSL-specific diagnostic codes:

| Code                                | Trigger                                                               |
| ----------------------------------- | --------------------------------------------------------------------- |
| `resilience-malformed`              | Syntactically invalid expression (missing paren, empty string, etc.)  |
| `resilience-unknown-policy`         | Policy name not in `retry \| circuitBreaker \| singleflight`          |
| `resilience-unknown-arg`            | Tunable name not defined for the policy, or any arg on `singleflight` |
| `resilience-bad-arg`                | Tunable value has wrong type or is outside the allowed range          |
| `resilience-multiple-inner`         | More than one nested policy call in a single policy's arg list        |
| `resilience-positional-after-named` | Positional argument appears after a named argument                    |

### How `tspMain` / `main` split works

- **`lib/main.tsp`** (via `tspMain`) — declares `extern dec` under `namespace D2`; imported by
  TypeSpec consumers. Imports `dist/tsp-index.js` to register the JS implementations.
- **`dist/index.js`** (via `main`) — emitter-facing barrel: re-exports state-key symbols
  (`D2_REQUIRE_ANY_SCOPE_KEY`, `D2_REQUIRE_ALL_SCOPES_KEY`, `D2_RATE_LIMIT_TIER_KEY`,
  `D2_AUDIENCE_KEY`, `D2_SERVED_BY_KEY`, `D2_GRPC_METHOD_KEY`, `D2_REDACT_KEY`,
  `D2_SERVER_PUSH_KEY`, `D2_IDEMPOTENT_KEY`, `D2_RESILIENCE_KEY`, `D2_CSRF_KEY`,
  `D2_HARMLESS_KEY`, `D2_IN_PROCESS_KEY`, `D2_COMMAND_KEY`, `D2_QUERY_KEY`, `D2_INTERNAL_KEY`),
  payload types (`GrpcMethodPayload`, `IdempotentPayload`), the resilience parser (`parse`,
  `ResiliencePolicyNode`, `ResilienceParseResult`, `ResilienceParseError`,
  `ResilienceDiagnosticCode`), `$lib`, and `$decorators`.
- **`$onValidate`** is exported from `dist/tsp-index.js` (the module `lib/main.tsp` imports);
  the TypeSpec compiler discovers and runs it after all decorators have applied.

## Emitter notes

These notes cover contracts between the AST/state-map layer and the emitter fleet.
Emitter authors must read this section before generating code from any `@d2*` decorator.

### Sparse tunables — absent key means "use library default"

`ResiliencePolicyNode.tunables` is a **sparse** record: only explicitly-provided tunable keys
are present. An absent key means "use the C# library default" — the emitter must omit that
property from the generated `RetryOptions` / `CircuitBreakerOptions` constructor, not emit a
zero or false value.

Example: `retry()` → `tunables: {}` (bare defaults); `retry(3)` → `tunables: { maxAttempts: 3 }`.
Emitting `MaxAttempts = 0` for an absent `maxAttempts` would override the library default of 5
and disable retries.

### `backoffMultiplier` — DSL is integer, C# type is double

The DSL restricts `backoffMultiplier` to integer tokens (the grammar excludes decimals). The C#
`RetryOptions.BackoffMultiplier` property is `double` (library default `2.0`). When the emitter
emits an explicit `backoffMultiplier` value, it must widen to double (e.g. emit `2` as
`BackoffMultiplier = 2` — C# performs the implicit int→double conversion, which is correct).
Non-integer multipliers (e.g. `1.5`) cannot be expressed in the DSL; the library default covers
the common exponential back-off case.

### Policy → DI wiring is the emitter's concern

The AST carries policy identity (`retry` / `circuitBreaker` / `singleflight`), tunables, and the
linear nesting order via `inner`. How the emitter keys and wires the DI services for
`CircuitBreaker<TValue>` and `Singleflight<TKey, TValue>` (both require keyed DI registration)
is an emitter-fleet responsibility, not encoded in the AST. The emitter must derive a service key
(e.g. from the operation name) and register the instance via `services.AddKeyedSingleton<...>`.

### Per-decorator read pattern

| Decorator | Emitter read pattern | Notes |
|---|---|---|
| `@d2RequireAnyScope` | `program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).get(op) as string[]` | Any-match guard |
| `@d2RequireAllScopes` | `program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).get(op) as string[]` | All-match guard |
| `@d2RateLimitTier` | `program.stateMap(D2_RATE_LIMIT_TIER_KEY).get(op) as string` | Validated value in `Standard \| Elevated \| Restricted` |
| `@d2Audience` | `program.stateMap(D2_AUDIENCE_KEY).get(op) as string` | Validated against spec at compile time |
| `@d2ServedBy` | `program.stateMap(D2_SERVED_BY_KEY).get(op) as string` | Capability name; transport (leaf vs gRPC) is deployment-resolved |
| `@d2GrpcMethod` | `program.stateMap(D2_GRPC_METHOD_KEY).get(op) as GrpcMethodPayload` | `{ service, method, streaming }` |
| `@d2Redact` | `program.stateMap(D2_REDACT_KEY).has(prop)` | Presence check on `ModelProperty` |
| `@d2ServerPush` | `program.stateMap(D2_SERVER_PUSH_KEY).get(op) as string` | `user \| session`; event-type is derived from the op name by the emitter |
| `@d2Idempotent` | `program.stateMap(D2_IDEMPOTENT_KEY).get(op) as IdempotentPayload` | `{ keySource, ttlSeconds, fields }` |
| `@d2Resilience` | call `parse(program.stateMap(D2_RESILIENCE_KEY).get(op))` | Re-parse the stored raw string via the exported `parse()` to get the AST |
| `@d2Csrf` | `program.stateMap(D2_CSRF_KEY).get(op) as string` | `required \| exempt` |
| `@d2Harmless` | `program.stateMap(D2_HARMLESS_KEY).has(op)` | Presence check; mutually exclusive with scope decorators (enforced at compile time) |
| `@d2InProcess` | `program.stateMap(D2_IN_PROCESS_KEY).has(op)` | Presence check; the explicit leaf-vs-gRPC trigger; requires `@d2ServedBy` (enforced at compile time) |
| `@d2Command`   | `program.stateMap(D2_COMMAND_KEY).has(op)` | Presence check; drives `Commands/` folder placement in generated handler scaffolding |
| `@d2Query`     | `program.stateMap(D2_QUERY_KEY).has(op)` | Presence check; drives `Queries/` folder placement in generated handler scaffolding |
| `@d2Internal`  | `program.stateMap(D2_INTERNAL_KEY).has(op)` | Presence check; when true, suppress all cross-boundary surface generation for this op |

`@d2Resilience` note: the decorator stores the validated raw DSL string. Emitters must call the
exported `parse()` function to obtain the `ResiliencePolicyNode` AST for code generation. The
`parse()` function is pure (no side effects) and safe to call at emit time.

## Build

```bash
pnpm --filter @d2/typespec-decorators build            # tsc -b → dist/
pnpm --filter @d2/typespec-decorators test             # vitest run (236 tests across decorators + resilience-dsl suites)
pnpm --filter @d2/typespec-decorators run test:coverage  # vitest run --coverage (100% threshold, requires dist/)
```

## Dependencies

Peer: `@typespec/compiler ^1.13.0`

Dev: `@typespec/compiler 1.13.0`, `@typespec/http 1.13.0`, `typescript 5.9.3`, `vitest 4.0.18`

No telemetry. No runtime configuration.
