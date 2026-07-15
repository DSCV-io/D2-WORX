<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog — @dcsv-io/d2-caching-local-default

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial `DefaultLocalCache` implementation of `ILocalCache` (twin of
  `DcsvIo.D2.Caching.Local.Default`): the 12 Basic + Atomic operations over an
  in-process LRU map store, constructor-injected clock, and the
  `d2.cache.local.*` OTel counters (`LOCAL_CACHE_METER_NAME`).
- Barrel export of `LOCAL_CACHE_INSTRUMENTS` + `LOCAL_CACHE_METER_VERSION`
  (instrument metadata SoT for counters + dual-runtime parity).

### Fixed

- `increment` returns validationFailed (`amount`, invalid not NOT_NULL) when the
  computed next counter would leave the JS safe-integer range (store unchanged).
