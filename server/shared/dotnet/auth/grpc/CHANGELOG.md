# Changelog — D2.Shared.Auth.Grpc

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `RequestOriginUnestablishedDenyInterceptor` — platform fail-closed deny for
  product gRPC when `RequestOrigin` is still `Unestablished` after
  `RequestOriginCrossProcessInterceptor` (no validated mTLS peer). Harmless
  methods skip. Folded into `AddD2RequestOriginGrpc()` so establish + deny are
  one registration path (inbound order: JwtAuth → Origin establish → this deny).
  Surfaces `AUTH_REQUEST_ORIGIN_UNESTABLISHED` (401 / Unauthenticated).

### Fixed

- Dual-path scoped `IRequestContext` resolver (Items when established, else
  `MutableRequestContext`) replaces the throw-only path that broke hosted System
  workers on the same host. Pairs with platform `AddD2SystemWorkPlane()`.
