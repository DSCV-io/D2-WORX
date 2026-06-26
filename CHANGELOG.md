# Changelog

All notable changes to this project will be documented in this file.

This project follows [Semantic Versioning](https://semver.org/) and uses [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) to drive automated changelog generation via [versionize](https://github.com/versionize/versionize).

---

## [Unreleased]

The v2 architecture rewrite.

### Added

- v2 architectural plan covering the Edge unified gateway, .NET consolidation, Geo decomposition, RFC 8693 token exchange, OAuth client_credentials for service identity, security policy framework, adaptive authentication, SeaweedFS object storage, and Docker Swarm + Portainer for production
- Pattern-improvement guidance covering primary constructors for handlers, monadic D2Result composition, the multi-tier cache fetcher, auto-validation, flattened interface grouping, and per-error-code semantic checks
- Versioning policy covering proto, REST, NuGet, library, and product versioning, with conventional-commits-driven version bumping via versionize
- v1 codebase snapshot preserved as a frozen reference

### Changed

- Branch model: v1 is preserved as a frozen reference; active development is the v2 rewrite

---

## [0.1.0] - 2026-04-30

Initial v2 baseline. Reset point for the new architecture. v1 codebase preserved for historical reference.
