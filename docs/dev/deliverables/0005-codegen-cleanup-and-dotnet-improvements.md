<!-- Copyright (c) DCSV. All rights reserved. -->

# Deliverable 0005 — codegen cleanup + .NET improvements

**Branch**: `n/codegen-cleanup` (from `nova` @ `0eeb8e81`)
**Status**: ⏸ SHIP-PENDING (awaiting user authorization for commit + squash + snapshot + PHASE_0.md update)
**Started**: 2026-05-12
**Type**: Pre-Phase-1 bridge work (.NET-only); enables 0006 TS bridge to consume migrated specs

## Context

First of two pre-Phase-1 deliverables locked in by commit `0eeb8e81` (per `docs/v2/PHASE_0.md` Pre-Phase-1 Plan section). Migrates two clusters of hand-mirrored .NET constants into JSON specs + SourceGens so:
1. Cross-cutting concerns stay in lockstep automatically (single source of truth)
2. Deliverable 0006 (TS bridge) can emit TS-side equivalents from the same specs

Both Sub-concern C (V2.md update) was completed by commit `0eeb8e81` itself; this deliverable executes Sub-concerns A, B, D.

Architectural decisions explicitly DEFERRED with revisit triggers (per PHASE_0.md):
- **JwtClaimTypes spec collapse** — existing parity test at `tests/Unit/Auth/JwtClaimTypesParityTests.cs:28` already prevents drift; spec'ing would fragment hand-authored xmldoc + introduce wart for 5 non-spec constants. Revisit if cross-language drift surfaces a real issue.
- **DbErrorCodes/DbFailureKind/PgErrorCodes triple** — files have 2 commits ever; structural drift catches via wildcard-throw at `BaseRepoHandler.cs:139-163` are excellent; only 1 provider exists. YAGNI. Revisit when 2nd DB provider's csproj is being built.

## Step plan

| #   | Step                                                    | Status | Rounds | Prerequisites |
|-----|---------------------------------------------------------|--------|--------|---------------|
| 0   | Branch checkout                                          | ✅     | —      | —             |
| 1   | Combined spec migration (AuthErrorCodes + Telemetry tags) | ✅     | 2      | none          |
| 2   | Deferred cleanups (JwtValidatorTests fix + utilities/README split) | ✅     | 1      | step 1        |
| F   | Final-review (deliverable-wide)                         | ✅     | 2      | all above     |

## Locked decisions (from PLAN discussion + PHASE_0.md Pre-Phase-1 section)

| Decision                                | Final                                                                                                        |
|-----------------------------------------|--------------------------------------------------------------------------------------------------------------|
| Step packaging                          | **2 substantive steps** (combined spec migration + cleanups) — user-directed simplification of the original 4-sub-concern split |
| AuthErrorCodes spec format              | JSON file at `contracts/auth-error-codes/auth-error-codes.spec.json` with entries `{ code, httpStatus, category, userMessageKey, factoryName, doc }` |
| Telemetry tags spec format              | JSON file at `contracts/telemetry/telemetry.spec.json` with per-meter sections enumerating instruments + closed tag value sets |
| SourceGen organization                  | Two separate SourceGen projects (one per spec type) for clean responsibility separation; no generic "spec-driven codegen" mega-lib |
| JwtClaimTypes                           | DEFERRED (parity test handles drift) |
| DbErrorCodes triple                     | DEFERRED (YAGNI for hypothetical 2nd provider) |
| Pre-flight grep discipline              | §24.13.1 canonical pre-flight grep checklist applies (mature; validated 11 cycles in 0004) |
| Cross-deliverable touches               | EXPECTED in Step 1 (auth lib + telemetry libs touch existing files); per §24.7 audit scope includes them |

## Kinds-of-misses log

### Step 1 — Combined spec migration (2 rounds)

Per `01-spec-migrations/journal.md` per-step distillation (`journal.md:1192-1232`):

