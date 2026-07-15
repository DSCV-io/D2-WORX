# Changelog — @dcsv-io/d2-encryption

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial runtime crypto twin of .NET `DcsvIo.D2.Encryption`: symmetric
  (v1 AES-256-GCM) `PayloadCrypto` + `PayloadCryptoKeyring`, and sealed
  (v2 P-256 ECDH-ES → HKDF-SHA256 → AES-256-GCM) `PayloadSealer` /
  `PayloadOpener` + `RecipientPublicKeyring` / `RecipientPrivateKeyring`, with
  the frozen `SealedKeyDerivation` conventions and a typed failure taxonomy.

### Fixed
