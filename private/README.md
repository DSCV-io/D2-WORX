<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/

**Never-export** product tree. Holds closed product hosts, product contracts, private tools, product ADRs, and phase design.

## Contents

| Path | Role |
| --- | --- |
| [`packages/`](packages/README.md) | Private `D2.Shared.*.Extensions` dual-target hosts (see packages/README.md) |
| [`services/`](services/README.md) | Product services and BFF hosts |
| [`contracts/`](contracts/README.md) | Product values, private-only schemas, private halves of dual-values catalogs |
| [`tools/`](tools/README.md) | Secrets-touching scripts and monorepo-only tooling |
| [`docs/adrs/`](docs/adrs/README.md) | Product / host ADRs |
| [`docs/v2/`](docs/v2/README.md) | Product phase design |

## Law

- May cite public surfaces by id or public path.
- Never place product IP under `public/`.
- Export never includes this tree.
