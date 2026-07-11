# Changelog — D2.Shared.EntityFrameworkCore.Postgres

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

- Renamed generated nested advisory-lock class `AdvisoryLocks.KeycustodianDb` →
  `AdvisoryLocks.D2Keycustodian` to match PostgreSQL database name `d2-keycustodian`
  (canonical `d2-{domain}` naming). Spec + PublicAPI updated in lockstep.

### Added

### Fixed
