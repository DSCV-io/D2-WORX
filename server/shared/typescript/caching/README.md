<!--
Copyright (c) DCSV. All rights reserved.
-->

# caching/ — TypeScript caching stack (twin of `D2.Shared.Caching.*`)

> Parent: [`server/shared/typescript/`](../README.md)

Behavioral model: [ADR-0008](../../../docs/adrs/0008-caching-marker-interfaces.md) · .NET canonical: [`server/shared/dotnet/caching/`](../../dotnet/caching/README.md).

## Packages (layout mirror)

| Folder | Package | Status | Purpose | .NET mirror |
| ------ | ------- | ------ | ------- | ----------- |
| [`abstractions/README.md`](abstractions/README.md) | `@d2/caching-abstractions` | **Built** | Marker + building-block cache ports (`ILocalCache` / `IDistributedCache` / `ITieredCache` + Basic/Atomic/Broadcast/Set + backplane + serializer + `InputFailures` / options). | `D2.Shared.Caching.Abstractions` |
| [`local-default/README.md`](local-default/README.md) | `@d2/caching-local-default` | **Built** | In-process L1 + atomics (no broadcast). | `D2.Shared.Caching.Local.Default` |
| [`distributed-redis/README.md`](distributed-redis/README.md) | `@d2/caching-distributed-redis` | **Built** | Redis distributed cache + invalidation backplane (Basic/Atomic/Broadcast/Set; default channel `d2:cache:invalidations`). | `D2.Shared.Caching.Distributed.Redis` |
| [`tiered/README.md`](tiered/README.md) | `@d2/caching-tiered` | **NOT IMPLEMENTED** | L1+L2 composition — L2-first writes, `*AndBroadcast*`, everyone-acts L1 drop. | `D2.Shared.Caching.Tiered` |

## Locked cross-runtime rules

- **Full surface** — Basic + Atomic + Broadcast + Set + tiered + backplane (no subset).
- **Shared invalidation channel** default `d2:cache:invalidations` (same as `RedisCacheOptions.InvalidationChannel` on .NET).
- **Everyone acts** — no sender-ID filter on the backplane.
- **At-most-once** delivery; missed message → next read hits L2.
- **Tiered writes** L2-first; atomics on L2 with L1 drop.
- All ops return `@d2/result` shapes aligned with .NET `D2Result` mapping.
