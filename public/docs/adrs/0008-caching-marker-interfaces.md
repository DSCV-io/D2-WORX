<!--
Copyright (c) DCSV. All rights reserved.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0008: Caching — marker-interface model (`ILocalCache` / `IDistributedCache` / `ITieredCache`) with `D2Result` ops + broadcast invalidation

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: D2 shared libraries (backfilled)

## Context

D²-WORX is a multi-instance cluster. At least three distinct caching behavioral profiles are needed simultaneously within a single process:

- **Per-process hot data** (JWKS, token fingerprints, per-instance counters) — microsecond reads, dies with the process, no cross-instance coordination.
- **Cluster-authoritative state** (rate-limit counters, distributed locks, ephemeral session data) — every read must hit the canonical remote store; freshness dominates read speed.
- **Read-heavy entity data** (user profiles, org settings) — sub-microsecond reads from an in-process L1 most of the time, with L2 as source of truth; freshness within a few seconds is acceptable.

.NET's built-in `Microsoft.Extensions.Caching.Abstractions.IDistributedCache` provides a single behavioral profile (remote `byte[]` get/set/refresh/remove) with no atomic primitives, and `null` doubles as both the miss sentinel and the failure sentinel. A consumer declaring it gives readers no signal about whether the cache is local, cluster-wide, or two-tier. The codebase additionally needs `SetNx`, `Increment`, `AcquireLock`/`ReleaseLock` at both local and cluster scope, and cluster-wide L1 coherency that TTL alone cannot achieve without unacceptable staleness.

## Decision

### 1. Three building blocks compose into three marker interfaces

Fine-grained building blocks: `ICacheBasic` (get/set/exists/ttl/remove + bulk variants), `ICacheAtomic` (`SetNx`, `Increment`, `AcquireLock`/`ReleaseLock`), `ICacheBroadcast` (`*AndBroadcast*` write variants), `ICacheSet` (SADD/SCARD/SREM/SISMEMBER). Three **marker interfaces** compose them and carry no members of their own — their name *is* the point:

- `ILocalCache : ICacheBasic, ICacheAtomic` — per-process; deliberately omits broadcast (nothing outside the process observes local state).
- `IDistributedCache : ICacheBasic, ICacheAtomic, ICacheBroadcast, ICacheSet` — cluster; every read hits remote.
- `ITieredCache : ICacheBasic, ICacheAtomic, ICacheBroadcast` — L1+L2.

`IDistributedCache` and `ITieredCache` share the same Basic + Atomic + Broadcast surface; the marker name is the primary distinguishing signal at the dependency site (`IDistributedCache` → every read hits remote; `ITieredCache` → L1-first, populate-on-L2-hit). They are **not** method-for-method identical: `ICacheSet` is exposed only on `IDistributedCache` — set primitives are cluster-scoped by nature, and tiered composition would silently hide that an op does not participate in L1.

### 2. Every operation returns `D2Result<T>` / `D2Result`

No exceptions for per-call failures, no null sentinels (an instance of the errors-as-values decision, ADR-0003): cache miss → `NotFound`; partial bulk hit → `SomeFound` (carries partial data); null/empty key or collection → `ValidationFailed` with an `InputError`; backing-store unreachable → `ServiceUnavailable`; type mismatch on `Increment` → `Conflict`; lock-already-held on `SetNx`/`AcquireLock` → `Ok(false)` (not a failure — callers inspect `Data`). Constructors and DI-registration errors still throw (programmer/startup failures). The one documented carve-out: `*AndBroadcast*` methods throw `InvalidOperationException` when no `ICacheInvalidationBackplane` is registered — that is a registration misconfiguration, not a per-call input error.

### 3. Broadcast invalidation for cluster-wide L1 coherency

TTL-only invalidation would require implausibly short TTLs (defeating L1) or accepting staleness after mutation. Instead, `ICacheBroadcast`'s `*AndBroadcast*` write variants publish an invalidation via the registered `ICacheInvalidationBackplane` after the write completes. The production impl, `RedisCacheInvalidationBackplane`, uses a single Redis pub/sub channel; delivery is at-most-once (a missed message means the next read falls through to L2 — correct and bounded). The **universal "everyone acts" rule** has no sender-ID filter: the publishing instance receives its own message and drops its own L1 entry (one extra L2 read on next access) — the chosen tradeoff over sender-ID-filter complexity, and it makes session-revocation flows correct automatically. The `*AndBroadcast*` names are deliberately greppable so review can see at a glance which writes propagate cross-cluster. `DefaultTieredCache` subscribes to the backplane at construction and removes the named key from L1 on invalidation; the subscription is held as `IAsyncDisposable` and released on dispose (prevents subscription leaks).

### 4. Tiered L1+L2 composition

`DefaultTieredCache` composes one `ILocalCache` (L1) + one `IDistributedCache` (L2): reads check L1, fall through to L2, populate L1 on L2 hit; **writes go L2-first** (L1 only if L2 succeeded — so partial-write states are impossible); atomic ops route through L2 (the cluster source of truth) and invalidate L1; `GetTtl` reports L2's TTL. `DefaultLocalCache` implements `ILocalCache` directly against `IMemoryCache` + a `ConcurrentDictionary` lock/TTL state, with **direct dispatch (no `BaseHandler`)** — the local-only carve-out noted in ADR-0005 (cache ops are tens of nanoseconds; the pipeline would be ~100× overhead). `Size=1` per entry makes `MaxEntries` a real entry-count cap.

