<!--
Copyright (c) DCSV. All rights reserved.
-->

# 0032 — OSS public/private layout + wiring (SHIP snapshot)

**Shipped:** 2026-07-15  
**Branch tip at SHIP:** `5019669d8` on `n/oss`  
**PR:** https://github.com/DCSV-io/D2-WORX/pull/57 → `nova`  
**Local journals:** `docs/wip/0032-oss-public-private/` (gitignored; not deleted)

This file is the committed post-SHIP snapshot of the deliverable root README.

---
<!--
Copyright (c) DCSV. All rights reserved.
Local PLAN workspace (gitignored). Not committed until SHIP snapshot.
-->

# 0032 â€” OSS public/private layout + wiring

| | |
| --- | --- |
| **Status** | **SHIPPED 2026-07-15** Â· FR_FULL CLEAN R4 Â· steps 01â€“07 CLEAN Â· tip `5019669d8` Â· PR #57 â†’ `nova` Â· snapshot `docs/dev/deliverables/0032-oss-public-private.md` |
| **Branch** | **`n/oss`** (from `n/auth-core` @ `4995b4bc`) â€” OSS structure only; Auth Core product continues on `n/auth-core` |
| **Type** | Structural reorg + CI/export gates + law (multi-step product deliverable) |
| **FR mode** | **FR_FULL** (layout/IP/CI product surface â€” full K=7 at deliverable scope + own FR journal) |
| **Plan-Audit open** | **Product full K=7** (not pure-meta Skip/Y-only) before first Implementer |
| **Plan-Audit results home** | Sibling dirs per round under `docs/wip/0032-oss-public-private/`: **`plan-audit-r1/`**, **`plan-audit-r2/`**, **`plan-audit-r3/`** (partials + aggregates/fix-logs), **`plan-audit-rN/`** for later rounds. Step journals only after Plan READY. |
| **Deliverable complete =** | Monorepo **wired** (layout + dual suites + export dry-run path + law) â€” **not** that remotes already exist |
| **Out of scope (auto)** | Implementing Auth Core product features; silent archive of D2-WORX without human checklist; silent publish to nuget.org/npmjs; **dual-binary pin-mode in Docker/release images** (L8 release-pin is documented call-out only this deliverable â€” see [L8 / dual binaries](#l8-dual-binaries--m10)) |

---

## Goal

Restructure the monorepo so **Apache-2.0-destined open surface** lives exclusively under `public/` and **closed product/IP** lives under `private/`, with MSBuild/pnpm/codegen/CI wired so:

1. A **public-only** build+test suite is mirror-identical to what the future `d2-public` remote will run (export gate).
2. A **combined** suite (public + private) is the private monorepo CI truth.
3. **Export** is a **gated** dry-run / checklist path (not silent every push).
4. Public package **release ownership** is public-repo-only; private must not publish public package IDs.
5. Codegen respects tree law: public packages generate only from `public/contracts`; private codegen emits only into `private/**`.

**Success is â€œeverything is wired upâ€ on this monorepo.** Creating remotes and archiving D2-WORX are **ordered human gates** listed below â€” required in the plan, not required for deliverable-complete on `n/oss`.

---

## End-state architecture (locked)

```
<monorepo root>                 # private SoT (d2-private-worx) â€” NOT exported
  README.md                     # PRIVATE map â†’ public/ + private/ + docs/dev (agent/human DX)
  LICENSE.md                    # All rights reserved
  AGENTS.md, CLAUDE.md          # agent law (private)
  D2.slnx                       # UMBRELLA
  Directory.Packages.props / NuGet.config / global.json / stylecop.json
  package.json / pnpm-workspace.yaml / pnpm-lock.yaml
  .github/                      # private monorepo CI (combined + public shared jobs)
  .husky/ .agents/ .claude/ .codex/ .grok/ .config/
  infra/                        # Compose / Docker / LGTM (private)
  secrets/                      # gitignored
  old/                          # archive reference (private)
  docs/                         # PRIVATE monorepo KEEP (never export)
    dev/                        # rules.md, process.md, harness-runtimes, â€¦
    COMMANDS.md PATTERNS.md TESTS.md PARITY.md SRC_GEN.md TIMESTAMPS.md
                                # full private monorepo reference (default home)
    wip/                        # gitignored journals often
    README.md                   # index: process + private KEEP here;
                                #   OSS ADRs â†’ public/docs/adrs;
                                #   product ADRs â†’ private/docs/adrs
  public/                       # *** ONLY export surface (d2-public) ***
    README.md, LICENSE (Apache-2.0), CONTRIBUTING.md, NOTICE?
    D2.Public.slnx
    packages/{dotnet,typescript}/
    services/                   # .gitkeep until first open service
    contracts/                  # public values + shared $schema
    tools/                      # public tools + scripts + scripts/lib
    docs/                       # OSS docs only
      adrs/                     # framework ADRs â€” each banner Visibility: PUBLIC
      # optional thin OSS guides only if 01 explicitly carves from private KEEP
      # (default: do NOT duplicate full PATTERNS here)
  private/                      # never export
    packages/                   # .gitkeep ok
    services/                   # edge, web, audit, files, courier, notifications
    contracts/                  # product values + private-only schemas
    tools/                      # gen-dev-keys, private helpers
    docs/
      adrs/                     # product / host ADRs â€” Visibility: PRIVATE
      v2/                       # PHASE_*, product design (from docs/v2)
```

**Export law (hard):** **`public/**` only.** Everything outside `public/` (incl. monorepo root + `docs/dev` + `private/**`) is **private** â€” not Apache, not export.

**Tools law:** physical homes **only** `public/tools/**` + `private/tools/**` (no monorepo-root tools SoT). `D2PublicToolsRoot` + `D2PrivateToolsRoot`.

### Docs law (user LGTM fold â€” dual docs + public ADRs)

| Surface | Lives where | Visibility |
| --- | --- | --- |
| OSS entry | `public/README.md` + `LICENSE` + thin `CONTRIBUTING.md` | **PUBLIC** |
| **Framework / open-feature ADRs** | **`public/docs/adrs/`** | **PUBLIC** â€” every file has **Visibility: PUBLIC** banner (folder path + annotation) |
| Optional OSS architecture notes | thin carve into `public/docs/` **only** with 01 ledger row; default **no** full PATTERNS copy | **PUBLIC** if carved |
| **Product / host ADRs** | **`private/docs/adrs/`** | **PRIVATE** â€” optional Visibility: PRIVATE banner |
| Product phase design | **`private/docs/v2/`** (from todayâ€™s `docs/v2`) | **PRIVATE** |
| Agent process / rules | **`docs/dev/`** at monorepo root | **PRIVATE** |
| Monorepo root README | thin **private** map for agents/humans | **PRIVATE** (not exported) |
| Symlinks between ADR trees | **Not SoT** â€” optional later convenience only | â€” |
| ADR numbering | Discontinuity OK; pick-and-choose public vs private at move | â€” |

**Public ADR banner (required on every file under `public/docs/adrs/`):**

```markdown
> **Visibility: PUBLIC** â€” ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
```

---

## Branch / Auth Core coordination

| Fact | Implication |
| --- | --- |
| Work branch | `n/oss` off `n/auth-core` |
| Auth Core design | Continues on `n/auth-core` |
| Archive of D2-WORX | Must **not** drop Auth Core commits: leave `n/auth-core` (or documented tip) intact; merge `n/oss` â†’ `main` only when ready; **never** force-lose auth-core history |
| Merge policy | Before archive: document merge order â€” typically finish/merge auth-core product slices **or** cherry/merge `n/oss` layout into the continuing private SoT without overwriting auth-core work |

---

## Cross-cutting decisions

### Locked (user + planner defaults consistent with locks)

| ID | Decision | Ruling | Rejected alternatives |
| --- | --- | --- | --- |
| **L1** | Tree shape | `public/{packages,services,contracts,tools,docs}` + `private/{packages,services,contracts,tools,docs}` + root private process (`docs/dev`, AGENTS, infra, â€¦). Export = **`public/**` only** | Root-only docs (starves public ADRs); monorepo-only license change without tree split |
| **L2** | Open libs today | **All** of today's `server/shared/{dotnet,typescript}` â†’ `public/packages/{dotnet,typescript}` | Cherry-pick â€œsome shared privateâ€ without inventory (violates clear SoT) |
| **L3** | Product hosts | **All** of today's `server/services/*` + `server/web` â†’ `private/services/*` (web = service/BFF host) | Leave web at root; put edge under public |
| **L4** | Public docs | **`public/` root** README/LICENSE/CONTRIBUTING **plus** **`public/docs/`** (framework ADRs with PUBLIC banner; optional OSS-safe guides). Private product ADRs â†’ `private/docs/adrs/`. Process â†’ root `docs/dev/`. Root README = private map only | Root-only docs (no public ADRs); all ADRs private; git symlinks as SoT |
| **L5** | Dual test suites | Public-only (mirror-identical) + combined private CI; public-parity copy in private so operator knows public will pass **before** mirror | Single suite only; silent export on every push |
| **L6** | Export | **Gated** (workflow_dispatch and/or checklist) â€” not every push | Auto-push public mirror on every main commit |
| **L7** | Releases | **Public repo owns** all real releases of open package IDs: NuGet/npm **and** GitHub Release attach. Private monorepo **must not** publish those IDs to nuget.org/npmjs **and must not** `gh release create` (or equivalent attach) of public package IDs â€” **pack / upload-artifact only**. Real Release + feeds only on **`d2-public`** | Dual-publish from private; private publishes same IDs â€œfor convenienceâ€; private GH Release of public IDs |
| **L8** | Dual binaries | Dev in private monorepo: **source-mode ProjectReference** into `public/packages/**`; product **release** pins **public package versions** (call-out documented; **pin-mode Docker/CI switch is out of scope this deliverable** â€” see [L8 / dual binaries](#l8-dual-binaries--m10)) | Always PackageReference-only (painful monorepo DX); always ProjectReference-only in release (no real package boundary) |
| **L9** | Codegen roots | Public packages generate **only** from `public/contracts`; private codegen emits **only** into `private/**`; generator **engines** may live under public **if** catalog-driven and free of product catalogs | Shared contracts root forever; private emitters writing into public/ |
| **L10** | Deliverable done | Monorepo **wired** (layout + gates + dual suites + export dry-run path) â€” remotes may not exist yet | Block complete until GitHub remotes exist |
| **L11** | tools + docs placement | **Tools:** `public/tools` + `private/tools` only. **Docs:** `public/docs` (OSS ADRs + optional thin guides) + `private/docs` (product ADRs + v2) + root **`docs/dev` + full monorepo KEEP** (COMMANDS/PATTERNS/TESTS/â€¦ stay private by default). Export includes `public/tools` + `public/docs` | Root-only tools; force all PATTERNS public; hang docs homes unlocked |
| **L12** | IP fence predicate | New rules.md (+ AGENTS.md lockstep) predicate: nothing product/proprietary under `public/`; CI/export dry-run enforces | Docs-only soft guidance |
| **L13** | Contract split / `$schema` | **Extract product-shaped catalog rows to private now** (OQ-1). Pattern: **values** files may be dual (`public/contracts/â€¦` + `private/contracts/â€¦`); **`$schema`** stays in **public** when both sides use it (or schema is shared); schema lives **private-only** only for private-only contract kinds. **Pre-label split catalogs** in [Dual-values architecture](#dual-values--registry-architecture-h2h3) â€” not â€œdefault public + hope IP scanâ€ | Inventory-only fence without split; single mixed values file |
| **L14** | Remote names | **`d2-public`** (Apache OSS working remote) + **`d2-private-worx`** (closed SoT) | Other names |
| **L15** | D2-WORX URL / cutover | **Keep `D2-WORX` public URL as archive** for some time (many external links). New work: private SoT = `d2-private-worx`; live OSS surface = `d2-public`. Do **not** force-rename away D2-WORX until links migrated | Immediately delete/rename D2-WORX; make D2-WORX the only public forever |
| **L16** | Licenses | **Private:** proprietary **All rights reserved**. **Public:** **Apache-2.0** (`public/LICENSE`) | PolyForm on private; dual-license ambiguity |
| **L17** | Empty dirs | **`.gitkeep`** on empty `public/services`, `private/packages`, empty tools sides, etc. on **either** tree | Omit empty dirs (export/layout drift) |
| **L18** | Solutions (OQ-7) | **Two solutions:** (1) **`public/D2.Public.slnx`** (locked path â€” monorepo-relative; never `public/packages/dotnet/â€¦`) â€” public packages + public tests only (OSS clone / public CI / export gate). (2) **Root `D2.slnx`** â€” **umbrella** public packages + private services/tests for **local monorepo DX** + private CI. Same for pnpm: public-only filter vs root workspace | Single solution only; combined only under `private/`; Public.slnx under packages/dotnet |

### L8 dual binaries (M10)

| Mode | When | Mechanism this deliverable |
| --- | --- | --- |
| **Dev / monorepo CI** | Always on `n/oss` after reorg | **ProjectReference** private â†’ `$(D2PublicPackagesDotnetRoot)â€¦` (source mode). Dockerfiles stay ProjectReference-compatible. |
| **Release pin** | Future product images / after public packages published from `d2-public` | PackageReference + version pin to public package IDs â€” **documented call-out only**; **no** Dockerfile dual-mode switch / MSBuild `UsePackageRefs` property required in 0032 |
| **Out of scope** | Dual-binary CI matrix, pin-mode Docker build args | Explicit deferred milestone; does **not** block L8 law or deliverable-complete |

### OPEN (true user decisions â€” not silently defaulted)

**OQ-1â€¦OQ-7 answered 2026-07-14** â€” see [Decision log](#open-questions) + L13â€“L18. No blocking OQs remain for Plan-Audit R2 / EXECUTE readiness (after R2 CLEAN).

**Residual non-blocking notes** (do not invent package ID policy beyond L7/L14):

| Note | Status |
| --- | --- |
| Exact public package `RepositoryUrl` host string | **Locked design:** public packs â†’ `https://github.com/<org>/d2-public` (or orgâ€™s final `d2-public` URL); private packables may keep private monorepo URL. Org slug filled at H1 remote create if not already known â€” do not invent a false org. |
| Whether any borderline catalog row is public vs private | Step **01 ledger + IP scan** with user ask on true borderline; pre-split catalogs in H3 table are **not** borderline (already product-shaped). |

---

## Path migration map (folder-cluster)

| From (today) | To (after reorg) | Class |
| --- | --- | --- |
| `server/shared/dotnet/**` | `public/packages/dotnet/**` | public packages |
| `server/shared/typescript/**` | `public/packages/typescript/**` | public packages |
| `server/shared/dotnet/tests/**` (`DcsvIo.D2.Tests`) | `public/packages/dotnet/tests/**` | **public-only** test project (mega suite stays with public packages) |
| `contracts/**` (public-eligible â€” see ledger) | `public/contracts/**` | public contracts |
| `contracts/**` (private-only â€” see ledger) | `private/contracts/**` | private contracts |
| `contracts/**` (split catalogs â€” see dual-values table) | **both** `public/contracts/**` (public values + shared schema) **and** `private/contracts/**` (product values) | split |
| `server/services/edge/**` | `private/services/edge/**` | private service |
| `server/services/audit/**` | `private/services/audit/**` | private service |
| `server/services/files/**` | `private/services/files/**` | private service (scaffold) |
| `server/services/courier/**` | `private/services/courier/**` | private service (scaffold) |
| `server/services/notifications/**` | `private/services/notifications/**` | private service (scaffold) |
| `server/web/**` | `private/services/web/**` | private BFF host â€” **joins pnpm workspace** under `private/services/**` (M3) |
| `server/D2.slnx` + `server/Directory.Build.props` + packages props | **Reshape** â€” see MSBuild / CPM below | dual roots |
| `server/Directory.Packages.props`, `server/NuGet.config`, `server/global.json` (if any), `server/stylecop.json` | **Root + dual package props** â€” see [CPM / props homes](#cpm--props-homes-h4) | CPM |
| `server/d2-version/**` | `private/tools/d2-version/**` (default) unless 01 proves public-only â€” **ledger row required** | private tooling default |
| `tools/ts-codegen` | `public/tools/ts-codegen` | public tools (H1) |
| `tools/release-runner` | `public/tools/release-runner` | public tools (H1) â€” public discovery only; private fence logic may live here but must not export private paths as default publish roots |
| `tools/contract-gate` | `public/tools/contract-gate` | public tools (H1) â€” must accept **dual contract roots** as args/env |
| `tools/geo-data-pipeline` | `public/tools/geo-data-pipeline` | public tools (H1) â€” geo reference data is public framework |
| `tools/commit-lint` | `public/tools/commit-lint` | public tools (H1) |
| `tools/loggermessage-splitter` | `public/tools/loggermessage-splitter` | public tools (H1) â€” **must** use `$(D2PublicToolsRoot)` not depth-relative `..\..\tools` |
| `tools/typespec-spike` | **classify in 01** â€” default `private/tools/typespec-spike` if product-shaped; else public | ledger |
| `tools/scripts/assemble-libs-bundle.mjs` | **`public/tools/scripts/`** (or fold under `public/tools/release-runner/`) | **public** â€” wired into public publish path (PA-R2-M4) |
| `tools/scripts/seed-publicapi-baselines.mjs` | **`public/tools/scripts/`** (or under release-runner) | **public** (PA-R2-M4 + **F-R3-F1** dual-mode discovery â€” see tools ledger) |
| `tools/scripts/seed-apiextractor-baselines.mjs` | **`public/tools/scripts/`** (or under release-runner) | **public** (PA-R2-M4 + **F-R3-F1**) |
| `tools/scripts/check-publicapi-shipped.mjs` | **`public/tools/scripts/`** | **public** â€” husky + CI consumer (PA-R2-M4 / M3) |
| `tools/scripts/lib/**` (live: `publicapi-empty-guard.mjs`, `apiextractor-empty-guard.mjs`, `source-fingerprint-compose.mjs` + `.d.mts`) | **`public/tools/scripts/lib/**`** (co-locate with public leaf seeders; or under release-runner if leaves fold there) | **public** â€” Â§26.23 empty-guard + Â§26.24 fingerprint-compose co-deps of public seeders/check-publicapi (**F-R3-F1**; relative `./lib/*` imports) |
| `tools/scripts/tests/publicapi-empty-guard.test.mjs` + `apiextractor-empty-guard.test.mjs` | **`public/tools/scripts/tests/`** (with public lib) | **public** â€” pins Â§26.23 guards (**F-R3-F1**); other `tools/scripts/tests/*` remain private default unless reclass |
| `tools/scripts/gen-dev-keys.sh` + secrets-touching helpers | **`private/tools/scripts/**`** | **private** â€” never export (M9/L2); REPO_ROOT after move = monorepo root, write only root `secrets/` (PA-R2-L3) |
| `tools/scripts/*` (other product regen / gates free of secrets) | **default `private/tools/scripts/**`** unless pre-seeded public above or 01 reclass with ledger row | private default |
| monorepo-root `tools/` after move | **removed as product SoT**; optional thin `tools/README.md` pointer â†’ `public/tools` + `private/tools` only | no dual SoT |
| Framework ADRs (`docs/adrs/*` public-class) | **`public/docs/adrs/`** + Visibility: PUBLIC banner | public docs |
| Product ADRs / host ADRs | **`private/docs/adrs/`** | private docs |
| `docs/v2/**` | **`private/docs/v2/`** | private product design |
| `docs/dev/**`, process KEEP | remain **`docs/dev/`** at monorepo root | private process |
| COMMANDS/PATTERNS/TESTS/PARITY/SRC_GEN/TIMESTAMPS | **Default stay monorepo `docs/` (private)**; optional thin public carve in 01 only with ledger row | E-R6-F1 |
| Root `docs/adrs/` after move | empty + redirect README | no mixed root ADR SoT |
| `infra/**`, `secrets/**` | monorepo root (never under `public/`) | private |
| `old/**` | untouched this deliverable | archive reference |

### Tools classification ledger (pre-seed â€” finalize counts in step 01) â€” H1 / M9 / L2

| Today path | Class | Target | Export? | Notes |
| --- | --- | --- | --- | --- |
| `tools/ts-codegen` | public | `public/tools/ts-codegen` | Y | Public emitters; must not hardcode root `contracts/` only â€” dual-root aware or public-root default + private override |
| `tools/release-runner` | public | `public/tools/release-runner` | Y | Discovery split: public root = `public/packages/**` only (H5) |
| `tools/contract-gate` | public | `public/tools/contract-gate` | Y | Dual roots: `public/contracts` + `private/contracts` when run on monorepo |
| `tools/geo-data-pipeline` | public | `public/tools/geo-data-pipeline` | Y | Reference data pipeline |
| `tools/commit-lint` | public | `public/tools/commit-lint` | Y | Shared commit convention |
| `tools/loggermessage-splitter` | public | `public/tools/loggermessage-splitter` | Y | Property-based includes; Docker COPY â†’ `public/tools/...` |
| `tools/typespec-spike` | **01** | prefer private if product | N if private | Spike may contain product IDL experiments |
| `tools/scripts/assemble-libs-bundle.mjs` | **public** | `public/tools/scripts/` (or `public/tools/release-runner/`) | **Y** | Live release-libs peer; free of secrets (PA-R2-M4) |
| `tools/scripts/seed-publicapi-baselines.mjs` | **public** | `public/tools/scripts/` (or release-runner) | **Y** | Â§26.20 seeder; **dual-mode discovery like H5** (public default = `public/packages/**`; monorepo private list **may** include KC client under `private/services/...`) â€” not â€œpublic packages onlyâ€ as sole ownership (**F-R3-F1**) |
| `tools/scripts/seed-apiextractor-baselines.mjs` | **public** | `public/tools/scripts/` (or release-runner) | **Y** | Â§26.20 seeder; same dual-mode roots as publicapi seeder (**F-R3-F1**) |
| `tools/scripts/check-publicapi-shipped.mjs` | **public** | `public/tools/scripts/` | **Y** | pre-commit + CI; retarget husky (PA-R2-M4 / M3) |
| `tools/scripts/lib/**` | **public** | `public/tools/scripts/lib/` (co-locate with leaves) | **Y** | Live inventory: `publicapi-empty-guard.mjs`, `apiextractor-empty-guard.mjs`, `source-fingerprint-compose.mjs` + `source-fingerprint-compose.d.mts` â€” required by relative imports Â§26.23/Â§26.24 (**F-R3-F1**). Export allowlist includes this tree when public release path needs it. |
| `tools/scripts/tests/*empty-guard*.test.mjs` | **public** | `public/tools/scripts/tests/` | **Y** | Unit pins for empty-guard modules; move with public lib (**F-R3-F1**) |
| `tools/scripts/gen-dev-keys.sh` + secrets-touching helpers | **private** | `private/tools/scripts/` | **N** | Never export; never under `public/tools`; after move resolve monorepo root (not `private/` as root) + write only root `secrets/` (PA-R2-L3) |
| `tools/scripts/*` (regen-typespec, other product gates, non-secret) | **private default** | `private/tools/scripts/` | N | Product monorepo scripts; promote only with explicit ledger reclass |
| `server/d2-version` | private default | `private/tools/d2-version` | N | Confirm in 01 |

**Â§26.20 seeder / fingerprint baseline ownership after reorg (F-R3-F1 â€” dual-mode, not â€œpublic packages onlyâ€):**

| Surface | Who re-seeds / check-baselines | Discovery roots |
| --- | --- | --- |
| **Public packages** (`public/packages/{dotnet,typescript}`) | Public seeders under `public/tools/scripts/` + `pnpm --filter release-runner check-baselines` on **public path** (default discovery = public packages only) | `public/packages/**` |
| **KC / other private consumables** (e.g. `private/services/edge/key-custodian/client`) | **Same public seeders in dual-mode** (monorepo private list includes KC path â€” mirrors release-runner H5 private list) **or** a named private seeder ownership row if 01 splits tools; **not** left unowned. Private consumable fingerprints remain private monorepo concern; public OSS clone never requires KC in seeder default list | public packages **+** private consumable paths when run on monorepo |
| **release-runner itself** | Public tool home; fingerprint identity tests stay under `public/tools/release-runner` | public tools |

Implementer **must not** drop KC from monorepo seeder discovery without either dual-mode args/env or an explicit private seeder ledger row.

**pnpm workspace target globs (H1 â€” no monorepo-root `tools/` entries):**

```yaml
packages:
  - "public/packages/typescript/**"
  - "private/packages/typescript/**"   # if any
  - "private/services/**"              # includes web + service client-ts (web JOINS workspace â€” M3)
  - "public/tools/ts-codegen"
  - "public/tools/geo-data-pipeline"
  - "public/tools/contract-gate"
  - "public/tools/release-runner"
  - "public/tools/commit-lint"
  # private tools packages if they are node packages:
  - "private/tools/**"                 # only if package.json present; scripts-only dirs need no glob
```

---

## Contract classification

### Hard private (must not export)

| Contract cluster | Why private |
| --- | --- |
| `keycustodian-error-codes/` | KC domain codes; consumer under Edge KC; **never** in public ErrorCodes.Registry AdditionalFiles (H2) |
| `typespec/key-custodian/`, `typespec/audit/` | Product op contracts |
| `typespec/fixtures/**` | Emitter fixtures may stay public **if** free of product names â€” **classify per file in 01** |
| `advisory-locks/` (domain-key fleet **values** / product lock rows) | **Values = private only** (100% KC product rows). **Public** keeps the `AdvisoryLocks.SourceGen` **engine** under public packages. **AdditionalFiles** bind lock catalogs **only on private host** (KC Infra). Public packages **never** glob private lock files (PA-R2-L1) |

### Pre-labeled **split** catalogs (H3 â€” not default-public)

These **already embed product rows** today (scopes `internal.kc.*`, product audiences, encryption domains audit/courier/â€¦, `keycustodian_*` TK). Step 01 **does not** re-litigate â€œmaybe publicâ€; it executes the split + ledger columns.

| Catalog / schema home (today) | Public values (after) | Private values (after) | Public emit | Private emit | Merge rule | Parity home |
| --- | --- | --- | --- | --- | --- | --- |
| `auth-scopes` (+ schema) | Framework / open scopes only | Product scopes (`internal.kc.*`, service-specific) | Auth.Abstractions public constants from **public** values only | Private host/composition or private generator over publicâˆªprivate values | Private **union** public+private at host/generator; public package **never** reads private file | Shared.Tests public-only pins public set; Edge/KC tests pin private set |
| `auth-audiences` | Open/framework audiences | Product service audiences | Public package from public values | Private merge | Same union rule | Dual parity tests |
| **`auth-protocol-audiences`** (PA-R2-M1 â€” **locked full-public**) | **Entire catalog public** as platform wire vocabulary (incl. host wire value **`d2-edge`** / `D2_EDGE_SELF_AUDIENCE` and framework protocol audiences e.g. `d2.internal`) | **None** â€” not a product-only catalog; IP fence **must not** treat `d2-edge` protocol audience as product IP requiring private extract | **Public** `DcsvIo.D2.Auth.Abstractions` AdditionalFiles bind **public** protocol-audiences only (same file stays under `public/contracts`) | Private hosts **consume** public constants; no private-only protocol-audience values file | No split; no private merge file | Shared.Tests pin public set; Edge composition uses public constants (consumer row in 01 ledger) |
| `encryption-domains` | Framework-only domains (if any pure) | Product domains (audit, courier, files, â€¦) | Public Encryption package from public values **only** | Private product domains emit under private | Union at private composition | Negative: public nupkg has no product domain names |
| `messages/` (i18n TK) | Platform/common keys; **multi-locale key-set identical** (M8) | Product keys (`keycustodian_*`, service UX) as **full multi-locale set** | Public packages AdditionalFiles â†’ `public/contracts/messages/**` only | Private TK emit from private messages + may merge public keys for hosts | Hosts load publicâˆªprivate; public emit never ingests private message files | Locale parity: every product key present in all locales under private |
| Other product-shaped rows found in IP scan | Extract to private | Product rows | â€” | â€” | Same | 01 ledger |

**PA-R2-M1 ruling (locked):** `auth-protocol-audiences` = **option (b) full-public platform vocabulary** â€” not split. Rejected alternative: split private Edge self-audience file (would dual-SoT a wire constant Edge already consumes from Auth.Abstractions). Step **01 ledger** names: consumer package(s) = `DcsvIo.D2.Auth.Abstractions` (+ Edge host as runtime consumer of public constants); emit artifact = public protocol-audience constants / AdditionalFiles under public contracts.

**Default public** (framework â€” still IP-scanned in 01; reclass to split/private if product rows found):

| Cluster examples | Notes |
| --- | --- |
| `error-codes/`, `error-category/`, `d2result-envelope/`, `input-error/`, `tk-message/`, `problem-details/`, `grpc-trailers/` | Framework result/error surface â€” **public registry catalogs only** (H2) |
| `headers/`, `jwt-claims/`, `in-process-keys/`, `auth-context/`, `request-context/` | Framework auth/context vocabulary â€” scan rows |
| `geo/`, `validation/`, `location/`, `temporal/`, `enum/`, `resilience/` | Reference data + fixtures |
| `encryption-frame*` (frame schema, not domains list) | Crypto framework wire |
| `dlq-failure-metadata/`, `otel-messaging-tags/`, `telemetry/` | Messaging/telemetry framework |
| `mq-messages/`, `mq-subscriptions/` | Scan for product routes |
| `protos/common/**` | Public wire primitives |
| `typespec/common/**` | Platform IDL helpers |

**Law application:** step 01 produces a ledger table (wip + eventually KEEP under `public/contracts/README.md` + `private/contracts/README.md`) with **every** current `contracts/*` folder labeled `public | private | split`, plus columns:

| Ledger column | Required |
| --- | --- |
| Contract folder | Y |
| Class `public \| private \| split` | Y |
| **Consumer csproj / package** | Y (H3) |
| **Emit artifact** (generated path / constants type) | Y (H3) |
| Schema home (public/private) | Y for split |
| Notes / IP evidence | Y |

No folder moves without a label.

---

## Dual-values + registry architecture (H2/H3)

### ErrorCodes.Registry strategy (locked)

| Surface | Catalogs consumed | Emit / package |
| --- | --- | --- |
| **Public** `DcsvIo.D2.ErrorCodes.Registry` (+ TS `@dcsv-io/d2-error-codes-registry`) | **Only** `$(D2PublicContractsRoot)**/*-error-codes.spec.json` (and public category/envelope siblings as today for public) | Public packable registry â€” **no** `KEYCUSTODIAN_*` / private catalog rows |
| **Private** KC error-codes generator shell | `private/contracts/keycustodian-error-codes/**` (+ shared engine from public packages via ProjectReference) | Emits under `private/services/edge/key-custodian/**` only |
| **Private composition / host** (Edge, combined suite) | Public registry **plus** private domain registries (KC, future services) merged at **host** or via **private generator** â€” never by public package globbing private paths | Host DI / merged lookup |

**Hard forbid:** public csproj `AdditionalFiles` / globs into `$(D2PrivateContractsRoot)` or any `private/**` path.

**Advisory-locks emit rule (PA-R2-L1):** domain-key **values** catalogs live **private only**. Public packages may ship the **`AdvisoryLocks.SourceGen` engine**. **AdditionalFiles** for lock value catalogs bind **only on private host** (KC Infra). Public packages **never** glob private lock files.

**Shared.Tests re-baseline (named exit â€” H2):** after split, public Shared.Tests count pins / catalog enumerations that assumed â€œall contracts including KCâ€ **must** re-baseline to public-only set in **step 02** (same step as move). Private Edge KC tests keep KC pins. Journal names the test classes touched (parity / count pins).

### Public package AdditionalFiles law

| Package class | AdditionalFiles / catalog roots |
| --- | --- |
| Public packable | `$(D2PublicContractsRoot)â€¦` **only** (incl. full-public `auth-protocol-audiences`; **no** private advisory-lock values) |
| Private service / private generator | May read `$(D2PrivateContractsRoot)` and, if needed, public roots; **advisory-lock values** only here (KC Infra) |
| TS public packages | Spec paths under `public/contracts` only |

### Negative export / IP tests (required)

| Test intent | Planned name / home |
| --- | --- |
| Product catalog rows must not re-enter `public/` | `PublicTree_ContainsNoProductCatalogRows` (or export dry-run fixture) â€” step 03 |
| Public registry has no KEYCUSTODIAN_ | `ErrorCodesRegistry_Public_ExcludesKeyCustodian` â€” Shared.Tests |
| Public messages have no private-only keys | `PublicMessages_NoProductOnlyKeys` |
| Export allowlist rejects `private/**` | export dry-run negative fixture |

---

## MSBuild / solution reshape

### Today (`server/Directory.Build.props`)

- `D2RepoRoot` = parent of `server/`
- `D2ContractsRoot` = `$(D2RepoRoot)contracts\`
- `D2SharedDotnetRoot` = `$(D2RepoRoot)server\shared\dotnet\`
- `D2SourceGenSharedRoot` = under shared
- `D2ErrorCodesEmitRoot` = under shared
- `D2ToolsRoot` = `$(D2RepoRoot)tools\`
- CPM / NuGet.config / stylecop under `server/` only

### Target properties (semantics locked; exact file placement in [CPM / props homes](#cpm--props-homes-h4))

| Property | Meaning (post-reorg) |
| --- | --- |
| `D2RepoRoot` | Monorepo root (private SoT root) |
| `D2PublicRoot` | `$(D2RepoRoot)public\` |
| `D2PrivateRoot` | `$(D2RepoRoot)private\` |
| `D2PublicContractsRoot` | `$(D2PublicRoot)contracts\` |
| `D2PrivateContractsRoot` | `$(D2PrivateRoot)contracts\` |
| `D2PublicPackagesDotnetRoot` | `$(D2PublicRoot)packages\dotnet\` |
| `D2PrivatePackagesDotnetRoot` | `$(D2PrivateRoot)packages\dotnet\` (may be empty) |
| `D2PrivateServicesRoot` | `$(D2PrivateRoot)services\` |
| `D2PublicToolsRoot` | `$(D2PublicRoot)tools\` |
| `D2PrivateToolsRoot` | `$(D2PrivateRoot)tools\` |
| `D2SourceGenSharedRoot` | under `$(D2PublicPackagesDotnetRoot)source-gen-shared\core\` (path follows package move) |
| `D2ErrorCodesEmitRoot` | under `$(D2PublicPackagesDotnetRoot)source-gen-shared\error-codes-emit\` |
| ~~`D2ToolsRoot`~~ | **Retired** as single root SoT â€” migrate call sites to Public/Private tools roots (M1) |
| ~~`D2ContractsRoot`~~ | **Retired** as single root â€” migrate to Public/Private contracts roots; temporary dual-alias only if needed for one PR, not end-state |
| ~~`D2SharedDotnetRoot`~~ | **Retired** â†’ `D2PublicPackagesDotnetRoot` |

**Property-based ProjectReference (M7 â€” primary):**

```xml
<ProjectReference Include="$(D2PublicPackagesDotnetRoot)utilities\DcsvIo.D2.Utilities.csproj" />
```

Relative `..\..\..` only if props unavailable (legacy escape hatch). **Law sentence:** private â†’ public ProjectReferences are **property-based** as primary; public **never** references private.

**loggermessage-splitter / depth-relative trap (M1):** all tool includes and Docker COPY must use `$(D2PublicToolsRoot)` (or explicit `public/tools/...` in Docker); **forbid** new boundary-crossing `..\tools` after reorg.

### Solutions (L18) â€” single locked Public.slnx path (PA-R2-M2)

| Solution | **Locked path** | Scope | Who runs it |
| --- | --- | --- | --- |
| **Public-only** | **`public/D2.Public.slnx`** | Public .NET packages + public tests only | **OSS clone / public CI / export gate** â€” must build with **zero** `private/` ProjectReference paths (L4 exit) |
| **Umbrella** | **`D2.slnx`** (monorepo root) | Public packages + private services + private tests | **You / private monorepo CI** |

**PA-R2-M2 lock:** there is **one** Public.slnx path string for tree, L18, CI, COMMANDS, export, success criteria: **`public/D2.Public.slnx`**. **Forbidden** dual softener (`public/packages/dotnet/D2.Public.slnx`, ellipsis â€œunder public/â€, â€œlock in 01â€). Step **01 journal only records** the lock â€” does not re-pick the path.

**Public isolation exit (L4):** `dotnet build public/D2.Public.slnx` + mechanical check: no ProjectReference / path under `private/`.

**License packing:** public package props pack **`public/LICENSE`** (Apache-2.0), not root PolyForm (M5).

### CPM / props homes (H4)

After `server/` dissolves, placement:

| File | Home (locked design) | Consumers |
| --- | --- | --- |
| `Directory.Packages.props` | **Monorepo root** (primary CPM SoT for umbrella + private + public when built in monorepo) | All .NET projects via Directory.Build import chain |
| `NuGet.config` | **Monorepo root** | restore |
| `global.json` | **Monorepo root** | SDK pin |
| `stylecop.json` | **Monorepo root** and/or `public/packages/dotnet/stylecop.json` with private importing shared rules â€” **01 picks one chain**; public-only clone must resolve StyleCop without `private/` |
| `Directory.Build.props` / `.targets` | **Root** defines `D2RepoRoot` + dual roots; optional `public/packages/dotnet/Directory.Build.props` for pack defaults (Apache LICENSE, RepositoryUrl); optional `private/services/Directory.Build.props` for service defaults | dual |
| Public-only export clone | Must include root-or-public props chain sufficient to build **`public/D2.Public.slnx`** **without** private tree (copy props into export allowlist if needed) | export dry-run / future d2-public |

**01 inventory:** count every `Directory.Packages.props` consumer / `PackageVersion` usage; list files that assume `server/` as props parent.

### InternalsVisibleTo (L3)

Public packages may keep `InternalsVisibleTo` for monorepo combined test assembly names (e.g. private Edge tests). **IVT strings are not private source export** â€” friend-tests pattern allowed; does not place private source under `public/`.

---

## pnpm workspace + web (M3)

**Today:** workspace lists `server/shared/typescript/**`, `server/services/**`, monorepo-root `tools/*`; `server/web` has `workspace:*` deps but is **outside** workspace.

**Target decisions (locked):**

1. **Web joins workspace** as `private/services/web` under glob `private/services/**`.
2. Exit: `pnpm install` resolves webâ€™s `workspace:*` deps; web package name stable or renamed with rewrite inventory row.
3. **Dockerfile.web** is **not** a mechanical `server/web` retarget â€” today still uses **v1** paths (`backends/node`, `clients/web`). **Dedicated rewrite inventory row:** Dockerfile.web / compose web service / package-name â€” redesign to `private/services/web` + `public/packages/typescript` as needed in step 02.

---

## release-runner discovery + publish fence (H5 + PA-R2-M5)

| Discovery mode | Root(s) | Includes KC client? | Real publish (nuget.org / npmjs / GH Release)? |
| --- | --- | --- | --- |
| **Public (`d2-public`)** | `public/packages/{dotnet,typescript}` **only** | **No** | **Only** here: nuget.org/npmjs **and** `gh release create` (or equivalent attach) of public package IDs (L7) |
| **Private monorepo list** | public packages + private consumables (`private/services/**/client`, future) | **Yes** (as private consumable) | **None** for public package IDs â€” **pack / upload-artifact only** |
| **Export / public OSS** | public packages only | No | Public ownership on `d2-public` after export |

**Private monorepo release-libs post-reorg (PA-R2-M5 â€” hard law, not soft â€œmayâ€):**

1. **`must`** pack + upload-artifact only for public package IDs â€” **no** `gh release create` (or any Release attach) of public package IDs from the private monorepo.
2. **Hard-fail** if `dry_run=false` (or equivalent publish path) would attach/publish public IDs from private (covers always-present `GITHUB_TOKEN` + `contents: write` â€” do not rely on absence of NUGET_API_KEY/NPM_TOKEN alone).
3. Real **GitHub Release + nuget.org/npmjs** for public IDs = **`d2-public` only**.
4. Adversarial test (step 03 matrix): **`PrivatePublishFence_NoGitHubReleaseOfPublicIds`** (and/or workflow static assert that private `release-libs.yml` has no `gh release create` for public IDs).

**Must retarget** `tools/release-runner` (â†’ `public/tools/release-runner`) hardcodes:

- `server/shared/typescript` â†’ `public/packages/typescript`
- `server/shared/dotnet` â†’ `public/packages/dotnet`
- KC client path â†’ **private** discovery only; never â€œpublic OSSâ€ list

**Public seeders dual-mode (F-R3-F1 â€” same fence spirit as H5):** leaf seeders + **`public/tools/scripts/lib/**`** (Â§26.23 empty-guard + Â§26.24 `source-fingerprint-compose`) move as one public cluster. Discovery:

- **Public / `d2-public` path:** seed + check-baselines over `public/packages/**` only (no KC).
- **Private monorepo path:** seeders accept dual roots â€” public packages **+** private consumables (KC client under `private/services/...`) so Â§26.20 private-consumable baselines stay owned; **or** 01 names a private seeder row. Never leave KC baseline re-seed unowned after reorg.

**Rewrite inventory must include (H5 + M4 + F-R3-F1):** seeders (`seed-publicapi*`, `seed-apiextractor*`), **`tools/scripts/lib/**`** (empty-guard + fingerprint-compose + empty-guard unit tests), `check-publicapi-shipped`, `release-libs.yml` path filters + **GH Release attach removal/fence**, `assemble-libs-bundle` (license + path hardcodes â€” lives under **public** tools/scripts), seeder dual-mode path hardcodes (`server/shared` + KC client), workflow path filters under `.github/workflows/*`.

**Exit (step 03):** public `--list` excludes KC; private list may include KC but never labeled public OSS; hard-fail private nuget/npm of public IDs; **no** private GH Release of public IDs; pack/artifact-only on private monorepo; **`check-baselines` green** for public packages; monorepo dual-mode (or private seeder) covers KC/private consumables.

---

## Docker / Compose / scripts

| Item | Target |
| --- | --- |
| `infra/docker/Dockerfile.*` (service images) | COPY from `private/services/...` + `public/packages/...` + `public/tools/...` as needed |
| `Dockerfile.web` | **Separate row** â€” v1 path redesign (M3) |
| `public/tools/scripts/*` (pre-seed public) | assemble-libs-bundle, seed-publicapi/apiextractor baselines, check-publicapi-shipped, **`lib/**`** (empty-guard + fingerprint-compose), empty-guard unit tests â€” **export allowlist includes** this tree when public release path needs them (PA-R2-M4 + **F-R3-F1**) |
| `private/tools/scripts/*` | regen-typespec, gen-dev-keys, other product gates; path-sweep step 02 |
| `gen-dev-keys` | **private/tools only**; export allowlist excludes; after move resolve **monorepo root** (sentinel/`findRepoRoot` preferred; fixed `..` count must become three-level or sentinel â€” not `private/` as root) and write only root `secrets/` (PA-R2-L3 / M8) |
| Compose build contexts | Retarget; secrets stay root env files |

---

## DcsvIo.D2.Tests (mega project) + TestPaths (M2)

| Concern | Rule |
| --- | --- |
| Home | Moves with public packages (`public/packages/dotnet/tests/**`) |
| Private host tests | Stay under each `private/services/*/tests` â€” never merge into public mega project |
| Combined CI | Public suite + all private service test projects |
| **TestPaths sentinels** | Today require `server/D2.slnx` + root `contracts/` â€” **rewrite** to root `D2.slnx` + dual helpers (`PublicContractsRoot`, `PrivateContractsRoot`, repo root without `server/`) |
| Rewrite inventory | All `D2.slnx` / `contracts/` / `server/shared` path sentinels in tests + tools |

---

## License cutover inventory (M5)

| Site | Today | After |
| --- | --- | --- |
| Public nupkg LICENSE | Root PolyForm via shared props | **`public/LICENSE` Apache-2.0 only** |
| Private / root LICENSE | PolyForm / mixed | **All rights reserved** (L16) |
| `assemble-libs-bundle` | Hardcodes PolyForm HOW-TO-USE | Apache path for public assemble; private ARR |
| KC client pack | Packs root LICENSE | Private ARR; not public OSS |
| Public TS packages | Often no `license` / `"private": true` | `license: Apache-2.0`; publishable readiness for public IDs when exportable |
| Source headers (Â§7.7 dual-header â€” PA-R2-L2) | ARR / company headers | **Public tree:** SPDX / Apache-2.0-compatible headers (**no** â€œAll rights reservedâ€); **private tree:** ARR remains. **Bulk public header rewrite in step 02** with path KEEP (preferred); step 04 residual polish only if any public ARR survivors. Update rules.md Â§7.7 in step **04** lockstep |
| Props chain | Must not let PolyForm shadow Apache for public packs | Public Directory.Build pack metadata points at `public/LICENSE` |

**01:** inventory **every** license pack site + header form; **02:** wire packing paths; **04:** polish public Apache root docs if residual header text.

### RepositoryUrl / PackageProjectUrl (M6)

| Package class | URL |
| --- | --- |
| Public packables | `d2-public` remote URL (not D2-WORX archive) â€” set in 02 with H-checklist polish in 04 |
| Private packables | Private monorepo / `d2-private-worx` URL OK |

---

## Multi-locale messages (M8)

- Public message locales stay **key-set-identical** across locales.
- Product-only keys move as a **full multi-locale set** to `private/contracts/messages`.
- Public package AdditionalFiles only `public/contracts/messages`.
- TK emit roots named in 01 ledger (public emit package vs private host emit).

---

## Steps (fat-step default)

| # | Step | Status | Prereqs | Goal (what is true after) |
| --- | --- | --- | --- | --- |
| **01** | `01-classify-scaffold` | âœ… CLEAN | none | Classification ledger complete; empty dual-tree scaffold; dual-values/registry + rewrite inventory; no bulk product move |
| **02** | `02-physical-reorg` | âœ… (smoke green; optional empty `server/` OS-lock) | 01 | Trees moved; dual Docker rebuild + multiproc gate-shape smoke green; path KEEP; Shared.Tests re-baseline |
| **03** | `03-extensions-packages` | âœ… CLEAN | 02 | 1:1 `DcsvIo.D2.*.Extensions` under private; no ProductConstants mega-bag |
| **04** | `04-backup-parity-regression` | âœ… CLEAN | 03 | Backup was-vs-is **MISSING 0**; full gate regression; EncryptionDomainModeCatalog overlay |
| **05** | `05-public-package-identity` | âœ… CLEAN + committed (`30ab5384c`) | 04 | **Package ids â†’ dcsv-io nomenclature.** Open npm `@dcsv-io/d2-*`; open NuGet **`DcsvIo.D2.<Rest>`** (strip `Shared.`; **no** `.Private.`); private overlays **`DcsvIo.D2.Private.<Rest>.Extensions`**; product **`DcsvIo.D2.Private.Edge.*` / `DcsvIo.D2.Private.Audit.*`** (`.Private.` **mandatory** â€” never open-looking `DcsvIo.D2.Edge.*`). Closed clients **`d2-private-â€¦`**. Claimed orgs: GitHub/npm/NuGet **dcsv-io**. **Before** dual-suites CI so pipelines never bake `@dcsv-io/d2-*` / bare `DcsvIo.D2.*` / bare `DcsvIo.D2.Private.Edge.*` / wrong product without Private |
| **06** | `06-dual-suites-ci-export` | âœ… CLEAN (code-audit R2) + CI follow-ups on `n/oss` | 05 | Public-only + combined suites; CI dual roots; export dry-run + IP fence; hard private publish fence; COMMANDS dual-suite + pack-only this step |
| **07** | `07-law-docs-cutover` | âœ… CLEAN (code-audit R2 Â· 0 FINDING) | 06 | Law + brand (D2 vs D2-WORX) + schema hosts under `*.d2.dcsv.io`; public Apache root docs; ADR + V2; H1â€“H9 |
| **FR** | `final-review` | âœ… CLEAN (FR_FULL R4 Â· 0 FINDING) | 07 | FR_FULL K=7 clean; completeness checklist next |

### Per-step audit mode map (M14)

| Step | Plan-Audit / code-audit mode | Notes |
| --- | --- | --- |
| **Deliverable Plan** | Product full **K=7** R1â€“R2; dirty R3â€“R4 CLEAN; **R5** dual-docs fold (E+G+C) until CLEAN | Results: `plan-audit-rN/` |
| **01** | Product full **K=7** or justified **Y** (inventory/scaffold â€” not Skip) | Journal under `01-classify-scaffold/journal.md` |
| **02** | Product full **K=7** | Multi-concern path move |
| **03** | Product full **K=7** (or Y = A/C/F justified) | Packaging / layer / codegen surface |
| **04** | Product full **K=7** (min Y = **A + E + G**; expand if parity findings) | Loss-detection + regression gates â€” **not** Skip |
| **05** | Product full **K=7 only** â€” **no** Y=A/B/F alternate | PackageId / npm name / baseline / import graph / KEEP + Â§24 process load-bearing / mega-rename >>50 files â†’ Â§24.16 full K |
| **06** | Product full **K=7 only** â€” **no** min Y=A/C/D/F | Export/CI/security surface â€” multi-concern (workflows, dual suites, IP export, private publish fence, contract-gate dual roots); G-R1-F1 / G-R2-F1 |
| **07** | Pure-meta **Y = E+G+B** (headers/source dual-header residual in scope) | Law/docs + dual-header conventions; path KEEP already done in 02 |
| **FR** | **FR_FULL** full **K=7** + own FR journal | Deliverable scope |
| After findings | **Dirty-only** (+ sister-blast) | Not K=1 |

### Locked â€” org + package identity (user 2026-07-15 final nomenclature)

| Surface | Identity | Status |
| --- | --- | --- |
| **GitHub** | org **`dcsv-io`** | âœ… claimed |
| **npm** | org/scope **`@dcsv-io`** | âœ… claimed (`dcsv` short name was taken) |
| **NuGet** | publisher **dcsv-io** (profile/gravatar) + company token **`DcsvIo`** (one Pascal token â€” not `Dcsv.Io`) | âœ… account ready |
| **Domain (URIs)** | **`dcsv.io`** â€” framework ids under **`*.d2.dcsv.io`** (e.g. `schemas.d2.dcsv.io`, existing `problems.d2.dcsv.io`) | law; scrub `$id`s that still say `d2-worx.dev` in step 05/07 |
| **Framework brand** | **D2** (human) | not WORX in public package ids |
| **Product brand** | **D2-WORX** / compose `d2-worx` | private monorepo product brand; **open** assemblies `DcsvIo.D2.<Rest>`; **closed** product assemblies **`DcsvIo.D2.Private.*`** in step 05 |
| **Visibility** | **Tree only** (`public/packages` vs `private/**`) â€” never encode public/private by omitting a segment on open packages | law |
| **Open vs closed id marker** | **Open** PackageIds/npm: **never** `.Private.` / `d2-private-`. **Closed** D2 packages: **must** include `.Private.` / `d2-private-` | law (Private-on-all-closed re-amend) |
| **Historical `Shared`** | Explains old folder layout; **do not keep `Shared` in long-term PackageIds** (`DcsvIo.D2.Shared.*` **FORBIDDEN**) | law |

**Package id law (step **05** implements):**

| Kind | From (today) | To (target) |
| --- | --- | --- |
| **Open npm** | `@dcsv-io/d2-<leaf>` | **`@dcsv-io/d2-<leaf>`** (e.g. `@dcsv-io/d2-result`) â€” no nested `@dcsv-io/d2/result`; **never** `d2-private-` under public |
| **Open NuGet library** | `DcsvIo.D2.<Rest>` | **`DcsvIo.D2.<Rest>`** (strip `Shared.`) e.g. `DcsvIo.D2.Result` â†’ `DcsvIo.D2.Result` â€” **never** `.Private.` |
| **Private thin overlay** | `DcsvIo.D2.<Rest>.Extensions` | **`DcsvIo.D2.Private.<Rest>.Extensions`** e.g. `DcsvIo.D2.Private.Encryption.Extensions` â€” 1:1 twin law; home `private/packages/**` |
| **npm private overlay** (if any) | â€” | **`@dcsv-io/d2-private-<leaf>-extensions`** under private tree only |
| **Product hosts / modules** | `DcsvIo.D2.Private.Edge.*`, `DcsvIo.D2.Private.Audit.*`, `DcsvIo.D2.Private.Edge.KeyCustodian.*` | **`DcsvIo.D2.Private.Edge.*`**, **`DcsvIo.D2.Private.Audit.*`**, **`DcsvIo.D2.Private.Edge.KeyCustodian.*`** (**IN SCOPE step 05**; **not** `DcsvIo.D2.Edge.*` without Private) |
| **Product client lib** | `DcsvIo.D2.Private.Edge.KeyCustodian.Client` / `@dcsv-io/d2-private-key-custodian-client` | **`DcsvIo.D2.Private.Edge.KeyCustodian.Client`** / **`@dcsv-io/d2-private-key-custodian-client`** |
| **Open SourceGen / Tests** | `DcsvIo.D2.*.SourceGen` / `DcsvIo.D2.Tests` | `DcsvIo.D2.*.SourceGen` / **`DcsvIo.D2.Tests`** (open â€” no Private) |
| **Forbidden** | unscoped npm `d2`; **`worx`** in **public** package ids; **open** PackageId/npm with **`.Private.`** / **`d2-private-`**; **closed** D2 without Private; **`DcsvIo.D2.Edge.*` / `DcsvIo.D2.Audit.*` without Private**; **`DcsvIo.D2.Shared.*`** long-term; bare product `DcsvIo.D2.Private.Edge.*` while libs are `DcsvIo.D2.*` | â€” |
| **Publish discovery** | â€” | **Must not** treat all `DcsvIo.D2.*` as public â€” **public tree / open-lib allowlist only** |

### Locked â€” private extension package law (step **03** shape; **05** renames ids)

| Rule | Law |
| --- | --- |
| **When** | Private needs product-only rows for a public generated surface |
| **Shape** | **1 public package â†’ 1 private Extensions package** |
| **Name (after 05)** | **`DcsvIo.D2.Private.<Rest>.Extensions`** (catalog/basic extend only â€” `.Extensions` OK under **Private** only). Pre-05 disk (step 03 CLEAN): `DcsvIo.D2.*.Extensions` |
| **Meaning** | Depends on public package; **adds** product-only generated surface / helpers â€” not a rename twin, not a multi-concern bag |
| **Home** | `private/packages/**` only (never export; never public PackageId containing `.Private.`) |
| **Forbidden** | `DcsvIo.D2.Private.ProductConstants` mega-bag; unrelated brand names as non-Extensions id; stuffing scopes+encryption+â€¦ into one host when public homes differ; public-looking `*.Extensions` without `.Private.` |

**1:1 map (03 on disk â†’ 05 target):**

| Public (pre-05 â†’ post-05) | Private extension (pre-05 â†’ post-05) |
| --- | --- |
| `DcsvIo.D2.Auth.Abstractions` â†’ `DcsvIo.D2.Auth.Abstractions` | `DcsvIo.D2.Auth.Abstractions.Extensions` â†’ **`DcsvIo.D2.Private.Auth.Abstractions.Extensions`** |
| `DcsvIo.D2.Encryption` â†’ `DcsvIo.D2.Encryption` | `DcsvIo.D2.Encryption.Extensions` â†’ **`DcsvIo.D2.Private.Encryption.Extensions`** |
| `DcsvIo.D2.I18n.Keys` â†’ `DcsvIo.D2.I18n.Keys` | `DcsvIo.D2.I18n.Keys.Extensions` â†’ **`DcsvIo.D2.Private.I18n.Keys.Extensions`** |

### Locked â€” backup parity baseline (step **04**)

| | |
| --- | --- |
| **Backup path** | `C:\DCSV\Projects\D2-WORX-2026-07-14-BACKUP` (pre-reorg monorepo snapshot; **read-only**) |
| **Working tree** | Current `n/oss` D2-WORX after 02+03 |
| **Method** | Area-by-area systematic scan (packages, services, contracts, tools, docs/ADRs/v2, CI, tests, infra, scripts) â€” inventory **present-in-backup** vs **present-in-new** with disposition: **moved** / **split** / **intentional drop** / **MISSING (FINDING)** |
| **Regression** | Re-run the same quality bar that mattered pre-reorg: `dotnet build` umbrella + public; `jb inspectcode`; Shared.Tests full + Unit; private host tests that existed; relevant pnpm/ts checks â€” **no silent regressions** |
| **New tests** | See [New / additional tests for new work](#new--additional-tests-for-new-work-locked) â€” step 04 owns parity-ledger + regression gate; step 03 owns Extensions-law tests |

---

## New / additional tests for new work (locked)

**Rule:** Reorg + dual-values + Extensions are **new product surface**. Retargeting old tests is necessary but **not sufficient**. Plan + Implementer for steps **03** and **04** must **add** tests (or fail-closed checks) that only make sense after the split. Skipping â€œbecause old suite is greenâ€ is a FINDING.

### Step 03 â€” `DcsvIo.D2.*.Extensions` (must land with the reshape)

| # | New test / gate | Why (not covered by pre-reorg suite) |
| --- | --- | --- |
| **T3.1** | **Package graph:** every `*.Extensions` csproj `ProjectReference`s its public twin; **no** reverse ref (public â†’ Extensions) | Prevents export/IP inversion |
| **T3.2** | **1:1 inventory:** known public generated surfaces with private rows have **exactly one** Extensions package; **zero** residual `ProductConstants` / misbranded private twins | Naming/mapping law is machine-checkable |
| **T3.3** | **Public isolation:** `public/D2.Public.slnx` build + graph assert **no** ProjectReference into `private/**` or `*.Extensions` | Extensions are private-only |
| **T3.4** | **Emit content:** dual-target generator tests per Extensions host â€” public half only on public types; publicâˆªprivate on Extensions types; **collision / wrong-assembly** pins | Replaces accidental mega-bag coverage |
| **T3.5** | **Consumer pins:** private hosts that need product rows reference the **correct** Extensions package only (not a bag, not a sibling concern) â€” at least Edge/KC + one non-Edge sample | Over-dep / wrong-ref regression |
| **T3.6** | **Public catalog purity:** public scopes/audiences/encryption/messages/registry (C# + TS ship surfaces + `etc/*.api.md` where applicable) contain **no** product-only rows (`internal.kc.*`, product encryption domains, KC error codes, â€¦) | Split stayed true after Extensions move |
| **T3.7** | **Type / FQN safety:** Extensions emit does not redefine public FQNs (CS0433 class) â€” dual-type or namespaced extension types pinned | Compile hazard specific to dual emit |

### Step 04 â€” backup parity + regression (must land with the scan)

| # | New test / gate | Why |
| --- | --- | --- |
| **T4.1** | **Parity ledger artifact** (wip or committed under step journal): area Ã— backup-path Ã— new-path Ã— disposition; **MISSING** rows are findings until resolved or user-waived | Systematic was-vs-is â€” not a vibes pass |
| **T4.2** | **Area scanners** (scripted preferred): packages (dotnet+ts), services, contracts top-level, tools, docs/adrs+v2, CI workflows, test projects â€” set-diff backup vs current mapped homes | Human-only scan will miss |
| **T4.3** | **Test project inventory:** every test csproj/package that existed in backup still has a home + is in a solution / runnable path (or explicit retire row) | Lost test assemblies |
| **T4.4** | **Full regression suite run** with exit codes pasted in journal: umbrella build, public build, inspectcode both, Shared.Tests **full** + Unit, private service tests that were in CI/pre-reorg, relevant `pnpm` test/typecheck | No â€œUnit-only masks full suite redâ€ |
| **T4.5** | **Baseline / fingerprint:** `check-baselines` (and any backup-era release gates still applicable) green or dispositioned | Consumable surface not silently drifted |
| **T4.6** | **Intentional-drop log:** anything in backup absent by design lists reason + approver (user/plan) â€” empty â€œwe forgotâ€ is illegal | Distinguishes loss from cleanup |

### Cross-cutting (03 + 04 + later 05)

| # | New test / gate | Owner step |
| --- | --- | --- |
| **T5.x** | Export dry-run / public-ID publish fence / dual-suite CI | **05** (not 03/04) â€” do not pretend 04 replaces 05 |
| **Ongoing** | Any bug found in parity or Extensions lands with a **regression pin** in the same change (Â§2) | 03/04 Fixer |

**Plan-Audit / Implementer gate:** step **03** and **04** journals must list the T-ids above as **planned tests** in pre-emptive Â§1 gates; code-audit seat **A** verifies they exist and fail without the new law.

### Step details

#### 01 â€” classify-scaffold

**Main work**

1. Walk every `contracts/*` folder + every package under `server/shared/**` + every service + **every `tools/*` dir** â†’ **ledger** (`public | private | split`) with consumer + emit artifact columns (H3).
2. Instantiate dual-values rows for pre-labeled split catalogs; IP scan remainder; flag anything that would land under `public/` incorrectly.
3. **ADR ledger (100% of `docs/adrs/*`):** each file â†’ **`public` | `private`** (no silent default). Record dense cross-links (e.g. 0020/0023â†’0016). Product/host ADRs (e.g. KeyCustodian lifecycle) â†’ private; framework ADRs â†’ public. Exit: **zero ADRs without a row**.
4. Create empty directory skeleton under `public/` and `private/` (+ placeholder READMEs stating law) including **`public/tools`**, **`private/tools`**, **`public/docs/adrs`**, **`private/docs/adrs`**, **`private/docs/v2`**.
5. Design dual MSBuild props + **CPM/NuGet/stylecop/global.json homes** + dual solutions + pnpm globs (document exact target paths in journal).
6. Produce **rewrite inventory** (mandatory seeds below) incl. Doc Update Map **inbound** links to retired `docs/adrs` / `docs/v2` paths.
7. License pack site inventory (M5); RepositoryUrl plan (M6).
8. Lock step-02 Implementer approach: `git mv` preferred; bulk path rewrite via declared-scope mechanical edit (rules Â§13.2).
9. Note IVT friend-tests pattern (L3); public zero-private-ref check recipe (L4).
10. **Cross-visibility link law:** public docs may link only public paths; private docs may cite public ADRs by id; never put product IP in public docs (content scrub checklist for public ADR set).

**Rewrite inventory seeds (non-exhaustive; 01 expands counts):**

| Cluster | Examples |
| --- | --- |
| MSBuild | `D2*Root` all call sites; Directory.Build*; CPM; stylecop; global.json; NuGet.config |
| Tools (path + property) | splitter Includes; Docker COPY tools; pnpm tools globs; MSBuild tool includes â†’ `$(D2PublicToolsRoot)` / `$(D2PrivateToolsRoot)` |
| Tools **REPO_ROOT / depth-relative** (PA-R2-M8 + **F-R3-F1**) | Every fixed `..` / hardcoded `server/shared` / root `contracts` / root `tools` under: (a) **public tools** (`ts-codegen` `paths.ts`, emitters, tests), (b) **public seeders + lib/** (`seed-publicapi*`, `seed-apiextractor*`, `check-publicapi-shipped`, `lib/*`) dual-mode package roots + KC path, (c) **private scripts** (gen-dev-keys + product gates), (d) MSBuild tool includes. Prefer sentinel **`findRepoRoot`** or env over fixed `..` counts |
| **Husky / monorepo-root hook consumers** (PA-R2-M3) | `.husky/pre-commit` â†’ `tools/scripts/check-publicapi-shipped.mjs`; `.husky/commit-msg` (and peers) â†’ `tools/commit-lint/â€¦`; grep monorepo-root consumers of product tools. Target: `public/tools/scripts/*` + `public/tools/commit-lint/*` (or private where classified). Exit: **no residual active monorepo-root `tools/` path in hooks** |
| Tests | TestPaths sentinels (`server/D2.slnx`, root `contracts/`, `server/shared`) |
| Web | Dockerfile.web, compose web, package name, workspace join |
| CI | `test.yml`, `release-libs.yml` (paths + **remove/fence GH Release of public IDs**), proto-arm, contract-gate `discovery.ts`, path filters |
| Release (public scripts pre-seed â€” PA-R2-M4 + **F-R3-F1**) | release-runner; **public** seeders + **`tools/scripts/lib/**`** (empty-guard + fingerprint-compose) + empty-guard tests + assemble-libs-bundle + check-publicapi-shipped; seeder dual-mode / KC private-consumable baseline ownership |
| Codegen | AdditionalFiles globs; ErrorCodes.Registry; TS registry; TK emit roots; advisory-locks engine public / values private host only (PA-R2-L1) |
| KEEP + **docs/v2 live links** | Step 02: `docs/v2` â†’ `private/docs/v2`; Doc Map **inbound** links retargeted |
| **ADRs** | **100% ledger** public\|private; physical move 02; PUBLIC banner; content scrub; root `docs/adrs` emptied; inbound Doc Map links |
| **Root docs residual (E-R6-F1 â€” locked)** | **Default:** `docs/COMMANDS.md`, `PATTERNS.md`, `TESTS.md`, `PARITY.md`, `SRC_GEN.md`, `TIMESTAMPS.md` stay **private monorepo root `docs/`** (full reference). **Optional:** 01 may carve a **thin** OSS-safe subset into `public/docs/` only with explicit ledger row + no product IP â€” default is **no** full copy to public |

**Exit criteria**

- [ ] Ledger covers 100% of current `contracts/*`, package roots, and `tools/*` (incl. scripts/lib + pre-seed public).
- [ ] **ADR ledger 100%** of `docs/adrs/*` â†’ public|private; zero unclassified; cross-links noted.
- [ ] Contract ledger rows have consumer + emit (incl. protocol-audiences).
- [ ] Dual-values + registry complete; protocol-audiences full-public lock.
- [ ] CPM/props + **`public/D2.Public.slnx`** recorded.
- [ ] Rewrite inventory: husky, REPO_ROOT tools, docs/v2, **ADR move + inbound links**, public scripts+lib.
- [ ] Scaffold includes **`public/docs/adrs`**, **`private/docs/adrs`**, **`private/docs/v2`**, tools dirs.
- [ ] OPEN QUESTIONS for borderline contracts/ADRs answered or user-deferred.
- [ ] Scaffold only â€” old layout still builds; journal inventory counts include **adrs**.

**Deps:** none.  
**Plan-Audit / code-audit:** per mode map above (not Skip).

#### 02 â€” physical-reorg

**Main work**

1. `git mv` packages â†’ `public/packages/{dotnet,typescript}`; services + web â†’ `private/services/*`; contracts per ledger (incl. split files); tools per H1 ledger (**public scripts + `lib/**` + empty-guard tests** â†’ `public/tools/scripts/` cluster; **gen-dev-keys** â†’ `private/tools/scripts/`).
2. **`git mv` dual-docs (R5):** ADRs per 01 ledger â†’ `public/docs/adrs/` or `private/docs/adrs/`; stamp **Visibility: PUBLIC** on every public ADR; content scrub product IP; `docs/v2/**` â†’ `private/docs/v2/`; empty root `docs/adrs/` with redirect README; process stays `docs/dev/`.
3. Rewrite Directory.Build.props chain + **CPM / NuGet.config / stylecop / global.json** homes so `D2*Root` properties resolve; retire single `D2ToolsRoot`/`D2ContractsRoot`/`D2SharedDotnetRoot` as SoT.
4. Rewrite all `ProjectReference` (**property-based primary**), `AdditionalFiles` (**public packages â†’ public contracts only**; advisory-locks values only on private host), `EmbeddedResource`, `Protobuf` AdditionalImportDirs, COPY_MANIFEST paths, TypeSpec regen script paths.
5. Split/reshape solutions: write **`public/D2.Public.slnx`** + umbrella root `D2.slnx`; public isolation zero-private-ref check.
6. Update `pnpm-workspace.yaml` to **public/private tools paths** (no root tools SoT); web joins workspace; root package scripts path-bound.
7. Update Dockerfiles + compose; **Dockerfile.web dedicated redesign**.
8. **Retarget husky / monorepo-root hooks** (PA-R2-M3): `.husky/**` â†’ `public/tools/â€¦` (or private where classified). Exit: no residual monorepo-root `tools/` in hooks.
9. **Tools REPO_ROOT / depth-relative rewrite** (PA-R2-M8 + L3 + F-R3-F1): public tools + seeders + lib; gen-dev-keys private â†’ monorepo root + root `secrets/` only.
10. **Path-sensitive KEEP + meta path rewrite in THIS step (H6 / Â§11.1)** â€” three-form zero residual on the **closed retired-token set** (below), including **inbound** Doc Update Map / AGENTS links to retired `docs/adrs` and `docs/v2`. Allowlist history only: `docs/dev/deliverables/`, `docs/archive/`, `old/`, `docs/wip/` historical journals.
11. **Bulk public source-header rewrite** to SPDX/Apache (PA-R2-L2); private keeps ARR.
12. **Â§14.5 rode-along (M12):** greps on `git status` R/RM set (incl. docs moves).
13. **Â§24.18 (M13):** status + diff + cached --stat co-consistent.
14. Shared.Tests + TestPaths re-baseline; public registry without KC; dual-values files in place.
15. Green: combined `dotnet build` + `dotnet build public/D2.Public.slnx` + `pnpm install`.

**Closed retired-token set (three-form zero residual on KEEP+meta minus history allowlist):**

| Token (active SoT form) | Must not remain as live SoT |
| --- | --- |
| `server/shared` | public packages home |
| `server/services` | private services home |
| `server/web` | private BFF home |
| `server/D2.slnx` | root umbrella + `public/D2.Public.slnx` |
| `server/Directory.Build` | root + dual package props |
| root `contracts/` as SoT | dual `public/contracts` + `private/contracts` |
| monorepo-root product `tools/` as SoT | `public/tools` + `private/tools` only |
| root **`docs/adrs`** as SoT | `public/docs/adrs` + `private/docs/adrs` only |
| root **`docs/v2`** as SoT | `private/docs/v2` only |

**Exit criteria**

- [ ] No residual **active** path SoT for any token in the **closed retired-token set** (history allowlist only).
- [ ] **Physical dual-docs move complete:** ADRs + v2 under new homes; PUBLIC banners on public ADRs; root `docs/adrs` not a live SoT.
- [ ] Path-sensitive KEEP / Doc Update Map / **inbound ADR+v2 links** rewritten **same step**.
- [ ] **Husky/hooks:** zero residual monorepo-root `tools/` paths in active hooks.
- [ ] **Tools REPO_ROOT:** public tools + private scripts resolve monorepo root correctly.
- [ ] Combined solution builds; **`public/D2.Public.slnx`** isolation (no private ProjectRefs).
- [ ] Private services ProjectReference via `$(D2PublicPackagesDotnetRoot)`.
- [ ] Public tree headers SPDX/Apache-compatible.
- [ ] Â§14.5 greps clean; Â§24.18 evidence in journal.
- [ ] Public ErrorCodes.Registry / public tests re-baselined without private catalogs.
- [ ] **No product IP** under `public/docs` (scrub checklist from 01).

**Risk class:** multi-concern cascading path migration â€” **Â§24.0i Sweeping Implementer carve-out** justified (cite multi-concern cascading + file count >>40). Prefer one fat step with sweeping implementer over micro-steps that leave half-broken trees.

**Rejected:** half-move â€œpackages onlyâ€ left green while contracts stay root forever (breaks L9); path KEEP deferred to 04 while tree already moved (H6).

#### 03 â€” extensions-packages

**Main work**

1. Delete / replace transitional `DcsvIo.D2.Private.ProductConstants` and misbranded private twins.
2. Create **1:1** private packages: `DcsvIo.D2.Auth.Abstractions.Extensions`, `DcsvIo.D2.Encryption.Extensions`, `DcsvIo.D2.I18n.Keys.Extensions` under `private/packages/**`.
3. Each Extensions package **ProjectReference**s its public twin; dual-target emit product-only (or publicâˆªprivate as type law requires) into **that** assembly only.
4. Retarget all private consumers to the correct Extensions package(s) only â€” no mega-bag refs.
5. Land **T3.1â€“T3.7** tests (see [New / additional tests](#new--additional-tests-for-new-work-locked)).
6. Green builds + public isolation; no public â†’ Extensions refs.

**Exit criteria**

- [ ] Zero `ProductConstants` (or equivalent multi-concern bag) in tree.
- [ ] Package ids match `DcsvIo.D2.<X>.<Y>.Extensions` law; mapping table in journal matches disk.
- [ ] T3.1â€“T3.7 present and green.
- [ ] Public isolation + private consumers compile.

**Plan-Audit / code-audit:** product K=7 or justified Y = A/C/F.

---

#### 04 â€” backup-parity-regression

**Main work**

1. Read-only scan of `C:\DCSV\Projects\D2-WORX-2026-07-14-BACKUP` vs current tree **area by area** (packages, services, contracts, tools, docs/ADRs/v2, CI, tests, infra, scripts).
2. Produce **parity ledger** with disposition per row: moved / split / intentional drop / **MISSING**.
3. Run **full regression** (not Unit-only mask): umbrella + public build, inspectcode, Shared.Tests full + Unit, private tests that existed, applicable pnpm gates; paste exit codes.
4. Land **T4.1â€“T4.6**; any real gap â†’ fix in this step or explicit user waiver â€” not â€œdefer silently.â€
5. Consider/add any further tests discovered by the scan (lost coverage, new paths).

**Exit criteria**

- [ ] Parity ledger complete; **zero unresolved MISSING**.
- [ ] Full regression green or every red has disposition + fix/waiver.
- [ ] T4.1â€“T4.6 done; intentional-drop log non-empty only for real design drops.

**Plan-Audit / code-audit:** product full K=7 or min Y = A+E+G.

---

#### 06 â€” dual-suites-ci-export (was 03; README once labeled â€œ05â€ during renumber drift)

> **Implement step number = 06.** Journal: `docs/wip/0032-oss-public-private/06-dual-suites-ci-export/journal.md`. Ignore obsolete stub `03-dual-suites-ci-export/`.

**Main work**

1. **Public shared tests (canonical surface):** after reorg, mega suite lives at `public/packages/dotnet/tests/DcsvIo.D2.Tests.csproj` (today: `server/shared/dotnet/tests/â€¦`). Commands:
   - `dotnet test public/packages/dotnet/tests/DcsvIo.D2.Tests.csproj` (or via **`public/D2.Public.slnx`**)
   - Filter traits/namespaces as today (Unit / Integration / ContractFixtures) â€” **retarget paths only**, do not drop coverage lanes.
2. **`.github/workflows/test.yml` â€” public shared-test jobs (required Plan surface):**
   - **Retarget** every current job that runs `DcsvIo.D2.Tests` (Unit, Integration, ContractFixtures â€” see live `test.yml` today) to the **public** csproj path / public solution.
   - **Add or keep a clearly named PR job** whose display name is stable for branch protection, e.g. **`Public shared tests (DcsvIo.D2.Tests)`** (or keep existing display names if branch protection already pins them â€” then only path retarget; document which names are protection-required).
   - That job (or jobs) must run **public contracts + public packages only** â€” no `private/` ProjectReferences, no product test assemblies.
   - **Also** keep/retarget **combined** lanes for private hosts (e.g. Edge tests) as separate jobs under the private monorepo workflow.
3. **Public-only suite package:** same as (1)+(2) plus `pnpm` filters limited to `public/packages/typescript/**` (+ public tools/contract-tests). Document command (COMMANDS.md in 04 polish OK).
4. **Combined suite:** full CI retargeted in `test.yml` â€” dual contract roots discoverable (M4); private service tests remain private monorepo only.
5. **Public-parity in private:** private monorepo CI runs the **same** public shared-test job definition (or identical command) that `d2-public` will run â€” so green private main implies public remote will pass **before** export.
6. **Export dry-run:** `workflow_dispatch` (script under `public/tools` or `private/tools` as classified) that:
   - assembles/export-checks only `public/**` (+ declared allowlist)
   - fails if any path outside allowlist or IP fence patterns match
   - does **not** push to a remote by default
7. **release-runner:** public discovery = `public/packages/**` only; private list may include KC; **hard-fail** private publish of public IDs to nuget.org/npmjs without d2-public policy (H5).
8. **release-libs.yml / seeders / assemble-libs (public tools):** path + license rewrites; private monorepo **must** pack/upload-artifact only â€” **no** `gh release create` of public package IDs (PA-R2-M5); real Release only on `d2-public`.
9. **Â§26.20 baselines (PA-R2-M9 + F-R3-F1 ownership):** after seeders retarget (leaves + **`public/tools/scripts/lib/**`**), **re-seed** `.release-fingerprint` (+ promote PublicAPI.Unshipped where required):
   - **Public packages** â†’ public seeder path + `pnpm --filter release-runner check-baselines` **green** on public discovery.
   - **KC / private consumables** â†’ dual-mode seeder (or named private seeder) on monorepo; not unowned after reorg.
   Cite Â§26.20 in journal / pre-emptive gates. (Prefer this step with release-runner; may start seeder retarget in 02 but **green check-baselines is a 03 exit**.)
10. **contract-gate:** dual roots; test that private-only spec changes are detected on combined suite (M4).
11. Land **Â§1.22 adversarial matrix** tests (H7 table below) â€” incl. **`PrivatePublishFence_NoGitHubReleaseOfPublicIds`**.
12. **Future remotes (documented for H-gates, land stubs or comments in 03 where useful):**
    - `d2-public` will host its own `test.yml` (or equivalent) that runs **only** public shared tests + public build â€” copy/adapt from the public-only jobs proven on the monorepo.
    - `d2-private-worx` will host the **combined** workflow (public shared jobs + private service jobs).

**Exit criteria**

- [ ] **`test.yml` runs public shared tests** against `public/packages/dotnet/tests/DcsvIo.D2.Tests` (or **`public/D2.Public.slnx`**) â€” Unit/Integration/ContractFixtures coverage retained or explicitly mapped.
- [ ] Public shared-test job(s) green on private monorepo CI (public-parity).
- [ ] Combined suite green (or known pre-existing failures explicitly listed â€” none silently absorbed).
- [ ] Export dry-run fails on deliberate product path / IP fixture (negative test).
- [ ] release-runner public `--list` excludes KC; private publish fence hard-fails public IDs to public feeds **and** no GH Release of public IDs from private monorepo.
- [ ] **`pnpm --filter release-runner check-baselines` green** after fingerprint re-seed (Â§26.20).
- [ ] Adversarial matrix surfaces covered by named tests (incl. GH Release fence).
- [ ] Plan/checklist documents that **human** creates remotes and wires CI/secrets (**H1â€“H9**) â€” not agent auto-create.

#### 06 â€” law-docs-cutover (was 04)

**Main work** (path-sensitive KEEP already done in 02)

1. **rules.md** new predicates: Public-tree IP fence; dual-suite / export-gate; codegen dual-root; publish ownership; dependency direction privateâ†’public only.
2. **AGENTS.md** + **process.md** lockstep (Â§11.32).
3. **public/** root: Apache-2.0 LICENSE, README, thin CONTRIBUTING; **`public/docs/adrs/`** polish + PUBLIC banners (moves in 02).
4. **Monorepo root README:** private map (public/ + private/ + docs/dev); license clarification ARR.
5. **COMMANDS.md**, **docs/README.md**, retire/redirect emptied `server/` README, PATTERNS/SRC_GEN path polish.
6. **Required ADR:** â€œPublic/private monorepo layout + dual-repo cutoverâ€ (M11) â€” home **`public/docs/adrs/`** if framework-facing **or** `private/docs/adrs/` if product-only process; **never** emptied root `docs/adrs` as live SoT. Visibility banner required if public.
7. **V2 pointer** under **`private/docs/v2/`** (or classified): active layout SoT; Auth Core branch coordination.
8. Finalize **Human cutover checklist** as operator doc; H1â€“H9 polish (create + wire remotes).
9. Closed KEEP inventory residual **non-path** tone only (law wording); if any path residual found, treat as **02 miss / sister fix**, not â€œleave for laterâ€.

**Exit criteria**

- [ ] New law predicates proposed; meta-docs lockstep.
- [ ] Public root docs present and free of private product IP.
- [ ] **ADR** for public/private layout under **`public/docs/adrs/` or `private/docs/adrs/`** (not emptied root `docs/adrs`) + **V2 pointer** under `private/docs/v2/`.
- [ ] Human checklist complete and ordered.
- [ ] Proposed rules.md additions listed for SHIP approval.

---

## Closed KEEP inventory (M11) â€” path rewrite in step 02

Path-sensitive surfaces (three-form zero residual on **closed retired-token set** â€” prose, links, code fences â€” Â§11.39 + PA-R2-M7):

| Surface |
| --- |
| AGENTS.md structure map + Doc Update Map rows |
| `docs/dev/process.md`, `docs/dev/rules.md` + `docs/dev/rules/*` path examples |
| `docs/dev/harness-runtimes.md`, `docs/dev/codebase-memory.md` if path-bound |
| `docs/COMMANDS.md`, `docs/PATTERNS.md`, `docs/TESTS.md`, `docs/PARITY.md`, `docs/SRC_GEN.md`, `docs/TIMESTAMPS.md` |
| `docs/README.md`, root `CONTRIBUTING.md` |
| **`docs/adrs/**`** â€” reclassified â†’ `public/docs/adrs` or `private/docs/adrs`; PUBLIC banner; resolving links retargeted in **02**; no leftover mixed root ADR SoT |
| **`docs/v2/**`** â€” move to **`private/docs/v2/`**; live links retargeted in **02**. Tokens: `server/shared`, `server/services`, `server/web`, `server/D2.slnx`, â€¦ |
| **`public/docs/**`** â€” new OSS doc surface; no product IP |
| **`private/docs/**`** â€” product ADRs + v2; never export |
| **Root README** â€” private map to public/ private/ docs/dev |
| `contracts/README.md` â†’ dual `public/contracts/README.md` + `private/contracts/README.md` |
| tools READMEs under new public/private tools homes |
| Parent overviews / `server/README.md` retire or redirect |
| Per-lib/per-service READMEs moved with code |
| CI workflow comments that name old roots |
| **`.husky/**`** path bindings to product tools (PA-R2-M3) |

**Closed retired-token set (repeat for implementer):** `server/shared` Â· `server/services` Â· `server/web` Â· `server/D2.slnx` Â· `server/Directory.Build` Â· root `contracts/` as SoT Â· monorepo-root product `tools/` as SoT Â· root **`docs/adrs`** Â· root **`docs/v2`**.

**Allowlist residual history (not rewritten to fake present SoT):** `docs/dev/deliverables/**`, `docs/archive/**`, `old/**`, `docs/wip/**` historical journals. **`docs/v2/**` is NOT on this allowlist for live links** â€” only clearly past-tense historical narrative may keep old path names without retargeting.

---

## CI design (target)

| Lane | Runs where | Scope | Trigger |
| --- | --- | --- | --- |
| **Public shared tests** | Private monorepo (`test.yml`) **and** future `d2-public` | **`DcsvIo.D2.Tests`** (+ public TS filters as applicable) under `public/packages/**` only | PR/push â€” **required gate** on private monorepo after reorg; **sole default test surface** on `d2-public` |
| **Public-only build** | Same | **`public/D2.Public.slnx`** / public packages build | With public shared tests or sibling job |
| **Combined** | Private monorepo only (`d2-private-worx` later) | public shared tests **+** private service tests (Edge, â€¦) | PR/push (current PR workflow retarget) |
| **Export dry-run** | Private monorepo | Assemble/validate export tree; **no push** by default | `workflow_dispatch` (+ optional schedule later) |
| **Release libs** | **`d2-public` owns real publish + GH Release**; private monorepo **must** pack/upload-artifact only (PA-R2-M5) | Public package IDs only; hard-fail private â†’ public feeds **or** private GH Release of public IDs | `workflow_dispatch` (human) |
| **Contract gates** | Dual trees on private monorepo; public-only specs on `d2-public` | Public specs + private specs both discoverable on private | PR â€” retarget |

### `test.yml` concrete requirement (step 03 â€” non-optional)

Today (pre-reorg) `test.yml` already runs e.g.:

- `dotnet test server/shared/dotnet/tests/DcsvIo.D2.Tests.csproj` (Unit / Integration / ContractFixtures filters)
- Edge private tests separately

**After reorg the Plan requires:**

| Requirement | Detail |
| --- | --- |
| **Retarget** | All `DcsvIo.D2.Tests` invocations â†’ `public/packages/dotnet/tests/DcsvIo.D2.Tests.csproj` (or `dotnet test` on **`public/D2.Public.slnx`** filter) |
| **Named public lane** | At least one job whose purpose is obviously **public shared tests** (keep stable **display names** if branch protection depends on them; otherwise name e.g. `Public shared tests (unit)` / `â€¦ (integration)` / `â€¦ (contract fixtures)`) |
| **No private deps** | Those jobs must not restore/build `private/services/**` as test hosts |
| **Parity** | Same job steps (or shared composite action) are what get copied into **`d2-public`** workflow at human H5 |
| **Private hosts** | Edge (and future service) jobs remain **separate** jobs on private monorepo only |

**Private publish fence (L7 + PA-R2-M5 â€” hard `must`):** private workflows **must not** publish `DcsvIo.D2.*` / `@dcsv-io/d2-*` public IDs to nuget.org/npmjs **and must not** `gh release create` (or equivalent) of those IDs. **Must** pack/upload-artifact only on the private monorepo. Fail-closed if publish env **or** GH Release path would attach public IDs from private (do not rely solely on absent NUGET_API_KEY/NPM_TOKEN â€” `GITHUB_TOKEN` is always present).

---

## Human gates â€” create + wire NEW remotes, first export, sunset D2-WORX

**Agents do not create GitHub remotes, org secrets, or branch protection.** The deliverable **wires the monorepo** and **documents** every human step. Cutover is **human intervention** after H0 green.

Ordered checklist (**who / what / order**):

| # | Who | What | When / order | Notes |
| --- | --- | --- | --- | --- |
| **H0** | Dev team / agents | Finish **wiring on `n/oss`** (this deliverable) | **First** | Layout + dual suites + **`test.yml` public shared tests** + export dry-run + law |
| **H0.5** | **Human** | **Full backup** (push `n/oss` + tags + optional zip incl. gitignored wip) | After Plan CLEAN, **before** Implementer bulk move | Recover pre-reorg tip; exclude secrets from cloud zips |
| **H1** | **Human** | **Create** GitHub remote **`d2-public`** (empty or seed-ready) | After H0 green | **Apache-2.0**; description â€œD2 public packages + contractsâ€; default branch `main` |
| **H2** | **Human** | **Create** GitHub remote **`d2-private-worx`** | After H0 green (âˆ¥ H1) | **All rights reserved**; full monorepo SoT (`public/` + `private/`) |
| **H3** | Operator + CI | **First export dry-run** on monorepo (`workflow_dispatch`) | After H0 | Must pass **public shared tests** + public-only build + IP fence |
| **H4** | **Human / operator** | **First real export** â†’ push **`public/` only** into **`d2-public`** (manual mirror / gated script â€” **not** every push) | After H1 + H3 | Verify remote tree = export allowlist; no `private/` |
| **H5** | **Human** | **Wire `d2-public` CI + release** | After H4 | Copy/adapt **public shared-test jobs** + public build from monorepo `test.yml`; add **release** workflow (release-runner â†’ nuget.org/npmjs); set **publish secrets** (NUGET_API_KEY, NPM_TOKEN, etc.) **only** on `d2-public`; branch protection requires public shared-test job names |
| **H6** | **Human** | **Wire `d2-private-worx`** | After H2 (+ ideally after H0) | Push full monorepo (or clone/mirror from current remote); port **combined** `test.yml` (public shared + private Edge/â€¦ jobs); **no** public package publish secrets (or fence hard-fail); branch protection as desired |
| **H7** | **Human** | Preserve **`n/auth-core`** (and other live product branches) â€” merge or pin SHAs | **Before** freeze of D2-WORX | No silent loss of Auth Core commits |
| **H8** | **Human** | Merge **`n/oss`** into **`d2-private-worx` `main`**; point day-to-day work at private remote | After H0â€“H7 | Live SoT = private remote |
| **H9** | **Human** | Park/archive **`D2-WORX`**: README â†’ â€œarchived; live OSS = d2-public; live product = d2-private-worxâ€; leave tips/links working; **forever archive** product work off old remote | **Last** | **Keep public URL** for external links (L15); document final SHAs |

**Wire-up detail (H5 / H6) â€” human checklist bullets**

| Remote | Human must |
| --- | --- |
| **d2-public** | Enable Actions; add `test.yml` (public shared tests + build only); add release workflow; configure NuGet/npm secrets; set branch protection on public shared-test job **display names**; set default LICENSE Apache-2.0; optional Pages/docs later |
| **d2-private-worx** | Enable Actions; combined `test.yml`; **do not** attach nuget.org publish for `DcsvIo.D2.*`/`@dcsv-io/d2-*` (or enable fence); secrets for product deploys only as needed; protect `main` |
| **Both** | Deploy keys / fine-grained PATs only if export script pushes (prefer human PR to d2-public first cut) |

**Deliverable-complete (agent):** H0 wiring criteria below all YES (including **test.yml public shared tests**).  
**Cutover-complete (human):** H1â€“H9 (create remotes + wire CI/secrets + first export + archive).

---

## Risks (rules-category walk)

| Area | Risk | Mitigation |
| --- | --- | --- |
| **Â§1 tests** | Public suite incomplete; private only tested | Dual suites + parity job + adversarial matrix (H7) |
| **Â§1.22** | Structural surfaces untested | Named matrix under Pre-emptive gates |
| **Â§8 tooling** | Broken paths; accidental services | Path rewrite inventory; never start services |
| **Â§9 layers** | Cycles publicâ†’private | Law: privateâ†’public only; property-based refs |
| **Â§9.34** | Depth-relative tool includes break on move | `D2PublicToolsRoot` / forbid `..\tools` |
| **Â§10 secrets** | Secrets / gen-dev-keys under public | private/tools only; export allowlist |
| **Â§11 docs** | KEEP drift if deferred past move | **Path KEEP in step 02** (H6); three-form Â§11.39 |
| **Â§11.1** | Docs later multi-step | Same fat step as `git mv` for path-sensitive |
| **Â§13 permissions** | Bulk mv without scope; commits | Declare bulk scope; no commit without user |
| **Â§14 verbiage** | Phase chatter / conversation IDs on move | Â§14.5 greps step 02 |
| **Â§23 config** | Compose env paths | Retarget; no secrets in public |
| **Â§24 audit** | FR_FULL; huge diff; index/tree | Fat steps; Â§24.18 evidence; dirty-only |
| **Â§24.18** | Rename vs content fix split | status + diff + cached --stat co-consistent |
| **Â§26 codegen** | Public registry still globs private | H2 strategy; dual roots; re-baseline |
| **Broken ProjectReferences** | Depth change | Property-based primary (M7) |
| **Dual contracts** | Silent wrong tree | Split properties; no single D2ContractsRoot end-state |
| **pnpm workspace** | Web outside / missed globs | Web joins; public tools globs |
| **Mega tests** | Private tests in public | Ledger; service tests private |
| **release-runner** | KC as public OSS; private GH Release | H5 discovery + PA-R2-M5 pack-only + named fence tests |
| **License mixup** | PolyForm in Apache packs | M5 inventory + public LICENSE only |
| **Auth-core loss** | Archive drops commits | H7 checklist (preserve branches) |
| **Product in catalogs** | scopes/domains/messages | H3 pre-split + negative tests; protocol-audiences full-public (M1) |
| **CPM missing** | Public-only build fails | H4 props homes |
| **Tools depth / hooks** | Broken REPO_ROOT; husky still root tools | M3 husky + M8 REPO_ROOT inventory |
| **Â§26.20 baselines** | Fingerprints stale after path move | M9 re-seed + check-baselines exit |
| **docs/v2 / ADR 404** | Live links after move | M6 + L4 retarget in 02 |

---

## Pre-emptive gate checks (deliverable-level)

| Gate | Plan |
| --- | --- |
| **Test coverage** | Public packages: existing tests move with them; add **fence tests** (export allowlist, no private path in public tree, release discovery, registry public-only, dual-suite). No new product handlers â€” coverage is structural. |
| **Conventions** | Path props not magic strings; American English KEEP; no phase IDs in public docs |
| **PII** | No new logging; do not grep `secrets/` |
| **Layer** | Public packages remain service-agnostic; product hosts private-only |
| **Â§13.15 do-it-now** | Dual suites + export dry-run **in this deliverable** (not â€œlater when remotes existâ€) |
| **Codegen** | Public packages AdditionalFiles only under `public/contracts`; private KC/typespec under `private/contracts`; registry strategy H2; advisory-locks values private host only |
| **Â§26.20 baselines** | After consumable path move: re-seed `.release-fingerprint` (+ PublicAPI promote where required); `pnpm --filter release-runner check-baselines` green (step 03 exit â€” PA-R2-M9). **Ownership (F-R3-F1):** public packages on public seeder path; KC/private consumables via dual-mode (H5-like) or named private seeder â€” never unowned. Public cluster includes `tools/scripts/lib/**` (Â§26.23/Â§26.24). |
| **Â§1.22 adversarial matrix** | See table below |

### Â§1.22 adversarial coverage matrix (H7)

| Surface | Â§1.2 categories | N/A rationale | Planned test names (step 03 unless noted) |
| --- | --- | --- | --- |
| **Export dry-run / allowlist** | Happy path; garbage path; path outside allowlist; empty tree; oversized path list; malformed allowlist entry; wrong-type path; cross-field allowlist vs IP pattern; error propagation; idempotency of dry-run (no push); concurrency N/A if single job | Concurrency N/A for dispatch-only gate unless parallel jobs added | `ExportDryRun_AllowsPublicOnly`; `ExportDryRun_FailsOnPrivatePath`; `ExportDryRun_FailsOnProductIpFixture`; `ExportDryRun_DoesNotPush` |
| **release-runner discovery** | Happy public list; missing root; empty packages dir; wrong-type config; KC present in private list only; error when public mode sees private path; idempotent `--list` | â€” | `ReleaseRunner_PublicList_ExcludesKc`; `ReleaseRunner_PublicList_FindsPublicPackages`; `ReleaseRunner_PrivateList_MayIncludeKc`; `ReleaseRunner_RetargetedRoots` |
| **IP fence (product under public)** | Happy clean public; product filename/content fixture; empty; whitespace-only match N/A; wrong-type; cross-field catalog row re-entry | â€” | `IpFence_FailsOnProductCatalogRow`; `IpFence_FailsOnPrivateContractPath`; `PublicTree_NoKeyCustodianCodes` |
| **Private publish fence** | Happy pack-without-publish; publish env set + public ID â†’ fail; wrong registry URL; **GH Release attach of public IDs from private** â†’ fail; idempotent guard | Real nuget.org publish N/A in CI (mock/env); real GH network N/A (static workflow assert or dry-run mock OK) | `PrivatePublishFence_HardFailsPublicIdsToPublicFeeds`; `PrivatePublishFence_AllowsArtifactOnlyPack`; **`PrivatePublishFence_NoGitHubReleaseOfPublicIds`** (PA-R2-M5) |
| **Dual-suite commands** | Public-only green path; combined includes private; public-only does not require private; filter miss; wrong working directory | â€” | `PublicOnlySuite_BuildsWithoutPrivateRefs`; `CombinedSuite_DiscoversDualContractRoots`; `PublicParity_MatchesPublicOnlyCommand` |
| **`test.yml` public shared tests** | Happy path Unit/Integration/ContractFixtures; wrong csproj path; private ref sneaks into public job; display-name drift vs branch protection | Real GH branch-protection click = human H5 | `TestYml_PublicSharedTests_TargetsPublicCsproj` (path assert or dry workflow lint); CI job green on monorepo = acceptance |
| **Dual-values / registry** | Public registry excludes KC; private still generates KC; merge at host; negative product row in public package | â€” | `ErrorCodesRegistry_Public_ExcludesKeyCustodian`; `KcGenerator_PrivateContractsOnly`; `PublicPackage_AdditionalFiles_PublicContractsOnly` (step 02/03) |

---

## Success criteria â€” deliverable â€œwired upâ€ (all YES)

Agent claims complete only when every box is YES with evidence:

- [ ] `public/` and `private/` trees exist per L1â€“L3/L11; `server/shared`, root `contracts/`, monorepo-root product `tools/` no longer active SoT.
- [ ] **IP law:** no product host, product TypeSpec, KC error-codes, secrets, or private-only contracts under `public/`.
- [ ] **Dual-values:** pre-split catalogs split; public packages emit public values only; negative tests green.
- [ ] **Registry:** public ErrorCodes.Registry / `@dcsv-io/d2-error-codes-registry` = public catalogs only; private KC via private path.
- [ ] **Public docs:** `public/` root README + Apache-2.0 LICENSE + CONTRIBUTING **and** `public/docs/adrs/` with **Visibility: PUBLIC** on each ADR; no product IP under `public/docs`.
- [ ] **Private docs:** product ADRs under `private/docs/adrs/`; v2 under `private/docs/v2/` (or classified); process under `docs/dev/`.
- [ ] **Monorepo root** private map README; export = **`public/**` only**.
- [ ] **MSBuild** dual roots + CPM homes; **`public/D2.Public.slnx`** builds with **zero private ProjectReference paths**; combined builds (zero-warnings policy).
- [ ] **pnpm workspace** resolves public TS + private services (**incl. web**) + **public/private tools** (no root tools SoT); husky/hooks point at public/private tools only.
- [ ] **Codegen:** public packages generate only from `public/contracts`; private codegen emits only under `private/**`.
- [ ] **Public-only suite** green (documented command; `public/D2.Public.slnx`).
- [ ] **`.github/workflows/test.yml` runs public shared tests** (`DcsvIo.D2.Tests` under `public/packages/â€¦`) as an explicit CI lane (Unit/Integration/ContractFixtures coverage retained or mapped).
- [ ] **Combined suite** green (documented command); private host tests (Edge, â€¦) remain separate jobs.
- [ ] **Public-parity** = same public shared-test command/job shape that will land on `d2-public`.
- [ ] **Export dry-run** gated; negative IP/path tests green.
- [ ] **release-runner** public list excludes KC; **hard** private publish fence for public IDs (**nuget/npm + no GH Release**); private monorepo pack/artifact only.
- [ ] **Â§26.20** `check-baselines` green after path-move re-seed (public packages on public path; KC/private consumables dual-mode or named private seeder â€” **F-R3-F1**).
- [ ] **Dual binaries** call-out documented; pin-mode Docker **out of scope** (M10).
- [ ] **Path-sensitive KEEP** (+ docs/v2 live links, ADR links, closed retired-token set) rewritten in step 02; **new law** + **required ADR** + V2 in step 04.
- [ ] **Â§24.18** evidence captured on move step; FR_FULL clean + completeness attestation ready.
- [ ] **Human cutover checklist H1â€“H9** present (create + **wire** `d2-public` / `d2-private-worx`, secrets, branch protection, first export, archive D2-WORX); Auth Core preservation called out; **agents do not create remotes**.
- [ ] **No auto-commit**; no destructive git; branch `n/oss` contains the work.

**Not required for agent â€œwired-upâ€:** remotes already exist; human has completed H1â€“H9; dual-binary pin-mode images.  
**Required for cutover-complete:** human H1â€“H9.

---

## OPEN QUESTIONS

**None blocking** (resolved 2026-07-14 â†’ L13â€“L18):

| ID | Resolution |
| --- | --- |
| **OQ-1** | Split product-shaped catalog content to **private now**. Dual **values** files; **`$schema` in public** when shared/both ends; private-only schema only for private-only kinds. Pre-labeled split catalogs locked in dual-values table (H3). |
| **OQ-2** | Remotes: **`d2-public`** + **`d2-private-worx`**. |
| **OQ-3** | **`D2-WORX` remains public archive URL** (links); live private = `d2-private-worx`; live OSS = `d2-public`. |
| **OQ-4** | Private: **All rights reserved**. Public: **Apache-2.0**. |
| **OQ-5** | **Split tools** â†’ `public/tools` + `private/tools` (`.gitkeep` if empty). **Physical SoT only** â€” no monorepo-root tools product home after reorg (H1). |
| **OQ-6** | Empty dirs OK with **`.gitkeep`** either side. |
| **OQ-7** | **Root umbrella `D2.slnx`** (local/private CI) + **`public/D2.Public.slnx`** (OSS/public CI) â€” path locked PA-R2-M2. |

---

## Approach + rejected alternatives (deliverable)

| Approach | Verdict |
| --- | --- |
| **In-monorepo dual tree + gates first, remotes later** | **Chosen** â€” matches L10; validates before irreversible remote cutover |
| Split remotes first, then move code | Rejected â€” unvalidated dual CI, high thrash |
| License-only OSS without tree split | Rejected â€” export would still ship product paths |
| Filter export via .gitattributes without physical split | Rejected â€” agents/humans will violate IP law; ProjectReferences stay entangled |
| Micro-steps per package | Rejected â€” process fat-step law; half-tree states more dangerous than one sweeping reorg step |
| Keep monorepo-root `tools/` as SoT with thin re-export | **Rejected** (H1) â€” contradicts L11 export of public tools; dual SoT |
| Public registry globs all contracts including KC | **Rejected** (H2) â€” breaks public pack IP |
| Defer path KEEP to step 04 after `git mv` | **Rejected** (H6 / Â§11.1) |

---

## Completeness attestation (SHIP)

I attest that this deliverable's process integrity has been verified against the
deliverable completeness checklist in `rules.md` (Deliverable completeness
checklist section). Every box is YES with the citations below. The deliverable
is ready for user **REVIEW**.

| Gate | Citation |
| --- | --- |
| Steps 01â€“07 journals CLEAN | `01`â€¦`07`/journal.md Status + Latest 0 FINDING (02 R8, 07 R2) |
| FR_FULL CLEAN | `final-review/journal.md` Round 4 CLEAN Â· tip `5019669d8` |
| Build / inspect / CI | local `dotnet build` 0W/0E Â· `jb` 2026.1.4 COUNT=0 Â· PR #57 green |
| Dual-tree law landed | Â§7.7a, Â§8.8â€“Â§8.10, Â§9.48â€“Â§9.49, Â§11.46â€“Â§11.47, Â§26.25â€“Â§26.26 in `docs/dev/rules/` |
| Versioning first-cut lock | First registry cut = **0.1.0**; later = release-runner â€” `docs/dev/human-cutover-oss-public-private.md` |
| Human remotes | H1â€“H9 operator; agents do not create remotes |

**Journal homes (local, gitignored):**  
`docs/wip/0032-oss-public-private/{01-classify-scaffold,02-physical-reorg,03-extensions-packages,04-backup-parity-regression,05-public-package-identity,06-dual-suites-ci-export,07-law-docs-cutover,final-review}/journal.md`

## Kinds-of-misses log (distilled)

| Class | Where it bit | Rule / fix |
| --- | --- | --- |
| Stale path greps after reorg | tests, KEEP, gates.sh, guards | Sister-sweep form-(b) backticks; dual-tree path law |
| Inspectcode false mass findings | jb **2025.3.x** on net10 | Pin **â‰¥2026.1.x** (COMMANDS/AGENTS) |
| Public tests needing private homes | typespec-emitters | `PUBLIC_ONLY` gate + monorepo-only suites |
| False step CLEAN map | FR prereq claimed 01â€“07 while 02 open | Honesty map before FR open |
| Prettier blocking commit | staged TS/mjs | `prettier --write` before cycle-commit |

## Proposed rule additions to rules.md (SHIP)

**Landed during step 07 (not pending approval):** public IP fence, dual suite /
export, codegen dual-root, publish ownership, dependency direction, tools dual-root,
dual-header Â§7.7a â€” see rules Â§7.7a, Â§8.8â€“Â§8.10, Â§9.48â€“Â§9.49, Â§11.46â€“Â§11.47, Â§26.25â€“Â§26.26.

**No further rule candidates blocked for SHIP.** Inspectcode CLT pin is doc/COMMANDS
discipline (not a new rules.md Â§ unless user wants it promoted).

---

## Living State (append-only EXECUTE decisions)

| When | Decision |
| --- | --- |
| 2026-07-14 | Deliverable-level Plan authored on `n/oss` by grok-d2-planner. Status PLAN. Next: user review of OPEN QUESTIONS + Plan-Audit product K=7 before Implementer. |
| 2026-07-14 | User answered OQ-1â€¦OQ-7. Locked L13â€“L18 (contract dual values + schema placement; remote names; D2-WORX archive URL; licenses; tools split; gitkeep; dual solutions). L11 revised (tools under public/private). Next: user confirm L18/OQ-7 thoughts â†’ Plan-Audit product K=7 â†’ EXECUTE. |
| 2026-07-14 | **Plan-Audit R1 = AMEND-FIRST** (H1â€“H7, M1â€“M14, L1â€“L4). Plan-amender folded all findings into Plan body without weakening L1â€“L18. Locks added/clarified: tools physical dual SoT only + pnpm globs + D2Public/PrivateToolsRoot; ErrorCodes.Registry public-only + private merge; dual-values pre-split table; CPM/props homes; publish discovery fence; KEEP path rewrite in step 02; Â§1.22 matrix; audit mode map + Plan-Audit results home; dual-binary pin-mode out of scope this deliverable. **Next:** dirty (or full K=7) Plan-Audit R2 â†’ CLEAN only then user execute OK â†’ step 01 Implementer. Do **not** open Implementer on AMEND-FIRST. |
| 2026-07-14 | User: (1) Plan must put **public shared tests** (`DcsvIo.D2.Tests`) in **`test.yml`** as explicit public lane + parity for `d2-public`. (2) Plan must include **human intervention** to **create and wire** NEW `d2-public` + `d2-private-worx` (CI, secrets, branch protection, first export). Expanded step 03, CI design, success criteria, human gates â†’ **H1â€“H9**. |
| 2026-07-14 | **Plan-Audit R2 = AMEND-FIRST** (0H / 9M / 5L: PA-R2-M1â€¦M9, PA-R2-L1â€¦L5). Plan-amender folded all findings into Plan body without weakening L1â€“L18 (L7/L18 clarified to still satisfy user intent). Locks: **auth-protocol-audiences full-public** (M1); **`public/D2.Public.slnx` single path** (M2); husky rewrite seed (M3); public release scripts pre-seed (M4); private **must** pack/artifact only + no GH Release of public IDs (M5); docs/v2 live-link retarget (M6); closed retired-token set (M7); tools REPO_ROOT inventory (M8); Â§26.20 check-baselines exit (M9); advisory-locks emit rule (L1); Â§7.7 dual-header (L2); gen-dev-keys root resolve (L3); ADR links in 02 (L4); H1â€“H9 + `plan-audit-rN/` homes (L5). **Next:** dirty Plan-Audit **R3** seats **Aâ€“F** (+ sister-blast tools/publish) â†’ CLEAN only then user execute OK â†’ step 01 Implementer. Do **not** open Implementer on AMEND-FIRST. |
| 2026-07-14 | **Plan-Audit R3 = AMEND-FIRST** residual **F-R3-F1 (M)** only (other R2 residuals ABSENT on dirty Aâ€“F). Plan-amender fold: (1) pre-seed public seeder cluster includes **`public/tools/scripts/lib/**`** live modules (`publicapi-empty-guard`, `apiextractor-empty-guard`, `source-fingerprint-compose` + `.d.mts`) + empty-guard unit tests â€” not leaf scripts alone (Â§26.23/Â§26.24 relative imports); (2) Â§26.20 baseline ownership = **dual-mode** like H5 (public packages on public path; KC/private consumables monorepo dual list or named private seeder â€” strike â€œpublic packages onlyâ€ as sole ownership); (3) export allowlist includes public `tools/scripts/lib` when public release path needs them. **Next:** dirty Plan-Audit **R4** seat **F** (+ sister C/D export allowlist) â†’ CLEAN only then user execute OK â†’ step 01 Implementer. Do **not** open Implementer on AMEND-FIRST. |
| 2026-07-14 | **Plan-Audit R4 = CLEAN** (0 FINDING). Dirty F+C+D READY; A/B/E/G re-cited READY from R3. Aggregate: `plan-audit-r4/r4-aggregate.md`. **Plan-Audit loop TERMINATED CLEAN.** Status **PLAN READY**. Next: **user execute OK** â†’ step 01 Implementer. Do not open Implementer without explicit execute authorization. |
| 2026-07-14 | **Dual-docs fold (user LGTM):** L1/L4/L11 â€” `public/docs` (framework ADRs + PUBLIC banner) + `private/docs` (product ADRs + v2) + root `docs/dev` (process); root README private map; export = `public/**` only; no symlink SoT. |
| 2026-07-14 | **Plan-Audit R5 = AMEND-FIRST** (E/C/G dual-docs). Amender folded dual-docs mechanics. |
| 2026-07-14 | **Plan-Audit R6** seat G+C READY; **E AMEND** E-R6-F1. Amender locked PATTERNS/COMMANDS/â€¦ default private monorepo `docs/`. |
| 2026-07-14 | **Plan-Audit R7 = CLEAN** (E READY 0 FINDING; G+C re-cited R6). Aggregate: `plan-audit-r7/r7-aggregate.md`. **Plan READY.** **Next: human H0.5 backup** â†’ user execute OK â†’ step 01. Do not Implement without backup + execute OK. |

---

## Sources consulted (Plan research)

| Path | What it added |
| --- | --- |
| User dispatch locks | End-state tree, dual suites, release ownership, codegen law, human gates, public docs vote |
| `server/Directory.Build.props` | `D2RepoRoot` / `D2ContractsRoot` / `D2SharedDotnetRoot` / `D2ToolsRoot` / source-gen roots |
| `server/shared/dotnet/Directory.Build.props` | Packable PolyForm LICENSE packing from repo root |
| `tools/release-runner/src/manifest-loader.ts` | Hardcoded `server/shared/**` + KC client discovery |
| `pnpm-workspace.yaml` | Current package globs |
| `.github/workflows/test.yml` / `release-libs.yml` | CI lanes; release is workflow_dispatch pack |
| `contracts/README.md` | Full contract inventory for public/private ledger |
| `server/D2.slnx` | ~95 projects under `server/` |
| `docs/dev/process.md` PLAN | Fat-step law; README template |
| AGENTS.md structure map | `server/` / `contracts/` / `tools/` roots to rewrite |
| `n/oss` @ `4995b4bc` | Branched from auth-core tip; structure work isolation |
| `plan-audit-r1/r1-aggregate.md` | R1 consolidated findings H1â€“H7, M1â€“M14, L1â€“L4 |
| `plan-audit-r2/r2-aggregate.md` | R2 consolidated findings PA-R2-M1â€¦M9, L1â€¦L5 |

---

## Next orchestrator actions

1. ~~R1â€“R4~~ done; dual-docs fold + **R5 amends** (this pass).
2. ~~R5â€“R7 dual-docs~~ **CLEAN** (`plan-audit-r7/r7-aggregate.md`).
3. **Human H0.5 backup** (push `n/oss` + tag + optional zip).
4. After backup: **user execute OK** â†’ step 01 Implementer.
5. **Do not** Implement without: Plan READY + H0.5 + execute OK.

