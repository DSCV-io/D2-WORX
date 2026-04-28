# @d2/files-domain

Pure domain types for the D2-WORX Files service. Zero infrastructure dependencies -- only depends on `@d2/utilities` for string cleaning and UUIDv7 generation.

## Purpose

Defines entities, enums, business rules, and constants for the **file upload and processing pipeline**. Files are uploaded, scanned (magic bytes + ClamAV), processed (resize/convert via sharp), and served via presigned URLs. These types are consumed by:

- `@d2/files-app` (CQRS handlers)
- `@d2/files-infra` (Drizzle repository, MinIO storage, ClamAV scanner)
- `@d2/files-api` (gRPC service, mappers)

## Design Decisions

| Decision                          | Rationale                                                                        |
| --------------------------------- | -------------------------------------------------------------------------------- |
| Readonly interfaces + factories   | More idiomatic TS than classes. Better tree-shaking, consistent functional style |
| Immutable-by-default              | "Mutation" returns new objects via spread+override                               |
| String literal unions (not enums) | `as const` arrays + derived types. No TS `enum` keyword                          |
| State machine on File             | `pending → processing → ready\|rejected` enforced via `transitionFileStatus`     |
| `sizeBytes` is `number`           | JS `number` handles up to 9 PB -- more than sufficient for file sizes            |
| Variants as JSONB array           | `FileVariant[]` stored on the File entity, populated when status = `ready`       |
| Context key prefix resolution     | `user_`/`org_` resolved from JWT, `thread_` via callback -- extensible           |
| Standard storage format           | Images converted to WebP for all variants; video/audio/document stored as-is     |
| No video transcoding              | Videos are store-and-serve only (original + thumbnail). Size limit is the guard  |
| MIME list includes smartphones    | HEIC/HEIF (iPhone photos), 3GPP (Android video), AAC/M4A (voice memos)           |

## Package Structure

```
src/
  index.ts                    Barrel exports
  constants/
    files-constants.ts        FILES_SIZE_LIMITS, FILES_FIELD_LIMITS, ALLOWED_CONTENT_TYPES,
                              FILES_MESSAGING
    error-codes.ts            FILES_ERROR_CODES (9 codes, FILES_ prefixed)
  enums/
    file-status.ts            FileStatus: pending | processing | ready | rejected (+ state machine)
    content-category.ts       ContentCategory: image | document | video | audio
    rejection-reason.ts       RejectionReason: size_exceeded | invalid_content_type | magic_bytes_mismatch |
                              content_moderation_failed | processing_timeout | corrupt_file
    variant-size.ts           VariantSize: plain string type (no fixed enum — variant names are
                              per-context-key config, e.g., "thumb", "preview", "banner")
  exceptions/
    files-domain-error.ts     Base error (extends Error)
    files-validation-error.ts Structured validation error (entityName, propertyName, invalidValue, reason)
  entities/
    file.ts                   File interface + createFile + transitionFileStatus
  value-objects/
    file-variant.ts           FileVariant interface + createFileVariant
    variant-config.ts         VariantConfig interface + requiresResize helper
  rules/
    content-type-rules.ts     resolveContentCategory, isContentTypeAllowed, getAllowedContentTypes
  storage-keys.ts             buildRawStorageKey, buildVariantStorageKey, getExtensionForContentType
                              + RawStorageKeyFile / VariantStorageKeyFile interfaces
```

> Storage-key construction lives in domain (single source of truth). `@d2/files-app` re-exports the helpers and types so consumers import from either layer transparently.

## Enums

All "enums" are `as const` arrays with derived union types and type guard functions.

| Enum            | Values                                                                                                                                 | Extras                                  |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------- |
| FileStatus      | pending, processing, ready, rejected                                                                                                   | `FILE_STATUS_TRANSITIONS` state machine |
| ContentCategory | image, document, video, audio                                                                                                          | `isValidContentCategory` guard          |
| RejectionReason | size_exceeded, invalid_content_type, magic_bytes_mismatch, content_moderation_failed, processing_timeout, corrupt_file, virus_detected | `isValidRejectionReason` guard          |
| VariantSize     | Plain string type (no fixed values — variant names are per-context-key runtime config)                                                 | —                                       |

### File Status State Machine

```
pending → processing | rejected
processing → ready | rejected
ready → (terminal)
rejected → (terminal)
```

## Entities

| Entity | Factory      | Transition             | Key Rules                                                                                                                                                                                                                                                                                                                                  |
| ------ | ------------ | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| File   | `createFile` | `transitionFileStatus` | Starts as `pending`, `variants` is `?: readonly FileVariant[]` (undefined until `ready`), `rejectionReason` is `?: RejectionReason` (undefined until `rejected`). `uploaderUserId` tracks who initiated the upload (for realtime push targeting). All string fields cleaned via `cleanStr()`, sizeBytes validated against configurable max |

## Value Objects

| Value Object  | Factory             | Notes                                                                   |
| ------------- | ------------------- | ----------------------------------------------------------------------- |
| FileVariant   | `createFileVariant` | Processed variant (size, MinIO key, dimensions, sizeBytes, contentType) |
| VariantConfig | -- (type-only)      | Per-variant config (name, maxDimension). `requiresResize()` helper      |

## Business Rules

