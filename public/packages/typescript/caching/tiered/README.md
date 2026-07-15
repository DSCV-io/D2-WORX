<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/caching-tiered

> Parent: [`../README.md`](../README.md) · .NET mirror: `D2.Shared.Caching.Tiered`

Node/BFF authors who need a composed L1+L2 `ITieredCache` inject this pure-composition implementation — reads check the in-process L1 first and fall through to the distributed L2, writes go L2-first so partial-write states are impossible, atomics route through L2 with L1 side-effects, and an optional invalidation backplane keeps every instance's L1 coherent under the universal everyone-acts rule. Every operation returns `@d2/result` shapes.

## Usage

```ts
import { DefaultLocalCache } from "@d2/caching-local-default";
import {
  connectRedis,
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
} from "@d2/caching-distributed-redis";
import { DefaultTieredCache } from "@d2/caching-tiered";
import type { ILogger } from "@d2/logging";

const logger = /* host ILogger */ undefined as unknown as ILogger;
const l1 = new DefaultLocalCache({ keyPrefix: "bff:" });
const options = createRedisCacheOptions({
  connectionString: process.env.REDIS_URL, // SECRET when credentials embedded — never log
  keyPrefix: "app:",
});
const redis = connectRedis(options);
const serializer = new JsonCacheSerializer();
const backplane = new RedisCacheInvalidationBackplane(redis, options, logger);
await backplane.ready; // channel SUBSCRIBE established (owned by redis package)
const l2 = new RedisDistributedCache({
  redis,
  options,
  serializer,
  logger,
  // L2 backplane optional; tiered holds its own for *AndBroadcast*
});
const cache = new DefaultTieredCache({ l1, l2, logger, backplane });

await cache.set("user:1", { displayName: "Ada" });
const hit = await cache.get<{ displayName: string }>("user:1");
await cache.setAndBroadcast("user:1", { displayName: "Ada Lovelace" });

await cache[Symbol.asyncDispose](); // unsubscribes only; host still owns l1/l2/backplane/redis
```

## Construction

```ts
new DefaultTieredCache({
  l1: ILocalCache,
  l2: IDistributedCache,
  logger: ILogger,
  backplane?: ICacheInvalidationBackplane,
});
```

Required `l1`, `l2`, and `logger`. Optional `backplane` enables `*AndBroadcast*` and cluster-wide L1 invalidation subscribe. Construction is synchronous. When the backplane is a Redis implementation, await that backplane's `ready` Promise before delivery-dependent work — this package does not own channel subscribe.

The tiered cache does not own or dispose `l1`, `l2`, or `backplane`. Disposing the tiered cache only unsubscribes its backplane handler. Construct once at the composition root and share the instance.

## Public surface

- **`DefaultTieredCache`** — implements `ITieredCache` (`ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast`) and `AsyncDisposable`.
- **`DefaultTieredCacheDeps`** — constructor dependency bag.
- **`BACKPLANE_NOT_REGISTERED_MESSAGE`** — pinned registration-error text thrown by `*AndBroadcast*` when no backplane was passed.
- **`TieredCacheOp`**, **`TIERED_ERROR_CODE_UNKNOWN`**, **`TieredCacheOpName`** — public twin-pin closed-set op names + errorCode sentinel for dual-runtime ContractFixtures parity.

**Basic:** `get`, `getMany`, `exists`, `getTtl`, `set`, `setMany`, `remove`, `removeMany`.

**Atomic:** `setNx`, `increment`, `acquireLock`, `releaseLock`.

**Broadcast:** `setAndBroadcast`, `setManyAndBroadcast`, `removeAndBroadcast`, `removeManyAndBroadcast`.

This package does **not** implement `ICacheSet` (SADD/SCARD). Callers that need set primitives inject `IDistributedCache` directly.

## Behavior

**Reads** (`get` / `getMany` / `exists`):

- Try L1 first.
- On L1 miss, fall through to L2.
- On L2 hit, populate L1 with the value (L1 default TTL; L2 remains the cluster freshness authority).
- Return the result.
- `exists`: L1 `true` short-circuits; otherwise query L2.
- `getTtl`: L2 only (cluster source of truth).

**Writes** (`set` / `setMany` / `remove` / `removeMany`):

- L2 first — if L2 fails, return that failure and do not touch L1.
- L1 second — only after L2 succeeded. The same `value` / `entries` / `expirationMs` / `signal` passed to L2 are passed to L1 on success.
- If L1 fails after L2 succeeded, the L2-success result is returned. The L1 failure is logged at Warning and swallowed. L1 is the optional layer: an L1 failure on this instance must not fail a write the cluster accepted. The next read on this instance re-fetches from L2.

**Atomic primitives** (`setNx` / `increment` / `acquireLock` / `releaseLock`):

- Route through L2 (cluster source of truth) with full port arity (`value` / `amount` / `expirationMs` / `signal` as applicable).
- After `setNx` succeeds: if L2 took the write, populate L1 with the same `value` and `expirationMs`; if the key already existed, drop L1.
- After `increment` succeeds: always drop L1 (counters held in L1 would diverge).
- Locks: pure L2 delegation — L1 is not involved. `releaseLock` is idempotent on ownership miss; a store-down failure surfaces as `serviceUnavailable` from L2.

**Broadcast** (`setAndBroadcast` and siblings):

