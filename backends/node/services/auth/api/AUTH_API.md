# @d2/auth-api

Hono HTTP server, gRPC job server, composition root, route definitions, and middleware for the Auth service. Wires together all DDD layers (`@d2/auth-domain`, `@d2/auth-app`, `@d2/auth-infra`) with shared infrastructure packages into a running service.

## Purpose

Serves as the entry point and composition root for the Auth service. Creates the Hono HTTP application with middleware pipeline, mounts BetterAuth at `/api/auth/*`, exposes custom authenticated routes for emulation, org contacts, and invitations, runs a gRPC server for scheduled job RPCs, and manages service lifecycle (startup, shutdown).

## Design Decisions

| Decision                              | Rationale                                                                          |
| ------------------------------------- | ---------------------------------------------------------------------------------- |
| Composition root in `createApp()`     | Mirrors .NET `Program.cs` — single function wires all dependencies                 |
| DI scope per protected request        | `createScopeMiddleware(provider)` builds `IRequestContext` + `IHandlerContext`     |
| Pre-auth handlers as singletons       | FindWhoIs, RateLimit, Throttle run before authentication — not in DI scope         |
| Ambient context via AsyncLocalStorage | All handlers (including pre-auth singletons) see per-request context automatically |
| API key required on all endpoints     | `require: true` on service-key middleware — no unauthenticated external access     |
| Thin route handlers                   | Routes extract input from request, resolve handler from DI scope, return result    |
| Auth middleware visible at route      | `requireOrg()`, `requireRole()`, `requireStaff()` declared inline for auditability |
| AppOverrides for testability          | Tests inject stub password functions to skip HIBP API calls                        |
| Separate Hono apps for route groups   | Auth routes, protected routes, and health mounted as sub-apps                      |
| gRPC server for jobs                  | Scheduled job RPCs on a separate port — keeps HTTP and gRPC concerns isolated      |
| Lock handlers as singletons           | AcquireLock/ReleaseLock share the Redis connection — registered once in DI         |
| withApiKeyAuth on gRPC                | Service key validation on gRPC RPCs mirrors HTTP service key middleware            |

## Package Structure

```
src/
  index.ts                  Barrel exports (createApp, middleware, routes)
  main.ts                   Production entry point (OTel, env vars, serve)
  composition-root.ts       createApp() — DI wiring, BetterAuth, Hono pipeline
  geo/                      Geo client configuration (context keys, caching)
  middleware/
    cors.ts                 CORS middleware factory
    csrf.ts                 CSRF protection (Origin header validation)
    distributed-rate-limit.ts  Rate limiting middleware (Redis sliding window)
    error-handler.ts        Global error handler (D2Result formatting)
    ambient-scope.ts        AsyncLocalStorage.run() wrapper for per-request ambient context
    request-enrichment.ts   IP resolution, fingerprinting, WhoIs lookup
    request-context-logging.ts  Per-request child logger with network/auth bindings
    scope.ts                Per-request DI scope (IRequestContext, IHandlerContext, enterWith upgrade)
    service-key.ts          X-Api-Key validation with optional require mode
    session.ts              BetterAuth session extraction (user + session on context)
    session-fingerprint.ts  Session-to-fingerprint binding (stolen token detection)
  routes/
    account-routes.ts       Per-user account ops (name/username/locale/timezone/avatar/sessions/sign-in events/email+phone OTP/delete)
    auth-routes.ts          BetterAuth catch-all + throttled sign-in endpoints
    check-email-routes.ts   Public pre-auth email availability check
    emulation-routes.ts     Emulation consent CRUD (POST, DELETE, GET)
    health.ts               Health check endpoint
    invitation-routes.ts    Org invitation with dual-path contact resolution
    org-contact-routes.ts   Org contact CRUD (POST, PATCH, DELETE, GET)
  services/
    auth-jobs-grpc-service.ts  gRPC AuthJobServiceServer (4 job RPCs)
```

