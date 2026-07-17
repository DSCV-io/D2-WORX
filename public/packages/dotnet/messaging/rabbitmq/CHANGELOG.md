# Changelog — DcsvIo.D2.Messaging.RabbitMq

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Sealed-mode payload handling in the encrypted-body pipeline (public API
  unchanged — the composer is internal): auto-seal-on-publish and
  auto-open-on-consume for sealed domains resolve the keyed `IPayloadSealer` /
  `IPayloadOpener` by the domain's consumer ServiceId and emit / accept
  version-2 sealed frames. A producer or consumer host missing the keyed
  registration fails loud (publish throws; consume routes the delivery to DLQ) —
  no plaintext fallback.
- An unconditional boot self-check that fails startup when a subscriber on a
  sealed domain has no matching `IPayloadOpener` registered — registered
  independently of the KeyCustodian sealing call it guards, so a forgotten
  wiring call crashes boot before any consumer channel opens rather than
  DLQ'ing every delivery.


### Changed

- Sealed-domain resolution for auto-seal / auto-open / `SealedConsumerStartupCheck`
  reads `EncryptionDomainModeCatalog` (public overlay + generated baseline), not
  product-private sealed domain constants on the public package. Product hosts must
  register sealed domains via `ProductEncryptionDomainBootstrap` (composed by every
  sealed DI entrypoint: Via + From / shared `AddSealedEncryptionOverSealingClient`).
### Fixed
