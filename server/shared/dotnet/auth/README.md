<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth

> Parent: [`server/shared/dotnet/`](../README.md)

Inbound auth runtime — JWT validation primitives, JWKS snapshot management, session liveness checking, and the DI composition root that wires it all into a service. Consumed by every backend service that validates inbound bearer tokens.

The vocabulary slice (enums, `ActorEntry`, `IJwksProvider`, `ISessionLivenessTracker`, codegen-emitted `Scopes` / `Audiences` / `JwtClaimTypes` / `D2HttpContextItems`) lives in the sibling [`D2.Shared.Auth.Abstractions`](../auth-abstractions/) project. Wire-protocol header constants live in the per-transport `D2.Shared.Headers.{Common,Http,Amqp,Grpc}` catalogs. Domain code references those, never this runtime lib.

The transport bindings live in two sibling csprojs: [`D2.Shared.Auth.Http`](../auth-http/) (HTTP middleware + RFC 7807 ProblemDetails + per-endpoint scope metadata) and [`D2.Shared.Auth.Grpc`](../auth-grpc/) (server-side gRPC interceptor + RpcException trailers + per-method scope metadata / attributes). Either or both can be wired into a host independently — see [Composing with siblings](#composing-with-siblings) below.

The token-acquisition complement (RFC 8693 token exchange + RFC 6749 §4.4 client credentials, machine-to-machine call credentials) lives in [`D2.Shared.Auth.Outbound`](../auth-outbound/).

## Public API surface

### Composition

```csharp
services.AddD2Auth(opts =>
{
    opts.Issuer = new Uri("https://edge.internal");
    opts.Audience = Audiences.MyService;        // codegen constant
    opts.ClockSkew = TimeSpan.FromSeconds(30);  // optional, default 30s
});
```

`AddD2Auth(IServiceCollection, Action<AuthOptions>)` registers the inbound runtime: options binding (with HTTPS-issuer + non-empty-audience validation), the named OIDC discovery `HttpClient`, the OIDC `IConfigurationManager<OpenIdConnectConfiguration>`, the JWKS provider + rotation backplane subscriber, the session liveness tracker + revoke-event observer, and the shared `TimeProvider`.

The named HTTP client is identified by `AuthServiceCollectionExtensions.OIDC_DISCOVERY_HTTP_CLIENT_NAME` (`"d2-auth-oidc-discovery"`).

### Configuration — `AuthOptions`

| Property | Type | Default | Notes |
|---|---|---|---|
| `Issuer` | `Uri` | required | OIDC issuer URL whose `/.well-known/openid-configuration` publishes `jwks_uri`. HTTPS-only — HTTP rejected at composition time. |
| `Audience` | `string` | required | Expected `aud` claim. Use a `D2.Shared.Auth.Abstractions.Audiences` codegen constant. |
| `ClockSkew` | `TimeSpan` | 30s | Tolerance applied to JWT `exp` / `nbf` checks. Matches `Microsoft.IdentityModel` default; accommodates typical NTP drift. |
| `Jwks` | `JwksProviderOptions` | `new()` | Sub-options for the JWKS provider — see table below. |
| `Sessions` | `SessionLivenessOptions` | `new()` | Sub-options for the session liveness tracker — see table below. |
| `Validator` | `JwtValidatorOptions` | `new()` | Sub-options for the JWT validator — see table below. |

#### `JwksProviderOptions` — JWKS provider sub-options

| Property | Type | Default | Notes |
|---|---|---|---|
| `RefreshCooldown` | `TimeSpan` | 30s | Minimum interval between consecutive forced JWKS refreshes — prevents reactive-refresh-on-unknown-kid stampedes. |
| `HttpRequestTimeout` | `TimeSpan` | 5s | Per-request timeout on the named OIDC discovery `HttpClient`. Without this override, the BCL default of 100s applies — a hung Edge would tie up the calling thread for the full window. |
| `CircuitBreakerFailureThreshold` | `int` | 5 | Consecutive failures before the JWKS-fetch circuit breaker opens. While open, calls fail fast with `AuthFailures.JwksUnavailable` — avoids per-call HTTP roundtrip during sustained Edge outage. |
| `CircuitBreakerCooldown` | `TimeSpan` | 30s | Duration the circuit breaker stays open before allowing a half-open probe. |
| `BackplaneChannelKey` | `string` | `"d2.security.key-rotated:jwks"` | Cache backplane channel pattern for cluster-wide JWKS rotation events. **Cross-service contract** — Edge's `D2.Shared.KeyCustodian` MUST publish on the same string. Empty / whitespace rejected at host build via `ValidateOnStart`. |

#### `SessionLivenessOptions` — session liveness sub-options

| Property | Type | Default | Notes |
|---|---|---|---|
| `CacheKeyPrefix` | `string` | `"session:"` | Cache key prefix used for session-liveness sentinel entries. Edge writes `session:{sessionId:N}` on session creation; backend services check existence under this prefix on every authenticated request. |

#### `JwtValidatorOptions` — JWT validator sub-options

| Property | Type | Default | Notes |
|---|---|---|---|
| `RequireSessionIdClaim` | `bool` | `true` | Reject JWTs missing the `d2_session_id` claim. Defense-in-depth: the session liveness check (transport-layer middleware / interceptor) needs the claim to perform its lookup. Set to `false` only for service-identity-only flows (RFC 6749 §4.4 client_credentials) that don't carry a user session. |
| `RequireExpirationTime` | `bool` | `true` | Reject JWTs missing the standard `exp` claim. Mirrors the Microsoft.IdentityModel default — declared explicitly so the contract is doc-complete and survives library default changes. |
| `ValidAlgorithms` | `IReadOnlyList<string>` | `["RS256"]` | Allowlist of accepted JWS `alg` header values. Pinning the list defends against `alg=none` and HMAC-with-public-key confusion attacks at the standard validator surface. Empty list / whitespace-only entries rejected at host build via `ValidateOnStart`. |

Validation runs at the first `IOptions<AuthOptions>.Value` resolution (typically during host startup composition) — fail-fast on invalid config.

### Failure helpers — `AuthFailures`

Pre-built `D2Result` failures. Caller code (middleware, validator, interceptor) picks the right helper rather than constructing raw failures by hand. Two-axis design:

- **User-facing message** — coarse on purpose. Two TK keys total (`auth_errors_UNAUTHORIZED`, `auth_errors_TEMPORARILY_UNAVAILABLE`) so we don't tell attackers which validation step failed.
- **Machine-readable code** — granular. One `AuthErrorCodes.AUTH_*` constant per failure mode, surfaced as the `d2_error_code` on RFC 7807 ProblemDetails.

`AuthErrorCodes` and `AuthFailures` are emitted by [`D2.Shared.Auth.ErrorCodes.SourceGen`](../auth-error-codes-source-gen/) from [`contracts/auth-error-codes/auth-error-codes.spec.json`](../../../../contracts/auth-error-codes/auth-error-codes.spec.json) — single source of truth. Adding a new error code = editing the JSON spec; the constant + the factory + the cross-spec telemetry tag-value enumeration on `d2.auth.problem.emitted` all materialize automatically. The emitted `*.g.cs` files land in the tracked `Generated/` directory (committed for inspection, IDE navigation, and PR diff review; re-emitted on every `dotnet build`; do not hand-edit).

> **Duplicated from [`contracts/auth-error-codes/auth-error-codes.spec.json`](../../../../contracts/auth-error-codes/auth-error-codes.spec.json) — update both in lockstep.** The 14-row failure surface table below is a per-row at-a-glance projection of the spec. The spec is the single source of truth; the `auth-error-codes-source-gen` analyzer emits the constants + factories. Adding a row here without a corresponding spec entry will fail at codegen time; adding a spec entry without updating this table will drift the docs.

| Helper | HTTP | Error code | TK key |
|---|---|---|---|
| `BearerMissing()` | 401 | `AUTH_BEARER_MISSING` | `UNAUTHORIZED` |
| `BearerMalformed()` | 401 | `AUTH_BEARER_MALFORMED` | `UNAUTHORIZED` |
| `JwtSignatureInvalid()` | 401 | `AUTH_JWT_SIGNATURE_INVALID` | `UNAUTHORIZED` |
| `JwtExpired()` | 401 | `AUTH_JWT_EXPIRED` | `UNAUTHORIZED` |
| `JwtNotYetValid()` | 401 | `AUTH_JWT_NOT_YET_VALID` | `UNAUTHORIZED` |
| `JwtIssuerMismatch()` | 401 | `AUTH_JWT_ISSUER_MISMATCH` | `UNAUTHORIZED` |
| `JwtAudienceMismatch()` | 401 | `AUTH_JWT_AUDIENCE_MISMATCH` | `UNAUTHORIZED` |
| `JwtClaimMissing()` | 401 | `AUTH_JWT_CLAIM_MISSING` | `UNAUTHORIZED` |
| `JwtActChainMalformed()` | 401 | `AUTH_JWT_ACT_CHAIN_MALFORMED` | `UNAUTHORIZED` |
| `JwtKidNotFound()` | 401 | `AUTH_JWT_KID_NOT_FOUND` | `UNAUTHORIZED` |
| `SessionRevoked()` | 401 | `AUTH_SESSION_REVOKED` | `UNAUTHORIZED` |
| `JwksUnavailable()` | 503 | `AUTH_JWKS_UNAVAILABLE` | `TEMPORARILY_UNAVAILABLE` |
| `SessionLivenessUnavailable()` | 503 | `AUTH_SESSION_LIVENESS_UNAVAILABLE` | `TEMPORARILY_UNAVAILABLE` |
| `ScopeInsufficient()` | 401 | `AUTH_SCOPE_INSUFFICIENT` | `UNAUTHORIZED` |

### Telemetry

ActivitySource: `D2.Shared.Auth`. Meter: `D2.Shared.Auth`. Hosts add both via standard `OpenTelemetryBuilder` registration:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(AuthTelemetry.ACTIVITY_SOURCE_NAME))
    .WithMetrics(m => m.AddMeter(AuthTelemetry.METER_NAME));
