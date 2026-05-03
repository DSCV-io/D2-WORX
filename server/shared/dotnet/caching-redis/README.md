<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Redis

> **Status**: placeholder — not yet implemented.

## Purpose

Redis distributed cache. Pluggable `ICacheSerializer` (JSON default, custom for binary protobuf if message size matters). Provides atomic `SetNx` / `Increment` / `AcquireLock` primitives that Microsoft's built-in `IDistributedCache` does NOT.

## Public API surface

- 7 handlers under `Handlers/{TLC}/{3LC}/`: Get, Set, SetNx, Remove, Exists, GetTtl, Increment, AcquireLock
- `ICacheSerializer` (pluggable): default `JsonCacheSerializer`; opt-in `ProtobufCacheSerializer` for binary
- DI registration: `services.AddD2RedisCache(connectionString, serializer)`

## Dependencies

- `D2.Shared.Handler` (handlers inherit BaseHandler)
- `D2.Shared.Result`
- `StackExchange.Redis`

## References

- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "Cache" section — pluggable serializer pattern, multi-tier hierarchy (memory → Redis → DB)
- [docs/OPERATIONAL-GUARANTEES.md](../../../../docs/OPERATIONAL-GUARANTEES.md) — Redis as the cross-instance coordination point (sessions tier-2, idempotency `SET NX`, rate-limit sliding-window counters)
- [`../caching-memory/README.md`](../caching-memory/README.md) — the L1 in-memory tier that pairs with this lib

## Important

Don't reach for Microsoft's `IDistributedCache` when you need `SetNx` / `Increment` / `AcquireLock`. Use this lib's richer abstraction. (Per [CLAUDE.md §5](../../../../CLAUDE.md) anti-pattern: "wrapping framework primitives without an opinionated semantic.")