## Composition Root

`createApp(config, publisher?, overrides?, messageBus?)` performs startup in order:

| Step | Action                                                                                                |
| ---- | ----------------------------------------------------------------------------------------------------- |
| 1    | Create singletons: `pg.Pool`, `ioredis`, Pino logger                                                  |
| 2    | Run Drizzle migrations, create Drizzle instance                                                       |
| 3    | Build `ServiceCollection` — register logger, handler context, cache, geo, infra, app                  |
| 3a   | Register `AcquireLock` / `ReleaseLock` singleton instances for job locking                            |
| 4    | Build `ServiceProvider`                                                                               |
| 5    | Create pre-auth singletons (FindWhoIs, RateLimitCheck, Throttle handlers) — see ambient context below |
| 6    | Create password functions (domain validation + HIBP k-anonymity cache)                                |
| 7    | Create BetterAuth instance with scoped callback hooks                                                 |
| 8    | Configure session fingerprint middleware (Redis-backed, 7-day TTL)                                    |
| 9    | Build Hono app with global + route-specific middleware                                                |
| 10   | Start gRPC server on `grpcPort` (if configured) with `withApiKeyAuth` wrapper                         |

Returns `{ app, auth, grpcServer, shutdown }`.

## Middleware Pipeline

### Global (all requests)

| Order | Middleware              | Purpose                                                                                                                           |
| ----- | ----------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| 1     | CORS                    | Allows configured SvelteKit origin                                                                                                |
| 2     | Body limit              | 256 KB max (auth payloads are small JSON)                                                                                         |
| 3     | Service key detection   | `X-Api-Key` → sets `IsTrustedService`. `require: true` → 401 if missing. Fail-closed at startup if `authApiKeys` mapping is empty |
| 4     | Request enrichment      | IP resolution, server fingerprint, WhoIs lookup                                                                                   |
| 5     | Request context logging | Per-request child logger with network/auth bindings                                                                               |
| 6     | Ambient scope           | `AsyncLocalStorage.run()` — seeds per-request context for all handlers                                                            |
| 7     | Distributed rate limit  | Multi-dimensional sliding window (Redis, skipped for trusted services)                                                            |
| 8     | Error handler           | Catches unhandled errors, returns D2Result                                                                                        |

### Auth routes (`/api/auth/*`)

| Order | Middleware             | Purpose                                                 |
| ----- | ---------------------- | ------------------------------------------------------- |
| 1     | Session fingerprint    | Binds/validates fingerprint to session token            |
| 2     | Fingerprint AsyncLocal | Stores fingerprint in AsyncLocalStorage for JWT payload |
| 3     | BetterAuth handler     | Delegates to BetterAuth (with throttle on sign-in)      |

### Protected routes (emulation, contacts, invitations)

| Order | Middleware          | Purpose                                                                                    |
| ----- | ------------------- | ------------------------------------------------------------------------------------------ |
| 1     | Session             | Extracts user + session from BetterAuth (401 if none)                                      |
| 2     | Session fingerprint | Validates fingerprint continuity                                                           |
| 3     | DI scope            | Creates per-request scope with IRequestContext, upgrades ambient context via `enterWith()` |
| 4     | CSRF                | Origin header validation                                                                   |

### Ambient Context Flow

Pre-auth singletons (FindWhoIs, RateLimit, Throttle) are constructed once with static service-level defaults. The `ambient-scope` middleware wraps the entire request pipeline in `AsyncLocalStorage.run()`, seeded with the enrichment-populated `IRequestContext`. `HandlerContext` checks this storage first — so pre-auth handlers automatically see per-request fields (IP, fingerprints, `isTrustedService`). After auth, the scope middleware upgrades the ambient context via `.enterWith()` to include identity/org fields. This mirrors .NET's DI scoping behavior.

## Routes

### Health

| Method | Path      | Auth | Description                     |
| ------ | --------- | ---- | ------------------------------- |
| GET    | `/health` | No   | Returns `{ status, timestamp }` |

