<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2-WORX

**D2-WORX** is the product monorepo (microservices SaaS). It hosts open **D2**
framework libraries under `public/` and closed product hosts under `private/`.

C# 14 / .NET 10 backend, SvelteKit BFF (TypeScript 5.9 / Svelte 5). Pre-Alpha.

## Repo layout

| Path | What |
| --- | --- |
| [`public/`](public/README.md) | **Open export surface (Apache-2.0):** packages, contracts, tools, framework ADRs. Public solution: [`public/D2.Public.slnx`](public/D2.Public.slnx). |
| [`private/`](private/README.md) | **Never-export** product tree: services (incl. SvelteKit BFF), private packages/contracts/tools, product ADRs + [`v2/`](private/docs/v2/V2.md). |
| [`docs/`](docs/README.md) | Monorepo-private KEEP — patterns, tests, process (`docs/dev/`). Not exported. |
| [`infra/`](infra/README.md) | Deployment + observability (Compose, Dockerfiles, LGTM). |
| [`D2.slnx`](D2.slnx) | Umbrella .NET solution (public packages + private services/hosts). |

`secrets/` is gitignored + agent-deny-ruled. `.env.local` is gitignored; `.env.local.example` + `.env.secrets.example` are committed templates.

## Brand

| Surface | Brand |
| --- | --- |
| Product monorepo / private docs | **D2-WORX** |
| Open libraries / public docs | **D2** (`DcsvIo.D2.*`, `@dcsv-io/d2-*`) |

## License

- **Monorepo outside `public/`:** proprietary **All rights reserved**. See [LICENSE.md](LICENSE.md).
- **Open surface:** **Apache-2.0** under [`public/LICENSE`](public/LICENSE).

## Contributing

Monorepo contribution notes: [CONTRIBUTING.md](CONTRIBUTING.md).
OSS-only contributions to the open surface: [`public/CONTRIBUTING.md`](public/CONTRIBUTING.md).

## Operator cutover (remotes)

Creating and wiring `d2-public` / `d2-private-worx` remotes is a **human** checklist —
agents do not create remotes or org secrets. See
[docs/dev/human-cutover-oss-public-private.md](docs/dev/human-cutover-oss-public-private.md).
