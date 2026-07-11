<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0012: Self-rolled .NET auth — shared vocabulary, token primitives, and per-transport bindings

- **Status**: Accepted
- **Date**: 2026-05-30 (amended 2026-06-01; outbound-auth re-scope: 2026-06-18)
- **Deliverable**: D2 shared libraries (backfilled)

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

> **Outbound-auth role re-scoped (2026-06-18).** The role these two outbound clients play in the service-to-service model has changed, though both remain built-but-unwired (their only callers are unit tests; no production request flow invokes them, and the code disposition — removal or repurposing — is a later deliverable). **RFC 8693 token exchange (`HttpTokenExchangeClient`) is no longer the per-hop business-call mechanism.** The service-to-service model mints one token at the Edge trust boundary and forwards it unchanged across internal hops, each hop re-validating it; token exchange is retained for the single boundary mint, cross-trust-domain calls, deliberate narrowing exceptions, and impersonation — not as a per-hop tax ([ADR-0022](0022-service-auth-mint-once-forward.md)). **The `client_credentials` service-identity layer (`HttpServiceIdentityClient` / `ServiceIdentityCallCredentials`) is superseded by mTLS workload identity** — workload identity comes from a KeyCustodian-issued client certificate verified on the channel, not from a second forwarded service-identity JWT, which removes both the per-hop service-token mint it implied and the audience-targeting problem a forwarded service-identity token would hit at a strict receiver ([ADR-0023](0023-mtls-workload-identity.md)). The inbound validation surface, RS256/JWKS choice, and `d2_`-claim vocabulary below are unaffected.

### 2. RS256 + JWKS

`JwtValidatorOptions.SR_DefaultValidAlgorithms = ["RS256"]`, pinned in `TokenValidationParameters.ValidAlgorithms` (defends `alg=none` and HMAC-with-public-key confusion). **Not HS256**: a shared HMAC secret known to every backend service is not a secret — RS256 keeps the private signing key only at Edge; services hold only public verify keys from JWKS. **Not EdDSA**: `Microsoft.IdentityModel` JWKS/`JsonWebTokenHandler` support for `kty=OKP`/`crv=Ed25519` is incomplete in .NET 10 as of the foundational shared-library build, and OIDC/JWKS interop for Ed25519 is inconsistent across tooling; RS256 has universal interop including future non-.NET services. JWKS is fetched from Edge's OIDC discovery via `HttpJwksProvider` (wrapping `IConfigurationManager<OpenIdConnectConfiguration>` with a `Singleflight` + cooldown on forced refresh and a `CircuitBreaker` on sustained outage); cluster-wide rotation coherency rides the backplane (`JwksBackplaneSubscriber` refreshes on key-rotated events). `IJwksProvider`/`JwksKeySetSnapshot` live in the abstractions slice so implementations swap without touching consumers.

### 3. `d2_`-prefixed snake_case custom JWT claims

D²-specific claims use a `d2_` prefix in lowercase snake_case (e.g. `d2_session_id`, `d2_username`, `d2_fp`, `d2_org_id`, `d2_org_name`, `d2_org_type`, `d2_org_role`, `d2_step_up_at`); standard claims keep canonical names (`sub`, `aud`, `scope`, `act`, `amr`). The `d2_` namespace avoids collision with standard or future IANA-registered JWT claims. D²'s scope format is dot-separated (e.g. `self.read`) rather than the OAuth `:`-separated convention because `:` collides with JSON-path and URI-encoding conventions in logging/tracing; snake_case claim names are consistent with that segment style and avoid camelCase-to-JSON-key mapping confusion. `JwtClaimTypes` is emitted from the same `contracts/jwt-claims/` spec that drives the TS-side `@d2/auth-abstractions` catalog — cross-language drift is structurally impossible.

### 4. Abstractions/runtime split with per-transport bindings

