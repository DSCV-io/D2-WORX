<!--
Copyright (c) DCSV. All rights reserved.
-->

# Changelog — @d2/caching-local-default

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial `DefaultLocalCache` implementation of `ILocalCache` (twin of
  `D2.Shared.Caching.Local.Default`): the 12 Basic + Atomic operations over an
  in-process LRU map store, constructor-injected clock, and the
  `d2.cache.local.*` OTel counters (`LOCAL_CACHE_METER_NAME`).

### Fixed

- `increment` returns validationFailed (`amount`) when the computed next
  counter would leave the JS safe-integer range (store unchanged).
