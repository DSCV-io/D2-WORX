<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/advisory-locks/`

PostgreSQL advisory-lock key catalog for coordinated startup and rotation operations.
Central fleet SoT — generator enforces per-database uniqueness at build time.

## Consumed by

- **.NET** — [`public/packages/dotnet/entity-framework-core/locks-source-gen/`](../../public/packages/dotnet/entity-framework-core/locks-source-gen/README.md) (Roslyn source-gen → `AdvisoryLocks` constants emitted into the **owning-module assembly**; currently `D2.Edge.KeyCustodian.Infra`). Shared Postgres supplies mechanism only (`PgAdvisoryLock` / migrator).

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
