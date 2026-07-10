<!--
Copyright (c) DCSV. All rights reserved.
Committed snapshot of the gitignored deliverable-0028 workspace root
(docs/wip/0028-ts-caching/README.md), captured at SHIP 2026-07-10.
Post-SHIP follow-ups (dispose fail-closed, TS numeric hardening, dual-runtime
constants parity) landed on the same branch tip; this snapshot updated 2026-07-10
to keep REVIEW dispositions honest.
Per-step journals remain local-only under docs/wip/0028-ts-caching/ (gitignored).
-->

# 0028 — TS tiered cache + Redis invalidation-backplane twin (T1)

**Status:** SHIPPED 2026-07-10 — product on branch `n/ts-caching` (committed tip `bec8508d`; post-ship hardening Fixer WT after `ed872ded`..`bec8508d` audited).  
**Branch:** `n/ts-caching` (from `nova` @ `933d2901`)  
**Phase:** [PHASE_3.md](../../v2/PHASE_3.md) **T1** · Architecture: [V2.md §5.8](../../v2/V2.md) · Behavioral model: [ADR-0008](../../adrs/0008-caching-marker-interfaces.md)

## Goal

Ship the **full behavioral TypeScript twin** of the .NET `D2.Shared.Caching.*` stack — abstractions (markers + building blocks + backplane), local default, Redis distributed + invalidation backplane, and tiered composition — so multi-instance Node workloads (BFF as mesh member, future Node services) keep L1 coherent via the shared Redis pub/sub channel `d2:cache:invalidations` with the universal **"everyone acts"** rule.

Success = package layout mirrors `server/shared/dotnet/caching/`; every Basic / Atomic / Broadcast / Set surface that .NET exposes has a TS counterpart returning `@d2/result` shapes; unit + Testcontainers Redis integration prove package-local parity; dual-runtime **constants/semantics** parity pins defaults / meters / Lua / channel / tiered EventId bindings; V2/PHASE/PARITY/KEEP docs describe the twin as shipped (stale “cache packages dropped” framing removed).

## What shipped

| Step | Commit / package | Notes |
| --- | --- | --- |
| 01-abstractions | `99724826` · `@d2/caching-abstractions` | Markers, building blocks, options, InputFailures, serializer seam |
| 02-local-default | `b49f6b3d` · `@d2/caching-local-default` | In-process L1 + atomics; post-dispose + numeric hardening |
| 03-distributed-redis | `0010d5aa` · `@d2/caching-distributed-redis` | Redis store + invalidation backplane; Lua pins; IT |
| 04-tiered | `7b04bd70` · `@d2/caching-tiered` | L1+L2 composition; L2-first; everyone-acts L1 drop |
| 05-docs-parity + FR fixes | `04edf08d` | PARITY/PATTERNS/PHASE_3/ADR honesty; `setMany` pipeline error path; log-op named constants; harness renames; fingerprints |
| Snapshot + T1 status | `32b6903b` | Deliverable snapshot + PHASE_3 T1 SHIPPED |
| TS numeric hardening | `ed872ded` | Non-finite / non-safe-integer guards on local + redis (REVIEW item 2) |
| .NET dispose fail-closed | `3ef66497` | `DefaultLocalCache` `ThrowIfDisposed()` on **all** public ops including locks — closes PHASE_3 Deferred **D1** |
| Dual-runtime constants parity | `aee86e90` | `CachingTwinFixtureEmitter` + `fixtures/caching-twin/constants.json` + `caching-twin.parity.test.ts` (KOM-01..08) |

### Layout

```
server/shared/typescript/caching/
  abstractions/      → @d2/caching-abstractions
  local-default/     → @d2/caching-local-default
  distributed-redis/ → @d2/caching-distributed-redis
  tiered/            → @d2/caching-tiered
```

### Behavioral law (ADR-0008 — non-negotiable)

- Markers compose building blocks; marker **name** is the inject-site intent.
- Every op returns `@d2/result` shapes; miss → NotFound; partial bulk → SomeFound; bad input → ValidationFailed; store down → ServiceUnavailable; type mismatch on Increment → Conflict.
- `*AndBroadcast*` after successful write → backplane publish; missing backplane = registration error (fail loud).
- **Everyone acts** — no sender-ID filter; publisher drops own L1 too.
- Delivery **at-most-once**; missed invalidation → next read hits L2.
- Tiered: write L2 first; only then L1; atomics on L2 + invalidate L1.
- `ICacheSet` only on distributed marker (not tiered).

### Out of scope (honest)

- BFF host DI wiring (later composition deliverable).
- Full dual-runtime **behavior** interop harness (cross-process algorithm equivalence suite) — package-local unit + Testcontainers cover algorithm; dual-runtime suite is **constants/semantics only** (KOM-01..08 closed).
- Spec/codegen catalogs for cache keys.

## Cross-cutting decisions (locked)

| # | Decision | Choice |
|---|----------|--------|
| **D1** | V2 “no TS cache packages” | **Override as stale** — T1 authoritative |
| **D2** | BFF / Node write posture | **Full twin** (not subscribe-only L1) |
| **D3** | Surface scope | **T1-FULL** Basic+Atomic+Broadcast+Set+tiered+backplane |
| **D4** | Package layout | Mirror .NET under `typescript/caching/*` |
| **D5** | Invalidation channel | Share `d2:cache:invalidations` |
| **D6** | Parity bar | Full behavioral runtime twin; package-local ITs + dual-runtime constants/semantics suite (not invent full algorithm interop corpus) |

## Process integrity

