# @d2/files-app

Application layer for the D2-WORX Files service. CQRS handlers implementing the file upload, processing, and management pipeline. Depends on `@d2/files-domain` for entities/rules and defines interfaces consumed by `@d2/files-infra`.

## Purpose

Orchestrates file lifecycle operations — upload (presigned URL generation), intake (post-upload validation), processing (scan + transform + notify), deletion, and cleanup. Defines all service keys and provider/repository interfaces that the infra layer implements.

## Package Structure

```
src/
  index.ts                    Barrel exports
  context-key-config.ts       ContextKeyConfig + parseContextKeyConfigs (env var parser)
  files-job-options.ts        FilesJobOptions (cleanup batch size, stale threshold)
  registration.ts             addFilesApp(services, configMap, jobOptions)
  service-keys.ts             35 ServiceKey<T> DI tokens
  implementations/
    cqrs/handlers/
      c/                      Commands (6 handlers)
        upload-file.ts         Presigned URL generation, access check, DB record creation
        intake-file.ts         Post-upload validation (pending → processing transition)
        process-file.ts        Scan + transform + notify pipeline orchestrator
        delete-file.ts         File deletion (DB + storage cleanup)
        run-cleanup.ts         Stale file cleanup job (distributed lock)
        notify-file-processed.ts  gRPC callback to owning service
      q/                      Queries (4 handlers)
        get-file-metadata.ts   Single file metadata retrieval (with access check)
        list-files.ts          Paginated file listing by context (with access check)
        check-file-access.ts   gRPC CanAccess delegation to owning service
        check-health.ts        DB + storage health probe
      u/                      Utilities (1 handler)
        resolve-file-access.ts  Access resolution strategy dispatcher (jwt_owner/jwt_org/callback)
  interfaces/
    cqrs/handlers/             CQRS handler interfaces (Commands, Queries, Utilities namespaces)
    repository/handlers/       Repository handler interfaces (C/R/U/D)
      c/  create-file-record.ts
      r/  get-file-by-id.ts, get-files-by-context.ts, get-stale-files.ts, ping-db.ts
      u/  update-file-record.ts
      d/  delete-file-record.ts, delete-file-records-by-ids.ts
    storage/handlers/          7 storage interfaces (C/R/D 3LC — see BACKENDS.md)
      c/  put-storage-object, presign-put-url
      r/  get-storage-object, head-storage-object, presign-get-url, ping-storage
      d/  delete-storage-object, delete-storage-objects
    scanning/handlers/         scan-file.ts (ClamAV interface)
    image-processing/handlers/ process-variants.ts (sharp interface)
    outbound/handlers/         gRPC callback interfaces
      call-on-file-processed.ts   Notify owning service of processing completion
      call-can-access.ts          Query owning service for access authorization
    realtime/handlers/         Real-time push interfaces
      push-file-update.ts        Push file status updates to connected clients via SignalR
    messaging/handlers/        Messaging handler interfaces
      pub/  publish-file-for-processing.ts
      sub/  intake-file-uploaded.ts, process-uploaded-file.ts
```

## CQRS Handler Summary

| Category | Handler             | Type    | Description                                                              |
| -------- | ------------------- | ------- | ------------------------------------------------------------------------ |
| C/       | UploadFile          | Command | Validates context key + access, creates DB record, returns presigned URL |
| C/       | IntakeFile          | Command | Post-MinIO-upload: validates pending record, transitions to processing   |
| C/       | ProcessFile         | Command | Scan (ClamAV) → transform (sharp) → store variants → notify owner        |
| C/       | DeleteFile          | Command | Removes DB record + all storage objects for a file                       |
| C/       | RunCleanup          | Command | Distributed-locked stale file cleanup job                                |
| C/       | NotifyFileProcessed | Command | gRPC `OnFileProcessed` callback to owning service                        |
| Q/       | GetFileMetadata     | Query   | Single file lookup with access resolution                                |
| Q/       | ListFiles           | Query   | Paginated listing by context key + related entity                        |
| Q/       | CheckFileAccess     | Query   | Delegates to owning service via gRPC `CanAccess`                         |
| Q/       | CheckHealth         | Query   | DB ping + storage ping                                                   |
| U/       | ResolveFileAccess   | Utility | Dispatches access check by resolution strategy                           |

## Service Keys (35 total)

| Group            | Count | Key pattern         |
| ---------------- | ----- | ------------------- |
| Repository       | 8     | `Files.Repo.*`      |
| Storage          | 7     | `Files.Infra.*`     |
| Scanning         | 1     | `Files.Provider.*`  |
| Image-processing | 1     | `Files.Provider.*`  |
| Outbound         | 2     | `Files.Outbound.*`  |
| Realtime         | 1     | `Files.Realtime.*`  |
| App (CQRS)       | 11    | `Files.App.*`       |
| Messaging        | 3     | `Files.Messaging.*` |
| Config           | 1     | `Files.Config.*`    |

> Storage / scanning / image-processing live as first-class TLCs (see [BACKENDS.md](../../../BACKENDS.md) for the canonical TLC list). Service-key string identifiers retain the legacy `Files.Provider.*` / `Files.Infra.*` prefixes for runtime compatibility — only the folder layout changed.

## Context Key Configuration

Per-context-key runtime config loaded from indexed env vars (`FILES_CK__0__KEY`, `FILES_CK__0__CALLBACK_ADDR`, etc.). Parsed by `parseContextKeyConfigs()` into a `ContextKeyConfigMap`.

Each context key config specifies:

- `contextKey` — the key name (e.g., `user_avatar`)
- `callbackAddress` — gRPC host:port of the owning service
- `allowedCategories` — which content categories are accepted
- `maxSizeBytes` — per-key upload size limit (optional, falls back to domain default)
- `variants` — per-key variant definitions with `name` + `maxDimension`
- `uploadResolution` — how upload access is checked (`jwt_owner`, `jwt_org`, `callback`, `authenticated`)
- `readResolution` — how read access is checked (`jwt_owner`, `jwt_org`, `callback`, `authenticated`)

## DI Registration

```typescript
addFilesApp(services, contextKeyConfigs, jobOptions?)
```

All CQRS handlers are transient. Config map is singleton. Requires infra-layer keys to be registered first.

## Dependencies

| Package            | Purpose                           |
| ------------------ | --------------------------------- |
| `@d2/files-domain` | Entities, enums, rules, constants |
| `@d2/handler`      | BaseHandler, IHandlerContext      |
| `@d2/di`           | ServiceKey, ServiceCollection     |
| `@d2/result`       | D2Result pattern                  |
| `@d2/cache-redis`  | Distributed lock (cleanup job)    |
| `@d2/interfaces`   | Distributed cache types           |

## Tests

All tests are in `@d2/files-tests` (`backends/node/services/files/tests/`):

```
src/unit/app/
  handlers/c/   delete-file, intake-file, notify-file-processed, process-file,
                run-cleanup, upload-file
  handlers/q/   check-file-access, check-health, get-file-metadata, list-files
  handlers/u/   resolve-file-access
  helpers/      mock-handlers.ts, test-config.ts
  utils/        context-key-config.test.ts
```

The `storage-keys` helpers live in `@d2/files-domain` — see `tests/src/unit/domain/storage-keys.test.ts`.

Run: `pnpm vitest run --project files-tests`
