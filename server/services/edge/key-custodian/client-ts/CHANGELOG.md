<!--
Copyright (c) DCSV. All rights reserved.
-->

# Changelog — @d2/key-custodian-client

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

### Fixed
