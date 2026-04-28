# @d2/files-infra

Infrastructure layer for the D2-WORX Files service. Implements all repository, storage, provider, outbound, realtime, and messaging interfaces defined in `@d2/files-app`.

## Purpose

Provides concrete implementations for:

- **Repository** — Drizzle ORM handlers for the `files` PostgreSQL table
- **Storage** — `@aws-sdk/client-s3` handlers for MinIO object storage
- **Scanning** — Direct TCP to ClamAV daemon (INSTREAM protocol)
- **Image processing** — Sharp (libvips) for resize + WebP conversion
- **Outbound** — gRPC clients for `FileCallback` service (CanAccess, OnFileProcessed)
- **Realtime** — gRPC client for SignalR Gateway (`PushToChannel` via `realtime/v1/realtime_gateway.proto`)
- **Messaging** — RabbitMQ handlers (1 publisher, 2 subscribers) + 2 consumers

## Package Structure

```
src/
  index.ts                    Barrel exports
  registration.ts             addFilesInfra(services, config)
  repository/
    schema/
      tables.ts               Drizzle pgTable definition (files)
      types.ts                 Row/insert types
      index.ts                 Barrel
    handlers/
      c/  create-file-record.ts
      r/  get-file-by-id.ts, get-files-by-context.ts, get-stale-files.ts, ping-db.ts
      u/  update-file-record.ts
      d/  delete-file-record.ts, delete-file-records-by-ids.ts
    mappers/
      file-mapper.ts           toFile(row) → File domain entity
  storage/handlers/            7 S3 handlers (C/R/D 3LC — see BACKENDS.md)
    c/  put-storage-object.ts        PutObjectCommand
        presign-put-url.ts           getSignedUrl (configurable expiry, default 15min)
    r/  get-storage-object.ts        GetObjectCommand → stream to buffer
        head-storage-object.ts       HeadObjectCommand (exists check)
        presign-get-url.ts           getSignedUrl (read access)
        ping-storage.ts              ListBucketsCommand (latency probe)
    d/  delete-storage-object.ts     DeleteObjectCommand (idempotent)
        delete-storage-objects.ts    DeleteObjectsCommand (batch)
  scanning/handlers/
    scan-file.ts               Direct TCP clamd INSTREAM protocol
  image-processing/handlers/
    process-variants.ts        Sharp resize + WebP (SVG passthrough)
  outbound/handlers/           gRPC FileCallback clients
    call-on-file-processed.ts  OnFileProcessed RPC (dynamic connection cache)
    call-can-access.ts         CanAccess RPC (dynamic connection cache)
  realtime/handlers/
    push-file-update.ts        SignalR Bridge PushToChannel RPC
  messaging/
    handlers/
      pub/  publish-file-for-processing.ts   Publishes { fileId } to processing queue
      sub/  intake-file-uploaded.ts          Delegates to IntakeFile app handler
            process-uploaded-file.ts         Delegates to ProcessFile app handler
    consumers/
      file-uploaded-consumer.ts    MinIO upload notifications → IntakeFileUploaded
      file-processing-consumer.ts  Processing dispatch → ProcessUploadedFile
```

## Handler Inventory

### Repository Handlers (8)

| Handler                | 3LC | Operation                                        |
| ---------------------- | --- | ------------------------------------------------ |
| CreateFileRecord       | C/  | Insert new file row                              |
| GetFileById            | R/  | Select by primary key, `notFound` if missing     |
| GetFilesByContext      | R/  | Paginated select by contextKey + relatedEntityId |
| GetStaleFiles          | R/  | Select by status + createdAt cutoff              |
| PingDb                 | R/  | `SELECT 1` latency probe                         |
| UpdateFileRecord       | U/  | Update + `.returning()`, `notFound` if empty     |
| DeleteFileRecord       | D/  | Delete + `.returning()`, `notFound` if empty     |
| DeleteFileRecordsByIds | D/  | Batch delete by ID array                         |

### Storage Handlers (7)

All receive `S3Client` + `bucketName` in constructor. Transient.

