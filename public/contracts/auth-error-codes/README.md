<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/auth-error-codes/`

Auth error-code catalog — the closed set of authentication and authorization failure codes (bearer missing, JWT expired, scope insufficient, session revoked, etc.) with their HTTP status, error category, and user-message key.

## Consumed by

- **.NET** — [`public/packages/dotnet/auth/error-codes-source-gen/`](../../public/packages/dotnet/auth/error-codes-source-gen/README.md) (Roslyn source-gen → `AuthErrorCodes` constants + `AuthFailures` typed `D2Result` factories in `DcsvIo.D2.Auth.Core`)
- **TypeScript** — [`tools/ts-codegen` › `error-codes-emit.ts`](../../tools/ts-codegen/README.md) (→ `AuthErrorCodes` constants + `AuthFailures.*` factories in `@dcsv-io/d2-auth-abstractions`)
- **TypeSpec** — [`public/packages/typescript/typespec-decorators/`](../../public/packages/typescript/typespec-decorators/README.md) reads every `*-error-codes.spec.json` to validate decorator arguments at compile time

This catalog is also merged into the cross-service registry — see [`contracts/error-codes/`](../error-codes/README.md).

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
