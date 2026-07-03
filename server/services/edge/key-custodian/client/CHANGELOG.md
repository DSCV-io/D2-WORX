# Changelog — D2.Edge.KeyCustodian.Client

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- The rotation-aware keyring consumer runtime: the internal `IKeyringClient` fetch
  seam + `GrpcKeyringClient` (cross-process source over the newly compiled
  `KeyCustodianKeyring` gRPC client stub + wire messages), the
  `KeyringBackedPayloadCrypto` hot-swap capability (in-process-memory-only key
  material, atomic rotation swap, grace-delayed zeroize, bounded
  keep-serving-current refresh), the `IRotationEventChannel` /
  `RabbitMqRotationEventChannel` rotation fan-out + `KeyringRefreshSubscriber`
  (`KeyRotatedEvent` fanout consumer), the `AddD2EncryptionForViaKeyring`
  registration source (marks provenance KeyCustodian for the deny-by-default
  encryption-source guard), the `KeyringMetrics` meter, and a type-level
  `[RedactData(SecretInformation)]` partial for the generated wire-proto
  `KeyringEntry`. The in-process sibling source lives in the KC App project.

- `IKeyCustodianApi.GetKeyringAsync` — fetch a payload-encryption key domain's
  distributable keyring (the active kid, every decryptable Active + Retiring AES key,
  and the domain's AAD context) for internal service-to-service / in-process consumers.
  New DTOs `GetKeyringInput`, `GetKeyringOutput`, and the nested `KeyringEntry`
  (`keyBytes` carries `[RedactData(SecretInformation)]`; `aadContext` is deliberately
  unredacted authenticated context). Served on both planes (the in-process leaf + the
  new `KeyCustodianKeyring` gRPC service).
- `IKeyCustodianApi.IssueLeafAsync` — issue a short-lived workload leaf certificate
  from a PKCS#10 certificate-signing request (CSR flow: the workload generates its
  own keypair; the leaf private key never crosses the wire). New DTOs
  `IssueLeafInput(csrDer)` + `IssueLeafOutput(certificateDer, issuerCertificateDer,
  notBefore, notAfter)` — all-public material, nothing redacted. Served on both
  planes (the in-process leaf + the new `KeyCustodianCertificateAuthority` gRPC
  service, wire method `IssueWorkloadCertificate`).
- `IKeyCustodianApi.GetCaCertificateAsync` — fetch the CA chain (the active root
  trust anchor + the active issuing intermediate) as public DER certificate
  material. New DTOs `GetCaCertificateInput` (parameterless) +
  `GetCaCertificateOutput(rootCertificateDer, intermediateCertificateDer)`. Served
  on both planes (the in-process leaf + the new `KeyCustodianCaCertificate` gRPC
  service).

### Renamed

- Package renamed `D2.Edge.KeyCustodian.Clients` → `D2.Edge.KeyCustodian.Client`
  (folder `clients/` → `client/`) — the singular client-package convention;
  unpublished, so no consumer-facing break. Generated DTO namespaces regenerated
  to `D2.Edge.KeyCustodian.Client` via the emitter pipeline (a codegen-input
  change, never a hand-edit of `.g.cs` output).

### Fixed
