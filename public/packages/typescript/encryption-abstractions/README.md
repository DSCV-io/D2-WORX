<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-encryption-abstractions

> Parent: [`public/packages/typescript/`](../README.md)

D2 encryption-domain identifiers + encryption-frame binary layout constants (symmetric version-1 AND sealed version-2). Mirrors .NET `DcsvIo.D2.Encryption.EncryptionDomains`, the `DcsvIo.D2.Encryption.EncryptionFrameLayout` byte-offset layout, and the `DcsvIo.D2.Encryption.SealedFrameLayout` sealed layout — all spec-driven.

## Public API

| Export                        | Source                         | Mirror                                              |
| ----------------------------- | ------------------------------ | --------------------------------------------------- |
| `EncryptionDomains`           | `encryption-domains.g.ts`      | `DcsvIo.D2.Encryption.EncryptionDomains`            |
| `EncryptionDomain`            | `encryption-domains.g.ts`      | n/a (TS-only union type)                            |
| `ALL_ENCRYPTION_DOMAINS`      | `encryption-domains.g.ts`      | `DcsvIo.D2.Encryption.EncryptionDomains.AllDomains` |
| `EncryptionFrame`             | `encryption-frame.g.ts`        | `DcsvIo.D2.Encryption.EncryptionFrameLayout`        |
| `EncryptionFrameField`        | `encryption-frame.g.ts`        | n/a (TS-only union type)                            |
| `ALL_ENCRYPTION_FRAME_FIELDS` | `encryption-frame.g.ts`        | n/a (TS-only enumeration)                           |
| `SealedFrame`                 | `encryption-frame-sealed.g.ts` | `DcsvIo.D2.Encryption.SealedFrameLayout`            |
| `SealedFrameField`            | `encryption-frame-sealed.g.ts` | n/a (TS-only union type)                            |
| `ALL_SEALED_FRAME_FIELDS`     | `encryption-frame-sealed.g.ts` | n/a (TS-only enumeration)                           |

## Codegen workflow

`prebuild` invokes `tools/ts-codegen/src/encryption-domains-emit.ts` AND `tools/ts-codegen/src/encryption-frame-emit.ts` AND `tools/ts-codegen/src/encryption-frame-sealed-emit.ts` before `tsc -b`. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

- `EncryptionDomains`: any TS code that needs to refer to a keyring domain identifier — ops tooling (`d2 keys`), TS-side encryption pipelines, RabbitMQ subscribers that route on domain. The `PLAINTEXT` sentinel is included as a closed-catalog entry so callers can distinguish "no encryption" from "encryption with the X domain."
- `EncryptionFrame`: TS-side reader for the on-wire symmetric encryption frame produced by `DcsvIo.D2.Encryption.EncryptedBodyComposer`. The frame layout is binary (`[version:1][kid_len:1][kid:UTF-8][nonce:12][ct+tag]`); this catalog exposes the field-byte-offsets and lengths a parser needs.
- `SealedFrame`: TS-side reader for the on-wire SEALED (version-2, asymmetric ECDH-ES hybrid) encryption frame (`[version:2][recipient_kid_len:1][recipient_kid:UTF-8][eph_pub_len:2 BE][eph_pub:SPKI][nonce:12][ct+tag]`). The leading version byte discriminates the two families; a reader dispatches on it and uses the matching catalog.

## Spec contracts

- `contracts/encryption-domains/encryption-domains.spec.json` — closed enum of domain identifiers (`audit` / `notifications` / `courier` + `plaintext` sentinel).
- `contracts/encryption-frame/encryption-frame.spec.json` — closed catalog of symmetric (version-1) frame field offsets + byte lengths.
- `contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json` — closed catalog of sealed (version-2) frame field offsets + byte lengths, including the 2-byte big-endian ephemeral-public-key length prefix + cap.

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.
