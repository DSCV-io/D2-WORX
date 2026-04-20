# RequestEnrichment.Default

HTTP middleware that enriches requests with client information including IP resolution, fingerprinting, and WhoIs geolocation data.

## Files

| File Name                                                                | Description                                                                                 |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------- |
| [MutableRequestContext.cs](MutableRequestContext.cs)                     | Mutable implementation of `IRequestContext` progressively populated by gateway middleware.  |
| [IpResolver.cs](IpResolver.cs)                                           | Static helper for resolving client IP from various headers.                                 |
| [FingerprintBuilder.cs](FingerprintBuilder.cs)                           | Static helpers for server + device fingerprint computation.                                 |
| [InfrastructurePaths.cs](InfrastructurePaths.cs)                         | Helper for detecting infrastructure endpoints (health, metrics) to skip enrichment/logging. |
| [RequestEnrichmentOptions.cs](RequestEnrichmentOptions.cs)               | Configuration options for the middleware.                                                   |
| [RequestEnrichmentMiddleware.cs](RequestEnrichmentMiddleware.cs)         | The middleware implementation that performs enrichment.                                     |
| [RequestContextLoggingMiddleware.cs](RequestContextLoggingMiddleware.cs) | Middleware pushing non-PII context fields into Serilog LogContext + OTel trace span tags.   |
| [Extensions.cs](Extensions.cs)                                           | DI registration and app builder extension methods.                                          |

## Overview

The middleware performs the following enrichment steps:

1. **IP Resolution** — Resolves client IP from configured `TrustedProxyHeaders` (default: Cloudflare only) in priority order:
   - `CF-Connecting-IP` (Cloudflare) — if `CfConnectingIp` is trusted
   - `X-Real-IP` (Nginx/reverse proxy) — if `XRealIp` is trusted
   - `X-Forwarded-For` (first entry) — if `XForwardedFor` is trusted
   - `HttpContext.Connection.RemoteIpAddress` (fallback)

2. **Server Fingerprint** — Computes SHA-256 hash of request headers for logging/analytics:
   - `User-Agent + "|" + Accept-Language + "|" + Accept-Encoding + "|" + Accept`

3. **Client Fingerprint** — Reads from `d2-cfp` cookie (primary) or `X-Client-Fingerprint` header (fallback). May be absent.

4. **Device Fingerprint** — Computes `SHA-256(clientFingerprint + serverFingerprint + clientIp)`. Always present — if client fingerprint is missing, degrades to `SHA-256("" + serverFP + clientIp)`.

5. **WhoIs Lookup** — Calls Geo.Client's WhoIs cache handler with the resolved IP address to enrich with geolocation data:
   - City, country code, subdivision code
   - VPN/proxy/Tor/hosting flags
   - Content-addressable hash ID for downstream lookups
   - Note: User-Agent is used for server fingerprint computation (step 2) but is NOT passed to FindWhoIs — WhoIs hashing is IP-only (`SHA256(ip|year|month)`)

## MutableRequestContext

`MutableRequestContext` implements `IRequestContext` (from `D2.Shared.Handler`) and is progressively populated by gateway middleware. Downstream consumers see the read-only `IRequestContext` interface.

The enrichment middleware creates the instance with network fields, then subsequent middleware mutates it:

- **RequestEnrichmentMiddleware** — creates with network/geo fields (clientIp, fingerprints, WhoIs data)
- **ServiceKeyMiddleware** (`ServiceKey.Default`) — sets `IsTrustedService` flag
- **JwtAuth + JwtFingerprintMiddleware** (`JwtAuth.Default`) — sets identity/org fields (userId, agentOrg, targetOrg, etc.)
- **TranslationMiddleware** (`Translation.Default`) — translates D2Result message/inputError keys using locale resolved from request headers

### Network / Enrichment Fields (set by RequestEnrichmentMiddleware)

