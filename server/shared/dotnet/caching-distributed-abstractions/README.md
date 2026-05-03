<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Distributed.Abstractions

> **Status**: placeholder — not yet implemented.

## Purpose

Domain-safe slice of the distributed-cache stack. Owns the `ID2DistributedCache` interface + `ICacheSerializer` interface + `DistributedCacheOptions` value type. Zero external deps (no NuGet packages, no other shared-lib references) so any handler / repo / service can declare a dependency on distributed caching without dragging in `StackExchange.Redis` or whichever provider ships behind the abstraction.

The default implementation lives in the sibling [`caching-distributed-redis/`](../caching-distributed-redis/) project. Future implementations (Valkey, Memcached, Garnet) would live as sibling projects under their own `caching-distributed-{impl}/` slot.

## Public API surface (planned)

- `ID2DistributedCache` — full distributed-cache semantic surface: `Get<T>` / `Set<T>` / `SetNx<T>` / `Remove` / `Exists` / `GetTtl` / `Increment` / `AcquireLock`. `SetNx` / `Increment` / `AcquireLock` are the atomic primitives that distinguish distributed from local — domain code calling these is implicitly relying on cross-instance atomicity guarantees.
- `ICacheSerializer` — pluggable serialization seam (default: JSON for dev-readability; binary protobuf when message size matters)
- `DistributedCacheOptions` — small Options record (default TTL, key prefix, retry policy, serializer choice)

## Dependencies

(planned) None — only the .NET runtime.

## V2.md reference

§4 Phase 0 (Stage 4 — Wave 5).
