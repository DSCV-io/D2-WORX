<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth

> Parent: [`server/shared/dotnet/`](../README.md)

> **Status**: placeholder — not yet implemented.

## Purpose

Runtime JWT validation, JWKS fetching, `KeyringClient` (consumes Edge's KeyCustodian endpoint), and the `AddD2Auth` DI extension. The shared auth-runtime bits used by every service that participates in D²'s auth model.

The vocabulary slice (enums, `ActorEntry`, `JwtClaimTypes`, `RequestHeaders`, codegen-emitted `Scopes`) lives in the sibling [`D2.Shared.Auth.Abstractions`](../auth-abstractions/) project — domain code references that, never this runtime lib.

## Public API surface

- `services.AddD2Auth(jwksUrl)` — registers JWT validation primitives + `KeyringClient`
- `KeyringClient` — fetches encryption-domain keyrings from Edge's `internal/keys/{domain}` endpoint; caches in memory; refreshes hourly; live-reloads on `d2.security.key-rotated` event
- JWT validation pipeline — RS256 signature, JWKS-based; `aud` / `exp` checks; `act` chain parsing (delegates to `ActorChainParser` in `D2.Shared.Context.Abstractions`); `scope` claim parsing (delegates to `ScopeClaimParser`)
- `JwtClaimsHelper` — ergonomic readers for the JWT shape (org / org_role / org_type / scope / act chain)

Impersonation gating is per-scope (the `impersonationBlocked` field in `contracts/auth-scopes/scopes.spec.json`) and enforced at JWT mint time by the issuing service stripping blocked scopes from impersonation tokens. Runtime needs no attribute or annotation — the minted JWT simply doesn't carry the blocked scopes, and the handler's `RequiredScopes` check fails naturally.

## Dependencies

- `D2.Shared.Auth.Abstractions`
- `D2.Shared.Context.Abstractions` (for `ActorChainParser` + `ScopeClaimParser`)
- `D2.Shared.Result`
- `D2.Shared.Utilities`
- `Microsoft.IdentityModel.Tokens` + `System.IdentityModel.Tokens.Jwt`
- `Grpc.Net.Client` (KeyringClient calls Edge via gRPC)

## References

- [`../auth-abstractions/README.md`](../auth-abstractions/README.md) — vocabulary slice (enums + claim names + `Scopes` catalog)
- [`docs/SECURITY-RUNBOOKS.md`](../../../../docs/SECURITY-RUNBOOKS.md) — KeyCustodian compromise response runbooks
- `docs/JWT-CLAIMS.md` (TBD — created when the first `d2_`-prefixed claim ships)

## Why the lib is named "Auth" but lives in `shared/`

The Auth MODULE within Edge owns the *server side* (issuing tokens, KeyCustodian, sessions, etc.). This `D2.Shared.Auth` lib is the *consumer side* — every service that validates tokens, checks scopes, fetches keyrings, etc. Both Edge AND every backend service consume this lib.
