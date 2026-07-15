<!--
Copyright (c) DCSV. All rights reserved.
-->

# public/

This tree is the **public** export surface. It is destined for **Apache-2.0** and is the only tree that may ship to the open remote.

## Contents

| Path | Role |
| --- | --- |
| [`packages/`](packages/README.md) | Open shared libraries (`dotnet/`, `typescript/`) |
| [`services/`](services/README.md) | Open product services root (empty) |
| [`contracts/`](contracts/README.md) | Public schemas and public values catalogs |
| [`tools/`](tools/README.md) | Public tooling and scripts |
| [`docs/adrs/`](docs/adrs/README.md) | Framework ADRs only (each file carries a **Visibility: PUBLIC** banner) |

## Export law

**Export = this tree only (`public/**`).** Nothing outside this directory is part of the open surface.

Public documentation and contracts never cite non-export monorepo paths or operator workspaces.
