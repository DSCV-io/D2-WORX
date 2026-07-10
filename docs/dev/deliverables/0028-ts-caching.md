<!--
Copyright (c) DCSV. All rights reserved.
Committed snapshot of the gitignored deliverable-0028 workspace root
(docs/wip/0028-ts-caching/README.md), captured at SHIP 2026-07-10.
Per-step journals remain local-only under docs/wip/0028-ts-caching/ (gitignored).
-->

# 0028 — TS tiered cache + Redis invalidation-backplane twin (T1)

**Status:** SHIPPED 2026-07-10 — product on branch `n/ts-caching` (tip includes `04edf08d`).  
**Branch:** `n/ts-caching` (from `nova` @ `933d2901`)  
**Phase:** [PHASE_3.md](../../v2/PHASE_3.md) **T1** · Architecture: [V2.md §5.8](../../v2/V2.md) · Behavioral model: [ADR-0008](../../adrs/0008-caching-marker-interfaces.md)

## Goal

Ship the **full behavioral TypeScript twin** of the .NET `D2.Shared.Caching.*` stack — abstractions (markers + building blocks + backplane), local default, Redis distributed + invalidation backplane, and tiered composition — so multi-instance Node workloads (BFF as mesh member, future Node services) keep L1 coherent via the shared Redis pub/sub channel `d2:cache:invalidations` with the universal **"everyone acts"** rule.

Success = package layout mirrors `server/shared/dotnet/caching/`; every Basic / Atomic / Broadcast / Set surface that .NET exposes has a TS counterpart returning `@d2/result` shapes; unit + Testcontainers Redis integration prove package-local parity; V2/PHASE/PARITY/KEEP docs describe the twin as in-scope (stale “cache packages dropped” framing removed).

## What shipped

| Step | Commit / package | Notes |
| --- | --- | --- |
| 01-abstractions | `99724826` · `@d2/caching-abstractions` | Markers, building blocks, options, InputFailures, serializer seam |
| 02-local-default | `b49f6b3d` · `@d2/caching-local-default` | In-process L1 + atomics; deliberate post-dispose + numeric hardening vs .NET |
| 03-distributed-redis | `0010d5aa` · `@d2/caching-distributed-redis` | Redis store + invalidation backplane; Lua pins; IT |
| 04-tiered | `7b04bd70` · `@d2/caching-tiered` | L1+L2 composition; L2-first; everyone-acts L1 drop |
| 05-docs-parity + FR fixes | `04edf08d` | PARITY/PATTERNS/PHASE_3/ADR honesty; `setMany` pipeline error path; log-op named constants; harness renames; fingerprints |

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
- .NET caching behavior changes (read-only twin reference).
- Dual-runtime `ts === cs` assert suite (KOM-01..08 deferred).
- Spec/codegen catalogs for cache keys.

## Cross-cutting decisions (locked)

| # | Decision | Choice |
|---|----------|--------|
| **D1** | V2 “no TS cache packages” | **Override as stale** — T1 authoritative |
| **D2** | BFF / Node write posture | **Full twin** (not subscribe-only L1) |
| **D3** | Surface scope | **T1-FULL** Basic+Atomic+Broadcast+Set+tiered+backplane |
| **D4** | Package layout | Mirror .NET under `typescript/caching/*` |
| **D5** | Invalidation channel | Share `d2:cache:invalidations` |
| **D6** | Parity bar | Full behavioral runtime twin; package-local ITs (not invent dual-runtime corpus) |

## Process integrity

- Orchestrator-only main thread; Grok Build `grok-d2-*` roles.
- Per-step Plan → Plan-Audit (as required) → Implementer → targeted code-audit Y ⊆ K=7 → dirty-only re-dispatch.
- Step 02: **user skip dirty R3** after CA-R2-E-1 (§13.14 trail in local journal + root README).
- **FINAL-REVIEW:** full K=7 vs `nova` + working tree; Fixer rounds closed product findings (incl. `setMany` pipeline errors, AbortSignal double fidelity, §21.11 named log ops, §26.20 reseed); FR Plan-Audit of thin FR Plan CLEAN (R1 + dirty R2).
- FR Latest fold: **zero FINDING** (R4).
- No `rules.md` predicates proposed at SHIP (table empty).

## Kinds-of-misses (distilled)

| ID | Class | Detail | Follow-up |
| --- | --- | --- | --- |
| KOM-01..08 | §26.12 dual-runtime constants | Hand-mirrored defaults / meters / Lua / channel / tiered log semantics with package-local pins only | Future dual-runtime suite: assert TS ↔ .NET constants / semantics (tiered: semantics only, not LoggerMessage byte-equality) |
| FR product | §18 / §1.32 | `setMany` ignored ioredis per-command errors; hollow AbortSignal double | Fixed in `04edf08d` with regression units |
| Process | §24 | Step journal Latest lag vs READY partials; FR Plan-Audit timing | Forensic journal merges + real FR Plan-Audit before SHIP |

## For REVIEW (user decisions)

1. **.NET `DefaultLocalCache` post-dispose lock ops** — keep working after `Dispose()` contrary to .NET package README; tracked in PHASE_3 Deferred D1. TS twin enforces documented contract. 0028 forbids .NET fix.
2. **TS twin deliberate deltas** — uniform post-dispose throw; non-finite/non-safe-integer numeric hardening (step 02).
3. **KOM-01..08** — approve deferred dual-runtime suite as follow-up work (or schedule).

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
| Doc gates | PARITY, PATTERNS Cache dual pointer, parent TS README, package READMEs, PHASE_3 T1, ADR-0008 |
| Build / inspectcode (.NET) | N/A for TS-only product path; TS unit suites green at last Implementer/Fixer runs; `check-baselines` 90/90 |
| Commits | Only after explicit user permission (cycle-commit marker path) |

**The deliverable is ready for user REVIEW.**

Spot-check anchors (committed product):

- Packages: `server/shared/typescript/caching/**`
- Docs: `docs/PARITY.md`, `docs/PATTERNS.md` (Cache), `docs/adrs/0008-caching-marker-interfaces.md`, `docs/v2/PHASE_3.md` T1
- Tip commits: `99724826`, `b49f6b3d`, `0010d5aa`, `7b04bd70`, `04edf08d`
