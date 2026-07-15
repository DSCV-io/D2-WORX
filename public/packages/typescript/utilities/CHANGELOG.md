# Changelog — @dcsv-io/d2-utilities

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `uuidv7(now?)` — a time-ordered RFC 9562 UUIDv7 minter (48-bit big-endian
  ms-timestamp prefix + 74 random bits, version/variant set), with an optional
  injectable `now: () => number` clock for deterministic tests. Homed here as the
  catalog's UUID helper (alongside `UUID_RE`); relocated out of
  `@dcsv-io/d2-messaging-rabbitmq`, which now consumes it from this package.

### Fixed
