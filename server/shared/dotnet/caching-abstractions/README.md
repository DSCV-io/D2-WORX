<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Abstractions

> Parent: [`server/shared/dotnet/`](../README.md)

Shared abstractions for the whole D² cache stack — used by `caching-local-default` (per-process),
`caching-distributed-redis` (cluster), `caching-tiered` (composed), and every consumer.

## Public surface

### Building blocks (three)

The fine-grained interfaces. Implementations declare which they support; marker interfaces compose
them.

- **`ICacheBasic`** — `Get` / `GetMany` / `Exists` / `GetTtl` / `Set` / `SetMany`
  / `Remove` / `RemoveMany`
- **`ICacheAtomic`** — `SetNx` / `Increment` / `AcquireLock` / `ReleaseLock`
- **`ICacheBroadcast`** — `SetAndBroadcast` / `SetManyAndBroadcast` /
  `RemoveAndBroadcast` / `RemoveManyAndBroadcast`
- **`ICacheSet`** — `SetAdd` / `SetCardinality` / `SetRemove` / `SetContains`
  (cluster-only — Redis SADD/SCARD/SREM/SISMEMBER)

All operations return `D2Result<T>` / `D2Result`.

Null or empty inputs (missing key, missing keys collection, missing entries dictionary) return
`D2Result.ValidationFailed` with an `InputError` naming the offending parameter (built via the
shared `InputFailures.Required(...)` helper). Implementations never throw `ArgumentException`
for **per-call** caller mistakes — every per-call failure shape is observable on the result.
(Construction-time / DI-registration errors are a different lifecycle concern and DO surface as
exceptions — see the `*AndBroadcast*` carve-out below.)

### Cache key convention

Cache keys follow the `EntityName:{id}` shape (`Session:{userId}`, `Jwks:{kid}`,
`WhoIs:{ip}`, etc.) per `docs/PATTERNS.md`. The `LocalCacheOptions.KeyPrefix` is a
_namespace_ prefix layered on top of that convention (handy when multiple caches share a
process), not a substitute for it. Keep PII out of keys — keys leak into logs, traces, and
Redis MONITOR / OBJECT inspection. Hash any user-supplied identifier first.

### Marker interfaces (three)

What DI resolves. The marker name documents the cache scope at the dependency site so the reader
sees the constructor parameter and immediately knows the behavioral profile, without checking
registration.

**`ILocalCache`** — composes `ICacheBasic` + `ICacheAtomic`.
Per-process scope. Atomic ops at process scope. No broadcast (nothing outside this
process can see local cache state). Use for instance-scoped data: per-instance
fingerprint cache, per-instance counters, hot in-process lookups.

**`IDistributedCache`** — composes `ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast`

- `ICacheSet`.
  Cluster scope. Atomic ops cluster-wide. Every read hits the remote store (no L1).
  Use when freshness matters more than read speed: rate-limit counters, distributed
  locks, ephemeral session lookups.

**`ITieredCache`** — composes `ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast`.
Composed L1+L2. Reads check L1 first / fall through to L2 / populate L1. Writes go
L2-first (L1 only if L2 succeeded). Atomic ops route through L2 with L1 invalidation
as side effect. Use for read-heavy entity data where freshness within a few seconds
is acceptable. **Does NOT compose `ICacheSet`** — set primitives are cluster-only
and tiered composition would silently hide their cluster-wide nature. Callers
needing SADD/SCARD inject `IDistributedCache` directly.

**Note on the structural identity of `IDistributedCache` and `ITieredCache`:** they compose the same
building blocks and are method-for-method identical. The marker distinction is purely behavioral —
DI registration determines which concrete implementation a consumer gets, but the parameter type at
the call site is what tells the reader whether they're consuming "single-tier remote" or "two-tier
composed." Implementation behavior differs significantly; the contract surface does not.

### Supporting types

**`LocalCacheOptions`** — `MaxEntries` (100_000), `DefaultExpiration` (1h),
`KeyPrefix` (empty).

