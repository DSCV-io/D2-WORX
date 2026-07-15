<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/auth-protocol-audiences/`

Protocol audience catalog — the two fixed internal receive audiences (`d2.internal` universal forward-unchanged audience, `d2-edge` Edge self-audience) that distinguish internal-transaction tokens from user-facing tokens.

## Consumed by

- **.NET** — [`public/packages/dotnet/auth/protocol-audiences-source-gen/`](../../public/packages/dotnet/auth/protocol-audiences-source-gen/) (Roslyn source-gen → `WellKnownAudiences` constants in `D2.Shared.Auth.Abstractions`; no README)
- **TypeScript** — [`tools/ts-codegen` › `protocol-audiences-emit.ts`](../../tools/ts-codegen/README.md) (→ `ProtocolAudiences` const-object in `@d2/auth-abstractions`)
- **TypeSpec** — [`public/packages/typescript/typespec-decorators/`](../../public/packages/typescript/typespec-decorators/README.md) reads `protocol-audiences.spec.json` to validate `@d2Audience` decorator arguments at compile time

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
