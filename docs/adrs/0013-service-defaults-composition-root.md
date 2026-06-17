<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0013: ServiceDefaults — thin-aggregator composition root with a locked middleware order

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: D2 shared libraries (backfilled)

## Context

Every service requires the same foundational cross-cutting stack: structured PII-safe logging, OpenTelemetry, i18n, the handler pipeline, RS256/JWKS inbound auth (HTTP and gRPC), in-process L1 caching, health endpoints, RFC 7807 problem details, and CORS. Without a shared wiring point each service hand-rolls `Program.cs`, creating three failure modes: (1) middleware installed in the wrong order (auth before CORS, infra-bypass before routing) produces silent security/correctness regressions; (2) new defaults added to a shared lib silently miss every existing service until someone propagates them; (3) per-service configuration for owning-lib options (issuer URIs, CORS origins, log levels) duplicates and drifts.

The abstractions/implementation split (ADR-0006), observability (ADR-0010), and inbound auth (ADR-0012) each defined the `AddD2X` / `UseD2X` extension shape; the only remaining question is how those extensions are assembled at the service boundary. The .NET Aspire `Microsoft.Extensions.ServiceDefaults` pattern — a dedicated aggregator csproj that delegates entirely to owning libraries — is the direct inspiration.

## Decision

A single thin-aggregator csproj (`D2.Shared.ServiceDefaults`) owns the composition of all foundational shared libs. **The aggregator owns zero logic and zero configuration knowledge. Middleware ordering is locked and exposes no insertion points.**

### 1. Thin aggregator with zero logic and pass-through delegates

`AddD2ServiceDefaults` is a sequenced list of `services.AddD2X(...)` calls and nothing else (the class comment: *"THIN AGGREGATOR — ZERO logic of its own"*); its constants file is intentionally empty (*"this aggregator owns ZERO logic and reads no env vars of its own"*). Per-component configurability is entirely through pass-through `Action<TFromOwningLib>?` delegates on `D2ServiceDefaultsOptions`, each forwarded verbatim to the owning lib's parameter — so new options on any owning lib surface at the aggregator's call site automatically with no aggregator-side maintenance. The wiring order is fixed: `D2Env.Load` → `AddD2Logging` → `AddD2Telemetry` → `AddD2I18n` → `AddD2Handler` → `AddD2Auth`(+`.Http`+`.Grpc`) → `AddD2LocalCache` → `AddD2HealthChecks` → `AddD2ProblemDetails` → `AddD2Cors`.

### 2. Locked middleware order with no insertion points

`UseD2DefaultPipeline` installs a single, non-customizable sequence: `UseD2SecurityHeaders → UseD2RequestLogging → UseD2Cors → UseRouting → UseD2InfrastructureBypass → UseAuthentication → UseD2Auth → UseAuthorization`. The ordering is correctness-critical, with five recorded reasons: security headers first (so OWASP headers — incl. HSTS only on HTTPS, preload opt-in only as a one-way door — attach to every response including short-circuits); request logging before routing (so early-pipeline failures still emit a completion line); CORS before routing (so OPTIONS preflights short-circuit before verb-specific endpoint matching); routing before infra-bypass (bypass invokes the routing-resolved `Endpoint.RequestDelegate` to skip heavy business middleware on `/health`, `/alive`, `/metrics`, `/.well-known` — without prior routing there is no endpoint to invoke); authentication → auth → authorization (the JWT middleware needs the ASP.NET authentication feature set before it can populate `IRequestContext`, which authorization then reads). No insertion points are exposed; services needing bespoke ordering call the underlying lib extensions directly and skip the aggregator's pipeline method. The canonical `InfrastructurePathMatcher` is the single source of truth for the infra path set, shared across logging, telemetry, and bypass.

### 3. Fail-fast composition

