<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/caching-abstractions

> Parent: [`../README.md`](../README.md) · .NET mirror: `D2.Shared.Caching.Abstractions`

Node/BFF authors inject these cache **ports** without pulling Redis, logging, or DI
wiring into domain-safe code. The package is the TypeScript twin of
`D2.Shared.Caching.Abstractions`: marker interfaces (`ILocalCache` /
`IDistributedCache` / `ITieredCache`), fine-grained building blocks
(`ICacheBasic` / `ICacheAtomic` / `ICacheBroadcast` / `ICacheSet`), plus
`ICacheInvalidationBackplane`, `ICacheSerializer`, `InputFailures`, and
`LocalCacheOptions` / `LOCAL_CACHE_DEFAULTS`. Every op returns
`Promise<D2Result<…>>` / `D2Result<…>` via `@d2/result`. **No implementations**
ship here — only contracts and pure helpers.

## Public surface — building blocks

Fine-grained interfaces. Implementations declare which they support; marker
interfaces compose them. Method names are camelCase (drop .NET `Async` suffix);
cancellation is optional `signal?: AbortSignal`; durations are milliseconds
(`expirationMs`, `defaultExpirationMs`, remaining TTL from `getTtl`).

- **`ICacheBasic`** — `get` / `getMany` / `exists` / `getTtl` / `set` /
  `setMany` / `remove` / `removeMany`
- **`ICacheAtomic`** — `setNx` / `increment` / `acquireLock` / `releaseLock`
- **`ICacheBroadcast`** — `setAndBroadcast` / `setManyAndBroadcast` /
  `removeAndBroadcast` / `removeManyAndBroadcast`
- **`ICacheSet`** — `setAdd` / `setCardinality` / `setRemove` / `setContains`
  (cluster-only — Redis SADD/SCARD/SREM/SISMEMBER)

All operations return `D2Result` / `D2Result<T>` (async ops wrap in
`Promise`). Multi-entry maps use `ReadonlyMap<string, T>` (callers may
`new Map(Object.entries(record))`).

Falsey inputs (missing key, missing keys collection, missing entries map)
return `validationFailed` with an `InputError` naming the offending parameter
(built via `InputFailures.required(...)`). Implementations never throw for
**per-call** caller mistakes — every per-call failure shape is observable on
the result. Construction-time / DI-registration errors are a different
lifecycle concern and **do** surface as throws — see the `*AndBroadcast*`
carve-out below.

## Cache key convention + PII

Cache keys follow the `EntityName:{id}` shape (`Session:{userId}`,
`Jwks:{kid}`, `WhoIs:{ip}`, etc.) per `docs/PATTERNS.md`.
`LocalCacheOptions.keyPrefix` is a **namespace** prefix layered on top of that
convention (handy when multiple caches share a process), not a substitute for
it. **Keep PII out of keys** — keys leak into logs, traces, and store
inspection. Hash any user-supplied identifier first.

## Marker interfaces

What consumers inject. The marker name documents the cache scope at the
dependency site so the reader sees the parameter and immediately knows the
behavioral profile, without checking registration.

**`ILocalCache`** — composes `ICacheBasic` + `ICacheAtomic`.
Per-process scope. Atomic ops at process scope. No broadcast (nothing outside
this process can see local cache state). Use for instance-scoped data: per-
instance fingerprint cache, per-instance counters, hot in-process lookups.

**`IDistributedCache`** — composes `ICacheBasic` + `ICacheAtomic` +
`ICacheBroadcast` + `ICacheSet`.
Cluster scope. Atomic ops cluster-wide. Every read hits the remote store
(no L1). Use when freshness matters more than read speed: rate-limit counters,
distributed locks, ephemeral session lookups.

**`ITieredCache`** — composes `ICacheBasic` + `ICacheAtomic` +
`ICacheBroadcast`.
Composed L1+L2. Reads check L1 first / fall through to L2 / populate L1.
Writes go L2-first (L1 only if L2 succeeded). Atomic ops route through L2 with
L1 invalidation as side effect. Use for read-heavy entity data where freshness
within a few seconds is acceptable. **Does NOT compose `ICacheSet`** — set
primitives are cluster-only and tiered composition would silently hide their
cluster-wide nature. Callers needing SADD/SCARD inject `IDistributedCache`
directly.

