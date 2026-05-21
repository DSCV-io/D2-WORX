<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.EncryptionFrame.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits `EncryptionFrameLayout` — the closed catalog of binary-layout offsets, byte lengths, and constraints for the D2 on-wire encryption frame — from `contracts/encryption-frame/encryption-frame.spec.json`.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

## What this emits

When the consuming assembly is `D2.Shared.Encryption`, the generator emits `EncryptionFrameLayout.g.cs` containing a `CURRENT_VERSION` constant, per-field `*_OFFSET` + `*_LENGTH` constants, and frame-level constraint constants (`CONSTRAINT_MIN_KID_LENGTH`, `CONSTRAINT_MAX_KID_LENGTH`, `CONSTRAINT_NONCE_LENGTH`, `CONSTRAINT_TAG_LENGTH`, `CONSTRAINT_MIN_FRAME_SIZE`). The `CONSTRAINT_` prefix disambiguates the frame-level constraint constants from the per-field `*_LENGTH` constants (e.g. `NONCE_LENGTH` is the per-field byte length declared for the `NONCE` field, and `CONSTRAINT_NONCE_LENGTH` is the frame-level AES-GCM-spec value the field MUST equal).

## Why spec-drive this

The TS-side `@d2/encryption-abstractions` package exposes the same binary frame-layout constants as the .NET encoder. With one spec catalog driving both sides, any TS reader and the .NET encoder reference identical byte offsets and lengths; neither side can maintain a parallel constant catalog that would drift on the next version bump.

## Cross-language parity

The SAME spec drives `@d2/encryption-abstractions` via `tools/ts-codegen/src/encryption-frame-emit.ts`. Both sides reference identical offsets + lengths; cross-language wire drift is structurally impossible.

## Diagnostics

| ID | Title | Severity |
|---|---|---|
| `D2EF001` | Encryption-frame spec is malformed | Error |
| `D2EF002` | Duplicate field constName | Error |
| `D2EF003` | Fixed-offset fields overlap | Error |
| `D2EF004` | Field has invalid length | Error |
| `D2EF005` | Spec version is invalid | Error |
