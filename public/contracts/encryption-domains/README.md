<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/encryption-domains/`

Encryption domain registry — the closed set of public keyring domains (`plaintext` sentinel + optional framework fixture sealed domain). Product sealed domains (`audit`, `notifications`, `courier`) live under `private/contracts/encryption-domains/` used to identify which keyring encrypts a given RabbitMQ payload.

## Per-domain mode

Each domain optionally declares a `mode`:

- **`symmetric`** (the default when the field is absent — strict back-compat) — a shared keyring AES-256-GCM (version-1 frame); every grant-holder both encrypts and decrypts.
- **`sealed`** — per-consumer-service ephemeral-static ECDH (version-2 frame); producers seal to the recipient service's public key and only that one service opens. A sealed domain MUST declare `consumerService` (the single decryptor's ServiceId, `[a-z0-9-]{1,64}`); a non-sealed domain MUST NOT. Product sealed domains (`audit` / `notifications` / `courier`) live under private dual-values; the public half carries the `plaintext` sentinel (no mode) plus a framework fixture sealed domain for Shared.Tests.

Both emitters fail the build on an inconsistent `mode` / `consumerService` pair.

## Consumed by

- **.NET** — [`public/packages/dotnet/encryption/domains-source-gen/`](../../public/packages/dotnet/encryption/domains-source-gen/README.md) (Roslyn source-gen → `EncryptionDomains` constants in `DcsvIo.D2.Encryption`)
- **TypeScript** — [`public/tools/ts-codegen` › `encryption-domains-emit.ts`](../../tools/ts-codegen/README.md) (→ matching `EncryptionDomains` const-object in `@dcsv-io/d2-encryption-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