### Auth (BetterAuth)

| Method | Path                         | Auth       | Description                               |
| ------ | ---------------------------- | ---------- | ----------------------------------------- |
| POST   | `/api/auth/sign-in/email`    | No         | Email sign-in with throttle guard         |
| POST   | `/api/auth/sign-in/username` | No         | Username sign-in with throttle guard      |
| ALL    | `/api/auth/*`                | BetterAuth | Catch-all for all other BetterAuth routes |

Sign-in throttle flow: extract identifier, check throttle (429 if blocked), forward to BetterAuth, record outcome (fire-and-forget). JWKS/discovery responses get `Cache-Control: public, max-age=3600`.

### Emulation Consent

| Method | Path                         | Auth                         | Description                      |
| ------ | ---------------------------- | ---------------------------- | -------------------------------- |
| POST   | `/api/emulation/consent`     | requireOrg + staff + officer | Create consent for target org    |
| DELETE | `/api/emulation/consent/:id` | requireOrg + staff + officer | Revoke consent by ID             |
| GET    | `/api/emulation/consent`     | requireOrg + staff           | List active consents (paginated) |

### Org Contacts

| Method | Path                    | Auth                 | Description                             |
| ------ | ----------------------- | -------------------- | --------------------------------------- |
| POST   | `/api/org-contacts`     | requireOrg + officer | Create contact (junction + Geo contact) |
| PATCH  | `/api/org-contacts/:id` | requireOrg + officer | Update metadata and/or contact data     |
| DELETE | `/api/org-contacts/:id` | requireOrg + officer | Delete junction + Geo contact           |
| GET    | `/api/org-contacts`     | requireOrg           | List contacts hydrated with Geo data    |

### Invitations

| Method | Path               | Auth                 | Description                                         |
| ------ | ------------------ | -------------------- | --------------------------------------------------- |
| POST   | `/api/invitations` | requireOrg + officer | Create invitation with dual-path contact resolution |

### Account (per-user, org-agnostic)

All routes require an authenticated session (`session` middleware). `userId` is derived from the request context — never accepted from the request body (IDOR safe). Routes that mutate sensitive fields are atomically password-gated (`currentPassword` in the SAME request body as the new value).

| Method | Path                                  | Auth    | Description                                                                                                              |
| ------ | ------------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------ |
| PATCH  | `/api/account/name`                   | session | Update real name (firstName + lastName) — SAGA via `UpdateUserRealName`                                                  |
| PATCH  | `/api/account/username`               | session | Update username (+ displayUsername) — uniqueness 409                                                                     |
| PATCH  | `/api/account/locale`                 | session | Update locale preference — SAGA via `UpdateUserLocale`                                                                   |
| PATCH  | `/api/account/timezone`               | session | Update timezone preference — SAGA via `UpdateUserTimezone`                                                               |
| DELETE | `/api/account/avatar`                 | session | Clear user image (`UpdateUserImage` with `clear: true`) + fire-and-forget invalidate-cache → push SignalR `user:updated` |
| GET    | `/api/account/sessions`               | session | List active sessions enriched with Geo WhoIs + `isCurrent` flag                                                          |
| POST   | `/api/account/sessions/revoke`        | session | Revoke one session by token — password-gated                                                                             |
| POST   | `/api/account/sessions/revoke-others` | session | Revoke every other session — password-gated                                                                              |
| POST   | `/api/account/change-password`        | session | BetterAuth-native change-password, atomic (current + new). Defaults to revoking other sessions; security email via hook  |
| GET    | `/api/account/sign-in-events`         | session | Paginated sign-in event history (default 50, max 100)                                                                    |
| POST   | `/api/account/email/request-change`   | session | Initiate email change — password-gated. Sends 15-min OTP to PENDING new email                                            |
| POST   | `/api/account/email/verify-change`    | session | Verify OTP and apply email change (SAGA). Sends "your email was changed" notification to OLD email                       |
| POST   | `/api/account/phone/request-change`   | session | Initiate phone change/add — password-gated. Sends 5-min SMS OTP                                                          |
| POST   | `/api/account/phone/verify-change`    | session | Verify OTP and apply phone change (SAGA) — sets `phoneVerified=true`                                                     |
| DELETE | `/api/account/phone`                  | session | Remove phone — password-gated, no OTP                                                                                    |
| POST   | `/api/account/delete`                 | session | Self-service deletion. Returns `{ scheduledFor: ISO date }`. 401 wrong password, 409 sole owner of one or more orgs      |

