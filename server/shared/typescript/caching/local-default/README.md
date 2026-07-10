<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/caching-local-default

> Parent: [`../README.md`](../README.md) · .NET mirror: `D2.Shared.Caching.Local.Default`

Node/BFF authors who need a per-process `ILocalCache` inject this default in-process implementation — an LRU map store with TTL tracking, process-scope atomic primitives (set-if-absent, counters, locks), aggregate OTel counters, and `@d2/result` shapes on every operation. This README covers usage, construction, per-operation semantics, TTL and eviction rules, telemetry, and disposal.

## Usage

```ts
import { DefaultLocalCache } from "@d2/caching-local-default";

using cache = new DefaultLocalCache({
  maxEntries: 10_000,
  keyPrefix: "bff:",
});

const setR = await cache.set("session:1", { userId: "u1" });
if (!setR.success) {
  throw new Error("set failed");
}

const getR = await cache.get<{ userId: string }>("session:1");
if (getR.success) {
  console.log(getR.data.userId);
}

const counter = await cache.increment("hits");
if (counter.success) {
  console.log(counter.data);
}
```

## Construction + options

```ts
new DefaultLocalCache(
  options?: Partial<LocalCacheOptions>,
  clock?: () => number,
);
```

`Partial<LocalCacheOptions>` is merged over `LOCAL_CACHE_DEFAULTS` via
`createLocalCacheOptions` (both owned by `@d2/caching-abstractions`). The
optional `clock` parameter defaults to `Date.now` and is the sole time source
for value TTL, lock expiry, and remaining-TTL arithmetic — inject a fake in
tests.

Construction throws `RangeError` on structurally broken numbers (negative /
non-integer / non-finite `maxEntries`; non-finite `defaultExpirationMs`).
Constructors throw; live operations return results. A throwing constructor is a
composition-time bug, never per-call data flow.

Construct once at the composition root and share the instance
(singleton-equivalent usage).

## Public surface

- **`DefaultLocalCache`** — implements `ILocalCache` (`ICacheBasic` +
  `ICacheAtomic`) and `Disposable`.
- **`LOCAL_CACHE_METER_NAME`** — OpenTelemetry meter name constant.

**Basic:** `get`, `getMany`, `exists`, `getTtl`, `set`, `setMany`, `remove`,
`removeMany` (each accepts optional trailing `signal?: AbortSignal`; durations
use `*Ms` number parameters).

**Atomic:** `setNx`, `increment`, `acquireLock`, `releaseLock`.

## Result mapping

| Op | Success | Failure / special |
| -- | ------- | ----------------- |
| `get` | `ok(value)` | miss/expired → `notFound`; falsey key → VF(`key`) |
| `getMany` | all hit → `ok(map)`; partial → `someFound` (206) | none → `notFound`; falsey keys → VF(`keys`) |
| `exists` | `ok(true\|false)` — never `notFound` | falsey key → VF(`key`) |
| `getTtl` | live+TTL → `ok(ms)`; live no TTL → `ok(undefined)` | absent/expired → `notFound` |
| `set` / `setMany` | `ok()` | falsey / bad `expirationMs` → VF |
| `remove` / `removeMany` | `ok()` (idempotent) | falsey → VF |
| `setNx` | wrote → `ok(true)`; exists → `ok(false)` with `data === false` | VF on bad input |
| `increment` | `ok(newValue)` | non-counter existing → `conflict` (409); VF on bad input |
| `acquireLock` | acquired → `ok(true)`; held → `ok(false)` | VF on falsey key/lockId or non-positive finite `expirationMs` |
| `releaseLock` | always `ok()` (idempotent) | VF on falsey key/lockId |

Validation failures name the TS parameter (`expirationMs`, `amount`, `key`,
`keys`, `entries`, `lockId`) per the `InputFailures` call-site-name contract.

## TTL semantics

- Omitted `expirationMs` on write → `defaultExpirationMs`.
- `defaultExpirationMs <= 0` means entries store without expiry.
- Explicit `expirationMs` must be a positive finite number (zero / negative /
  non-finite are validation failures, never "no TTL").
- `increment` / `setNx` apply TTL only on create; existing keys preserve TTL
  (`increment` ignores `expirationMs` on the existing path).
- `getTtl` has three outcomes: `notFound` / `ok(ms)` / `ok(undefined)`.
- Expiry is checked lazily on access with the injected clock (strict `<=`).

## Capacity + eviction

