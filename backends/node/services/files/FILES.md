# Files Service

## Overview

The **Files** service is a standalone Node.js service that handles file uploads, virus scanning, image processing, and storage for D2-WORX. It is a **shared infrastructure service** -- any feature that needs file attachments (avatars, documents, media) registers a context key and the Files service manages the full lifecycle.

**Runtime:** Node.js (Hono REST + gRPC server + RabbitMQ consumer)
**Database:** PostgreSQL (own schema, Drizzle ORM)
**Object Storage:** MinIO (S3-compatible)
**Inbound:** REST (public upload/download), gRPC (internal queries), RabbitMQ (processing pipeline)
**Outbound:** gRPC (owning service callbacks + SignalR gateway push)

> **Public-facing REST API.** Unlike Auth (Hono + BetterAuth) and Comms (gRPC-only), the Files service exposes purpose-specific REST endpoints directly to browsers for presigned URL uploads and file downloads. This requires JWT validation middleware (ADR-027) since these requests bypass the .NET gateway.

---

## Documentation

| Document                                  | Description                                                                  |
| ----------------------------------------- | ---------------------------------------------------------------------------- |
| [FILES_DOMAIN.md](domain/FILES_DOMAIN.md) | Domain model: File entity, FileVariant, VariantConfig, enums, business rules |
| [FILES_APP.md](app/FILES_APP.md)          | Application layer: CQRS handlers, service keys, context key configuration    |
| [FILES_INFRA.md](infra/FILES_INFRA.md)    | Infrastructure: Drizzle repos, S3 storage, ClamAV, Sharp, gRPC, messaging    |
| [FILES_API.md](api/FILES_API.md)          | API layer: Hono REST routes, gRPC services, composition root, config         |

---

## Design Principles

1. **Context-key-driven configuration** -- Each feature that uses files registers a context key (e.g., `user_avatar`, `org_logo`). The context key determines: allowed content types, max file size, variant definitions (thumbnail sizes), access control strategy, and the gRPC callback address for the owning service. No hardcoded feature knowledge in the Files service.

2. **Presigned URLs for uploads** -- Clients upload directly to MinIO via presigned PUT URLs, bypassing the Files service for large binary payloads. The Files service generates the URL (with size/type constraints) and MinIO notifies via RabbitMQ when the upload completes.

3. **Two-stage async processing** -- Upload notification (MinIO bucket event) and file processing (scan + transform) are separate pipeline stages connected via RabbitMQ. Each stage is independently retriable and the pipeline is idempotent.

4. **Always-ACK messaging** -- Consumers always acknowledge messages. Failed files remain in their current status (`pending` or `processing`) and are caught by the cleanup job, which runs on a schedule and handles abandoned files.

5. **Owning service callbacks** -- After processing, the Files service notifies the owning service via gRPC `OnFileProcessed` RPC. The owning service decides what to do (e.g., Auth sets `user.image = fileId`). Access control queries also delegate to the owning service via `CanAccess` RPC.

6. **Immutable post-processing** -- Once a file reaches `ready` or `rejected` status, it is terminal. "Updates" to file metadata are create-new + delete-old operations. Variants are stored as a JSONB array on the file record.

---

## Core Concepts

### File Lifecycle

```
Client requests upload (REST)
  -> Files service validates context key + access, creates DB record (pending)
  -> Returns presigned PUT URL

Client uploads to MinIO (direct)
  -> MinIO bucket notification -> RabbitMQ -> intake consumer
  -> Validates pending record, transitions to "processing"
  -> Publishes to processing queue

Processing consumer picks up
  -> ClamAV scan (virus detection)
  -> Sharp transform (resize + WebP conversion per variant config)
  -> Store variants in MinIO
  -> Update DB record (ready + variants, or rejected + reason)
  -> gRPC OnFileProcessed callback to owning service
  -> PushFileUpdate (fire-and-forget) pushes to user:{uploaderUserId}
     via RealtimeGateway.PushToChannel gRPC on the SignalR Gateway
```

### Status State Machine

```
pending -> processing | rejected
processing -> ready | rejected
ready -> (terminal)
rejected -> (terminal)
```

### Context Keys

Each context key is a runtime-configured feature slot. Configuration is loaded from indexed environment variables:

| Env Var                           | Purpose                                             |
| --------------------------------- | --------------------------------------------------- |
| `FILES_CK__0__KEY`                | Context key name (e.g., `user_avatar`)              |
| `FILES_CK__0__CALLBACK_ADDR`      | gRPC address of owning service                      |
| `FILES_CK__0__ALLOWED_CATEGORIES` | Accepted content categories (image, document, etc.) |
| `FILES_CK__0__MAX_SIZE_BYTES`     | Per-key upload size limit                           |
| `FILES_CK__0__VARIANTS`           | Variant definitions (name + maxDimension)           |
| `FILES_CK__0__UPLOAD_RESOLUTION`  | Access check strategy for uploads                   |
| `FILES_CK__0__READ_RESOLUTION`    | Access check strategy for reads                     |

