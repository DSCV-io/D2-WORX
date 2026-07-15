<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Caching.Tiered

> Parent: [`public/packages/dotnet/`](../../README.md)

Two-tier cache that composes one [`ILocalCache`](../abstractions/README.md) (L1) and one
[`IDistributedCache`](../abstractions/README.md) (L2) into the
[`ITieredCache`](../abstractions/README.md) marker interface.

## Public surface

**`DefaultTieredCache : ITieredCache, IAsyncDisposable`** — the implementation.

**`services.AddD2TieredCache()`** — DI registration. Singleton. Requires
`ILocalCache` + `IDistributedCache` to be registered first.

## Behavior

**Reads** (`GetAsync` / `GetManyAsync` / `ExistsAsync`):

- Try L1 first
- On L1 miss, fall through to L2
- On L2 hit, populate L1 with the value (next read on this instance is sub-microsecond)
- Return the result

**Writes** (`SetAsync` / `SetManyAsync` / `RemoveAsync` / `RemoveManyAsync`):

- L2 first — if L2 fails, return that failure and do not touch L1
- L1 second — only fires after L2 succeeded
- **If L1 fails after L2 succeeded**, the L2-success result is what's returned — the L1 failure is
  logged at Warning (`L1WriteFailedAfterL2Success`) and swallowed. Rationale (§18 graceful
  degradation): L1 is the optional layer; an L1 sneeze on this instance must not fail a write the
  cluster has successfully accepted. Next read on this instance re-fetches from L2.
- L2-first ordering means partial-write states are impossible; the result is binary (L2's outcome,
  with any L1 noise logged separately).

**Atomic primitives** (`SetNxAsync` / `IncrementAsync` / `AcquireLockAsync` / `ReleaseLockAsync`):

- Route through L2 (the cluster source of truth)
- After L2 succeeds, invalidate L1 so the next read fetches the canonical value
  - For `SetNxAsync`: if L2 took the write, populate L1 with the same value; if L2 already had the
    key, drop our (potentially stale) L1
  - For `IncrementAsync`: always invalidate L1 (counters held in L1 would diverge from L2
    immediately as other instances increment)
  - For `AcquireLockAsync` / `ReleaseLockAsync`: pure delegation; L1 isn't involved in lock state at
    all

**Broadcast variants** (`SetAndBroadcastAsync` etc.):

- Do the underlying write, then publish via the registered `ICacheInvalidationBackplane`
- All subscribers (across every connected instance, including this one) receive the key and drop
  their L1 entry — universal "everyone acts" rule
- The publishing instance also receives its own message and drops L1; next read re-fetches from L2
  (one extra round-trip — bounded cost; acceptable for read-heavy use cases)

## Backplane subscription

If an `ICacheInvalidationBackplane` is registered, the tiered cache subscribes to it in its
constructor. Every received invalidation key triggers a `RemoveAsync` on the L1 cache — that's the
entire L1-coherency mechanism. **If the L1 `RemoveAsync` fails**, the failure is logged at Warning
(`L1InvalidationFailed`) and processing continues; L1 stays stale on this instance until TTL or the
next write/broadcast for that key. The subscription disposes when the tiered cache disposes.

If no backplane is registered, `*AndBroadcast*` methods throw `InvalidOperationException`. The plain
ops (`SetAsync` / `RemoveAsync` etc.) work fine; L1 caches drift from each other until their TTLs
expire.

## Design rationale: no `ICacheSet`

`ITieredCache` deliberately does NOT implement `ICacheSet` (SADD/SCARD). Set-cardinality is
inherently cluster-only — there's no honest way to compose it across L1+L2 (an L1 set would only see
this instance's adds; cluster cardinality lives in L2). Callers needing set primitives inject
`IDistributedCache` directly.

## Test coverage

`Integration/Caching/Tiered/` (xunit collection `"Redis"`, shared Testcontainers Redis fixture):

- `DefaultTieredCacheRedisTests` — end-to-end against real L1 (IMemoryCache) + real L2 (Redis):
  - `Get_L1Miss_PopulatesFromL2`
  - `Set_WritesToBoth` / `Remove_RemovesFromBoth`
  - `SetAndBroadcast_OtherInstanceL1Drops` — two tiered instances on the same backplane; A writes,
    B's L1 receives the invalidation and drops
  - `SetAndBroadcast_OwnInstanceL1AlsoDrops` — universal "everyone acts" rule verified end-to-end
  - `IncrementAsync_RoutesThroughL2_InvalidatesL1`
  - `RemoveAndBroadcast_OtherInstancesDropL1`

## Dependencies

- [`abstractions/`](../abstractions/README.md) — `ITieredCache`, `ILocalCache`,
  `IDistributedCache`, `ICacheInvalidationBackplane`
- No backing-store deps directly — the impl is pure composition

## References

- [`abstractions/README.md`](../abstractions/README.md) — interface + result-mapping
  reference
- [`local-default/README.md`](../local-default/README.md) — typical L1 impl
- [`distributed-redis/README.md`](../distributed-redis/README.md) — typical L2 impl
