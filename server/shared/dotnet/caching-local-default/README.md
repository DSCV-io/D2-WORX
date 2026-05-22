<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Local.Default

> Parent: [`server/shared/dotnet/`](../README.md)

Default per-process implementation of [`ILocalCache`](../caching-abstractions/README.md). Wraps
`Microsoft.Extensions.Caching.Memory.IMemoryCache` for value storage; uses a `ConcurrentDictionary`
for in-process lock state.

## Public surface

**`DefaultLocalCache : ILocalCache, IDisposable`** — the implementation.

**`LocalCacheOptions`** — `MaxEntries` (default 100_000), `DefaultExpiration` (1h),
`KeyPrefix` (empty). Default sized for production Edge / Files-class workloads where
session-liveness, JWKS, token-exchange, and other per-process caches share a single
singleton; ~100 MB process RSS worst-case at ~1 KB / entry.

**`services.AddD2LocalCache(opts => …)`** — DI registration. Singleton lifetime.

## Design rationale: no BaseHandler wrapping

Every other shared lib that does I/O wraps each operation in `BaseHandler` for OTel spans +
structured logs + per-call metrics + universal try/catch. This lib doesn't — and that's deliberate.

`IMemoryCache.TryGetValue` is **~60 ns with zero allocations** on .NET 8+. The BaseHandler pipeline
is **~1–5 μs with allocations** (ActivitySource start, Stopwatch, ILogger.BeginScope dict alloc,
D2Result alloc, 4 metric instrument calls). For an op that does 60 ns of real work, handler overhead
is 100× the work itself, and per-call cache spans flood traces with noise that's never useful.

For distributed cache (Redis: 1–5 ms network round-trip) the same handler overhead is 0.1–0.5% —
there it stays.

Local cache uses **direct method dispatch** with a static `Meter` for aggregate counters
(`d2.cache.local.hits` / `.misses` / `.sets` / `.removes` / `.evictions`). The Meter instruments are
nearly free (atomic increment) and the aggregate signal is what actually matters for cache health
(hit rate, eviction pressure).

## Design rationale: `IMemoryCache` over self-rolling LRU

`IMemoryCache` is `ConcurrentDictionary`-backed (confirmed by Microsoft Learn) — lock-free reads
with striped writes. The actual cache work is hardware-accelerated. The community consensus
(BitFaster, FusionCache, ASP.NET Core internals) is "wrap `IMemoryCache` for typical workloads;
switch to `BitFaster.Caching.ConcurrentLru` only if profiling shows contention biting." At our scale
(10K-entry ceiling, normal CRUD throughput) MemoryCache's known pitfalls (`_cacheSize` contention,
compaction task explosion) don't bite.

Strict-LRU semantics — which IMemoryCache doesn't give (it's "compaction by priority + age") — would
require an exclusive lock per read (move-to-front on a linked list). That destroys the read
concurrency `ConcurrentDictionary` gives for free. Don't self-roll unless you have a measured
reason.

If we ever need strict LRU or strict FIFO, the abstraction is the seam: ship
`D2.Shared.Caching.Local.Lru` as a sibling, swap the registration, zero call-site changes.

## The SizeLimit footgun, mitigated

`IMemoryCache` only enforces `SizeLimit` if every `Set` call provides a `Size` value on the entry.
Without it, the cache grows unbounded (eviction only fires on system-wide memory pressure — see
[dotnet/runtime#114714](https://github.com/dotnet/runtime/issues/114714)). The "10K ceiling" would
be fictional.

This impl always sets `entry.Size = 1` internally. Caller never sees `Size` on the API surface.
`MaxEntries` becomes the actual entry-count cap most callers expect. `CompactionPercentage` is left
at the default 0.05 (single overshoot evicts 5% rather than triggering a giant sort+evict pass).

## Atomic primitives at process scope

- **`SetNxAsync`** — atomic via `IMemoryCache.TryGetValue` + write-if-absent under per-cache lock.
- **`IncrementAsync`** — `lock` on the cache instance for the read-modify-write window. Counter
  values stored in the same key namespace as regular values (Redis WRONGTYPE parity); incrementing a
  key that holds a non-`long` value returns `D2Result.Conflict`.
- **`AcquireLockAsync` / `ReleaseLockAsync`** — `ConcurrentDictionary<string, LockEntry>` with
  `(lockId, expiresAt)`. Acquire uses `AddOrUpdate` with expiration check; release uses
  compare-and-remove keyed on the lockId. Locks expire automatically — crashed callers don't hold
  forever.

All four operate at **process scope**. They guarantee atomicity within this instance; cluster-wide
coordination requires `IDistributedCache`.

## Test coverage

- **Unit tests** (`Unit/Caching/Local/DefaultLocalCacheUnitTests.cs`) — surface tests on D2Result
  mapping, argument validation, key prefixing, idempotency, TTL semantics (preservation across
  Increment, rejection of non-positive values), per-entry validation in multi-key ops. Fast.
- **Behavior tests** (`Unit/Caching/Local/DefaultLocalCacheBehaviorTests.cs`) — exercise real
  `IMemoryCache` semantics: capacity-driven eviction, real TTL expiration with wall-clock waits,
  16-32 thread concurrency stress on Increment + AcquireLock, race-protection probes for SetCore +
  SetNx contender races. Stay in the unit tier because no external infra is required (in-process
  `IMemoryCache` covers everything).

## Notes

- **Calls on a disposed instance throw `ObjectDisposedException`** — matching `IMemoryCache`'s own
  contract. The "ops never throw" guarantee applies to per-call data flow under a live instance, not
  to misuse-after-dispose. Don't share the cache instance between the disposing scope and any
  in-flight async caller.
- **`GetTtlAsync` may transiently report no-TTL after a Capacity eviction** — see the
  `r_expirations` field doc on `DefaultLocalCache.cs`. Workloads needing strict-LRU-with-coherent-TTL
  semantics should compose a sibling impl rather than rely on this one's tradeoffs.

## Dependencies

- [`caching-abstractions/`](../caching-abstractions/README.md) — `ILocalCache` + options
- `Microsoft.Extensions.Caching.Memory` — backing store
- `Microsoft.Extensions.Options` — DI options binding
- `Microsoft.Extensions.DependencyInjection.Abstractions` — registration

## References

- [PATTERNS.md](../../../../docs/PATTERNS.md) cache section
- [`caching-abstractions/README.md`](../caching-abstractions/README.md) — interface + result-mapping
  reference
- [`caching-distributed-redis/README.md`](../caching-distributed-redis/README.md) — cluster-scoped
  counterpart
- [`caching-tiered/README.md`](../caching-tiered/README.md) — composes this lib as the L1 layer