**`InputFailures`** — pre-built `D2Result.ValidationFailed` factory
(`Required<T>(paramName)` / `Required(paramName)`) used by impls so the cache surface
stays errors-as-values rather than `ArgumentException`-throws for per-call caller
mistakes. Constructors / DI registration still throw — that's a registration-time concern,
not per-call input.

**`ICacheSerializer`** — pluggable serialization for distributed caches. Default
impl in `caching-distributed-redis` is JSON; provider-specific impls may swap in
MessagePack / Protobuf. Local caches store objects directly and don't need this.

**`ICacheInvalidationBackplane`** — optional pub/sub backplane for cross-instance
L1 invalidation. Tiered consumers subscribe at construction; `*AndBroadcast*`
writes publish on every send. Provider-agnostic — swappable for Redis pub/sub,
Postgres LISTEN/NOTIFY, in-process for tests.

Provider-specific options (Redis connection string, Sentinel topology, channel prefixes for pub/sub,
retry config, etc.) live on the implementation's own options class — there's no shared
abstraction-level "DistributedCacheOptions" base because every distributed impl will need its own
provider-specific surface anyway, and the few common fields (`DefaultExpiration`, `KeyPrefix`) are
easier to redeclare per-impl than to inherit. Same for tiered: `DefaultTieredCache` will declare its
own options when there's a real knob to expose.

## Result mapping

- `GetAsync<T>` — hit → `Ok(value)`; miss → `NotFound`; backing-store error →
  failure.
- `GetManyAsync<T>` — all hit → `Ok(dict)`; some hit → `SomeFound(partialDict)`;
  none → `NotFound`.
- `SetAsync<T>` / `RemoveAsync` — `Ok` (idempotent); failure on backing-store
  error.
- `ExistsAsync` — `Ok(true)` if present, `Ok(false)` if not.
- `GetTtlAsync` — key + TTL → `Ok(span)`; key but no TTL → `Ok(null)`; absent
  → `NotFound`.
- `SetNxAsync<T>` — wrote → `Ok(true)`; already existed → `Ok(false)`.
- `IncrementAsync` — `Ok(newValue)`; type mismatch (Redis WRONGTYPE parity) →
  `Conflict`.
- `AcquireLockAsync` — acquired → `Ok(true)`; held by other → `Ok(false)`.
- `ReleaseLockAsync` — `Ok` (idempotent — no-op if not held by this caller).
- `*AndBroadcast*` — same as their plain counterparts. Throws
  `InvalidOperationException` if no `ICacheInvalidationBackplane` is
  registered. (Registration error, not a per-call failure — caught by
  the same lifecycle carve-out as construction-time exceptions.)
- `SetAddAsync` — new member → `Ok(true)`; already present → `Ok(false)`.
- `SetCardinalityAsync` — `Ok(count)`; absent set → `Ok(0)`.
- `SetRemoveAsync` — was present → `Ok(true)`; was absent → `Ok(false)`
  (idempotent).
- `SetContainsAsync` — `Ok(true|false)`.

## Broadcast variants — when to use

The `*AndBroadcast*` methods write/remove AND publish an invalidation message. Other instances'
tiered caches subscribe to the backplane and drop their L1 copies on receipt — keeping cluster L1
caches in sync without polling.

**Use the broadcast variant when:**

- The data is shared across instances (user profile, org settings, contact info, JWKS).
- A user-initiated remove must be visible cluster-wide quickly.
- Coordinated state changes that depend on every instance seeing the new state.

**Use the plain (non-broadcast) variant when:**

