<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Startup

> Parent: [`public/packages/dotnet/`](../../README.md)

Deny-by-default auth endpoint boot guard for D² services. Fails host startup when any mapped `RouteEndpoint` lacks a declared auth intent — surfacing configuration errors before any traffic is served rather than silently permitting unauthenticated access at runtime.

## Purpose

Every D² service endpoint must explicitly declare what it requires from callers: a scope the caller must hold, or an explicit opt-in stating the endpoint is harmless. An endpoint with no declaration is a configuration error. This library enforces that contract at boot, not at the first unauthenticated request.

## Public API surface

### Composition

The guard is wired automatically when using `D2.Shared.ServiceDefaults` — see [`../../../service-defaults/README.md`](../../../service-defaults/README.md). For direct registration:

```csharp
services.AddD2AuthEndpointGuard();
```

`AddD2AuthEndpointGuard()` registers the `AuthEndpointGuardStartupFilter` as a transient `IStartupFilter`. Idempotent — uses `TryAddEnumerable` so calling twice does not register a second instance.

The opt-out path via `D2ServiceDefaultsOptions.SkipAuthEndpointGuard = true` is only available through the service-defaults aggregator. Callers that invoke `AddD2AuthEndpointGuard()` directly unconditionally register the guard.

### Declared intent — what satisfies the guard

Any of the following on an endpoint's `EndpointMetadataCollection` counts as a declared auth intent:

| Declaration | Transport | How it gets on the endpoint |
| --- | --- | --- |
| `EndpointScopeMetadata` | HTTP | `.RequireAnyScope(...)` / `.RequireAllScopes(...)` / `.MarkAsD2HarmlessEndpoint()` fluent call on the builder |
| `MethodScopeMetadata` | gRPC | `.RequireAnyScope(...)` / `.RequireAllScopes(...)` / `.MarkAsD2HarmlessEndpoint()` fluent call on the `MapGrpcService<T>()` builder |
| `D2RequireAnyScopeAttribute` | gRPC | `[D2RequireAnyScope("scope")]` on the service class or method |
| `D2RequireAllScopesAttribute` | gRPC | `[D2RequireAllScopes("scope", "other")]` on the service class or method |
| `D2HarmlessEndpointAttribute` | gRPC | `[D2HarmlessEndpoint]` on the service class or method |

> **Note (gRPC attributes):** the three gRPC attributes are checked transport-agnostically by this guard. Applying them to an HTTP endpoint (rather than a gRPC service class) satisfies the guard but yields no HTTP runtime enforcement — `JwtAuthMiddleware` enforces only `EndpointScopeMetadata`. Use the HTTP fluent extensions (`RequireAnyScope` / `RequireAllScopes`) for HTTP endpoints.

The guard does not interpret the declaration — it only checks for presence. Enforcement of the declared policy at runtime is the job of `JwtAuthMiddleware` (HTTP) and `JwtAuthInterceptor` (gRPC).

### Skipped endpoints

The guard skips two categories of endpoints regardless of metadata:

- **Infrastructure paths** — any `RouteEndpoint` whose route pattern matches the canonical infra-path prefixes from `D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS`: `/health`, `/alive`, `/metrics`, `/.well-known` (including sub-paths). These endpoints are served by the infrastructure bypass layer and do not require auth declarations.
- **gRPC catch-all endpoints** — `MapGrpcService<T>()` registers infrastructure catch-all slots for unknown-method routing (e.g. `{pkg}.{Svc}/{unimplementedMethod:grpcunimplemented}`). These carry no auth metadata and are not real callable methods; the guard identifies them by the `grpcunimplemented` route constraint and skips them.
- **Non-`RouteEndpoint` instances** — plain `Endpoint` base instances with no route pattern carry no route identity and cannot be guarded by convention; the guard skips them.

### Failure behavior

When the guard finds an endpoint without a declared intent, it:

1. Logs an error via `[LoggerMessage]` at `LogLevel.Error` (event 4101) listing the undeclared route templates.
2. Throws `InvalidOperationException` with a message that: names every undeclared route by its template raw text only (no route values, no query string — PII-safe); explains the three fix options (HTTP fluent, gRPC fluent, gRPC attribute); documents the `SkipAuthEndpointGuard` opt-out for test hosts.

