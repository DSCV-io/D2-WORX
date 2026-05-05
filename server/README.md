<!--
Copyright (c) DCSV. All rights reserved.
-->

# server/ — Trusted Code

> Parent: [`/`](../README.md)

All code that runs inside the trust boundary — .NET services, shared .NET libraries, SvelteKit BFF. The .NET solution at [`D2.slnx`](D2.slnx) is the entry point for `dotnet build` / `dotnet test`.

## Layout

| Path | What |
|---|---|
| [`shared/`](shared/README.md) | Shared libraries consumed by services — currently .NET only |
| [`services/`](services/README.md) | .NET service implementations (Edge gateway, audit, courier, notifications, files) |
| [`web/`](web/README.md) | SvelteKit Backend-for-Frontend (browser-side UI + SSR) |
| [`d2-version/`](d2-version/) | Version anchor csproj — drives `dotnet versionize` releases |
| [`D2.slnx`](D2.slnx) | .NET solution file — references every shared lib + service csproj |
| [`Directory.Build.props`](Directory.Build.props) | Solution-wide MSBuild defaults (target framework, lang version, StyleCop, treat-warnings-as-errors) |
| [`Directory.Packages.props`](Directory.Packages.props) | Centrally-managed NuGet package versions |
| [`stylecop.json`](stylecop.json) | StyleCop analyzer config (file-header format, naming) |
| [`global.json`](global.json) | Pinned .NET SDK version |
| [`NuGet.config`](NuGet.config) | NuGet feed config |

## Build + test

```bash
dotnet build server/D2.slnx              # full solution (every shared lib + service)
dotnet test server/shared/dotnet/tests   # shared-lib test suite
```

The web client builds independently:

```bash
cd server/web && pnpm exec svelte-check
```

## Conventions

- **Folder naming**: lowercase outer (`shared/`, `services/`, `dotnet/`, `web/`). PascalCase inside .NET projects (where Rider auto-creates folders from namespace operations).
- **One handler per file** under `Implementations/{TLC}/Handlers/{3LC}/` per the TLC convention in [`docs/PATTERNS.md`](../docs/PATTERNS.md).
- **Every project has a `README.md`** describing its own domain. Parent READMEs link down to children; child READMEs link back up.