```

Tag-key + tag-value constants are emitted by [`D2.Shared.Telemetry.Tags.SourceGen`](../telemetry-tags-source-gen/) into `AuthTelemetryTags.g.cs` from [`contracts/telemetry/telemetry.spec.json`](../../../../contracts/telemetry/telemetry.spec.json). Counter call sites reference `AuthTelemetryTags.JwtValidations.Outcome.SUCCESS` / `AuthTelemetryTags.JwksFetches.Trigger.REACTIVE` / etc. instead of bare string literals — drift between the spec and the runtime tag values is impossible.

| Counter | Tags | Description |
|---|---|---|
| `d2.auth.jwt.validations` | `outcome` (closed enum; see `AuthTelemetryTags.JwtValidations.Outcome.*`) | Total inbound JWT validations. |
| `d2.auth.session.liveness.checks` | `outcome` (`AuthTelemetryTags.SessionLivenessChecks.Outcome.*`) | Total session liveness checks (`alive` / `revoked` / `unavailable` / `invalid_input` from `IsAliveAsync`) and revoke-event observations (`backplane_revoked` from `SessionRevokedBackplaneSubscriber`). |
| `d2.auth.jwks.fetches` | `trigger` × `outcome` (`AuthTelemetryTags.JwksFetches.Trigger.*` / `Outcome.*`) | Total JWKS fetches from the upstream OIDC issuer. `parse_error` distinguishes malformed-JSON discovery docs from generic network failures; `circuit_open` indicates the breaker fast-failed without an upstream call. |
| `d2.auth.problem.emitted` | `d2_error_code` (one of `AuthErrorCodes.AUTH_*` — cross-spec resolved) | RFC 7807 ProblemDetails / gRPC trailers emitted by the auth-http / auth-grpc transport bindings. |

| Histogram | Unit | Description |
|---|---|---|
| `d2.auth.jwt.validation.duration` | ms | Wall-clock duration of the full JWT validation pipeline (signature verify + standard claim checks + claim → context mapping + session liveness check). |
| `d2.auth.session.liveness.lookup.duration` | ms | Wall-clock duration of a session liveness lookup (cache check + on-miss backplane reconciliation). |
| `d2.auth.jwks.fetch.duration` | ms | Wall-clock duration of a JWKS fetch from the upstream OIDC issuer (HTTP round-trip + JSON parse). |

`AuthTelemetry.SR_Activity` (the static `ActivitySource`) and `AuthTelemetry.SR_Meter` (the static `Meter`) are exposed for pipeline implementations that need to start spans / record histograms directly.

### Bearer extraction edge cases (RFC 6750 §2.1)

Canonical bearer-extraction behavior for both transport bindings. HTTP middleware reads from the `Authorization` request header; gRPC interceptor reads from the `authorization` request metadata — identical semantics, only header-name casing differs per transport convention.

| Input | Result |
|---|---|
| Header / metadata absent | `BearerMissing` |
| `Basic foo` (wrong scheme) | `BearerMissing` |
| `bearer eyJ...` | OK (case-insensitive prefix match) |
| `BEARER eyJ...` | OK |
| `Bearer ` (empty after prefix) | `BearerMissing` (semantically nothing to validate) |
| `Bearer not.a.jwt.too.many.parts` | Validator returns `BearerMalformed` |
| Multiple `Authorization` / `authorization` entries | First wins (RFC 7230 §3.2.2 / gRPC parity) |
| Whitespace inside token | NOT trimmed — passed through verbatim; validator rejects. Trimming would mask client bugs. |

Per-transport extension catalogs note any transport-specific variation; the table above is the single source of truth for both bindings.

### Failure surface — transport status mapping

Every `AuthFailures.*` factory carries an HTTP status (see the `AuthFailures` table above). Transport bindings render that status verbatim or map it as follows:

| Binding | Rendering |
|---|---|
| HTTP middleware (`auth-http`) | `D2Result.StatusCode` written verbatim to the RFC 7807 ProblemDetails `status` field. |
| gRPC interceptor (`auth-grpc`) | `D2Result.StatusCode` mapped to `Status.StatusCode`: 401 → `Unauthenticated` (16); 503 → `Unavailable` (14); other → `Internal` (13). NEVER `PermissionDenied` (7) — `AUTH_SCOPE_INSUFFICIENT` also maps to `Unauthenticated` so the wire never leaks which check failed. |

The granular `AUTH_*` code (see [Failure helpers — `AuthFailures`](#failure-helpers--authfailures) above) carries the machine-readable taxonomy across both transports via the `d2_error_code` ProblemDetails extension / gRPC trailer.

### PII discipline — `SanitizedExceptionRender`

`AuthLog` `[LoggerMessage]` delegates never accept `Exception` — exception messages can interpolate JWT bytes, request URIs, response bodies, or other runtime data that must not reach the log pipeline. Callers pass `SanitizedExceptionRender.TypeName(ex)` and `SanitizedExceptionRender.FirstFrame(ex)` as separate strings instead. The helper itself is the canonical `D2.Shared.Utilities.Diagnostics.SanitizedExceptionRender` (consumed by every lib whose log delegates carry exception-derived strings). The no-`Exception`-parameter shape is enforced locally by `AuthLogDelegateContractTests` via reflection across the entire `AuthLog` class.

Bearer bytes, claim values, and scope strings NEVER reach logs / span tags / metric tags / ProblemDetails fields / gRPC trailer fields / exception interpolations. Both transport bindings reuse this lib's `AuthLog` delegates and emit only outcome categories:

- `BearerHeaderMissing` — boolean fact, no header value.
- `ScopeRequirementUnmet(string requiredScopesSummary)` — closed-enumeration summary (`"N scopes required, first=files.read"`), NOT the full scope set verbatim.
- `LivenessRevoked` — no parameters; sessionId is PII, never logged.

The HTTP ProblemDetails `Detail` field and the gRPC `Status.Detail` field are DELIBERATELY EMPTY on every auth failure — telling an attacker which validation step failed is an info leak. The closed-enum `d2_error_code` carries the machine-readable taxonomy for legitimate operators.

## Dependencies

| Package | Why |
|---|---|
| `D2.Shared.Auth.Abstractions` | `IJwksProvider`, `ISessionLivenessTracker`, `Audiences`, `JwtClaimTypes`, `Scopes`. |
| `D2.Shared.AuthContext.Abstractions` | `IAuthContext` shape (consumed when claims-to-context mapping lands). |
| `D2.Shared.Context.Abstractions` | `IRequestContext` extension. |
| `D2.Shared.Caching.Abstractions` | `ICacheInvalidationBackplane` for cluster-wide rotation / revoke event delivery. |
| `D2.Shared.Caching.Tiered` | `ITieredCache` for sentinel-only session liveness lookups. |
| `D2.Shared.Resilience` | `Singleflight` to dedupe concurrent JWKS force-refreshes. |
| `D2.Shared.Result` | `D2Result` typed factories. |
| `D2.Shared.I18n.Abstractions` | `TK.Auth.Errors` translation keys. |
| `D2.Shared.Utilities` | `Falsey()` / `Truthy()` extensions. |
| `Microsoft.IdentityModel.Tokens` | `SecurityKey` + JWT validation primitives. |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect` | `ConfigurationManager<OpenIdConnectConfiguration>` for OIDC discovery + JWKS retrieval. |
| `Microsoft.Extensions.Http` | `IHttpClientFactory` for the named OIDC discovery client. |
| `Microsoft.Extensions.{DependencyInjection,Hosting,Logging,Options}.Abstractions` | DI / hosted services / logging / options binding. |
| `JetBrains.Annotations` | `[MustDisposeResource]` for DI-managed singletons / hosted services. |

