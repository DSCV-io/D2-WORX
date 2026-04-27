# REST

HTTP/REST gateway providing public API access to D2 microservices via gRPC backend calls. Thin routing layer with versioned APIs, OpenAPI documentation, and standardized error handling.

## Files

| File Name                                              | Description                                                                                           |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| [Program.cs](Program.cs)                               | Service bootstrap with gRPC clients, middleware pipeline, endpoint registration, and OpenAPI.         |
| [ResultExtensions.cs](ResultExtensions.cs)             | Extension methods for converting gRPC `D2ResultProto` responses to HTTP results.                      |
| [REST.csproj](REST.csproj)                             | Project file — references shared middleware packages, gRPC client factory, and proto-generated types. |
| [REST.http](REST.http)                                 | HTTP request file for testing endpoints in Rider/VS Code.                                             |
| [GeoEndpoints.cs](Endpoints/GeoEndpoints.cs)           | Geo gRPC client + REST routes that proxy to Geo (`/api/v1/geo/...`).                                  |
| [AuthEndpoints.cs](Endpoints/AuthEndpoints.cs)         | Auth gRPC client (placeholder — no REST routes yet).                                                  |
| [CommsEndpoints.cs](Endpoints/CommsEndpoints.cs)       | Comms gRPC client + `/api/v1/notification-preferences` (GET + PUT).                                   |
| [FilesEndpoints.cs](Endpoints/FilesEndpoints.cs)       | Files gRPC client (placeholder — no REST routes yet).                                                 |
| [SignalREndpoints.cs](Endpoints/SignalREndpoints.cs)   | SignalR push gRPC client (placeholder — no REST routes yet).                                          |
| [AuthJobEndpoints.cs](Endpoints/AuthJobEndpoints.cs)   | Auth scheduled job endpoints — proxies to Auth gRPC `AuthJobService` (Dkron-only).                    |
| [GeoJobEndpoints.cs](Endpoints/GeoJobEndpoints.cs)     | Geo scheduled job endpoints — proxies to Geo gRPC `GeoJobService` (Dkron-only).                       |
| [CommsJobEndpoints.cs](Endpoints/CommsJobEndpoints.cs) | Comms scheduled job endpoints — proxies to Comms gRPC `CommsJobService` (Dkron-only).                 |
| [HealthEndpoints.cs](Endpoints/HealthEndpoints.cs)     | Aggregated health check endpoint fanning out to Geo, Auth, Comms, and gateway cache.                  |

All middleware (JWT auth, service key, fingerprint validation, request enrichment, rate limiting, idempotency, translation) lives in shared packages under `backends/dotnet/shared/Implementations/Middleware/`. The REST gateway has no local `Auth/` or `Middleware/` directories — it consumes shared packages via project references.

### Shared Middleware Packages

| Package                                                                                           | Provides                                                                                |
| ------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| [`JwtAuth.Default`](../../shared/Implementations/Middleware/JwtAuth.Default/)                     | JWT Bearer auth (RS256/JWKS) and `JwtFingerprintMiddleware` / `JwtFingerprintValidator` |
| [`ServiceKey.Default`](../../shared/Implementations/Middleware/ServiceKey.Default/)               | `ServiceKeyMiddleware`, constant-time API key validation, `RequireServiceKey()` filter  |
| [`AuthPolicy.Default`](../../shared/Implementations/Middleware/AuthPolicy.Default/)               | `AuthPolicies` constants, `AddD2Policies()`, fluent `RequireAuth()` route extensions    |
| [`RequestEnrichment.Default`](../../shared/Implementations/Middleware/RequestEnrichment.Default/) | IP resolution, fingerprinting, WhoIs lookup, `IRequestContext` population               |
| [`RateLimit.Default`](../../shared/Implementations/Middleware/RateLimit.Default/)                 | Multi-dimensional sliding-window rate limiting                                          |
| [`Idempotency.Default`](../../shared/Implementations/Middleware/Idempotency.Default/)             | `Idempotency-Key` header middleware (SET NX + response caching)                         |
| [`Translation.Default`](../../shared/Implementations/Middleware/Translation.Default/)             | `D2-Locale` header parsing, BCP 47 locale resolution, `D2Result.messages` translation   |
| [`I18n`](../../shared/I18n/)                                                                      | `SupportedLocales` (IETF BCP 47 tags), `ITranslator`, translation key constants         |

