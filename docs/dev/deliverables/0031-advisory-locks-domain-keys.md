<!--
Copyright (c) DCSV. All rights reserved.
Committed snapshot of deliverable-0031 workspace (docs/wip/0031-advisory-locks-domain-keys/),
captured at SHIP readiness 2026-07-12. Per-step journals remain local-only under docs/wip/ (gitignored).
-->

# 0031 — Advisory-lock domain keys out of shared PublicAPI (PHASE_3 D2)

**Status:** READY TO COMMIT (code-audit R2 **CLEAN**)  
**Branch:** `n/advisory-locks-domain-keys`  
**Type:** Hygiene mini (single fat step `01-domain-keys`)  
**Date:** 2026-07-12  
**Audit mode:** single-agent Plan-Audit + single-agent code audit (user-explicit K=1)

## Goal

Shared Postgres owns mechanism only (`PgAdvisoryLock`, migrator, design-time factory base, `NpgsqlContextDefaults`, generator tooling). Domain advisory-lock key catalogs live with the owning module (KC). No `AdvisoryLocks.D2Keycustodian.*` on shared PublicAPI. Central fleet catalog retained; uniqueness still at gen time. KEEP docs lock post-0030 Phase 3 spine.

## What shipped

### Generator hard-retarget (Approach A)

| Surface | Before | After |
| --- | --- | --- |
| Destination assembly | `DcsvIo.D2.EntityFrameworkCore.Postgres` | `DcsvIo.D2.Private.Edge.KeyCustodian.Infra` |
| Emitted namespace | `DcsvIo.D2.EntityFrameworkCore.Postgres` | `DcsvIo.D2.Private.Edge.KeyCustodian.Infra` |
| Leaf shape | `AdvisoryLocks.D2Keycustodian.{MIGRATOR,ROTATION,CA_SEED}` | **unchanged** |
| Spec SoT | `contracts/advisory-locks/advisory-locks.spec.json` | **same** (central fleet catalog) |
| Generator home | `locks-source-gen/` | **same** (shared tooling) |
| Uniqueness | Full catalog at gen | **same** |

- `AdvisoryLocksGenerator._TARGET_ASSEMBLY_NAME` → `DcsvIo.D2.Private.Edge.KeyCustodian.Infra`
- `AdvisoryLocksEmitter.ROOT_NAMESPACE` → `DcsvIo.D2.Private.Edge.KeyCustodian.Infra`
- Analyzer + AdditionalFiles moved from postgres csproj → KC Infra csproj
- Deleted postgres `Generated/DcsvIo.D2.AdvisoryLocks.SourceGen/**`
- Regenerated: `server/services/edge/key-custodian/infra/Generated/DcsvIo.D2.AdvisoryLocks.SourceGen/.../AdvisoryLocks.g.cs`
- Softened mechanism remarks that pointed at shared-hosted lock catalogs

### Call sites

- KC Infra production (3): same-assembly `AdvisoryLocks`; keep shared Postgres for `PgAdvisoryLock` / migrator
- Edge.Tests (3): MigratorComposition / Lifecycle / Concurrency — `using DcsvIo.D2.Private.Edge.KeyCustodian.Infra`

### Tests retargeted

- `AdvisoryLocksGeneratorTests`, `AdvisoryLocksEmitterTests`, `AdvisoryLocksOutputParityTests`

### Consumable hygiene (shared Postgres)

- Removed all `AdvisoryLocks*` from `PublicAPI.Shipped.txt` (seed script)
- CHANGELOG `[Unreleased]` **API-breaking** note
- `.release-fingerprint` reseeded
- `pnpm --filter release-runner check-baselines` → **PASS**
- **Ship commit must carry `BREAKING CHANGE:` footer** (API-break on 0.x → MINOR floor)

### KEEP docs — Phase 3 spine (mandatory this deliverable)

On disk in `docs/v2/PHASE_3.md`:

1. **D2 row** → ✅ SHIPPED 0031  
2. **A1 row** → ✅ SHIPPED 0030  
3. **Durable spine (as locked in this ship):** **D2 (0031) → A2 fat (token mint + multiproc) → A3+E1 fat (sessions/credentials/anon + WhoIs) → E2** — **superseded post-ship** by living [PHASE_3.md](../../v2/PHASE_3.md): **D2 → A2 Auth Core → A3 Minting → Auth Extras + E1 → E2** (proper mint depends on Auth Core prims/storage; do not mint-first with fixtures)  
4. Status header + critical-path + numbered build order updated  

### Doc parity (same change)

postgres README (real registration only — phantom `AddD2AdvisoryLockMigrator` removed in R1 fix), locks-source-gen README, entity-framework-core README, shared/dotnet README, KC infra README, contracts/advisory-locks README + schema description, contracts/README, SRC_GEN.md D2LCK row.

## Out of scope (explicit)

- A2 token mint / multiproc product work  
- Multi-target emit filter (documented extension only)  
- New lock keys / second-domain catalog entries  
- Hand constants without catalog  

## Process / audit

| Phase | Result |
| --- | --- |
| Plan | Approach **A** (hard-retarget); OPEN QUESTIONS: none |
| Plan-Audit (K=1) | **READY** |
| Implement | Gates green (see below) |
| Code-audit R1 (K=1) | 1 FINDING-LOW R1-1 (phantom DI helper in postgres README) |
| Fix R1-1 | Deleted phantom paragraph; fix-log appended |
| Code-audit R2 (K=1) | **CLEAN** (0 FINDING; R1-1 absent) |

Local journals (gitignored): `docs/wip/0031-advisory-locks-domain-keys/01-domain-keys/`.

## Gates

| Gate | Result |
| --- | --- |
| `dotnet build server/D2.slnx` | 0w / 0e |
| `dotnet test` Shared (full) | PASS (6870) |
| `dotnet test` Edge | PASS (1793 + 8 skipped) |
| `jb inspectcode` deliverable scope | 0 findings; 2 pre-existing unused-using in `DcsvIo.D2.Auth.Grpc` (untouched) |
| Baseline currency | PASS |

## Steps

| # | Step | Status |
|---|---|---|
| 01 | Domain keys move + docs + spine lock | **CLEAN** |

## Suggested commit (when authorized)

```
fix(shared)!: move advisory-lock domain keys out of Postgres PublicAPI

PHASE_3 D2 / deliverable 0031: shared EntityFrameworkCore.Postgres keeps
mechanism only; locks-source-gen emits AdvisoryLocks into KeyCustodian.Infra.
Central contracts/advisory-locks catalog retained. KEEP spine as locked **at ship** was
D2 → A2 fat → A3+E1 fat → E2 — **living SoT supersession** in [PHASE_3.md](../../v2/PHASE_3.md):
**D2 → Auth Core → Minting → Auth Extras + E1 → E2**.

BREAKING CHANGE: DcsvIo.D2.EntityFrameworkCore.Postgres no longer ships
AdvisoryLocks.D2Keycustodian.*; consume constants from DcsvIo.D2.Private.Edge.KeyCustodian.Infra.
```

## Residuals (not findings)

1. Multi-domain catalog growth needs multi-target emit filter (documented).  
2. Auth.Grpc inspect unused-usings pre-existing / out of scope.  
3. Commit not performed — user will commit / PR from branch.  
