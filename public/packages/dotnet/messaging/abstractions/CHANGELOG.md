# Changelog — DcsvIo.D2.Messaging.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `MqMessageDescriptor.IsSealed` and `MqMessageDescriptor.ConsumerService` —
  computed properties reading `EncryptionDomainModeCatalog` (public overlay first,
  then generated `EncryptionDomainModes` baseline): `IsSealed` is true when the
  descriptor's domain is in per-consumer-service sealed (asymmetric) mode;
  `ConsumerService` is the single ServiceId that opens the domain's sealed frames
  (else `null`). Product sealed domains register via the catalog overlay (private
  `ProductEncryptionDomainBootstrap`); never new record parameters on the descriptor.

### Fixed
