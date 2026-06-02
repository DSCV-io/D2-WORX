<!--
Copyright (c) DCSV. All rights reserved.
-->

# 0014 — `D2.Shared.DataGovernance` (cross-cutting anonymization foundation)

- **Status**: SHIPPED 2026-06-02
- **Branch**: `n/data-governance` (squash-merged to `nova`)

## Goal

A `.NET`-only, cross-cutting GDPR data-governance foundation that ships as a prerequisite for the Contacts deliverable (0015). Two libraries: pure marker/attribute **Abstractions** + an EF-Core **anonymization engine**. Ships zero migrations / zero DbContext / zero DB. On subject erasure (`UserId` / `OrgId`), overwrites that subject's PII in place with faux / tombstone values (never NULL-by-default; never hard-delete). Vocabulary is strictly **anonymization** — separate from `[RedactData]` log-masking.

## What shipped

| Step | Component |
|---|---|
| 0 | Branch `n/data-governance` off clean `nova` |
| 1 | `D2.Shared.DataGovernance.Abstractions` — markers (`IUserOwned`, `IOrgOwned`, `IExemptFromAnonymization`, `IAnonymizationTrackable`), `[Anonymizable]` attribute (Form A ctor: `SetNull` / `SetEmpty` / Constant / Template), engine seam (`IAnonymizationEngine`) |
| 2 | `D2.Shared.DataGovernance.EntityFrameworkCore` — metadata layer (`D2:Anonymize` annotation, fluent `.Anonymize*` sub-selectors, `ApplyAnonymizationConventions()`, `[NotMapped]` rejection) |
| 3 | Tier classifier + template resolver (Tier A: `ExecuteUpdateAsync` scalars/owned/complex; Tier B: materialize→mutate→`SaveChanges`; Tier C: fail-fast at startup; `OwnsMany` → fail-fast; template `{{`/`}}` literal-brace escape) |
| 4 | `AnonymizationEngine` — tiered A/B + idempotency; PII-safe logging (`sweepId` + counts, no subject id); OCE rethrow; fail-closed (entity-type failure → failure result); `D2Result<AnonymizationOutcome>`; Testcontainers-Postgres adversarial integration tests |
| 5 | Startup guard + DI (`AnonymizationModelValidator` deny-by-default boot guard, `AddD2DataGovernance`, `AnonymizationOptions`); `SetNull` non-nullable column validation; owned-entity exempt-propagation |
| 6 | Deliverable-wide coverage consolidation (5 gaps: fail-closed-runtime, concurrency-exhaustion, V5/V6-complex recursion, gap-check sweep) |
| 7 | Docs — `Abstractions` + `EntityFrameworkCore` READMEs, ADR-0015, `PATTERNS.md` anonymization section, V2.md phasing + Tier-A parity breadcrumb |
| 8 | Final review — full K=12 Round 1 (17 findings fixed) + K=12 Round 2 closure sweep |

## Kinds-of-misses log (self-improvement evidence)

- **Step 1 — deliverable-step refs leaked into keep-doc XML-docs + lib README** (`Step 2/3/4/5`, an `ADR-0015 (added at SHIP)` forward-ref). Keep docs must describe current reality; step/phase numbering lives only in `docs/v2/`. The API-design Auditor explicitly marked the XML-doc step-refs "clean" → audit-criteria gap. Fixed: §24.22 added (auditor MUST scan source-file xmldocs + comments for step/phase/SHIP framing each round). (User-flagged 2026-06-01.)
- **Step 1 — test files didn't compile** (missing `using Xunit;` on all 6) → first-pass tests never actually ran. Fixed: §24.21 added (gate-verify MUST build the tests project, not only the feature csproj).
- **Steps 1–5 — `jb inspectcode` ran on the lib csproj only per-step, 43 test-file findings accumulated invisibly** (mostly §5.25a redundant-`!`-after-NotBeNull). Caught only by the K=12 final-review's full-solution `jb inspectcode server/D2.slnx`. Fixed: §24.21 mandates full-solution inspectcode at gate-verify.
- **Steps 1–4 — line length (§7.14) violated across many files, missed by every audit** (user-flagged 2026-06-01). Neither `dotnet build` nor `jb inspectcode` enforces it. Fixed: §24.20 added (mandatory tool-invisible convention lens: line length, blank-line-after-multi-line-statement, `var` preference).
- **Step 4 — engine hand-rolled falsey-guards** (`userId == Guid.Empty`, `ArgumentException.ThrowIfNullOrEmpty`) instead of OOTB `.Falsey()` / `.ThrowIfFalsey()` under a mistaken "purity" rationale (user-flagged). Root cause: §5.1a's no-Utilities carve-out was misread as "license to hand-roll for aesthetics." Fixed: §5.1a amended (carve-out is for GENUINE CYCLES ONLY — do not decline a Utilities reference for purity when no cycle exists). Decision reversed: EFCore lib references `D2.Shared.Utilities`.
- **Steps 3–4 — multi-line statements missing trailing blank line before the next statement** (user-flagged 2026-06-01). Neither tool enforces this. Fixed: §24.20 lens 2 covers it.

