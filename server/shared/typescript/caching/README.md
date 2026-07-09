<!--
Copyright (c) DCSV. All rights reserved.
-->

# caching/ — TypeScript caching stack (twin of `D2.Shared.Caching.*`)

> Parent: [`server/shared/typescript/`](../README.md)

**Status**: deliverable **0028** / PHASE_3 **T1** — PLAN locked on branch `n/ts-caching`. Packages below are **not built yet**; this index records the committed layout and parity bar.

Behavioral model: [ADR-0008](../../../docs/adrs/0008-caching-marker-interfaces.md) · .NET canonical: [`server/shared/dotnet/caching/`](../../dotnet/caching/README.md) · Tracking: `docs/wip/0028-ts-caching/` (gitignored).

## Packages (layout mirror)

| Folder | Package | .NET mirror |
| ------ | ------- | ----------- |
| [`abstractions/`](abstractions/) | `@d2/caching-abstractions` | `D2.Shared.Caching.Abstractions` |
| [`local-default/`](local-default/) | `@d2/caching-local-default` | `D2.Shared.Caching.Local.Default` |
| [`distributed-redis/`](distributed-redis/) | `@d2/caching-distributed-redis` | `D2.Shared.Caching.Distributed.Redis` |
| [`tiered/`](tiered/) | `@d2/caching-tiered` | `D2.Shared.Caching.Tiered` |

## Locked cross-runtime rules

- **Full surface** — Basic + Atomic + Broadcast + Set + tiered + backplane (no subset).
- **Shared invalidation channel** default `d2:cache:invalidations` (same as `RedisCacheOptions.InvalidationChannel` on .NET).
- **Everyone acts** — no sender-ID filter on the backplane.
- **At-most-once** delivery; missed message → next read hits L2.
- **Tiered writes** L2-first; atomics on L2 with L1 drop.
- All ops return `@d2/result` shapes aligned with .NET `D2Result` mapping.