Invitation flow: validate input, look up user by email, create BetterAuth invitation, create Geo contact for non-existing invitees (contextKey=auth_org_invitation), resolve recipient contactId (via ext-keys for existing users), publish notification via comms-client.

## gRPC Server (Scheduled Jobs)

A secondary `@grpc/grpc-js` server runs on `AUTH_GRPC_PORT` (default: 5101) alongside the Hono HTTP server on port 5100. This server exposes `AuthJobService` RPCs for the .NET REST gateway to invoke on behalf of Dkron.

### Service: `auth-jobs-grpc-service.ts`

`createAuthJobsGrpcService(provider)` implements `AuthJobServiceServer` with 4 RPCs. Each RPC creates a DI scope via `createRpcScope`, resolves its handler, and disposes when done. Trace context is propagated via `withTraceContext`.

| RPC                               | Handler Key                      | Job Name                             |
| --------------------------------- | -------------------------------- | ------------------------------------ |
| `PurgeExpiredSessions`            | `IRunSessionPurgeKey`            | `purge-expired-sessions`             |
| `PurgeSignInEvents`               | `IRunSignInEventPurgeKey`        | `purge-sign-in-events`               |
| `CleanupExpiredInvitations`       | `IRunInvitationCleanupKey`       | `cleanup-expired-invitations`        |
| `CleanupExpiredEmulationConsents` | `IRunEmulationConsentCleanupKey` | `cleanup-expired-emulation-consents` |

All RPCs return `{ result: D2ResultProto, data: { jobName, rowsAffected, durationMs, lockAcquired, executedAt } }`.

### Authentication

When `authApiKeys` is configured, the gRPC server wraps all handlers with `withApiKeyAuth` from `@d2/service-defaults/grpc`, validating the `x-api-key` metadata header against the same key set used by the HTTP service key middleware.

## Authorization Middleware

Policy middleware lives in `@d2/auth-policy` (`backends/node/shared/implementations/middleware/auth-policy/default/`). `auth-api`'s `index.ts` re-exports them for backward compatibility.

| Middleware                | Purpose                                                     |
| ------------------------- | ----------------------------------------------------------- |
| `requireAuth()`           | Authenticated request required (session or trusted service) |
| `requireTrustedService()` | Trusted service (`X-Api-Key`) required                      |
| `requireOrg()`            | Active org required (orgId + valid orgType + valid role)    |
| `requireOrgType()`        | Session orgType must be in allowed set                      |
| `requireRole()`           | Session role must meet minimum hierarchy level              |
| `requireStaff()`          | Shorthand for `requireOrgType("admin", "support")`          |
| `requireAdmin()`          | Shorthand for `requireOrgType("admin")`                     |

## Configuration

`AuthServiceConfig` fields (from `@d2/auth-infra`):

| Field                  | Required | Default                       |
| ---------------------- | -------- | ----------------------------- |
| `databaseUrl`          | Yes      | —                             |
| `redisUrl`             | Yes      | —                             |
| `rabbitMqUrl`          | No       | — (events logged, not sent)   |
| `baseUrl`              | Yes      | —                             |
| `corsOrigin`           | Yes      | —                             |
| `jwtIssuer`            | Yes      | —                             |
| `jwtAudience`          | Yes      | —                             |
| `jwtExpirationSeconds` | No       | 900 (15 min)                  |
| `jwksRotationDays`     | No       | 30                            |
| `geoAddress`           | No       | — (contact ops fail without)  |
| `geoApiKey`            | No       | — (contact ops fail without)  |
| `authApiKeys`          | No       | — (service key auth disabled) |
| `grpcPort`             | No       | — (gRPC server not started)   |

