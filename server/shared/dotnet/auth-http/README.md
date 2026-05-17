<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Http

> Parent: [`server/shared/dotnet/`](../README.md)

HTTP-transport binding for [`D2.Shared.Auth`](../auth/README.md) — convention-based middleware that runs the JWT validation pipeline + session liveness check on inbound HTTP requests, emits RFC 7807 ProblemDetails on failure, and supports per-endpoint scope requirements via the ASP.NET endpoint-metadata pattern.

Lives in its own csproj (separate from `D2.Shared.Auth`) so the AspNetCore framework reference is opt-in: worker / console / gRPC-only services that consume `D2.Shared.Auth` for JWT validation in non-HTTP paths don't need to drag in `Microsoft.AspNetCore.App`. Sibling [`D2.Shared.Auth.Grpc`](../auth-grpc/README.md) holds the gRPC-transport binding under the same logic. The two transport-binding csprojs are siblings (no inter-csproj dep): each registers an identical scoped `IRequestContext` resolver lambda that reads from a shared `HttpContext.Items` slot, so a single dual-transport host wires both extensions and resolves `IRequestContext` correctly under either transport.

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
app.MapGet("/files/{id}", H).RequireD2Scope("files.read");
app.MapGet("/healthz", () => "ok").MarkAsD2HarmlessEndpoint();
```

`AddD2AuthHttp()` registers `IHttpContextAccessor` and a scoped `IRequestContext` resolver that reads from `HttpContext.Items` (populated by the middleware). `AddD2Auth(...)` MUST be called first — fail-fast `InvalidOperationException` otherwise.

`UseD2Auth()` MUST sit AFTER `UseRouting()` (so endpoint metadata is matched) and BEFORE the endpoint dispatcher (`UseEndpoints` / `MapXxx`) so the middleware can short-circuit before handlers run.

### Composing with siblings (dual-transport host)

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

Both transport extensions register an IDENTICAL scoped `IRequestContext` resolver lambda reading from the same `HttpContext.Items` slot. The HTTP middleware writes the slot on successful auth; the gRPC interceptor mirrors that write (alongside its own `ServerCallContext.UserState` write). Constructor-injecting `IRequestContext` works correctly under either transport. Registration order does not matter: `TryAddScoped` first-wins is harmless because the lambdas behave identically given the same `HttpContext` state.

For HTTP-only or gRPC-only hosts, omit the unused `AddD2AuthXxx()` call — each transport extension is opt-in via the host's csproj `<PackageReference>` chain.

### Endpoint metadata — `EndpointScopeMetadata`

Carries the per-endpoint scope requirement (or harmless-endpoint opt-in). Two flavors:

- `EndpointScopeMetadata.ForScopes(["files.read", "files.admin"])` — at-least-one-of semantics. Mirrors `IAuthContextExtensions.HasAnyScope` and `BaseHandler.RequiredScopes` enforcement.
- `EndpointScopeMetadata.HarmlessEndpoint` — singleton; middleware short-circuits the validator + liveness pipeline.

Attach via the fluent builder extensions:

| Extension | Semantics |
|---|---|
| `.RequireD2Scope("scope")` | Endpoint requires this scope. |
| `.RequireD2Scope("scope", "alt-scope")` | Endpoint requires at least one of these scopes. |
| `.MarkAsD2HarmlessEndpoint()` | Endpoint bypasses auth entirely (probes / OIDC discovery / harmless intra-cluster info only). |

The deny-by-default state: an endpoint with NO `EndpointScopeMetadata` attached gets the FULL pipeline (validator + liveness; scope check passes against the empty required set). Endpoints that need to bypass auth entirely MUST opt in explicitly via `.MarkAsD2HarmlessEndpoint()` — the codebase deliberately does NOT recognize the BCL `[AllowAnonymous]` attribute (its semantic is tied to the BCL `AuthenticationMiddleware` chain we bypass).

### ProblemDetails — `D2ProblemDetailsExtensions`

Single emit point (path A) for every middleware-produced 4xx / 5xx auth failure. Usage:

```csharp
var problem = failure.ToProblemDetails(httpContext);
```

`D2ProblemDetailsExtensions` is a plain static class carrying the `ToProblemDetails` extension method body + runtime `TypeUriFor` + `MaterializeMessages` helpers. The wire-format catalog (`TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*` extension keys, `TITLE_*` per-status titles, + the `TitleFor` switch) lives in [`D2ProblemDetailsKeys`](../problem-details-abstractions/README.md) (codegen-emitted into `D2.Shared.ProblemDetails.Abstractions` from `contracts/problem-details/problem-details.spec.json`). The same spec drives the TS-side `@d2/headers` catalog AND the path-B Customizer in `D2.Shared.AspNetCore`, so the three emit paths produce byte-identical Shape A bodies for identical inputs.

Populates an RFC 7807 `Microsoft.AspNetCore.Mvc.ProblemDetails`:

| Field | Source |
|---|---|
| `Status` | `D2Result.StatusCode` (verbatim — no remapping). |
| `Title` | Locale-neutral coarse English from the spec-driven closed enumeration (e.g. `"Unauthorized"` for 401, `"Service Unavailable"` for 503, `"Request Failed"` as fallback). Locale-aware translation is the client's job via the `d2_messages` extension. |
| `Type` | `https://problems.d2.dcsv.io/{kebab-cased-error-code}` (spec-driven `TYPE_URI_PREFIX` value). |
| `Detail` | DELIBERATELY OMITTED. Telling an attacker which validation step failed (signature vs expired vs claim missing) is an info leak; the granular `d2_error_code` carries the machine-readable taxonomy for legitimate operators. |
| `Instance` | `"{Method} {Path}"` (no query string — matches the path-B Customizer shape for cross-path consistency). |
| `Extensions["d2_error_code"]` | `D2Result.ErrorCode` (one of the `AUTH_*` constants from `D2.Shared.Auth.Errors.AuthErrorCodes`). |
| `Extensions["d2_messages"]` | `D2Result.Messages` array (TK keys + parameter bindings). Client-side Paraglide translates. |
| `Extensions["d2_input_errors"]` | `D2Result.InputErrors` array — only emitted when non-empty. Field-level form errors keyed by field name; client renders under each input directly. |
| `Extensions["traceId"]` | `Activity.Current?.TraceId` (W3C lower-hex format). Omitted when no Activity is on the execution context — never surfaced as null. |

