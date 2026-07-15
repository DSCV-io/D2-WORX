# Changelog — DcsvIo.D2.Auth.Http

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Fixed

- Dual-path scoped `IRequestContext` resolver (Items when established, else
  `MutableRequestContext`) replaces the throw-only path that broke hosted System
  workers on the same host. Pairs with platform `AddD2SystemWorkPlane()`.