## Tests

`server/shared/dotnet/tests/Unit/Auth/Inbound/`:

- `AuthOptionsTests.cs` — defaults (ClockSkew=30s), with-expression overrides, equality, baseline-not-mutated invariant.
- `Errors/AuthFailuresTests.cs` — every helper × (status code, error code, TK key) triple pinned.
- `Telemetry/AuthTelemetryTests.cs` — ActivitySource + Meter names; every counter / histogram name + unit pinned; inbound vs outbound source separation.
- `Telemetry/AuthLogDelegateContractTests.cs` — reflection scan asserting zero `Exception` parameters across `AuthLog`. PII enforcement gate.
- `AuthServiceCollectionExtensionsTests.cs` — `AddD2Auth` composition resolution, null-arg throws, options validation (Issuer required + HTTPS, Audience required), named HTTP client registration, idempotency.

Run: `dotnet test server/shared/dotnet/tests`.

## Debugging

When a service starts emitting `AUTH_*` failures:

- **`AUTH_BEARER_MISSING` / `AUTH_BEARER_MALFORMED`** — caller didn't include a parseable `Authorization: Bearer <jwt>` header. Check the caller's outbound auth wiring (typically `AddD2ServiceIdentity()` from `D2.Shared.Auth.Outbound`).
- **`AUTH_JWT_SIGNATURE_INVALID` / `AUTH_JWT_KID_NOT_FOUND`** — JWKS drift. Check the `d2.auth.jwks.fetches{trigger=backplane_rotation}` counter — if it's flat after a known key rotation, the rotation backplane isn't reaching this service. Either the `ICacheInvalidationBackplane` isn't registered (check the `JwksBackplaneAbsent` warning at startup) or the configured `BackplaneChannelKey` doesn't match Edge's publish key.
- **`AUTH_JWT_ISSUER_MISMATCH` / `AUTH_JWT_AUDIENCE_MISMATCH`** — service's `AuthOptions.Issuer` / `AuthOptions.Audience` doesn't match what Edge minted. Check the env-var binding.
- **`AUTH_JWT_EXPIRED` / `AUTH_JWT_NOT_YET_VALID`** — clock drift between the issuer and this service. `ClockSkew` (default 30s) bounds tolerance.
- **`AUTH_JWKS_UNAVAILABLE`** — OIDC issuer unreachable. Check the `d2.auth.jwks.fetches{outcome=failure}` counter and the `JwksFetchFailed` log line for the exception type. If the issuer URL is recently changed, check that `D2_AUTH_ISSUER` is pointing at the right host.
- **`AUTH_SESSION_REVOKED`** — session was revoked (sign-out, admin action, anomaly detection). User must re-authenticate.
- **`AUTH_SESSION_LIVENESS_UNAVAILABLE`** — Redis liveness store unreachable. Check the cluster-wide cache backplane health; the consumer fails closed (returns 503, never treats unknown liveness as alive).

