# @d2/files-api

API layer for the D2-WORX Files service. Composition root (Hono REST + gRPC + RabbitMQ consumers) that wires `@d2/files-app` and `@d2/files-infra` into a runnable service.

## Purpose

Provides the external surface area for the Files service:

- **REST API** (Hono) -- purpose-specific upload endpoints, file download proxy, paginated listing, health checks
- **gRPC server** -- `FilesService` (health) + `FilesJobService` (cleanup job, invoked by Dkron)
- **RabbitMQ consumers** -- intake (MinIO upload notifications) + processing (scan/transform pipeline)
- **Composition root** -- creates singletons, runs migrations, registers DI, starts all transports

## REST API Endpoints

All protected routes are under `/api/v1` and require a valid JWT (`Authorization: Bearer <token>`).

### Protected (`/api/v1/*`)

| Method | Path                                     | Auth | Description                                           |
| ------ | ---------------------------------------- | ---- | ----------------------------------------------------- |
| POST   | `/api/v1/avatar`                         | JWT  | Upload user avatar (`user_avatar` context key)        |
| POST   | `/api/v1/org/logo`                       | JWT  | Upload org logo (`org_logo`, target org from JWT)     |
| POST   | `/api/v1/org/documents`                  | JWT  | Upload org document (`org_document`, target from JWT) |
| POST   | `/api/v1/threads/:threadId/attachments`  | JWT  | Upload thread attachment (`thread_attachment`)        |
| GET    | `/api/v1/files/:fileId/:variantName`     | JWT  | Download file variant (streams via app handler)       |
| GET    | `/api/v1/files/:fileId/:variantName/url` | JWT  | Returns a presigned GET URL for the variant           |
| GET    | `/api/v1/files`                          | JWT  | List files by contextKey + relatedEntityId            |

**Upload endpoints** accept JSON body `{ contentType, displayName, sizeBytes }`. The `contextKey` is hardcoded per route -- users never provide it directly. `relatedEntityId` is derived from JWT claims: `userId` for avatar, `targetOrgId` for org_logo / org_document, route param for thread attachments. Org routes return 401 if no active org is in the session (IDOR-safe — never accept org IDs from the URL).

**Download endpoint** delegates to the app-layer `DownloadFileVariant` query. The handler performs file lookup, access resolution, variant validation, and storage retrieval; the route serializes the buffer with `Content-Type`, `Cache-Control: public, max-age=31536000, immutable`, and `Content-Disposition: attachment` (the latter prevents XSS via uploaded SVG/HTML).

**Variant URL endpoint** delegates to `GetFileVariantUrl`. Returns a time-limited presigned GET URL (default 1 hour, see `FilesStorageOptions.presignGetExpirySeconds`) that browsers can drop straight into `<img src>`. Useful for hot-path image rendering — avoids streaming through the API.

**List endpoint** requires `contextKey` and `relatedEntityId` query params. Pagination: `limit` (default 50, max 100), `offset` (default 0).

### Public (no auth)

| Method | Path      | Description                               |
| ------ | --------- | ----------------------------------------- |
| GET    | `/health` | Full health check with component statuses |
| GET    | `/ready`  | Readiness probe (same as `/health`)       |

## Hono Context Typing

