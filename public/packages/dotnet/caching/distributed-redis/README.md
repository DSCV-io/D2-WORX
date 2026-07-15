<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Caching.Distributed.Redis

> Parent: [`public/packages/dotnet/`](../../README.md)

Redis-backed implementation of [`IDistributedCache`](../abstractions/README.md) and
[`ICacheInvalidationBackplane`](../abstractions/README.md). Wraps `StackExchange.Redis`.

## Public surface

**`RedisDistributedCache : IDistributedCache`** — implements every building block
(`ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast` + `ICacheSet`) over a
`StackExchange.Redis.IConnectionMultiplexer`.

**`RedisCacheInvalidationBackplane : ICacheInvalidationBackplane`** — pub/sub-backed
invalidation channel. Subscribes once at construction; dispatches to all registered
handlers per the universal "everyone acts" rule.

**`JsonCacheSerializer : ICacheSerializer`** — default value serializer.
`System.Text.Json`, dev-friendly (Redis CLI can inspect values directly).
Pluggable via `ICacheSerializer` registration.

**`RedisCacheOptions`** — connection string, default TTL, key prefix, channel name,
command/connect timeouts, retries.

**`services.AddD2DistributedCacheRedis(opts => …)`** — registers
`RedisDistributedCache` + the underlying `IConnectionMultiplexer`.

**`services.AddD2RedisCacheInvalidationBackplane()`** — opt-in registration of the
pub/sub backplane. Required for `*AndBroadcast*` methods on the cache (and on
tiered caches that compose it).

## Lua scripts (internal)

Three Lua scripts live in `RedisLuaScripts.cs`. They make compound atomic ops into single
round-trips:

- **`INCREMENT_WITH_OPTIONAL_TTL`** — `INCRBY` + conditional `PEXPIRE`. Used by `IncrementAsync`.
  Sets the TTL only when explicitly requested; existing TTLs survive subsequent increments.
- **`RELEASE_LOCK_IF_OWNER`** — compare-and-delete. `ReleaseLockAsync` only DELs if the stored value
  matches the caller's `lockId`. Defeats the ABA pattern where someone else's lock would otherwise
  be released by a stale releaser.
- **`SET_ADD_WITH_OPTIONAL_TTL`** — `SADD` + conditional `PEXPIRE` on first add. Sets TTL only when
  the set is being created; existing sets keep their TTL.

Lua is intentionally NOT exposed as a public surface (`IRedisScriptExecutor` etc.). If a future
caller genuinely needs custom scripts, they can take a private dep on this lib and add what they
need internally — keeping Lua as an implementation detail of the Redis impl preserves portability if
we ever swap to a different backing store.

## Error handling

- **Connection lost / timeout (`RedisException`)** —
  `D2Result.ServiceUnavailable` (graceful degradation; caller decides fail-open
  vs fail-closed via Result inspection).
- **WRONGTYPE on Increment** (key holds a non-string data structure) —
  `D2Result.Conflict`.
- **"value is not an integer or out of range" on Increment** (key holds a
  non-numeric string) — `D2Result.Conflict`.
- **Serialization failure** — `D2Result` with error code
  `COULD_NOT_BE_SERIALIZED` / `COULD_NOT_BE_DESERIALIZED`.

## Connection management

`AddD2DistributedCacheRedis(opts)` registers `IConnectionMultiplexer` as a singleton — one
connection pool shared across the app. The connection is built from
`RedisCacheOptions.ConnectionString` (StackExchange format:
`host:port[,host:port],password=...,ssl=true`). Sentinel and Cluster topologies are inferred from
the connection string.

Defaults that matter:

- `AbortOnConnectFail = false` — graceful degradation if Redis isn't up at startup; cache calls
  return `ServiceUnavailable` until it comes back.
- `CommandTimeout = 2s`, `ConnectTimeout = 5s`, `ConnectRetries = 3` — tuneable per
  `RedisCacheOptions`.

## Observability

Static `Meter` (`DcsvIo.D2.Caching.Distributed.Redis`) emits aggregate counters:
`d2.cache.redis.{hits, misses, sets, removes, broadcasts, errors}`. No per-call spans — Redis ops
are network-bound (1-5ms) and a per-call ActivitySource span would dominate at typical cache rates
without adding signal worth the noise. The aggregate counters are what tells you "is the cache
healthy" (hit rate, error rate); per-call observability is reserved for the consuming handler's own
pipeline.

## Test coverage

`Integration/Caching/Distributed/` (xunit collection `"Redis"`, shared Testcontainers Redis
fixture):

- `RedisDistributedCacheTests` — full op surface against real Redis. Includes:
  - `SetNxAsync_Concurrent_OnlyOneWinsAcrossCluster` — 32 contenders, exactly one wins (cluster-wide
    atomic SET NX)
  - `IncrementAsync_Concurrent_AggregatesAtomically` — 8×200 increments aggregate to exactly 1600
    (cluster-wide atomic INCR via Lua)
  - `IncrementAsync_OnNonNumeric_ReturnsConflict` — Redis WRONGTYPE / "not an integer" → `Conflict`
    (verifies both paths)
  - `SetAddAsync_Concurrent_BuildsCorrectCardinality` — 100 concurrent SADDs with 30 distinct values
    → SCARD = 30
  - `AcquireReleaseLock_RoundTrip` + `ReleaseLockAsync_WrongOwner_DoesNotRelease` — Lua
    compare-and-delete defeats ABA
- `RedisCacheInvalidationBackplaneTests` — pub/sub backplane against real Redis. Verifies:
  - Universal "everyone acts" rule (publisher receives own messages)
  - Multi-subscriber independence (each gets every message)
  - Error isolation (one throwing handler doesn't break others)
  - Disposal stops handler invocation
  - Cross-instance delivery (publish on instance A → received on instance B)

The test container is launched once per test run via `Testcontainers.Redis` and shared across all
`[Collection("Redis")]` test classes. Each test uses a fresh key prefix / channel name for
isolation.

## Dependencies

- [`abstractions/`](../abstractions/README.md) — `IDistributedCache`,
  `ICacheInvalidationBackplane`, `ICacheSerializer`
- `StackExchange.Redis` — Redis client
- `Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`

## References

- [PATTERNS.md](../../../../../docs/PATTERNS.md) cache section
- [`abstractions/README.md`](../abstractions/README.md) — interface + result-mapping
  reference
- [`tiered/README.md`](../tiered/README.md) — composes Local + this lib for L1+L2
  cascades
- Edge rate-limit middleware (not yet shipped) is the primary consumer of `ICacheSet` for FP-too-common detection. Reference will migrate to the Edge lib README when rate-limiting ships.