**Shared blocks vs Set:** Basic + Atomic + Broadcast are method-identical on
`IDistributedCache` and `ITieredCache`. **`ICacheSet` is only on
`IDistributedCache`** (not full structural identity). The marker distinction
is behavioral — DI registration determines the concrete implementation; the
parameter type at the call site tells the reader whether they consume
"single-tier remote" or "two-tier composed."

## Supporting types

**`LocalCacheOptions`** / **`LOCAL_CACHE_DEFAULTS`** / **`createLocalCacheOptions`**
— `maxEntries` (100_000), `defaultExpirationMs` (3_600_000 / 1h),
`keyPrefix` (`""`). Factory merges an optional partial over defaults and
returns a fresh mutable object. No POCO validation (mirrors .NET).

**`InputFailures`** — pre-built `validationFailed` factory
(`required(paramName)` / `required<T>(paramName)`) used by impls so the
cache surface stays errors-as-values rather than throws for per-call caller
mistakes. Constructors / DI registration still throw — registration-time
concern, not per-call input.

**`ICacheSerializer`** — pluggable serialization for distributed caches.
`contentType: string` (free string, e.g. `"application/json"`);
`serialize` / `deserialize` with `Uint8Array`. This package owns the port only; a default JSON implementation is part of the
`@d2/caching-distributed-redis` package surface. Local
caches store objects directly and do not need this. Impls use `COULD_NOT_BE_SERIALIZED` /
`COULD_NOT_BE_DESERIALIZED` failure codes.

**`ICacheInvalidationBackplane`** — optional pub/sub backplane for
cross-instance L1 invalidation (`AsyncDisposable`). Tiered consumers
subscribe at construction; `*AndBroadcast*` writes publish on every send.
Provider-agnostic — swappable for Redis pub/sub, Postgres LISTEN/NOTIFY,
in-process for tests.

## Result mapping

| Op family | Success / partial | Failure / notes |
| --- | --- | --- |
| `get` | hit → `ok(value)` | miss → `notFound`; store down → `serviceUnavailable` |
| `getMany` | all → `ok(map)`; some → `someFound(partial)` | none → `notFound` |
| `exists` | `ok(true\|false)` | store down → fail |
| `getTtl` | remaining ms → `ok(number)`; no expiry → `ok(undefined)` | absent → `notFound` |
| `set` / `setMany` | `ok` | store down → fail |
| `remove` / `removeMany` | `ok` (**idempotent**) | store down → fail |
| `setNx` | wrote → `ok(true)`; exists → `ok(false)` | store down → fail |
| `increment` | `ok(newValue)`; **TTL applied only on key create; subsequent ops preserve TTL** | type mismatch → `conflict`; store down → fail |
| `acquireLock` | acquired → `ok(true)`; held → `ok(false)` | **requires** `expirationMs`; store down → fail |
| `releaseLock` | `ok` (**idempotent** — no-op if not held) | store down → fail |
| `*AndBroadcast*` | same as plain counterparts | **throws** if no backplane registered (registration error, not D2Result) |
| `setAdd` | new → `ok(true)`; present → `ok(false)`; **TTL only on set create** | store down → fail |
| `setCardinality` | `ok(count)`; absent → `ok(0)` | store down → fail |
| `setRemove` | removed → `ok(true)`; absent → `ok(false)` (**idempotent**) | store down → fail |
| `setContains` | `ok(true\|false)` | store down → fail |
| `publishInvalidation` / `Many` | `ok` | backplane error → fail |
| bad input (falsey key, etc.) | — | `validationFailed` via `InputFailures` (impl duty; ports document) |

**Counter width:** `number` (not `bigint`) is the intentional TS ergonomic
delta vs .NET `long`. Stay within `Number.MAX_SAFE_INTEGER`.

## Broadcast variants — when to use

The `*AndBroadcast*` methods write/remove AND publish an invalidation message.
Other instances' tiered caches subscribe to the backplane and drop their L1
copies on receipt — keeping cluster L1 caches in sync without polling.

**Use the broadcast variant when:**

- The data is shared across instances (user profile, org settings, JWKS).
- A user-initiated remove must be visible cluster-wide quickly.
- Coordinated state changes that depend on every instance seeing the new state.

**Use the plain (non-broadcast) variant when:**

