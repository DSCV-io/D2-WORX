<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Http

> Parent: [`public/packages/dotnet/`](../../README.md)

HTTP-transport binding for [`D2.Shared.Auth`](../core/README.md) — convention-based middleware that runs the JWT validation pipeline + session liveness check on inbound HTTP requests, emits RFC 7807 ProblemDetails on failure, and supports per-endpoint scope requirements via the ASP.NET endpoint-metadata pattern.

Lives in its own csproj (separate from `D2.Shared.Auth`) so the AspNetCore framework reference is opt-in: worker / console / gRPC-only services that consume `D2.Shared.Auth` for JWT validation in non-HTTP paths don't need to drag in `Microsoft.AspNetCore.App`. Sibling [`D2.Shared.Auth.Grpc`](../grpc/README.md) holds the gRPC-transport binding under the same logic. The two transport-binding csprojs are siblings (no inter-csproj dep): each registers an identical scoped `IRequestContext` resolver lambda that reads from a shared `HttpContext.Items` slot, so a single dual-transport host wires both extensions and resolves `IRequestContext` correctly under either transport.

The csproj is named `.Http` (not `.AspNetCore`) for naming-symmetry parity with sibling `.Grpc` — both are transport-binding csprojs running on the same AspNetCore Kestrel runtime; naming the HTTP one for the host runtime while naming the gRPC one for the transport protocol would be misleading.

## Public API surface

### Composition

```csharp
services
    .AddD2Auth(opts =>
    {
        opts.Issuer = new Uri("https://edge.internal");
        opts.Audience = Audiences.MyService;
    })
    .AddD2AuthHttp();

// ... later, in the request pipeline:
app.UseRouting();
app.UseD2Auth();              // BETWEEN UseRouting() and the endpoint dispatcher
app.MapGet("/files/{id}", H).RequireAnyScope("files.read");
app.MapGet("/healthz", () => "ok").MarkAsD2HarmlessEndpoint();
```

