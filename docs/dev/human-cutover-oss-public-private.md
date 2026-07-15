<!--
Copyright (c) DCSV. All rights reserved.
-->

# Human cutover — OSS public/private remotes

Operator checklist for dual-remote cutover after the monorepo is **wired**
(layout + dual suites + export dry-run path + law).

> **Hard law**
>
> - **Agents do not create remotes, org secrets, or branch protection.**
> - **Deliverable-complete ≠ cutover-complete.** Wiring on the monorepo can
>   finish while remotes still do not exist.
> - **Preserve Auth Core / product history** — never force-lose live tips.

| # | Who | What |
| --- | --- | --- |
| **H0** | Agents/dev | Wiring complete on monorepo work branch (`n/oss` → PR target **`nova`**, not silent `main` archive). |
| **H0.5** | Human | Backup before irreversible ops (if not already). |
| **H1** | Human | Create **`d2-public`** remote (Apache-2.0 open surface). |
| **H2** | Human | Create **`d2-private-worx`** remote (ARR closed SoT). |
| **H3** | Operator | First export **dry-run** (no push) — allowlist `public/**` only. |
| **H4** | Human | First **real** export of `public/**` only → `d2-public`. |
| **H5** | Human | Wire `d2-public` CI + release secrets + branch protection. |
| **H6** | Human | Wire `d2-private-worx` combined CI; **no** public-ID publish secrets on private. |
| **H7** | Human | Preserve `n/auth-core` (and other live product tips) — do not drop history at archive. |
| **H8** | Human | Merge layout into private SoT main / day-to-day remote. |
| **H9** | Human | Park/archive **D2-WORX** URL: README → archived; live OSS = `d2-public`; live product = `d2-private-worx`. **Optional future:** empty commit + fat showcase README on the archive URL (not an agent step). |

## Publish ownership reminder

| Remote | May |
| --- | --- |
| `d2-public` | Real nuget.org / npmjs publish + GitHub Release of public package IDs |
| `d2-private-worx` | Combined CI; pack + upload-artifact for public IDs; **hard-fail** real publish / GH-Release of those IDs |

## Versioning strategy (locked at SHIP 0032 — 2026-07-15)

Consumable libs (`DcsvIo.D2.*`, `@dcsv-io/d2-*`) use **per-package semver** via
`public/tools/release-runner` (§26.19–§26.20). Deployable services are **not** on this line.

| Phase | Policy |
| --- | --- |
| **Development on monorepo** | Keep **fingerprints / PublicAPI / api.md** current (pre-commit). **Do not** hand-bump `<Version>` / `version` / CHANGELOG on every PR. |
| **First registry cut (`d2-public`)** | Publish current consumables at **`0.1.0`** (pre-stable). No mass pre-bump required. Typespec emitters currently `0.0.1` — leave or align to `0.1.0` at first public cut (operator choice). |
| **Later cuts** | `pnpm --filter release-runner exec tsx src/cli.ts --against <previous-release-tip> --apply` → pack → publish from **`d2-public` only**. Footers escalate only. Dependent PATCH propagation default-ON. |
| **Private monorepo** | `release-libs.yml` = pack + artifact only — **never** nuget.org / npmjs / GH Release of public IDs. |
| **Graduation to 1.0.0** | Deliberate `--graduate` only — not automatic. |

Mechanism readiness (monorepo): release-runner unit suite (**483** tests); CI versioning lanes;
pre-commit `check-baselines`. Public-remote pack+push workflow is installed at **H5** using
exportable `public/tools/**` (do not invent a second version engine).

## Related

- Layout ADR: [`public/docs/adrs/0026-public-private-monorepo-layout.md`](../../public/docs/adrs/0026-public-private-monorepo-layout.md)
- Dual-suite commands: [COMMANDS.md](../COMMANDS.md)
- Law: rules §8.8–§8.10, §9.48–§9.49, §11.46–§11.47, §26.25–§26.26, §7.7a, §26.19–§26.20
