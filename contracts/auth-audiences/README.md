<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/auth-audiences/`

Token-exchange target audience catalog — the per-service audience URLs used when the Edge mints scoped internal tokens via RFC 8693 exchange.

## Consumed by

- **.NET** — [`server/shared/dotnet/auth/audiences-source-gen/`](../../server/shared/dotnet/auth/audiences-source-gen/README.md) (Roslyn source-gen → `Audiences` constants in `D2.Shared.Auth.Abstractions`)
- **TypeSpec** — [`server/shared/typescript/typespec-decorators/`](../../server/shared/typescript/typespec-decorators/README.md) reads `audiences.spec.json` to validate `@d2Audience` decorator arguments at compile time

No `tools/ts-codegen` emitter consumes this catalog — the TS side reaches the audience names through the `@d2Audience` decorator rather than a generated const-object.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
