# Changelog — DcsvIo.D2.Auth.Outbound

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

- `IWorkloadCertificateIssuer.IssueAsync` now takes the workload's DER-encoded
  PKCS#10 certificate-signing request (`IssueAsync(byte[] csrDer, ct)`) — the CSR
  flow: the leaf keypair is generated inside `WorkloadLeafClient` (fresh per
  reissue) and the private key never crosses the issuer seam. Implementations
  verify the CSR's proof-of-possession, ignore its subject (the leaf SAN is the
  issuer's authenticated peer view), and return certificates only.
- `WorkloadLeafMaterial` drops the `PrivateKeyPkcs8` member — the record is now
  `(CertificateDer, IssuerCertificateDer, NotAfter)`, all-public material with
  nothing to redact or zero.

### Added

- `WorkloadLeafClient` mismatch defense: a returned leaf whose public key does not
  match the locally-generated keypair is rejected before any cache write (the
  reissue counts as a transient failure; a still-valid cached leaf keeps serving),
  with a new issuer-key-mismatch warning log (EventId 3006).

### Fixed