- Cache-warming / startup seed.
- Single-writer-single-reader keys (derived from this instance's process ID).
- Hot-path writes where the broadcast cost dominates (counter ticks).
- Refresh writes of effectively-the-same data.
- Single-instance deployments (don't register a backplane).
- Short-TTL data where staleness is naturally bounded.

## Invalidation backplane

`ICacheInvalidationBackplane` powers `*AndBroadcast*` writes and any other
"tell every instance to drop K from its L1" flow.

### Everyone acts

Every subscriber receives every message — **including messages this instance
itself published**. There is no sender-ID filter. The cost of self-receive is
bounded (tiered next-read re-fetch from L2, or a no-op remove).

### Dispose unsubscribes / stops further delivery

`subscribe(handler)` returns an `AsyncDisposable`. Disposing a subscription
**removes that handler from fan-out** and **stops further invalidation key
delivery** to it. The handler's `AbortSignal` is aborted on dispose
(accompanies unsubscribe; does not replace it). One handler throw must not
break delivery to other handlers. Each `subscribe` is independent. Dispose is
idempotent. Disposing the backplane tears down shared provider resources,
unsubscribes remaining handlers, and cancels in-flight handler work.

### At-most-once delivery

Missed message → next read hits L2. Acceptable for cache invalidation.

```ts
const subscription = backplane.subscribe(async (key, signal) => {
  // Drop L1 entry for `key`. signal aborts when subscription is disposed.
  await dropLocal(key, signal);
});

// Later:
await subscription[Symbol.asyncDispose]();
```

## Atomic on tiered — how it works

`ITieredCache` exposes the same atomic surface as `IDistributedCache` because
the atomicity guarantee comes from L2 (the cluster source of truth). Pattern:

- **`increment`** → L2 atomic increment; on success invalidate L1 + broadcast.
- **`setNx`** → L2 SetNx; on success write L1 + broadcast; on fail invalidate L1.
- **`acquireLock` / `releaseLock`** → pure delegation to L2 (lock state is
  coordination, not a cached value).

L1 is never authoritative for atomic state. L2 is. L1 just reflects (or
invalidates). Concrete behavior lands in `@d2/caching-tiered`.

## Configuration carve-out

There is **no** shared `DistributedCacheOptions` in abstractions. Provider-
specific options (Redis connection string, Sentinel topology, channel name
for pub/sub, retry config, etc.) live on the implementation package's own
options class. The few common fields (`defaultExpirationMs`, `keyPrefix`) are
easier to redeclare per-impl than to inherit. Same for tiered: the tiered
package declares its own options when there is a real knob to expose.

The invalidation channel constant (`d2:cache:invalidations`) lives on the
Redis package options — abstractions own the **interface only**.

## Usage

Inject markers at composition roots; this package registers nothing.

```ts
import type { ILocalCache, IDistributedCache, ITieredCache } from "@d2/caching-abstractions";
import { InputFailures, createLocalCacheOptions } from "@d2/caching-abstractions";

// Domain / handler code depends only on the marker:
async function loadProfile(cache: ITieredCache, userId: string) {
  return cache.get<Profile>(`Profile:${userId}`);
}

// Impls use InputFailures for per-call validation:
function guardKey(key: string) {
  if (!key) return InputFailures.required("key");
  return undefined;
}

const localOpts = createLocalCacheOptions({ keyPrefix: "jwks:" });
```

## Telemetry

**N/A in this package** — abstractions are hook-free. Metrics live in
**local-default + redis** implementations; **tiered owns structured logs
(not meters)**.

## Dependencies

- `@d2/result` — every op returns `D2Result` / `D2Result<T>`
- `@d2/i18n-keys` — `TK.common.errors.NOT_NULL_VIOLATION` for `InputFailures`

No runtime deps beyond those (no DI, no logging, no provider libs). This
abstraction stays domain-safe so any handler can declare a cache dependency
without dragging in implementation runtime.

## References

- [PATTERNS.md](../../../../../docs/PATTERNS.md) cache section
- [`local-default/README.md`](../local-default/README.md) — local in-process impl
- [`distributed-redis/README.md`](../distributed-redis/README.md) — Redis + backplane
- [`tiered/README.md`](../tiered/README.md) — L1+L2 composition
- .NET twin: `server/shared/dotnet/caching/abstractions/`
