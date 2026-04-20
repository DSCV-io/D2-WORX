# D2.Shared.Implementations.Middleware.ServiceKey.Default

Service-to-service (S2S) API key authentication for ASP.NET Core gateways.
Validates an `X-Api-Key` header against a configured allowlist and marks the
request as a trusted service on `IRequestContext.IsTrustedService`.

Mirrors `@d2/service-key` on Node — same header name, same constant-time
comparison, same trusted-service flag semantics.

## Components

| File | Role |
|---|---|
| `ServiceKeyExtensions.cs` | `services.AddServiceKeyAuth(configuration)` + `app.UseServiceKeyDetection()` extensions. Reads valid keys from configuration section `GATEWAY_SERVICEKEY` (or supplied name) |
| `ServiceKeyMiddleware.cs` | Reads the `X-Api-Key` header, compares against valid keys with `CryptographicOperations.FixedTimeEquals` (constant-time, no timing leaks), sets `MutableRequestContext.IsTrustedService = true` on match |
| `ServiceKeyEndpointFilter.cs` | `.RequireServiceKey()` endpoint filter — applies to job endpoints / S2S-only routes that should reject browser traffic |
| `ServiceKeyOptions.cs` | Bound from configuration. `ValidKeys` is the allowlist (typically multiple to support rotation) |

## Pipeline order

`UseServiceKeyDetection()` runs **before** `UseJwtAuth()` so that
`IsTrustedService` is populated before any JWT-bearing request is evaluated.
Trusted requests then bypass the JWT fingerprint check.

```csharp
app.UseRequestEnrichment();
app.UseServiceKeyDetection();
app.UseRateLimiting();
app.UseJwtAuth();
```

## Constant-time comparison

The middleware compares the inbound key against **every** valid key (no
short-circuit), using `CryptographicOperations.FixedTimeEquals`. This
prevents an attacker from inferring key bytes via response-time
side-channels.

## Endpoint filter usage

Routes that should ONLY be reachable via S2S (e.g., Dkron job endpoints)
attach the filter:

```csharp
group.MapPost("/jobs/purge-sessions", PurgeSessionsAsync)
    .RequireServiceKey()
    .WithName("PurgeExpiredSessions");
```

For policy-style enforcement (S2S validated as a real authorization policy
rather than an endpoint filter), use `.RequireTrustedService()` from the
`AuthPolicy.Default` package — same effect, different mechanism.

## Node parity

| `@d2/service-key` | `D2.Shared.ServiceKey.Default` |
|---|---|
| `validateServiceKey(apiKey, validKeys)` | `ServiceKeyMiddleware` (constant-time loop) |
| `createServiceKeyMiddleware(keys, opts)` Hono middleware | `services.AddServiceKeyAuth(configuration)` |
| `withApiKeyAuth(grpcService, opts)` from `@d2/service-defaults/grpc` | (.NET gRPC: `IInterceptor` pattern, separate concern) |
