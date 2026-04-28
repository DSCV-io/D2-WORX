# @d2/jwt-auth -- JWT Auth Middleware

Hono middleware for RS256 JWT validation on public-facing Node.js services. Fetches keys from a remote JWKS endpoint, verifies token signature/claims, checks fingerprint binding, and populates `IRequestContext` from JWT claims.

## Architecture

```
Browser ──Bearer token──> Hono Service ──JWKS fetch──> Auth Service (/api/auth/jwks)
                               |
                          jwtAuth middleware:
                            1. Extract Bearer token
                            2. Verify signature + claims (RS256, issuer, audience)
                            3. Fingerprint check (SHA-256(UA|Accept) vs `fp` claim)
                            4. Populate IRequestContext from JWT claims
                               |
                          c.set("requestContext", ctx) -> downstream handlers
```

## Package Structure

```
src/
  index.ts                Barrel exports
  jwks-provider.ts        createJwksProvider (cached remote JWKS via jose)
  verify-token.ts         verifyToken (RS256 verification, returns D2Result)
  fingerprint-check.ts    checkFingerprint (timing-safe SHA-256 comparison)
  populate-context.ts     populateRequestContext (JWT claims -> IRequestContext)
  hono-middleware.ts       jwtAuth factory + JwtAuthOptions + unauthorizedResponse
```

## Public API

### `createJwksProvider(jwksUrl: string): JWTVerifyGetKey`

Creates a cached JWKS provider using `jose.createRemoteJWKSet`. Keys are fetched once and refreshed automatically on rotation. The provider is created once at middleware initialization and shared across all requests.

### `verifyToken(token, options): Promise<D2Result<VerifiedToken>>`

Verifies a JWT against the JWKS provider. Enforces RS256 algorithm, issuer, and audience. Returns `ok(VerifiedToken)` on success or `unauthorized()` with a specific message for each failure mode (expired, claim validation, signature, no matching key).

**`VerifyTokenOptions`**: `{ jwks, issuer, audience }`

**`VerifiedToken`**: `{ payload: JWTPayload, protectedHeader }`

### `checkFingerprint(payload, userAgent, accept): Promise<D2Result<void>>`

Validates the `fp` claim in a JWT against `SHA-256(userAgent + "|" + accept)` of the current request. Uses `timingSafeEqual` for constant-time comparison. If the `fp` claim is absent, the check passes (backward compatibility). Returns `unauthorized()` on mismatch.

### `populateRequestContext(payload: JWTPayload): IRequestContext`

Maps JWT claims to `IRequestContext` fields following the D2 JWT convention:

| JWT Claim          | IRequestContext Field                           |
| ------------------ | ----------------------------------------------- |
| `sub`              | `userId`                                        |
| `email`            | `email`                                         |
| `username`         | `username`                                      |
| `orgId`            | `agentOrgId` (+ `targetOrgId` if not emulating) |
| `orgName`          | `agentOrgName` (+ `targetOrgName`)              |
| `orgType`          | `agentOrgType` (+ `targetOrgType`)              |
| `role`             | `agentOrgRole` (+ `targetOrgRole`)              |
| `isEmulating`      | `isOrgEmulating`                                |
| `emulatedOrgId/..` | `targetOrgId/Name/Type` (when emulating)        |
| `isImpersonating`  | `isUserImpersonating`                           |
| `impersonatedBy`   | `impersonatedBy`                                |

Computed helpers: `isAgentStaff`, `isAgentAdmin`, `isTargetingStaff`, `isTargetingAdmin`.

When emulating, target org fields come from `emulatedOrg*` claims and `targetOrgRole` is forced to `"auditor"`.

### `jwtAuth(options: JwtAuthOptions): MiddlewareHandler`

Hono middleware factory. Creates the JWKS provider once, then on each request:

1. Extracts `Bearer` token from `Authorization` header
2. Calls `verifyToken` (RS256, issuer, audience)
3. Calls `checkFingerprint` (if enabled, default: true)
4. Calls `populateRequestContext` and sets `c.set("requestContext", ctx)`
5. Returns 401 JSON (D2Result error shape) on any failure

### `JwtAuthOptions`

| Field              | Type      | Required | Default | Description                                       |
| ------------------ | --------- | -------- | ------- | ------------------------------------------------- |
| `jwksUrl`          | `string`  | Yes      | --      | URL of the JWKS endpoint                          |
| `issuer`           | `string`  | Yes      | --      | Expected JWT issuer                               |
| `audience`         | `string`  | Yes      | --      | Expected JWT audience                             |
| `fingerprintCheck` | `boolean` | No       | `true`  | Enable fingerprint validation (SHA-256 UA+Accept) |

## Usage

```typescript
import { jwtAuth } from "@d2/jwt-auth";

const app = new Hono();

app.use(
  "/api/v1/*",
  jwtAuth({
    jwksUrl: "http://d2-auth:5100/api/auth/jwks",
    issuer: "d2-worx",
    audience: "d2-services",
  }),
);

// Downstream route — requestContext is populated
app.get("/api/v1/files", (c) => {
  const ctx = c.get("requestContext"); // IRequestContext
  // ctx.userId, ctx.agentOrgId, ctx.isAuthenticated, etc.
});
```

## Dependencies

| Package       | Type | Purpose                          |
| ------------- | ---- | -------------------------------- |
| `jose`        | dep  | JWKS fetching, JWT verification  |
| `hono`        | dep  | Middleware types                 |
| `@d2/handler` | dep  | IRequestContext, OrgType         |
| `@d2/result`  | dep  | D2Result for typed error returns |

## Consumers

- `@d2/files-api` -- composition root uses `jwtAuth()` on public REST routes
