<!--
  Copyright (c) 2026 DCSV. All rights reserved.
  WHO: Deliverable workspace (gitignored) — orchestrator-owned Plan artifact.
  Deliverable 0012 — Convert bare TK key-string assertions to TK constants in tests.
-->

# 0012 — TK constants in tests (repo-wide)

> Workspace root README — the deliverable-level Plan artifact (Living State + cross-cutting decisions + step list). Per-step journals live in the numbered subfolders.

## Goal

Replace bare translation-key string literals in **test assertions** with references to the generated TK constant catalog, repo-wide, on `n/validation-followups`. Surfaced by the validation K=12 audit as rules.md §12.5.

## Why

Bare literals are refactor-blind: a key rename in `contracts/messages/en-US.json` regenerates the catalog but leaves bare-string tests asserting a stale literal with no compile-time signal. A constant ref makes a rename a compile error in the test. Production code already uses `TK.*`; tests are the gap. Catalogs:

- .NET `DcsvIo.D2.I18n`: `TK.Common.Errors.NOT_FOUND` is a `TKMessage`; raw key via `.Key`. Import `using DcsvIo.D2.I18n;`.
- TS `@dcsv-io/d2-i18n/keys`: `TK.common.errors.NOT_FOUND` value **is** the string. Import `import { TK } from "@dcsv-io/d2-i18n/keys";`.

Orphan check (recon): **every in-scope bare key has a matching constant in BOTH runtimes — zero orphans.** A converted assertion's expected value is byte-identical → green test proves behavior-preserving.

## Locked decisions (with user)

| # | Decision | Rationale |
| --- | --- | --- |
| D1 | Scope = repo-wide, on `n/validation-followups` | User choice at PLAN |
| D2 | Carve-outs keep bare literals (see below) | The literal IS the test there |
| D3 | Audit cadence: targeted per-step + full K=12 at FINAL-REVIEW | §13.14-authorized deviation (mechanical low-risk); user-approved in plan |
| D4 | No commit without per-occurrence permission | rules.md §13.1 |
| D5 | Pause for user after each step's gates green before next step | feedback_pause_between_steps |
| D6 | `@dcsv-io/d2-time`: add `@dcsv-io/d2-i18n` dep + convert BOTH production (`types.ts`) AND test (`types.test.ts`) — Option 3 | User decision (§13.4/§13.14 scope expansion); no circular dep (verified); `types.test.ts` is NOT a carve-out. Triggers a `pnpm install` (no containers running → low-risk now; node-init recreate needed before next container start). |

## Carve-outs — KEEP BARE (treated `⚪ N/A` by audits, not findings)

1. **i18n generation-verification tests** — `Unit/I18n/TKGeneratedTests.cs`, `Unit/I18n/TKMessageTests.cs`, `typescript/i18n/tests/tk-keys.test.ts`. Assert the catalog itself; converting → tautology.
2. **Cross-language parity/wire-contract tests** — `typescript/contract-tests/tests/{d2result-envelope,input-error,tk-message}.parity.test.ts`. Bare literal = drift tripwire independent of the catalog.
3. **Simulated-wire INPUT fixtures** — PascalCase `{ Key: "..." }` in `gateway-response.test.ts` (raw .NET JSON entering `normalizeKeys`). Only the post-transform camelCase assertions convert.

Out of scope: `ErrorCodeSpecLoaderTests.cs:48` `"validation_failure"` (not a TK key); generated catalog files; production source (already compliant).

## Steps (prerequisite order)

| NN | Step | Scope | Project/Gate |
| --- | --- | --- | --- |
| 01 | .NET test project | 10 files: Validation/Location/HandlerRepo/Result tests | `DcsvIo.D2.Tests` — build 0W + inspectcode 0W + dotnet test |
| 02 | TS shared packages | validation/default (3 tests) + `@dcsv-io/d2-time` { dep add + `types.ts` prod (2) + `types.test.ts` (10) + README + `pnpm install` } — **15 conversions** (D6/Option 3) | type-check:test + vitest + eslint/prettier |
| 03 | TS web client | 3 files: gateway-response (assert side) + gateway-client + auth-gateway-client | svelte-check + vitest + eslint/prettier |
| — | FINAL-REVIEW | whole deliverable | full K=12 + full gates |

