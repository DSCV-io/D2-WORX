<!--
Copyright (c) DCSV. All rights reserved.
Committed snapshot of the gitignored deliverable-0030 workspace root
(docs/wip/0030-edge-host/README.md), captured at SHIP 2026-07-12.
Per-step and final-review journals remain local-only under docs/wip/0030-edge-host/ (gitignored).
-->

# 0030 — Edge host + transport platform slice (Landing 1)

**Status:** SHIPPED 2026-07-12 (FR_FULL + post-FR residuals)  
**Branch:** `n/edge-host` @ **`8954da31`** (merge-base `nova` @ `9fcdffd1`)  
**Type:** Product — Edge host, TypeSpec process-kind platform, multiproc Audit PoC

## Goal (honest end state)

1. **Edge process** boots (Compose + test host), healthy, composes KeyCustodian **in-process**.
2. **Generated** well-known HTTP + **generated** KC gRPC under **`D2.Edge.Api`** (not TestServer-only production path).
3. **TypeSpec emitters** understand **process kind** (`edge-module` vs `standalone`): public HTTP only on Edge; standalone gRPC on service.Api; **typed Edge→service gRPC bridge** generated up front.
4. **Stub standalone Audit** multiproc path: public HTTP → Edge pipeline → generated bridge → **`https://d2-audit:8443`** gRPC (NIE) with **complete mTLS** rails + establishment on both hosts + honest Redis/tiered (AuthConfigure on).
5. **Docs/KEEP** tell the truth (`d2-keycustodian`, multiproc honesty, private-CA JWKS, mTLS-only product gRPC).

## What shipped (product)

| Area | Landed |
| --- | --- |
| TypeSpec | Process-kind dual-emit, routes ns, Edge bridge emitter, multi-package KC + Audit, regen COPY_MANIFEST / byte-parity |
| Edge host | `AddD2EdgeHost` / `UseD2EdgePipeline` / three-bind Kestrel (8080 / Issuer 8443 / mTLS 9443) / well-known Map / pipeline with reserved rate-limit **slot empty** |
| KC transport | Six gRPC services + well-known JWKS/OIDC on Edge; **MapWhen isolates KC gRPC to mTLS :9443 only** |
| Audit multiproc | Dual-bind Audit (HTTP health only + mTLS gRPC); Compose `d2-edge` / `d2-audit`; CSR PoC outbound leaf issuer |
| Auth residual | Private-CA OIDC/JWKS trust (`TrustedRootCertificatePath`); **issuer in-process JWKS** (no Edge self-HTTP); `AUTH_REQUEST_ORIGIN_UNESTABLISHED` platform deny after Origin establish |
| System plane (D7) | `ISystemWorkScopeFactory` only entry for System work; dual-path Auth context; KC seed/rotate unblocked |
| Env law (D8) | Host product config env-only (`§23.9` / `§23.10`) |
| Tests | Shared / Edge / Audit suites green at residual tips; §1.22 matrix + seam inventory (S5) |

## Explicitly OUT of this deliverable

| Item | Tracker |
| --- | --- |
| JWT boundary mint on general Edge | AUTH-R3 — Auth module deliverable |
| Full dual-factor multiproc ping with real Bearer | AUTH-R4 — after mint |
| Edge rate-limit middleware body | D-SEC-01 defer — [PHASE_3_RATE_LIMITING.md](../v2/PHASE_3_RATE_LIMITING.md) |
| Product Auth REST / YARP / first-leaf foreign-workload bootstrap | Phase 3 Edge/Auth track |

## Steps (committed)

| Step | Commit (subject) | Audit |
| --- | --- | --- |
| 1 Emitter platform | `d71849bb` | CLEAN |
| 2 Edge host shell | `0dcecb94` | CLEAN |
| 3 KC gRPC home | `f784f91f` | CLEAN |
| 4 Audit multiproc + Docker | `f8370484` | CLEAN |
| 5 Test bar + docs honesty | `0abc9bad` | CLEAN |
| Residual D7/D8 + multiproc | `1799bee7` | FR residual R4/R5 product open 0 |
| AUTH-R1/R1b private-CA + in-process JWKS | `fe23463d` | Residual security + tests |
| mTLS-only gRPC + Unestablished deny | `8954da31` | Y audit A–F → dirty CLEAN |

Local journals (gitignored): `docs/wip/0030-edge-host/0{1..5}-*/journal.md`, `final-review/journal.md`, residual partials under `final-review/`.

## Locked decisions (summary)

| ID | Ruling |
| --- | --- |
| D1 / 1A | Dual emit by process kind; typed gRPC bridge; public HTTP Edge-only |
| D2 / 2C | RequestOrigin on every host day one |
| D3 / 3B | Honest PoC end state (no half-rails) |
| R2-HA | Honest A across Redis/Auth/mTLS |
| D4–D6 | Placement law; docs truth; `UseD2EdgePipeline` composition |
| D7 | System work plane via `ISystemWorkScopeFactory` only |
| D8 | Env-only host product config |
| Q1 | AuthConfigure on; **no** JWT mint on general host |