Two fail-closed behaviors are baked in at host build, not deferred to request time: **auth wiring** — when `SkipAuthAutoWiring = false` (the default), `AuthConfigure` must be non-null or the aggregator throws at registration time (synchronously inside `AddD2ServiceDefaults`) with a remediation message (prevents accidentally shipping unauthenticated); **CORS origins** — `AddD2Cors` reads the indexed `D2_CORS_ORIGINS__*` env convention and `ValidateOnStart()` fails the host on an empty list (and rejects `AllowCredentials` + wildcard origin). `RunD2ServiceAsync` adds a PII-safe startup-failure fence: `Log.Fatal` emits only `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)`, never `ex.Message`. A service's `Program.cs` is consequently ~four calls: `AddD2ServiceDefaults(config, opts => { opts.AuthConfigure = ...; })`, `UseD2DefaultPipeline()`, `MapD2DefaultEndpoints()` (`/health` full + `/alive` live-tag + `/metrics` IP-restricted, `OTEL_SDK_DISABLED`-gated), `RunD2ServiceAsync("name")`.

## Consequences

**Positive.**

- A new service is fully wired (logging, telemetry, auth, caching, health, CORS) in ~10–15 lines of service-specific `Program.cs`; correctness of the cross-cutting stack is guaranteed by construction.
- New owning-lib options surface automatically at the call site via pass-through delegates — no aggregator maintenance pass when owning libs evolve.
- The locked pipeline eliminates an entire bug class (auth after authorization, headers skipped on preflights, infra-bypass before routing) that reappears whenever an engineer wires a new service from scratch.
- Fail-fast auth + CORS validation catches misconfiguration in CI (host build fails) rather than production.

**Negative / risks.**

- Services with genuinely non-standard ordering (e.g. a future Edge rate-limit layer between infra-bypass and authentication) cannot use `UseD2DefaultPipeline` and must hand-wire from the underlying lib extensions (the README acknowledges this; the rate-limit middleware will slot between infra-bypass and authentication when it ships).
- The locked order has no in-band override: a consumer needing a single insertion must forgo the aggregator entirely — there is no partial use.
- The aggregator csproj pulls the full ASP.NET Core web SDK graph into every referencing service, including services that might otherwise target a smaller SDK — intentional, but a transitive dependency cost.
- The opt-out flags (`SkipAuthAutoWiring`, `SkipLocalCacheAutoWiring`) require explicit consumer awareness; a test host that omits the opt-out wires auth and fail-fasts without an `AuthConfigure` delegate — intended, but can surprise first-time contributors.

## Alternatives considered

**Each service hand-wires its own pipeline.** The pre-aggregator state: every `Program.cs` calls the extensions independently. Requires every author to know the correct order, propagate new defaults manually, and duplicate opt-in/opt-out logic; the middleware-ordering bug class is live at every new service.

**A configurable pipeline with insertion points.** `UseD2DefaultPipeline` could accept a delegate to insert middleware between named stages. This avoids "forgo the aggregator entirely" for non-standard cases but turns the locked-order correctness guarantee into a best-effort suggestion — a consumer could insert auth-affecting middleware before `UseAuthentication` with no compile-time protection — and the insertion API complexity grows with the stage count.

**A fat base `Program` class with virtual hooks.** Inherit from a base that calls the extensions and exposes virtual overrides. Composes poorly with the ASP.NET host-builder model (not inheritance-friendly), obscures call order from the author, and tightly couples to a base type the aggregator must maintain.

## References

- `server/shared/dotnet/service-defaults/` — `ServiceDefaultsServiceCollectionExtensions.cs` (`AddD2ServiceDefaults`), `WebApplicationServiceDefaultsExtensions.cs` (`UseD2DefaultPipeline`/`MapD2DefaultEndpoints`/`RunD2ServiceAsync`), `D2ServiceDefaultsOptions.cs` (pass-through delegates + opt-out flags), `D2ServiceDefaultsConstants.cs` (intentionally empty), README.
- `server/shared/dotnet/aspnetcore/` — `SecurityHeadersApplicationBuilderExtensions.cs`, `CorsServiceCollectionExtensions.cs` (fail-closed `ValidateOnStart`), `InfrastructureBypassApplicationBuilderExtensions.cs`, `InfrastructurePathMatcher.cs`, `RunD2ServiceWebApplicationExtensions.cs` (PII-safe `Log.Fatal`).
- `docs/PATTERNS.md` (Composition root section).
- [ADR-0006](0006-abstractions-implementation-split.md) (the layering this composes), [ADR-0010](0010-observability-dual-enrichment.md) (`AddD2Logging` + `AddD2Telemetry`), [ADR-0012](0012-self-rolled-dotnet-auth.md) (`AddD2Auth` + `.Http` + `.Grpc`).
