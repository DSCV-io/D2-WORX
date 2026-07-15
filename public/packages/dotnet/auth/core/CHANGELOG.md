# Changelog — DcsvIo.D2.Auth

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `AUTH_REQUEST_ORIGIN_UNESTABLISHED` / `AuthFailures.RequestOriginUnestablished`
  — policy-denied (401) when gRPC product Origin stays Unestablished after
  cross-process establishment (no validated mTLS peer). Spec:
  `contracts/auth-error-codes/auth-error-codes.spec.json`.
- `JwksProviderOptions.TrustedRootCertificatePath` (optional) — path to a PUBLIC
  CA root PEM/DER used as `X509ChainTrustMode.CustomRootTrust` for the named
  OIDC discovery / JWKS `HttpClient` (`d2-auth-oidc-discovery`). Empty = system
  trust store only (public-CA deployments). Hosts in private-PKI meshes set this
  to the same public root used for mTLS TrustAnchors. Chain + hostname
  still validated; no accept-any-cert / Development free pass.

### Fixed
