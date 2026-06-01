<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0012: Self-rolled .NET auth — shared vocabulary, token primitives, and per-transport bindings

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: Phase 0 — shared libraries (backfilled)

> **Scope:** this ADR covers the BUILT shared-library auth surface (`server/shared/dotnet/auth/`). The **Edge auth module** — login flows, session three-tier storage, OAuth client registry, KeyCustodian — is not built yet and will receive its own ADR.

## Context

Every inbound HTTP request and gRPC call must be authenticated and authorized before a handler sees it. Constraints:

- **Cross-service identity propagation.** A token minted at Edge travels downstream to Files, Notifications, Courier, Audit, and future services, each validating independently. No shared symmetric secret can cross service boundaries — a secret held by N services is not secret.
- **Spec-driven discipline.** Auth constants — scope strings, audience URLs, JWT claim names, in-process slot keys — are shared vocabulary and must get the same structural-drift-proof codegen treatment (ADR-0002) used for i18n keys, messaging constants, and error codes.
- **Abstractions/implementation split (ADR-0006).** Domain code reads identity through `IAuthContext`/`IRequestContext` and never imports the concrete validation stack.
- **No ready-made IdP SDK fit.** Microsoft's `AddJwtBearer` pipeline is coupled to the ASP.NET `AuthenticationMiddleware` + `[Authorize]` flow; it does not cover gRPC interceptors on the same code path, the bespoke RFC 8693 actor-chain claim shape, or a Roslyn-native way to enforce that every per-handler scope reference is a compile-time constant from a spec-driven catalog.

## Decision

### 1. Self-rolled auth primitives with codegen catalogs as the spec-driven vocabulary

Auth vocabulary is defined in JSON contracts and emitted by dedicated Roslyn source-gens: `contracts/auth-scopes/` → `Scopes` (nested `const` dot-path tree, a `GrantedScopes` dictionary keyed on `(OrgType, Role)`, `ActionSensitivity` lookup, `IsImpersonationBlocked` — with build-time scope-name validation); `contracts/auth-audiences/` → `Audiences` (URL constants + `IsKnown`/`Resolve`); `contracts/jwt-claims/` → `JwtClaimTypes` (standard OAuth/OIDC + `d2_`-prefixed custom claims + inside-act claims); `contracts/in-process-keys/` → `D2HttpContextItems`. The hand-written identity vocabulary (`OrgType`, `Role`, `ActorKind`, `ImpersonationKind`, `ActionSensitivity`, `ActorEntry`) lives alongside in `auth/abstractions/` — small, stable, domain-meaningful enums where codegen would add complexity without drift-risk payoff. Outbound token acquisition is hand-rolled against Edge's OIDC token endpoint: RFC 6749 §4.4 `client_credentials` (`HttpServiceIdentityClient`, in-process cache + proactive refresh) and RFC 8693 token exchange (`HttpTokenExchangeClient`, results cached keyed on `(d2_session_id, targetAudience, scope-set)` so session-revoke can invalidate); both dedup cold-start via `Singleflight` (ADR-0014). No third-party IdP SDK (OpenIddict, Duende, Auth0) is introduced.

### 2. RS256 + JWKS

`JwtValidatorOptions.SR_DefaultValidAlgorithms = ["RS256"]`, pinned in `TokenValidationParameters.ValidAlgorithms` (defends `alg=none` and HMAC-with-public-key confusion). **Not HS256**: a shared HMAC secret known to every backend service is not a secret — RS256 keeps the private signing key only at Edge; services hold only public verify keys from JWKS. **Not EdDSA**: `Microsoft.IdentityModel` JWKS/`JsonWebTokenHandler` support for `kty=OKP`/`crv=Ed25519` is incomplete in .NET 10 as of Phase 0, and OIDC/JWKS interop for Ed25519 is inconsistent across tooling; RS256 has universal interop including future non-.NET services. JWKS is fetched from Edge's OIDC discovery via `HttpJwksProvider` (wrapping `IConfigurationManager<OpenIdConnectConfiguration>` with a `Singleflight` + cooldown on forced refresh and a `CircuitBreaker` on sustained outage); cluster-wide rotation coherency rides the backplane (`JwksBackplaneSubscriber` refreshes on key-rotated events). `IJwksProvider`/`JwksKeySetSnapshot` live in the abstractions slice so implementations swap without touching consumers.