`maxEntries` is a literal entry-count cap. Every access moves the touched entry
to the most-recently-used position; when a write pushes the count past
`maxEntries`, the least-recently-used entries are evicted synchronously in the
same call, so the count never exceeds the cap. The .NET twin delegates to
IMemoryCache (SizeLimit + 5% async compaction), where eviction lags the
overflowing write; this implementation evicts deterministically instead. The
evictions counter counts capacity evictions and expired-entries-detected-on-access
only - explicit removes and overwrites are not evictions.

Values and expirations live in one record, so the .NET-documented transient
no-TTL `getTtl` report after a capacity eviction cannot occur here.

## Atomicity model

Atomic primitives are process-scope only (cluster coordination is
`IDistributedCache` territory). Every operation body is synchronous (no
internal awaits), which is what makes check-then-write windows atomic on the
event loop. Locks live in a store separate from values; re-acquiring a held
lock returns `ok(false)` even for the same `lockId`; release with a wrong id is
a no-op.

The lock store is not capped by `maxEntries` and has no sweeper: an expired lock
entry lingers until it is reacquired, released by its holder, or the cache is
disposed (the .NET twin behaves the same way).

## Telemetry

Meter `"D2.Shared.Caching.Local"` v`1.0.0` with five aggregate counters (no tags,
no spans, no logs — per-call instrumentation would dominate sub-microsecond
work):

| Name | Unit | Description | When |
| ---- | ---- | ----------- | ---- |
| `d2.cache.local.hits` | `{hit}` | Local cache hits. | `get` / `getMany` hits |
| `d2.cache.local.misses` | `{miss}` | Local cache misses. | `get` / `getMany` misses |
| `d2.cache.local.sets` | `{write}` | Local cache writes. | `set` / `setMany` / successful `setNx` / new-key `increment` |
| `d2.cache.local.removes` | `{removal}` | Local cache removals (explicit). | every `remove` / `removeMany` key (incl. absent) |
| `d2.cache.local.evictions` | `{eviction}` | Entries evicted by capacity / expiration. | capacity + expired-on-access only |

`increment` on an existing counter does **not** increment `sets`.

Counters bind to the global OpenTelemetry MeterProvider at construction time.
Construct caches after telemetry setup (setupTelemetry from @d2/telemetry) or
the counters bind to the no-op meter for the life of the instance. With no
MeterProvider registered every operation still works - metric adds are silent
no-ops.

## Disposal

`dispose()` clears all state and is idempotent; the class also implements
`Symbol.dispose` for `using` declarations. Every operation on a disposed
instance throws an Error ("DefaultLocalCache is disposed.") - a disposed cache
is a lifecycle bug, not a per-call failure, so it does not surface as a
D2Result. The disposal check runs before input validation.

## Cancellation

`signal` parameters are accepted for interface parity and ignored: every
operation completes synchronously in process, so there is no cancellation window
(the .NET twin ignores CancellationToken the same way).

## Divergences from the .NET implementation

- Synchronous LRU vs async IMemoryCache compaction (see Capacity + eviction).
- Single-record TTL (no transient no-TTL `getTtl` after capacity eviction).
- Post-dispose uniformity — EVERY operation, including `acquireLock` /
  `releaseLock`, throws after `dispose()`, where the .NET lock ops do not check
  disposal and keep working (this implementation enforces the documented
  contract uniformly). The thrown type is a plain `Error` with a pinned message
  where .NET throws `ObjectDisposedException`, and the dispose check precedes
  input validation where .NET returns a validation failure for invalid input on
  a disposed instance.
- Validation additionally rejects non-finite `expirationMs` / non-safe-integer
  `amount` (the `number` type admits values `TimeSpan` / `long` structurally
  cannot).
- Counter values are safe-integer JS numbers (port-documented bound).
- `get<T>` type parameters are caller-asserted and erased at runtime (the .NET
  generic cast throws on a wrong-type read; TS cannot).

## Dependencies

- `@d2/caching-abstractions` — ports + options + `InputFailures`
- `@d2/result` — result factories
- `@d2/utilities` — `falsey`
- `@opentelemetry/api` — counters

## References

- [`../abstractions/README.md`](../abstractions/README.md) — ports + result mapping
  + key convention / PII guidance
- [`../README.md`](../README.md) — caching cluster
- [`PATTERNS.md`](../../../../../docs/PATTERNS.md) cache section
- .NET twin: `server/shared/dotnet/caching/local-default/`
