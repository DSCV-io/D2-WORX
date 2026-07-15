<!--
Copyright (c) DCSV. All rights reserved.
-->

# Changelog — @dcsv-io/d2-private-key-custodian-client

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Initial release: the Node workload-leaf certificate client — the behavioral
  twin of the .NET `WorkloadLeafClient`. Fresh ECDSA P-256 keypair per (re)issue
  (private key never leaves the process), PKCS#10 CSR over the emitted
  KeyCustodian gRPC wire client, leaf-to-local-key mismatch defense, CA-chain
  fetch + trust assembly, refresh-ahead + serve-stale, and mutual-TLS channel
  credential presentation.
- The TS crypto consumer twins over the mTLS gRPC channel. Symmetric keyring
  runtime: `GrpcKeyringClient` (+ the `KeyringClient` seam) over the `getKeyring`
  wire client — `GetKeyringInput` / `GetKeyringOutput` / `KeyringEntry` DTOs —
  feeding `KeyringBackedPayloadCrypto` (rotation hot-swap). Sealed runtime:
  `GrpcSealingClient` (+ the `SealingClient` seam), `KeyringBackedPayloadSealer` /
  `KeyringBackedPayloadOpener`, and the one-call `createSealedCryptoViaKeyCustodian`
  factory (+ `SealedCryptoWiring`, `CreateSealedCryptoOptions`,
  `RotationSubscription`) — the TS twin of `AddD2SealedEncryptionViaKeyCustodian`.

### Fixed
