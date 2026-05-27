<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/encryption-abstractions

> Parent: [`server/shared/typescript/`](../README.md)

D2 encryption-domain identifiers + encryption-frame binary layout constants. Mirrors .NET `D2.Shared.Encryption.EncryptionDomains` and the `D2.Shared.Encryption.EncryptionFrame` byte-offset layout — both spec-driven.

## Public API

| Export                        | Source                    | Mirror                                              |
| ----------------------------- | ------------------------- | --------------------------------------------------- |
| `EncryptionDomains`           | `encryption-domains.g.ts` | `D2.Shared.Encryption.EncryptionDomains`            |
| `EncryptionDomain`            | `encryption-domains.g.ts` | n/a (TS-only union type)                            |
| `ALL_ENCRYPTION_DOMAINS`      | `encryption-domains.g.ts` | `D2.Shared.Encryption.EncryptionDomains.AllDomains` |
| `EncryptionFrame`             | `encryption-frame.g.ts`   | `D2.Shared.Encryption.EncryptionFrame` (offsets)    |
| `EncryptionFrameField`        | `encryption-frame.g.ts`   | n/a (TS-only union type)                            |
| `ALL_ENCRYPTION_FRAME_FIELDS` | `encryption-frame.g.ts`   | n/a (TS-only enumeration)                           |

## Codegen workflow

`prebuild` invokes `tools/ts-codegen/src/encryption-domains-emit.ts` AND `tools/ts-codegen/src/encryption-frame-emit.ts` before `tsc -b`. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

- `EncryptionDomains`: any TS code that needs to refer to a keyring domain identifier — ops tooling (`d2 keys`), TS-side encryption pipelines, RabbitMQ subscribers that route on domain. The `PLAINTEXT` sentinel is included as a closed-catalog entry so callers can distinguish "no encryption" from "encryption with the X domain."
- `EncryptionFrame`: TS-side reader for the on-wire encryption frame produced by `D2.Shared.Encryption.EncryptedBodyComposer`. The frame layout is binary (`[version:1][kid_len:1][kid:UTF-8][nonce:12][ct+tag]`); this catalog exposes the field-byte-offsets and lengths a parser needs.

## Spec contracts

- `contracts/encryption-domains/encryption-domains.spec.json` — closed enum of domain identifiers (`audit` / `notifications` / `courier` + `plaintext` sentinel).
- `contracts/encryption-frame/encryption-frame.spec.json` — closed catalog of frame field offsets + byte lengths.

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.