| Handler              | S3 Command           | Notes                                        |
| -------------------- | -------------------- | -------------------------------------------- |
| PutStorageObject     | PutObjectCommand     | Buffer + ContentType                         |
| GetStorageObject     | GetObjectCommand     | Stream → buffer                              |
| HeadStorageObject    | HeadObjectCommand    | Returns `{ exists, contentType, sizeBytes }` |
| DeleteStorageObject  | DeleteObjectCommand  | Idempotent (missing = success)               |
| DeleteStorageObjects | DeleteObjectsCommand | Batch delete                                 |
| PresignPutUrl        | PutObjectCommand     | `getSignedUrl`, default 900s                 |
| PingStorage          | ListBucketsCommand   | Latency probe                                |

### Scanning Handlers (1)

| Handler  | Technology   | Notes                                                        |
| -------- | ------------ | ------------------------------------------------------------ |
| ScanFile | ClamAV (TCP) | INSTREAM protocol, `net.Socket`, returns `{ clean, threat }` |

### Image-processing Handlers (1)

| Handler         | Technology      | Notes                                                     |
| --------------- | --------------- | --------------------------------------------------------- |
| ProcessVariants | Sharp (libvips) | Resize + WebP, SVG passthrough, returns processed buffers |

### Outbound Handlers (2)

gRPC clients for the `FileCallback` proto service. Dynamic connection cache (`Map<string, FileCallbackClient>`) keyed by address — different context keys may point to different gRPC hosts.

| Handler             | RPC             | Returns                |
| ------------------- | --------------- | ---------------------- |
| CallOnFileProcessed | OnFileProcessed | `{ success: boolean }` |
| CallCanAccess       | CanAccess       | `{ allowed: boolean }` |

### Realtime Handlers (1)

| Handler        | Proto Service   | RPC           | Notes                                                                                  |
| -------------- | --------------- | ------------- | -------------------------------------------------------------------------------------- |
| PushFileUpdate | RealtimeGateway | PushToChannel | Pushes file status to `user:{uploaderUserId}` via `realtime/v1/realtime_gateway.proto` |

### Messaging Handlers (3)

| Handler                  | Direction | Exchange     | Routing Key  |
| ------------------------ | --------- | ------------ | ------------ |
| PublishFileForProcessing | Pub       | files.events | file-process |
| IntakeFileUploaded       | Sub       | —            | —            |
| ProcessUploadedFile      | Sub       | —            | —            |

### Consumers (2)

| Consumer                     | Queue            | Routing Key   | Resolves                |
| ---------------------------- | ---------------- | ------------- | ----------------------- |
| createFileUploadedConsumer   | files.intake     | file-uploaded | IIntakeFileUploadedKey  |
| createFileProcessingConsumer | files.processing | file-process  | IProcessUploadedFileKey |

Both consumers always ACK — failed files stay in their current status and are caught by the cleanup job.

## Messaging Topology

Two-stage pipeline via a single direct exchange:

```
MinIO bucket notification
  → files.events exchange (routing key: file-uploaded)
  → files.intake queue
  → FileUploadedConsumer → IntakeFileUploaded handler
      → validates pending record, transitions to processing
      → PublishFileForProcessing (routing key: file-process)
          → files.events exchange
          → files.processing queue
          → FileProcessingConsumer → ProcessUploadedFile handler
              → scan + transform + notify pipeline
```

## Drizzle Schema

Single `files` table:

| Column            | Type                  | Notes                                |
| ----------------- | --------------------- | ------------------------------------ |
| id                | varchar(36) PK        | UUIDv7                               |
| context_key       | varchar(100) NOT NULL |                                      |
| related_entity_id | varchar(255) NOT NULL |                                      |
| uploader_user_id  | varchar(36) NOT NULL  | JWT userId of the uploader           |
| status            | varchar(20) NOT NULL  | Default: `"pending"`                 |
| content_type      | varchar(255) NOT NULL |                                      |
| display_name      | varchar(255) NOT NULL |                                      |
| size_bytes        | bigint NOT NULL       |                                      |
| variants          | jsonb                 | Nullable, `FileVariant[]` when ready |
| rejection_reason  | varchar(50)           | Nullable                             |
| created_at        | timestamp NOT NULL    | Default: `now()`                     |
| updated_at        | timestamp NOT NULL    | Default: `now()`                     |

**Indexes:**

- `(context_key, related_entity_id)` — composite for listing by owner
- `(status, created_at)` — for stale file queries (cleanup job)