**2xx guard**: `ToProblemDetails` throws `InvalidOperationException` when `(int)result.StatusCode < 400`. RFC 7807 frames the wire around 4xx / 5xx; a 2xx partial-success (e.g. `SomeFound` / 206) belongs on the D2Result envelope, not the ProblemDetails body.

Side-effect: increments `AuthTelemetry.ProblemEmitted` tagged with `d2_error_code`. Response `Content-Type` set to `D2ProblemDetailsKeys.CONTENT_TYPE` (`"application/problem+json"` per RFC 7807 §6.1) by the middleware wrapper.

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

Every failure in this lib's middleware terminates the request with a 401 or 503 ProblemDetails (NEVER 403 — see [`AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT`](../auth/Errors/AuthErrorCodes.cs) remarks for the rationale).

| `d2_error_code` | HTTP | Trigger |
|---|---|---|
| `AUTH_BEARER_MISSING` | 401 | No `Authorization` header / non-`Bearer ` prefix / empty token after prefix. |
| `AUTH_BEARER_MALFORMED` | 401 | Header present but token is not a parseable JWT. |
| `AUTH_JWT_SIGNATURE_INVALID` | 401 | Signature verification failed. |
| `AUTH_JWT_EXPIRED` | 401 | `exp` in the past, beyond clock skew. |
| `AUTH_JWT_NOT_YET_VALID` | 401 | `nbf` in the future, beyond clock skew. |
| `AUTH_JWT_ISSUER_MISMATCH` | 401 | `iss` mismatch. |
| `AUTH_JWT_AUDIENCE_MISMATCH` | 401 | `aud` mismatch. |
| `AUTH_JWT_CLAIM_MISSING` | 401 | Required claim absent (e.g. `d2_session_id` when `RequireSessionIdClaim`). |
| `AUTH_JWT_KID_NOT_FOUND` | 401 | Unknown `kid` after one reactive JWKS refresh. |
| `AUTH_SESSION_REVOKED` | 401 | Session liveness check returned revoked. |
| `AUTH_SCOPE_INSUFFICIENT` | 401 | Caller has no scopes overlapping the endpoint's required set. |
| `AUTH_JWKS_UNAVAILABLE` | 503 | Upstream OIDC issuer unreachable; no cached snapshot. |
| `AUTH_SESSION_LIVENESS_UNAVAILABLE` | 503 | Liveness store unreachable; fail-closed. |

## Bearer extraction edge cases (RFC 6750 §2.1)

| Input | Result |
|---|---|
| Header absent | `BearerMissing` |
| `Authorization: Basic foo` | `BearerMissing` (wrong scheme) |
| `Authorization: bearer eyJ...` | OK (case-insensitive prefix match) |
| `Authorization: BEARER eyJ...` | OK |
| `Authorization: Bearer ` (empty after prefix) | `BearerMissing` (semantically nothing to validate) |
| `Authorization: Bearer not.a.jwt.too.many.parts` | Validator returns `BearerMalformed` |
| Multiple `Authorization` headers | First wins (RFC 7230 §3.2.2) |
| Whitespace inside token | NOT trimmed — passed through verbatim; validator rejects. Trimming would mask client bugs. |

## PII discipline

Bearer bytes, claim values, and scope strings NEVER reach logs / span tags / metric tags / ProblemDetails fields / exception interpolations. The middleware's `[LoggerMessage]` delegates emit only outcome categories:

