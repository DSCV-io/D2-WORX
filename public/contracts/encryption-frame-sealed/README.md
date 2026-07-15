<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/encryption-frame-sealed/`

Sealed encryption frame binary layout spec — the byte-offset positions and field lengths of the on-wire **sealed** (asymmetric, ECDH-ES hybrid) encryption envelope: version byte, recipient key-id length prefix + key id, 2-byte big-endian ephemeral-public-key length prefix + ephemeral P-256 public key (SPKI DER), AES-GCM nonce, ciphertext + auth tag.

Sibling of [`contracts/encryption-frame/`](../encryption-frame/README.md) (the symmetric version-1 frame). The two catalogs deliberately live in separate spec files so the version-1 spec and its generated artifacts stay byte-identical while the sealed layout evolves — the leading version byte (`1` symmetric, `2` sealed) is the wire discriminator, and each decoder hard-rejects the other family's version.

The `EPH_PUB` field introduces the `variable_binary_u16be` field kind: raw (non-UTF-8) binary bytes whose length is declared by the immediately preceding 2-byte **big-endian** unsigned-integer length field. A P-256 SubjectPublicKeyInfo is ~91 bytes, beyond the 1-byte length prefix the symmetric frame's kid uses; the `maxEphPubLength` constraint caps the declared length so an attacker-controlled prefix can never force a large allocation.

## Consumed by

- **.NET** — [`public/packages/dotnet/encryption/frame-source-gen/`](../../public/packages/dotnet/encryption/frame-source-gen/README.md) (Roslyn source-gen → `SealedFrameLayout` byte-offset + length constants in `D2.Shared.Encryption`)
- **TypeScript** — [`tools/ts-codegen` › `encryption-frame-sealed-emit.ts`](../../tools/ts-codegen/README.md) (→ matching field-offset + byte-length constants in `@d2/encryption-abstractions` for ops tooling and TS frame readers)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- Cross-language parity rows: [docs/PARITY.md](../../docs/PARITY.md)
- All contracts: [contracts catalog](../README.md)