| Rule                 | Function                                 | Description                                               |
| -------------------- | ---------------------------------------- | --------------------------------------------------------- | --------------------------------- |
| Content category     | `resolveContentCategory(contentType)`    | MIME → category mapping, returns `ContentCategory         | undefined` (undefined if unknown) |
| Content type allowed | `isContentTypeAllowed(type, categories)` | Checks MIME type belongs to one of the allowed categories |
| All allowed types    | `getAllowedContentTypes(categories)`     | Flat array of all MIME types for given categories         |
| Resize needed        | `requiresResize(config)`                 | True when `maxDimension > 0` (original = no resize)       |

## Storage Keys

| Function                                          | Purpose                                                                        |
| ------------------------------------------------- | ------------------------------------------------------------------------------ |
| `getExtensionForContentType(contentType)`         | MIME → file extension (falls back to `bin` for unknown types)                  |
| `buildRawStorageKey(file)`                        | `{contextKey}/{relatedEntityId}/{fileId}/raw.{ext}` — raw upload key           |
| `buildVariantStorageKey(file, size, contentType)` | `{contextKey}/{relatedEntityId}/{fileId}/{size}.{ext}` — processed variant key |

`RawStorageKeyFile` (`contextKey`, `relatedEntityId`, `id`, `contentType`) and `VariantStorageKeyFile` (without `contentType`) are minimal interfaces — consumers pass the full `File` entity or any object satisfying the shape.

## Constants

### FILES_SIZE_LIMITS

| Constant                 | Value | Purpose                                                       |
| ------------------------ | ----- | ------------------------------------------------------------- |
| `DEFAULT_MAX_SIZE_BYTES` | 25 MB | Default upload limit (per-context-key overrides in app layer) |

### FILES_FIELD_LIMITS

| Constant                       | Value | Purpose                     |
| ------------------------------ | ----- | --------------------------- |
| `MAX_CONTEXT_KEY_LENGTH`       | 100   | Max context key string      |
| `MAX_RELATED_ENTITY_ID_LENGTH` | 255   | Max related entity ID       |
| `MAX_CONTENT_TYPE_LENGTH`      | 255   | Max MIME type string        |
| `MAX_DISPLAY_NAME_LENGTH`      | 255   | Max user-provided filename  |
| `MAX_VARIANT_KEY_LENGTH`       | 512   | Max MinIO storage key       |
| `MAX_UPLOADER_USER_ID_LENGTH`  | 36    | Max uploader user ID (UUID) |

### ALLOWED_CONTENT_TYPES

| Category | MIME Types                                                |
| -------- | --------------------------------------------------------- |
| image    | jpeg, png, gif, webp, svg+xml, avif, heic, heif           |
| document | pdf, docx (openxml), xlsx (openxml), text/plain, text/csv |
| video    | mp4, webm, quicktime, 3gpp                                |
| audio    | mpeg, ogg, wav, webm, aac, mp4                            |

### FILES_MESSAGING

Two-stage pipeline: MinIO → intake queue → processing queue.

| Constant                 | Value              | Purpose                                             |
| ------------------------ | ------------------ | --------------------------------------------------- |
| `EVENTS_EXCHANGE`        | `files.events`     | Direct exchange for all files lifecycle events      |
| `EVENTS_EXCHANGE_TYPE`   | `direct`           | Exchange type — direct routing                      |
| `UPLOAD_ROUTING_KEY`     | `file-uploaded`    | MinIO bucket notification events (upload completed) |
| `PROCESSING_ROUTING_KEY` | `file-process`     | Internal processing dispatch (post-intake)          |
| `INTAKE_QUEUE`           | `files.intake`     | MinIO upload notifications → intake handler         |
| `PROCESSING_QUEUE`       | `files.processing` | Processing dispatch → process handler               |

### FILES_ERROR_CODES

| Code                        | Value                             |
| --------------------------- | --------------------------------- |
| `FILE_TOO_LARGE`            | `FILES_FILE_TOO_LARGE`            |
| `INVALID_CONTENT_TYPE`      | `FILES_INVALID_CONTENT_TYPE`      |
| `CONTENT_TYPE_NOT_ALLOWED`  | `FILES_CONTENT_TYPE_NOT_ALLOWED`  |
| `PROCESSING_FAILED`         | `FILES_PROCESSING_FAILED`         |
| `INVALID_CONTEXT_KEY`       | `FILES_INVALID_CONTEXT_KEY`       |
| `ACCESS_DENIED`             | `FILES_ACCESS_DENIED`             |
| `VARIANT_NOT_FOUND`         | `FILES_VARIANT_NOT_FOUND`         |
| `FILE_REJECTED`             | `FILES_FILE_REJECTED`             |
| `INVALID_STATUS_TRANSITION` | `FILES_INVALID_STATUS_TRANSITION` |

## Tests

All tests are in `@d2/files-tests` (`backends/node/services/files/tests/`):

```
src/unit/domain/
  constants/     files-constants.test.ts, error-codes.test.ts
  enums/         file-status.test.ts, content-category.test.ts,
                 rejection-reason.test.ts, variant-size.test.ts
  exceptions/    files-domain-error.test.ts, files-validation-error.test.ts
  entities/      file.test.ts
  value-objects/  file-variant.test.ts, variant-config.test.ts
  rules/         content-type-rules.test.ts
  storage-keys.test.ts
```

Run: `pnpm vitest run --project files-tests`

## Dependencies

| Package         | Purpose                 |
| --------------- | ----------------------- |
| `@d2/utilities` | String cleaning, UUIDv7 |