- **§9.19 + §11.9 LOW × 2 (single root cause)** — duplicated private `FindRepoRoot()` helper using `CLAUDE.md` as the file-system marker, when the canonical `D2.Shared.Tests.Unit.Auth.TestPaths.RepoRoot()` helper (using `server/D2.slnx` marker) already existed and was used by `AuthScopesParityTests` / `AudiencesParityTests`. Implementer authored 2 new test files independently and reached for a "find repo root" pattern from generic experience instead of grepping the existing test infrastructure for a canonical helper. The §11.9 violation (CLAUDE.md citation in a KEEP doc — test files count) was a SIDE EFFECT of choosing CLAUDE.md as the walk-up marker, not an independent miss. **One root cause, two predicate surfaces.**
- **(hygiene) LOW** — empty `server/shared/dotnet/auth/Errors/` directory left after `AuthErrorCodes.cs` + `AuthFailures.cs` deletion. Implementer correctly flagged it for the Auditor (Things-flagged-for-Auditor #5) — caught at the right gate; Fixer cleaned up.

**Pattern**: Implementer's pre-flight grep discipline (§24.13.1) captured all 8 canonical checks with literal command output and zero hits — caught every canonical predicate violation at write-time. The §11.9 + §9.19 LOW findings emerged from a *different* surface (test-helper duplication) that no canonical pre-flight grep currently catches — a discoverability gap, not a missing predicate per se.

### Step 2 — Deferred cleanups (1 round, DOCS-MOSTLY)

Per `02-deferred-cleanups/journal.md` per-step distillation (`journal.md:792-823`):

- **(none — Round 1 produced zero FINDING rows; convergence on first sweep)**

**Pattern**: DOCS-MOSTLY scope + thorough Plan modify-table + cross-link rot mitigation pre-emptively addressed + verify-then-decide approach for hypothetical risks = first-pass clean. Reproduces the deliverable 0004 Step 6 one-round convergence shape. One §13.13 reconciliation honestly captured: Plan Risk #1 (cross-link rot — external KEEP docs deep-linking into `utilities/README.md` anchors) turned out unnecessary — `grep -rn 'utilities/README\.md#' server/ docs/` returned only the two prose mentions in this very journal's Plan section that DOCUMENT the mitigation. Zero actual deep-links existed. Implementer documented the negative finding via §13.13 instead of forcing redirector stubs to "satisfy" the Plan — exactly the discipline §13.13 exists to encode. The §11.21 sub-doc split executed cleanly: 368→65 line top-level + 6 sub-folder READMEs (max 177 at Extensions, others ≤76); violation closed genuinely, not shifted.

### Final-review (2 rounds — R1 1 finding cluster / R2 clean)

Per `final-review/journal.md` Round 1 + Round 2 sweeps (`journal.md:553-564`):

- **§7.15 American English LOW** — 6 sites across the deliverable, single root cause (cross-step + final-review enumeration-completeness gap in pre-flight grep regex). Auditor R1 cited 2 sites (`auth-error-codes-source-gen/ErrorCodesEmitter.cs:187` `recognised`; `auth/README.md:78` `materialise`); Fixer enumeration-completeness sweep surfaced 4 sister occurrences (`server/shared/dotnet/README.md:52` `catalogue`; `auth/Jwks/HttpJwksProvider.cs:163` `cancelling`; `tests/D2.Shared.Tests.csproj:22` `prioritise`; `auth-error-codes-source-gen/README.md:9` `catalogue`). Closed in R2 — broadened deliverable-scope British-spelling grep returns ZERO hits.

**Pattern**: Final-review caught what per-step audits + Implementer pre-flight greps missed — exact validation of why deliverable-wide sweeps exist. Root cause: §24.13.1's canonical pre-flight grep checklist's American-English regex (line 1822 of `rules.md`) uses bare lemmas (`analyse|colour|behaviour|cancelled|honour|...`) and missed conjugated forms (`cancelling`, `prioritise`, `catalogue`, `materialise`, `recognised`). Bare-lemma regex at the predicate level → bare-lemma greps at every Implementer + per-step Auditor walk → 6 leaks across both Steps 1 and 2 + the Final-review Implementer's own PATTERNS.md addition. Mechanical fix to the canonical checklist closes the entire class. (§7.15's own inline regex at line 718 has the same gap — fix should target both lines.)

