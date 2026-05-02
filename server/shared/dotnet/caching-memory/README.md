<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Memory

> **Status**: placeholder — not yet implemented.

## Purpose

In-memory cache — per-instance, lazy TTL eviction + always-on LRU + max 10K default. Fine for read-heavy TTL-bounded data where cache consistency across replicas is NOT required. Used heavily inside client libraries' multi-tier cache hierarchies (memory → Redis → DB).

## Public API surface

- `MemoryCacheStore` — concrete store (Map + lazy TTL + LRU)
- 5 handlers under `Handlers/{TLC}/{3LC}/`: Get, Set, Remove, Exists, Clear (per [docs/PATTERNS.md](../../../../docs/PATTERNS.md) Cache section)
- DI registration: `services.AddD2MemoryCache(options)`

## Dependencies

- `D2.Shared.Handler` (handlers inherit BaseHandler)
- `D2.Shared.Result` (return values)

## References

- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "Cache" section — lazy TTL + LRU mechanics
- MultiTierFetcher utility (consumes memory cache as L1)

## Important

Per-instance only. **Correctness must NOT depend on cache consistency across replicas.** For shared cache state, use `D2.Shared.Caching.Redis`.
