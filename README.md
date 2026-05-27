<!--
Copyright (c) DCSV. All rights reserved.
-->

# D²-WORX

Microservices SaaS framework. C# 14 / .NET 10 backend, SvelteKit BFF (TypeScript 5.9 / Svelte 5). Pre-Alpha. PolyForm Strict license — reference implementation, non-commercial.

## Repo layout

| Path                          | What                                                                                                                                                                                                                          |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`server/`](server/README.md) | All trusted code — .NET services, shared .NET libraries, SvelteKit BFF                                                                                                                                                        |
| [`docs/`](docs/README.md)     | Cross-cutting documentation — patterns, test discipline, cross-language parity, spec-driven codegen, workflow + rules catalog                                                                                                 |
| [`infra/`](infra/README.md)   | Deployment + observability — Docker Compose, per-service Dockerfiles, LGTM stack configs                                                                                                                                      |
| [`tools/`](tools/README.md)   | Dev tooling — scripts + small utilities                                                                                                                                                                                       |
| `contracts/`                  | Source-of-truth contract files — proto definitions, i18n message catalogs, JSON schemas + spec files for codegen. Includes [`contracts/geo/`](contracts/geo/README.md) (geo reference-data spec catalog consumed by codegen). |

`secrets/` is gitignored + Claude-deny-ruled. `.env.local` is gitignored; `.env.local.example` + `.env.secrets.example` are committed templates.

## License

[PolyForm Strict License 1.0.0](https://polyformproject.org/licenses/strict/1.0.0). See [LICENSE.md](LICENSE.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
