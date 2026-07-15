# Changelog — D2.Shared.ServiceDefaults

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Always wires `AddD2SystemWorkPlane()` (platform System work entry + default
  scoped `IRequestContext`) so hosted System workers work with or without auth
  auto-wiring.

### Fixed