Five independently referenceable assemblies: `Auth.Abstractions` (vocabulary + codegen catalogs + `IJwksProvider`/`ISessionLivenessTracker`, zero runtime deps), `AuthContext.Abstractions` (`IAuthContext` extensions), `Auth` core (`JwtValidator`, `HttpJwksProvider`, session-liveness tracker, claims→context mapper), `Auth.Http` (`JwtAuthMiddleware` + RFC 7807 helpers + `RequireAnyScope` / `RequireAllScopes` / `MarkAsD2HarmlessEndpoint` fluent extensions), `Auth.Grpc` (`JwtAuthInterceptor` + trailer helpers + `D2RequireAnyScopeAttribute` / `D2RequireAllScopesAttribute` / `D2HarmlessEndpointAttribute` attributes + `RequireAnyScope` / `RequireAllScopes` gRPC fluent extensions), `Auth.Outbound` (token-exchange + service-identity clients + gRPC call credentials). Domain handlers reference only the two abstractions slices; the `IRequestContext` that arrives is populated by the transport layer and injected via a scoped resolver.

### 5. Per-layer validation: transport enforces signature/expiry/audience/liveness + scope match-mode; per-handler enforces scopes as defense-in-depth; deny-by-default boot guard

#### Transport-layer pipeline

`JwtAuthMiddleware` and `JwtAuthInterceptor` run an identical five-step pipeline: harmless-endpoint short-circuit → bearer extraction → JWT validation (RS256 pin, issuer, audience, lifetime with clock skew, reactive-refresh-on-unknown-`kid` + single retry) → session liveness (fail-closed) → per-endpoint scope enforcement with explicit match mode.

**Audience validation is a transport-layer invariant** configured once on `AuthOptions` and applied uniformly — not per-handler, because a handler accepting the wrong audience would accept tokens minted for a different service.

**Scope declaration is now explicit about match semantics.** The original design had a single `RequireD2Scope("…")` / `[D2RequireScope("…")]` surface that used an implicit any-of semantic — the footgun was that a handler needing all-of silently got any-of and the error was invisible at the declaration site. The shipped design removes the ambiguity: every endpoint declares its match mode at the call site:

- **HTTP fluent**: `.RequireAnyScope("scope1", "scope2")` (any-of) or `.RequireAllScopes("scope1", "scope2")` (all-of) on `IEndpointConventionBuilder`. Metadata type: `EndpointScopeMetadata` with `Match = ScopeMatch.Any` or `ScopeMatch.All`.
- **gRPC attribute**: `[D2RequireAnyScope("scope1")]` or `[D2RequireAllScopes("scope1", "scope2")]` on the service class or method (method-level overrides class-level: last-declared-wins over the metadata collection). Metadata type resolved from attributes: `MethodScopeMetadata`.
- **gRPC fluent**: `.RequireAnyScope("…")` / `.RequireAllScopes("…")` on the gRPC service builder. Produces `MethodScopeMetadata` directly (fluent takes precedence over attribute path).
- **Harmless bypass**: `.MarkAsD2HarmlessEndpoint()` / `[D2HarmlessEndpoint]` opts out of the full pipeline (k8s probes, OIDC discovery endpoints). `[AllowAnonymous]` is deliberately NOT recognized — its semantic ties to the BCL `AuthorizationMiddleware` chain that this stack bypasses.

`ScopeMatch.Any` and `ScopeMatch.All` live in `D2.Shared.Auth.Abstractions`; `MethodScopeMetadata` mirrors `EndpointScopeMetadata` by type (namespace-distinct so per-transport options can grow independently).

The gRPC interceptor covers all four server handler kinds via a single `RunAuthAsync` entry point (streaming methods cannot bypass auth by omission) and dual-writes `IRequestContext` to both `ServerCallContext.UserState` and `HttpContext.Items`. All failures surface a uniform 401 / `StatusCode.Unauthenticated`; granularity is communicated only via a `d2_error_code` trailer/ProblemDetails field, never via distinct HTTP/gRPC status codes (no structural information leaked to an unauthenticated caller).