- Cache-warming / startup seed (no other instance has a stale copy of a key that didn't exist).
- Single-writer-single-reader keys (derived from this instance's process ID, etc.).
- Hot-path writes where the broadcast cost dominates (counter ticks, telemetry).
- Refresh writes of effectively-the-same data.
- Single-instance deployments (don't register a backplane and the question goes away).
- Short-TTL data where staleness is naturally bounded.

The `*AndBroadcast*` naming is greppable — code review can see at a glance which writes propagate
cross-cluster.

## The invalidation backplane

`ICacheInvalidationBackplane` is the cross-instance pub/sub primitive that powers `*AndBroadcast*`
writes and any other "tell every instance to drop K from its L1" flow.

### Universal "everyone acts" rule

Every subscriber receives every message — **including messages this instance itself published**.
There is no sender-ID filter. The cost of self-receive is bounded:

- Tiered cache after `SetAndBroadcastAsync` — the publisher's L1 entry just got dropped; next read
  of K re-fetches from L2 (one extra round-trip, bounded by write rate)
- Tiered cache after `RemoveAndBroadcastAsync` — L1 was already removed; receive is a no-op
- External callers (`SessionService.RevokeAsync` etc.) — usually want their OWN instance's L1 to
  drop too, so this is correct behavior, not a cost

Universal rule = no flags, no per-call config, no caller bookkeeping. The session-revoke flow works
automatically because the caller's L1 drops alongside everyone else's.

### Subscribe pattern

```csharp
public sealed class MySubscriber : IAsyncDisposable
{
    private readonly IAsyncDisposable _subscription;

    public MySubscriber(ICacheInvalidationBackplane backplane)
    {
        _subscription = backplane.Subscribe(async (key, ct) =>
        {
            // Handle the invalidation. Async-native; isolated from other
            // subscribers' handlers (an exception here doesn't affect them).
            await DoSomethingWithKey(key, ct);
        });
    }

    public ValueTask DisposeAsync() => _subscription.DisposeAsync();
}
```

`Subscribe` returns an `IAsyncDisposable`; the subscriber holds it as a field and disposes it when
the subscriber itself is disposed. Lifetime tracking is explicit (no `event +=` / `-=` mismatch
potential).

The tiered cache subscribes automatically in its constructor — the act of registering
`services.AddD2TieredCache()` is the subscription. External subscribers (the rare case where someone
holds state outside the cache lib) explicitly call `Subscribe(handler)` in their own constructor.

## Atomic on tiered — how it works

`ITieredCache` exposes the same atomic surface as `IDistributedCache` because the atomicity
guarantee comes from L2 (the cluster source of truth) regardless of L1's role. Implementation
pattern:

- **`IncrementAsync`** → call L2's atomic increment. On success, invalidate L1 (force next read to
  repopulate from canonical L2 value) + broadcast invalidation. Counters are usually contested
  across instances; holding a stale local copy after-increment is worse than the tiny re-read cost.
- **`SetNxAsync`** → call L2's SetNx. On success, write L1 + broadcast invalidation. On fail (key
  already existed in L2), invalidate L1 (in case L1 had a stale entry) + don't broadcast.
- **`AcquireLockAsync` / `ReleaseLockAsync`** → pure delegation to L2. L1 isn't involved — lock
  state is a coordination primitive, not a cached value.

L1 is never authoritative for atomic state. L2 is. L1 just reflects (or invalidates).

## Dependencies

- `D2.Shared.Result` — every op returns `D2Result<T>`
- `D2.Shared.I18n.Abstractions` — `TKMessage` for typed default error messages

No runtime deps beyond those (no DI, no logging, no provider libs). This abstraction stays
domain-safe so any handler / repo / domain code can declare a cache dependency without dragging in
implementation runtime.

## References

- [PATTERNS.md](../../../../docs/PATTERNS.md) cache section
- [`caching-local-default/`](../caching-local-default/README.md) — `DefaultLocalCache` impl
- [`caching-distributed-redis/`](../caching-distributed-redis/README.md) —
  `RedisDistributedCache` + `RedisCacheInvalidationBackplane` impls
- [`caching-tiered/`](../caching-tiered/README.md) — `DefaultTieredCache` (L1+L2 composition)