## Consequences

**Positive.**

- The dependency site is self-documenting: a constructor parameter typed `ITieredCache` declares the behavioral profile without opening DI registration.
- No ambiguous null: misses are `NotFound`, partial hits `SomeFound`, store failures `ServiceUnavailable`; callers branch on named states.
- Cluster-coherent L1 without implausibly short TTLs, within the at-most-once delivery window.
- Atomic ops have a consistent surface at both process and cluster scope; Redis atomicity is enforced by Lua scripts for single-round-trip compound ops.
- The backplane is provider-agnostic: tests inject an in-process stub; production uses Redis pub/sub.

**Negative / risks.**

- **Near-identical Basic/Atomic/Broadcast surface of `IDistributedCache` and `ITieredCache`** is a persistent reader hazard — same write/read/broadcast method surface except `ICacheSet` only on distributed; the difference is the marker name + registered concrete (+ Set). Registering the wrong concrete wires the wrong profile with no compile error. Mitigated by naming + docs, not by the type system.
- **At-most-once backplane delivery**: a missed invalidation leaves a stale L1 entry until TTL. For never-stale data, callers must use `IDistributedCache` (no L1).
- **Publisher receives its own broadcast**: one extra L2 round-trip after a write-then-read-immediately — bounded and intentional, but it slightly reduces L1 hit rate for that pattern.
- **`*AndBroadcast*` misconfiguration throws** rather than returning a result failure — chosen because a missing backplane is a registration error, but it makes that one failure surface inconsistent with the rest.
- **`ICacheSet` is cluster-only** — set consumers must inject `IDistributedCache` and forgo tiered read acceleration.

## Alternatives considered

**Use `Microsoft.Extensions.Caching.Abstractions.IDistributedCache` directly.** No `SetNx`/`Increment`/lock primitives, `null` for both miss and failure, no broadcast surface, no behavioral declaration at the dependency site. Adopting it as the primary abstraction would force each caller to reimplement atomics + miss/failure discrimination + L1 coherency ad hoc. `PATTERNS.md` permits the framework `IDistributedCache` only where plain get/set/refresh/remove suffices.

**A single `ICache` interface.** Eliminates the structural-identity hazard but removes all behavioral declaration at the dependency site — callers would need docs/conventions to communicate local vs cluster vs tiered intent, weaker than the marker approach.

**Throw / return null instead of `D2Result`.** The standard .NET shape leaves callers to distinguish miss from failure via exceptions or swallowed-exception-plus-null. The errors-as-values decision (ADR-0003) applies uniformly; a special-case cache surface would break the no-throws-for-data-flow rule the handler pipeline depends on.

**TTL-only invalidation (no backplane).** Forces a choice between short TTLs (L1 adds latency without benefit) and stale reads after mutation. For profile/session data a seconds-to-minutes staleness window is unacceptable; a broadcast backplane is the better tradeoff. Short TTLs remain an option for keys where broadcast cost dominates or single-instance deployment applies.

## References

- `public/packages/dotnet/caching/abstractions/` — `ILocalCache.cs`, `IDistributedCache.cs`, `ITieredCache.cs`, the four building-block interfaces, `ICacheInvalidationBackplane.cs`, `InputFailures.cs`, `LocalCacheOptions.cs`, and the README (result-mapping table + "everyone acts" rationale).
- `public/packages/dotnet/caching/local-default/DefaultLocalCache.cs` — `IMemoryCache` + `ConcurrentDictionary`; `Size=1`; direct dispatch.
- `public/packages/dotnet/caching/distributed-redis/` — `RedisDistributedCache.cs`, `RedisLuaScripts.cs`, `RedisCacheInvalidationBackplane.cs`.
- `public/packages/dotnet/caching/tiered/DefaultTieredCache.cs` — L2-first writes, populate-on-hit, backplane subscription.
- TypeScript twin cluster: `public/packages/typescript/caching/` — `@dcsv-io/d2-caching-abstractions`, `@dcsv-io/d2-caching-local-default`, `@dcsv-io/d2-caching-distributed-redis`, `@dcsv-io/d2-caching-tiered` (behavioral runtime twin of this ADR; package READMEs under each folder). Markers are **not** method-for-method identical across distributed vs tiered: `ICacheSet` is distributed-only (see Decision §1).
- Dual-runtime constants/semantics parity: `public/packages/typescript/contract-tests/fixtures/caching-twin/constants.json` + `tests/caching-twin.parity.test.ts` (emitted by `CachingTwinFixtureEmitter`) — defaults / meters / Lua / channel / tiered EventId bindings; not a full algorithm interop harness.
- `docs/PATTERNS.md` (Cache section); `docs/PARITY.md` (caching stack RUNTIME row); `docs/dev/rules.md` (backplane unsubscribe-on-dispose; broadcast-variant for cluster-wide L1 invalidation).
- [ADR-0006](0006-abstractions-implementation-split.md) — the abstractions/impl split. [ADR-0003](0003-d2result-errors-as-values.md) — the errors-as-values surface. [ADR-0005](0005-handler-pipeline.md) — the `DefaultLocalCache` no-`BaseHandler` carve-out.