#### Per-handler scope check (defense-in-depth)

`BaseHandler.RunCorePipelineAsync` can perform a redundant scope check before `ExecuteAsync` runs, declared via `HandlerOptions.ScopeRequirement`. The type is `ScopeRequirement(HandlerScopeMatch Match, IReadOnlySet<string> Scopes)`:

- `HandlerScopeMatch.Any` / `HandlerScopeMatch.All` mirror the transport enum but live in `D2.Shared.Handler.Abstractions` so handler assemblies carry no compile-time dependency on the auth layer (layer-hygiene invariant).
- A `null` `ScopeRequirement` (the default) disables the per-handler check entirely — any authenticated caller that passed the transport layer invokes the handler.
- An empty `Scopes` set is rejected at construction time — the `ScopeRequirement` constructor throws `ArgumentException` if `Scopes` is empty. Pass a `null` `ScopeRequirement` to disable the per-handler check. The `is { Scopes.Count: > 0 }` pipeline guard remains as defense-in-depth for a now-unreachable branch.

The per-handler check returns `D2Result.Forbidden` on mismatch. Its role is defense-in-depth: if a handler is wired to a new endpoint that accidentally omits the transport-layer declaration, the per-handler check still rejects under-scoped callers. It is NOT a substitute for the transport declaration — both layers should declare.

#### Deny-by-default boot guard

A misconfigured endpoint — one whose declaration was omitted — silently admits any authenticated caller at runtime. To surface this class of error before any traffic is served, `D2.Shared.Auth.Startup` ships `AuthEndpointGuardStartupFilter`, an `IStartupFilter` that walks the fully-populated `EndpointDataSource` **at host startup** (inside `GenericWebHostService.StartAsync`, after middleware pipeline construction and before Kestrel accepts connections) and fails the host with `InvalidOperationException` when any `RouteEndpoint` lacks a declared auth intent.

"Declared auth intent" is any of: `EndpointScopeMetadata` (HTTP fluent path), `MethodScopeMetadata` (gRPC fluent path), `[D2RequireAnyScopeAttribute]`, `[D2RequireAllScopesAttribute]`, or `[D2HarmlessEndpointAttribute]` on the endpoint's metadata collection.

