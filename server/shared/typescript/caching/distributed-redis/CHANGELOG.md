<!--
Copyright (c) DCSV. All rights reserved.
-->

# Changelog — @d2/caching-distributed-redis

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial `RedisDistributedCache` + `RedisCacheInvalidationBackplane` + `JsonCacheSerializer` implementation of `IDistributedCache` / `ICacheInvalidationBackplane` / `ICacheSerializer` (twin of `D2.Shared.Caching.Distributed.Redis`): Basic + Atomic + Broadcast + Set over Redis, default channel `d2:cache:invalidations`, Lua atomics, and the `d2.cache.redis.*` OTel counters (`REDIS_CACHE_METER_NAME`).
- Barrel export of Lua twin-pin constants (`INCREMENT_WITH_OPTIONAL_TTL`, `RELEASE_LOCK_IF_OWNER`, `SET_ADD_WITH_OPTIONAL_TTL`) for dual-runtime ContractFixtures parity. Not an executor surface.
- Barrel export of `REDIS_CACHE_INSTRUMENTS` + `REDIS_CACHE_METER_VERSION` (instrument metadata SoT for counters + parity).

### Fixed

- `increment` returns validationFailed (`amount`) when `Number(result)` is
  outside the JS safe-integer range (in addition to rejecting non-safe-integer
  amounts up front).
- `increment` best-effort pre-INCRBY `GET`: refuse without mutating when
  `current + amount` is already outside JS safe-integer range; keep post-INCRBY
  reverse DECRBY as second line. Lua scripts unchanged (byte-equal .NET twin).
  Residual concurrent race between GET/INCRBY or INCRBY/reverse documented.
