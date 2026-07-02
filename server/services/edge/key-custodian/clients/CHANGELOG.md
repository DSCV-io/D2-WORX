# Changelog — D2.Edge.KeyCustodian.Clients

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `IKeyCustodianApi.GetKeyringAsync` — fetch a payload-encryption key domain's
  distributable keyring (the active kid, every decryptable Active + Retiring AES key,
  and the domain's AAD context) for internal service-to-service / in-process consumers.
  New DTOs `GetKeyringInput`, `GetKeyringOutput`, and the nested `KeyringEntry`
  (`keyBytes` carries `[RedactData(SecretInformation)]`; `aadContext` is deliberately
  unredacted authenticated context). Served on both planes (the in-process leaf + the
  new `KeyCustodianKeyring` gRPC service). The consumer client library is a later step.

### Fixed
