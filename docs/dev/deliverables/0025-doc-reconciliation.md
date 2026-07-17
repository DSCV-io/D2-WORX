<!-- Committed snapshot of the gitignored deliverable-0025 workspace root (docs/wip/doc-cleanup/README.md), captured at SHIP 2026-06-27. -->

# Deliverable 0025 — Documentation reconciliation & tracking-hierarchy cleanup

**Status:** SHIPPED 2026-06-27. **Branch:** `n/keycustodian` (piggy-backed — doc-only, no new code). **Scope:** ~29 committed files (docs only).

## Goal

The project's documentation had drifted: stale plans presented as open work, the same status tracked in three places that disagreed, dead wip workspaces, and design specs doing double duty as progress trackers. This deliverable reconciled every doc to shipped reality, removed redundant and dead docs, and established a durable three-tier doc model so the drift cannot regrow — making the upcoming Edge build phase unambiguous to navigate.

## The doc model (the durable output of this deliverable)

This hierarchy is now codified in `docs/README.md` and enforced by `rules.md §11.43`.

**Persistent docs** — survive forever; each owns exactly one altitude, and points (does not restate) to the tier below:

| Tier | Doc | Owns |
| --- | --- | --- |
| 1 — whole project | `docs/v2/V2.md` | Phase map + one-line status per phase + vision |
| 2 — per phase | `docs/v2/PHASE_N.md` | That phase's deliverable DAG + per-deliverable scope/status/deps + build order |
| 3 — per deliverable | `docs/dev/deliverables/NNNN.md` (ship docs) + ADRs | What shipped, decisions, lessons |
| Reference | KEEP docs (`PATTERNS` / `rules` / `process` / `TESTS` / …) + per-lib/service READMEs | Current-truth conventions and APIs |

**Ephemeral docs** — holding-pens; folded into durable form on ship, then pruned:

- Research docs (e.g. `phase-3-edge-planning/`) → distilled into ship docs + ADRs on ship, then deleted.
- Design annexes for not-yet-built deliverables (`PHASE_3_AUTH`, `PHASE_3_RATE_LIMITING`, `PHASE_3_EDGE`) → kept until their deliverable builds, then folded into its ship doc + ADRs and pruned.
- Wip workspaces (`docs/wip/NNNN/`) → working journals; pruned after ship.

**The rule that prevents regrowth:** once work ships, its durable form is the ship doc + ADRs. The forward-looking holding-pen folds in and gets pruned. Standing up a new doc that duplicates a tier already owned requires an explicit decision.

## What changed (~29-file footprint, docs only)

### Docs deleted (2)

- **`docs/v2/POST_PIVOT_ROADMAP.md`** — dissolved; high-level direction → V2.md (already there); Phase-3 breakdown → `PHASE_3.md` deferred-work checklist + §G wire-up ledger; non-Phase-3 tail (versioning ADR) → ADR-0024 pointer. Then deleted.
- **`docs/v2/PHASE_0_MESSAGING.md`** — the messaging lib shipped; its durable form is the messaging READMEs. Capture confirmed, then deleted.

### Docs renamed (1)

- **`PHASE_0_AUTH.md` → `PHASE_3_AUTH.md`** — the doc describes Edge-auth work (Phase 3, not Phase 0). `git mv` + role banner added; dead `§14a` anchor (true `§15a`) fixed; stale lifecycle framing removed. Four live cross-references in the CLAUDE.md/docs tree updated to match.

### V2.md trimmed (2782 → 1157 lines, ~58%)

- §4 collapsed to a phase-map pointer.
- §5.x design tables collapsed: four patterns confirmed non-existent by code grep (`MultiTierFetcher`, `InputParsers`, `Validator-property pattern`, `WithXxx builder`) and dropped; four already captured in PATTERNS; one (`IsUnhandledException` exclusion) moved to PATTERNS as a real pattern. §5.3 Operation Risk Tier full detail homed in `PHASE_3_RATE_LIMITING §12`.
- §7 superseded by ADR-0024 → dropped, pointer added.
- §8 fully covered by `TESTS.md` → collapsed to pointer.
- Five stale `DcsvIo.D2.GeoReference` references → `DcsvIo.D2.Geo`.