### Access Control Strategies

| Strategy        | Upload | Read | How it works                                                 |
| --------------- | ------ | ---- | ------------------------------------------------------------ |
| `jwt_owner`     | Yes    | Yes  | JWT userId must match relatedEntityId                        |
| `jwt_org`       | Yes    | Yes  | JWT orgId must match relatedEntityId                         |
| `callback`      | Yes    | Yes  | Delegates to owning service via gRPC `CanAccess`             |
| `authenticated` | Yes    | Yes  | Any authenticated user — no ownership check beyond valid JWT |

Public access (unauthenticated reads) is deferred to a future phase.

### Variants

Files are processed into variants (thumbnails, previews) defined per context key. Each variant specifies a `name` and `maxDimension`. Images are resized (fit inside, no enlargement) and converted to WebP. SVGs are passed through unchanged.

Example variant config for `user_avatar`:

- `thumb` (maxDimension: 64) -- navigation bar
- `medium` (maxDimension: 256) -- profile page
- `original` (maxDimension: 0) -- full resolution, WebP conversion only

---

## Integration Points

### How Other Services Use Files

1. **Register a context key** in `FILES_CK__*` env vars with callback address pointing to the owning service
2. **Implement `FileCallback` gRPC service** with `OnFileProcessed` and `CanAccess` RPCs
3. **Request uploads** via the Files REST API (presigned URL flow)
4. **Receive callbacks** when processing completes

### Auth Service (`user_avatar`)

- Upload: `jwt_owner` -- user can only upload their own avatar
- Read: `jwt_owner` -- user can only view their own avatar
- Callback: Auth's `OnFileProcessed` sets `user.image = fileId`

### MinIO Bucket Notifications

MinIO is configured (via `mc event add`) to publish `s3:ObjectCreated:*` events to RabbitMQ:

- Exchange: `files.events` (direct)
- Routing key: `file-uploaded`
- Queue: `files.intake`

---

## RabbitMQ Topology

Two-stage pipeline via a single direct exchange:

```
MinIO bucket notification
  -> files.events exchange (routing key: file-uploaded)
  -> files.intake queue
  -> FileUploadedConsumer -> IntakeFileUploaded handler
      -> PublishFileForProcessing (routing key: file-process)
          -> files.events exchange
          -> files.processing queue
          -> FileProcessingConsumer -> ProcessUploadedFile handler
```

---

## DDD Layer Structure

| Layer  | Package            | Depends On            | Purpose                                                   |
| ------ | ------------------ | --------------------- | --------------------------------------------------------- |
| Domain | `@d2/files-domain` | `@d2/utilities`       | Entities, enums, rules, constants                         |
| App    | `@d2/files-app`    | domain, `@d2/handler` | CQRS handlers, service keys, interfaces, DI registration  |
| Infra  | `@d2/files-infra`  | app, domain           | Drizzle repos, S3 storage, ClamAV, Sharp, gRPC, messaging |
| API    | `@d2/files-api`    | app, infra            | Hono REST + gRPC server, composition root, Docker service |
| Tests  | `@d2/files-tests`  | all layers            | Unit + integration tests (Testcontainers)                 |

---

## Database

Single `file` table (Drizzle ORM, PostgreSQL):

| Column            | Type         | Notes                                   |
| ----------------- | ------------ | --------------------------------------- |
| id                | varchar(36)  | PK, UUIDv7                              |
| context_key       | varchar(100) | Feature context                         |
| related_entity_id | varchar(255) | Owning entity (userId, orgId, etc.)     |
| uploader_user_id  | varchar(36)  | JWT userId of the uploader              |
| status            | varchar(20)  | pending / processing / ready / rejected |
| content_type      | varchar(255) | MIME type                               |
| display_name      | varchar(255) | User-provided filename                  |
| size_bytes        | bigint       | Total file size                         |
| variants          | jsonb        | `FileVariant[]` when ready              |
| rejection_reason  | varchar(50)  | When rejected                           |
| created_at        | timestamp    | Row creation                            |
| updated_at        | timestamp    | Last update                             |

**Indexes:** `(context_key, related_entity_id)` for listing, `(status, created_at)` for cleanup job.

---

## Dependencies

| Package                         | Purpose                                      |
| ------------------------------- | -------------------------------------------- |
| `@d2/handler`                   | BaseHandler, IHandlerContext, OTel metrics   |
| `@d2/di`                        | DI container (ServiceKey, ServiceCollection) |
| `@d2/result`                    | D2Result error handling pattern              |
| `@d2/messaging`                 | RabbitMQ publish/subscribe                   |
| `@d2/protos`                    | gRPC FileCallback + RealtimeGateway stubs    |
| `@d2/cache-redis`               | Distributed lock (cleanup job)               |
| `@aws-sdk/client-s3`            | S3 commands for MinIO                        |
| `@aws-sdk/s3-request-presigner` | Presigned URL generation                     |
| `sharp`                         | Image processing (libvips)                   |
| `drizzle-orm`                   | PostgreSQL ORM                               |
| `zod`                           | Input validation (consumers + handlers)      |

