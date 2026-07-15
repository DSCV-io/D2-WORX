<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog — @dcsv-io/d2-caching-distributed-redis

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial `RedisDistributedCache` + `RedisCacheInvalidationBackplane` + `JsonCacheSerializer` implementation of `IDistributedCache` / `ICacheInvalidationBackplane` / `ICacheSerializer` (twin of `DcsvIo.D2.Caching.Distributed.Redis`): Basic + Atomic + Broadcast + Set over Redis, default channel `d2:cache:invalidations`, Lua atomics, and the `d2.cache.redis.*` OTel counters (`REDIS_CACHE_METER_NAME`).
- Barrel export of Lua twin-pin constants (`INCREMENT_WITH_OPTIONAL_TTL`, `RELEASE_LOCK_IF_OWNER`, `SET_ADD_WITH_OPTIONAL_TTL`) for dual-runtime ContractFixtures parity. Not an executor surface.
- Barrel export of `REDIS_CACHE_INSTRUMENTS` + `REDIS_CACHE_METER_VERSION` (instrument metadata SoT for counters + parity).

### Fixed

- `increment` refuses out-of-range results via shared Lua
  `INCREMENT_WITH_OPTIONAL_TTL` (atomic INCRBY + DECRBY reverse +
  `ERR safe_integer_overflow`) twin of .NET — no client-side race window.
  Non-safe-integer `amount` still rejected up front.
