<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/jwt-claims/`

JWT claim-name catalog — the closed set of standard and `d2_`-prefixed custom JWT claim names parsed by the auth middleware into `IAuthContext` properties.

## Consumed by

- **.NET** — [`server/shared/dotnet/auth/jwt-claims-source-gen/`](../../server/shared/dotnet/auth/jwt-claims-source-gen/README.md) (Roslyn source-gen → `JwtClaimTypes` constants in `D2.Shared.Auth.Abstractions`)
- **TypeScript** — [`tools/ts-codegen` › `jwt-claims-emit.ts`](../../tools/ts-codegen/README.md) (→ matching `JwtClaimTypes` constants + the `JwtPayload` typed-shape interface in `@d2/auth-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
