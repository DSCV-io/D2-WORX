<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.ServiceDefaults

> Parent: [`server/shared/dotnet/`](../README.md)

The composition-root convenience csproj that wires every prior shared lib into a single `AddD2ServiceDefaults()` + `UseD2DefaultPipeline()` + `MapD2DefaultEndpoints()` + `RunD2ServiceAsync()` extension surface, so per-service `Program.cs` shrinks to ~10-15 lines of service-specific declarations.

**Pure thin aggregator — owns ZERO logic.** Every behavior is owned by a logic-bearing prior shared lib (`D2.Shared.Logging`, `D2.Shared.Telemetry`, `D2.Shared.AspNetCore`, `D2.Shared.I18n`, `D2.Shared.Handler`, `D2.Shared.Auth`, `D2.Shared.Auth.Http`, `D2.Shared.Auth.Grpc`, `D2.Shared.Caching.Local.Default`, `D2.Shared.Utilities`). Pattern parity inspiration: `Microsoft.Extensions.ServiceDefaults` from .NET Aspire, adapted for the D² shared-lib stack with locked middleware ordering and Serilog-based PII discipline.

The lib does NOT own (each is opt-in via the owning lib's own builder):

- Encryption — `D2.Shared.Encryption` (per-domain keying).
- Distributed cache — `D2.Shared.Caching.Distributed.Redis`.
- Tiered cache — `D2.Shared.Caching.Tiered`.
- PostgreSQL — `D2.Shared.Handler.Repo.Postgres`.
- RabbitMQ — `D2.Shared.Messaging.RabbitMq`.
- Outbound auth — `D2.Shared.Auth.Outbound`.

## Public API surface

```csharp
using D2.Shared.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddD2ServiceDefaults(
    builder.Configuration,
    opts =>
    {
        opts.AuthConfigure = auth =>
        {
            auth.Issuer = new Uri("https://edge.example.com");
            auth.Audience = "files-service";
        };
    });

var app = builder.Build();

app.UseD2DefaultPipeline();
app.MapD2DefaultEndpoints();

// service-specific endpoints + Map* calls here

await app.RunD2ServiceAsync("files");
```

### `AddD2ServiceDefaults(IConfiguration, Action<D2ServiceDefaultsOptions>?)`

Wires the canonical D² service-defaults stack in this exact order:

1. `D2Env.Load()` — discovers + loads `.env` / `.env.local` / `.env.secrets` from the nearest discovery directory (idempotent; container deploys are no-ops because Compose injects env vars before host start).
2. `AddD2Logging(configuration, opts.LoggingConfigure)` — Serilog + `RedactDataDestructuringPolicy` + machine-name + service-name + environment enrichers + `CompactJsonFormatter` console sink + MEL bridge with `writeToProviders: true`.
3. `AddD2Telemetry(configuration, opts.TelemetryConfigure)` — OTel SDK (traces + metrics + logs) + OTLP exporters (when canonical env vars set) + AspNetCore + HttpClient + GrpcNetClient + Process + Runtime auto-instrumentations + Prometheus exporter (when enabled). Honors `OTEL_SDK_DISABLED`.
4. `AddD2I18n(configuration)` — `SupportedLocales` + `ITranslator` singletons. Idempotent. Reads `PUBLIC_DEFAULT_LOCALE` + indexed `PUBLIC_ENABLED_LOCALES__*`. The lib has no `Action<T>` config callback, so `D2ServiceDefaultsOptions` does NOT carry an `I18nConfigure` field.
5. `AddD2Handler()` — open-generic `HandlerContext<>` Transient registration. Idempotent.
6. `AddD2Auth(opts.AuthConfigure).AddD2AuthHttp().AddD2AuthGrpc()` — JWKS provider, session liveness tracker, JWT validator, named OIDC discovery `HttpClient`, backplane subscribers, HTTP middleware, gRPC interceptor, scoped `IRequestContext` resolver. Skipped when `SkipAuthAutoWiring = true`. Also registers `AddD2AuthEndpointGuard()` (deny-by-default boot guard — see [`../auth/startup/README.md`](../auth/startup/README.md)) unless `SkipAuthEndpointGuard = true`.
7. `AddD2LocalCache(opts.LocalCacheConfigure)` — `DefaultLocalCache` as `ILocalCache` singleton. Idempotent. Skipped when `SkipLocalCacheAutoWiring = true`.
8. `AddD2HealthChecks()` — baseline `"self"` check tagged `"live"`. Idempotent.
9. `AddD2ProblemDetails(opts.ProblemDetailsConfigure)` — RFC 7807 customizer (`traceId` + `correlationId` + `instance` enrichment).
10. `AddD2Cors(configuration, opts.CorsConfigure)` — `D2_DEFAULT` policy + indexed `D2_CORS_ORIGINS__*` env-var binding. Fail-closed via `ValidateOnStart()`.

**Auth wiring contract (fail-fast)**: when `SkipAuthAutoWiring = false` (the default), `AuthConfigure` MUST be non-null — the aggregator throws `InvalidOperationException` at host build with a remediation message otherwise. Setting `SkipAuthAutoWiring = true` opts out of auth wiring entirely (test hosts, anonymous-only admin endpoints).

### `UseD2DefaultPipeline()` — LOCKED middleware order

```csharp
app.UseD2SecurityHeaders(opts.SecurityHeadersConfigure);
app.UseD2RequestLogging();
app.UseD2Cors();
app.UseRouting();
app.UseD2InfrastructureBypass(opts.InfrastructureBypassConfigure);
app.UseAuthentication();
app.UseD2Auth();
app.UseAuthorization();
```

No insertion points exposed — services that need bespoke ordering call the underlying lib extensions themselves and skip `UseD2DefaultPipeline`. The order rationale (companion Edge rate-limit design — Canonical: not yet shipped; design at [`docs/v2/PHASE_3_RATE_LIMITING.md`](../../../../docs/v2/PHASE_3_RATE_LIMITING.md). Will migrate to the Edge lib README when Edge rate-limiting ships):

- **`UseD2SecurityHeaders` first** — OWASP headers apply on EVERY response, including responses produced by middleware that short-circuits the pipeline (CORS preflight, infrastructure bypass).
- **`UseD2RequestLogging` early (before routing)** — even early-pipeline failures emit a structured request-completion line.
- **`UseD2Cors` after RequestLogging, before Routing** — CORS preflight (OPTIONS) must short-circuit before routing tries to match a verb-specific endpoint.
- **`UseRouting` then `UseD2InfrastructureBypass`** — bypass needs the routing-resolved endpoint on the context to invoke the matched `RequestDelegate` directly when short-circuiting.
- **`UseAuthentication` → `UseD2Auth` → `UseAuthorization`** — JWT auth middleware (`UseD2Auth`) requires the AspNetCore authentication feature on the context (`UseAuthentication`) and runs BEFORE `UseAuthorization` so the authorization stage fires scope / policy gates against the populated `IRequestContext`.

The rate-limit middleware (an Edge concern, owned outside this lib) slots BETWEEN `UseD2InfrastructureBypass` and `UseAuthentication` per the companion Edge rate-limit design (Canonical: not yet shipped; design at [`docs/v2/PHASE_3_RATE_LIMITING.md`](../../../../docs/v2/PHASE_3_RATE_LIMITING.md). Will migrate to the Edge lib README when Edge rate-limiting ships). The current LOCKED order matches that wiring.

### `MapD2DefaultEndpoints()`

```csharp
endpoints.MapD2HealthEndpoints();        // /health (full) + /alive (live-tag-only)
endpoints.MapD2PrometheusEndpoint();     // /metrics — IP-restricted, honors OTEL_SDK_DISABLED
```

### `RunD2ServiceAsync(string? serviceName = null)`

Re-exports `D2.Shared.AspNetCore.RunD2ServiceWebApplicationExtensions.RunD2ServiceAsync` at the aggregator namespace so a single `using D2.Shared.ServiceDefaults;` directive at a composition root makes every default surface available without importing each underlying lib's namespace separately. Behavior is identical to the underlying:

- Logs `Log.Information("Starting {ServiceName} ({EnvironmentName})", ...)` at entry.
- On exception: logs `Log.Fatal` with PII-safe exception rendering — type FullName + first stack frame only, NEVER `ex.Message`. Re-throws so the host exit code reflects failure.
- In `finally`: awaits `Log.CloseAndFlushAsync()` to drain Serilog's buffered batch sink before process exit.

## Opt-out matrix

| `D2ServiceDefaultsOptions` flag    | When `true`, the aggregator does NOT call...                               |
| ---------------------------------- | -------------------------------------------------------------------------- |
| `SkipAuthAutoWiring`               | `AddD2Auth` / `AddD2AuthHttp` / `AddD2AuthGrpc` / `AddD2AuthEndpointGuard` |
| `SkipAuthEndpointGuard`            | `AddD2AuthEndpointGuard` (only effective when `SkipAuthAutoWiring = false`) |
| `SkipLocalCacheAutoWiring`         | `AddD2LocalCache`                                                          |

Defaults are all `false` — every component is auto-wired by default. The opt-out flag set is intentionally narrow: only components where the >95% case wants auto-wire AND a small set of services (test hosts, dry-run admin tools) need to opt out. Components without an opt-out flag (Logging, Telemetry, I18n, Handler, HealthChecks, ProblemDetails, Cors) are wired unconditionally because every D² service needs them.

## Per-component pass-through `Action<TFromOwningLib>?` delegates

| Property                        | Forwards to                                                                                                                                        |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LoggingConfigure`              | `AddD2Logging`'s `Action<D2LoggingOptions>?`                                                                                                       |
| `TelemetryConfigure`            | `AddD2Telemetry`'s `Action<D2TelemetryOptions>?`                                                                                                   |
| `CorsConfigure`                 | `AddD2Cors`'s `Action<D2CorsOptions>?`                                                                                                             |
| `ProblemDetailsConfigure`       | `AddD2ProblemDetails`'s `Action<D2ProblemDetailsOptions>?`                                                                                         |
| `SecurityHeadersConfigure`      | `UseD2SecurityHeaders`'s `Action<D2SecurityHeadersOptions>?` (applied at pipeline-installation time)                                               |
| `InfrastructureBypassConfigure` | `UseD2InfrastructureBypass`'s `Action<D2InfrastructureBypassOptions>?` (applied at pipeline-installation time)                                     |
| `LocalCacheConfigure`           | `AddD2LocalCache`'s `Action<LocalCacheOptions>?`                                                                                                   |
| `AuthConfigure`                 | `AddD2Auth`'s required `Action<AuthOptions>` (the underlying lib has no parameterless overload — every caller MUST populate `Issuer` + `Audience`). |

The aggregator owns ZERO field-level configuration knowledge. New options on any owning lib show up at the aggregator's call site automatically — no aggregator-side maintenance required.

## File layout

| Path                                            | Role                                                                                                                              |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.ServiceDefaults.csproj`              | csproj — `Microsoft.NET.Sdk.Web` + `OutputType=Library`. 11 `ProjectReference`s + `JetBrains.Annotations` package.               |
| `ServiceDefaultsServiceCollectionExtensions.cs` | The `AddD2ServiceDefaults` extension. Body = ordered sequence of `services.AddD2X(...)` calls.                                    |
| `WebApplicationServiceDefaultsExtensions.cs`    | The `UseD2DefaultPipeline` + `MapD2DefaultEndpoints` + `RunD2ServiceAsync` extensions.                                            |
| `D2ServiceDefaultsOptions.cs`                   | Sealed options class — opt-out flags + per-component pass-through `Action<T>?` delegates.                                         |
| `D2ServiceDefaultsConstants.cs`                 | Empty placeholder — this aggregator owns no env-var keys; every constant lives on its owning lib's own `D2*Constants` class.      |

## Dependencies

| Project reference                 | Why                                                                                                                                                                           |
| --------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Logging`               | `AddD2Logging` + `UseD2RequestLogging`                                                                                                                                        |
| `D2.Shared.Telemetry`             | `AddD2Telemetry` + `MapD2PrometheusEndpoint`                                                                                                                                  |
| `D2.Shared.AspNetCore`            | `AddD2HealthChecks` + `AddD2ProblemDetails` + `AddD2Cors` + `MapD2HealthEndpoints` + `UseD2SecurityHeaders` + `UseD2Cors` + `UseD2InfrastructureBypass` + `RunD2ServiceAsync` |
| `D2.Shared.I18n`                  | `AddD2I18n`                                                                                                                                                                   |
| `D2.Shared.Handler`               | `AddD2Handler`                                                                                                                                                                |
| `D2.Shared.Auth`                  | `AddD2Auth`                                                                                                                                                                   |
| `D2.Shared.Auth.Http`             | `AddD2AuthHttp` + `UseD2Auth`                                                                                                                                                 |
| `D2.Shared.Auth.Grpc`             | `AddD2AuthGrpc`                                                                                                                                                               |
| `D2.Shared.Auth.Startup`          | `AddD2AuthEndpointGuard` — deny-by-default boot guard                                                                                                                         |
| `D2.Shared.Caching.Local.Default` | `AddD2LocalCache`                                                                                                                                                             |
| `D2.Shared.Utilities`             | `D2Env.Load`                                                                                                                                                                  |

| Package reference     | Why                                                                                                           |
| --------------------- | ------------------------------------------------------------------------------------------------------------- |
| `JetBrains.Annotations` | Transitive helper attributes (consumed nowhere directly here, but matches sibling foundation csproj pattern). |

## Edge cases / gotchas

- **`AuthConfigure` null + `SkipAuthAutoWiring = false` is fail-fast.** The aggregator throws `InvalidOperationException` at host build with a clear remediation message. The fail-fast prevents services from accidentally shipping without auth wiring; opt out of auth entirely by setting `SkipAuthAutoWiring = true` (test hosts, anonymous-only admin endpoints).
- **`OTEL_SDK_DISABLED=true` short-circuits both `AddD2Telemetry` and `MapD2PrometheusEndpoint` symmetrically.** No OTel providers / exporters are registered AND the `/metrics` route is not mapped. Consumers MUST tolerate the absence of both surfaces under the kill-switch condition.
- **`AddD2I18n` has no `Action<T>` overload.** The aggregator passes `IConfiguration` only. The lib defaults `messagesDirectory` to `{AppContext.BaseDirectory}/messages` populated at build time via the consuming csproj's `<Content Include="...contracts/messages/*.json" />` item group.
- **`UseD2DefaultPipeline` middleware ordering is LOCKED — no insertion points.** Services that need bespoke ordering call the underlying lib extensions themselves and skip the aggregator's pipeline method entirely. Per the companion Edge rate-limit design (Canonical: not yet shipped; design at [`docs/v2/PHASE_3_RATE_LIMITING.md`](../../../../docs/v2/PHASE_3_RATE_LIMITING.md). Will migrate to the Edge lib README when Edge rate-limiting ships).
- **`MapD2DefaultEndpoints` MUST run exactly once per host.** `MapD2HealthEndpoints` raises a duplicate-route exception per the underlying ASP.NET Core endpoint-routing convention; `MapD2PrometheusEndpoint` similarly. The aggregator does not idempotency-guard the `Map*` calls.
- **`SecurityHeadersConfigure` and `InfrastructureBypassConfigure` apply at pipeline-installation time.** Both underlying middleware extensions accept their `Action<T>?` at `Use*` time, not at service-registration time. The aggregator resolves `IOptions<D2ServiceDefaultsOptions>` from `app.ApplicationServices` to read these — when no options were registered (typical case; `AddD2ServiceDefaults` snapshots into a local but doesn't bind into DI), each underlying middleware uses its own defaults.
- **`D2Env.Load` is idempotent.** Calling `AddD2ServiceDefaults` twice is safe — the second `D2Env.Load` no-ops via the `s_loaded` flag.