## API Versioning

All endpoints use URL path versioning (`/api/v1/...`). This approach was chosen for:

- Explicit version visibility in URLs
- Easy testing via browser/curl
- Simple routing without header inspection

## Endpoints

### Health (`/api/health`)

| Method | Path          | Description                                                                |
| ------ | ------------- | -------------------------------------------------------------------------- |
| GET    | `/api/health` | Aggregated health check — fans out to Geo, Auth, Comms, and gateway cache. |

Returns `200` (all healthy) or `503` (degraded) with per-service status, latency, and component breakdown. All services (Geo, Comms, Auth) via gRPC `CheckHealth`, gateway cache via distributed cache ping handler.

### Geo (`/api/v1/geo`)

| Method | Path                     | Description                                                                      |
| ------ | ------------------------ | -------------------------------------------------------------------------------- |
| GET    | `/reference-data`        | Returns full geographic reference data.                                          |
| POST   | `/reference-data/update` | Requests that geographic reference data be updated. Returns the updated version. |

### Auth Jobs (`/api/v1/auth/jobs`)

All job endpoints require `RequireServiceKey()` — accessible only to Dkron or internal callers.

| Method | Path                          | Description                                        |
| ------ | ----------------------------- | -------------------------------------------------- |
| POST   | `/purge-sessions`             | Purges expired auth sessions.                      |
| POST   | `/purge-sign-in-events`       | Purges old sign-in events beyond retention period. |
| POST   | `/cleanup-invitations`        | Cleans up expired org invitations.                 |
| POST   | `/cleanup-emulation-consents` | Cleans up expired or revoked emulation consents.   |

### Geo Jobs (`/api/v1/geo/jobs`)

| Method | Path                          | Description                                              |
| ------ | ----------------------------- | -------------------------------------------------------- |
| POST   | `/purge-stale-whois`          | Purges WhoIs records older than the retention period.    |
| POST   | `/cleanup-orphaned-locations` | Cleans up locations with no contact or WhoIs references. |

### Comms Jobs (`/api/v1/comms/jobs`)

| Method | Path                      | Description                                           |
| ------ | ------------------------- | ----------------------------------------------------- |
| POST   | `/purge-deleted-messages` | Purges soft-deleted messages beyond retention period. |
| POST   | `/purge-delivery-history` | Purges old delivery request and attempt history.      |

## Endpoint Organization

Each backing service owns one endpoint file under [`Endpoints/`](Endpoints/). The file owns BOTH the `IServiceCollection.Add*GrpcClient()` extension AND the `IEndpointRouteBuilder.Map*EndpointsV1()` extension. `Program.cs` calls each one — no per-route logic lives in `Program.cs` itself.

| File                   | gRPC Client                     | REST Routes                                                     |
| ---------------------- | ------------------------------- | --------------------------------------------------------------- |
| `GeoEndpoints.cs`      | Geo data client                 | `/api/v1/geo/...` (reference data + update)                     |
| `AuthEndpoints.cs`     | Auth client                     | _(placeholder — no REST routes yet)_                            |
| `CommsEndpoints.cs`    | Comms client                    | `/api/v1/notification-preferences` (GET + PUT)                  |
| `FilesEndpoints.cs`    | Files client                    | _(placeholder — no REST routes yet)_                            |
| `SignalREndpoints.cs`  | SignalR push client             | _(placeholder — no REST routes yet)_                            |
| `AuthJobEndpoints.cs`  | Auth job client (with API key)  | `/api/v1/auth/jobs/...` (Dkron-only via `RequireServiceKey()`)  |
| `GeoJobEndpoints.cs`   | Geo job client (with API key)   | `/api/v1/geo/jobs/...` (Dkron-only via `RequireServiceKey()`)   |
| `CommsJobEndpoints.cs` | Comms job client (with API key) | `/api/v1/comms/jobs/...` (Dkron-only via `RequireServiceKey()`) |
| `HealthEndpoints.cs`   | Reuses Geo/Auth/Comms clients   | `/api/health`                                                   |