### 3. `d2_`-prefixed snake_case custom JWT claims

D²-specific claims use a `d2_` prefix in lowercase snake_case (e.g. `d2_session_id`, `d2_username`, `d2_fp`, `d2_org_id`, `d2_org_name`, `d2_org_type`, `d2_org_role`, `d2_step_up_at`); standard claims keep canonical names (`sub`, `aud`, `scope`, `act`, `amr`). The `d2_` namespace avoids collision with standard or future IANA-registered JWT claims. D²'s scope format is dot-separated (e.g. `self.read`) rather than the OAuth `:`-separated convention because `:` collides with JSON-path and URI-encoding conventions in logging/tracing; snake_case claim names are consistent with that segment style and avoid camelCase-to-JSON-key mapping confusion. `JwtClaimTypes` is emitted from the same `contracts/jwt-claims/` spec that drives the TS-side `@d2/auth-abstractions` catalog — cross-language drift is structurally impossible.

### 4. Abstractions/runtime split with per-transport bindings

Five independently referenceable assemblies: `Auth.Abstractions` (vocabulary + codegen catalogs + `IJwksProvider`/`ISessionLivenessTracker`, zero runtime deps), `AuthContext.Abstractions` (`IAuthContext` extensions), `Auth` core (`JwtValidator`, `HttpJwksProvider`, session-liveness tracker, claims→context mapper), `Auth.Http` (`JwtAuthMiddleware` + RFC 7807 helpers + `RequireD2Scope`), `Auth.Grpc` (`JwtAuthInterceptor` + trailer helpers + `D2RequireScopeAttribute`), `Auth.Outbound` (token-exchange + service-identity clients + gRPC call credentials). Domain handlers reference only the two abstractions slices; the `IRequestContext` that arrives is populated by the transport layer and injected via a scoped resolver.

### 5. Per-layer validation: transport enforces signature/expiry/audience/liveness; per-handler enforces scopes

`JwtAuthMiddleware` and `JwtAuthInterceptor` run an identical five-step pipeline: harmless-endpoint short-circuit → bearer extraction → JWT validation (RS256 pin, issuer, audience, lifetime with clock skew, reactive-refresh-on-unknown-`kid` + single retry) → session liveness (fail-closed) → per-endpoint any-of scope enforcement. **Audience validation is a transport-layer invariant** configured once on `AuthOptions` and applied uniformly — not per-handler, because a handler accepting the wrong audience would accept tokens minted for a different service. **`RequiredScopes` varies by operation** and is declared at the endpoint/method site (`.RequireD2Scope("self.read")` / `[D2RequireScope]`). The gRPC interceptor covers all four server handler kinds via one `RunAuthAsync` (streaming methods cannot bypass auth by omission) and dual-writes `IRequestContext` to both `ServerCallContext.UserState` and `HttpContext.Items`. All failures surface a uniform 401; granularity is communicated only via a `d2_error_code` trailer/ProblemDetails field, not distinct status codes (no structural information leaked to an unauthenticated caller).

## Consequences

**Positive.**

- Scope strings, audience URLs, claim names, and slot keys are defined once in specs and compiled into every consumer; a rename triggers the codegen safety net (ADR-0002).
- RS256 + JWKS means no shared secret distributed to backend services; rotation is zero-downtime via the backplane event + reactive-refresh backstop.
- Domain handlers are fully decoupled from the transport auth stack — `IRequestContext.Scopes` / `HasScope(...)` compile and test without any reference to the validator or middleware.
- Token exchange propagates user identity downstream without re-issuing credentials; the `d2_session_id`-keyed cache enables per-session revocation across cached exchange tokens.
- The per-transport split means HTTP-only services do not pay the gRPC dependency and vice versa.

