<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.

# ADR-0026: Public/private monorepo layout + dual-repo cutover

- **Status**: Accepted
- **Date**: 2026-07-15
- **Deliverable**: 0032-oss-public-private

## Context

The D2 framework needs a physical layout that cleanly separates:

1. **Open surface** — libraries, contracts, tools, and framework ADRs that
   ship under Apache-2.0 and may be mirrored to an open remote.
2. **Closed product** — product hosts, product contracts, secrets tooling,
   operator process law, and product design docs that must never export.

Without a hard tree boundary, open packages risk depending on product
sources, and export automation risks mirroring closed IP.

## Decision

Adopt a **dual tree** inside the monorepo SoT:

| Tree | Role | License |
| --- | --- | --- |
| `public/**` | **Only** export surface | Apache-2.0 (`public/LICENSE`) |
| Monorepo root + `private/**` + `docs/dev/**` + `infra/**` | Never export | Proprietary (closed monorepo) |

### Layout summary (L1–L18)

1. **Export** = `public/**` only.
2. **Open packages** live under `public/packages/{dotnet,typescript}/`.
3. **Product hosts** live under `private/services/` (including the BFF).
4. **Public docs** = `public/README` + `LICENSE` + thin `CONTRIBUTING` +
   `public/docs/adrs/` (every ADR: **Visibility: PUBLIC** banner).
5. **Dual test suites**: public-only (`public/D2.Public.slnx`) + combined
   umbrella (root `D2.slnx` / private monorepo CI).
6. **Export is gated** (dry-run / dispatch / checklist) — not every push.
7. **Publish ownership**: real nuget.org/npmjs and GitHub Releases of
   public package IDs only on the open remote (`d2-public`). Private
   monorepo may pack + upload-artifact only for those IDs.
8. **Dual binaries (dev)**: private hosts ProjectReference public packages
   in monorepo; product release pin-mode is a documented future call-out.
9. **Codegen dual-root**: public packages generate only from
   `public/contracts`; private codegen emits only into `private/**`.
10. **Deliverable-complete** = monorepo wired (layout + dual suites +
    export dry-run path + law) — **not** that remotes already exist.
11. **Tools** live only under `public/tools/**` and `private/tools/**`.
12. **IP fence**: no product hosts, product catalogs, secrets tooling, or
    private-only contracts under `public/`.
13. **Contract split**: public values + shared `$schema` under public;
    product values under private when dual-values apply.
14. **Remote names**: open working remote `d2-public`; closed SoT
    `d2-private-worx`. Agents do **not** create remotes.
15. **Archive URL**: the historical product public URL may remain as an
    archive; live OSS = `d2-public`; live product = `d2-private-worx`.
16. **Licenses**: private ARR; public Apache-2.0.
17. **Empty dirs**: `.gitkeep` where needed for layout stability.
18. **Solutions**: `public/D2.Public.slnx` (public-only) + root `D2.slnx`
    (umbrella).

### Dependency direction

- **Allowed:** private → public (`ProjectReference` / package consume).
- **Forbidden:** public → private (csproj, pnpm, Docker product COPY,
  AdditionalFiles into `private/**`).

### Dual headers

- Files under `public/**`: Apache StyleCop form
  (`Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.`).
- Files under `private/**` and monorepo-root private KEEP: proprietary ARR.

### Brand

| Surface | Brand |
| --- | --- |
| Public KEEP / open packages | **D2** |
| Private product monorepo | **D2-WORX** |
| Public package ids | never contain `worx` |

### Schema hosts

Public contract `$id` / problem-type hosts use `*.d2.dcsv.io`
(e.g. `schemas.d2.dcsv.io`, `problems.d2.dcsv.io`).

## Consequences

- CI runs dual lanes (public-only + combined).
- Human operators follow an ordered cutover checklist (create remotes,
  first export, wire secrets, preserve product branches). Agents document
  only — they do not create remotes or org secrets.
- Public clone of this tree does not require private monorepo paths.
- Private monorepo operators run the combined suite for full product truth.

## Notes (private monorepo illustration — not required for public clone)

In the product monorepo that embeds this open tree, process law lives under
root `docs/dev/`, product ADRs under `private/docs/adrs/`, and product phase
design under `private/docs/v2/`. Those paths are outside the export surface.
