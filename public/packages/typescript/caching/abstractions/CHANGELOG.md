<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog — @dcsv-io/d2-caching-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial public surface twin of `DcsvIo.D2.Caching.Abstractions`: building-block
  ports (`ICacheBasic`, `ICacheAtomic`, `ICacheBroadcast`, `ICacheSet`), marker
  interfaces (`ILocalCache`, `IDistributedCache`, `ITieredCache`), supporting
  seams (`ICacheInvalidationBackplane`, `ICacheSerializer`),
  `LocalCacheOptions` / `LOCAL_CACHE_DEFAULTS` / `createLocalCacheOptions`, and
  `InputFailures.required` / `InputFailures.invalid` (present-but-invalid values
  use `VALIDATION_FAILED` field TK, not `NOT_NULL_VIOLATION`).

### Fixed