The thrown exception propagates out of `GenericWebHostService.StartAsync` before Kestrel accepts connections. The host process exits with a non-zero exit code.

### `IStartupFilter` mechanism — why not `IHostedService`

The guard runs as `IStartupFilter` rather than `IHostedService` because:

- `IStartupFilter.Configure(next)` executes during HTTP-pipeline construction inside `GenericWebHostService.StartAsync`. The returned delegate calls `next(app)` first, which triggers `UseRouting()` — the step that merges all endpoint data sources (including `WebApplication.DataSources`, where `app.MapXxx()` calls land) into the DI-resolved `EndpointDataSource` composite. Only after `next` returns does the delegate walk the now-fully-populated endpoint set.
- An `IHostedService` starts AFTER pipeline construction. The `EndpointDataSource` singleton is resolved at DI-resolve time, before `WebApplication`'s sources are merged in — so `IHostedService` sees an empty collection in the `WebApplication` model.

The `IStartupFilter` window is the only point in the lifecycle where the endpoint set is both fully populated and still pre-traffic.

## Opt-out

Set `D2ServiceDefaultsOptions.SkipAuthEndpointGuard = true` in the service-defaults configure callback:

```csharp
builder.Services.AddD2ServiceDefaults(
    builder.Configuration,
    opts =>
    {
        opts.SkipAuthEndpointGuard = true; // test hosts, anonymous-only admin tools
        opts.SkipAuthAutoWiring = true;
    });
```

Note: `SkipAuthEndpointGuard` only takes effect when `SkipAuthAutoWiring = false` (the default). When `SkipAuthAutoWiring = true` (auth opt-out), the guard is never registered regardless of `SkipAuthEndpointGuard`.

## Dependencies

| Project reference | Why |
| --- | --- |
| `D2.Shared.Auth.Http` | `EndpointScopeMetadata` — HTTP declared-intent type. |
| `D2.Shared.Auth.Grpc` | `MethodScopeMetadata`, `D2RequireAnyScopeAttribute`, `D2RequireAllScopesAttribute`, `D2HarmlessEndpointAttribute` — gRPC declared-intent types. |
| `D2.Shared.AspNetCore` | `D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS` (infra-path list) + `InfrastructurePathMatcher`. |
| `Microsoft.AspNetCore.App` (framework ref) | `IStartupFilter`, `IApplicationBuilder`, `EndpointDataSource`, `RouteEndpoint`. |
| `Microsoft.Extensions.{DependencyInjection,Logging}.Abstractions` | DI / logging. |

## Tests

`public/packages/dotnet/tests/Unit/Auth/Inbound/Startup/`:

- `AuthEndpointGuardStartupFilterTests.cs` — `IStartupFilter` structural contract; constructor null-guard; pass cases for all five intent types (HTTP `EndpointScopeMetadata`, gRPC `MethodScopeMetadata`, `D2RequireAnyScopeAttribute`, `D2RequireAllScopesAttribute`, `D2HarmlessEndpointAttribute`); infrastructure-path skip (theory over all `DEFAULT_INFRASTRUCTURE_PATHS` + sub-paths); non-`RouteEndpoint` skip; fail cases (undeclared endpoint throws, exception message names the route template, all offenders named in multi-offender case, infra + undeclared mix names only the undeclared, PII-safe message contains only route template).
- `AuthEndpointGuardServiceCollectionExtensionsTests.cs` — DI registration; `TryAddEnumerable` idempotency; registered as `IStartupFilter` transient.

Run: `dotnet test public/packages/dotnet/tests`.

## References

- [`../http/README.md`](../http/README.md) — `EndpointScopeMetadata` + `RequireD2ScopeExtensions`
- [`../grpc/README.md`](../grpc/README.md) — `MethodScopeMetadata` + gRPC scope attributes
- [`../../../service-defaults/README.md`](../../../service-defaults/README.md) — `D2ServiceDefaultsOptions.SkipAuthEndpointGuard`
