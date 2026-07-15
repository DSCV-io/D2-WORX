<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.EncryptionFrame.SourceGen

> Parent: [`public/packages/dotnet/`](../README.md)

**Input contracts:** [`contracts/encryption-frame/`](../../../../../contracts/encryption-frame/README.md) (symmetric, version 1) + [`contracts/encryption-frame-sealed/`](../../../../../contracts/encryption-frame-sealed/README.md) (sealed, version 2)

Roslyn incremental source generators (two arms in one analyzer project) that emit the closed catalogs of binary-layout offsets, byte lengths, and constraints for the D2 on-wire encryption frames: `EncryptionFrameLayout` from `contracts/encryption-frame/encryption-frame.spec.json` (the symmetric version-1 frame) and `SealedFrameLayout` from the sibling `contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json` (the sealed version-2 frame). The two generators filter on different spec file names, so neither ever reads the other's catalog and the version-1 artifacts stay byte-identical while the sealed layout evolves.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

## What this emits

When the consuming assembly is `D2.Shared.Encryption`:

- `EncryptionFrameGenerator` emits `EncryptionFrameLayout.g.cs` containing a `CURRENT_VERSION` constant, per-field `*_OFFSET` + `*_LENGTH` constants, and frame-level constraint constants (`CONSTRAINT_MIN_KID_LENGTH`, `CONSTRAINT_MAX_KID_LENGTH`, `CONSTRAINT_NONCE_LENGTH`, `CONSTRAINT_TAG_LENGTH`, `CONSTRAINT_MIN_FRAME_SIZE`). The `CONSTRAINT_` prefix disambiguates the frame-level constraint constants from the per-field `*_LENGTH` constants (e.g. `NONCE_LENGTH` is the per-field byte length declared for the `NONCE` field, and `CONSTRAINT_NONCE_LENGTH` is the frame-level AES-GCM-spec value the field MUST equal).
- `SealedFrameGenerator` emits `SealedFrameLayout.g.cs` — same constants-only shape plus the sealed-only constraints `CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE` (the 2-byte big-endian length-prefix width in front of the ephemeral public key) and `CONSTRAINT_MAX_EPH_PUB_LENGTH` (the allocation cap on the declared key length).

## The `variable_binary_u16be` field kind (sealed spec only)

The sealed schema adds a field kind the symmetric catalog does not need: **`variable_binary_u16be`** — raw (non-UTF-8) binary bytes whose length is declared by the immediately preceding 2-byte **big-endian** unsigned-integer length field. The sealed frame's ephemeral public key uses it (a P-256 SubjectPublicKeyInfo is ~91 bytes, beyond the 1-byte prefix the kid uses). The emitter enforces the structural rule at build time: a `variable_binary_u16be` field that is not immediately preceded by a `byte_fixed` field of exactly the declared prefix width fails with `D2EF012` — the codec can never meet a spec it cannot parse.

## Why spec-drive this

The TS-side `@d2/encryption-abstractions` package exposes the same binary frame-layout constants as the .NET codecs. With one spec catalog per frame version driving both sides, any TS reader and the .NET codec reference identical byte offsets and lengths; neither side can maintain a parallel constant catalog that would drift on the next version bump.

## Cross-language parity

The SAME specs drive `@d2/encryption-abstractions` via `tools/ts-codegen/src/encryption-frame-emit.ts` (v1) and `tools/ts-codegen/src/encryption-frame-sealed-emit.ts` (v2 sealed). Both sides reference identical offsets + lengths; cross-language wire drift is structurally impossible.

## Diagnostics

| ID        | Title                                                                       | Severity |
| --------- | --------------------------------------------------------------------------- | -------- |
| `D2EF001` | Encryption-frame spec is malformed                                          | Error    |
| `D2EF002` | Duplicate field constName                                                   | Error    |
| `D2EF003` | Fixed-offset fields overlap                                                 | Error    |
| `D2EF004` | Field has invalid length                                                    | Error    |
| `D2EF005` | Spec version is invalid                                                     | Error    |
| `D2EF006` | Sealed encryption-frame spec is malformed                                   | Error    |
| `D2EF007` | Duplicate sealed field constName                                            | Error    |
| `D2EF008` | Sealed fixed-offset fields overlap                                          | Error    |
| `D2EF009` | Sealed field has invalid length                                             | Error    |
| `D2EF010` | Sealed spec version is invalid (must be ≥ 2 — version 1 is the symmetric frame) | Error    |
| `D2EF011` | Sealed field kind is unknown                                                | Error    |
| `D2EF012` | `variable_binary_u16be` field lacks its preceding 2-byte length prefix      | Error    |