```csharp
// Program.cs — gRPC client registration
builder.Services.AddGeoGrpcClient();
builder.Services.AddAuthGrpcClient();
builder.Services.AddCommsGrpcClient();
builder.Services.AddFilesGrpcClient();
builder.Services.AddSignalRGrpcClient();
builder.Services.AddAuthJobsGrpcClient();
builder.Services.AddGeoJobsGrpcClient();
builder.Services.AddCommsJobsGrpcClient();
builder.Services.AddHealthEndpointDependencies();

// Program.cs — endpoint mapping
app.MapGeoEndpointsV1();
app.MapCommsEndpointsV1();
app.MapAuthJobEndpointsV1();
app.MapGeoJobEndpointsV1();
app.MapCommsJobEndpointsV1();
app.MapHealthEndpointsV1();
```

Authorization is applied via fluent route extensions at the group level:

- `.RequireAuth()` — from `D2.Shared.AuthPolicy.Default`, applied to user-facing protected route groups (JWT bearer required)
- `.RequireServiceKey()` — from `D2.Shared.ServiceKey.Default`, applied to S2S-only route groups (job endpoints, internal callers)

## gRPC Client Registration

Clients read service addresses from environment variables (bare `host:port` format, Gateway prepends `http://`). Job-service clients also attach `x-api-key` via `AddCallCredentials` so the target gRPC server can validate trusted callers.

```csharp
// Standard pattern — Geo data client (no API key needed)
var geoAddress = Environment.GetEnvironmentVariable("GEO_GRPC_ADDRESS");
services.AddGrpcClient<GeoService.GeoServiceClient>(o =>
{
    o.Address = new Uri($"http://{geoAddress}");
});

// Job client pattern — Auth job client (with API key call credentials)
var apiKey = Environment.GetEnvironmentVariable("GATEWAY_AUTH_GRPC_API_KEY");
services.AddGrpcClient<AuthJobService.AuthJobServiceClient>(o =>
{
    o.Address = new Uri($"http://{authGrpcAddress}");
})
.AddCallCredentials((_, metadata) =>
{
    metadata.Add("x-api-key", apiKey!);
    return Task.CompletedTask;
})
.ConfigureChannel(o => o.UnsafeUseInsecureChannelCallCredentials = true);
```

### Client Environment Variables

| Client                                  | Address Env Var      | API Key Env Var              |
| --------------------------------------- | -------------------- | ---------------------------- |
| `GeoService.GeoServiceClient`           | `GEO_GRPC_ADDRESS`   | ---                          |
| `GeoJobService.GeoJobServiceClient`     | `GEO_GRPC_ADDRESS`   | `GATEWAY_GEO_GRPC_API_KEY`   |
| `AuthJobService.AuthJobServiceClient`   | `AUTH_GRPC_ADDRESS`  | `GATEWAY_AUTH_GRPC_API_KEY`  |
| `CommsJobService.CommsJobServiceClient` | `COMMS_GRPC_ADDRESS` | `GATEWAY_COMMS_GRPC_API_KEY` |
| `AuthService.AuthServiceClient`         | `AUTH_GRPC_ADDRESS`  | ---                          |
| `CommsService.CommsServiceClient`       | `COMMS_GRPC_ADDRESS` | ---                          |

The last two are registered by `AddHealthEndpointDependencies()` for the aggregated health check fan-out (no API key — `CheckHealth` is exempt from API key auth).

## Middleware Pipeline

The gateway composes shared middleware packages in a specific order. Registration and activation happen separately.

**Service registration** (in `Program.cs` `builder.Services`):

```csharp
builder.Services.AddWhoIsCache(builder.Configuration);
builder.Services.AddRedisCaching(redisConnectionString);
builder.Services.AddRequestEnrichment(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddServiceKeyAuth(builder.Configuration);
builder.Services.AddIdempotency(builder.Configuration);
builder.Services.AddTranslation(builder.Configuration);
```

**Pipeline order** (in `Program.cs` `app.Use*`):

```
SecurityHeaders → ExceptionHandler → StructuredRequestLogging → CORS
    → RequestEnrichment → ServiceKeyDetection → RateLimiting
    → JwtAuth → RequestContextLogging → Idempotency → Translation
    → Endpoints
```

Trusted services (valid `X-Api-Key`) bypass rate limiting and fingerprint validation. The `D2-Locale` header is parsed by Translation middleware, which resolves BCP 47 locale tags (e.g., `en-US`, `fr-CA`) against the configured `SupportedLocales` and translates `D2Result.messages` before returning responses.

