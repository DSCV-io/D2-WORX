# Changelog — DcsvIo.D2.Context.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- `ISystemWorkScope` / `ISystemWorkScopeFactory` + `AddD2SystemWorkPlane()` —
  platform System work entry. Hosted/background authority-bearing work opens a
  DI scope with `RequestOrigin.System` already established via the factory;
  modules must not re-register `IRequestContext`.
- `[MustDisposeResource(true)]` on `ISystemWorkScopeFactory.BeginAsync` (and
  concrete scope) so inspectcode flags undisposed System work scopes.

### Fixed
