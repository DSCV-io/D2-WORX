# Changelog — @dcsv-io/d2-messaging-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `MqMessagesCatalog` — the literal-typed (`as const`) per-message catalog plus
  the `MqMessageCatalogKey` union: the compile-time type-witness input the
  @dcsv-io/d2-messaging-rabbitmq publisher consumes (same data as `MqMessagesRegistry`,
  but each `encryption` value keeps its literal type).

### Fixed
