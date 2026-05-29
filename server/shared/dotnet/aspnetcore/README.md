<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.AspNetCore

> Parent: [`server/shared/dotnet/`](../README.md)

Cross-cutting ASP.NET Core middleware + endpoint primitives every D² service composition root needs but that don't belong on a single domain lib. Six public surfaces — `UseD2SecurityHeaders`, `UseD2Cors`, `UseD2InfrastructureBypass`, `AddD2ProblemDetails`, `MapD2HealthEndpoints`, `RunD2ServiceAsync` — plus the canonical `InfrastructurePathMatcher` consumed by `D2.Shared.Logging`'s request-logging middleware and `D2.Shared.Telemetry`'s AspNetCore-instrumentation `Filter` callback so all three libs share one source of truth for `/health`, `/alive`, `/metrics`, `/.well-known` matching.

Foundation tier — depends only on `D2.Shared.Utilities` (for `Falsey()` / `Truthy()` / `ToNullIfEmpty()`) and `Serilog.AspNetCore` (for the `RunD2ServiceAsync` startup wrapper's `Log.Fatal` + `CloseAndFlushAsync`). `D2.Shared.Logging` and `D2.Shared.Telemetry` depend on this lib, not the other way around.

The lib does NOT own:

- Authentication / authorization — `D2.Shared.Auth.Http` owns JWT validation, scope checks, identity extraction.
- OpenTelemetry SDK setup — `D2.Shared.Telemetry` owns it.
- Serilog configuration / sinks — `D2.Shared.Logging` owns them.
- `[LoggerMessage]` source-generated delegates — middleware in this lib runs in the request pipeline; logging at MEL-bridge time is cleaner via the host's standard `ILogger<T>` injection (and the startup wrapper uses Serilog static `Log.*` calls because host startup runs outside the request pipeline).

## Public API surface

### Composition

```csharp
// builder.Services
services.AddD2HealthChecks();
services.AddD2Cors(builder.Configuration);   // reads D2_CORS_ORIGINS__0/1/...
services.AddD2ProblemDetails();

// builder.Build() then app pipeline
app.UseD2SecurityHeaders();
app.UseRouting();
app.UseD2InfrastructureBypass();             // tags + (default) short-circuits
app.UseD2Cors();
// ... business middleware (auth, rate-limit, idempotency, etc.) ...
app.MapD2HealthEndpoints();                  // /health + /alive
// ... other Map* ...

await app.RunD2ServiceAsync("edge");
```

### `UseD2SecurityHeaders(Action<D2SecurityHeadersOptions>?)`

Installs a middleware that writes a OWASP-aligned default header set on every response via `HttpResponse.OnStarting`:

| Header                              | Default value                                      | Reason                                                                            |
| ----------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------------------- |
| `X-Content-Type-Options`            | `nosniff`                                          | Prevents MIME-sniffing attacks.                                                   |
| `X-Frame-Options`                   | `DENY`                                             | Prevents clickjacking via framing.                                                |
| `Referrer-Policy`                   | `strict-origin-when-cross-origin`                  | MDN-recommended default; preserves Referer same-origin, only origin cross-origin. |
| `X-Permitted-Cross-Domain-Policies` | `none`                                             | Blocks legacy Adobe Flash / PDF cross-domain.                                     |
| `Cross-Origin-Resource-Policy`      | `same-origin`                                      | Prevents cross-origin embedding (CORB / CORP).                                    |
| `Cross-Origin-Opener-Policy`        | `same-origin`                                      | Isolates browsing context (Spectre mitigation).                                   |
| `Strict-Transport-Security`         | `max-age=31536000; includeSubDomains` (HTTPS only) | 1-year HSTS with subdomain coverage.                                              |

Per-header override semantic on `D2SecurityHeadersOptions`: `null` override → default written; empty / whitespace override → header suppressed; non-empty override → override literal written.

HSTS preload submission is intentionally NOT included by default — preload is a one-way door (once the apex domain is in the browser-built-in preload list, removal is slow and incomplete). Each service that wants preload submission opts in by setting `StrictTransportSecurity` to a value that includes `preload`.

The following are NOT set by default (each is service-specific):

- `Content-Security-Policy` — service-specific (each service knows its own script / style / connect sources).
- `Permissions-Policy` — service-specific (each service knows which browser features it uses).
- `X-XSS-Protection` — deprecated by all major browsers (replaced by CSP).

### `AddD2Cors(IConfiguration, Action<D2CorsOptions>?)` + `UseD2Cors()`

Registers the `D2_DEFAULT` CORS policy reading the canonical indexed env-var convention `D2_CORS_ORIGINS__0`, `D2_CORS_ORIGINS__1`, ... per .NET `IConfiguration` array binding. Validates fail-closed at host build via `ValidateOnStart()` — empty origins list is a deliberate decision, not an oversight.

Default `D2CorsOptions`:

| Property                 | Default                                                                                                                                                                                                                     |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Origins`                | `[]` (validated non-empty at startup; fail-closed)                                                                                                                                                                          |
| `AllowedHeaders`         | `Content-Type`, `Authorization`, `X-Correlation-Id` (from spec-driven `HttpHeaders.CORRELATION_ID`), `Idempotency-Key` (from spec-driven `HttpHeaders.IDEMPOTENCY_KEY`), `X-Forwarded-For`, `X-Real-IP`, `CF-Connecting-IP` |
| `AllowedMethods`         | `GET`, `HEAD`, `POST`, `PUT`, `PATCH`, `DELETE`, `OPTIONS`                                                                                                                                                                  |
| `AllowCredentials`       | `true` (BFF cookies + service-to-service Authorization headers)                                                                                                                                                             |
| `PreflightMaxAgeSeconds` | `600` (10 minutes)                                                                                                                                                                                                          |

The `AllowCredentials = true` + `Origins = ["*"]` combination is forbidden per CORS spec; the validator rejects it at host build.

Services that need NO CORS don't call `AddD2Cors` at all — calling it IS the explicit decision to use CORS. New canonical `X-D2-*` headers added to any cross-cutting middleware MUST be added to `AllowedHeaders` in the same change per the doc-parity discipline.

### `UseD2InfrastructureBypass(Action<D2InfrastructureBypassOptions>?)`

Installs a middleware that, for each request, sets `HttpContext.Items["D2.IsInfrastructure"]` to a boolean indicating whether the request path matches the configured infrastructure-path list (default: `/health`, `/alive`, `/metrics`, `/.well-known`).

Default short-circuit mode (`TagOnly = false`): when the path matches AND a routing-resolved endpoint is present on the context, the middleware invokes the matched endpoint's `RequestDelegate` directly and returns — bypassing every middleware registered AFTER this one. Heavy business middleware (rate limiting, idempotency, auth) does NOT execute on cheap probe / metrics / well-known requests.

Tag-only mode (`TagOnly = true`): the middleware ONLY sets the `Items` flag and continues the pipeline. Business middleware downstream still runs and can opt-out by reading the flag — for services that want custom middleware to execute on infrastructure paths.

Pipeline placement: install AFTER `app.UseRouting()` (which resolves the matched endpoint without invoking it) and BEFORE the business middleware. When no endpoint has been routed (caller put bypass before `UseRouting()`), the middleware falls through to the next delegate so the pipeline still completes correctly.

### `AddD2ProblemDetails(Action<D2ProblemDetailsOptions>?)`

Registers ASP.NET Core's `IProblemDetailsService` with the D² customizer applied as the `CustomizeProblemDetails` callback. The customizer is FULL D2Result-aware (path B of the RFC 7807 emit stack — sibling to `D2.Shared.Auth.Http`'s path A `D2ProblemDetailsExtensions.ToProblemDetails`).

When the request pipeline has stashed a `D2Result` on `HttpContext.Items[D2ProblemDetailsContextItems.D2_RESULT]` (via the `SetD2Result` typed extension), the customizer populates the RFC 7807 Shape A body from spec-driven constants in `D2ProblemDetailsKeys` ([`problem-details/abstractions/`](../problem-details/abstractions/README.md)):

1. `Type` ← `TYPE_URI_PREFIX + KebabCase(D2Result.ErrorCode)` (fallback `"unhandled-exception"` on empty error code).
2. `Title` ← `TitleFor(D2Result.StatusCode)`.
3. `Status` ← `(int)D2Result.StatusCode`.
4. `Extensions[d2_error_code]` ← `D2Result.ErrorCode`.
5. `Extensions[d2_messages]` ← `D2Result.Messages`.
6. `Extensions[d2_input_errors]` ← `D2Result.InputErrors` (only when non-empty).

Whether or not a `D2Result` is stashed, the customizer always populates:

7. `Extensions[traceId]` ← `Activity.Current?.TraceId.ToString()` falling back to `HttpContext.TraceIdentifier`.
8. `Extensions[correlationId]` ← inbound `X-Correlation-Id` request header (capped at 128 chars). When absent / over-cap, generates `Guid.NewGuid().ToString("N")` and (when `EchoCorrelationIdInResponse` is true) writes it to the response header.
9. `Instance` ← `"{Method} {Path}"` when `IncludeRequestPath` is true (matches the path-A emit shape exactly — cross-path wire-shape consistency by construction).

Cross-language parity: the .NET path-A + path-B body shapes are byte-identical, AND match the TS-side BFF `toProblemDetails` output for the same `D2Result` inputs. All three emit sites consume the same spec-derived constants.

PII discipline: the customizer NEVER reads `HttpContext.Request.QueryString`, `Request.Body`, or any user-input source. The 128-char cap on the inbound correlation id prevents an arbitrary-length user header from inflating the response body — values exceeding the cap are treated as absent.

**Stashing a D2Result for the customizer**: handlers / middleware call `httpContext.SetD2Result(result)` ahead of letting the response flow through the `AddProblemDetails` pipeline. Consumers that don't stash a D2Result (raw exception path) still get `traceId` + `correlationId` + `instance` for diagnostic correlation; the framework defaults for `Type` / `Title` apply.

### `AddD2HealthChecks()` + `MapD2HealthEndpoints()`

`AddD2HealthChecks()` registers a baseline `"self"` check tagged `"live"` that always returns `Healthy`. Idempotent — calling twice no-ops on the second call. Per-service infrastructure layers add their own checks (DB, Redis, RabbitMQ, KeyCustodian) by chaining `services.AddHealthChecks().AddDbContextCheck<...>()` etc. — those auto-flow into `/health`. Checks tagged `"live"` additionally participate in `/alive`.

`MapD2HealthEndpoints()` maps:

- `/health` — full health-check status (every registered check, regardless of tag).
- `/alive` — only checks tagged `"live"` (the kubernetes-conventional liveness split).

### `RunD2ServiceAsync(string? serviceName = null)`

Async wrapper around `WebApplication.RunAsync()` that:

- Logs `Log.Information("Starting {ServiceName} ({EnvironmentName})", ...)` at entry.
- On exception: logs `Log.Fatal` with PII-safe exception rendering — type FullName + first stack frame only, NEVER `ex.Message`. Re-throws so the host exit code reflects failure.
- In `finally`: awaits `Log.CloseAndFlushAsync()` to drain Serilog's buffered batch sink before process exit.

PII discipline: exception messages at host startup can carry connection strings, configured secrets, and host-environment specifics; the wrapper renders only the type FullName + first stack frame so log dashboards reflect the failure class without leaking secrets via `ex.Message`. Operators triage deeper via the host's process logs.

Async form captures both synchronously-faulted (host build / hosted-service `StartAsync`) and asynchronously-faulted (post-startup, mid-request) exceptions.

### Constants — `D2AspNetCoreConstants`

| Constant                               | Value                                                                                          |
| -------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `HEALTH_ENDPOINT_PATH`                 | `/health`                                                                                      |
| `ALIVE_ENDPOINT_PATH`                  | `/alive`                                                                                       |
| `METRICS_ENDPOINT_PATH`                | `/metrics`                                                                                     |
| `WELL_KNOWN_ENDPOINT_PATH`             | `/.well-known`                                                                                 |
| `LIVE_HEALTH_TAG`                      | `live`                                                                                         |
| `SELF_HEALTH_CHECK_NAME`               | `self`                                                                                         |
| `DEFAULT_INFRASTRUCTURE_PATHS`         | `[HEALTH_ENDPOINT_PATH, ALIVE_ENDPOINT_PATH, METRICS_ENDPOINT_PATH, WELL_KNOWN_ENDPOINT_PATH]` |
| `CORS_ORIGINS_CONFIG_KEY`              | `D2_CORS_ORIGINS`                                                                              |
| `DEFAULT_CORS_POLICY_NAME`             | `D2_DEFAULT`                                                                                   |
| `MAX_CORRELATION_ID_LENGTH`            | `128`                                                                                          |
| `INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY` | `D2.IsInfrastructure`                                                                          |

Note: wire-header values consumed by this lib live in the spec-driven [`HttpHeaders`](../headers/http/README.md) catalog — `HttpHeaders.CORRELATION_ID` (`"X-Correlation-Id"`) and `HttpHeaders.IDEMPOTENCY_KEY` (`"Idempotency-Key"`) are referenced directly. One wire value for one concept across the platform; no intra-.NET drift between the CORS allowlist / ProblemDetails customizer and the headers/http catalog.

### Constants — `D2ProblemDetailsContextItems`

HttpContext.Items slot keys consumed by the path-B Customizer to source the originating `D2Result`:

| Constant    | Value         | Use                                                                                                                                                                                                                                                                               |
| ----------- | ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2_RESULT` | `__d2_result` | Slot under which middleware / handlers stash the originating `D2Result` so the customizer can populate the full RFC 7807 Shape A body from spec constants. Set via the typed `httpContext.SetD2Result(result)` extension; read by the customizer via `httpContext.GetD2Result()`. |

### `InfrastructurePathMatcher` (public static)

Single source of truth for infrastructure-path matching across the D² shared-lib stack. Consumed by `D2.Shared.Logging.WebApplicationLoggingExtensions.UseD2RequestLogging` to down-rank request-completion log lines for infrastructure endpoints to `Verbose`, by `D2.Shared.Telemetry.TelemetryServiceCollectionExtensions.AddD2Telemetry` in the AspNetCore-instrumentation `Filter` callback to suppress auto-spans, and by `UseD2InfrastructureBypass` (this lib).

```csharp
bool IsInfrastructurePath(PathString path, IReadOnlyList<string>? infrastructurePaths);
```

Uses the AspNetCore-canonical `PathString.StartsWithSegments(PathString)` overload — case-insensitive, segment-boundary-matched (`/healthz` does NOT match prefix `/health`; `/health/db` does). Empty `PathString`, null configured list, and per-entry null / empty / whitespace prefixes are all defensive no-ops (returns `false` rather than throwing).

## File layout

| File                                                  | Role                                                                                                          |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.AspNetCore.csproj`                         | csproj — `Microsoft.NET.Sdk.Web` + `OutputType=Library`. Project reference to `utilities/`.                   |
| `D2AspNetCoreConstants.cs`                            | Public constants (endpoint paths, header names, config keys, default infrastructure-path list).               |
| `InfrastructurePathMatcher.cs`                        | Public static helper — canonical path-matcher consumed by Logging + Telemetry + this lib's bypass middleware. |
| `D2SecurityHeadersOptions.cs`                         | Sealed record — per-header override Options-pattern config.                                                   |
| `D2CorsOptions.cs`                                    | Sealed record — CORS policy Options-pattern config.                                                           |
| `D2InfrastructureBypassOptions.cs`                    | Sealed record — bypass middleware Options-pattern config.                                                     |
| `D2ProblemDetailsOptions.cs`                          | Sealed record — ProblemDetails customizer Options-pattern config.                                             |
| `SecurityHeadersApplicationBuilderExtensions.cs`      | Public extension: `UseD2SecurityHeaders`.                                                                     |
| `CorsServiceCollectionExtensions.cs`                  | Public extension: `AddD2Cors`.                                                                                |
| `CorsApplicationBuilderExtensions.cs`                 | Public extension: `UseD2Cors`.                                                                                |
| `InfrastructureBypassApplicationBuilderExtensions.cs` | Public extension: `UseD2InfrastructureBypass`.                                                                |
| `ProblemDetailsServiceCollectionExtensions.cs`        | Public extension: `AddD2ProblemDetails`.                                                                      |
| `HealthEndpointsServiceCollectionExtensions.cs`       | Public extension: `AddD2HealthChecks`.                                                                        |
| `HealthEndpointsRouteBuilderExtensions.cs`            | Public extension: `MapD2HealthEndpoints`.                                                                     |
| `RunD2ServiceWebApplicationExtensions.cs`             | Public extension: `RunD2ServiceAsync`.                                                                        |
| `Internal/SecurityHeadersMiddleware.cs`               | Internal sealed middleware impl behind `UseD2SecurityHeaders`.                                                |
| `Internal/InfrastructureBypassMiddleware.cs`          | Internal sealed middleware impl behind `UseD2InfrastructureBypass`.                                           |
| `Internal/D2ProblemDetailsCustomizer.cs`              | Internal helper — the `Action<ProblemDetailsContext>` body.                                                   |

## Dependencies

| Package                 | Why                                                                                                               |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `Serilog.AspNetCore`    | Static `Log.*` facade used by `RunD2ServiceAsync`. Already pinned (consumed transitively by `D2.Shared.Logging`). |
| `JetBrains.Annotations` | `[MustDisposeResource]` annotations on disposable factory paths (none currently; consumed transitively).          |

| Project reference     | Why                                                                                                                                                                                |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Utilities` | `Falsey()` / `Truthy()` / `ToNullIfEmpty()` extensions consumed throughout (options validation, env-var resolution, header-override tri-state, per-entry path / origin filtering). |

The `Microsoft.AspNetCore.App` framework reference (via `Microsoft.NET.Sdk.Web`) provides `IApplicationBuilder`, `IEndpointRouteBuilder`, `WebApplication`, `HttpContext`, `PathString`, `ProblemDetails`, `ProblemDetailsContext`, `IProblemDetailsService`, `HealthCheckResult`, `HealthCheckOptions`, the CORS `CorsPolicyBuilder` + middleware, and the `Microsoft.Extensions.{DependencyInjection,Options,Configuration,Logging,Hosting}.Abstractions` packages.

## Edge cases / gotchas

- **HSTS only on HTTPS.** The `Strict-Transport-Security` header is conditionally written only when `request.IsHttps` is `true` — HSTS over HTTP is meaningless and the spec forbids preload submission for non-HTTPS-only origins. TestHost defaults to HTTPS; tests covering the HTTP branch construct an explicit `http://` request URI.
- **CORS fail-closed on empty origins.** `D2_CORS_ORIGINS__*` empty / unset → `ValidateOnStart()` raises `OptionsValidationException` at host build. Services that need NO CORS don't call `AddD2Cors` at all.
- **`AllowCredentials = true` + `Origins = ["*"]` is forbidden.** The validator rejects this combination per CORS spec — listing explicit origins is the only safe way to accept credentials cross-origin.
- **Infrastructure-bypass requires `UseRouting()` first.** The short-circuit invokes the routing-matched endpoint directly via `context.GetEndpoint()?.RequestDelegate`. When no endpoint has been routed yet (caller put bypass before `UseRouting()`), the middleware falls through to the next delegate so the pipeline still completes correctly — but the bypass intent is then a no-op.
- **`AddD2HealthChecks` is idempotent; `MapD2HealthEndpoints` is NOT.** Calling `MapD2HealthEndpoints` twice raises a duplicate-route exception per the underlying ASP.NET Core endpoint-routing convention; per-pipeline registration SHOULD happen exactly once.
- **`RunD2ServiceAsync` uses PII-safe exception rendering.** `Log.Fatal` on the catch path captures only the exception type FullName + first stack frame — NEVER `ex.Message`, since exception messages at host startup can carry connection strings, configured secrets, and host-environment specifics. Operators triage deeper via the host's process logs.
- **`X-Correlation-Id` length-cap.** The ProblemDetails customizer caps the inbound `X-Correlation-Id` header value at 128 chars; over-cap values are treated as absent and a fresh GUID is generated. Prevents an arbitrary-length user header from inflating the response body.
- **`InfrastructurePathMatcher` is public — single source of truth for `D2.Shared.Logging`, `D2.Shared.Telemetry`, and this lib's bypass middleware.** Earlier per-lib `internal` duplicates were collapsed into this canonical public matcher in the same change that introduced `D2.Shared.AspNetCore` so all consumers stay aligned on the path set.
- **`RunD2ServiceAsync` consumes `D2.Shared.Utilities.Diagnostics.SanitizedExceptionRender`** for the `Log.Fatal` PII-safe exception rendering. The helper is the canonical foundation-lib copy shared with `D2.Shared.Auth`, `D2.Shared.Auth.Outbound`, and `D2.Shared.Messaging.RabbitMq` — `D2.Shared.Utilities` is the natural home (already a `ProjectReference` for `Falsey()` / `Truthy()` / `ToNullIfEmpty()`).
