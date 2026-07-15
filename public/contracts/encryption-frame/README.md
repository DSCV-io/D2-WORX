<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/encryption-frame/`

Encryption frame binary layout spec — the byte-offset positions and field lengths of the on-wire encryption envelope (version byte, domain identifier, key-id length prefix, IV, ciphertext, auth tag).

## Consumed by

- **.NET** — [`public/packages/dotnet/encryption/frame-source-gen/`](../../public/packages/dotnet/encryption/frame-source-gen/README.md) (Roslyn source-gen → `EncryptionFrameLayout` byte-offset + length constants in `DcsvIo.D2.Encryption`)
- **TypeScript** — [`tools/ts-codegen` › `encryption-frame-emit.ts`](../../tools/ts-codegen/README.md) (→ matching field-offset + byte-length constants in `@dcsv-io/d2-encryption-abstractions` for ops tooling and TS frame readers)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