- Orchestrator-only main thread; Grok Build `grok-d2-*` roles.
- Per-step Plan → Plan-Audit (as required) → Implementer → targeted code-audit Y ⊆ K=7 → dirty-only re-dispatch.
- Step 02: **user skip dirty R3** after CA-R2-E-1 (§13.14 trail in local journal + root README).
- **FINAL-REVIEW:** full K=7 vs `nova` + working tree; Fixer rounds closed product findings (incl. `setMany` pipeline errors, AbortSignal double fidelity, §21.11 named log ops, §26.20 reseed); FR Plan-Audit of thin FR Plan CLEAN (R1 + dirty R2).
- FR Latest fold: **zero FINDING** (R4).
- Post-SHIP follow-ups on same branch: .NET dispose D1 fix, TS numeric hardening, dual-runtime constants parity + KEEP docs sweep (`ed872ded`..`bec8508d`).
- Post-ship hardening **audited** (full K=7 R1) then Fixer remediations (overflow reverse, dispose flag-first, InputFailures.invalid, Public surface, instrument SoT, process journal). Not a false “FR-only CLEAN” claim for post-ship commits — post-ship had its own R1 + fix wave.
- No `rules.md` predicates proposed at SHIP (table empty).

## Kinds-of-misses (distilled)

| ID | Class | Detail | Follow-up |
| --- | --- | --- | --- |
| KOM-01..08 | §26.12 dual-runtime constants | Hand-mirrored defaults / meters / Lua / channel / tiered log semantics with package-local pins only | **CLOSED** via `aee86e90` — `CachingTwinFixtureEmitter` + `@d2/contract-tests` `caching-twin.parity.test.ts` + `fixtures/caching-twin/constants.json` (constants/semantics; tiered: EventId/bindings only, not LoggerMessage byte-equality) |
| FR product | §18 / §1.32 | `setMany` ignored ioredis per-command errors; hollow AbortSignal double | Fixed in `04edf08d` with regression units |
| Process | §24 | Step journal Latest lag vs READY partials; FR Plan-Audit timing | Forensic journal merges + real FR Plan-Audit before SHIP |

## For REVIEW (user decisions) — disposition

| # | Original item | Disposition |
| --- | --- | --- |
| 1 | .NET `DefaultLocalCache` post-dispose lock ops kept working after `Dispose()` (PHASE_3 Deferred D1); 0028 forbade .NET fix at ship | **FIXED** on `n/ts-caching` (`3ef66497`) — `ThrowIfDisposed()` on all public ops including locks; PHASE_3 D1 ✅ fixed; TS + .NET both fail-closed |
| 2 | TS twin deliberate deltas — uniform post-dispose throw; non-finite/non-safe-integer numeric hardening | **LANDED** — dispose alignment with .NET (`3ef66497`); numeric hardening (`ed872ded`). Residual deliberate deltas: exception **type** (`Error` vs `ObjectDisposedException`); JS `number` width / non-finite guards |
| 3 | KOM-01..08 dual-runtime suite deferred | **LANDED** (`aee86e90`) — constants/semantics suite only; full behavior interop harness still out of scope (honest) |

## Proposed rule additions to rules.md

_(none)_

## Final attestation

I attest that this deliverable's process integrity has been verified against the deliverable completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is YES with the following **honest scoped notes** (not silent NO):

| Checklist class | Citation |
| --- | --- |
| Per-step journals + 3-artifact model | Local WIP `docs/wip/0028-ts-caching/{01..05,final-review}/journal.md` (gitignored; not in this snapshot) |
| Mid-step audits | Targeted Y ⊆ K=7 per deliverable audit strategy (user-locked); not full-catalog mid-step |
| Step 02 final dirty re-walk | User-skipped R3 after CA-R2-E-1 (§13.14) — product residual closed on disk |
| Final-review | Full K=7 vs `nova`+WT; Latest zero-FINDING (R4); FR Plan-Audit CLEAN |
| Doc gates | PARITY (incl. caching-twin constants suite), PATTERNS Cache dual pointer, parent TS + package READMEs, PHASE_3 T1 + D1 fixed, ADR-0008 |
| Build / inspectcode (.NET) | N/A for original TS-only product path; post-SHIP .NET dispose + abstractions `InputFailures.Invalid` + ContractFixtures emitter on tip; TS unit + parity suites green at last Fixer gates; `check-baselines` re-run when consumables touch |
| Commits | Only after explicit user permission (cycle-commit marker path) |
| Post-ship hardening | Commits `ed872ded`..`bec8508d` audited (R1+R2 dirty); residual Fixer (pre-INCRBY GET + negative-validation + process G) — re-audit dirty A/G proves closure (not FR-only CLEAN) |

**The deliverable remains ready for user REVIEW** after post-ship Fixer gates + dirty-seat re-audit prove zero FINDING (post-SHIP follow-ups closed D1 + dual-runtime constants suite + numeric hardening + post-ship R1+R2 residual A; docs sweep keeps KEEP honest).

Spot-check anchors (committed product):

- Packages: `server/shared/typescript/caching/**`
- Docs: `docs/PARITY.md`, `docs/PATTERNS.md` (Cache), `docs/adrs/0008-caching-marker-interfaces.md`, `docs/v2/PHASE_3.md` T1 + D1
- Dual-runtime: `server/shared/typescript/contract-tests/tests/caching-twin.parity.test.ts` + `fixtures/caching-twin/constants.json` + `CachingTwinFixtureEmitter.cs`
- Tip commits: `99724826`, `b49f6b3d`, `0010d5aa`, `7b04bd70`, `04edf08d`, `32b6903b`, `ed872ded`, `3ef66497`, `aee86e90`, `bec8508d` (docs tip); residual post-ship Fixer WT uncommitted pending user commit