### CORS

CORS origins are configurable via the `CorsOrigin` setting (comma-separated). Allowed headers:

- `Content-Type`, `Authorization`, `Idempotency-Key`, `X-Client-Fingerprint`, `D2-Locale`
- `traceparent`, `tracestate` — W3C Trace Context — Faro/OTel browser SDK propagates trace context on every fetch.

### Translation & i18n

Translation files (`contracts/messages/*.json`) are copied to `AppContext.BaseDirectory/messages/` via MSBuild (`<Content>` item in `.csproj`). Locale files use IETF BCP 47 tags (e.g., `en-US.json`, `fr-CA.json`). The `SupportedLocales` class (from `D2.Shared.I18n`) provides the canonical list of supported locales.

## Error Handling

All responses return a `D2Result<T>` with the HTTP status code derived from `D2Result.StatusCode`:

```json
{
  "success": true,
  "statusCode": 200,
  "errorCode": null,
  "traceId": "abc123",
  "messages": [],
  "inputErrors": [],
  "data": {}
}
```

On failure:

```json
{
  "success": false,
  "statusCode": 404,
  "errorCode": "NOT_FOUND",
  "traceId": "abc123",
  "messages": ["Resource not found"],
  "inputErrors": [],
  "data": null
}
```

This uses the existing `ToD2Result()` extension from `Result.Extensions` to convert `D2ResultProto` to `D2Result<T>`, ensuring consistent response shapes across the entire system.

## Architecture

```
Client (HTTP)
    |
    └──► REST Gateway (/api/v1/geo/...)
            |
            └──► GeoService.GeoServiceClient (gRPC)
                    |
                    └──► Geo.API (d2-geo)
```

## OpenTelemetry

Trace propagation flows automatically through the gRPC client thanks to:

- `OpenTelemetry.Instrumentation.AspNetCore` (incoming HTTP)
- `OpenTelemetry.Instrumentation.GrpcNetClient` (outgoing gRPC)

Both are registered via `AddServiceDefaults()`.

## Service-to-Service Authentication

Trusted backend callers (e.g., SvelteKit server, Dkron) authenticate via `X-Api-Key` header. The service-key middleware lives in [`D2.Shared.ServiceKey.Default`](../../shared/Implementations/Middleware/ServiceKey.Default/); JWT bearer auth lives in [`D2.Shared.JwtAuth.Default`](../../shared/Implementations/Middleware/JwtAuth.Default/); shared authorization policies live in [`D2.Shared.AuthPolicy.Default`](../../shared/Implementations/Middleware/AuthPolicy.Default/).

1. `ServiceKeyMiddleware` validates the key early in the pipeline (after request enrichment, before rate limiting)
2. Valid key → sets `IRequestContext.IsTrustedService = true` (trusted services bypass rate limiting and fingerprint validation)
3. Invalid key → 401 immediately (fail fast)
4. No key → browser request, continues normally
5. `RequireServiceKey()` endpoint filter checks the trust flag on service-only endpoints

### Service Key Validation

The `ServiceKeyMiddleware` uses **constant-time comparison** to prevent timing side-channel attacks:

```csharp
// Pre-compute byte arrays at startup (constructor)
var apiKeyBytes = Encoding.UTF8.GetBytes(apiKey!);
var matched = false;
foreach (var validKeyBytes in r_validKeyBytes)
{
    if (CryptographicOperations.FixedTimeEquals(apiKeyBytes, validKeyBytes))
    {
        matched = true;
    }
    // Continue loop — always compare ALL keys to prevent timing leaks.
}
```

**Why this matters:**

- `===` in JS or `==` in C# exits early on first mismatch — an attacker can measure response time to brute-force keys character by character
- `CryptographicOperations.FixedTimeEquals` (C#) / `timingSafeEqual` (Node.js `node:crypto`) always takes the same time regardless of where the mismatch occurs
- The loop iterates ALL valid keys even after a match — prevents leaking which key index matched

**Fail-closed on missing config:** If no valid keys are configured, the middleware returns 401 immediately. Empty key lists never silently bypass authentication.

**Registration:** `AddServiceKeyAuth(configuration)` + `UseServiceKeyDetection()` in `Program.cs` (provided by `D2.Shared.ServiceKey.Default`).