- Perform the tiered write first; if the write fails, return that result and do **not** publish (all four ops).
- On write success, publish via the injected `ICacheInvalidationBackplane`. Publish failure returns the backplane `D2Result` as-is.
- All subscribers (every connected instance, including this one) receive the key and drop their L1 entry — everyone acts.
- Missing backplane throws a plain `Error` with `BACKPLANE_NOT_REGISTERED_MESSAGE` (registration error, not a `D2Result`).

## Backplane subscription

When a backplane is passed at construction, the tiered cache subscribes in the constructor. Every received invalidation key triggers `remove` on L1. If that L1 remove fails, the failure is logged at Warning and processing continues; L1 stays stale on this instance until TTL or the next write/broadcast for that key. Disposing the tiered cache disposes the subscription only.

Without a backplane, plain ops still work; L1 caches drift across instances until their TTLs expire. `*AndBroadcast*` throws.

## Design rationale: no ICacheSet

`ITieredCache` deliberately does not implement `ICacheSet`. Set cardinality is cluster-only — there is no honest way to compose it across L1+L2 (an L1 set would only see this instance's adds; cluster cardinality lives in L2). Callers needing set primitives inject `IDistributedCache` directly.

## Logging

Structured Warning logs only (see also **Telemetry**):

| When | Message | Bindings |
| ---- | ------- | -------- |
| L1 remove fails inside the invalidation handler | `Tiered cache L1 invalidation handler failed.` | `key`, `errorCode` |
| L1 write/remove fails after L2 succeeded | `Tiered cache L1 write failed after L2 success.` | `operation`, `keyOrCount`, `errorCode` |

`operation` is one of `set` / `setMany` / `remove` / `removeMany`. `errorCode` comes from the L1 `D2Result` (`"unknown"` when absent). Bindings never include exception messages. Keep PII out of cache keys (see abstractions).

## Telemetry

This package publishes **no OTel meters**. L1 and L2 packages own their meters. Observability for this package is structured logging only — see **Logging**.

## Result mapping

Per-call validation and store failures surface from L1/L2 (`notFound`, `someFound`, `validationFailed` via `InputFailures`, `serviceUnavailable`, `conflict` on increment type mismatch). Tiered composition adds:

| Path | Outcome |
| ---- | ------- |
| L2 write fails | Return L2 result; L1 untouched |
| L1 fails after L2 write ok | Return L2 success; Warning log |
| L2 hard fail on `getMany` missing keys (`!success && !isPartialSuccess`) | Propagate L2 even if L1 had partial hits |
| L2 `someFound` on missing keys | Merge with L1 hits; `ok(merged)` or `someFound({ data: merged })` |
| L2 `notFound` on missing keys | `someFound({ data: l1Hits })` or `notFound()` |
| `*AndBroadcast*` write fails | Return write result; no publish |
| `*AndBroadcast*` publish fails | Return backplane result |
| `*AndBroadcast*` without backplane | Throws `Error` (`BACKPLANE_NOT_REGISTERED_MESSAGE`) |

## Disposal

`[Symbol.asyncDispose]` unsubscribes the backplane handler and is idempotent. It does not dispose L1, L2, or the backplane. Live operations remain callable after dispose (subscription already torn down).

## Divergences from the .NET implementation

- Construction uses a deps object instead of MS.DI `AddD2TieredCache()`; hosts compose L1/L2/logger/backplane explicitly.
- Durations on L1/L2 use millisecond numbers (`expirationMs`), not `TimeSpan`.
- Registration errors throw a plain `Error` with a pinned message, not `InvalidOperationException`.
- When the backplane is Redis-backed, channel-subscribe readiness is owned by `@d2/caching-distributed-redis` (`ready` Promise); this package keeps a sync constructor and a sync port `subscribe`.
- Counter / lock numeric width follows the TS port (`number` within `Number.MAX_SAFE_INTEGER`).
- Logging uses short structured redis-style Warning **message strings** + camelCase binding fields; dual-runtime parity is EventId **meanings** (L1 invalidation fail / L1 write fail after L2 ok), Warning-only, and `errorCode` presence — **not** LoggerMessage template byte-equality or .NET `"SetAsync"` / `"SetManyAsync"` operation-name strings (TS uses `"set"` / `"setMany"` / `"remove"` / `"removeMany"`).
- Runtime dependencies are abstractions + result + logging only (no `@d2/utilities`; .NET tiered likewise has no Utilities package dependency).

## Dependencies

| Package | Role |
| ------- | ---- |
| `@d2/caching-abstractions` | Ports (`ITieredCache`, `ILocalCache`, `IDistributedCache`, `ICacheInvalidationBackplane`) |
| `@d2/result` | Result factories |
| `@d2/logging` | `ILogger` |

No backing-store packages at runtime — this package is pure composition. No `@d2/utilities` (validation/falsey live in L1/L2). Typical hosts also depend on `@d2/caching-local-default` and `@d2/caching-distributed-redis` for L1/L2/backplane implementations.

## References

- [`../abstractions/README.md`](../abstractions/README.md) — ports + result mapping
- [`../local-default/README.md`](../local-default/README.md) — typical L1
- [`../distributed-redis/README.md`](../distributed-redis/README.md) — typical L2 + backplane
- [`../README.md`](../README.md) — caching cluster
- [`PATTERNS.md`](../../../../../docs/PATTERNS.md) cache section
- .NET twin: `public/packages/dotnet/caching/tiered/`