### Job Options (env vars)

| Env Var                              | Field                      | Default |
| ------------------------------------ | -------------------------- | ------- |
| `AUTH_GRPC_PORT`                     | `grpcPort`                 | — (off) |
| `AUTH_APP__SIGNINEVENTRETENTIONDAYS` | `signInEventRetentionDays` | —       |
| `AUTH_APP__INVITATIONRETENTIONDAYS`  | `invitationRetentionDays`  | 7       |
| `AUTH_APP__JOBLOCKTTLMS`             | `jobLockTtlMs`             | 300000  |

Job options are only parsed when `AUTH_APP__SIGNINEVENTRETENTIONDAYS` is set. When absent, `DEFAULT_AUTH_JOB_OPTIONS` from `@d2/auth-app` is used.

## Dependencies

| Package                  | Purpose                                                               |
| ------------------------ | --------------------------------------------------------------------- |
| `@d2/auth-app`           | CQRS handlers, service keys                                           |
| `@d2/auth-domain`        | Constants, enums, session fields                                      |
| `@d2/auth-infra`         | BetterAuth factory, config, migrations, throttle                      |
| `@d2/auth-policy`        | Authorization middleware (requireAuth, requireOrg, requireRole, etc.) |
| `@d2/cache-memory`       | Local caches (WhoIs, throttle, contacts, HIBP)                        |
| `@d2/cache-redis`        | Redis handlers (session storage, throttle, rate limit)                |
| `@d2/comms-client`       | `INotifyKey` for sending notifications via comms                      |
| `@d2/di`                 | `ServiceCollection`, `ServiceProvider`                                |
| `@d2/geo-client`         | Geo contact CRUD handlers + FindWhoIs                                 |
| `@d2/handler`            | `HandlerContext`, `IHandlerContextKey`                                |
| `@d2/logging`            | Pino logger creation                                                  |
| `@d2/messaging`          | RabbitMQ `MessageBus` + `IMessagePublisher`                           |
| `@d2/ratelimit`          | Distributed rate limit check                                          |
| `@d2/request-enrichment` | IP/fingerprint/WhoIs middleware (imported indirectly)                 |
| `@d2/result`             | `D2Result`, `HttpStatusCode`                                          |
| `@d2/protos`             | `AuthJobServiceService` definition for gRPC server                    |
| `@d2/result-extensions`  | `d2ResultToProto()` for gRPC response conversion                      |
| `@d2/service-defaults`   | `setupTelemetry()`, `withApiKeyAuth`, `createRpcScope`                |
| `@d2/utilities`          | General utilities                                                     |
| `hono`                   | HTTP framework                                                        |
| `@grpc/grpc-js`          | gRPC server for scheduled job RPCs                                    |
| `@hono/node-server`      | Node.js adapter for Hono                                              |
| `ioredis`                | Redis client (direct for session fingerprint binding)                 |
| `drizzle-orm`            | Database queries in invitation routes                                 |
| `pg`                     | PostgreSQL connection pool                                            |

## Tests

All tests are in `@d2/auth-tests` (`backends/node/services/auth/tests/`):

```
src/unit/api/
  middleware/
    authorization.test.ts, csrf.test.ts, error-handler.test.ts,
    jwt-fingerprint.test.ts, scope.test.ts, service-key.test.ts,
    session.test.ts, session-fingerprint.test.ts
  routes/
    auth-routes.test.ts, emulation-routes.test.ts,
    invitation-routes.test.ts, org-contact-routes.test.ts
```

Run: `pnpm vitest run --project auth-tests`