## Cross-cutting decisions

| Topic | Decision |
| --- | --- |
| Conversion method | Replace only the expected-value literal; asserted value unchanged (no behavior change). |
| .NET form | `.Be("x_y_Z")` → `.Be(TK.X.Y.Z.Key)`; match `using DcsvIo.D2.I18n;` import style of files already using `TK.*`. |
| TS form | `.toBe("x_y_Z")` → `.toBe(TK.x.y.Z)`; add `import { TK } from "@dcsv-io/d2-i18n/keys";`. |
| Platform parity | .NET + TS validation conversions mirror each other. |
| Completeness check | Post-step grep converted files for residual bare TK literals → expect zero (carve-outs unchanged). |

## Living State

- **Step 1 (.NET test project)**: ✅ COMPLETE — 63 conversions across 10 files + 3 §7.14 wraps. Gates green (build 0W · inspectcode 0 · 4502 tests pass). Round 1 audit: A & C CLEAN, B 2× FINDING-LOW §7.14 → fixed + closure verified by absence.
- **Step 2 (TS shared)**: ✅ COMPLETE — Option 3 (D6): 15 conversions (3 validation + 2 prod `types.ts` + 10 time test) + `@dcsv-io/d2-i18n` dep on `@dcsv-io/d2-time` (pnpm-lock surgical 3-line, no circular dep, no tsconfig change) + `time/README.md` doc parity. Gates green (type-check ×2 · vitest ×2 [37+97 tests, 100% cov] · eslint · prettier · completeness grep zero). Round 1 audit: A & B CLEAN; C's HIGH (uncommitted Step-1 co-mingling) assessed as NON-FINDING (workflow accumulates uncommitted; §13.1 needs per-occurrence permission) → SHIP-time staging action item. 0 valid findings.
- **⚠️ Operational follow-up**: the `pnpm install` rotated workspace symlinks — user needs a **node-init recreate + restart of Node services before next container start** (`feedback_pnpm_install_symlink_rotation`).
- **Step 3 (TS web client)**: ✅ CONVERSIONS COMPLETE — 9 conversions (gateway-response 7 + auth-gateway-client 2; gateway-client untouched, already used `TK.*`). Audit Round 1: A/B/C all CLEAN, 0 valid findings. Type-correct via cclsp diagnostics (clean), prettier-conformant, line-length clean, headers preserved. Carve-outs honored: 7 wire-INPUT fixtures + 4 orphan keys stay bare.
- **⚠️ Step 3 GATE-EXECUTION GAP (resolved — deferred per user)**: `server/web` (`d2-sveltekit`) is excluded from the root `pnpm-workspace.yaml` by design; both `cd server/web && pnpm install` (installs the root workspace, skips web) and `pnpm install --ignore-workspace` (`ERR_PNPM_WORKSPACE_PKG_NOT_FOUND` on `@dcsv-io/d2-auth-bff-client@workspace:*` etc.) fail to install it on the host — the TS side is mid-refactor. **User decision: "forget about server/web ... its nbd atm"** → web CLI gates (svelte-check/vitest/eslint/prettier) DEFERRED to container/CI. Web conversions verified via cclsp/tsserver (clean) + clean K=3 audit + byte-identical (provably value-preserving). Recorded in attestation as a known, user-acknowledged deferral.
- **Orphan discovery**: 4 test-only mock keys (`auth_errors_{BEARER_INVALID,BEARER_MISSING,INVALID_OR_EXPIRED_JWT}`, `common_validation_EMAIL_REQUIRED`) have no catalog entry → correctly stay bare. Not a production gap (test-fixtures only). The earlier recon's "zero orphans" was convert-list-scoped.
- **FINAL-REVIEW**: ✅ CONVERGED — full K=12 (canonical A1–E2 partition) over the whole deliverable. 11/12 clusters CLEAN; D1 surfaced §11.29 (FINDING-MED: parent TS README Mermaid graph missing `Time` dep edges) → fixed (`Time --> Result` + `Time --> I18n`) → CLOSED (D1 Round-2 PASS). E2 raised 4 §24 audit-FORMAT findings (per-step journal granularity) → orchestrator-dispositioned (see attestation). FINAL-REVIEW journal: 56-row big table, zero FINDING = convergence. Gates: `dotnet build` 0W · `inspectcode` 0 · contract-tests parity 2145 pass · TS-shared 134 pass. 1 flaky out-of-scope integration test (`DlqTests`, not in diff).
- **Status**: SHIP prep — FINAL-REVIEW converged; deliverable ready for user REVIEW (with explicit caveats — see attestation).

