# Changelog — @dcsv-io/d2-encryption-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- The spec-emitted publisher type-witness catalog in `encryption-domains.g.ts`:
  the `EncryptionDomainModes` `as const` (literal per-domain `"symmetric"` /
  `"sealed"` mode) plus the `EncryptionDomainMode` type, and
  `ConsumerServiceByDomain` (the single consumer ServiceId per sealed domain).
  Mirrors .NET `EncryptionDomainModes`.

### Fixed
