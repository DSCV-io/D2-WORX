# Changelog — DcsvIo.D2.Caching.Distributed.Redis

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed

- `IncrementAsync` + Lua `INCREMENT_WITH_OPTIONAL_TTL`: after INCRBY, if the
  result is outside ±9007199254740991 (IEEE-754 max safe integer / dual-runtime
  bound shared with TypeScript), reverse DECRBY in-script and return
  validation failure for `amount` (`ERR safe_integer_overflow`). Behavior matches
  `@dcsv-io/d2-caching-distributed-redis`.