`AddD2AuthHttp()` registers `IHttpContextAccessor`, a dual-path scoped `IRequestContext` resolver (prefers `HttpContext.Items` when the middleware has established a context; otherwise the scope's `MutableRequestContext` — Unestablished until established, fail-closed in authority rules), a scoped `IForwardedJwtAccessor` holder (the same registration `AddD2AuthGrpc()` makes), and a singleton `IAmbientRequestScopeAccessor` adapter (see below). The dual-path **replaces** any prior plain Mutable-only `IRequestContext` from platform `AddD2SystemWorkPlane()` so hosted System workers on the same host never hit a throw-only path. `AddD2Auth(...)` MUST be called first — fail-fast `InvalidOperationException` otherwise. System work itself enters via `ISystemWorkScopeFactory` (see [`D2.Shared.Context.Abstractions`](../../context/abstractions/README.md)).

The middleware ALSO captures the validated raw bearer into the request-scoped `IForwardedJwtAccessor` (the [redacting forwarded-JWT holder](../abstractions/README.md#forwarded-jwt-credential--forwardedjwt--iforwardedjwtaccessor)) — so an outbound hop can replay it byte-for-byte ([ADR-0022 §Realization](../../../../../public/docs/adrs/0022-service-auth-mint-once-forward.md)). Capture is the LAST pre-continuation operation — placed AFTER every inbound gate (harmless short-circuit, bearer extraction, JWT validation, session liveness, AND per-endpoint scope enforcement), mirroring the gRPC `JwtAuthInterceptor`; only a token that cleared ALL gates ever enters the holder (the harmless / bearer-missing / validation-failure / liveness-revoked / scope-insufficient paths all leave it unset). Capture is best-effort: a host that does not register the holder (does not forward) no-ops. The raw bearer is never logged.

### Ambient-scope adapter — `HttpContextAmbientRequestScopeAccessor`

The read-back door for the forwarded-JWT holder. The outbound forwarding credential ([`ForwardedJwtCallCredentials`](../outbound/README.md#forwarded-transaction-token--per-request-callcredentials-the-forward-unchanged-rail-of-adr-0022) in `D2.Shared.Auth.Outbound`) must reach the _current_ request's request-scoped holder on each outbound RPC, but a gRPC `CallCredentials` runs outside the DI request-scope ambient flow. It depends on the framework-free `IAmbientRequestScopeAccessor` port (declared in `D2.Shared.Auth.Abstractions`, referenced by BOTH this lib and the outbound lib) rather than on `IHttpContextAccessor` directly — so the outbound lib stays free of any AspNetCore framework reference without needing an `auth/http → auth/outbound` ProjectReference. This lib supplies the concrete adapter: `HttpContextAmbientRequestScopeAccessor` reads `IHttpContextAccessor.HttpContext.RequestServices` (backed by an `AsyncLocal<>` the AspNetCore pipeline sets per request), so the same singleton adapter observes each concurrent request's own scope — and thus its own holder, and thus its own token. `AddD2AuthHttp()` registers it as a singleton, symmetric to where it registers the holder write-side; the framework-bound adapter is quarantined here (not in the framework-free outbound lib) by design. The gRPC transport binding supplies its own sibling adapter (`GrpcHttpContextAmbientRequestScopeAccessor`, registered by `AddD2AuthGrpc()`), so a gRPC-inbound forwarding host self-wires the read-back door too — a deliberate tiny duplicate rather than a shared type, since the two transport libs have no inter-csproj dep. On a dual-transport host (HTTP + gRPC on one Kestrel) the two adapters read the same door, so first-wins `TryAddSingleton` is harmless.

`UseD2Auth()` MUST sit AFTER `UseRouting()` (so endpoint metadata is matched) and BEFORE the endpoint dispatcher (`UseEndpoints` / `MapXxx`) so the middleware can short-circuit before handlers run.

### Composing with siblings (dual-transport host)

> See [`../core/README.md` § Composing with siblings](../core/README.md#composing-with-siblings) for the canonical dual-transport composition pattern (fluent chain, identical `IRequestContext` resolver across both transports, HTTP-only / gRPC-only carve-outs).

### Edge-inbound establishment — `RequestOriginEdgeInboundMiddleware` + `AddD2RequestOriginEdge()` / `UseD2RequestOriginEdge()`

A second convention-based middleware, inserted AFTER `UseD2Auth()`, establishes the [`RequestOrigin.EdgeInbound`](../abstractions/README.md#requestorigin--callpath--local-establishment-facts-vs-propagated-telemetry) plane — the external trust boundary, the START of the call-path — on the same scoped `IRequestContext` the auth middleware populated ([ADR-0025](../../../../../public/docs/adrs/0025-request-context-establishment.md)):

```csharp
services
    .AddD2Auth(opts => { /* ... */ })
    .AddD2AuthHttp()
    .AddD2RequestOriginEdge(opts => opts.ServiceId = "edge"); // this host's own workload id

app.UseRouting();
app.UseD2Auth();
app.UseD2RequestOriginEdge();
```

`RequestOriginEdgeInboundMiddleware` sets `IRequestContext.Origin = RequestOrigin.EdgeInbound`, `ImmediateCaller = null` (the external client is not an internal workload), and STARTS a fresh call-path with a single `CallPathKind.Edge` entry carrying the host's own service id (`D2WorkloadIdentityOptions.ServiceId`). No-op-safe when no `MutableRequestContext` is on `HttpContext.Items` (e.g. a harmless endpoint the auth middleware already short-circuited). `AddD2RequestOriginEdge()` binds `D2WorkloadIdentityOptions` with a required-`ServiceId` startup validation and registers `IClock` as `SystemClock` when the host has not already bound one (`TryAdd`).

### Endpoint metadata — `EndpointScopeMetadata`

Carries the per-endpoint scope requirement (or harmless-endpoint opt-in). Two flavors:

- `EndpointScopeMetadata.ForScopes(IEnumerable<string> scopes, ScopeMatch match)` — creates a metadata instance with an explicit match mode: `ScopeMatch.Any` (at-least-one-of) or `ScopeMatch.All` (every-scope). The match mode is always stated explicitly at declaration time. The per-handler `BaseHandler.ScopeRequirement` enforces the same dual-mode (any-of OR all-of) via `HandlerScopeMatch`, so transport and handler layers are consistent.
- `EndpointScopeMetadata.HarmlessEndpoint` — singleton; middleware short-circuits the validator + liveness pipeline.

Attach via the fluent builder extensions:

| Extension                                        | Semantics                                                                                     |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| `.RequireAnyScope("scope")`                      | Endpoint requires the caller to hold at least one of the listed scopes (any-of).             |
| `.RequireAnyScope("scope", "alt-scope")`         | Endpoint requires at least one of these scopes (any-of).                                     |
| `.RequireAllScopes("scope", "other-scope")`      | Endpoint requires the caller to hold every listed scope (all-of).                            |
| `.MarkAsD2HarmlessEndpoint()`                    | Endpoint bypasses auth entirely (probes / OIDC discovery / harmless intra-cluster info only). |

The deny-by-default state: an endpoint with NO `EndpointScopeMetadata` attached gets the FULL pipeline (validator + liveness; any authenticated caller passes — deny-by-default lives in the ABSENCE of metadata). Endpoints that need to bypass auth entirely MUST opt in explicitly via `.MarkAsD2HarmlessEndpoint()` — the codebase deliberately does NOT recognize the BCL `[AllowAnonymous]` attribute (its semantic is tied to the BCL `AuthenticationMiddleware` chain we bypass). A metadata that is PRESENT and non-harmless but carries an EMPTY scope set is treated as a configuration anomaly and fails CLOSED (401 `ScopeInsufficient` + a config-anomaly log) rather than silently admitting any authenticated caller: the public `ForScopes` factory rejects empty sets, so such a metadata can only arise from a serializer / record-clone / reflection path.

#### Deny-by-default boot guard

`D2.Shared.Auth.Startup` (see [`../startup/README.md`](../startup/README.md)) registers an `IStartupFilter` that requires **every** mapped `RouteEndpoint` to carry a declared auth intent before the host accepts any traffic. Infrastructure paths (`/health`, `/alive`, `/metrics`, `/.well-known`) are auto-exempt. If any endpoint lacks a declaration, the host fails to start with an `InvalidOperationException` that lists the undeclared routes — never silently. Set `D2ServiceDefaultsOptions.SkipAuthEndpointGuard = true` to opt out (test hosts, anonymous-only admin tools).

### ProblemDetails — `D2ProblemDetailsExtensions`

Single emit point (path A) for every middleware-produced 4xx / 5xx auth failure. Usage:

```csharp
var problem = failure.ToProblemDetails(httpContext);
```

`D2ProblemDetailsExtensions` is a plain static class carrying the `ToProblemDetails` extension method body + runtime `TypeUriFor` + `MaterializeMessages` helpers. The wire-format catalog (`TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*` extension keys, `TITLE_*` per-status titles, + the `TitleFor` switch) lives in [`D2ProblemDetailsKeys`](../../problem-details/abstractions/README.md) (codegen-emitted into `D2.Shared.ProblemDetails.Abstractions` from `contracts/problem-details/problem-details.spec.json`). The same spec drives the TS-side `@d2/problem-details-abstractions` catalog (re-exported from `@d2/headers` for compatibility) AND the path-B Customizer in `D2.Shared.AspNetCore`, so the three emit paths produce byte-identical Shape A bodies for identical inputs.

Populates an RFC 7807 `Microsoft.AspNetCore.Mvc.ProblemDetails`:

| Field                           | Source                                                                                                                                                                                                                                                |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Status`                        | `D2Result.StatusCode` (verbatim — no remapping).                                                                                                                                                                                                      |
| `Title`                         | Locale-neutral coarse English from the spec-driven closed enumeration (e.g. `"Unauthorized"` for 401, `"Service Unavailable"` for 503, `"Request Failed"` as fallback). Locale-aware translation is the client's job via the `d2_messages` extension. |
| `Type`                          | `https://problems.d2.dcsv.io/{kebab-cased-error-code}` (spec-driven `TYPE_URI_PREFIX` value).                                                                                                                                                         |
| `Detail`                        | DELIBERATELY OMITTED. Telling an attacker which validation step failed (signature vs expired vs claim missing) is an info leak; the granular `d2_error_code` carries the machine-readable taxonomy for legitimate operators.                          |
| `Instance`                      | `"{Method} {Path}"` (no query string — matches the path-B Customizer shape for cross-path consistency).                                                                                                                                               |
| `Extensions["d2_error_code"]`   | `D2Result.ErrorCode` (one of the `AUTH_*` constants from `D2.Shared.Auth.Errors.AuthErrorCodes`).                                                                                                                                                     |
| `Extensions["d2_messages"]`     | `D2Result.Messages` array (TK keys + parameter bindings). Client-side Paraglide translates.                                                                                                                                                           |
| `Extensions["d2_input_errors"]` | `D2Result.InputErrors` array — only emitted when non-empty. Field-level form errors keyed by field name; client renders under each input directly.                                                                                                    |
| `Extensions["d2_category"]`     | `D2Result.Category?.ToWire()` — only emitted when non-null. Carries the closed-enum semantic `ErrorCategory` wire string (e.g. `"policy_denied"`, `"validation_failure"`). Mirrors the gRPC envelope's `category` field — cross-transport parity. Lets a consumer branch on failure class without resolving the error code through the registry. |
| `Extensions["traceId"]`         | `Activity.Current?.TraceId` (W3C lower-hex format). Omitted when no Activity is on the execution context — never surfaced as null.                                                                                                                    |

**2xx guard**: `ToProblemDetails` throws `InvalidOperationException` when `(int)result.StatusCode < 400`. RFC 7807 frames the wire around 4xx / 5xx; a 2xx partial-success (e.g. `SomeFound` / 206) belongs on the D2Result envelope, not the ProblemDetails body.

Side-effect: increments `AuthTelemetry.SR_ProblemEmitted` tagged with `d2_error_code`. Response `Content-Type` set to `D2ProblemDetailsKeys.CONTENT_TYPE` (`"application/problem+json"` per RFC 7807 §6.1) by the middleware wrapper.

### `HttpContext.GetD2RequestContext()`

Typed accessor for the populated `IRequestContext` the middleware writes to `HttpContext.Items`. Preferred over raw key lookups; the raw key constant lives on the abstractions-slice `D2HttpContextItems` class precisely so callers reach for this extension.

```csharp
endpoints.MapGet("/me", (HttpContext ctx) =>
{
    var requestContext = ctx.GetD2RequestContext();
    // requestContext is the validated IRequestContext when the middleware ran;
    // null on harmless endpoints / pre-auth pipeline stages.
});
```

Or better, constructor-inject `IRequestContext` directly — the scoped resolver registered by `AddD2AuthHttp()` resolves from the same slot.

## Failure surface

> See [`../core/README.md` § Failure helpers — `AuthFailures`](../core/README.md#failure-helpers--authfailures) for the canonical 15-row failure-code table (single source: [`contracts/auth-error-codes/auth-error-codes.spec.json`](../../../../../contracts/auth-error-codes/auth-error-codes.spec.json)). HTTP renders `D2Result.StatusCode` verbatim into the RFC 7807 `status` field — no remapping. Every failure terminates with 401 or 503 ProblemDetails (NEVER 403 — see [`../core/README.md` § Failure surface — transport status mapping](../core/README.md#failure-surface--transport-status-mapping) for the cross-transport rationale).

## Bearer extraction edge cases (RFC 6750 §2.1)

> See [`../core/README.md` § Bearer extraction edge cases](../core/README.md#bearer-extraction-edge-cases-rfc-6750-21) for the canonical edge-case table. HTTP middleware reads from the `Authorization` request header; the table is identical semantics across both transports.

## PII discipline

> See [`../core/README.md` § PII discipline — `SanitizedExceptionRender`](../core/README.md#pii-discipline--sanitizedexceptionrender). PII rules apply uniformly across HTTP and gRPC bindings; both reuse the auth-core lib's `AuthLog` delegates and emit closed-enumeration outcome categories only.

## Dependencies

| Package                                                                   | Why                                                                                                                                      |
| ------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Auth`                                                          | `JwtValidator` (consumed via `InternalsVisibleTo`), `AuthFailures`, `AuthErrorCodes`, `AuthLog`, `AuthTelemetry`.                        |
| `D2.Shared.Auth.Abstractions`                                             | `ISessionLivenessTracker` contract + `JwtClaimTypes` + `D2HttpContextItems.REQUEST_CONTEXT` (shared with sibling `D2.Shared.Auth.Grpc`) + `IAmbientRequestScopeAccessor` port — this lib hosts the `IHttpContextAccessor`-backed adapter for it so the outbound lib stays framework-free. |
| `D2.Shared.Context.Abstractions`                                          | `IRequestContext` shape.                                                                                                                 |
| `D2.Shared.Result`                                                        | `D2Result` typed factories.                                                                                                              |
| `D2.Shared.I18n.Abstractions`                                             | `TKMessage` shape.                                                                                                                       |
| `D2.Shared.Utilities`                                                     | `Falsey()` / `Truthy()` extensions.                                                                                                      |
| `D2.Shared.Time`                                                          | `IClock` / `SystemClock` — timestamps the Edge call-path entry `RequestOriginEdgeInboundMiddleware` starts the path with.                |
| `Microsoft.AspNetCore.App` (framework ref via `Sdk.Web`)                  | `HttpContext`, `IEndpointConventionBuilder`, `Microsoft.AspNetCore.Mvc.ProblemDetails`, `IApplicationBuilder`.                           |
| `Microsoft.Extensions.{DependencyInjection,Logging,Options}.Abstractions` | DI / logging / options.                                                                                                                  |
| `JetBrains.Annotations`                                                   | Standard annotations.                                                                                                                    |

## Tests

`public/packages/dotnet/tests/Unit/Auth/Inbound/Http/`:

- `Middleware/JwtAuthMiddlewareTests.cs` — every `InvokeAsync` branch (harmless-endpoint short-circuit, no-metadata endpoint, bearer-missing / wrong-prefix / empty-after-prefix / multi-Authorization-header, validator failures, liveness outcomes, scope pass / fail, cancellation propagation, HttpContext.Items contract, double-write avoidance).
- `Endpoints/EndpointScopeMetadataTests.cs` — `HarmlessEndpoint` singleton invariants; `ForScopes` deduping + frozen; record equality; zero-scope throws; **`HarmlessEndpoint` factory + `IsHarmlessEndpoint` property names pinned via reflection (literal-string lookup) to guard against type / property renames that downstream analyzers depend on.**
- `Endpoints/RequireD2ScopeExtensionsTests.cs` — fluent extensions correctly attach metadata; null/whitespace argument throws.
- `ProblemDetails/D2ProblemDetailsExtensionsTests.cs` — every AuthFailures surface → expected ProblemDetails JSON shape; `Type` URI scheme; `Status` per error; `d2_error_code` extension verbatim; `d2_messages` array shape; `d2_category` emitted when `D2Result.Category` is set, absent when null; `traceId` presence/absence per `Activity.Current`; counter increment.
- `AuthHttpServiceCollectionExtensionsTests.cs` — dual-path `IRequestContext` (Items when established; else Unestablished Mutable when middleware hasn't run / no HttpContext / wrong slot type); missing-`AddD2Auth` fail-fast; idempotency.
- `AuthAppBuilderExtensionsTests.cs` — `UseD2Auth()` integration; middleware-pipeline-order invariant.
- `Middleware/HttpContextRequestContextExtensionsTests.cs` — typed accessor returns null pre-middleware, populated value post-middleware.
- `Middleware/D2HttpContextItemsTests.cs` — slot-key constant value pinned.
- `Errors/AuthFailuresScopeInsufficientTests.cs` (in the existing `AuthFailures` test folder) — `ScopeInsufficient()` status code + error code + TK key.

`public/packages/dotnet/tests/Unit/Auth/Inbound/Http/Establishment/`:

- `RequestOriginEdgeInboundMiddlewareTests.cs` — establishes `Origin = EdgeInbound`, `ImmediateCaller = null`, and a fresh single-`CallPathKind.Edge`-entry call-path; no-op when no `MutableRequestContext` is present.
- `RequestOriginEdgeServiceCollectionExtensionsTests.cs` — `D2WorkloadIdentityOptions.ServiceId` required-startup-validation; `IClock` `TryAdd`.

Cross-transport companions (in `tests/Unit/Auth/Inbound/`):

- `RequestContextResolverParityTests.cs` — the two scoped resolver lambdas (`AddD2AuthHttp()` + `AddD2AuthGrpc()`) return equivalent results given identical `HttpContext` state. Defends against future drift between the duplicated inline lambdas.
- `DualTransportHostCompositionTests.cs` — both extensions registered in either order; interceptor on `GrpcServiceOptions.Interceptors` exactly once; `IRequestContext` resolves correctly under either transport.

Run: `dotnet test public/packages/dotnet/tests`.

## References

- [`../core/README.md`](../core/README.md) — JWT validator + JWKS + liveness internals
- [`../grpc/README.md`](../grpc/README.md) — gRPC-transport sibling
- [`../startup/README.md`](../startup/README.md) — deny-by-default boot guard
- [`../abstractions/README.md`](../abstractions/README.md) — `ISessionLivenessTracker`, `JwtClaimTypes`, `Audiences`, `Scopes`, `D2HttpContextItems`
- [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) — Bearer Token Usage
- [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) — Problem Details for HTTP APIs