### PHASE_3.md reconciled

Four staleness fixes + corrected Edge build order + trimmed open-prereqs. `POST_PIVOT_ROADMAP` §G seam→consumer table became the **§G Edge wire-up ledger** (the authoritative deferred-work checklist for the Edge build). `VALIDATION.md`'s four Phase deep-links re-homed here.

### Design annexes reconciled (3 files)

`PHASE_3_AUTH.md`, `PHASE_3_RATE_LIMITING.md`, `PHASE_3_EDGE.md` each received: a role banner ("design annex, not a tracker"), ref updates, and consistency fixes. `PHASE_3_RATE_LIMITING §12` received the §5.3 Operation Risk Tier design (the only content that had no durable home).

### KEEP docs + READMEs (~16 files)

- ~16 forward-looking `docs/v2/PHASE_*` / `V2.md` deep-links replaced with inline prose across KEEP docs and per-service/lib READMEs. Present-tense docs now describe current reality without pointing into phase-tracking holding-pens.
- `edge/README.md`: `DcsvIo.D2.GeoReference` → `DcsvIo.D2.Geo`; `leaf_issuance_audit_record` row added to the schema table.
- `courier/README.md` + `files/README.md`: same `GeoReference` → `Geo` fix.
- Deliverables index (`docs/dev/deliverables/README.md`): +4 missing rows (0016 / 0018 / 0019 / 0020) + a note documenting the 0010 gap (that index slot was used by a now-deleted legacy doc).

### Doc model + rules.md (2 files + rules additions)

- `docs/README.md`: new "Doc model" section codifying the three-tier hierarchy and prune-on-ship rule.
- `rules.md`: three new predicates — §11.43 (doc-model lifecycle), §11.44 (plain-language), §17.8 (`IsUnhandledException`-not-retryable).
- `CLAUDE.md`: `docs/README.md` row added to the Doc Update Map table (§3.5) in lockstep.

### Ephemeral workspace purge

~1,430 dead wip files (33 dead wip workspaces + `daily-reports/`) + 4 stale auto-loading plan files deleted in Step 0. Full `docs/` tree backed up externally before deletion.

### SRC_GEN.md + process.md (plain-language edits)

The FINAL-REVIEW Fixer also rewrote "load-bearing" in `SRC_GEN.md` (×5) and `process.md` (×3) as a side effect of the §11.44 plain-language check. User kept these edits; they are counted in the footprint.

## Decisions

| # | Decision |
| --- | --- |
| D1 | The 3-tier persistent model + ephemeral holding-pen / prune-on-ship rule. Codified in `docs/README.md` + `rules.md §11.43`. |
| D2 | `POST_PIVOT_ROADMAP.md` dissolves into V2 (Tier 1) + PHASE_3 (Tier 2), then deletes. |
| D3 | `PHASE_0_MESSAGING.md` deletes (shipped lib; durable form = messaging READMEs). |
| D4 | Design annexes (`PHASE_3_AUTH` / `PHASE_3_RATE_LIMITING` / `PHASE_3_EDGE`) reconciled + role-clarified + kept until their deliverables build; `PHASE_0_AUTH` renamed to reflect Edge-auth (Phase 3) work. |
| D5 | `phase-3-edge-planning/` research workspace → verify-then-prune (capture confirmed against 0018/0019 ship docs + ADRs, then deleted). |
| D6 | Wip purge (~1,430 files) authorized + executed in Step 0; full `docs/` backup retained externally at `C:\DCSV\D2-WORX-docs-backup-2026-06-26`. |

## Audit-loop outcome + kinds-of-misses

**Step 2 completeness gap caught by a second audit lens.** The first Step-2 audit (accuracy) came back clean. A second pass (completeness) caught that §5.x had been partially trimmed — V2 §5.6 still held a large contacts design table that belonged in the collapse. The lesson: a single audit lens (accuracy) is not the same as a full rules.md walk; the completeness lens is a distinct pass.