**Negative / risks.**

- The shared-lib auth surface has a hard runtime dependency on Edge being reachable at startup (JWKS discovery) and validation time (liveness). The circuit breaker + fail-closed semantics mitigate sustained outage, but a cold start with Edge unreachable rejects all authenticated requests until a JWKS snapshot is obtained.
- Two transport bindings implement structurally identical pipelines (ASP.NET cannot share one base across middleware + interceptor); any future validation step must be added to both.
- Self-rolling RFC 8693 means the Edge auth module (future ADR) must implement the server side correctly; deviation breaks the exchange client at runtime, not compile time.
- `d2_` snake_case claims are non-standard: a future non-.NET service must implement its own parser for the `d2_` set rather than relying on an off-the-shelf OIDC library's standard mapping.

## Alternatives considered

**Off-the-shelf IdP product/SDK (OpenIddict, Duende IdentityServer).** These are authorization-server frameworks — they implement the OAuth/OIDC server side. Adopting one would put the Edge auth module on the framework's session/token-minting abstractions; the upside is a battle-tested server, the downside is opinionated data models (OpenIddict's EF Core schema; Duende's license + data model) that conflict with D²'s session three-tier architecture and intent to own `auth_db`. Neither addresses the scope/claim codegen requirement nor the gRPC interceptor gap. Deferred — revisiting OpenIddict for the *server-side token-minting* layer of the future Edge module is viable without disturbing this shared-lib surface.

**HS256 shared secret.** Every validating service holds the same key; a compromise of any service exposes signing material for the whole cluster. Rejected unconditionally.

**EdDSA (Ed25519).** Shorter/faster signatures, but incomplete `Microsoft.IdentityModel` + JWKS ecosystem interop for `OKP` keys in Phase 0. Revisitable when support matures.

**Standard camelCase claim names.** Conventional in some ecosystems, but D²'s dot-separated scope format and mixed-stack log/dashboard disambiguation favor `d2_`-prefixed snake_case (consistent with the scope segment style; avoids camelCase-to-JSON-key mapping confusion).

**Validate the JWT per-handler.** Considered to let handlers override `ValidAudience`/`ValidAlgorithms`. Rejected: audience validation is a service-identity invariant, not a per-operation concern; pushing it per-handler creates a footgun where a handler that omits the check silently accepts cross-service tokens. `RequiredScopes` is the per-handler concern; audience/signature/expiry/liveness are transport-layer invariants.

## References

- `server/shared/dotnet/auth/abstractions/` (vocabulary + codegen catalogs + `IJwksProvider`/`ISessionLivenessTracker`); `auth/core/Validation/JwtValidator.cs` + `JwtValidatorOptions.cs` (RS256 pin); `auth/http/Middleware/JwtAuthMiddleware.cs`; `auth/grpc/Interceptors/JwtAuthInterceptor.cs`; `auth/outbound/TokenExchange/HttpTokenExchangeClient.cs` + `ServiceIdentity/HttpServiceIdentityClient.cs`.
- `contracts/jwt-claims/`, `contracts/auth-scopes/`, `contracts/auth-audiences/`, `contracts/in-process-keys/`.
- `docs/PATTERNS.md` (JWT inbound auth); RFC 6749, 6750, 7519, 7517, 8693.
- [ADR-0002](0002-spec-driven-codegen.md) (the codegen pattern governing `Scopes`/`Audiences`/`JwtClaimTypes`), [ADR-0006](0006-abstractions-implementation-split.md) (vocabulary in `Abstractions`, validation in `core`), [ADR-0003](0003-d2result-errors-as-values.md) (`D2Result` throughout the auth stack), [ADR-0004](0004-i18n-tkmessage.md) (`TKMessage` referenced by `AuthFailures`), [ADR-0007](0007-request-context-propagation.md) (`IRequestContext` populated by the transport layer).
