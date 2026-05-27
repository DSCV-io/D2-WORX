<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Files

> Parent: [`server/services/`](../README.md)

> **Status**: NOT IMPLEMENTED — tracked at [docs/v2/V2.md](../../../docs/v2/V2.md).

## Purpose

File management + processing + variants. Per-context-key config (each file type has its own access control + size limits + categories + variant ladder). SeaweedFS for storage. ClamAV virus scanning (single container, fail-closed for ALL uploads).

## Six design principles

1. Context-key-driven config
2. Presigned URLs (client-direct upload — bytes never proxy through Files)
3. Two-stage async processing (intake → presigned → storage event → processing → ready/rejected)
4. Always-ACK messaging
5. Owning service callbacks
6. Immutable post-processing

## Status state machine

```
pending → processing → ready
            │           ↑
            └─→ rejected
```

Transitions managed by `transitionFileStatus()` (transactional, audit-logged). Forbidden transitions throw — once terminal, stays terminal.

## Public API surface

- REST API:
  - `POST /api/v1/files/{contextKey}/upload-url` — issue presigned PUT URL
  - `GET /api/v1/files/{id}` — file metadata + presigned read URL
  - `GET /api/v1/files/{contextKey}/list` — list files for a context (per `LIST_RESOLUTION` resolution policy)
  - `DELETE /api/v1/files/{id}` — delete file + variants
- gRPC: callback resolution endpoints (Files calls owning services to verify access)
- RabbitMQ consumer: storage notifications (SeaweedFS upload-complete events)
- RabbitMQ publisher: `d2.files.events` (file lifecycle: uploaded, processed, ready, rejected)

## Dependencies (.NET shared libs)

- `D2.Shared.Messaging` (consumer + publisher)
- `D2.Shared.Encryption` (NOT used for `d2.files.events` — `metadata` is not classified PII; `[Encrypted(Domain.X)]` is the opt-in attribute when payload encryption is required)
- `D2.Shared.Auth` (JWT validation on REST endpoints, scope checks)
- `D2.Shared.Contacts` (file ownership references via `files_contacts_db`)
- `D2.Shared.GeoReference` (locale-aware filename normalization, optional)

## Database

- `files_db` — owned by D2.Files. Schema: `file` (id, context_key, owner_user_id, owner_org_id, status, content_type, size_bytes, storage_key, metadata JSONB, created_at, processed_at), `file_variant` (file_id, variant_name, storage_key, dimensions, size_bytes), `file_processing_log` (audit trail of state transitions).
- `files_contacts_db` — via `D2.Shared.Contacts` library.

## Storage

- SeaweedFS (Filer with PostgreSQL backend) — replaces MinIO for user files
- MinIO retained as backend for LGTM block storage (Loki / Mimir / Tempo)

## ClamAV

Single ClamAV container on the internal network. Files service uses `nClam` to communicate over TCP. **Fail-closed for ALL uploads** — if ClamAV is down, uploads return 503. No exceptions; uploading unscanned files is too risky regardless of file type or user tier.

## Per-context-key config (env var pattern)

```
FILES_CK__0__KEY=user_avatar
FILES_CK__0__UPLOAD_RESOLUTION=jwt_owner
FILES_CK__0__READ_RESOLUTION=authenticated
FILES_CK__0__LIST_RESOLUTION=jwt_owner
FILES_CK__0__CALLBACK_ADDR=d2-edge:5101
FILES_CK__0__CATEGORY__0=image
FILES_CK__0__MAX_SIZE_BYTES=5242880
FILES_CK__0__VARIANT__0__NAME=thumb
FILES_CK__0__VARIANT__0__MAX_DIM=64
# ... more variants
```

Smartphone MIME types (HEIC / HEIF, 3GPP, AAC / M4A, etc.) ship in `D2.Files.Domain` as a constant list per category — keep the list there as the canonical source.

## Client library

`server/services/files/clients/dotnet/D2.Files.Client.csproj` — gRPC client + multi-tier cache (memory → Redis → service) per the [docs/PATTERNS.md](../../../docs/PATTERNS.md) cache pattern. Uses `DefaultOptions` log-suppression for proto-DTOs that can't carry `[RedactData]`.

## References

- [docs/PATTERNS.md](../../../docs/PATTERNS.md) — handler / D2Result / RedactionSpec / cache patterns
- [`server/shared/dotnet/messaging-rabbitmq/README.md`](../../shared/dotnet/messaging-rabbitmq/README.md) — `d2.files.events` exchange contract + RabbitMQ wire format
