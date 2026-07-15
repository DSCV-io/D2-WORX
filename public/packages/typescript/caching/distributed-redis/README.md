<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-caching-distributed-redis

> Parent: [`../README.md`](../README.md) · .NET mirror: `DcsvIo.D2.Caching.Distributed.Redis`

Node/BFF authors who need a cluster-scoped `IDistributedCache` inject this Redis implementation — Basic + Atomic + Broadcast + Set over Redis, a pub/sub invalidation backplane on channel `d2:cache:invalidations`, JSON serialization, aggregate OTel counters, and `@dcsv-io/d2-result` shapes on every operation.

## Usage

```ts
import {
  connectRedis,
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
} from "@dcsv-io/d2-caching-distributed-redis";
import type { ILogger } from "@dcsv-io/d2-logging";

const options = createRedisCacheOptions({
  connectionString: process.env.REDIS_URL, // SECRET when credentials embedded — never log
  keyPrefix: "app:",
});
const redis = connectRedis(options); // command client; host owns lifecycle
const logger = /* host ILogger */ undefined as unknown as ILogger;
const serializer = new JsonCacheSerializer();
const backplane = new RedisCacheInvalidationBackplane(redis, options, logger);
await backplane.ready; // channel SUBSCRIBE established (ioredis async setup; construction stays sync)
const cache = new RedisDistributedCache({
  redis,
  options,
  serializer,
  logger,
  backplane,
});

await cache.set("user:1", { displayName: "Ada" });
const hit = await cache.get<{ displayName: string }>("user:1");
await cache.setAndBroadcast("user:1", { displayName: "Ada Lovelace" });

// port subscribe(handler) is sync → AsyncDisposable — do not await subscribe itself
await using (backplane.subscribe(async (key) => {
  // everyone-acts handler — key is as published (prefixed when from cache broadcast)
})) {
  // subscription active
}
// backplane dispose quits the owned subscriber only; host still owns `redis`
await backplane[Symbol.asyncDispose]();
```

## Construction + options

| Field | Default | Notes |
| --- | --- | --- |
| `connectionString` | (none) | Required for `connectRedis` only. SECRET when credentials embedded — never log. |
| `defaultExpirationMs` | `3_600_000` | Applied when a write omits `expirationMs`. Values `<= 0` mean no default TTL. |
| `keyPrefix` | `""` | Prepended to store keys and broadcast payloads. |
| `invalidationChannel` | `d2:cache:invalidations` | Shared with .NET. |
| `commandTimeoutMs` | `2000` | Must be finite `> 0`. |
| `connectTimeoutMs` | `5000` | Must be finite `> 0`. |
| `connectRetries` | `3` | Non-negative safe integer. |
| `abortOnConnectFail` | `false` | When false, ops return `serviceUnavailable` until Redis is up. |

**Dual-connection:** host injects a **command** ioredis client; the backplane owns `commandRedis.duplicate()` as the subscriber. Publish uses the command client; channel subscribe uses the subscriber. After construct, **`await backplane.ready`** before delivery-dependent work. Dispose quits the **owned subscriber only**. `connectRedis` throws with fixed message `RedisCacheOptions.ConnectionString is required.` when the string is falsey — never interpolates input. Broken config throws at construction; live ops return results.

## Public surface

- `RedisDistributedCache` — full `IDistributedCache` (Basic + Atomic + Broadcast + Set)
- `RedisDistributedCacheDeps` — constructor dependency bag type
- `RedisCacheInvalidationBackplane` — `ready: Promise<void>`, sync port `subscribe(handler)`, publish, `AsyncDisposable`
- `JsonCacheSerializer` — default `ICacheSerializer`
- `REDIS_CACHE_METER_NAME`, `REDIS_CACHE_METER_VERSION`, `REDIS_CACHE_INSTRUMENTS`, `REDIS_CACHE_DEFAULTS`, `createRedisCacheOptions`, `RedisCacheOptions`
- `INCREMENT_WITH_OPTIONAL_TTL`, `RELEASE_LOCK_IF_OWNER`, `SET_ADD_WITH_OPTIONAL_TTL` — public twin-pin Lua body constants (ContractFixtures parity; not an executor API)
- `connectRedis` — builds a host-owned command client

## Result mapping

| Situation | Result |
| --- | --- |
| Miss | `notFound` |
| Partial bulk hit | `someFound` (206) |
| Redis / connection down | `serviceUnavailable` (including `releaseLock`) |
| Increment WRONGTYPE / non-integer | `conflict` |
| Increment amount not a safe integer / next not safe integer | validationFailed (`amount`) |
| Serializer fail (get / set / setMany / setNx / broadcast wrappers) | whole-op `bubbleFail` |
| `getMany` deserialize fail | **skips that entry** (no whole-op bubbleFail) |
| Broadcast without backplane | **throws** registration `Error` |
| `releaseLock` ownership miss | `ok` (idempotent) |
| Aborted `AbortSignal` at entry | `canceled` |

## Validation (JS number guards)

Construction (`RangeError` / fixed connect throw — not `D2Result`):

- `commandTimeoutMs` / `connectTimeoutMs` — finite `> 0`.
- `connectRetries` — non-negative safe integer.
- `connectionString` falsey on `connectRedis` — fixed message throw (never
  interpolates input).

Per-call (validationFailed via `InputFailures` unless noted):