## Multiproc / private-PKI honesty (operator)

**Proven:** container health, Active KC keys, JWKS **publish** 200, auth **gate shapes** (401 noauth / kid-not-found), mTLS wiring, private-CA OIDC trust, issuer in-process JWKS, Compose Watch hot-reload, mTLS-port isolation for product gRPC, platform Unestablished deny on gRPC product paths.

**Not proven:** full dual-factor authenticated Audit ping with boundary-minted Bearer (blocked on AUTH-R3).

Canonical residual ledger: [PHASE_3_AUTH.md §15b](../v2/PHASE_3_AUTH.md).

## Process integrity

- **FR mode:** **FR_FULL** (product full K=7 + FR journal).
- **FR code-audit:** R1 → Fixer → R2 → **R3 CLEAN** (pre-smoke); multiproc surfaced D7 → residual **R4 full K=7** + **R5 dirty product open 0**; residual pure-meta Plan-Audit **Y=E+G** READY (N1 disposition honesty / FR-R5-G-2 path).
- **Post-FR product residuals** (AUTH-R1/R1b, D-SEC-02/03): dedicated implement + Y general audit CLEAN (not a silent re-claim of FR without residual walks).
- **Commits:** only with explicit per-occurrence permission + cycle-commit.
- **D-SEC-01:** user-locked document+defer (rate-limit not invented).

## Completeness checklist (SHIP gate)

Walked against rules.md Deliverable completeness checklist before attestation:

| Gate class | Result | Citation (summary) |
| --- | --- | --- |
| Per-step journals 01–05 + CLEAN sweeps | YES | Local step journals; commits `d71849bb`…`0abc9bad` |
| FR_FULL journal + clean full-catalog residual | YES | Local `final-review/journal.md` R5/R6 residual CLEAN; residual PA E+G READY |
| Post-FR residual audits CLEAN | YES | AUTH-R1/R1b; D-SEC Y A–F + dirty R2/R3.1 CLEAN |
| Docs / Doc Update Map | YES | PHASE_3_AUTH §15b; Edge/Audit/KC/PATTERNS; pipeline/rate-limit honesty |
| No mint on general Edge | YES | DI isolation tests; Q1 lock |
| Gates (build/tests residual) | YES | Residual Shared/Edge/Audit green; solution build at SHIP walk |
| Process permissions | YES | cycle-commit markers; no force-push |
| Deferred work named | YES | AUTH-R3+; D-SEC-01; Phase 3 rate-limit |

## Kinds of misses (distilled)

| Class | Example | Candidate rule / note |
| --- | --- | --- |
| Multi-bind shared Maps | KC gRPC on Issuer port | Prefer bind/role isolation for crown-jewel gRPC; platform Unestablished deny as second factor |
| Issuer self-HTTP JWKS | Edge ConfigurationManager self-fetch | Issuer-host in-process JWKS after AddD2Auth |
| Private-CA OIDC | OS trust store only | Explicit TrustAnchor path for OIDC HttpClient |
| Residual Plan disposition | Skip vs Y for pure-meta residual | Residual pure-meta Plan-Audit Y (not Skip) when new public types |
| Completeness pin lag | Registry count/lists after new AUTH code | Update hand catalogs + count pins in same change as spec code |

## Rules added at SHIP (user-approved)

| Predicate | Surface |
| --- | --- |
| **§10.23** | Multi-bind product gRPC → mTLS listen only; internal HTTP = infra only |
| **§10.24** | Platform Unestablished deny on product gRPC after Origin establish |
| **§9.47** | Issuer-host in-process JWKS (no HTTP self-fetch of own well-known) |

Lockstep: `docs/dev/rules/09-…`, `docs/dev/rules/10-…`, `AGENTS.md` §5.

## Final report

0030 delivers the Edge landing platform: process-kind emitters, Edge host composition, KC transport home, multiproc Audit PoC with honest mTLS and Redis rails, System work plane, private-CA JWKS trust, issuer in-process JWKS, and structural gRPC isolation + Unestablished deny. Next product value: **Auth module boundary mint (AUTH-R3)** then full dual-factor multiproc proof (AUTH-R4). Rate-limit remains a separate Phase 3 deliverable.

## Attestation

I attest that this deliverable's process integrity has been verified against the deliverable completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is YES. The deliverable is ready for user REVIEW.

Spot-check: local `docs/wip/0030-edge-host/final-review/journal.md`; residual audits under `final-review/audit-dsec-mtls-r1/` and `final-review/security-deep-vs-nova/`; multiproc notes in local `final-review/MULTIPROC-SMOKE-AND-PLATFORM-FINDINGS.md`.
