# D2.Shared.Implementations.Middleware.JwtAuth.Default

JWT Bearer authentication middleware for ASP.NET Core gateways. Validates
RS256-signed tokens against a remote JWKS endpoint, enforces a fingerprint
binding (`fp` claim hashed against `User-Agent` + `Accept` headers), and
populates `IRequestContext` from the token's claims.

Mirrors `@d2/jwt-auth` on Node — same RS256 algorithm, same JWKS fetch
pattern, same fingerprint formula.

## Components

| File                            | Role                                                                                                                                                                                    |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `JwtAuthExtensions.cs`          | `services.AddJwtAuth(configuration)` + `app.UseJwtAuth()` extensions. Wires JWT Bearer + the fingerprint middleware + `AddD2Policies` (from `AuthPolicy.Default`)                       |
| `JwksConfigurationRetriever.cs` | Fetches the raw JWKS document from the auth service (BetterAuth doesn't serve OIDC discovery, so we wrap the bare JWKS in an `OpenIdConnectConfiguration`)                              |
| `JwtFingerprintMiddleware.cs`   | Reads the `fp` claim, computes the expected fingerprint from request headers, 401s on mismatch. Populates the `MutableRequestContext` identity fields after a successful match          |
| `JwtFingerprintValidator.cs`    | Pure helper: `SHA-256(UserAgent + "                                                                                                                                                     | " + Accept)`. Same formula on Node (`@d2/jwt-auth/fingerprint-check`) |
| `JwtAuthOptions.cs`             | Bound from configuration section `GATEWAY_AUTH` (or whichever `sectionName` is passed to `AddJwtAuth`) — `AuthServiceBaseUrl`, `Issuer`, `Audience`, JWKS refresh intervals, clock skew |

## Pipeline order

`UseJwtAuth()` adds three middleware in sequence:

```csharp
app.UseAuthentication();                         // validate JWT, populate HttpContext.User
app.UseMiddleware<JwtFingerprintMiddleware>();   // check fp claim + populate IRequestContext
app.UseAuthorization();                          // apply route policies
```

Place after `UseRequestEnrichment()` (which sets up `MutableRequestContext`)
and before `UseIdempotency()` / route mapping.

## Fingerprint binding

Every browser-issued JWT carries an `fp` claim — `SHA-256(UA + "|" + Accept)`
computed at issue time. On every request the middleware recomputes the same
hash from the inbound headers and 401s on mismatch. This stops a stolen JWT
from being replayed by a different client.

Trusted-service requests (validated by `ServiceKeyMiddleware` from the
`ServiceKey.Default` package) skip the fingerprint check entirely — internal
service-to-service calls don't carry browser headers.

## Node parity

| `@d2/jwt-auth`                                              | `D2.Shared.JwtAuth.Default`                                             |
| ----------------------------------------------------------- | ----------------------------------------------------------------------- |
| `jwtAuth({ jwksUrl, issuer, audience })` Hono middleware    | `services.AddJwtAuth(configuration)`                                    |
| `verifyToken(token, opts)`                                  | `Microsoft.AspNetCore.Authentication.JwtBearer` (framework)             |
| `checkFingerprint(payload, ua, accept)`                     | `JwtFingerprintValidator.ComputeFingerprint(httpContext)`               |
| `populateRequestContext(payload)` → returns IRequestContext | `JwtFingerprintMiddleware.SetAuthState` mutates `MutableRequestContext` |