Exempt from the guard:
- Endpoints whose route pattern matches the canonical infrastructure path set (`/health`, `/alive`, `/metrics`, `/.well-known` — via `D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS` + `InfrastructurePathMatcher`).
- Non-`RouteEndpoint` entries (no route pattern, can't be guarded by convention).
- gRPC infrastructure catch-all endpoints registered by `MapGrpcService<T>()` for unknown-method / unknown-service routing, identified by the `grpcunimplemented` route constraint in the route pattern parameters.

The guard is wired by `ServiceDefaults` via `AddD2AuthEndpointGuard` (registered as a transient `IStartupFilter`, idempotent via `TryAddEnumerable`). Opt-out is `D2ServiceDefaultsOptions.SkipAuthEndpointGuard = true` — intended for test hosts that register synthetic endpoints without auth declarations or anonymous-only admin tools.

The `IStartupFilter` lifecycle was chosen over `IHostedService` because, in the `WebApplication` model, `app.MapXxx()` calls happen before `StartAsync` and write into `WebApplication.DataSources` — sources that are merged into the DI-resolved `EndpointDataSource` composite during pipeline construction (the `next` chain in `IStartupFilter.Configure`). An `IHostedService` captures the `EndpointDataSource` singleton at DI-resolve time, which is before that merge, so it sees an empty collection.

## Consequences

**Positive.**

- Scope strings, audience URLs, claim names, and slot keys are defined once in specs and compiled into every consumer; a rename triggers the codegen safety net (ADR-0002).
- RS256 + JWKS means no shared secret distributed to backend services; rotation is zero-downtime via the backplane event + reactive-refresh backstop.
- Domain handlers are fully decoupled from the transport auth stack — `IRequestContext.Scopes` / `HasScope(...)` compile and test without any reference to the validator or middleware.
- Where token exchange applies — the boundary mint and the retained exceptions, no longer the per-hop default ([ADR-0022](0022-service-auth-mint-once-forward.md)) — it propagates user identity without re-issuing credentials, and the `d2_session_id`-keyed cache enables per-session revocation across cached exchange tokens.
- The per-transport split means HTTP-only services do not pay the gRPC dependency and vice versa.
- Scope match-mode is stated explicitly at the declaration site (`RequireAnyScope` / `RequireAllScopes`); the footgun of an implicit any-of semantic on multi-scope declarations is structurally eliminated — `RequireAllScopes("a","b")` and `RequireAnyScope("a","b")` are different call sites with different names.
- The boot guard converts a class of silent runtime misconfiguration (undeclared endpoint silently admitting any authenticated caller) into a fast, deterministic startup failure before traffic is ever served.

**Negative / risks.**

- The shared-lib auth surface has a hard runtime dependency on Edge being reachable at startup (JWKS discovery) and validation time (liveness). The circuit breaker + fail-closed semantics mitigate sustained outage, but a cold start with Edge unreachable rejects all authenticated requests until a JWKS snapshot is obtained.
- Two transport bindings implement structurally identical pipelines (ASP.NET cannot share one base across middleware + interceptor); any future validation step must be added to both.
- Self-rolling RFC 8693 means the Edge auth module (future ADR) must implement the server side correctly; deviation breaks the exchange client at runtime, not compile time.
- `d2_` snake_case claims are non-standard: a future non-.NET service must implement its own parser for the `d2_` set rather than relying on an off-the-shelf OIDC library's standard mapping.
- The boot guard exempts infrastructure paths and gRPC catch-alls by convention; if a future framework version introduces a new class of infrastructure endpoint with a non-matching route pattern, it will trip the guard until the exemption list is updated.

## Alternatives considered

**Off-the-shelf IdP product/SDK (OpenIddict, Duende IdentityServer).** These are authorization-server frameworks — they implement the OAuth/OIDC server side. Adopting one would put the Edge auth module on the framework's session/token-minting abstractions; the upside is a battle-tested server, the downside is opinionated data models (OpenIddict's EF Core schema; Duende's license + data model) that conflict with D²'s session three-tier architecture and intent to own `d2-auth`. Neither addresses the scope/claim codegen requirement nor the gRPC interceptor gap. Deferred — revisiting OpenIddict for the *server-side token-minting* layer of the future Edge module is viable without disturbing this shared-lib surface.

**HS256 shared secret.** Every validating service holds the same key; a compromise of any service exposes signing material for the whole cluster. Rejected unconditionally.

**EdDSA (Ed25519).** Shorter/faster signatures, but incomplete `Microsoft.IdentityModel` + JWKS ecosystem interop for `OKP` keys at the time the shared-library auth surface was built. Revisitable when support matures.

**Standard camelCase claim names.** Conventional in some ecosystems, but D²'s dot-separated scope format and mixed-stack log/dashboard disambiguation favor `d2_`-prefixed snake_case (consistent with the scope segment style; avoids camelCase-to-JSON-key mapping confusion).

**Validate the JWT per-handler.** Considered to let handlers override `ValidAudience`/`ValidAlgorithms`. Rejected: audience validation is a service-identity invariant, not a per-operation concern; pushing it per-handler creates a footgun where a handler that omits the check silently accepts cross-service tokens. `ScopeRequirement` (per-handler defense-in-depth) is the per-handler concern; audience/signature/expiry/liveness are transport-layer invariants.

**Single `RequireD2Scope` with implicit any-of (original design).** The original surface had one declaration verb for all scope endpoints — any multi-scope declaration silently used any-of semantics. The problem: a caller needing `files.read` AND `files.write` simultaneously could pass with only one, and the author had no call-site signal that the check was any-of rather than all-of. The shipped split into `RequireAnyScope` / `RequireAllScopes` with distinct method names makes the match mode visible at every declaration site — the any-of / all-of distinction is impossible to express incorrectly by omission.

**`IHostedService` for the boot guard.** An `IHostedService` starts after pipeline construction and would need a `BackgroundService.ExecuteAsync` that inspects the `EndpointDataSource` and calls `IHostApplicationLifetime.StopApplication()` on violation. The `IStartupFilter` approach is more direct: it runs inside `GenericWebHostService.StartAsync` (the same call that returns the host), throws `InvalidOperationException` to abort startup before Kestrel binds, and does not require the secondary `IHostApplicationLifetime` dance. The `IStartupFilter` path also guarantees the guard runs before the first request byte is processed in both the `WebApplication` and generic-host models.

## References

- `server/shared/dotnet/auth/abstractions/ScopeMatch.cs` (transport-layer `ScopeMatch` enum); `auth/http/Endpoints/EndpointScopeMetadata.cs` + `RequireD2ScopeExtensions.cs` (`RequireAnyScope` / `RequireAllScopes` / `MarkAsD2HarmlessEndpoint` fluent); `auth/http/Middleware/JwtAuthMiddleware.cs`; `auth/grpc/Endpoints/MethodScopeMetadata.cs` + `D2RequireAnyScopeAttribute.cs` + `D2RequireAllScopesAttribute.cs` + `RequireD2GrpcScopeExtensions.cs`; `auth/grpc/Interceptors/JwtAuthInterceptor.cs`; `auth/startup/AuthEndpointGuardStartupFilter.cs` + `AuthEndpointGuardServiceCollectionExtensions.cs` (boot guard); `service-defaults/D2ServiceDefaultsOptions.cs` (`SkipAuthEndpointGuard` opt-out).
- `server/shared/dotnet/handler/abstractions/HandlerScopeMatch.cs` + `ScopeRequirement.cs` + `HandlerOptions.cs` (per-handler defense-in-depth); `handler/core/BaseHandler.cs` (`RunCorePipelineAsync` scope pre-check).
- `server/shared/dotnet/auth/abstractions/` (vocabulary + codegen catalogs + `IJwksProvider`/`ISessionLivenessTracker`); `auth/core/Validation/JwtValidator.cs` + `JwtValidatorOptions.cs` (RS256 pin); `auth/outbound/TokenExchange/HttpTokenExchangeClient.cs` + `ServiceIdentity/HttpServiceIdentityClient.cs`.
- `contracts/jwt-claims/`, `contracts/auth-scopes/`, `contracts/auth-audiences/`, `contracts/in-process-keys/`.
- `docs/PATTERNS.md` (JWT inbound auth + deny-by-default boot guard); RFC 6749, 6750, 7519, 7517, 8693.
- [ADR-0002](0002-spec-driven-codegen.md) (the codegen pattern governing `Scopes`/`Audiences`/`JwtClaimTypes`), [ADR-0005](0005-handler-pipeline.md) (per-handler `ScopeRequirement` defense-in-depth), [ADR-0006](0006-abstractions-implementation-split.md) (vocabulary in `Abstractions`, validation in `core`), [ADR-0003](0003-d2result-errors-as-values.md) (`D2Result` throughout the auth stack), [ADR-0004](0004-i18n-tkmessage.md) (`TKMessage` referenced by `AuthFailures`), [ADR-0007](0007-request-context-propagation.md) (`IRequestContext` populated by the transport layer).
- [ADR-0022](0022-service-auth-mint-once-forward.md) — the service-to-service auth model that re-scopes this surface's outbound clients: token exchange moves off the per-hop path to the boundary mint and deliberate exceptions, and an internal hop forwards the once-minted token rather than exchanging.
- [ADR-0023](0023-mtls-workload-identity.md) — mTLS workload identity, which supersedes the `client_credentials` service-identity layer described above: workload identity comes from a KeyCustodian-issued channel certificate, not a second forwarded JWT.
