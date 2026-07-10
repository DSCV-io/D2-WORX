<!--
Copyright (c) DCSV. All rights reserved.
-->

# Changelog — @d2/caching-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial public surface twin of `D2.Shared.Caching.Abstractions`: building-block
  ports (`ICacheBasic`, `ICacheAtomic`, `ICacheBroadcast`, `ICacheSet`), marker
  interfaces (`ILocalCache`, `IDistributedCache`, `ITieredCache`), supporting
  seams (`ICacheInvalidationBackplane`, `ICacheSerializer`),
  `LocalCacheOptions` / `LOCAL_CACHE_DEFAULTS` / `createLocalCacheOptions`, and
  `InputFailures.required`.

### Fixed
