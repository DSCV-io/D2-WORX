<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/encryption-domains/`

Encryption domain registry — the closed set of named keyring domains (`audit`, `notifications`, `courier`, and the `plaintext` sentinel) used to identify which keyring encrypts a given RabbitMQ payload.

## Consumed by

- **.NET** — [`server/shared/dotnet/encryption/domains-source-gen/`](../../server/shared/dotnet/encryption/domains-source-gen/README.md) (Roslyn source-gen → `EncryptionDomains` constants in `D2.Shared.Encryption`)
- **TypeScript** — [`tools/ts-codegen` › `encryption-domains-emit.ts`](../../tools/ts-codegen/README.md) (→ matching `EncryptionDomains` const-object in `@d2/encryption-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