**Four phantom patterns stopped from polluting PATTERNS.md.** Before moving V2 §6 pattern descriptions into PATTERNS, a code grep currency-check confirmed four patterns (`MultiTierFetcher`, `InputParsers`, `Validator-property pattern`, `WithXxx builder`) had no implementation in the codebase. They were dropped rather than transcribed. Documenting a pattern that does not exist is worse than a gap.

**Pre-existing dead link surfaced by the rename.** The `PHASE_0_AUTH` → `PHASE_3_AUTH` rename exposed a pre-existing dead link (`PHASE_0.md`) in `PHASE_3_AUTH §16`. The rename made the sweep own all references; the dead link was fixed.

**Pre-existing broken anchor fixed by ref sweep.** `§14a` appeared in four docs pointing to a section that had been renumbered `§15a`. The rename's ref sweep caught it; all four updated.

**Move-don't-lose guardrail.** The Step-2 decision to collapse §5.3 Operation Risk Tier detail without a confirmed home was held until Step 3 supplied one (`PHASE_3_RATE_LIMITING §12`). Content was never committed to deletion until capture was confirmed.

**FINAL-REVIEW scope extension kept.** The Fixer's out-of-scope edits to `SRC_GEN.md` and `process.md` (the §11.44 plain-language check catching two files the step plan had not listed) were surface for user decision, not silently committed. User kept them.

## rules.md predicates added at SHIP

| §-home | Predicate gist | Evidence |
| ------ | -------------- | -------- |
| **§11.43** | Doc-model lifecycle rule: three-tier persistent hierarchy (V2 → PHASE_N → ship docs/ADRs) + reference KEEP docs; ephemeral holding-pens fold into the durable form on ship and are then pruned. Standing up a new doc duplicating an owned tier requires explicit decision. | The core output of this deliverable; prevents regrowth of the drift this deliverable cleaned. |
| **§11.44** | Plain-language rule: docs and ADRs use plain direct language. A phrase that says less than its literal rewrite (e.g. "load-bearing", "by physics") is noise — cut it. | Surfaced during FINAL-REVIEW when §11.44 (freshly added in Step 5) immediately caught two instances in PATTERNS.md and several in SRC_GEN.md / process.md. |
| **§17.8** | `IsUnhandledException` must never appear in `IsTransientRetryable` (or any "should I retry?" predicate). Unknown system state must not be retried — retrying an unhandled exception escalates an unknown failure into a repeated one. | Code-grep confirmed `IsTransientRetryable => IsServiceUnavailable \|\| IsRateLimited` (correct); the predicate pins that invariant and prevents future regression. |

## Deliverable completeness checklist

Audit cadence: the user-authorized targeted-per-step audit + full final-review model (the §13.14-named deviation also used in 0015 / 0017 / 0022 / 0023 / 0024).

### Per-step gates

This is a documentation-only deliverable. The per-step gate items that require code (build/inspect/test) are N/A for each step; the process items are evaluated below.

- **Journal exists** — `docs/wip/doc-cleanup/README.md` is the unified journal for this deliverable (flat workspace; step notes are sections in the root README rather than sub-folder journals because Steps 0–5 are doc-only operations that produced no code artifacts). ✅
- **Big table / anti-laziness preamble** — ⚪ N/A: targeted audit cadence authorized per §13.14 (same carve-out as 0024). The Step-level audits used targeted two-reviewer passes scoped to each step's risk surface (accuracy + completeness separately) rather than the full K=12 big-table form. Final-review ran three dedicated reviewers (link-integrity / coherence / hygiene+completeness) with findings → Fixer → closure re-sweep. Evidence lives in the Living State section of `docs/wip/doc-cleanup/README.md`.
- **Zero-finding clean sweep** — ✅ FINAL-REVIEW closure re-sweep confirmed all 4 findings closed (PHASE_3_AUTH dead link; VALIDATION.md deep-links; docs/README annex row; PATTERNS "load-bearing" ×2). Zero findings in the terminating re-sweep.
- **Findings + fix logs** — ✅ Step notes in `README.md` Living State record every finding and its resolution; FINAL-REVIEW findings (4 total: 1 HIGH + 2 MEDIUM + 1 LOW) each have explicit fix entries in the Living State.
- **Code tests** — ⚪ N/A: documentation-only deliverable. No code was added or modified. No test coverage gap.
- **Build clean / JetBrains inspect** — ⚪ N/A: no C# or TS code changes. Branch inherits the clean build state from `n/keycustodian`.
- **Test suite passes** — ⚪ N/A: no code changes; test suite is unmodified.

