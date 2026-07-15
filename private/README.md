<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/

**Never-export** product tree for the **D2-WORX** monorepo. Holds closed product hosts, product contracts, private tools, product ADRs, and phase design.

## Contents

| Path | Role |
| --- | --- |
| [`packages/`](packages/README.md) | Private `DcsvIo.D2.Private.*` / extension hosts (see packages/README.md) |
| [`services/`](services/README.md) | Product services and BFF hosts |
| [`contracts/`](contracts/README.md) | Product values, private-only schemas, private halves of dual-values catalogs |
| [`tools/`](tools/README.md) | Secrets-touching scripts and monorepo-only tooling |
| [`docs/adrs/`](docs/adrs/README.md) | Product / host ADRs |
| [`docs/v2/`](docs/v2/README.md) | Product phase design (`V2.md` active tracking) |

## Law

- May cite public surfaces by id or public path.
- Never place product IP under `public/`.
- Export never includes this tree.
- Human remote cutover: monorepo-root `docs/dev/human-cutover-oss-public-private.md`.
