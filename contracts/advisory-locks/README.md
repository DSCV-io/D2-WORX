<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/advisory-locks/`

PostgreSQL advisory-lock key catalog for coordinated startup and rotation operations.

## Consumed by

- **.NET** — [`server/shared/dotnet/entity-framework-core/locks-source-gen/`](../../server/shared/dotnet/entity-framework-core/locks-source-gen/README.md) (Roslyn source-gen → `AdvisoryLockKeys` constants in `D2.Shared.EntityFrameworkCore`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
