<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Distributed.Redis

> Parent: [`server/shared/dotnet/`](../README.md)

> **Status**: placeholder — not yet implemented.

## Purpose

Redis-backed implementation of `ID2DistributedCache` (from [`caching-distributed-abstractions/`](../caching-distributed-abstractions/)). Wraps `StackExchange.Redis` and provides atomic `SetNx` / `Increment` / `AcquireLock` primitives that distinguish distributed caching from local.

This is one possible implementation; if we ever need to swap to Valkey, Memcached, or Garnet, those would land as sibling projects (e.g., `caching-distributed-valkey/`) with the same `ID2DistributedCache` surface — consumers wouldn't change.

## Public API surface (planned)

- `RedisDistributedCache` — implements `ID2DistributedCache` over `StackExchange.Redis.IConnectionMultiplexer`
- `JsonCacheSerializer` — default `ICacheSerializer` impl
- 7 CRUD-ish handlers (Get / Set / SetNx / Remove / Exists / GetTtl / Increment) per [PATTERNS.md](../../../../docs/PATTERNS.md) TLC convention, plus a separate `AcquireLock` handler
- `AddD2DistributedCacheRedis(IServiceCollection, Action<DistributedCacheOptions>?)` DI extension

## Dependencies

(planned) `D2.Shared.Caching.Distributed.Abstractions`, `D2.Shared.Handler`, `D2.Shared.Result`, `StackExchange.Redis`.
