<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Contributing to D2 (open surface)

Thanks for your interest in contributing to the **D2** open framework
under this tree (`public/`).

## License

Contributions to this tree are accepted under the
**Apache License, Version 2.0** ([LICENSE](LICENSE)).

## Scope

- Work in this repository is limited to the open surface: packages,
  contracts, tools, and framework ADRs under `public/`.
- Product hosts, private contracts, and monorepo process law live outside
  this export surface and are out of scope for OSS-only PRs.

## Commits

Prefer [Conventional Commits](https://www.conventionalcommits.org/)
(`feat`, `fix`, `docs`, `chore`, …). Public tooling may enforce commit
format when hooks are present in the clone.

## Pull requests

- Keep PRs focused on one concern.
- Ensure public-only builds pass:

  ```bash
  dotnet build public/D2.Public.slnx
  ```

- Do not add product IP, private paths as clone requirements, or secrets.

## Architecture decisions

Framework ADRs live under [docs/adrs/](docs/adrs/README.md). Each ADR
carries a **Visibility: PUBLIC** banner.

## Issues & remote

Issues and PRs for the open surface belong on the **`d2-public`** remote
when that remote is the live OSS home for this tree.