- Falsey `key` / keys / `lockId` / set member as applicable.
- Optional `expirationMs` when present: finite and `> 0`.
- `increment` `amount` when present: must be a safe integer
  (`NaN` / ±`Infinity` / `0.5` / non-safe → field `amount`, invalid not
  NOT_NULL).
- `increment` result bound: Lua `INCREMENT_WITH_OPTIONAL_TTL` (byte-equal twin
  of .NET) refuses results outside ±9007199254740991 (`Number.MAX_SAFE_INTEGER`)
  by reversing `DECRBY` **in the same script** and returning
  `ERR safe_integer_overflow` → validationFailed field `amount`. No
  client-side race window; behavior matches .NET.
- `acquireLock` `expirationMs`: finite and `> 0` (required param; invalid value
  → invalid field error).

## TTL semantics

Default write TTL is 1 hour (`defaultExpirationMs`). Explicit `expirationMs` overrides when provided (must be finite `> 0`). `increment` and `setAdd` apply TTL **on create only** via Lua `PTTL < 0` gating. `getTtl`: absent → `notFound`; present no expiry → `ok(undefined)`; present with TTL → `ok(ms)`.

## Atomics + Lua

Three public twin-pin Lua script constants (not an executor API): INCRBY + safe-integer bound + optional PEXPIRE, compare-and-delete lock release, SADD + optional PEXPIRE. Bodies are byte-equivalent to .NET `RedisLuaScripts` and re-exported for dual-runtime ContractFixtures parity. Cluster-wide SET NX / INCR / setAdd atomicity is Redis-enforced.

## Backplane

Everyone acts (publisher receives own messages). At-most-once delivery. Multi-subscriber independence. Handler isolation (one throw does not break others). Dispose unsubscribes and quits the owned subscriber; publish after dispose does **not** throw. Default channel `d2:cache:invalidations` is shared with .NET. Construction is sync; channel readiness is `ready`. Port `subscribe(handler)` is **sync** (register handlers — never await the call itself).

## Key prefix

Applies to store keys **and** to keys published on `*AndBroadcast*` (prefixed payload).

## Telemetry

| Counter | Unit | Description |
| --- | --- | --- |
| `d2.cache.redis.hits` | `{hit}` | Redis cache hits. |
| `d2.cache.redis.misses` | `{miss}` | Redis cache misses. |
| `d2.cache.redis.sets` | `{write}` | Redis cache writes. |
| `d2.cache.redis.removes` | `{removal}` | Redis cache removals. |
| `d2.cache.redis.broadcasts` | `{broadcast}` | Invalidation messages published to backplane. |
| `d2.cache.redis.errors` | `{error}` | Redis-side failures. |

Meter name `DcsvIo.D2.Caching.Distributed.Redis` v`1.0.0` (`REDIS_CACHE_METER_NAME`). Construct the cache after host `setupTelemetry` so instruments bind to the real meter; without a MeterProvider counters are no-op-safe.

## Logging

`ILogger` is required. Redis-op failures log `{ operation, exceptionType, keyOrCount }`. Handler isolation logs `{ exceptionType, key }`. Never log exception messages or `connectionString`. Keep PII out of cache keys (see abstractions).

## Cancellation

An aborted signal at method entry returns `canceled` without touching Redis. There is no mid-flight cancellation guarantee on every ioredis command.

## Divergences from .NET

- Client library is ioredis (not StackExchange.Redis).
- `IConnectionMultiplexer` is not one ioredis TCP client — dual-connection (command + owned subscriber via `duplicate()`).
- ioredis channel `subscribe` is Promise-based; .NET StackExchange registers channel Subscribe synchronously. TS keeps sync `new` and surfaces completion via `readonly ready: Promise<void>`.
- Connection is injected (no MS.DI helper required beyond `connectRedis`).
- Durations are millisecond numbers, not `TimeSpan`.
- Counter / lock values are JS numbers within `Number.MAX_SAFE_INTEGER`;
  `increment` rejects non-safe-integer `amount` and non-safe-integer results
  (validationFailed field `amount`). Redis integer range is wider; callers must
  keep counters in the JS safe-integer band.
- `AbortSignal` is entry-level only.
- Broadcast registration and subscribe-after-dispose use plain `Error` (not BCL exception types).
- Cache type has no dispose (host owns the command client), matching .NET not disposing the multiplexer from the cache.
- Backplane dispose quits the owned subscriber (TS ownership of the duplicate).
- JSON wire uses `JSON.stringify` / `JSON.parse` with camelCase + cycle ignore twin of STJ Web; residual deltas (prototype chain, `undefined` omission, `Date` encoding) may differ.

## Dependencies

| Package | Role |
| --- | --- |
| `@dcsv-io/d2-caching-abstractions` | Ports (`IDistributedCache`, backplane, serializer) |
| `@dcsv-io/d2-result` | Result factories + `bubbleFail` + `fail` |
| `@dcsv-io/d2-utilities` | `falsey` / helpers |
| `@dcsv-io/d2-logging` | `ILogger` |
| `@dcsv-io/d2-i18n-keys` | `TK.common.errors.COULD_NOT_BE_*` for serializer messages |
| `@opentelemetry/api` | Meter / counters |
| `ioredis` | Redis command + subscriber clients |

## References

- [`../abstractions/README.md`](../abstractions/README.md)
- [`../README.md`](../README.md)
- [`docs/PATTERNS.md`](../../../../../docs/PATTERNS.md)
- .NET twin: `public/packages/dotnet/caching/distributed-redis/`