- `BearerHeaderMissing` — boolean fact, no header value.
- `ScopeRequirementUnmet(string requiredScopesSummary)` — closed-enumeration summary (`"N scopes required, first=files.read"`), NOT the full scope set verbatim.
- `LivenessRevoked` — no parameters; sessionId is PII, never logged.

The auth-core lib's `AuthLog` enforces the no-`Exception`-parameter contract via reflection-based contract tests; this lib reuses the same delegates.

## Dependencies

| Package | Why |
|---|---|
| `D2.Shared.Auth` | `JwtValidator` (consumed via `InternalsVisibleTo`), `AuthFailures`, `AuthErrorCodes`, `AuthLog`, `AuthTelemetry`. |
| `D2.Shared.Auth.Abstractions` | `ISessionLivenessTracker` contract + `JwtClaimTypes` + `D2HttpContextItems.REQUEST_CONTEXT` (shared with sibling `D2.Shared.Auth.Grpc`). |
| `D2.Shared.Context.Abstractions` | `IRequestContext` shape. |
| `D2.Shared.Result` | `D2Result` typed factories. |
| `D2.Shared.I18n.Abstractions` | `TKMessage` shape. |
| `D2.Shared.Utilities` | `Falsey()` / `Truthy()` extensions. |
| `Microsoft.AspNetCore.App` (framework ref via `Sdk.Web`) | `HttpContext`, `IEndpointConventionBuilder`, `Microsoft.AspNetCore.Mvc.ProblemDetails`, `IApplicationBuilder`. |
| `Microsoft.Extensions.{DependencyInjection,Logging,Options}.Abstractions` | DI / logging / options. |
| `JetBrains.Annotations` | Standard annotations. |

## Tests

`server/shared/dotnet/tests/Unit/Auth/Inbound/Http/`:

- `Middleware/JwtAuthMiddlewareTests.cs` — every `InvokeAsync` branch (harmless-endpoint short-circuit, no-metadata endpoint, bearer-missing / wrong-prefix / empty-after-prefix / multi-Authorization-header, validator failures, liveness outcomes, scope pass / fail, cancellation propagation, HttpContext.Items contract, double-write avoidance).
- `Endpoints/EndpointScopeMetadataTests.cs` — `HarmlessEndpoint` singleton invariants; `ForScopes` deduping + frozen; record equality; zero-scope throws; **`HarmlessEndpoint` factory + `IsHarmlessEndpoint` property names pinned via reflection (literal-string lookup) to guard against type / property renames that downstream analyzers depend on.**
- `Endpoints/RequireD2ScopeExtensionsTests.cs` — fluent extensions correctly attach metadata; null/whitespace argument throws.
- `ProblemDetails/D2ProblemDetailsExtensionsTests.cs` — every AuthFailures surface → expected ProblemDetails JSON shape; `Type` URI scheme; `Status` per error; `d2_error_code` extension verbatim; `d2_messages` array shape; `traceId` presence/absence per `Activity.Current`; counter increment.
- `AuthHttpServiceCollectionExtensionsTests.cs` — `IRequestContext` resolves via accessor adapter; throws when middleware hasn't run; throws when slot holds wrong type; missing-`AddD2Auth` fail-fast; idempotency.
- `AuthAppBuilderExtensionsTests.cs` — `UseD2Auth()` integration; middleware-pipeline-order invariant.
- `Middleware/HttpContextRequestContextExtensionsTests.cs` — typed accessor returns null pre-middleware, populated value post-middleware.
- `Middleware/D2HttpContextItemsTests.cs` — slot-key constant value pinned.
- `Errors/AuthFailuresScopeInsufficientTests.cs` (in the existing `AuthFailures` test folder) — `ScopeInsufficient()` status code + error code + TK key.

Cross-transport companions (in `tests/Unit/Auth/Inbound/`):

- `RequestContextResolverParityTests.cs` — the two scoped resolver lambdas (`AddD2AuthHttp()` + `AddD2AuthGrpc()`) return equivalent results given identical `HttpContext` state. Defends against future drift between the duplicated inline lambdas.
- `DualTransportHostCompositionTests.cs` — both extensions registered in either order; interceptor on `GrpcServiceOptions.Interceptors` exactly once; `IRequestContext` resolves correctly under either transport.

Run: `dotnet test server/shared/dotnet/tests`.

## References

- [`../auth/README.md`](../auth/README.md) — JWT validator + JWKS + liveness internals
- [`../auth-grpc/README.md`](../auth-grpc/README.md) — gRPC-transport sibling
- [`../auth-abstractions/README.md`](../auth-abstractions/README.md) — `ISessionLivenessTracker`, `JwtClaimTypes`, `Audiences`, `Scopes`, `D2HttpContextItems`
- [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) — Bearer Token Usage
- [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) — Problem Details for HTTP APIs