## Kinds-of-misses log (distillation — filled per step)

- **Step 1 — Planner worklist under-count (62 vs 63)**: the Plan's enumeration missed one occurrence inside a `.NotContain(...)` call (`D2ResultCombineTests`). Caught by the Implementer's completeness grep. _Candidate refinement_: TK-literal enumeration must scan ALL assertion verbs (`.Be`/`.NotContain`/`.Equal`/`.Contain`), not just `.Be`.
- **Step 1 — literal→constant expansion crossed §7.14**: replacing a bare literal with `TK.X.Y.Z.Key` lengthens the line; 2 converted lines exceeded 100 chars (+1 pre-existing in a touched file). _Candidate refinement_: any literal→longer-symbol refactor should run an `awk 'length>100'` pre-flight as part of the Implementer's own checks before audit.
- **Step 2 — Auditor false-positive on commit-boundary**: Auditor C flagged the uncommitted Step-1 files co-mingled in the working tree as a HIGH "step/commit-boundary violation" — but this workflow accumulates uncommitted across steps and commits-with-permission at SHIP (then squashes into nova). _Candidate refinement_: audit dispatch briefs should state the commit model so auditors don't false-positive on the normal accumulate-then-squash state (the §13 lens is "no commit WITHOUT permission", not "must commit per step").
- **Step 3 — recon "zero orphans" was convert-list-scoped**: the deliverable recon verified only the keys it planned to convert, missing 4 test-only mock keys with no catalog entry. The Step-3 Planner caught them. _Candidate refinement_: an orphan-scan must enumerate EVERY TK-shaped literal in the in-scope files (incl. ones that won't convert), not just the planned convert-list — so orphans are classified up front, not discovered mid-implementation.
- **Step 3 — web CLI gates can't run on host (standalone-project install gap)**: `server/web` isn't in the root pnpm workspace and isn't installed on the host, so svelte-check/vitest/eslint/prettier couldn't run; only cclsp/tsserver type-resolution was available. _Candidate refinement_: any deliverable touching `server/web` must plan for CLI-gate execution via the container/CI path up front (the host can't run them), and the PLAN should say so — otherwise the gate-green claim is incomplete.
- **FINAL-REVIEW — targeted per-step cadence (D3) under-shoots the completeness-checklist per-step gates**: E2 correctly flagged that per-step journals are per-category (~26 rows) not per-subsection (~280), Step-1 had no per-step Round-2 clean sweep, and §24 self-audit rows weren't per-step. These are CONSEQUENCES of the user-authorized D3 targeted-cadence (targeted per-step + full K=12 at FINAL-REVIEW). _Candidate refinement_: when D3 is authorized, the completeness checklist's per-step big-table/Round-2/§24-self-audit gates are satisfied at the FINAL-REVIEW level (which IS per-subsection + zero-finding + §24-self-audit) — the attestation must state this explicitly so the targeted per-step journals aren't mistaken for full-audit gaps.
- **FINAL-REVIEW — full `dotnet test server/D2.slnx` pulls in flaky Testcontainers integration tests**: Microsoft.Testing.Platform ignores the VSTest `--filter Category=Unit`, so the full suite (incl. RabbitMQ/Docker integration) ran; `DlqTests.HandlerReturnsFailure_MessageGoesToDlq` flaked on timing (passed 4502/4502 in earlier identical runs, not in our diff). _Candidate refinement_: for test-only deliverables, scope the .NET test gate to the affected project/unit category via the MTP-native filter, and treat infra-dependent integration flakes as out-of-scope when the diff proves no overlap.

## SHIP checklist (deferred to ship, with approval)

- [ ] rules.md §12 predicate (TK constants in test assertions + 3 carve-outs)
- [ ] Update memory `feedback_tk_constants_not_literals` (test side + carve-outs)
- [ ] docs/TESTS.md convention note
- [ ] Snapshot README → `docs/dev/deliverables/0012-tk-constants-in-tests.md`
- [ ] **Deliberate staging at commit** (Step 2 audit action item): stage only the intended deliverable files; EXCLUDE the `TK.g.cs` CRLF-only artifact; decide commit structure with the user (single deliverable commit vs per-step) — the branch squashes into nova regardless.

## Final attestation (completeness-checklist walk — honest, with explicit caveats)

Walked the rules.md Deliverable completeness checklist. **Code, gates, and FINAL-REVIEW: clean.** Three boxes are YES *only under explicitly user-authorized process deviations* — stated transparently rather than papered over (a false blanket "every box YES" would be a §24.0 integrity breach):

1. **Per-step big-table granularity / per-step Round-2 / §24 self-audit rows** → satisfied at the FINAL-REVIEW level per decision **D3** (§13.14-authorized targeted-per-step cadence). The per-step journals are targeted (per-category, scoped to each step's mechanical risk). The FINAL-REVIEW journal carries the full record: 56-row big table with §24.0a–§24.18 self-audit expanded, **zero FINDING** (convergence), whole-deliverable sweep. Step-1's 2 §7.14 FINDING-LOW are closed by absence in the FINAL-REVIEW fresh sweep (B1 §5 / B2 §7.14 PASS) + orchestrator `awk 'length>100'`/grep at step close.
2. **TS-web CLI gates** (svelte-check/vitest/eslint/prettier) → **deferred to container/CI** per user ("forget about server/web ... nbd atm"); `server/web` is not host-installable (excluded from root workspace; `workspace:*` deps unresolvable standalone). Web conversions verified via cclsp diagnostics (clean) + clean K=3 audit + byte-identity.
3. **Cross-cutting docs (rules.md §12 predicate, TESTS.md note)** → **SHIP-time items** pending user approval (per plan).

**Verified clean (citations):** `dotnet build server/D2.slnx` 0W/0E · `jb inspectcode` 0 issues · .NET unit scope green (one flaky out-of-scope integration test `DlqTests.HandlerReturnsFailure_MessageGoesToDlq` — zero messaging/integration files in the diff, byte-identical baseline path, passed 4502/4502 in earlier runs) · TS-shared `@dcsv-io/d2-validation` 37 + `@dcsv-io/d2-time` 97 · contract-tests parity 24 files / 2145 tests pass (carve-out parity tests intact) · doc parity (`time/README.md` deps + parent `server/shared/typescript/README.md` Mermaid graph §11.29 fix) · no commit · no destructive git · all deferrals user-authorized · no phase/conversation-ID verbiage in source (D2 §14).

**Attestation:** I attest the deliverable's CODE is complete and verified, the FINAL-REVIEW converged zero-finding (one real finding §11.29 fixed+closed), and the only open items are the user-authorized deferrals + SHIP-time doc/rule application listed above. Ready for user REVIEW.

Journals: [01-dotnet-tests](01-dotnet-tests/journal.md) · [02-ts-shared](02-ts-shared/journal.md) · [03-ts-web](03-ts-web/journal.md) · [04-final-review](04-final-review/journal.md)