### Final-review gate

- **Final-review ran** — ✅ Three dedicated reviewers dispatched: (1) link-integrity sweep across all ~29 modified files; (2) coherence pass (V2 reads as clean Tier-1; PHASE_3 carries the deferred-work checklist; design annexes are role-clarified); (3) hygiene + completeness (no leaked process IDs; no forward-looking language in KEEP docs; all plan items confirmed landed).
- **Full deliverable scope** — ✅ All steps' outputs covered: deleted files confirmed gone; renamed file confirmed at new path with updated cross-references; V2 1157-line state verified clean Tier-1; PHASE_3 §G wire-up ledger complete; design annexes role-clarified; KEEP docs / READMEs grep-clean for phase deep-links; deliverables index +4 rows; rules.md §11.43 / §11.44 / §17.8 present; docs/README.md doc-model section present; CLAUDE.md §3.5 row added.
- **3-artifact model** — ⚪ N/A (targeted cadence authorized); findings and fixes are recorded in the README Living State rather than separate big-table / findings-log / fix-log files. Evidence is auditable from the Living State section.
- **Deliverable-wide consistency** — ✅ Confirmed: no redundant source-of-truth for phase status; V2 links to phases, not vice-versa; KEEP docs describe current reality without pointing into holding-pens.

### Deliverable-wide doc gates

- **Root README updated** — ✅ `docs/wip/doc-cleanup/README.md` is the root; Living State section updated through FINAL-REVIEW convergence. This ship doc is the committed snapshot.
- **Cross-cutting docs updated** — ✅ `docs/README.md` (doc model); `rules.md` (§11.43 / §11.44 / §17.8 + ToC); `CLAUDE.md` (§3.5 row); `PATTERNS.md` (IsUnhandledException exclusion moved here); `TESTS.md` (§8 pointer); `SRC_GEN.md` / `process.md` (plain-language cleanup). All per CLAUDE.md §3.5 Doc Update Map.
- **Per-lib / per-service READMEs** — ✅ `edge/README.md`, `courier/README.md`, `files/README.md` updated (GeoReference → Geo; leaf_issuance_audit_record row).
- **Parent shared dotnet README** — ⚪ N/A: no new lib added.
- **Tracking doc updated** — ✅ `PHASE_3.md` carries this deliverable's deferred-work checklist rows; `V2.md` phase-map row for this deliverable is current.
- **No phase / sweep / audit verbiage** — ✅ Grep-verified: no `Phase 0/3/4/5`, `PHASE_*.md`, `V2.md`, or audit-round language leaked into KEEP docs or source code.
- **No conversation-scoped IDs** — ✅ No `Q-`, `F-`, `R#`, `Step-N`, `Round-N`, `OJC`, or similar IDs appear in committed docs or source.

### Process-integrity gates

- **No uncommitted commit** — ✅ No commit was made during this deliverable without explicit user permission per occurrence.
- **No undeclared bulk ops** — ✅ Step 0's ~1,430-file purge was scope-declared (33 dead wip workspaces + `daily-reports/` + 4 plan files) and user-authorized before execution.
- **No destructive git** — ✅ `PHASE_0_MESSAGING.md` and `POST_PIVOT_ROADMAP.md` deletions were user-authorized via D2 and D3. No force push, hard reset, or branch delete.
- **No deferred work without permission** — ✅ One item held for user decision mid-Step 4 Part 2 (§6 patterns coverage) was explicitly asked about and resolved with the user before proceeding.
- **No mid-execution architectural deviation** — ✅ All architectural calls (prune-on-ship rule, naming convention for design annexes, §6 patterns disposition) were locked with the user before execution.

### Final attestation

> "I attest that this deliverable's process integrity has been verified against the deliverable completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is YES. The deliverable is ready for user REVIEW."
>
> Per-step and final-review evidence: `docs/wip/doc-cleanup/README.md` (Living State section, Steps 0–6).
