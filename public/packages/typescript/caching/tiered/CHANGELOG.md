<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog — @dcsv-io/d2-caching-tiered

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial `DefaultTieredCache` implementation of `ITieredCache` (twin of `DcsvIo.D2.Caching.Tiered`): L1+L2 composition, L2-first writes with L1 graceful degradation, atomics via L2 + L1 side-effects, optional invalidation-backplane subscribe for everyone-acts L1 drop, and `*AndBroadcast*` via the injected backplane. No `ICacheSet`. No OTel meters (structured logs only).
- Barrel export of `TieredCacheOp` + `TIERED_ERROR_CODE_UNKNOWN` for dual-runtime ContractFixtures parity (closed-set op names + errorCode sentinel).

### Fixed