| Property            | Type      | Description                                                |
| ------------------- | --------- | ---------------------------------------------------------- |
| `ClientIp`          | `string?` | Resolved client IP address.                                |
| `ServerFingerprint` | `string?` | Server-computed fingerprint for logging.                   |
| `ClientFingerprint` | `string?` | Client-provided fingerprint from cookie/header (nullable). |
| `DeviceFingerprint` | `string?` | Combined fingerprint: SHA-256(clientFP + serverFP + IP).   |
| `WhoIsHashId`       | `string?` | Content-addressable hash for WhoIs lookups.                |
| `City`              | `string?` | City from WhoIs data.                                      |
| `CountryCode`       | `string?` | ISO 3166-1 alpha-2 country code.                           |
| `SubdivisionCode`   | `string?` | ISO 3166-2 subdivision code.                               |
| `IsVpn`             | `bool?`   | Whether IP is from a VPN.                                  |
| `IsProxy`           | `bool?`   | Whether IP is from a proxy.                                |
| `IsTor`             | `bool?`   | Whether IP is from a Tor exit node.                        |
| `IsHosting`         | `bool?`   | Whether IP is from a hosting provider.                     |

See `HANDLER.md` for the full `IRequestContext` interface (including identity, org, trust, and helper fields).

## RequestContextLoggingMiddleware

Runs after authentication middleware. Pushes non-PII `IRequestContext` fields into:

- **Serilog LogContext** — every log entry within the request scope includes these fields automatically
- **OTel span tags** — for Tempo trace queries (userId, agentOrgId, agentOrgType, targetOrgId)

Skips infrastructure endpoints (health, metrics) to reduce noise. If no `IRequestContext` is found on `HttpContext.Features`, the middleware is a no-op.

## Configuration

```csharp
public class RequestEnrichmentOptions
{
    // Enable/disable WhoIs lookup (default: true).
    public bool EnableWhoIsLookup { get; set; } = true;

    // Header name for client fingerprint fallback (default: "X-Client-Fingerprint").
    public string ClientFingerprintHeader { get; set; } = "X-Client-Fingerprint";

    // Cookie name for client fingerprint (default: "d2-cfp").
    public string ClientFingerprintCookie { get; set; } = "d2-cfp";

    // Proxy headers trusted for IP resolution (default: CfConnectingIp only).
    public HashSet<TrustedProxyHeader> TrustedProxyHeaders { get; set; } = [TrustedProxyHeader.CfConnectingIp];

    // Maximum length for client fingerprint header values (default: 256). Values exceeding this are truncated.
    public int MaxFingerprintLength { get; set; } = 256;

    // Maximum length for forwarded-for / IP proxy headers (default: 2048). Values exceeding this are truncated.
    public int MaxForwardedForLength { get; set; } = 2048;
}
```

## Usage

```csharp
// In Program.cs or Startup.cs

// Register services.
builder.Services.AddRequestEnrichment(builder.Configuration);

// Add middleware (after exception handler, before rate limiting).
app.UseRequestEnrichment();

// Add request context logging (after authentication middleware).
app.UseRequestContextLogging();
```

## Fail-Open Behavior

- **WhoIs lookup fails**: Log warning, continue with IP/fingerprint only.
- **Localhost detected**: Skip WhoIs lookup entirely.

## Infrastructure Path Bypass

`InfrastructurePaths.IsInfrastructure()` detects infrastructure endpoints that should skip business middleware:

```csharp
public static bool IsInfrastructure(HttpContext context)
{
    return context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
           context.Request.Path.StartsWithSegments("/alive", StringComparison.OrdinalIgnoreCase) ||
           context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase) ||
           context.Request.Path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);
}
```

**Critical rule:** ALL business middleware must check `InfrastructurePaths.IsInfrastructure()` and skip processing for infrastructure paths. This includes:

- Request enrichment (WhoIs lookups)
- Rate limiting
- Request context logging
- Idempotency

When adding a new infrastructure path, add it to `InfrastructurePaths` — never add path bypass logic to individual middleware. When adding new business middleware, check `IsInfrastructure()` at the top and call `next(context)` immediately if true.

## Dependencies

- `D2.Geo.Client` — For WhoIs cache handler (`IComplex.IFindWhoIsHandler`). Only `IpAddress` is passed to FindWhoIs (no User-Agent).
- `Microsoft.AspNetCore.Http` — For HTTP context and features.
