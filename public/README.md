<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# D2 — open surface

**D2** is an open framework of shared libraries, contracts, and tools
for building production .NET and TypeScript services. This tree is the
entire open export surface and is licensed under **Apache License 2.0**
([LICENSE](LICENSE)).

Package identity:

| Ecosystem | Form |
| --- | --- |
| NuGet | `DcsvIo.D2.<Rest>` |
| npm | `@dcsv-io/d2-*` |
| Org | `dcsv-io` |

## Tree

| Path | Role |
| --- | --- |
| [`packages/`](packages/README.md) | Open shared libraries (`dotnet/`, `typescript/`) |
| [`contracts/`](contracts/README.md) | Public schemas and public values catalogs |
| [`tools/`](tools/README.md) | Public tooling and scripts |
| [`docs/adrs/`](docs/adrs/README.md) | Framework ADRs (**Visibility: PUBLIC** on every file) |
| [`services/`](services/README.md) | Open product services root (empty until first open service) |
| [`D2.Public.slnx`](D2.Public.slnx) | Public-only .NET solution |

## Build & test (public-only)

```bash
dotnet build public/D2.Public.slnx
dotnet test public/D2.Public.slnx
```

Public TypeScript packages use the monorepo `pnpm` workspace filters for
`public/packages/typescript/**` (see package READMEs). The public-only
suite must not require private product sources.

## Export law

**Export = this tree only (`public/**`).** This repository surface is the
open framework. Product monorepos that consume these packages are separate;
they are not required to clone or run anything outside `public/`.

Public documentation and contracts never require non-export operator paths,
product hosts, or closed runbooks.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
