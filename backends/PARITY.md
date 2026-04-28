# Cross-Platform Parity Checklist

Tracks which shared infrastructure packages exist on both .NET and Node.js platforms.

## Shared Infrastructure

| Concern                   | .NET Project                         | Node.js Package           | Status    | Why exclusive?                                                                              |
| ------------------------- | ------------------------------------ | ------------------------- | --------- | ------------------------------------------------------------------------------------------- |
| D2Result                  | `D2.Shared.Result`                   | `@d2/result`              | Parity    |                                                                                             |
| D2Result ↔ Proto          | `D2.Shared.Result.Extensions`        | `@d2/result-extensions`   | Parity    |                                                                                             |
| Handler pattern           | `D2.Shared.Handler`                  | `@d2/handler`             | Parity    |                                                                                             |
| Handler JWT extensions    | `D2.Shared.Handler.Extensions`       | —                         | .NET-only | Node.js uses BetterAuth directly — no JWT validation middleware needed                      |
| DI                        | `Microsoft.Extensions.DI` (built-in) | `@d2/di`                  | Parity    |                                                                                             |
| Logging                   | `ILogger<T>` (built-in)              | `@d2/logging`             | Parity    |                                                                                             |
| Interfaces / contracts    | `D2.Shared.Interfaces`               | `@d2/interfaces`          | Parity    |                                                                                             |
| Utilities                 | `D2.Shared.Utilities`                | `@d2/utilities`           | Parity    |                                                                                             |
| Service defaults (OTel)   | `D2.Shared.ServiceDefaults`          | `@d2/service-defaults`    | Parity    |                                                                                             |
| Proto types               | `D2.Shared.Protos`                   | `@d2/protos`              | Parity    |                                                                                             |
| In-memory cache           | `InMemoryCache.Default`              | `@d2/cache-memory`        | Parity    |                                                                                             |
| Distributed cache (Redis) | `DistributedCache.Redis`             | `@d2/cache-redis`         | Parity    |                                                                                             |
| Request enrichment        | `RequestEnrichment.Default`          | `@d2/request-enrichment`  | Parity    |                                                                                             |
| Rate limiting             | `RateLimit.Default`                  | `@d2/ratelimit`           | Parity    |                                                                                             |
| Idempotency               | `Idempotency.Default`                | `@d2/idempotency`         | Parity    |                                                                                             |
| PG batch queries          | `Batch.Pg`                           | `@d2/batch-pg`            | Parity    |                                                                                             |
| PG transactions           | `Transactions.Pg`                    | —                         | .NET-only | Drizzle lacks ambient/scoped transactions across handler calls                              |
| PG error helpers          | `Errors.Pg`                          | `@d2/errors-pg`           | Parity    |                                                                                             |
| Messaging                 | `Messaging.RabbitMQ`                 | `@d2/messaging`           | Parity    |                                                                                             |
| Geo client                | `Geo.Client`                         | `@d2/geo-client`          | Parity    |                                                                                             |
| Comms client              | —                                    | `@d2/comms-client`        | Node-only | .NET services don't publish to Comms exchange (Comms is Node.js-only)                       |
| Auth BFF client           | —                                    | `@d2/auth-bff-client`     | Node-only | .NET gateway validates JWTs directly via JWKS, no BFF proxy needed                          |
| JWT auth middleware       | `JwtAuth.Default`                    | `@d2/jwt-auth`            | Parity    | Both validate JWT Bearer + JWKS + fingerprint binding                                       |
| Service key middleware    | `ServiceKey.Default`                 | `@d2/service-key`         | Parity    | Both enforce S2S API key authentication                                                     |
| Auth policy middleware    | `AuthPolicy.Default`                 | `@d2/auth-policy`         | Parity    | Both expose `AuthPolicies` constants + route extensions (`.RequireAuth()`, `.RequireOrg()`) |
| Session fingerprint       | —                                    | `@d2/session-fingerprint` | Node-only | BetterAuth session-cookie binding — no .NET equivalent                                      |
| Translation middleware    | `Translation.Default`                | `@d2/translation`         | Parity    | Both translate D2Result messages/inputErrors with locale resolution                         |
| CSRF middleware           | —                                    | `@d2/csrf`                | Node-only | .NET gateway uses CORS alone for CSRF protection (no explicit CSRF middleware)              |
| i18n                      | `D2.Shared.I18n`                     | `@d2/i18n`                | Parity    | Both provide Translator + TK constants loading from contracts/messages/ (10 BCP 47 locales) |

## API-Level Differences

Where parity exists, the APIs are adapted to each platform's idioms:

| Concern           | .NET API                                                                                                                                   | Node.js API                                                                                                                                                                               |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Batch queries     | `BatchQuery<T,K>` fluent builder + LINQ                                                                                                    | `batchQuery(ids, queryFn, options)` utility fn                                                                                                                                            |
| Batch result      | `ToD2ResultAsync()` extension method                                                                                                       | `toBatchResult(results, count)` utility fn                                                                                                                                                |
| Transactions      | Handler-based `Begin`/`Commit`/`Rollback` — scoped `DbContext` enables ambient transactions across multiple handler calls within a request | Drizzle lacks ambient/scoped transactions. `db.transaction(cb)` requires all operations inside one callback — no way to open a transaction in one handler and commit/rollback in another. |
| PG error checking | `PgErrorCodes.IsUniqueViolation(ex)` (Npgsql)                                                                                              | `isPgUniqueViolation(err)` (node-postgres)                                                                                                                                                |
| Cache handler DI  | `IServiceCollection.Add*()` extension methods                                                                                              | Handler constructors + `addInstance()` in DI                                                                                                                                              |
| Extension methods | C# 14 `extension(T)` syntax                                                                                                                | Standalone utility functions                                                                                                                                                              |

## Platform-Exclusive Packages

### .NET Only

- **Handler.Extensions** — JWT claim extraction, `IsBearerOrSession` helpers. Not needed in Node.js because BetterAuth handles auth directly (no JWT validation middleware in Node services).
- **Transactions.Pg** — Handler-based Begin/Commit/Rollback for EF Core. Enables ambient transactions scoped to a request, spanning multiple handler calls. Drizzle lacks this capability — `db.transaction(cb)` forces all operations into a single callback with no cross-handler transaction support.

### Node.js Only

- **@d2/comms-client** — Thin RabbitMQ publisher for the Comms delivery engine. Not needed in .NET because .NET services don't publish to the Comms notification exchange (Comms is a Node.js-only service).
- **@d2/di** — Custom DI container mirroring `IServiceCollection`/`IServiceProvider`. .NET uses the built-in `Microsoft.Extensions.DependencyInjection`.
- **@d2/auth-bff-client** — BFF auth client for SvelteKit: session resolution, JWT management, auth proxy, route guards. HTTP-only (no BetterAuth dependency). No .NET equivalent — .NET gateway validates JWTs directly via JWKS.
- **@d2/csrf** — CSRF protection middleware (Content-Type + Origin validation). .NET gateway relies on CORS alone for CSRF protection — browser-enforced preflight prevents cross-origin form submissions.
