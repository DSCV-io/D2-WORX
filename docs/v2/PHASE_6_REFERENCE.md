<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_6_REFERENCE.md — D2.Files (.NET)

> **Phase 6** per V2.md §4. Reference doc preserved during rebuild — **delete this file after Phase 6 ships**.
>
> **Source**: distilled from v1 `backends/node/services/files/FILES.md` + `FILES_DOMAIN.md` + .NET Geo.Client log-suppression pattern (preserved in `/old/v1/D2-WORX/`).

---

## Files Service Rebuild

v1 D2.Files was Node.js. Phase 6 rebuilds it in .NET. Same domain decisions, new implementation.

Storage backend changes per V2.md §5.6: SeaweedFS (with PG Filer) replaces MinIO for user files. MinIO retained as backend for LGTM block storage.

---

## Six Design Principles (preserved from v1 FILES.md)

1. **Context-key-driven config.** All file behavior (upload resolution, read resolution, max size, allowed categories, variants) is driven by a `context_key` (e.g., `user_avatar`, `org_logo`, `thread_attachment`). Per-context-key config in env vars / DB. Adding a new file type = adding a new context key, not a new code path.
2. **Presigned URLs (client-direct upload).** Browser uploads directly to storage via presigned PUT URLs. Files service issues the URL + records the intent; storage notifies Files when upload completes (via S3 / SeaweedFS event). **Bytes never proxy through Files** — saturates the service.
3. **Two-stage async processing.** Upload → "intake" record (status: `pending`) → presigned URL issued → client uploads → storage event → Files moves to `processing` → variants generated (thumbnails, resizes) → final state `ready` (or `rejected` if validation fails). Each stage is observable + retryable.
4. **Always-ACK messaging.** Files consumes events from RMQ (storage notifications, processing requests) and ALWAYS ACKs after the work commits to PG. Failures land in DLQ with explicit error context. No silent message loss.
5. **Owning service callbacks.** Each context key has a `callback_addr` (e.g., `d2-edge:5101` for `user_avatar`). Files calls back to the owning service to (a) check access permissions before issuing presigned URL, and (b) notify on processing completion (`ready` / `rejected`). Owning services are the source of truth for "who can upload/read this".
6. **Immutable post-processing.** Once a file reaches `ready`, its bytes + variants are immutable. "Updates" = upload a new file (new ID), repoint the owner's reference, delete the old (background cleanup). Cached references stay valid forever — no invalidation logic needed.

---

## Status State Machine

```
pending  →  processing  →  ready
              │              ↑
              └──────→  rejected
```

Transitions managed by a single `transitionFileStatus()` method (transactional, with audit logging). Forbidden transitions throw — there's no `ready → processing` or `rejected → ready`. Once terminal, stays terminal.

---

## Smartphone-Aware MIME List

Modern smartphones produce media in formats that older MIME lists miss. Phase 6 must include:

**Images**:
- HEIC / HEIF (iOS default since iOS 11; Android adopted)
- AVIF (next-gen, increasing browser support)
- WebP (Android default)
- JPEG, PNG, GIF (legacy)

**Audio**:
- AAC / M4A (iOS voice memos, podcasts)
- 3GPP (Android voice recordings)
- MP3, OGG, WAV (legacy)

**Video**:
- MOV / QuickTime (iOS camera default)
- 3GPP / 3GP (older Android)
- MP4 (cross-platform default)
- WebM (newer Android)

**Documents**:
- PDF, DOCX, XLSX, PPTX, ODT (office)
- HEIC live photos (treat as image)

The full canonical list ships in v2 `D2.Files.Domain` constants; reference v1 `FILES_DOMAIN.md` for the seed.

---

## DefaultOptions Log-Suppression Pattern (from v1 .NET Geo.Client)

For client libraries that wrap proto-generated DTOs (which can't carry `[RedactData]` per V2.md §6.10), use `DefaultOptions` overrides on the handler.

Pattern:

```csharp
public sealed partial class GetFilesByIds : BaseHandler<GetFilesByIds, Input, Output>, IGetFilesByIds
{
    protected override HandlerOptions DefaultOptions => new()
    {
        LogInput = false,    // Input contains presigned URLs (PII)
        LogOutput = false,   // Output contains File DTO with PII fields
    };

    // ... handler body
}
```

Rules:
- Document **why** in a class-level XML comment (e.g., "Output contains presigned URLs which embed PII")
- Apply at the handler level, NOT at every call site (call sites would have to know the suppression rule — bad)
- For domain types we own, use `[RedactData]` instead — it's recursive, type-cached, applies to all logging
- Use `DefaultOptions` only when `[RedactData]` can't apply (proto-generated DTOs we don't control)

This pattern goes in v2 `D2.Files.Client` and any other client wrapping proto DTOs with PII.

---

## Files internals (per V2.md §5.6)

- gRPC + REST API (REST for browser presigned URL flow; gRPC for service-to-service callback resolution)
- Storage backend: **SeaweedFS** (Filer with PG backend per V2.md §5.6) — replaces v1 MinIO for user files
- ClamAV virus scanning (single container, fail-closed for ALL uploads per V2.md §5.4)
- Sharp (or .NET equivalent) for image variant generation
- RabbitMQ consumer for storage notifications + processing
- gRPC outbound to owning services (callbacks)
- gRPC outbound to Edge SignalR push (delivery status notifications)

---

## Per-Context-Key Config (preserved env-var convention from v1)

```
FILES_CK__0__KEY=user_avatar
FILES_CK__0__UPLOAD_RESOLUTION=jwt_owner    # who can upload (jwt_owner | jwt_org | callback | authenticated)
FILES_CK__0__READ_RESOLUTION=authenticated  # who can read
FILES_CK__0__LIST_RESOLUTION=jwt_owner      # who can list (often stricter than read)
FILES_CK__0__CALLBACK_ADDR=d2-edge:5101     # owning service for permission callbacks
FILES_CK__0__CATEGORY__0=image              # allowed categories
FILES_CK__0__MAX_SIZE_BYTES=5242880         # 5 MiB
FILES_CK__0__VARIANT__0__NAME=thumb
FILES_CK__0__VARIANT__0__MAX_DIM=64
# ... more variants
```

Phase 6 rebuild should preserve this convention so existing `.env.local` configs migrate cleanly.

Resolution types:
- `jwt_owner` — JWT `sub` must match the file's owner (e.g., user uploading their own avatar)
- `jwt_org` — JWT `org` must match the file's org (e.g., org logo)
- `callback` — Files calls owning service to confirm permission (e.g., thread attachment — Comms confirms membership)
- `authenticated` — any signed-in user (e.g., reading another user's avatar by ID)

---

## When This Doc Gets Deleted

Phase 6 completion criteria includes:
- [ ] D2.Files (.NET) ships with the 6 design principles preserved
- [ ] Status state machine implemented + tested
- [ ] Smartphone MIME list seeded
- [ ] Per-context-key config matches v1 convention (or documented divergences)
- [ ] DefaultOptions log-suppression pattern documented in `server/services/files/clients/dotnet/README.md`
- [ ] SeaweedFS integration shipped (replacing MinIO for user files)
- [ ] Per-service README (`server/services/files/README.md`) captures the Phase 6 details
- [ ] V2.md §5.6 is the canonical reference for storage decisions going forward

Once the per-service README exists + is accurate, this reference doc has served its purpose. Move to `docs/archive/PHASE_6_REFERENCE.md` or delete.
