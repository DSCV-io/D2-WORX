<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth

> **Status**: placeholder — not yet implemented.

## Purpose

`Scopes` constants, JWT claim helpers, token primitives, `KeyringClient` (consumes Edge's KeyCustodian endpoint). The shared auth-aware bits used by every service that participates in D²'s auth model.

## Public API surface

- `Scopes` — static class with `const string` members. Naming: `service.resource.action` (three-part, lowercase, dot-separated). E.g., `Scopes.FilesUploadRead = "files.upload.read"`.
- `JwtClaimTypes` — D²-specific custom claim constants, all `d2:`-prefixed(`d2:kind`, `d2:session_id`, etc.)
- `JwtClaimsHelper` — ergonomic readers for D²'s JWT shape (org / org_role / org_type / scope / act chain / etc.)
- `[ImpersonationBlocked]` attribute — applied to scope constants that are stripped from impersonation tokens at JWT mint time
- `KeyringClient` — fetches encryption-domain keyrings from Edge's `internal/keys/{domain}` endpoint; caches in memory; refreshes hourly; live-reloads on `d2.security.key-rotated` event
- `IRequestContext` — request-scoped identity context (used by `BaseHandler` + middleware)
- DI registration: `services.AddD2Auth(jwksUrl)` — registers JWT validation primitives + `KeyringClient`

## Dependencies

- `D2.Shared.Result`
- `D2.Shared.Utilities`
- `Microsoft.IdentityModel.Tokens` + `System.IdentityModel.Tokens.Jwt`
- `Grpc.Net.Client` (KeyringClient calls Edge via gRPC)

## References

- Auth & Security — full architectural context (scope registry, custom-claim namespacing, impersonation, KeyCustodian)
- Code Conventions — scope vs permission terminology
- `docs/JWT-CLAIMS.md` (TBD — created when the first `d2:`-prefixed claim ships)

## Why the lib is named "Auth" but lives in `shared/`

The Auth MODULE within Edge owns the *server side* (issuing tokens, KeyCustodian, sessions, etc.). This `D2.Shared.Auth` lib is the *consumer side* — every service that validates tokens, checks scopes, fetches keyrings, etc. Both Edge AND every backend service consume this lib.