---

## Phasing

| Step | Name                            | Status  | Notes                                                                             |
| ---- | ------------------------------- | ------- | --------------------------------------------------------------------------------- |
| F1   | Domain layer                    | Done    | 236 tests at commit. Entities, enums, rules, constants                            |
| F2   | App layer                       | Done    | 323 tests at commit. 11 CQRS handlers, 35 service keys                            |
| F3   | Infra layer                     | Done    | 532 tests total. Drizzle, S3, ClamAV, Sharp, gRPC, messaging                      |
| F4   | JWT middleware (`@d2/jwt-auth`) | Done    | RS256 JWKS verification, fingerprint check, IRequestContext                       |
| F5   | API layer (`@d2/files-api`)     | Done    | Hono REST + gRPC server, composition root, Docker service                         |
| F6   | SignalR Gateway                 | Done    | Separate .NET service, JWT-authed WebSocket, gRPC push interface                  |
| F7   | Owning service callback         | Done    | Auth `FileCallbackService` gRPC server (OnFileProcessed + CanAccess) on port 5101 |
| F8   | Full-stack tests                | Pending | API + E2E adversarial upload testing                                              |
| F9   | SvelteKit profile route         | Pending | Avatar upload, real-time update via SignalR                                       |

---

## Resolved Decisions

| Decision                   | Resolution                                                                        |
| -------------------------- | --------------------------------------------------------------------------------- |
| Storage backend            | MinIO (S3-compatible). Bucket notifications → RabbitMQ for event-driven intake    |
| Image processing           | Sharp (libvips). WebP output for all raster images. SVG passthrough               |
| Virus scanning             | ClamAV via direct TCP INSTREAM protocol (no REST wrapper)                         |
| Access control             | Per-context-key resolution strategy (jwt_owner, jwt_org, callback, authenticated) |
| Variant definitions        | Per-context-key runtime config, not hardcoded. Indexed env vars                   |
| Context key naming         | Feature-prefixed: `user_avatar`, `org_logo`, `thread_attachment`                  |
| Public REST API            | Hono (same as Auth). JWT validation via `@d2/jwt-auth` (ADR-027)                  |
| Presigned URL expiry       | 15 minutes (900 seconds)                                                          |
| Processing pipeline        | Two-stage RabbitMQ (intake → processing). Always-ACK + cleanup job                |
| Real-time status push      | gRPC → SignalR Gateway (ADR-028)                                                  |
| Owning service integration | gRPC `FileCallback` service with `OnFileProcessed` + `CanAccess` RPCs             |

---

## Docker Service

The `d2-files` service in `docker-compose.yml` runs the Files API (Hono REST + gRPC):

- **HTTP port:** `${FILES_HTTP_PORT}` (default 5300) — public REST endpoints (upload, download, list)
- **gRPC port:** `${FILES_GRPC_PORT}` (default 5301) — internal queries and job RPCs
- **Depends on:** `d2-postgres`, `d2-redis`, `d2-rabbitmq`, `d2-minio`, `d2-clamav`, `d2-auth` (JWKS source)
- **Dockerfile:** `docker/Dockerfile.files` (multi-stage, `dev` target for local)

### Environment Variables (Docker overrides)

| Variable                   | Purpose                                                                                                                                               |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `FILES_DATABASE_URL`       | PostgreSQL connection string (points to `d2-postgres` container)                                                                                      |
| `REDIS_URL`                | Redis connection string                                                                                                                               |
| `RABBITMQ_URL`             | RabbitMQ AMQP connection string                                                                                                                       |
| `FILES_S3_ENDPOINT`        | MinIO S3 endpoint (internal Docker network, e.g., `http://d2-minio:9000`)                                                                             |
| `FILES_S3_PUBLIC_ENDPOINT` | Optional. Browser-reachable S3 endpoint for presigned URLs. Used when MinIO is behind a tunnel (e.g., cloudflared). Falls back to `FILES_S3_ENDPOINT` |
| `FILES_JWKS_URL`           | Auth Service JWKS endpoint for JWT verification (e.g., `http://d2-auth:5100/api/auth/jwks`)                                                           |
| `CLAMAV_HOST`              | ClamAV daemon hostname                                                                                                                                |

The `FILES_S3_PUBLIC_ENDPOINT` is necessary because presigned URLs contain the S3 endpoint hostname. When MinIO runs inside Docker, its internal hostname (e.g., `d2-minio:9000`) is not reachable from the browser. Setting `FILES_S3_PUBLIC_ENDPOINT` to a tunnel URL (e.g., a cloudflared tunnel) ensures browsers can PUT directly to MinIO via the presigned URL. The composition root creates a separate `S3Client` for presigning when this variable is set