## Composing with siblings

A host that serves both HTTP endpoints and gRPC services on the same AspNetCore Kestrel host wires all three extensions in a fluent chain:

```csharp
builder.Services
    .AddD2Auth(opts =>
    {
        opts.Issuer = new Uri("https://edge.internal");
        opts.Audience = Audiences.MyService;
    })
    .AddD2AuthHttp()
    .AddD2AuthGrpc();

builder.Services.AddGrpc(o => o.MaxReceiveMessageSize = 16 * 1024 * 1024);

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(AuthTelemetry.ACTIVITY_SOURCE_NAME))
    .WithMetrics(m => m.AddMeter(AuthTelemetry.METER_NAME));

var app = builder.Build();
app.UseRouting();
app.UseD2Auth();              // HTTP middleware
app.MapGet("/files/{id}", H).RequireD2Scope("files.read");
app.MapGrpcService<MyGrpcService>();   // interceptor handles gRPC auth
app.Run();
```

Both transport extensions register an IDENTICAL scoped `IRequestContext` resolver lambda reading from a shared `HttpContext.Items` slot — the HTTP middleware writes that slot on successful auth; the gRPC interceptor mirrors the write (alongside its own `ServerCallContext.UserState` write for the gRPC-specific hot-path accessor). Constructor-injecting `IRequestContext` works correctly under either transport. Registration order does not matter: `TryAddScoped` first-wins is harmless because the lambdas behave identically given the same `HttpContext` state. The two transport-binding csprojs are siblings (no inter-csproj dep) — the resolver lambda is duplicated inline in each transport extension, with a parity test (`tests/Unit/Auth/Inbound/RequestContextResolverParityTests.cs`) defending against future drift.

