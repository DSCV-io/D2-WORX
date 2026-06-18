<!--
Copyright (c) DCSV. All rights reserved.
-->

# auth/

> Parent: [`server/shared/dotnet/`](../README.md)

Authentication and authorization building blocks for D²-WORX services that issue, validate, or act on D² bearer tokens. The cluster spans the domain-safe vocabulary slice (enums, scope / audience / claim catalogs, the read-only JWKS and session-liveness contracts), the inbound runtime that validates tokens and projects claims into the request context, the per-transport bindings (HTTP middleware, gRPC interceptor), the outbound token-acquisition complement (RFC 8693 token exchange — the Edge boundary mint plus the deliberate exception cases; service-to-service workload identity is mTLS, not a forwarded bearer), and the spec-driven source generators that emit the scope / audience / claim / error-code catalogs from `contracts/`. Domain code references the abstractions; hosts wire the runtime plus the transport bindings they need.

## Packages

| Package                                                | Description                                                                                                                                       |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md)              | Identity / authorization vocabulary plus consumer-side runtime contracts — enums, codegen-emitted `Scopes` / `Audiences` / `JwtClaimTypes` / `D2HttpContextItems`, the read-only `IJwksProvider` / `ISessionLivenessTracker` interfaces. |
| [`context-abstractions/`](context-abstractions/README.md) | Domain-safe slice of the request context — codegen-emitted `IAuthContext` plus hand-written `IAuthContextExtensions`.                            |
| [`core/`](core/README.md)                              | Inbound auth runtime — `AddD2Auth` composition root, JWT validation, claims-to-context mapping, JWKS provider, session liveness tracking.        |
| [`http/`](http/README.md)                              | HTTP-transport binding — `JwtAuthMiddleware`, RFC 7807 ProblemDetails emit point, per-endpoint scope metadata.                                   |
| [`grpc/`](grpc/README.md)                              | gRPC-transport binding — server-side `JwtAuthInterceptor`, `RpcException` trailer triple, per-method scope metadata.                             |
| [`outbound/`](outbound/README.md)                      | Outbound auth runtime — `ITokenExchangeClient` (RFC 8693, the boundary mint + the exception cases) + `IServiceIdentityClient` (`client_credentials`), with caching. The `client_credentials` service-identity layer is superseded by mTLS workload identity ([ADR-0023](../../../../docs/adrs/0023-mtls-workload-identity.md)); both clients are built but wired into no request flow today (their disposition is a later deliverable). |
| [`scopes-source-gen/`](scopes-source-gen/README.md)    | Roslyn generator emitting the `Scopes.*` constant tree into `abstractions/` from `contracts/auth-scopes/scopes.spec.json`.                       |
| [`audiences-source-gen/`](audiences-source-gen/README.md) | Roslyn generator emitting the `Audiences.*` const-string catalog into `abstractions/` from `contracts/auth-audiences/audiences.spec.json`.    |
| [`jwt-claims-source-gen/`](jwt-claims-source-gen/README.md) | Roslyn generator emitting `JwtClaimTypes.g.cs` into `abstractions/` from `contracts/jwt-claims/jwt-claims.spec.json`.                         |
| [`error-codes-source-gen/`](error-codes-source-gen/README.md) | Roslyn generator emitting the auth-failure taxonomy (`AuthErrorCodes` + `AuthFailures` factories) into `core/` from `contracts/auth-error-codes/auth-error-codes.spec.json`. |