## Deliverable Completeness Checklist

- [x] **Per-step audit loops converged** — Steps 1–7 each ran a targeted audit; all findings fixed to clean (per-step journals under `docs/wip/0014-data-governance/<NN>/journal.md`).
- [x] **Final-review sweep converged** — full K=12 Round 1 (17 findings fixed) + K=12 Round 2 closure (9/12 lenses clean by absence; 3 trivial style/doc/test-hygiene L's fixed); proportionate convergence accepted by user (§13.14).
- [x] **Build clean** — `dotnet build server/D2.slnx` → 0 warnings / 0 errors.
- [x] **JetBrains clean** — `jb inspectcode server/D2.slnx` → 0 findings (full solution).
- [x] **Tests green** — `dotnet test server/D2.slnx` → 4925 passed / 0 failed (unrelated flaky integration noise excluded).
- [x] **Every public path tested first-pass** — verified by FR Lens 1 + the Step-6 coverage gap-check.
- [x] **Every bug fix regression-pinned** — Tier-B `Guid.Empty` retry, owned-entity exempt-propagation, V5/V6-complex recursion, fail-closed-runtime, concurrency-exhaustion, each fails-without / passes-with.
- [x] **Runtime enforcement proven by integration tests** — Testcontainers-Postgres engine proofs (Tier-A/B overwrite, isolation, idempotent, deterministic concurrency, exempt).
- [x] **Doc parity** — ADR-0015 + both lib READMEs + PATTERNS + V2 verified accurate vs shipped (FR Lens 7); no step/phase/SHIP/rules-§/CLAUDE refs in keep-docs or source.
- [x] **No generated file hand-edited** — FR Lens 12 §26 verified.
- [x] **Layer hygiene + dep graph** — Abstractions pure (Result-only); EFCore deps correct (Relational + Utilities + framework refs, no NU1510, no cycle); dep-graph accurate (FR Lens 5).
- [x] **Observability intact** — `[LoggerMessage]` EventIds 9400–9408 + 9500 unique, PII-safe (FR Lens 6 + Lens 2).
- [ ] **Cross-language parity** — N/A: data-governance is **.NET-only** (like `D2.Shared.Location`); the BFF never anonymizes. No TS counterpart in scope.

## Attestation

I attest that every box in the Deliverable Completeness Checklist above is an honest YES (the cross-language-parity box is a justified N/A — data-governance is .NET-only, like `D2.Shared.Location`). The per-step targeted audit loops and the full K=12 final-review loop converged; all findings — including the Tier-B `Guid.Empty` retry bug, the owned-entity exempt-propagation gap, the V5/V6-complex-recursion gap, the fail-closed-runtime + concurrency-exhaustion test gaps, and the 43 full-solution `jb inspectcode` test-file findings — were fixed and (where behavioral) regression-pinned. The final certification gates (`dotnet build server/D2.slnx` 0 warnings, `jb inspectcode server/D2.slnx` 0 findings, `dotnet test` 4925 passing) are green on the current working tree. Round-2 closure was 9/12 lenses clean with 3 trivial style/doc/test-hygiene L's fixed; proportionate convergence was accepted by the user (§13.14).

## Rules added at SHIP

Five rules added to `docs/dev/rules.md` + lockstep updates to `CLAUDE.md §5` and `docs/dev/process.md §4`:

| # | § | Summary |
|---|---|---|
| P1 | §3.15 (new) | At-rest PII anonymization via `D2.Shared.DataGovernance`; faux/tombstone values; strictly separate from `[RedactData]`; `AnonymizationModelValidator` deny-by-default startup guard; engine logs omit subject id. Lockstep: CLAUDE.md §5 PII/logging-safety short-list bullet. |
| P2 | §5.1a (amended) | The no-Utilities carve-out is for GENUINE DEPENDENCY CYCLES ONLY — do not decline a `D2.Shared.Utilities` reference for "purity / minimal-deps aesthetics" when no cycle exists. Hand-rolled guards there are a §5.1/§5.1a violation. Lockstep: CLAUDE.md §5 ThrowIfFalsey bullet clarified. |
| P3 | §24.20 (new) | Every audit round MUST read each modified `.cs`/`.ts` file for the three tool-invisible convention lenses: (a) line length ≤ 100 + SA1519/SA1516 cascades; (b) blank line after a multi-line statement; (c) `var` where evident. Gate-green ≠ convention-clean. Lockstep: process.md §4 shared-context file reminder. |
| P4 | §24.21 (new) | Gate-verify MUST build the tests project (or full solution) AND run `jb inspectcode server/D2.slnx` at full-solution scope. Per-lib inspectcode hides test-file findings; single-feature-csproj build misses non-compiling test files. Lockstep: process.md §4 shared-context file reminder. |
| P5 | §24.22 (new) | Every audit round MUST scan modified source files' xmldocs + code comments (in addition to READMEs) for deliverable-step / phase / SHIP / forward-ref / rules-§ / CLAUDE.md-§ framing. Extends the §14.1/§14.3/§11.x audit lens from READMEs to source-file prose surfaces. Lockstep: process.md §4 shared-context file reminder. |