Routes declare `new Hono<{ Variables: FilesVariables }>()` so `c.var.scope`, `c.var.requestContext`, and `c.var.requestLogger` type-check without `any` casts. The shape is centralized in [`context-keys.ts`](src/context-keys.ts) (mirrors auth-api's `SessionVariables` pattern).

## Middleware Pipeline

Order matters -- middleware executes top-to-bottom on request, bottom-to-top on response:

```
Request
  1. CORS (origin allowlist, expose Content-Disposition)
  2. Security headers (X-Content-Type-Options: nosniff, X-Frame-Options: DENY, CSP, Referrer-Policy)
  3. Body limit (1 MB -- REST payloads are metadata only, files upload direct to MinIO)
  4. Request enrichment (IP resolution, fingerprinting, WhoIs -- requires Geo client)
  5. Request context logging (child logger with traceId, userId, etc.)
  6. Ambient scope (AsyncLocalStorage for per-request context on singletons)
  7. Rate limiting (multi-dimensional sliding window -- requires Geo client)
  8. Error handler (catches unhandled errors, returns D2Result shape)
  ├── /health, /ready  → public (no further middleware)
  └── /api/v1/*        → protected:
        9. JWT auth (@d2/jwt-auth -- JWKS, issuer, audience, fingerprint check)
       10. DI scope (createServiceScope, merge enriched + JWT IRequestContext)
       11. Route handler
Response
```

## gRPC Services

Started only when `FILES_GRPC_PORT` is configured. API key authentication via `withApiKeyAuth` when `FILES_API_KEYS` is set.

### FilesService

| RPC         | Auth   | Description                                   |
| ----------- | ------ | --------------------------------------------- |
| CheckHealth | Exempt | Health probe (same handler as REST `/health`) |

### FilesJobService

| RPC        | Auth    | Description                                                                                    |
| ---------- | ------- | ---------------------------------------------------------------------------------------------- |
| RunCleanup | API key | Stale file cleanup (pending/processing/rejected). Returns rowsAffected = sum of cleaned counts |

`RunCleanup` maps the handler's `{ pendingCleaned, processingCleaned, rejectedCleaned }` output to the standard `JobRpcOutput` shape (`rowsAffected` = total cleaned).

## RabbitMQ Consumers

Two-stage pipeline via `files.events` direct exchange. Both consumers always ACK -- failed files stay in their current status and are caught by the cleanup job.

| Consumer                       | Queue              | Routing Key     | Resolves Handler          |
| ------------------------------ | ------------------ | --------------- | ------------------------- |
| `createFileUploadedConsumer`   | `files.intake`     | `file-uploaded` | `IIntakeFileUploadedKey`  |
| `createFileProcessingConsumer` | `files.processing` | `file-process`  | `IProcessUploadedFileKey` |

```
MinIO bucket notification (s3:ObjectCreated:*)
  -> files.events exchange (routing key: file-uploaded)
  -> files.intake queue
  -> FileUploadedConsumer -> IntakeFileUploaded handler
      -> validates pending record, transitions to processing
      -> PublishFileForProcessing (routing key: file-process)
          -> files.events exchange
          -> files.processing queue
          -> FileProcessingConsumer -> ProcessUploadedFile handler
              -> scan + transform + notify pipeline
```

## Composition Root

`createFilesApp(config, publisher, messageBus)` wires the entire service. Initialization order:

1. **Create singletons** -- `pg.Pool`, `S3Client` (internal + optional public), `Redis`, logger, `HandlerContext`
2. **Ensure database + run migrations** -- `ensureDatabase()` (creates DB if missing) + Drizzle migrations
3. **Parse context key configs** -- `parseContextKeyConfigs(process.env)` reads `CK_*` env vars
4. **Build ServiceCollection** -- register logger, handler context, infra layer (`addFilesInfra`), app layer (`addFilesApp`), distributed locks, health check handlers
5. **Build ServiceProvider** -- `services.build()`
6. **Build Hono app** -- REST endpoints with middleware pipeline
7. **Build gRPC server** -- `FilesService` + `FilesJobService` (if `grpcPort` configured)
8. **Start RabbitMQ consumers** -- intake + processing (if `messageBus` provided)

Shutdown is graceful: gRPC server (5s timeout then force), S3 clients destroyed, provider disposed, Redis disconnected, PG pool ended.

## Configuration Reference

### Required

| Env Var               | Description                                           |
| --------------------- | ----------------------------------------------------- |
| `FILES_DATABASE_URL`  | PostgreSQL connection string (ADO.NET or URI format)  |
| `REDIS_URL`           | Redis connection string (StackExchange or URI format) |
| `RABBITMQ_URL`        | RabbitMQ AMQP connection string                       |
| `FILES_S3_ENDPOINT`   | MinIO/S3 endpoint URL                                 |
| `FILES_S3_ACCESS_KEY` | S3 access key                                         |
| `FILES_S3_SECRET_KEY` | S3 secret key                                         |
| `FILES_JWKS_URL`      | JWKS endpoint for JWT verification                    |

### Optional (with defaults)

| Env Var                    | Default          | Description                                          |
| -------------------------- | ---------------- | ---------------------------------------------------- |
| `FILES_HTTP_PORT` / `PORT` | `5300`           | Hono HTTP server port                                |
| `FILES_GRPC_PORT`          | _(disabled)_     | gRPC server port (gRPC disabled if not set)          |
| `FILES_S3_BUCKET`          | `d2-files`       | S3 bucket name                                       |
| `FILES_S3_REGION`          | `us-east-1`      | S3 region                                            |
| `FILES_S3_PUBLIC_ENDPOINT` | _(none)_         | Public S3 endpoint for presigned URLs (e.g., tunnel) |
| `FILES_JWT_ISSUER`         | `d2-worx`        | Expected JWT issuer claim                            |
| `FILES_JWT_AUDIENCE`       | `d2-services`    | Expected JWT audience claim                          |
| `CLAMAV_HOST`              | `localhost`      | ClamAV daemon hostname                               |
| `CLAMAV_PORT`              | `3310`           | ClamAV daemon port                                   |
| `SIGNALR_GRPC_ADDRESS`     | `localhost:5200` | SignalR Gateway gRPC address for realtime push       |

### Arrays (indexed env var convention)

| Env Var Pattern         | Example                                       | Description                |
| ----------------------- | --------------------------------------------- | -------------------------- |
| `FILES_API_KEYS__N`     | `FILES_API_KEYS__0=secret123`                 | gRPC API keys for S2S auth |
| `FILES_CORS_ORIGINS__N` | `FILES_CORS_ORIGINS__0=http://localhost:5173` | Allowed CORS origins       |

### Job Options (via `FILES_APP` section)

| Property                     | Default  | Description                                    |
| ---------------------------- | -------- | ---------------------------------------------- |
| `pendingThresholdMinutes`    | `15`     | Minutes before pending files are cleaned up    |
| `processingThresholdMinutes` | `30`     | Minutes before processing files are cleaned up |
| `rejectedThresholdDays`      | `30`     | Days before rejected files are cleaned up      |
| `jobLockTtlMs`               | `300000` | Distributed lock TTL in milliseconds (5 min)   |

### Context Key Configs

Context keys are configured via `CK_*` environment variables parsed by `parseContextKeyConfigs()`. Each context key defines allowed content types, max file size, variant configurations, and optional callback addresses. See [FILES_APP.md](../app/FILES_APP.md) for the full context key config schema.

## Docker Integration

### Dockerfile

`docker/Dockerfile.files` -- multi-stage build:

| Stage   | Base           | Purpose                                           |
| ------- | -------------- | ------------------------------------------------- |
| `deps`  | `node:24-slim` | `pnpm install --frozen-lockfile` (full workspace) |
| `build` | deps           | `pnpm --filter @d2/files-api... run build`        |
| `dev`   | base           | `tsx watch` with bind-mounted source              |
| `prod`  | base           | Compiled JS + `pnpm prune --prod`                 |

Prod entrypoint: `node --import @d2/service-defaults/register dist/main.js` (OTel auto-instrumentation).

### Docker Compose

Service name: `d2-files`, container: `d2-files`.

| Port | Protocol | Description |
| ---- | -------- | ----------- |
| 5300 | HTTP     | Hono REST   |
| 5301 | HTTP/2   | gRPC        |

**Depends on:** `d2-node-init` (completed), `d2-postgres` (healthy), `d2-redis` (healthy), `d2-rabbitmq` (healthy), `d2-minio` (healthy), `d2-clamav` (healthy), `d2-auth` (healthy).

**Health check:** TCP connection to `FILES_HTTP_PORT` (10s interval, 5s timeout, 15 retries, 60s start period).

**Volumes:** Source bind-mounted (`backends/node`, `contracts`), shared `d2-node-modules` volume.

## Dependencies

| Package                         | Purpose                                  |
| ------------------------------- | ---------------------------------------- |
| `@d2/files-app`                 | CQRS handlers, interfaces, service keys  |
| `@d2/files-infra`               | Repo, storage, messaging implementations |
| `@d2/files-domain`              | Entities, enums, constants               |
| `@d2/handler`                   | BaseHandler, IHandlerContext, scope      |
| `@d2/di`                        | ServiceCollection, ServiceProvider       |
| `@d2/result`                    | D2Result pattern                         |
| `@d2/logging`                   | ILogger + Pino                           |
| `@d2/protos`                    | FilesService, FilesJobService proto defs |
| `@d2/messaging`                 | MessageBus, IMessagePublisher            |
| `@d2/cache-redis`               | AcquireLock, ReleaseLock, PingCache      |
| `@d2/jwt-auth`                  | JWT auth middleware for Hono             |
| `@d2/service-defaults`          | Config helpers, gRPC utilities, OTel     |
| `@d2/database-startup-pg`       | Ensure database exists before migrations |
| `@aws-sdk/client-s3`            | S3Client for MinIO                       |
| `@aws-sdk/s3-request-presigner` | Presigned URL generation                 |
| `@grpc/grpc-js`                 | gRPC server                              |
| `@hono/node-server`             | Hono Node.js adapter                     |
| `hono`                          | HTTP framework                           |
| `drizzle-orm`                   | ORM (migrations + query builder)         |
| `ioredis`                       | Redis client                             |
| `pg`                            | PostgreSQL client                        |

## Package Structure

```
src/
  index.ts                       Barrel exports (createFilesApp, FilesServiceConfig)
  main.ts                        Entry point: config, RabbitMQ, server lifecycle
  composition-root.ts            createFilesApp() — DI wiring + initialization
  setup/
    index.ts                     Barrel
    hono-app-setup.ts            buildHonoApp() — middleware + route composition
    grpc-server-setup.ts         buildGrpcServer() — FilesService + FilesJobService
  routes/
    index.ts                     Barrel
    health-routes.ts             GET /health, GET /ready (public)
    upload-routes.ts             POST /avatar, /org/logo, /org/documents, /threads/:threadId/attachments
    download-routes.ts           GET /files/:fileId/:variantName (delegates to DownloadFileVariant)
    variant-url-routes.ts        GET /files/:fileId/:variantName/url (presigned GET URL)
    list-routes.ts               GET /files (paginated by context)
  middleware/
    request-enrichment.ts        IP / fingerprint / WhoIs (calls Geo client)
    request-context-logging.ts   Child logger with traceId
    ambient-scope.ts             AsyncLocalStorage for pre-auth singletons
    distributed-rate-limit.ts    Multi-dimensional sliding-window check
  context-keys.ts                Hono `c.var` typing — FilesVariables (scope, requestContext, …)
  services/
    files-grpc-service.ts        FilesServiceServer implementation (CheckHealth)
    files-jobs-grpc-service.ts   FilesJobServiceServer implementation (RunCleanup)
```

## Tests

All tests are in `@d2/files-tests` (`backends/node/services/files/tests/`). The API layer is tested indirectly through:

- **Unit tests** for app-layer handlers (upload, download, list, health, cleanup)
- **Integration tests** via Testcontainers (full repo + storage pipeline)

Run: `pnpm vitest run --project files-tests`
