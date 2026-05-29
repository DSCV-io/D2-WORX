<!--
Copyright (c) DCSV. All rights reserved.
-->

# caching/

> Parent: [`server/shared/dotnet/`](../README.md)

The cache stack for every backend service — shared abstractions plus the concrete local, distributed, and tiered implementations. Consumers inject one of the marker interfaces (`ILocalCache` / `IDistributedCache` / `ITieredCache`) from the abstractions package; the marker name carries the behavioral intent (process scope vs cluster scope vs L1+L2 composition) at the dependency site. Every operation returns `D2Result<T>` / `D2Result`.

## Packages

| Package                                            | Description                                                                                                                                          |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md)          | The cache-stack abstractions — three building-block interfaces composed by the `ILocalCache` / `IDistributedCache` / `ITieredCache` marker interfaces. |
| [`local-default/`](local-default/README.md)        | `DefaultLocalCache : ILocalCache` over `IMemoryCache` with in-process lock state and hit/miss/eviction counters.                                    |
| [`distributed-redis/`](distributed-redis/README.md) | `RedisDistributedCache : IDistributedCache` over StackExchange.Redis with a pub/sub invalidation backplane and atomic Lua scripts.                  |
| [`tiered/`](tiered/README.md)                       | `DefaultTieredCache : ITieredCache` composing an L1 local cache with an L2 distributed cache and cluster-wide L1 coherency.                         |
