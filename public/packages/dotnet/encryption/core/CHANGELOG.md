# Changelog — DcsvIo.D2.Encryption

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- The spec-emitted per-domain encryption-MODE surface, generated from
  `contracts/encryption-domains`: the `EncryptionDomainMode` enum (`Symmetric` /
  `Sealed`) plus the `EncryptionDomainModes` static catalog — `ModeFor(domain)`
  (an unknown domain resolves to `Symmetric`), `TryGetConsumerService(domain,
  out consumerService)`, and the `ConsumerServiceByDomain` map (the single
  decryptor ServiceId per sealed domain). A domain's sealed-ness originates ONLY
  in the spec catalog — never a second hand-set field.
- `EncryptionDomainModeCatalog` — public composition overlay for domain mode +
  sealed-consumer lookups used by messaging (`MqMessageDescriptor.IsSealed` /
  `ConsumerService`). Baseline is generated `EncryptionDomainModes`; product hosts
  call `RegisterSealedDomain` (via private `ProductEncryptionDomainBootstrap
  .EnsureRegistered` on every sealed DI entrypoint) so product sealed wire values
  resolve without public→private package references. Thread-safe; identical
  re-register is idempotent; conflicting re-register throws.
- `SealedEncryptionServiceCollectionExtensions` promoted `internal` → public
  (additive) — the sealed sibling of `EncryptionSourceServiceCollectionExtensions`
  that a registration source composes: `AddD2SealedEncryptionRecipient(
  recipientServiceId)`, `AddD2SealedEncryptionStartupCheck()`, and the new
  `AddD2SealedEncryptionSourceCheck()` (registers the hosted service enforcing
  sealed-keyring provenance deny-by-default — a static / unmarked sealed recipient
  is rejected outside a Development host; the KeyCustodian-backed source marks
  each registration's provenance KeyCustodian).

### Fixed