## DI Registration

```typescript
addFilesInfra(services: ServiceCollection, config: FilesInfraConfig): void
```

Config shape:

```typescript
interface FilesInfraConfig {
  readonly db: NodePgDatabase;
  readonly s3: S3Client;
  readonly bucketName: string;
  readonly clamd: ClamdConfig; // { host, port }
  readonly publisher: IMessagePublisher;
  readonly signalrGatewayAddress: string; // e.g., "d2-signalr:5200"
  readonly s3Public?: S3Client;
}
```

The optional `s3Public` field is a separate `S3Client` configured with a browser-reachable endpoint (e.g., a cloudflared tunnel URL via `FILES_S3_PUBLIC_ENDPOINT`). It is used only by `PresignPutUrl` to generate presigned URLs that browsers can PUT to directly. When MinIO runs inside Docker, its internal hostname is not reachable from the browser -- `s3Public` provides an alternative endpoint for presigning. Falls back to the primary `s3` client if not provided.

All handlers are transient. gRPC callback clients share a `Map<string, FileCallbackClient>` connection cache.

## Docker Compose

The infra layer requires these services in `docker-compose.yml`:

- **ClamAV** — `clamav/clamav:1.4`, port 3310, `d2-clamav-data` volume
- **MinIO** — AMQP notification config pointing to RabbitMQ (`files.events` exchange, `file-uploaded` routing key)
- **MinIO init** — Creates `d2-files` bucket + `mc event add` for `s3:ObjectCreated:*`
- **PostgreSQL** — `d2-services-files` database (alongside existing auth/comms DBs)

## Dependencies

| Package                         | Purpose                                   |
| ------------------------------- | ----------------------------------------- |
| `@d2/files-app`                 | Interfaces, service keys                  |
| `@d2/files-domain`              | Entities, enums, constants                |
| `@d2/handler`                   | BaseHandler, IHandlerContext              |
| `@d2/di`                        | ServiceCollection, ServiceKey             |
| `@d2/result`                    | D2Result pattern                          |
| `@d2/messaging`                 | MessageBus, ConsumerResult                |
| `@d2/protos`                    | FileCallbackClient, RealtimeGatewayClient |
| `@d2/service-defaults`          | gRPC trace context interceptor            |
| `@aws-sdk/client-s3`            | S3 commands                               |
| `@aws-sdk/s3-request-presigner` | Presigned URL generation                  |
| `sharp`                         | Image processing (libvips)                |
| `drizzle-orm`                   | ORM for PostgreSQL                        |
| `@grpc/grpc-js`                 | gRPC client                               |
| `zod`                           | Message validation in consumers           |

## Tests

All tests are in `@d2/files-tests` (`backends/node/services/files/tests/`):

### Unit Tests

```
src/unit/infra/
  helpers/          test-context.ts
  repository/
    handlers/       create-file-record, get-file-by-id, get-files-by-context,
                    get-stale-files, ping-db, update-file-record, delete-file-record,
                    delete-file-records-by-ids
    mappers/        file-mapper.test.ts
  storage/          storage-handlers.test.ts (29 tests) — covers all C/R/D storage handlers
  scanning/         scan-file.test.ts
  image-processing/ process-variants.test.ts
  outbound/         outbound-handlers.test.ts
  messaging/        messaging-handlers.test.ts
```

### Integration Tests (Testcontainers)

```
src/integration/
  helpers/
    postgres-test-helpers.ts   PostgresTestHelper wrapper (start/stop/clean)
    minio-test-helpers.ts      MinIO GenericContainer + S3Client + Content-MD5 middleware
    test-context.ts            Silent logger + IRequestContext
  repository-handlers.test.ts  29 tests — all 8 repo handlers against real PostgreSQL
  storage-handlers.test.ts     13 tests — all 7 storage handlers against real MinIO
```

Integration tests use `@testcontainers/postgresql` (Postgres 18) and `testcontainers` `GenericContainer` (MinIO latest). The MinIO S3Client includes Content-MD5 middleware for AWS SDK v3 compatibility (MinIO requires Content-MD5 on `DeleteObjects`, but SDK v3.800+ switched to CRC32).

Run: `pnpm vitest run --project files-tests`
