# Changelog — DcsvIo.D2.EntityFrameworkCore.Postgres

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

- **Removed** domain lock-key catalog (`AdvisoryLocks` / `AdvisoryLocks.D2Keycustodian.*`)
  from this package. Shared Postgres owns mechanism only (`PgAdvisoryLock`,
  `AdvisoryLockMigrator<TContext>`, design-time factory base, `NpgsqlContextDefaults`).
  Consumers must take domain constants from the owning module
  (`DcsvIo.D2.Private.Edge.KeyCustodian.Infra.AdvisoryLocks.D2Keycustodian.{MIGRATOR,ROTATION,CA_SEED}`).
  Central fleet catalog SoT remains `contracts/advisory-locks/`;
  `DcsvIo.D2.AdvisoryLocks.SourceGen` emits into the owning-module assembly.
- Renamed generated nested advisory-lock class `AdvisoryLocks.KeycustodianDb` →
  `AdvisoryLocks.D2Keycustodian` to match PostgreSQL database name `d2-keycustodian`
  (canonical `d2-{domain}` naming). Spec + PublicAPI updated in lockstep.

### Added

### Fixed
