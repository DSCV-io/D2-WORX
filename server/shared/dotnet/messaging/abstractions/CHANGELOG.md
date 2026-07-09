# Changelog — D2.Shared.Messaging.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `MqMessageDescriptor.IsSealed` and `MqMessageDescriptor.ConsumerService` —
  computed properties reading the spec-derived `EncryptionDomainModes` catalog:
  `IsSealed` is true when the descriptor's domain is in per-consumer-service
  sealed (asymmetric) mode; `ConsumerService` is the single ServiceId that opens
  the domain's sealed frames (else `null`). Both derive from the single-source
  domain-mode catalog — never new record parameters on the descriptor.

### Fixed