For HTTP-only hosts, omit `.AddD2AuthGrpc()`. For gRPC-only hosts, omit `.AddD2AuthHttp()` (and the `app.UseD2Auth()` middleware insertion). Each transport extension is opt-in via the host's csproj `<PackageReference>` chain — neither transport's framework reference reaches a host that doesn't reach for it.

## References

- [`../auth-abstractions/README.md`](../auth-abstractions/README.md) — vocabulary slice (enums + claim names + `Audiences` / `Scopes` catalog + `IJwksProvider` / `ISessionLivenessTracker` contracts + `D2HttpContextItems` cross-transport slot key)
- [`../auth-http/README.md`](../auth-http/README.md) — HTTP-transport binding (middleware + ProblemDetails + endpoint scope metadata)
- [`../auth-grpc/README.md`](../auth-grpc/README.md) — gRPC-transport binding (interceptor + RpcException trailers + method scope attributes)
- [`../auth-outbound/README.md`](../auth-outbound/README.md) — token-acquisition complement
- [`../../../../docs/v2/PHASE_0_AUTH.md`](../../../../docs/v2/PHASE_0_AUTH.md) §14a — KeyCustodian compromise runbook scaffolding (Canonical: not yet shipped; design at the cited path. Will migrate to a shipped lib README when the KeyCustodian implementation ships.)
- [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519) — JSON Web Token
- [RFC 8414](https://datatracker.ietf.org/doc/html/rfc8414) — OAuth 2.0 Authorization Server Metadata (OIDC discovery)
