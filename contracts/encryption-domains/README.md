<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/encryption-domains/`

Encryption domain registry — the closed set of named keyring domains (`audit`, `notifications`, `courier`, and the `plaintext` sentinel) used to identify which keyring encrypts a given RabbitMQ payload.

## Per-domain mode

Each domain optionally declares a `mode`:

- **`symmetric`** (the default when the field is absent — strict back-compat) — a shared keyring AES-256-GCM (version-1 frame); every grant-holder both encrypts and decrypts.
- **`sealed`** — per-consumer-service ephemeral-static ECDH (version-2 frame); producers seal to the recipient service's public key and only that one service opens. A sealed domain MUST declare `consumerService` (the single decryptor's ServiceId, `[a-z0-9-]{1,64}`); a non-sealed domain MUST NOT. `audit` / `notifications` / `courier` are sealed, each to its own service; the `plaintext` sentinel carries no mode.

Both emitters fail the build on an inconsistent `mode` / `consumerService` pair.

## Consumed by

- **.NET** — [`server/shared/dotnet/encryption/domains-source-gen/`](../../server/shared/dotnet/encryption/domains-source-gen/README.md) (Roslyn source-gen → `EncryptionDomains` constants in `D2.Shared.Encryption`)
- **TypeScript** — [`tools/ts-codegen` › `encryption-domains-emit.ts`](../../tools/ts-codegen/README.md) (→ matching `EncryptionDomains` const-object in `@d2/encryption-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
