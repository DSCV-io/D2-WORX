<!--
Copyright (c) DCSV. All rights reserved.
-->

# D²-WORX

Microservices SaaS framework. C# 14 / .NET 10 backend, SvelteKit BFF (TypeScript 5.9 / Svelte 5). Pre-Alpha. PolyForm Strict license — reference implementation, non-commercial.

## Repo layout

| Path | What |
| --- | --- |
| [`public/`](public/README.md) | **Public export surface** (Apache-destined): [`packages/`](public/packages/README.md), [`contracts/`](public/contracts/README.md), [`tools/`](public/tools/README.md), [`docs/adrs/`](public/docs/adrs/README.md). Umbrella public solution: [`public/D2.Public.slnx`](public/D2.Public.slnx). |
| [`private/`](private/README.md) | **Never-export** product tree: [`services/`](private/services/README.md) (incl. SvelteKit BFF under `web/`), [`packages/`](private/packages/README.md) (product emit hosts), [`contracts/`](private/contracts/README.md), [`tools/`](private/tools/README.md), [`docs/`](private/docs/adrs/README.md) (product ADRs + [`v2/`](private/docs/v2/V2.md)). |
| [`docs/`](docs/README.md) | Monorepo-private KEEP — patterns, tests, process (`docs/dev/`), workflow rules. Not the product phase tracker (that is [`private/docs/v2/`](private/docs/v2/V2.md)). |
| [`infra/`](infra/README.md) | Deployment + observability — Docker Compose, per-service Dockerfiles, LGTM stack configs. |
| [`D2.slnx`](D2.slnx) | Umbrella .NET solution at monorepo root (public packages + private services/hosts). |

`secrets/` is gitignored + Claude-deny-ruled. `.env.local` is gitignored; `.env.local.example` + `.env.secrets.example` are committed templates.

## License

[PolyForm Strict License 1.0.0](https://polyformproject.org/licenses/strict/1.0.0). See [LICENSE.md](LICENSE.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
