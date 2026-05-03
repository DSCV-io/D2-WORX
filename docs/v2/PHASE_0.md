<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_0.md — Wipe + v2 Foundation

**Purpose**: tracking doc for the v1 → v2 wipe + Phase 0 (shared libraries) execution. This doc lives only until Phase 0 ships, then gets archived.

**Architectural source of truth**: [V2.md](V2.md). This doc is execution detail.

---

## Status snapshot

Phase 0 has four execution stages. The **Granular checklist** column links to the section that breaks each stage into individual line items — flip those and update the stage status here when each stage's checklist completes.

| Stage | Status | Granular checklist |
|---|---|---|
| 1. Pre-wipe checkpoint (tag `pre-v2-wipe`) | ✅ Complete | (single git tag — no detail checklist) |
| 2. Wipe commit (single commit on `nova` branch) | ✅ Complete | [Definition of done (wipe commit)](#definition-of-done-wipe-commit) |
| 3. Documentation pass (placeholder READMEs + extracted patterns) | ✅ Complete | [Definition of done (documentation pass)](#definition-of-done-documentation-pass) |
| 4. Shared library implementation (11 libs per V2.md §4 Phase 0) | 🔄 **In progress — LLM: read this stage's per-lib checklist below before any work** | [Per-library checklist (Stage 4)](#per-library-checklist-stage-4) |

**Status legend**: ✅ Complete · 🔄 In progress · ☐ Not started · ⏸ Blocked

**LLM CTA**: when starting work in this phase, scan the snapshot above to identify the active 🔄 stage, then jump to its granular checklist via the link. Don't start work that doesn't match the active stage without explicit user approval.

When all four stages flip to ✅, this doc gets archived (move to `docs/archive/PHASE_0_WIPE.md` or delete) per the lifecycle rule in V2.md §10.

---

## Per-library checklist (Stage 4)

Build order respects the dependency graph. Each lib lands as one squash-merged commit on `nova` (from a `nova/{lib}` branch). Flip ✅ when the lib ships with: full code + adversarial tests + README expanded from placeholder to real doc + zero `dotnet build` / `jb inspectcode` warnings.

| Wave | Lib | Status | Branch | Depends on |
|---|---|---|---|---|
| 1 | `D2.Shared.Result` | ✅ Complete | `n/result` | (none — foundational) |
| 1 | `D2.Shared.Utilities` | ✅ Complete | `n/utilities` | (none — foundational) |
| 1 | `D2.Shared.Resilience` | ✅ Complete | `n/utilities` | Result (for the `RetryD2ResultAsync` predicate) — split out from Utilities so retry / circuit-breaker / singleflight can be consumed independently of the boundary helpers |
| 2 | `D2.Shared.Handler` | ☐ Not started | `n/handler` | Result, Utilities, Resilience — includes BaseRepoHandler design (see [Phase 0 design notes](#phase-0-design-notes)) |
| 3 | `D2.Shared.Tests` | 🔄 In progress (scaffolded with Result coverage; grows per-lib) | `n/result` (born here) | Handler (test infra for the libs above) |
| 3 | `D2.Shared.I18n` | ☐ Not started | `n/i18n` | Utilities |
| 4 | `D2.Shared.Encryption` | ☐ Not started | `n/encryption` | Result, Utilities |
| 4 | `D2.Shared.Auth` | ☐ Not started | `n/auth` | Result, Utilities |
| 5 | `D2.Shared.Caching.Memory` | ☐ Not started | `n/caching-memory` | Handler |
| 5 | `D2.Shared.Caching.Redis` | ☐ Not started | `n/caching-redis` | Handler |
| 6 | `D2.Shared.Messaging` | ☐ Not started | `n/messaging` | Handler, Encryption |
| 7 | `D2.Shared.ServiceDefaults` | ☐ Not started | `n/service-defaults` | All of the above (composition root) |

**Notes:**
- Geo, Location, Contacts placeholder READMEs in `server/shared/dotnet/` belong to **Phase 1** (Geo libs) and **Phase 2** (Contacts) per V2.md §4 — not Stage 4.
- Within a wave, libs can ship in either order or together if small enough to bundle.
- Each lib's commit message: `feat(shared/{lib}): {one-line summary of public API}` plus a body listing key types / OTel metrics / tests added.
- Branches use the `n/` prefix (not `nova/`) so they coexist with the `nova` leaf branch in `refs/heads/n/...`.
- `D2.Shared.Tests` was scaffolded alongside `D2.Shared.Result` (rather than waiting for Wave 3) so the Result lib lands with full test coverage at point-of-merge. Each subsequent shared-lib PR adds its own `Unit/{Lib}/` subdirectory.

---

## Philosophy

V2.md §12 originally specified "wipe-and-rebuild" for everything outside a small KEEP list. After deep tree research (5 areas), that's too aggressive in 4 of 5 areas.

**Revised principle**: KEEP if edit cost is meaningfully less than rebuild cost. YEET if rebuild ≈ edit.

- **Observability dashboards**: 30 lines of edits vs full rebuild → **KEEP + edit**
- **Compose service blocks**: ~13 services translate verbatim → **KEEP + edit**
- **Translation files (10 locales × 620 keys)**: re-translation cost is huge → **KEEP + edit per phase**
- **Service protos**: cheap to rewrite + v1 encodes v1 architecture → **YEET (except common/v1)**
- **Pattern docs (HANDLER.md, RESULT.md, etc.)**: tribal knowledge → **KEEP + adapt**

`/old/v1/` snapshot remains as the safety net — anything yeeted is recoverable via `git show <pre-wipe-sha>:path`.

---

## KEEP / MOVE — content largely unchanged

### Stays at root (with no path changes)

| Item | Notes |
|---|---|
| `.editorconfig` | Generic + C# StyleCop carve-outs all transfer |
| `.gitattributes` | Generic LF normalization |
| `.dockerignore` | Generic |
| `.git/`, `.github/{labels.json, CODEOWNERS, pull_request_template.md, instructions/, templates/}` | Mostly generic |
| `.husky/commit-msg` | AI Co-Authored-By rejection — mandatory keep |
| `Makefile` | Compose helper targets — content updates per new service set |
| `LICENSE.md` | Conventional |
| `CLAUDE.md`, `README.md`, `CHANGELOG.md`, `V2.md` | Project docs |
| `CONTRIBUTING.md` | TBD — to scan during the .md sweep |

### Moves to `infra/observability/` (zero-touch)

11 of 14 files:
- `loki/config/loki.yaml`
- `mimir/config/mimir.yaml`
- `tempo/config/tempo.yaml`
- `grafana/provisioning/datasources/datasources.yaml`
- `grafana/provisioning/dashboards/dashboards.yaml`
- 5 community dashboards (cAdvisor 19792, MinIO 13502, PG 9628, RabbitMQ 10991, Redis 11835)
- `grafana/provisioning/dashboards/d2-worx/web-vitals-rum.json`

### Moves to `infra/compose/`

`docker-compose.yml` and `docker-compose.prod.yml` move with 13 service blocks intact (path-swap `./observability/` → `./infra/observability/`):
- `d2-postgres` + `d2-pgadmin` + `d2-pg-exporter`
- `d2-redis` + `d2-redisinsight` + `d2-redis-exporter`
- `d2-rabbitmq`, `d2-clamav`, `d2-portainer`
- `d2-loki`, `d2-tempo`, `d2-mimir`, `d2-cadvisor`, `d2-grafana`
- All Swarm `deploy:` blocks (resource limits + restart policies) carry forward — V2.md §5.9 explicitly targets Swarm
- All dev-tools `profiles:` overrides carry forward

### Moves to `infra/docker/`

**Template-forward**: ONE of the .NET Dockerfiles (gateway / geo / signalr) is the copy-modify template for the 5 new .NET Dockerfiles. Same `mcr.microsoft.com/dotnet/sdk:10.0` build → `aspnet:10.0` prod, `dotnet watch` dev pattern — exactly what V2.md §9 prescribes. Keep one open for reference during the wipe, then delete v1 file.

### Moves to `server/`

| File | Action |
|---|---|
| `Directory.Build.props` | Move + add version-anchor inheritance per V2.md §7 |
| `NuGet.config` | Move (generic NuGet source) |
| `global.json` | Move (.NET 10.0.100 SDK pin) |
| `stylecop.json` | Move (DCSV company name + copyright text) |

### Moves to `server/web/`

| File | Action |
|---|---|
| `.npmrc` | Move (engine-strict, save-exact, frozen-lockfile — all apply to SvelteKit) |
| `.prettierrc` | Move (Svelte-specific config) |
| `.prettierignore` | Move (has Paraglide-generated path) |
| `package.json` | Move + strip `pnpm -r` workspace scripts (SvelteKit standalone) |
| `pnpm-lock.yaml` | Move + regenerate after `package.json` restructure |
| `eslint.config.js` | Move + drop `backends/node` blocks (only Svelte rules survive) |
| `vitest.config.ts` | Move + drop backend test project pointers (only SvelteKit's) |

### Moves to `docs/`

| File | Action |
|---|---|
| `AUDIT_CHECKLIST.md` | Move + trim ~3-4 v1-specific items (Drizzle reference, specific SAGA file path) |
| `OPERATIONAL-GUARANTEES.md` | Move + edit (9-job table, JWKS endpoint path, file paths) |

### Stays in `contracts/`

- `contracts/protos/common/v1/*` — 4 foundational protos (`d2_result`, `health`, `jobs`, `ping`)
- `contracts/messages/*` — all 10 locale files (UPDATE per phase as features change)

---

## UPDATE IN PLACE — surgical edits

| File | Edit summary |
|---|---|
| `observability/alloy/config/config.alloy` | ~30 lines — rename scrape jobs `gateway-rest` → `edge`; swap env vars `GATEWAY_*` / `GEO_*` → `EDGE_*`; update Docker drop-list regex `(d2-geo|d2-gateway|d2-signalr)` → `(d2-edge|d2-files|d2-courier|d2-notifications|d2-audit)`; collapse Pino-services regex to `d2-web` only |
| `observability/grafana/.../d2-worx/rest-gateway-performance.json` | Rename file → `edge-performance.json`; swap `gateway-rest` → `edge`, `REST` → `Edge` in panel queries |
| `observability/grafana/.../d2-worx/sveltekit-bff-performance.json` | Rename file → `web-bff-performance.json`; swap `d2-sveltekit` → `d2-web` |
| `docker-compose.yml` and `.prod.yml` | (1) Path swap `./observability/` → `./infra/observability/`; (2) delete 8 v1 service blocks; (3) update `d2-alloy` env vars; (4) decide MinIO+SeaweedFS coexistence per V2.md §5.6; (5) keep `d2-dkron` daemon, drop `d2-dkron-mgr` (Phase 8); (6) switch `d2-sveltekit` to `develop.watch` per V2.md §9; (7) ADD blocks for `d2-edge`, `d2-files` (.NET this time), `d2-courier`, `d2-notifications`, `d2-audit`, `d2-seaweedfs` |
| `docker/Dockerfile.sveltekit` | Drop pnpm workspace bits; drop `backends/node` + `contracts` copies; retarget `clients/web/` → `server/web/` |
| `.gitignore` | Add `.env.secrets`, `secrets/`, `.aspire/settings.json`; drop `clients/web/src/routes/debug/` carve-out (paths change); update Paraglide path `clients/` → `server/web/` |
| `.github/workflows/test.yml` | **YEET-and-rewrite** per V2.md §8 single-lane shape (proto-checks, build, lint-and-format, unit-tests-{dotnet,web}, integration-{edge,files,courier,notifications,audit,key-rotation}, web-component-tests, web-mock-playwright-tests) |
| `.github/copilot-instructions.md` | Trim ~10-15 lines: drop Drizzle section, drop "Cross-platform enum changes in one commit," drop other Node-service-specific bullets |
| `Makefile` | Update service names in `make infra` and `make otel`; consider new `make dev` per V2.md §9 (Compose Watch) |
| `Directory.Build.props` (after move to `server/`) | Add version-anchor inheritance from `d2-version/D2.Version.csproj` |
| `package.json` (after move to `server/web/`) | Strip `pnpm -r` workspace scripts; standalone SvelteKit |

---

## YEET — replaced or obsolete

### Files at repo root
- `D2.sln`, `D2.sln.DotSettings`, `D2.sln.DotSettings.user` (replaced by `server/D2.slnx`)
- `pnpm-workspace.yaml` (no workspace; SvelteKit standalone)
- `inspectcode.log`, `inspectcode2.log`, `inspectcode_apphost.log` (build artifacts)
- `nul` (Windows accident)
- `PROFILE_PROGRESS.md` (v1 progress tracker)

### Already gone (handled in pre-wipe checkpoint commit)
- `PLANNING.md`, `RESEARCH_REPORT.md`, `VERSIONING.md`, `TO-REVIEW.md`

### Whole trees
- `/backends/` — entire .NET + Node service tree (rebuild fresh per phase using `/old/v1/` as reference)
- `/clients/` — SvelteKit moves to `server/web/`; mobile placeholder dropped

### `.github/`
- `.github/workflows/test.yml` (replaced — see UPDATE table)
- `tools/proto-gen/.gitkeep` (empty placeholder)

### Docker
- 8 v1 Dockerfiles: `Dockerfile.{auth, comms, dkron-mgr, files, gateway, geo, signalr}` (template-forward via gateway → edge before delete)
- `Dockerfile.sveltekit` deleted AFTER its surgical edit migrates to a new path

### Compose
- 8 v1 service blocks in compose files: `d2-{geo, auth, comms, files, gateway, signalr, dkron-mgr, node-init}` and matching prod overrides

### Contracts (only foundational protos survive)
- `contracts/protos/auth/v1/*` (auth.proto + auth_jobs.proto)
- `contracts/protos/comms/v1/*` (comms.proto + comms_jobs.proto)
- `contracts/protos/files/v1/*` (files.proto + files_jobs.proto + files_service.proto)
- `contracts/protos/geo/v1/*` (geo.proto + geo_jobs.proto)
- `contracts/protos/realtime/v1/*` (realtime_gateway.proto)
- `contracts/protos/events/v1/*` (geo_events.proto)
- `contracts/fixtures/*` (recreated per phase as test data)

---

## Wipe sequence

Single commit at the end. Tag `pre-v2-wipe` first as the safety net.

```
1. git tag pre-v2-wipe
2. Delete root files: D2.sln, D2.sln.DotSettings(.user), pnpm-workspace.yaml, inspectcode*.log, nul, PROFILE_PROGRESS.md
3. Delete /backends/ entire tree
4. Delete /clients/ entire tree (after copying clients/web/ → server/web/)
5. Delete YEET protos: contracts/protos/{auth,comms,files,geo,realtime,events}/, contracts/fixtures/
6. Create server/ tree per V2.md §2:
   - server/services/{edge,files,courier,notifications,audit}/{api,app,domain,infra,tests}/ (empty placeholders)
   - server/services/{files,courier,notifications,audit}/clients/dotnet/ (empty placeholders)
   - server/shared/dotnet/{handler,result,i18n,utilities,service-defaults,caching-memory,caching-redis,messaging,encryption,geo-reference,location,contacts,auth,tests}/ (empty placeholders)
   - server/shared/typescript/README.md (deferred placeholder)
   - server/d2-version/D2.Version.csproj (per V2.md §7)
   - server/web/ (recipient of /clients/web/)
   - server/Directory.Build.props (moved + updated)
   - server/Directory.Packages.props (new — per V2.md §7)
   - server/D2.slnx (empty XML solution; projects added in phase work)
   - server/NuGet.config, server/global.json, server/stylecop.json (moved)
7. Create infra/ tree:
   - infra/docker/ (will receive surgically-edited Dockerfiles per phase)
   - infra/compose/compose.yml, compose.prod.yml (moved + edited per UPDATE table)
   - infra/observability/ (moved + 3 surgical edits)
8. Create docs/:
   - docs/AUDIT_CHECKLIST.md (moved + trimmed)
   - docs/OPERATIONAL-GUARANTEES.md (moved + edited)
   - docs/SECURITY-RUNBOOKS.md (placeholder — populated in Phase 3 per V2.md §5.4)
9. Create tools/scripts/gen-dev-keys.sh (generates dev root key + dev encryption keys; populates secrets/)
10. Create .config/dotnet-tools.json (versionize as local tool)
11. Create .versionize at root (per V2.md §7)
12. Create env split:
    - .env.local.example (committed; non-secret defaults)
    - .env.secrets.example (committed; placeholder values like TWILIO_AUTH_TOKEN=replace_with_real_value)
    - secrets/.gitkeep (gitignored directory for key material)
13. Create .claude/settings.json with deny rules per V2.md §12
14. Update .gitignore: add .env.secrets, secrets/, drop v1 paths, update Paraglide path
15. Surgical edits per UPDATE table:
    - infra/observability/alloy/config/config.alloy
    - 2 grafana dashboards (rename + label swaps)
    - infra/compose/compose.yml + compose.prod.yml
    - infra/docker/Dockerfile.sveltekit
    - .github/workflows/test.yml (full rewrite per V2.md §8)
    - .github/copilot-instructions.md (trim)
    - Makefile (service names + new dev target)
    - server/Directory.Build.props (version anchor inheritance)
    - server/web/package.json (strip workspace scripts)
    - server/web/eslint.config.js (Svelte-only)
    - server/web/vitest.config.ts (SvelteKit-only)
    - server/web/pnpm-lock.yaml (regenerate)
16. Update CLAUDE.md per V2.md §12 "What CLAUDE.md needs updated" list
17. Update README.md per V2.md §12 (replace v1 paths with v2 paths; document env-file split + dev-key generation)
18. Single commit: chore(v2): wipe v1 implementation, restructure for v2 architecture (per V2.md)
```

---

## Definition of done (wipe commit)

- [ ] `git tag pre-v2-wipe` exists
- [ ] Single wipe commit on `nova` branch
- [ ] `git status` clean post-commit
- [ ] Tree matches V2.md §2 layout
- [ ] `.env.local.example` + `.env.secrets.example` committed; `.env.local` + `.env.secrets` + `secrets/` gitignored
- [ ] `.claude/settings.json` committed with deny rules
- [ ] `tools/scripts/gen-dev-keys.sh` exists and is executable
- [ ] CLAUDE.md updated per V2.md §12 list
- [ ] README.md updated per V2.md §12 list
- [ ] `docs/AUDIT_CHECKLIST.md` and `docs/OPERATIONAL-GUARANTEES.md` exist (moved + edited)
- [ ] `infra/observability/` 14 files present; alloy + 2 dashboards updated
- [ ] `infra/compose/compose.yml` + `compose.prod.yml` present, infra services intact, v1 service blocks deleted, new v2 service blocks added (placeholders or noted as TODO per phase)
- [ ] `server/` tree skeleton present with empty per-service folders + foundational config files
- [ ] `contracts/protos/common/v1/*` intact (4 foundational protos)
- [ ] `contracts/messages/*` 10 locale files intact
- [ ] `/old/v1/` snapshot intact

---

## Documentation pass (post-wipe, pre-Phase 0 code)

After the wipe commit lands but BEFORE any shared library code is written, complete a documentation pass so every directory in the new tree has a clear description of what it WILL be. This anchors the structure, gives Phase 0 implementation a contract to land against, and surfaces tribal knowledge extracted from the v1 .md sweep into permanent homes.

### Tribal knowledge extraction (from the .md sweep)

**Intent**: ~3000 lines of v1 docs contain hard-won correctness invariants that aren't obvious from clean code in isolation. The extraction distills these into ~600 lines of evergreen rules in `docs/`. Without it, v2 risks regressing on:

- Adversarial test discipline (drift to happy-path-only)
- Rate-limit dimension hierarchy + sliding-window approximation algorithm
- Idempotency SET NX sentinel pattern
- Constant-time service-key comparison
- Partial-success ladder (NOT_FOUND → SOME_FOUND → OK)
- RedactDataDestructuringPolicy mechanics

The 5x compression is the point: surviving content is exactly the load-bearing tribal knowledge.

**Source**: extraction reads from `/old/v1/` post-wipe (the snapshot is preserved). v1 working-tree files have been deleted by the wipe commit; that's fine — the snapshot is the authoritative reference.

**Extraction order** (dependencies — start with foundational, layer up):

1. `docs/PATTERNS.md` first — biggest doc, most cross-references. Establishes shared vocabulary (TLC/2LC/3LC, D2Result factory list) that other docs reference.
2. `docs/TESTS.md` — references PATTERNS.md for handler categories.
3. `docs/MESSAGING.md` — independent, but references PATTERNS.md handler pattern.
4. `docs/PARITY.md` — short template-style doc; can land any time.
5. `docs/SECURITY-RUNBOOKS.md` placeholder — single TOC stub; expanded in Phase 3.
6. Phase-scoped reference docs (`PHASE_5/6/8_REFERENCE.md`) — independent, can land in any order or be deferred to just-before-each-phase.
7. `server/web/STRATEGY.md` and `server/web/README.md` — moves with edits; happens during the wipe (file relocation), trim happens in doc pass.
8. V2.md §5 inline edits — small surgical edits; verify each is missing before adding.

#### Evergreen docs (create in `docs/`)

| New file | Sources | Content |
|---|---|---|
| **`docs/PATTERNS.md`** | BACKENDS.md + .NET HANDLER.md + .NET RESULT.md + .NET ServiceDefaults SERVICE_DEFAULT.md + .NET Utilities UTILITIES.md + .NET Repo BATCH_PG.md + ERRORS_PG.md + .NET Cache+Node Cache + .NET Middleware (5 files) + Node i18n I18N.md + Node service-defaults SERVICE_DEFAULTS.md (parseEnvArray) | Single distillation. Sections: TLC/2LC/3LC convention + canonical TLCs table; Handler (DefaultOptions/RedactionSpec/4 OTel metrics, both app AND repo declare); D2Result (12 factory list + partial-success ladder NOT_FOUND→SOME_FOUND→OK + auto-injected traceId); Utilities (Truthy/Falsey + ToNullIfEmpty + CleanStr + CircuitBreaker + Singleflight + retry options); Repo (Batch chunking + PG ~32K param limit + PG error codes 23505/23503/23502/23514 + "catch and return Conflict not 500"); Cache (lazy TTL + LRU + pluggable serializer); Middleware (Idempotency SET NX + sentinel + 30s in-flight TTL; RateLimit 4-dim hierarchy + sliding window approximation; RequestEnrichment IP precedence CF→XR→XF→Remote + fingerprint formulas; JwtAuth fingerprint formula `SHA256(UA\|Accept)`; ServiceKey constant-time `CryptographicOperations.FixedTimeEquals` + "compare against EVERY valid key, no short-circuit"; AuthPolicy route-gate registry); Configuration (parseEnvArray indexed convention `PREFIX__0`); RedactDataDestructuringPolicy mechanics (type-level + property-level + reflection caching + auto via `{@obj}`); i18n (10-locale BCP 47 list + env-driven SUPPORTED_LOCALES + TK constants). |
| **`docs/TESTS.md`** | .NET TESTS.md + Node testing TESTING.md | 8-category adversarial Case Coverage Checklist (happy / garbage / boundary / format / cross-field / error-prop / idempotency / concurrency); test naming convention; form + endpoint testing patterns; "if it accepts user input, try to break it" principle; 7 Vitest custom matchers (`toBeSuccess`/`toBeFailure`/`toHaveData`/`toHaveErrorCode`/`toHaveStatusCode`/`toHaveMessages`/`toHaveInputErrors`) — pattern transfers to xUnit assertion helpers. **Single highest-value extraction.** |
| **`docs/MESSAGING.md`** | backends/MESSAGING.md (drop v1 exchange/event tables, keep rules) | Proto-canonical-JSON wire format; exchange naming `events.{service}` / `commands.{service}`; queue patterns (exclusive auto-delete vs durable shared); AMQP headers contract (content-type / x-proto-type / message-id / timestamp); at-least-once + idempotent-consumer requirement. |
| **`docs/PARITY.md`** | backends/PARITY.md (reset row inventory for v2) | Parity-tracking template + the "Why exclusive?" justification framework for any future cross-language additions. |
| **`docs/SECURITY-RUNBOOKS.md`** | placeholder during wipe | Expanded in Phase 3 (Edge build) with compromise runbooks per V2.md §5.4 KeyCustodian: root key rotation, JWT signing key compromise, message-key compromise. |

#### Phase-scoped reference docs (deleted as each phase ships)

These preserve specific design decisions for upcoming rebuilds. They live only until the corresponding phase ships, then get archived.

| New file | Sources | Used in |
|---|---|---|
| **`docs/PHASE_5_REFERENCE.md`** | COMMS.md + COMMS_CLIENT.md (Universal Message Shape: 8-field contract `title`/`content`/`plaintext`/`channels`/`urgency`/`correlationId`/`senderService`/`metadata`) + COMMS.md 6 design principles | Phase 5 (Courier + Notifications build) |
| **`docs/PHASE_6_REFERENCE.md`** | FILES.md (6 design principles + status state machine `pending → processing → ready\|rejected`) + FILES_DOMAIN.md (smartphone-aware MIME list HEIC/HEIF/3GPP/AAC/M4A + design decisions table) + GEO_CLIENT.md (.NET) DefaultOptions LogInput/LogOutput suppression pattern | Phase 6 (Files .NET rebuild) |
| **`docs/PHASE_8_REFERENCE.md`** | DKRON_MGR.md (reconciler pattern: every-5min fetch → filter `metadata.managed_by` → diff → upsert/delete; change-detection field list) | Phase 8 (dkron-mgr port to .NET) |

#### Doc moves (no extraction, just relocate)

- **`server/web/STRATEGY.md`** — moved from `clients/web/SVELTEKIT_STRATEGY.md`, trimmed. Contains library recommendations (Superforms+Formsnap+Zod 4, shadcn-svelte+Bits UI, Sonner toasts, LayerChart 2.0, etc.), testing strategy (Vitest browser-mode + Playwright with mocks + a11y), Faro telemetry. Most carries forward.
- **`server/web/README.md`** — moved from `clients/web/README.md`, trimmed. Hybrid Pattern C diagram + middleware pipeline order + route groups + i18n list. Update v1→v2 paths (browser → Edge direct per V2.md §5.8).

#### Folded into V2.md §5 (small inline edits)

- **§5.5 SignalR** — channel naming convention from SignalR.md (`user:{userId}`, `org:{orgId}`, `thread:{threadId}`) + push-only hub + auto-subscribe-on-connect (verify if already there)
- **§5.4 Auth & Security** — "two role concepts" note (user-level vs org-level) from AUTH.md
- **§5.6 Storage** — content-addressable + immutability rationale from GEO_SERVICE.md (verify if already there)
- **§5.7 Messaging & Notifications** — verify the 6 design principles from COMMS.md are reflected

### Index docs (new)

Every "container" directory gets a README.md acting as a table of contents:

- `docs/README.md` — TOC for all `docs/*.md` files
- `server/shared/dotnet/README.md` — index of shared libs with one-line description of each
- `server/services/README.md` — index of services with one-line description + phase number
- `infra/README.md` — overview of infra layout (compose, docker, observability) + ops commands
- `tools/README.md` — overview of dev tooling (scripts, generators)

### Per-lib placeholder READMEs

Every shared lib in `server/shared/dotnet/{lib}/` gets a `README.md` describing:
- **Purpose** — one paragraph
- **Public API surface** — high-level (no implementation detail)
- **Dependencies** — which other libs it pulls in
- **V2.md reference** — which architectural section governs this lib

Files to create (14 total):
- `server/shared/dotnet/handler/README.md`
- `server/shared/dotnet/result/README.md`
- `server/shared/dotnet/i18n/README.md`
- `server/shared/dotnet/utilities/README.md`
- `server/shared/dotnet/service-defaults/README.md`
- `server/shared/dotnet/caching-memory/README.md`
- `server/shared/dotnet/caching-redis/README.md`
- `server/shared/dotnet/messaging/README.md`
- `server/shared/dotnet/encryption/README.md`
- `server/shared/dotnet/geo-reference/README.md`
- `server/shared/dotnet/location/README.md`
- `server/shared/dotnet/contacts/README.md`
- `server/shared/dotnet/auth/README.md`
- `server/shared/dotnet/tests/README.md`

### Per-service placeholder READMEs

Every service in `server/services/{service}/` gets a `README.md` describing:
- **Purpose** — one paragraph
- **Public API surface** — high-level (HTTP/gRPC endpoints by category)
- **Dependencies** — other services it consumes + shared libs it uses
- **V2.md reference** — §5.x section
- **Phase number** — when built per V2.md §4

Files to create (5 total):
- `server/services/edge/README.md`
- `server/services/files/README.md`
- `server/services/courier/README.md`
- `server/services/notifications/README.md`
- `server/services/audit/README.md`

### Commit

This documentation pass lands in a SEPARATE commit AFTER the wipe commit:

```
docs(v2): post-wipe documentation pass — placeholder READMEs + extracted patterns
```

**Why separate from the wipe commit**: keeps the wipe commit clean (file restructuring only) and lets the doc pass be reviewed independently. Both commits are required before Phase 0 (shared library code) begins. The wipe commit can stand alone in git history; the doc pass builds on top.

### Definition of done (documentation pass)

**Tribal knowledge extraction (evergreen)**:
- [ ] `docs/PATTERNS.md` — TLC/2LC/3LC + handler + D2Result + utilities + repo + cache + middleware + RedactionSpec + i18n sections present
- [ ] `docs/TESTS.md` — 8-category Case Coverage Checklist + Vitest matchers reference present
- [ ] `docs/MESSAGING.md` — proto-canonical-JSON + exchange naming + queue patterns + AMQP headers + at-least-once present
- [ ] `docs/PARITY.md` — template + "Why exclusive?" framework present (rows reset for v2)
- [ ] `docs/SECURITY-RUNBOOKS.md` — placeholder with TOC stub (expanded Phase 3)

**Tribal knowledge extraction (phase-scoped)**:
- [ ] `docs/PHASE_5_REFERENCE.md` — Universal Message Shape + COMMS 6 principles
- [ ] `docs/PHASE_6_REFERENCE.md` — FILES 6 principles + state machine + smartphone MIME list + GEO_CLIENT log-suppression
- [ ] `docs/PHASE_8_REFERENCE.md` — DKRON_MGR reconciler pattern + change-detection fields

**Doc moves**:
- [ ] `server/web/STRATEGY.md` — moved + trimmed from `clients/web/SVELTEKIT_STRATEGY.md`
- [ ] `server/web/README.md` — moved + v1→v2 path updates from `clients/web/README.md`

**V2.md inline edits** (verify-before-adding to avoid duplication):
- [ ] §5.4 — "two role concepts" note (user-level vs org-level) added if missing
- [ ] §5.5 — SignalR channel naming convention added if missing
- [ ] §5.6 — content-addressable + immutability rationale added if missing
- [ ] §5.7 — COMMS 6 design principles reflected if missing

**Index docs**:
- [ ] `docs/README.md` — TOC for all `docs/*.md`
- [ ] `server/shared/dotnet/README.md` — index of 14 libs
- [ ] `server/services/README.md` — index of 5 services + phase numbers
- [ ] `infra/README.md` — overview + ops commands
- [ ] `tools/README.md` — overview of tooling

**Per-lib placeholder READMEs (14)**:
- [ ] All 14 shared libs have `README.md` in `server/shared/dotnet/{lib}/` per the format above

**Per-service placeholder READMEs (5)**:
- [ ] All 5 services have `README.md` in `server/services/{service}/` per the format above

**Cross-references**:
- [ ] CLAUDE.md §3 reference table updated to include all new `docs/*.md` files
- [ ] PHASE_0.md (this doc) marked for archive (move to `docs/archive/` once Phase 0 ships)

**Commit**:
- [ ] Single docs commit on `nova` branch immediately following the wipe commit
- [ ] Commit message: `docs(v2): post-wipe documentation pass — placeholder READMEs + extracted patterns`

---

## Phase 0 design notes

Design decisions captured during planning that govern Phase 0 implementation. Each note describes the *intent*; implementation lands in the per-library code under `server/shared/dotnet/{lib}/` when `D2.Shared.Handler` (and its consumers) are built. Summarised in `docs/PATTERNS.md` once landed.

### `BaseHandler` refactor + `BaseRepoHandler` for EF exception mapping

**Problem.** v1 `BaseHandler.HandleAsync` (`old/v1/D2-WORX/backends/dotnet/shared/Handler/BaseHandler.cs`) has a universal try/catch that swallows every exception and converts it to `D2Result.UnhandledException`. Repo handlers that need to map EF exceptions (e.g., PG unique-violation → `Conflict`) must add their own try/catch at the top of `ExecuteAsync` — survey of v1 Geo.Infra showed only `CreateContacts.cs` does this; the other ~15 repo handlers have zero exception handling, so constraint violations surface as generic 500s.

**Goal.** Centralise EF→`D2Result` mapping into a dedicated `BaseRepoHandler` so repo handlers stop having to think about it. Eliminate boilerplate while keeping the original `Exception` object out of every wire-format type (per the long-standing rule that `D2Result` is pure data — no exception coupling).

**Shape.**

1. **Extract today's `HandleAsync` body into a sealed-by-default protected method.** Name: `RunCorePipelineAsync`. Returns a value tuple `(D2Result<TOutput?> Result, Exception? CapturedException)`. The existing universal catch sets `CapturedException` to the thrown exception (and returns `UnhandledException` as the Result). On success, `CapturedException` is null. The method is `protected` (not `virtual`) — subclasses cannot tamper with the observability/metrics pipeline; they consume its outcome only.

2. **Make `HandleAsync` `virtual`.** Default implementation is a one-line pass-through:
   ```csharp
   public virtual async ValueTask<D2Result<TOutput?>> HandleAsync(
       TInput input, CancellationToken ct = default, HandlerOptions? options = null)
       => (await RunCorePipelineAsync(input, ct, options)).Result;
   ```
   Existing concrete handlers need zero changes.

3. **Add `BaseRepoHandler<TSelf, TInput, TOutput> : BaseHandler<...>`.** Overrides `HandleAsync`, calls `RunCorePipelineAsync`, switches on `CapturedException` type to remap known EF exceptions to specific `D2Result` codes via existing factories. Unknown exceptions fall through (the original `UnhandledException` Result is returned unchanged).

4. **The `Exception` object lives only on the stack frame inside the BaseHandler hierarchy.** The protected tuple is destructured locally. Only `D2Result` ever escapes. **`D2Result` itself is unchanged** — no new field, no `[JsonIgnore]`, no proto exclusion, no TS parity work. The "no exception details on D2Result" rule (intentional removal during the DeCAF→D2 transition) is preserved by *structure*, not by attribute discipline.

**Why structure-not-attribute matters.** Any `D2Result` field guarded by `[JsonIgnore]` is one new serializer (or one Newtonsoft consumer, or one YAML log destructurer, or one .ts JSON.stringify) away from leaking. A field that doesn't exist can't leak. The exception travels through OTel (`activity?.AddException(ex)` in `RunCorePipelineAsync`) and Loki (log scope) — both already secured against client exposure — and those carriers are the join keys (via `traceId` on `D2Result`) that ops uses for triage.

**Mapping table.** Match on EF exception *type* first (the type is the reliable signal). EF doesn't guarantee that inner driver exceptions populate primitives like `SqlState` or `ConstraintName`, so use those only as opportunistic refinement. v1 already provides `D2.Shared.Repository.Errors.Pg.PgErrorCodes` static predicates (`IsUniqueViolation`, `IsForeignKeyViolation`, `IsNotNullViolation`, `IsCheckViolation`) that handle both direct `PostgresException` and EF-wrapped `DbUpdateException.InnerException` — port these forward and reuse.

| EF exception type (Microsoft.EntityFrameworkCore + Npgsql) | Default mapping | Notes |
|---|---|---|
| `DbUpdateConcurrencyException` | `D2Result.Conflict` | Row-version mismatch / optimistic concurrency. EF-determined, no driver involvement. |
| `DbUpdateException` w/ `PgErrorCodes.IsUniqueViolation` | `D2Result.Conflict` | PG `23505`. |
| `DbUpdateException` w/ `PgErrorCodes.IsForeignKeyViolation` | `D2Result.ValidationFailed` | PG `23503`. Caller passed an invalid FK. |
| `DbUpdateException` w/ `PgErrorCodes.IsNotNullViolation` | `D2Result.ValidationFailed` | PG `23502`. Required field missing. |
| `DbUpdateException` w/ `PgErrorCodes.IsCheckViolation` | `D2Result.ValidationFailed` | PG `23514`. Check constraint failed. |
| `DbUpdateException` (no recognized inner) | Fall through | Forces handlers that hit unrecognized DB errors to add a recognizer rather than papering over with broad `Conflict`. |
| `RetryLimitExceededException` | Fall through (= `UnhandledException`) | EF execution-strategy gave up; root cause is in the inner. Logs already capture it. |
| `OperationCanceledException` when `ct.IsCancellationRequested` | `D2Result.Cancelled` | User-initiated cancellation only. Other `OperationCanceledException` flavors (framework-internal) fall through — they're a different bug class. |
| Anything else | Fall through | Original `UnhandledException` Result unchanged. |

**Per-handler refinement.** Subclasses needing constraint-specific mapping override `HandleAsync` themselves. Pattern:

```csharp
public override async ValueTask<D2Result<TOutput?>> HandleAsync(
    TInput input, CancellationToken ct = default, HandlerOptions? options = null)
{
    var ctx = await RunCorePipelineAsync(input, ct, options);
    if (ctx.CapturedException is DbUpdateException dbEx
        && dbEx.InnerException is Npgsql.PostgresException { ConstraintName: "users_email_unique" })
    {
        return D2Result<TOutput?>.Conflict(messages: [TK.account_errors_emailTaken], traceId: TraceId);
    }
    return await base.HandleAsync(input, ct, options);
}
```

**Observability is unchanged.** `RunCorePipelineAsync` still calls `activity?.AddException(ex)`, records the exception metric, emits the unhandled-exception log. Tempo + Loki get the full exception regardless of whether a subclass remaps the Result. **Add one enhancement** at implementation time: push `exceptionType` + `innermostExceptionType` onto the log scope (via `BeginScope`) so Loki queries can filter by type without parsing the message.

**Open questions to resolve at implementation time.**

1. Naming: `RunCorePipelineAsync` vs `RunPipelineAsync` vs `ExecuteWithObservabilityAsync`. Default proposal: `RunCorePipelineAsync`.
2. `DbUpdateException` with no recognized inner — fall through (conservative) vs default `Conflict` (broad). Default proposal: **fall through**, force explicit recognition.
3. Tuple element naming — `(Result, CapturedException)` vs `(Result, Exception)`. Default proposal: `CapturedException` (reads better at the call site, avoids shadowing the type name).
4. Does this also get a parallel `BaseRepoHandler` for the SvelteKit BFF or any future Node.js backend? Per current scope (.NET-only backend per V2.md §5.1), **no**. Revisit if cross-language services land later.

**Out of scope (rejected during design).**

- Adding any exception-metadata field to `D2Result` itself — including sanitised "type-name only" variants. Leakage surface, cross-platform coupling, OTel span already carries this.
- Generalising the wrapping pattern into a `Pipeline` delegate property or middleware-style stack. The simple virtual-`HandleAsync` + protected `RunCorePipelineAsync` covers every realistic use case. Future bases (`BaseAuditedHandler`, etc.) override `HandleAsync` the same way without changing `BaseHandler`.

---

## Then: Phase 0 (shared libraries)

After BOTH the wipe commit AND the documentation pass land, Phase 0 code begins per V2.md §4 — implementing the 14 foundational shared libraries against the contracts established in their placeholder READMEs.

Each library, as it's implemented, expands its placeholder README into a full doc (full public API, examples, gotchas, OTel metrics if applicable). Per V2.md §6: "Every project/module has a corresponding `.md` file."

This PHASE_0.md doc gets archived (move to `docs/archive/PHASE_0_WIPE.md` or delete) once Phase 0 ships and all 14 shared libs have full READMEs.