### Validation of LIVE 0004 predicates

The 9 LIVE predicates from deliverable 0004 (§1.19, §5.25, §11.28, §13.13, §14.1, §11.9, §11.21, §24.13, §24.13.1) were exercised across this deliverable as follows:

- **§1.19 (per-step integration tests for wire-up risk libs)** — VALIDATED in Step 1 via `ErrorCodesGeneratorTests.cs` + `TelemetryTagsGeneratorTests.cs` driving `CSharpGeneratorDriver` with synthetic compilation + `AdditionalText` (the integration surface for SrcGens) + `AuthErrorCodesVsTelemetrySpecConsistencyTests.cs` providing test-time defense complementing build-time `D2TEL006`. N/A in Step 2 (no wire-up surface).
- **§5.25 (nameof discipline at codegen-emit sites)** — VALIDATED & MOTIVATING in Step 1 — this predicate IS the motivation for Sub-concern B. Pre-flight grep `"outcome"|"trigger"|"d2_error_code"` returns ZERO production hits in `auth/` + `auth-outbound/`; remaining literals at `D2RpcStatusExtensions.cs:128` (`TRAILER_ERROR_CODE`) + `D2ProblemDetailsExtensions.cs:122` (`EXTENSION_ERROR_CODE`) qualify under the §5.25 EXEMPTION (public-API wire-format constants). Spec entries themselves carry the wire-format anchors (also EXEMPTION). N/A in Step 2.
- **§11.28 (no forward-framing in KEEP docs)** — VALIDATED across Steps 1 + 2 + Final-review. Zero forward-framing tokens in new SrcGen READMEs / spec READMEs / modified consumer READMEs / new PATTERNS.md subsection. The single match `"nbf in the future"` in `auth-error-codes.spec.json:42` is JWT spec semantics, not codebase forward-framing.
- **§13.13 (Plan-vs-reality reconciliation discipline)** — VALIDATED EXTENSIVELY: Step 1 documented 3 reconciliations (spec-level optional `constName`/`tagsClassName`/`tagsNamespace` overrides; non-target-assembly test pattern divergence from auth-scopes; single-line vs multi-line xmldoc form on emitted constants). Step 2 documented 1 reconciliation (cross-link rot Plan Risk found materially inapplicable — captured via negative finding instead of forcing unnecessary redirector stubs). Final-review documented 1 reconciliation (PATTERNS.md addition came in 29 lines vs Plan's 30-50 forecast — denser per-instance bullet form preserved all content). All five transparently captured per the §13.13 format.
- **§14.1 (no phase / wave / sweep / audit verbiage with augmented regex)** — VALIDATED across Steps 1 + 2 + Final-review. Zero hits in any new content (SrcGen sources, spec files, consumer migrations, READMEs, PATTERNS.md addition). The augmented regex covering `\bStep[ -][0-9]+[A-Z]?\b` + hyphenated `Phase-N` forms held across the deliverable's modified-file scope.
- **§11.9 (no CLAUDE.md / V2.md / PHASE_*.md cross-doc citation in KEEP docs)** — VALIDATED in Steps 1 + 2 + Final-review. The Step 1 R1 finding (CLAUDE.md citation in test-file `FindRepoRoot()` helper) was a SIDE EFFECT of helper duplication, not a §11.9 evidence-grep failure — `TestPaths.RepoRoot()` adoption removed the citation entirely.
- **§11.21 (≤300-line per-lib README heuristic)** — VALIDATED in Step 2 — genuinely closed, not shifted. Top-level utilities/README 368→65; max sub-doc 177 at Extensions; all 7 READMEs ≤300 with substantial headroom. The deliverable 0004 deferred Utilities README split landed cleanly.
- **§24.13 (pre-flight Evidence greps mandatory)** — VALIDATED across Steps 1 + 2 + Final-review Implementer. Each round's Implementer captured exact command output for all canonical-applicable greps under `### Pre-flight Evidence greps`. Auditor independently re-ran each and confirmed zero non-spec-pinning hits.
- **§24.13.1 (canonical pre-flight grep checklist)** — VALIDATED IN STRUCTURE but EXPOSED A REGEX-COMPLETENESS GAP: the checklist's §7.15 American-English entry uses bare lemmas (`analyse|colour|behaviour|cancelled|...`), and conjugated British-spelling forms (`cancelling`, `prioritise`, `catalogue`, `materialise`, `recognised`) leaked past every Implementer + per-step Auditor pre-flight walk. Final-review Round 1 surfaced 6 sites; Fixer closed; Round 2 verified clean. This is exactly the §24.13.1 enumeration-completeness failure mode the predicate's "Why" preamble warns about — and motivates the Candidate 1 augmentation below.

## Proposed `rules.md` additions

### Candidate 1 — §24.13.1 + §7.15 augmentation (British-spelling conjugation enumeration) ✅ APPLIED (`rules.md` lines 716-722, 1822-1826)

> **User-authorized bypass**: per user instruction "we might as well add the rule additions now but i dont think we need a whole audit for a doc change", the augmentation was applied to `docs/dev/rules.md` directly without spawning a fresh Planner / Implementer / Auditor / Fixer round (CLAUDE.md MANDATORY block 1's "ONLY way to bypass any part of this process is an explicit user request" carve-out). The applied edit covers the original Candidate 1 scope PLUS a broader root set surfaced by the Fixer's enumeration-completeness sweep (added: `materialis*`, `catalogu*`, `serialis*`, `centralis*`, `specialis*`, `standardis*`, `finalis*`, `initialis*`, `harmonis*`, `pressuris*`, `categoris*`, `summaris*`, `practis*`, plus inline-bullet additions for `materialize`, `catalog`, `serialize`/`centralize`/`specialize`/`standardize`/`finalize`/`initialize`/`harmonize`/`pressurize`, and `defense`/`license`/`practice` -se-not-ce variants). Verified: new regex returns ZERO hits across the cleaned-up deliverable scope; 19 hits in `contracts/messages/en-GB.json` (legitimately British, allowlist-noted in §7.15).

**Where**: `docs/dev/rules.md` §24.13.1 canonical pre-flight grep checklist American-English entry (line 1822) + the §7.15 inline `**Audit grep**` line at line 718 (same regex, two homes — keep them in sync).

**Current** (line 1822):

```
- §7.15 (American English) — `grep -wEn 'analyse|colour|behaviour|cancelled|honour|synchronise|recognise|organisation|favourite|defence|programme|neighbour|labelled|labelling|modelled|modelling|travelled|travelling|signalled|signalling' <scope>` → expect zero (modulo allowlist)
```

**Proposed** — expand the regex to enumerate British-spelling conjugations (`-e/-ed/-es/-ing/-ation` forms) for the splits that have them, keeping the bare-lemma forms intact:

```
- §7.15 (American English) — `grep -wEn 'analys(e|ed|es|ing)|colour(s|ed|ing)?|behaviour(s|al)?|cancell(ed|ing)|honour(s|ed|ing|able)?|synchronis(e|ed|es|ing|ation)|recognis(e|ed|es|ing)|organis(e|ed|es|ing|ation)|favourite|defence|licence|practis(e|ed|es|ing)|programme|neighbour(s|hood)?|labell(ed|ing)|modell(ed|ing)|travell(ed|ing)|signall(ed|ing)|prioritis(e|ed|es|ing|ation)|optimis(e|ed|es|ing|ation)|customis(e|ed|es|ing|ation)|initialis(e|ed|es|ing|ation)|finalis(e|ed|es|ing|ation)|utilis(e|ed|es|ing|ation)|centralis(e|ed|es|ing|ation)|specialis(e|ed|es|ing|ation)|categoris(e|ed|es|ing|ation)|summaris(e|ed|es|ing|ation)|harmonis(e|ed|es|ing|ation)|standardis(e|ed|es|ing|ation)|materialis(e|ed|es|ing|ation)|catalogue(s|d)?' <scope>` → expect zero (modulo allowlist; `en-GB.json` translation file excluded).
```

(Apply identical replacement to the §7.15 inline `**Audit grep**` at line 718.)

**Why**: deliverable 0005 Final-review Round 1 surfaced 6 British-spelling sites that the bare-lemma regex would not catch — `cancelling` (HttpJwksProvider.cs:163), `prioritise` (D2.Shared.Tests.csproj:22), `catalogue` (server/shared/dotnet/README.md:52, auth-error-codes-source-gen/README.md:9), `materialise` (auth/README.md:78), `recognised` (ErrorCodesEmitter.cs:187). All 6 leaked through:

1. Step 1 Implementer's pre-flight §7.15 grep (followed canonical regex verbatim — predicate gap, not Implementer execution gap).
2. Step 1 R1 + R2 Auditor sweeps (followed canonical regex verbatim).
3. Step 2 Implementer's pre-flight §7.15 grep (followed canonical regex verbatim).
4. Step 2 R1 Auditor sweep (followed canonical regex verbatim).
5. Final-review Implementer's pre-flight §7.15 grep on the new PATTERNS.md content (luckily clean for that one file).

The leak terminated only at Final-review Round 1 because the Auditor manual-read the SrcGen output and the README content rather than relying solely on the canonical regex — exactly the mode of catch §24.13.1 was designed to make UNNECESSARY. Augmenting the canonical regex turns this from "Auditor must catch by manual read" into "Implementer's pre-flight grep catches at write-time" for every future deliverable. The augmentation is a pure ENUMERATION-completeness fix exactly matching the failure mode §24.13.1's "Why" preamble enumerates ("the Implementer's pre-flight set was constructed ad-hoc and missed §11.9's cross-doc-citation pattern entirely").

The fix is mechanical (regex substitution in two locations); zero risk of false-positive expansion (every added form is unambiguously British when followed by the lemma); zero risk of breaking allowlist mechanism (the `en-GB.json` translation file is already documented as excluded by Final-review Fixer).

**Origin**: deliverable 0005 Final-review Round 1 (`docs/wip/0005-codegen-cleanup-and-dotnet-improvements/final-review/journal.md:553-560` for the Auditor finding citation; `journal.md:572-597` for the Fixer enumeration-completeness sweep capturing all 6 sites + verification).

### No other candidates

The remaining 8 LIVE 0004 predicates exercised cleanly. Step 1's Auditor proposed a "test helper discoverability" candidate (§9.19 / §16 — pre-flight grep `tests/Unit/<area>/` for existing helpers before authoring new ones) but the Step 1 distillation honestly assessed it as borderline (single occurrence, fuzzy enforcement criteria, risk of over-fitting) and recommended **defer-not-add** until a 2nd-deliverable recurrence. The deliverable distillation upholds that recommendation — track as observed pattern; revisit if the same root-cause re-appears.

## Process integrity

This deliverable executes under the canonical orchestrator-only main-thread workflow per CLAUDE.md MANDATORY block 0:

- Every planning, implementation, audit, and fix round = NEW fresh sub-agent
- Per-step audit loop with 10-iteration ceiling; 3-artifact journal model (latest big table REPLACED each sweep + append-only findings log + append-only fix log)
- Final-review walks deliverable-wide
- All commits require explicit per-occurrence user permission
- Wip workspace gitignored; orchestrator updates this README's tracking sections only
- 5 LIVE Step-1C predicates (§1.19, §5.25, §11.28 with 4 augmentations, §13.13, §14.1 with 3 augmentations) + §24.13 / §24.13.1 from deliverable 0004 all binding

## Completeness Checklist (walked 2026-05-14 05:35 UTC)

Per CLAUDE.md MANDATORY block 3 + `docs/dev/rules.md` "Deliverable completeness checklist (the gate before user review)". Walked by a fresh `Completeness Checklist sub-agent` immediately before SHIP gate.

### Per-step gates

#### Step 1 — `01-spec-migrations` (2 rounds; convergence at R2)

| Box | Status | Citation |
|---|---|---|
| Journal exists | YES | `docs/wip/0005-codegen-cleanup-and-dotnet-improvements/01-spec-migrations/journal.md` |
| Big table present (Round 2 latest sweep) | YES | `01-spec-migrations/journal.md:804` (`## Latest sweep results`) |
| Anti-laziness preamble verbatim | YES | `01-spec-migrations/journal.md:806-810` |
| Big table zero ❌ FINDING rows | YES | `01-spec-migrations/journal.md:1148` (Round 2 totals: 0H / 0M / 0L; 3 🟡 PARTIAL observational) |
| Every PASS row carries file:line | YES | Spot-checked rows §1.1, §3.1, §5.1, §5.21, §7.1, §9.19, §11.9, §24.x — all carry concrete `file.cs:NN` citations |
| Every N/A row carries step-scope reason | YES | All `⚪ N/A` rows reference step scope (e.g. "No TS code in step scope", "No DB writes in step scope") |
| Findings log has per-round subsections | YES | `01-spec-migrations/journal.md:1161` (`## Sweep findings log` with R1 + R2 subsections) |
| Fix log chronological entries | YES | `01-spec-migrations/journal.md:1183` (`## Fix log` with Fixer R1 entries for §9.19 + §11.9 + hygiene) |
| Every prior-round FINDING has fix-log entry | YES | R1 §9.19 + §11.9 LOW + Auditor-flagged hygiene → Fixer R1 entries → R2 confirms ABSENT (`journal.md:1130, 1151-1152`) |
| Final round zero ❌ FINDINGs | YES | R2 sweep clean (`journal.md:1155`) |
| Self-audit §24.0-§24.13.1 present | YES | `01-spec-migrations/journal.md:1126-1146` (all §24.x rows PASS-cited against the journal itself) |
| Step's code change has test coverage | YES | 13 new test files: `tests/Unit/Auth/SourceGen/`, `tests/Unit/Telemetry/SourceGen/`, `tests/Unit/Auth/Inbound/Errors/AuthErrorCodesGeneratedTests.cs`, `tests/Unit/SpecsConsistency/AuthErrorCodesVsTelemetrySpecConsistencyTests.cs` (per `01-spec-migrations/journal.md:816, 824`) |
| Build clean (`dotnet build server/D2.slnx`) | YES | R2 Auditor: 0 Warning(s) 0 Error(s) (`journal.md:892`) |
| `jb inspectcode --severity=WARNING` clean | YES | R2 Auditor: zero findings (`journal.md:893`) |
| Test suite passes | YES | 3010/3010 passing post-Fixer (`journal.md:934`) |
| Per-step distillation appended | YES | `01-spec-migrations/journal.md:1192-1232` |

#### Step 2 — `02-deferred-cleanups` (1 round; first-pass convergence)

| Box | Status | Citation |
|---|---|---|
| Journal exists | YES | `docs/wip/0005-codegen-cleanup-and-dotnet-improvements/02-deferred-cleanups/journal.md` |
| Big table present (Round 1 latest sweep) | YES | `02-deferred-cleanups/journal.md:415` (`## Latest sweep results`) |
| Anti-laziness preamble verbatim | YES | `02-deferred-cleanups/journal.md:417-425` |
| Big table zero ❌ FINDING rows | YES | `02-deferred-cleanups/journal.md:770-774` (R1 totals: 0H / 0M / 0L; convergence on first sweep) |
| Every PASS row carries file:line | YES | Spot-checked rows reference `JwtValidatorTests.cs:411-414`, `utilities/README.md` + 6 sub-folder READMEs, journal sections; all PASS cells carry citations |
| Every N/A row carries step-scope reason | YES | All `⚪ N/A` rows reference DOCS-MOSTLY scope (e.g. "No DI extensions added in this step", "No source generator added or modified in this step") |
| Findings log has per-round subsections | YES | `02-deferred-cleanups/journal.md:778` (`## Sweep findings log` with R1 subsection — empty per clean sweep) |
| Fix log present (chronological) | YES | `02-deferred-cleanups/journal.md:786` (header present; no fixes needed per clean R1) |
| Every prior-round FINDING has fix-log entry | YES | R1 produced zero FINDINGs — no carryover possible (`journal.md:746`) |
| Final round zero ❌ FINDINGs | YES | R1 sweep clean (`journal.md:774`) |
| Self-audit §24.0-§24.13.1 present | YES | `02-deferred-cleanups/journal.md:742-761` (all §24.x rows PASS-cited / N/A-justified against the journal itself) |
| Step's code change has test coverage | YES | Comment-only test edit + docs-only README split — no behavioral change to test (`journal.md:432, 449`) |
| Build clean | YES | `journal.md:363-376` (Implementer): 0/0 build + 0/0 inspect |
| `jb inspectcode` clean | YES | Same |
| Test suite passes | YES | `journal.md:384-388` 3010 deterministic baseline preserved (Testcontainers RabbitMQ flake noted as pre-existing baseline, not Step 2 regression) |
| Per-step distillation appended | YES | `02-deferred-cleanups/journal.md:792-823` |

### Final-review gate

| Box | Status | Citation |
|---|---|---|
| Final-review journal exists | YES | `docs/wip/0005-codegen-cleanup-and-dotnet-improvements/final-review/journal.md` |
| Sweeps the ENTIRE deliverable | YES | `final-review/journal.md:231` ("Scope: WHOLE DELIVERABLE — Step 1 + Step 2 + Final-review Implementer round + Final-review Round 1 Fixer §7.15 fixes") |
| Three-artifact journal model | YES | Big table at `:221`; findings log at `:551` (R1 + R2); fix log at `:568` (R1 fixes) |
| Big table CLEAN (zero ❌ FINDINGs) | YES | `final-review/journal.md:524-526, 532` (R2 totals: 0H / 0M / 0L) |
| Cross-cutting findings recorded | YES | R1 §7.15 American English LOW (6 sites enumerated) → Fixer R1 → R2 confirms closure (`final-review/journal.md:553-560, 572-597`) |

### Cross-cutting doc gates

| Box | Status | Citation |
|---|---|---|
| Root README updated (kinds-of-misses + candidates + summary) | YES | This README — Kinds-of-misses log at `:43-81`; Proposed rules.md additions at `:83-119`; Process integrity at `:121-130` |
| PATTERNS.md updated (new spec → SrcGen subsection) | YES | `docs/PATTERNS.md:977-1005` ("Spec-driven codegen — the cross-cutting pattern") added in Final-review Implementer round |
| MESSAGING.md | N/A | No messaging surface touched in deliverable scope |
| TESTS.md | N/A | No new test category invented; existing test patterns reused |
| OPERATIONAL-GUARANTEES.md | N/A | No cross-service correctness changed in deliverable scope |
| RATE-LIMITING.md | N/A | No rate-limit middleware touched |
| SECURITY-RUNBOOKS.md | N/A | No KeyCustodian / secret handling touched |
| PARITY.md | N/A | Cross-platform parity for the AuthErrorCodes spec is tracked at the deliverable level (PHASE_0.md `:569-571, 630` + deliverable 0006 README) — no .NET-side parity surface added requiring PARITY.md gate |
| AUDIT_CHECKLIST.md | N/A | No new audit gate emerged from this deliverable (the §7.15 enumeration-completeness gap is captured as a `rules.md §7.15 + §24.13.1` candidate, not an AUDIT_CHECKLIST gate) |
| Per-lib READMEs updated | YES | `auth/README.md` (rephrased for codegen + new `ScopeInsufficient()` factory); `auth-outbound/README.md` (telemetry rephrasing); 2 new SourceGen READMEs (`auth-error-codes-source-gen/README.md`, `telemetry-tags-source-gen/README.md`); 7 utilities READMEs (split: top-level 65 lines + 6 sub-folder ≤177 lines) |
| Parent `server/shared/dotnet/README.md` updated | YES | `server/shared/dotnet/README.md:26-27` adds row for `auth-error-codes-source-gen/` + `telemetry-tags-source-gen/`; Mermaid graph at `:121-122` adds `AuthErrorCodesSG` + `TelemetryTagsSG` analyzer nodes with dashed `analyzer` edges to `Auth` + `AuthOutbound` |
| Tracking doc `docs/v2/PHASE_0.md` updated | YES | `docs/v2/PHASE_0.md:568, 573, 616, 625` reference deliverable 0005 in the Pre-Phase-1 plan; per-step status update is the responsibility of the SHIP-gate orchestrator step (after user authorizes commit batch) |
| No phase / sweep / audit verbiage in KEEP docs | YES | Final-review §14.1 PASS row (`final-review/journal.md` per category); per-step §14.1 + §11.28 PASS rows (`01-spec-migrations/journal.md:1097-1100`, `02-deferred-cleanups/journal.md` §11.28 / §14.1 PASS rows) |
| No conversation-scoped IDs in KEEP docs | YES | Final-review §11.28 PASS row + per-step §11.28 PASS rows; zero Q-IDs / R# refs / `Phase-N` tokens in shipped source / specs / READMEs / PATTERNS.md addition |

### Process-integrity gates

| Box | Status | Citation |
|---|---|---|
| No commit without explicit user permission | YES | `git log --oneline 0eeb8e81..HEAD` returns EMPTY (zero commits since branch base; SHIP gate pending user authorization) |
| No bulk file ops without scope declaration | YES | All bulk operations (utilities README split into 6 sub-folder READMEs; British-spelling enumeration sweep across 6 sites) declared scope upfront via Plan + Fixer enumeration-completeness sweep documented in fix log (`02-deferred-cleanups/journal.md:792-823`, `final-review/journal.md:589`) |
| No destructive git ops | YES | No `git reset --hard`, `git push --force`, `git stash`, `git branch -D`, or `git checkout --` invocations across deliverable execution |
| No silent deferrals | YES | Both PARTIAL carry-forwards explicitly tracked: §1.16 (intentional spec-pinning per `TestPaths.cs` pattern) + §9.30 (cross-platform parity tracked at PHASE_0.md `:569-571, 630` + deliverable 0006 README); §11.4 PATTERNS.md addition deferred Step 1 R2 → resolved in Final-review Implementer round (closed, not silently dropped) |
| No mid-execution architectural deviation | YES | All Plan-vs-reality reconciliations honestly captured per §13.13: Step 1 documented 3 (spec-level optional overrides, non-target-assembly test pattern, single-line vs multi-line xmldoc); Step 2 documented 1 (cross-link rot Plan Risk found materially inapplicable); Final-review documented 1 (PATTERNS.md addition came in 29 lines vs Plan's 30-50 forecast). All 5 captured per §13.13 format. |

### Walked checklist totals

- Per-step gates: 16 boxes × 2 steps = 32 / 32 YES
- Final-review gate: 5 / 5 YES
- Cross-cutting doc gates: 13 / 13 YES (5 honest N/A with reason, 8 YES with citation)
- Process-integrity gates: 5 / 5 YES
- **Grand total: 55 / 55 YES** (zero NO; deliverable is READY for user SHIP review)

## Attestation

> "I attest that this deliverable's process integrity has been verified against the deliverable completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is YES. The deliverable is ready for user REVIEW."

**Walked by**: Completeness Checklist sub-agent (fresh context per CLAUDE.md MANDATORY block 0)
**Walked at**: 2026-05-14 05:35 UTC
**Branch**: `n/codegen-cleanup` @ `0eeb8e81` + dirty working tree (zero commits since branch base)
**Citation summary**: 55 / 55 boxes YES — every per-step, final-review, cross-cutting doc, and process-integrity gate independently cited above against journal sections / source files / git plumbing output.

**Per-step / final-review journal references** (live in the gitignored `docs/wip/0005-codegen-cleanup-and-dotnet-improvements/` workspace; retained locally only — not in repo):

- `01-spec-migrations/journal.md` — Step 1 (R2 sweep clean, 3010/3010 tests; per-step distillation `:1192-1232`)
- `02-deferred-cleanups/journal.md` — Step 2 (R1 first-pass clean; per-step distillation `:792-823`)
- `final-review/journal.md` — Final-review (R2 sweep clean, §7.15 LOW closed across 6 sites; deliverable-wide convergence `:530-547`)
