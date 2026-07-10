<!--
Copyright (c) DCSV. All rights reserved.
-->

<a name="top"></a>

# D2-WORX Process — phase lifecycle + orchestrator-only main thread + audit-loop mechanics

The single source of truth for HOW work moves through D²-WORX — phase lifecycle (PLAN → EXECUTE → FINAL-REVIEW → SHIP → REVIEW), permission gates, sub-agent architecture (orchestrator + worker roles), audit-loop mechanics (K=7 concern-bundle partition A–G + Aggregator for mid-step and FINAL-REVIEW; K=12 atomic dispatch retired), and the self-improvement loop (distillation → rules.md additions).

Predicate-level enforcement lives in [rules.md](rules.md); pattern reference in [../PATTERNS.md](../PATTERNS.md); AGENTS.md condenses this doc + rules.md.

> **Read [rules.md](rules.md) end-to-end at the start of every deliverable's PLAN phase.** It is the central requirements catalog (security, races, naming, disposal, D2Result, OOTB libs, logging, PII, degradation, UX, DX, observability, idempotency, config, and more). Knowing the rules upfront is what lets code pass audit round 1 instead of round 5. Designed for AGENT ergonomics first, human readability second.

## Table of contents

1. [Phase lifecycle](#1-phase-lifecycle) — [Glossary](#glossary) · [Folder shape](#folder-shape) · [PLAN](#plan) · [EXECUTE](#execute) · [FINAL-REVIEW](#final-review) · [SHIP](#ship-handoff-to-user-review) · [REVIEW](#review-user-phase) · [Append-only discipline](#append-only-discipline) · [Scope of work shape](#scope-of-work-shape) · [What this does NOT do](#what-this-process-does-not-do) · [When to invoke](#when-to-invoke-this-process)
2. [Permission gates (when to pause for the user)](#2-permission-gates-when-to-pause-for-the-user)
3. [Sub-agent architecture](#3-sub-agent-architecture) — [Why structural](#why-this-is-structural-not-stylistic) · [Allowed](#allowed-in-main-thread-context) · [Forbidden](#forbidden-in-main-thread-context) · [Canonical roles](#canonical-sub-agent-roles) · [Model policy per role](#sub-agent-model-policy-per-role) · [Every round = fresh](#every-round--a-new-fresh-sub-agent) · [Orchestrator cannot mark CLEAN](#the-orchestrator-cannot-mark-clean) · [Cluster partition (K=7 A–G)](#auditor-cluster-partition-dual-mode) · [Aggregator role](#aggregator-role-post-cluster-consolidation)
4. [Audit-loop mechanics](#4-audit-loop-mechanics) — [Three-artifact model](#three-artifact-journal-model) · [Round sequence](#mandatory-round-sequence) · [Plan currency](#plan-currency-before-dispatch) · [Dispatch-brief template](#dispatch-brief-template) · [Per-round dispatch](#per-round-dispatch-protocol) · [Audit wave policy](#audit-wave-policy) · [Orchestrator verification](#orchestrator-verification-of-sub-agent-outputs) · [Sister-sweep checklist](#cross-cluster-sister-sweep-checklist-aggregator-baseline) · [K=1 carve-out policy](#k1-carve-out-usage-policy) · [Partial-file template](#partial-file-template-per-auditor) · [Why sweep-only-replaceable](#why-the-table-is-sweep-only-replaceable) · [Why append-only](#why-findings--fixes-are-append-only) · [Evidence requirements](#evidence-requirements-mechanical-no-exceptions) · [Loop count](#loop-count-expectations)
5. [Self-improvement loop](#5-self-improvement-loop)
6. [Appendices](#6-appendices) — [A: Failure-mode mapping](#appendix-a-how-this-addresses-each-empirical-failure-mode) · [C: 0002-auth-inbound trial](#appendix-c-trial-outcomes-from-deliverable-0002-auth-inbound)

> ## ⚠️ MISSION CONTEXT
>
> **D²-WORX is built as an enterprise-level, production-ready, robust SaaS framework.** This process enforces that standard at the process level: PLAN locks design rigor, EXECUTE locks autonomous convergence on quality, REVIEW preserves architectural feedback — shipping production-ready code without the user having to push the agent through bug-hunting cycles.

> Companion docs: [rules.md](rules.md) (authoritative requirements catalog — read end-to-end during PLAN, walk during the EXECUTE audit loop + final-review); [deliverables/](deliverables/README.md) (surviving root READMEs for shipped deliverables — lessons + final report, committed).

---

## 1. Phase lifecycle

Three phases: **PLAN → EXECUTE → REVIEW**, with a deliverable-wide **FINAL-REVIEW** sub-step before **SHIP** hands off to the user's REVIEW. Convergence is autonomous — the **main-thread orchestrator** spawns fresh sub-agents for every round of planning, implementation, auditing, and fixing, and loops until each step's audit terminates clean, then ships to user review.

### Glossary

- **Deliverable** — a coherent unit of shipped work (one feature, one library set, one cross-cutting refactor). Has a name, a branch, a `docs/wip/<deliverable>/` folder, and a final committed report at `docs/dev/deliverables/<deliverable>.md`.
- **Step** — one coherent shippable/testable unit within a deliverable (see [PLAN — Break into steps](#plan) fat-step law). Ordered; may declare prerequisites on earlier steps. **Fat step** = PLAN step-list granularity only — does not waive journals, multi-seat Y/K, tests, or §24 for small code changes inside the step.
- **FR_FULL** — default FINAL-REVIEW open for product deliverables: full **K=7** at whole-deliverable scope + own `final-review/journal.md`.
- **FR_LITE** — FINAL-REVIEW when all eligibility gates pass: deliverable-scope **Y ⊆ K=7** + Aggregator + own FR journal (multi-seat Y ≠ K=1). See [Audit wave policy](#audit-wave-policy).
- **FR_COLLAPSED** — narrow exception: pure-meta **1-step** deliverable with README lock; no separate FR journal; step CLEAN Y-audit is the deliverable gate. Multi-seat Y still required. See [Audit wave policy](#audit-wave-policy).
- **Evidence ledger** — per-partial (and optionally journal) table of commands/reads run once (`E#`); big-table rows cite `E#` instead of re-pasting full stdout. Ledger alone ≠ PASS.
- **Audit round** — one pass through every category in `rules.md`, producing per-predicate evidence. Findings are fixed inside the same round; the next round runs against the post-fix state.
- **Clean round** — an audit round producing zero findings across every category. The termination signal.
- **Iteration ceiling** — 10 audit rounds per step (and 10 at final review). Hitting 11 = escalate to the user; the mental model is wrong, not the execution.
- **Self-improvement** — at each step's audit termination AND at ship, the agent distills the kinds of misses into proposed `rules.md` additions. User approves; rules are appended; future deliverables start stricter.
- **Orchestrator** — the main-thread agent. Decision-making + delegation only. Cannot edit / write / read source code; cannot walk `rules.md`; cannot mark anything CLEAN. Spawns sub-agents for everything domain-level.
- **Sub-agent** — a fresh-context worker spawned for one role (Planner / Implementer / Auditor / Aggregator / Fixer / Final-reviewer): IF Claude Code → `Agent` + `claude-d2-<role>`; IF Grok Build → `spawn_subagent` + `grok-d2-<role>`; IF Codex → `spawn_agent` + `codex-d2-<role>`. Returns a structured summary; its context dies on return.
- **Atom (A1…E3)** — one of twelve stable thematic groupings of `rules.md` predicates by §-ownership. **Provenance / Y-maps / historical journals only** — not a dispatch mode. Full atom table: [§3 historical atom IDs](#atomic-k12-partition-final-review-default).
- **Bundle (A…G)** — one of seven concern-first **dispatch seats** that union one or more atoms. **The only full-partition size (K=7)** for mid-step Plan-Audit, mid-step code-audit, and **FR_FULL** FINAL-REVIEW (FR_LITE uses Y ⊆ K=7; FR_COLLAPSED uses step Y): [§3 K=7 partition](#auditor-cluster-partition-dual-mode).
- **Cluster** — generic term for a dispatch seat (a bundle A–G). Partition: [§3 K=7](#auditor-cluster-partition-dual-mode).
- **Audit round (K≤7)** — one full audit pass = K parallel cluster Auditors + 1 Aggregator + (if findings) 1 Fixer. **Default full partition is K=7** concern bundles (A–G) for product Plan-Audit R1 (in-scope), justified full code-audit, and **FR_FULL** FINAL-REVIEW. Per-step first code-audit may use **Y ⊆ K=7** (step-relevant) with journal justification. Plan-Audit has a **three-way** open: Skip (narrow carve-outs) \| pure-meta/docs **Y ⊆ K=7** \| product full **K=7** — see [Audit wave policy](#audit-wave-policy) + [§24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). FINAL-REVIEW open mode is **FR_FULL** (default product), **FR_LITE** (eligibility gates), or **FR_COLLAPSED** (pure-meta 1-step only). After findings (Plan or code), re-dispatch **dirty bundles only** (plus sister-blast) — not a full re-fan-out by default. Sequential K=1 requires explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). Dirty-only is a **subset of the active partition**, not a K=1 carve-out. **K=12 atomic dispatch is retired.**

### Folder shape

```
docs/
  dev/
    process.md          ← this file (committed)
    rules.md            ← the rule catalog (committed)
    deliverables/       ← surviving root READMEs (committed snapshots)
      README.md
      0001-auth-outbound.md
      0002-handler-stack.md
  wip/                  ← gitignored; per-deliverable local workspace
    NNNN-<deliverable>/  ← 4-digit deliverable index, e.g. `0001-shared-libs-review/`
      README.md         ← progress tracker + final report (snapshot copied to deliverables/ at ship)
      01-<step-name>/
        journal.md      ← append-only; LOCAL-ONLY, never committed; never auto-deleted
      02-<step-name>/
        journal.md
      ...
      final-review/
        journal.md
```

Deliverables use a 4-digit index prefix (`0001-`, `0002-`, …) so they sort by ship order; the wip folder and the committed snapshot share the same index. Pick the next free index at PLAN by `ls docs/dev/deliverables/` + incrementing the highest. At SHIP, **only the root README** is copied out of `wip/NNNN-<name>/` to `docs/dev/deliverables/NNNN-<name>.md` (committed single file). Per-step journals stay in `docs/wip/NNNN-<name>/` — gitignored, local-only, NEVER auto-deleted (the user removes them manually); they remain local audit-trail evidence but never cross the commit boundary.

### PLAN

The user and agent reach alignment on what's being built. Output: a fully-populated `docs/wip/<deliverable>/README.md` plus empty step folders.

0. **READ [rules.md](rules.md) END-TO-END.** Mandatory before any other PLAN activity — knowing the catalog upfront is what makes code pass audit round 1 instead of round 5.
1. **Discuss + lock high-level goal.** Loop until the user agrees on success. Capture as the first journal entry.
2. **Create the deliverable workspace.** Populate the root README (template below); each step gets a numbered folder (`01-<short-name>/`, …) with an empty `journal.md`.
3. **Break into steps (fat-step default).** **Prefer fewer fatter steps.** A step is a coherent shippable/testable unit the Implementer can complete and the audit can gate once. Prefer one fat step over many micro-steps when work shares one risk class and one mergeable surface.

   **Split ONLY when mechanically beneficial**, e.g.:
   - Distinct csproj / package / deployable boundary that should gate independently.
   - Tests or migrations/codegen must pass between steps (true build dependency).
   - True parallel independent tracks (separate worktrees / no shared contested files).
   - Different risk class requiring separate audit scope (e.g. auth vs pure docs).
   - File-count / context blow-up that makes one Implementer brief unsafe (cite §24.0i Sweeping carve-out order of magnitude: multi-concern cascading / >~40 files when not a single coherent unit).

   **Anti-pattern:** micro-steps for human-pretty reviewability that multiply Plan-Audit + Implement + audit waves without a mechanical gate between them.

   Pure-meta process deliverables may be **1-step** (exemplar: `0029-audit-token-discipline`).

   **Boundary:** fat-step governs **PLAN step-list granularity only**. It does **not** waive journals, multi-seat Y/K, tests, audit loops, or §24 for small **code** changes. AGENTS MANDATORY 1 ("no small-change carve-out") still binds every code change inside a fat step. Thin rules pointer: `rules.md` glossary **"Fat step"** → this section (no §13.16).

   Loop until step list + ordering + prerequisites are agreed.
4. **Lock detailed design per step.** Discuss trade-offs, layer choices (which ctor, interface, transport). Document rejected alternatives — the most valuable thing the journal carries forward for diagnosing design-time mistakes later.
5. **Risk pass — walk every rules.md category against the design.** For each: "what predicates apply? does the design satisfy them upfront?" Refine, loop until agreed.
6. **PLAN exit.** Root README has populated step list + cross-cutting decisions + open-questions-empty; step folders exist with empty journals; agent confirmed end-to-end rules.md read in the journal. Enter EXECUTE.

**`docs/wip/<deliverable>/README.md` template (populated during PLAN):**

```
# <Deliverable Name>

Branch: <branch>
Started: YYYY-MM-DD
Status: PLAN | EXECUTE step N | FINAL-REVIEW | SHIPPED

## Goal
<2-3 sentences — what success looks like, why this is being built>

## Steps
- ⏸  01-<step-name>    (prereqs: none)
- ⏸  02-<step-name>    (prereqs: 01)
- ...
- ⏸  final-review

## Cross-cutting decisions (during PLAN)
- <decision>: <choice> — alternatives rejected: <list, why>

## Open / escalated to user
- (none) | <question, blocked since YYYY-MM-DD>

## Kinds-of-misses log (populated during EXECUTE per-step + final-review)
<empty initially; grows append-only>

## Proposed rule additions to rules.md (populated at ship)
<empty initially; finalized at final-review termination>
```

### EXECUTE

For each step in prerequisite order, the **main-thread orchestrator** drives the per-step loop by spawning fresh sub-agents (per [§3 Sub-agent architecture](#3-sub-agent-architecture)). The orchestrator never edits source, never walks `rules.md`, never marks anything CLEAN.

**1. Spawn Planner sub-agent (step plan entry).** Given the step description, prerequisites, applicable rules.md categories, relevant docs. The Planner appends to `docs/wip/<deliverable>/<NN>-<step>/journal.md`:

```
=================================================
[YYYY-MM-DD HH:MM] Plan
=================================================
Goal: <what should be true after this step>
Files to create / modify: <list>
Approach: <2-3 sentences>
Decisions made: <list, with rejected alternatives>
Pre-emptive gate checks (try to nail first-pass):
  - Test coverage plan: <list public methods → planned tests>
  - Convention check: <Falsey/Truthy used? D2Result factories? extension members syntax?>
  - PII check: <any LoggerMessage with Exception? any try/catch logging ex.Message?>
  - Layer check: <transport vs handler decisions; alternatives considered>
```

The pre-emptive gate checks push category-A/E/F catches to BEFORE code is written — this is where loop count drops from 5 rounds to 1-2.

**1a. Plan-Audit (three-way law per [rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) + [Audit wave policy](#audit-wave-policy)).** **Skip ≠ Y ≠ full K=7** — three distinct modes; the orchestrator records the chosen mode + Y list (if any) in journal / shared-context **before** dispatch.

| Mode | When | Dispatch |
| --- | --- | --- |
| **Skip** | Only a **narrow listed carve-out** applies; orchestrator log **cites which** | No Plan-Audit seats |
| **Y ⊆ K=7 + Aggregator** | **Docs / pure-meta** steps that still need Plan-Audit (multi-surface process/rules/skills/KEEP law) — default seats **E+G**; +B if convention/pin text; +D if permission-doc | Multi-seat Y + Aggregator; partials = \|Y\|; Aggregator still merges **full-catalog** big table (non-Y seats N/A-coded) |
| **Full K=7 + Aggregator** | **Product** §24.16 in-scope (new types / new patterns / >50-file scope) and any step that does not qualify for Skip or pure-meta Y | K=7 bundles A–G + Aggregator |

**Skip carve-outs only** (Plan-Audit NOT required): trivial single-file edits (<5 net-new files, no new types/patterns/public surface); Step 0 branch-checkout / scaffolding-only; sub-dispatches within a step that already had upfront Plan-Audit; optional **trivial single-KEEP-doc polish** only (one KEEP file, no process/rules multi-surface law change). **Not a skip:** multi-surface process/rules/skills pure-meta — those are **Y-eligible default**, not skip.

Scoped to the Plan section (reality alignment + naming + rules.md compliance + cross-language parity + existing-pattern consistency + stale assumptions + §26 spec-mirror anti-pattern as applicable). Aggregator merges → `## Plan-Audit results`. On findings: **Plan-amender** → **dirty-only** re-audit (+ sister-blast) — do **not** default full K=7 every re-round. Terminate on CLEAN; Implementer receives the AMENDED Plan. *Empirical: `n/geo-libs` Step 2 Plan-Audit returned 35 findings (13 HIGH + 13 MEDIUM + 9 LOW) incl. stale assumptions + one security flaw — caught before the Implementer built on them.*

**2. Spawn Implementer sub-agent.** Given the journal Plan + applicable rules.md categories + files-to-touch. Writes code + tests, then appends:

```
=================================================
[YYYY-MM-DD HH:MM] Implementation
=================================================
Files: <list with brief purpose>
Approach notes: <anything not in the plan>
Tests written:
  Per-public-method coverage: N/N
  <method> -> <test file:line>
Adversarial coverage: <count, summary>
Build state: clean | <warnings to address>
Baseline currency: PASS | <packages needing re-seed>
```

If any consumable shared package's source was modified, the Implementer runs `pnpm --filter release-runner check-baselines` before declaring complete; on stale baselines it re-seeds, re-stages the baseline files, and records `Baseline currency: PASS` only after the gate exits 0 (a stale baseline left for "later" is FINDING-HIGH at audit, §26.20). The orchestrator consumes the summary — it does NOT read the source files itself.

**3. Audit loop (the core forcing function).** Per mid-step round the orchestrator dispatches a **fresh Auditor batch** in parallel (READ-ONLY — cannot edit source), then a **fresh Aggregator** once all partials return. First code-audit round may target **Y ⊆ K=7** (step-relevant bundles only) with justification in the journal; subsequent rounds after findings re-dispatch **dirty bundles only** (plus sister-blast). Each seat walks its slice per the [§3 K=7 partition](#auditor-cluster-partition-dual-mode), produces per-predicate evidence (grep results, file:line, "checked X by Y, found Z" — vibes are not evidence), and writes its own partial (`r{N}-partial-{SEAT}-{seat-name}.md` — one partial per **bundle** A–G). The Aggregator merges the K partials into the canonical big table (REPLACES `## Latest sweep results`) + appends one `### Round N findings` subsection (per [§3 Aggregator role](#aggregator-role-post-cluster-consolidation)). Workflow: [§4 Per-round dispatch protocol](#per-round-dispatch-protocol).

On FINDING rows, spawn a **fresh Fixer** with the consolidated list. The Fixer applies fixes + appends fix-log entries — it cannot mark anything CLEAN; closure is proven only by the NEXT round's fresh dirty-or-full batch + Aggregator not surfacing the finding. **A second audit round is a BRAND-NEW Auditor batch + brand-new Aggregator, never the same ones re-running** — the fresh-context property is non-negotiable. **K=1 carve-out** requires explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit); NEVER self-invoked. Dirty-only re-dispatch is **not** K=1. Detailed mechanics: [§4](#4-audit-loop-mechanics).

**4. Per-step distillation.** Once the step terminates clean (a fresh Auditor's big table came back with zero FINDING rows), the orchestrator spawns a sub-agent to append the distillation block (template in [§5](#5-self-improvement-loop)) — kinds of misses by category + candidate rules.md predicates. These surface in the root README's Kinds-of-misses log; final merge into `rules.md` happens at ship.

**5. Update root README.** After distillation the **orchestrator** updates `docs/wip/<deliverable>/README.md` (one of the few `Edit` activities it may do itself, since the root README is its tracking artifact): step status ⏸ → 🔄 → ✅ (with round count), append to Kinds-of-misses log, append any new cross-cutting decisions.

**6. Move to next step.** Step N starts when all prerequisites are ✅. The orchestrator does NOT spawn a new Planner for step N while the previous step has open audit findings.

### FINAL-REVIEW

Same orchestrator-driven loop as EXECUTE, scope = the whole deliverable (unless **FR_COLLAPSED** — below). Catches integration / consistency bugs no single-step audit finds: cross-step type drift, telemetry-tag drift between two libs, README parity, end-to-end integration paths. **Mode is selected before open** and recorded in journal / shared-context — see [Audit wave policy](#audit-wave-policy). Three named modes:

| Mode | When | Dispatch / journal |
| --- | --- | --- |
| **FR_FULL** (default product open) | Whenever FR_LITE / FR_COLLAPSED ineligible | Full **K=7** at **whole-deliverable** scope; own `docs/wip/<deliverable>/final-review/journal.md` |
| **FR_LITE** | **All** [FR_LITE eligibility gates](#audit-wave-policy) pass | Deliverable-scope **Y ⊆ K=7** + Aggregator; own FR journal; multi-seat Y ≠ K=1 |
| **FR_COLLAPSED** | Pure-meta **1-step** deliverable where step path-set ≡ deliverable; locked in deliverable README | **No** separate FR journal; step CLEAN multi-seat Y-audit is the deliverable gate; completeness FR boxes cite **step** journal + collapse mode |

**FR_FULL / FR_LITE procedure** (own FR journal):

1. Fresh **Planner** defines the deliverable-wide cross-cutting focus areas (the Aggregator verifies these in [§3 Aggregator role](#aggregator-role-post-cluster-consolidation) step 3).
2. Fresh **Implementer** for cross-cutting fixes (only if planning surfaces work).
3. Fresh **Final-reviewer batch** per mode: **FR_FULL** = full K=7 (bundles A–G); **FR_LITE** = journal-justified Y ⊆ K=7. Scope = **whole deliverable** — not dirty-only of the last step, not K=12 atoms. Fresh **Aggregator** merges (canonical big table always full catalog; non-Y seats N/A-coded under FR_LITE). After findings within FINAL-REVIEW, re-dispatch **dirty bundles only** (+ sister-blast) unless the orchestrator justifies a full re-fan-out.
4. Fresh **Fixer** when the Aggregator surfaces findings.
5. 10-iteration ceiling (one iteration = one K-seat batch + Aggregator + Fixer); escalate if hit.
6. Distillation entry.

**FR_COLLAPSED:** skip the separate FR folder; the step's CLEAN multi-seat Y-audit (path-set = whole deliverable KEEP surfaces) + completeness checklist walk satisfy the deliverable gate. Multi-seat Y still required (FR_COLLAPSED ≠ K=1).

Zero FINDING rows in the latest Aggregator's big table (FR journal under FR_FULL/FR_LITE, or **step** journal under FR_COLLAPSED) → ready to SHIP.

### SHIP (handoff to user REVIEW)

**Trigger is FR-mode-aware:**

| Mode | SHIP triggers on | Distillations |
| --- | --- | --- |
| **FR_FULL / FR_LITE** | Final-review's clean termination | All **step + final-review** distillations |
| **FR_COLLAPSED** | **Step CLEAN Y-audit** (step scope ≡ deliverable) **+** completeness checklist walk (Final-review gate boxes cite **step** journal + collapse mode) — **not** a missing `final-review/journal.md` | **Step only** |

0. **Walk the [Deliverable completeness checklist](rules.md#deliverable-completeness-checklist-the-gate-before-user-review) BEFORE anything else.** Every box must be an honest YES with a citation (or **N/A** with `META`/`NO_CS` + path-set for pure-meta product gates — never forge green build/inspect/test); if any is NO, go back into fix-loops and re-walk. Under **FR_COLLAPSED**, Final-review gate boxes map to the **step** journal. Then write the verbatim attestation block (from rules.md) into the root README — without it, SHIP cannot proceed.
1. **Aggregate proposed rule additions** from distillations per the FR-mode table above; deduplicate; append to the root README's Proposed-rule-additions section.
2. **Present the root README to the user** — did the audit catch what the user would have? (spot-check 1-2 journals); approve/tweak each proposed rule; approve the merge.
3. **Apply approved rule additions** to `docs/dev/rules.md` (committed).
4. **Copy the root README as a snapshot** to `docs/dev/deliverables/NNNN-<name>.md` (committed single file); flip Status to `SHIPPED YYYY-MM-DD`; populate the final-report section; rephrase per-step-journal references as prose (journals don't cross the commit boundary).
5. **Leave the wip/ workspace untouched** — journals stay gitignored/local-only; the process never auto-deletes them.
6. **Consumable-lib changes carry the conventional-commit footer** the `tools/release-runner` reads at release time (per `rules.md §26.19`); SHIP itself does not bump versions — the runner runs separately, after the merge.
7. **Commit separately, in order**: approved `rules.md` additions → shipped deliverable code (squash-merge) → the new `docs/dev/deliverables/NNNN-<name>.md` snapshot.

Each commit needs explicit user permission (no auto-commit).

### REVIEW (user phase)

**REVIEW is observe-and-capture, not fix-on-sight.** When the user surfaces feedback: (1) capture it as a numbered list — fix nothing yet; (2) per item, confirm understanding + ask "fix? leave? discuss further?"; (3) user decides per item; (4) approved fixes get a fresh deliverable folder (or, for trivial single-item fixes, a small follow-up commit with a regression test).

If REVIEW finds bugs the audit should have caught, the response isn't just "fix the bug" — it's "what category was this, and why didn't the predicate catch it?" That gap becomes a new `rules.md` predicate. Without this loop the catalog stays static and the agent re-makes the same misses.

### Append-only discipline

Per-step `journal.md` files are append-only at the **substantive content** level: ✅ fix typos / formatting / rendering; ❌ rewrite an audit finding to look smaller; ❌ delete entries from earlier rounds; ❌ edit a previous round's "Findings: 0 (clean)" to add a bug a later round found. The journal IS the evidence of process integrity — if round 3 missed something round 5 caught, the journal must show it (hiding the miss prevents the kind from feeding back into `rules.md`). **Honest journals are self-rewarding**: every honest miss becomes a future gate-check.

### Scope of work shape

Scales across sizes. **Small deliverable** — one csproj, one feature: `01-<feature>` + `final-review`, two journals. **Large deliverable** — multi-csproj build-out: `01-csproj-1` … `09-csproj-9` + `final-review`; cross-cutting decisions in the root README. There's no "lightweight path" for trivial changes — even a typo fix benefits from "did you check whether this typo appears elsewhere?" The cost of running the full ruleset on a small change is minutes; NOT running it is a future audit round. **The orchestrator-only-main-thread + fresh-sub-agent-per-round pattern (see [§3](#3-sub-agent-architecture)) applies at every scope**: a one-line typo fix still spawns Planner / Implementer / Auditor / (if findings) Fixer.

### What this process does NOT do

- **Doesn't replace AGENTS.md** — AGENTS.md is the shared agent-directive root + conventions references; this doc defines the _process_ ensuring conventions are followed.
- **Doesn't replace `docs/v2/`** — phase / wave tracking lives there; this process is per-deliverable, `docs/v2/` is the long-arc roadmap.
- **Doesn't replace per-lib READMEs** — each lib documents its own public API.
- **Doesn't run scripts** — no pre-commit hook fires `rules.md` mechanically; the discipline is the agent walking the rules each round and producing journal-verifiable evidence.

### When to invoke this process

Always, for any work substantial enough to warrant a deliverable folder. The user can override per-task ("just do this small thing, no journal needed" — per [rules.md §13.14](rules/13-permission-action-discipline.md#13-permission--action-discipline)), but the default is the loop. Forcing function: if there's no `docs/wip/<deliverable>/README.md` for the work in flight, the agent ASKS whether to create one before proceeding past PLAN.

---

## 2. Permission gates (when to pause for the user)

The following require explicit user permission **per occurrence**, not implied from prior turns. Predicate-of-record: [rules.md §13 Permission / Action Discipline](rules/13-permission-action-discipline.md#13-permission--action-discipline).

> **Duplicated from [rules.md §13](rules/13-permission-action-discipline.md#13-permission--action-discipline) for at-a-glance protocol context — the canonical full version (Evidence + Why + How per predicate) lives in rules.md; update both in lockstep when either changes (per [rules.md §11.32](rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices)).**

- **Commit creation** — "go ahead and commit" approves the batch just discussed; the next commit needs fresh permission. Take every commit through the sanctioned `cycle-commit` marker path (one-shot `.claude/.commit-authorized` marker, EXIT-trap-removed). Structural backstops: Claude/Grok → `git-guard` PreToolUse; Codex → `d2-policy-guard.mjs` PreToolUse — both block raw `git commit` / destructive git without the shared marker. Never a direct git command. (§13.1 / §13.1a)
- **Bulk file operations** (sed across N files, mass rename, multi-file delete, bulk format-write) — declare scope (file count, glob, what changes) BEFORE executing; user can redirect. (§13.2)
- **Destructive git operations** (force push, hard reset, branch delete, overwriting checkout) — explicit authorization required. (§13.3)
- **Deferring planned work** — if a step turns out larger, ASK to defer; don't unilaterally skip. (§13.4)
- **Architectural decision changes mid-execution** — if implementation surfaces a reason to deviate from the locked PLAN, ASK; don't silently rework. (§13.5)
- **Process-bypass naming** — every bypass requires per-occurrence user-quoted authorization NAMING the specific rule / step skipped. "Go ahead" / "looks good" / implicit consent does NOT qualify. (§13.14)
- **K=1 audit-round dispatch** — never self-invoked; requires explicit per-round user permission with quoted authorization in the orchestrator log. (§24.0h + [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy))

### Deferral posture — do-it-now is the default

Reflexive deferral is a recurring failure mode. Its form: marking work "inert until a consumer", "deferred to the live-wiring step", or "track for follow-on" when the work was fully buildable and provable in isolation — no missing build dependency, only the absence of a live downstream caller.

The operative test for legitimate deferral: is a **build dependency genuinely missing** — something that must exist before the work can be built AND proven in isolation (an unbuilt collaborator with no faithful §1.32 test-double, an undesigned decision whose outcome changes the work's shape, missing infrastructure, or a running host/process needed for LIVE wiring)? "No consumer yet", "not wired into the live host yet", "not exercised cross-process yet", "a fixture/tracker labels it deferred", "the real config/domain values don't exist yet" are NOT build dependencies — proving in isolation (Testcontainers, in-memory TestServer, faithful §1.32 doubles) needs no live host and no real consumer. Default: **if the work is in-scope and no build dependency is missing, build it and prove it in isolation now** — don't wait for the first consumer (waiting is how no-dependency work gets silently forgotten). Correct-and-complete is preferred over fast-and-partial even when substantially slower. A genuine blocker gets a committed tracker row (not a comment/journal-only TODO) and is surfaced per §13.4. YAGNI applies only to work that is NOT known-needed. Predicate-of-record: [rules.md §13.15](rules/13-permission-action-discipline.md#13-permission--action-discipline).

---

## 3. Sub-agent architecture

**The main thread is an ORCHESTRATOR. It does not plan, implement, audit, or fix domain work itself. EVERY round of planning, implementation, auditing, and fixing is performed by a FRESH sub-agent** — IF Claude Code → spawn via `Agent`; IF Grok Build → spawn via `spawn_subagent`; IF Codex → spawn via `spawn_agent` — always with the **runtime-prefixed** pin name (`claude-d2-<role>` / `grok-d2-<role>` / `codex-d2-<role>`; see [harness-runtimes.md](harness-runtimes.md)). Canonical workflow, not optional.

### Why this is structural, not stylistic

Anthropic's multi-agent research system (orchestrator-Opus + worker-Sonnet) outperforms single-agent Opus by 90.2% on internal evals — the orchestrator-worker pattern is empirically validated for adversarial separation of concerns. Adversarial code-review research shows LLM self-review has systematic leniency bias, and that a reviewer + generator sharing context share blind spots ("most agent-reviews-agent implementations are one LLM pretending to be three reviewers, rubber-stamping itself"). The structural fix is SEPARATE sub-agent invocations with fresh contexts, not roleplay. Empirically (0002-auth-inbound trial): per-step audits converged in 1-3 rounds, main-thread context stayed small, and two production bugs were caught that single-context implementation would have shipped (full writeup: [Appendix C](#appendix-c-trial-outcomes-from-deliverable-0002-auth-inbound)). This mirrors Claude Code's sub-agent design: each sub-agent gets a fresh isolated context and returns only relevant output — context rot in the main thread is near-impossible because it holds almost no domain state.

### Allowed in main-thread context

- ✅ Spawn sub-agents — the primary orchestrator activity: IF Claude Code → `Agent` with `claude-d2-<role>`; IF Grok Build → `spawn_subagent` with `grok-d2-<role>`; IF Codex → `spawn_agent` with `codex-d2-<role>` (never bare `d2-*`; never another runtime's prefix)
- ✅ `Bash` — git plumbing ONLY (`git status`, `git log`; commits only via `/cycle-commit` after per-occurrence user permission — §13.1 / §13.1a; `git push` only after user permission)
- ✅ `Read` — ONLY the deliverable root README + the orchestrator's own decision log; sub-agents handle source / test / journal reads
- ✅ Task / todo tools as the host provides (`TaskCreate` / … or equivalent)
- ✅ `Edit` / `Write` to the root README's tracking sections + its own decision log (`docs/wip/<deliverable>/orchestrator-log.md` if used)
- ❌ `Edit` / `Write` to source / tests / per-lib READMEs / framework docs (sub-agents do this)
- ❌ `Edit` / `Write` to journal big-table or findings log (Auditor / Aggregator do this)

The main thread's job is decision-making, not implementation or auditing. **It cannot mark anything CLEAN or PASS itself** — it only consumes those verdicts from sub-agents.

### Forbidden in main-thread context

- `Edit` / `Write` to ANY source file, test file, per-csproj / per-service README, or framework doc
- `Bash` for builds, tests, `jb inspectcode`, or any domain-level grep / inspection
- `Read` on source / test files or per-lib READMEs — delegate to sub-agents
- Reading journal files mid-deliverable for content review — delegate state-checks to a sub-agent that reports a summary
- Walking `rules.md` predicates — always Auditor sub-agents
- Marking anything CLEAN / PASS / converged from main-thread judgment — those verdicts come from Auditor output

### Canonical sub-agent roles

Each role is spawned with fresh context + a tightly-scoped prompt (**no reuse across roles or across rounds**) and maps to a git-tracked **runtime-prefixed** pin file that pins its model / effort / tool-access / spawn `name:` — IF Claude Code → `.claude/agents/claude-d2-<role>.md` spawn `claude-d2-<role>`; IF Grok Build → `.grok/agents/grok-d2-<role>.md` spawn `grok-d2-<role>`; IF Codex → `.codex/agents/codex-d2-<role>.toml` spawn `codex-d2-<role>` ([harness-runtimes.md](harness-runtimes.md)). Final-reviewer is not a separate agent — it reuses the Auditor definitions at deliverable-wide scope.

| Role | Spawn / file (Claude · Grok) | Spawned when | Tool access | Returns |
| --- | --- | --- | --- | --- |
| **Planner** | `claude-d2-planner` · `grok-d2-planner` · `codex-d2-planner` | Start of each step | Read, Grep, Glob, codebase-memory (discovery), Write (journal Plan section; no source Edit) | Step Plan section + summary |
| **Plan-Auditor** (parallel ×K seats) | `claude-d2-plan-auditor` · `grok-d2-plan-auditor` · `codex-d2-plan-auditor` | After Planner (new types / patterns / >50-file scope per §24.16) | Read, Grep, Glob, codebase-memory (discovery), shell (read-only), Write (own partial; no source edit / no sub-agent spawn) | Partial big-table chunk auditing the Plan section for its seat (mid-step bundle A–G / final atom) |
| **Plan-amender** | `claude-d2-plan-amender` · `grok-d2-plan-amender` · `codex-d2-plan-amender` | When Plan-Audit Aggregator surfaces findings | Read, Grep, Glob, codebase-memory (discovery), Edit (journal Plan section + Plan-Audit fix log only) | Plan-section edits + appended Plan-Audit fix-log entries |
| **Implementer** | `claude-d2-implementer` · `grok-d2-implementer` · `codex-d2-implementer` | After Planner (carve-out steps) OR after Plan-Audit CLEAN | All (graph-first discovery; §24.13.1 Evidence greps still literal) | Files touched + tests added + build / inspectcode status |
| **Auditor** (parallel ×K seats) | `claude-d2-auditor` · `grok-d2-auditor` · `codex-d2-auditor` | After Implementer | Read, Grep, Glob, codebase-memory (discovery), shell (read-only), Write (own partial; no source edit / no sub-agent spawn) | Partial big-table chunk for its seat ([partition](#auditor-cluster-partition-dual-mode)) |
| **Auditor-deep** (parallel ×K) | `claude-d2-auditor-deep` · `grok-d2-auditor-deep` · `codex-d2-auditor-deep` | After Implementer, for deep seats (bundles **C/D/G**) + ruling-critical | Same as Auditor | Partial big-table chunk for its judgment-heavy seat |
| **Aggregator** (one per audit round) | `claude-d2-aggregator` · `grok-d2-aggregator` · `codex-d2-aggregator` | After all K Auditors (or K Plan-Auditors) return | Read, Edit (journal + audit artifacts only; no sub-agent spawn) | Merged canonical big table + consolidated findings-log entry + cross-cluster verification |
| **Fixer** | `claude-d2-fixer` · `grok-d2-fixer` · `codex-d2-fixer` | When findings exist | All (graph-first discovery; sister-sweep Greps still literal per §24.13.3–.4) | Files changed + own fix-log file |
| **Fixer-mechanical** | `claude-d2-fixer-mechanical` · `grok-d2-fixer-mechanical` · `codex-d2-fixer-mechanical` | When findings are enumerated mechanical scope (rewrites / re-points / renames / spelling / line-wraps) | All | Files changed + own fix-log file; STOPs and hands back on judgment work |
| **Final-reviewer** (parallel ×K) | (auditor / auditor-deep defs) | Before SHIP | Same as Auditor | Cluster-scoped partial big tables at deliverable scope; Aggregator merges |

**Code discovery (token discipline):** the orchestrator resolves `MCP_PROJECT` by canonical Git root once per session/dispatch per [codebase-memory.md](codebase-memory.md) and injects it into every sub-agent brief. Roles consume only dispatch-provided `MCP_PROJECT`; if missing, they fail closed/report and use disk. When the project is available and indexed, sub-agents **prefer** graph tools (`search_graph`, `search_code` files/compact) over Grep/Glob for locating symbols and files. The graph is **not** source of truth — disk Read + literal Evidence Greps win for predicate rows ([codebase-memory.md](codebase-memory.md)).

**Key design decisions:**

- **Planner is its own role** — writes the step's Plan (goal, files, decisions, pre-emptive gate checks) and returns; the Implementer receives the Plan as input, fresh context.
- **Auditors cannot modify source** (read-only Bash) — "audit + fix in same session" is structurally impossible; fixes happen in a separate Fixer invocation AFTER findings are RECORDED (no "I fixed it before recording it" sleight-of-hand).
- **Auditor adversarial framing** — the prompt states it's rewarded for finding issues, not for declaring CLEAN; its role is hostile critic.
- **Parallel cluster dispatch is the default** — K≤7 Auditors run concurrently per round on [bundles A–G](#auditor-cluster-partition-dual-mode) (full K=7, Y ⊆ K=7, or dirty subset); the [Aggregator](#aggregator-role-post-cluster-consolidation) merges + cross-verifies. After findings: dirty-only re-dispatch of seats with ≥1 finding (+ sister-blast).
- **Effort-scaling in prompts** — each prompt caps effort proportional to the step's surface area; cluster scope already constrains per-Auditor effort to ~10-40 rows.
- **Aggregator is required whenever K>1** — it produces the canonical big table + consolidated findings entry; it dedupes / merges / adds cross-cluster findings but cannot flip a per-cluster verdict unilaterally (escalates ties to the orchestrator). Runs on the deep-workhorse tier (Opus / grok-4.5 / Sol; spawn `claude-d2-aggregator` / `grok-d2-aggregator` / `codex-d2-aggregator` per [harness-runtimes.md](harness-runtimes.md) + the [model policy](#sub-agent-model-policy-per-role)).
- **K=1 carve-out requires explicit user permission** (§24.0h + [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy)); NEVER self-invoked.
- **Plan-Audit is mandatory before Implementer dispatch for non-trivial steps** (§24.16) — details + carve-outs in [EXECUTE step 1a](#execute).

### Sub-agent model policy per role

**Multi-runtime pins + spawn names:** process law (roles, tiers, K≤7 seats, fences) is shared. **Product model IDs and spawn names are runtime-owned** — IF Claude Code → `.claude/agents/claude-d2-*.md` spawn `claude-d2-<role>`; IF Grok Build → `.grok/agents/grok-d2-*.md` spawn `grok-d2-<role>`; IF Codex → `.codex/agents/codex-d2-*.toml` spawn `codex-d2-<role>`. Three-runtime map → [harness-runtimes.md](harness-runtimes.md). Never rewrite one runtime's pins/names to satisfy another; never use bare unprefixed `d2-*` spawn names while multiple trees exist.

**The git-tracked pin files for the active runtime are the CANONICAL source** for each role's model, effort, tool-access, and mission prompt **when that host applies the pin** (Claude Code and Grok Build do; Codex TOML is intended inventory until host application is proven — [harness-runtimes.md](harness-runtimes.md) known limits); this section describes how those roles OPERATE and WHY each sits on its **capability tier**. The table below uses Claude/Anthropic product names (historical + Claude-default wording) and **Claude spawn/file names**; Grok and Codex equivalents are in [harness-runtimes.md](harness-runtimes.md). All other references (rules.md, AGENTS.md) cross-link here for *behavior*; on any model / effort / tool / spawn-name specific, the **active runtime's** applied pin wins. Predicate-of-record (walked every audit round): [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

Three tiers: **Fable** = planning-shaped reasoning; **Opus** = deep-reasoning workhorse (synthesis + heavy bounded authorship); **Sonnet** = light workhorse (predicate-walking + grep + mechanical). Claude pins: Fable / Opus / Sonnet per table. **Grok (user ruling 2026-07-09):** **all** roles pin `grok-4.5` — planning/deep · **`high`**; volume seats (mechanical Auditor / Investigator / Fixer-mechanical) · **`medium`** (high overkill for ex-Composer work). **`grok-composer-2.5-fast` is cost-banned**. Role fences still apply.

| Role | Claude definition (spawn / file) | Model · effort | Why this model |
| --- | --- | --- | --- |
| **Orchestrator** (main thread) | (main thread) | Fable 5 | Judgment + delegation + trust-but-verify discipline ([§4](#orchestrator-verification-of-sub-agent-outputs)); catches workhorse hallucinations / short-circuits. |
| **Planner** | `claude-d2-planner` / `claude-d2-planner.md` | Fable 5 · max | Plan quality drives every downstream sub-agent; high-leverage low-volume — a missed gate cascades into Implementer + Auditor + Fixer cycles. |
| **Plan-Auditor** (per-seat, K≤7 bundles A–G) | `claude-d2-plan-auditor` / `claude-d2-plan-auditor.md` | Opus 4.8 · xhigh | Judgment-heavy verification of Plan claims against real code — at multi-seat volume per round, the deep-workhorse tier at `xhigh` holds ruling fidelity at cluster-loop cost (retiered off Fable, cost ruling 2026-07-09; K=7 max seats vs historical always-12). |
| **Plan-amender** | `claude-d2-plan-amender` / `claude-d2-plan-amender.md` | Fable 5 · high | Writes to the same canonical Plan artifact; amendments must stay coherent with locked decisions. |
| **Aggregator** (one per audit round) | `claude-d2-aggregator` / `claude-d2-aggregator.md` | Opus 4.8 · high | Merges K cluster partials (≤7, or dirty subset) + cross-cluster sister-sweep + severity arbitration; deep bounded synthesis. |
| **Auditor** (per-seat; mechanical seats) | `claude-d2-auditor` / `claude-d2-auditor.md` | Sonnet 4.6 · high | Predicate pattern-matching + grep + file:line citations; bounded structured output, no synthesis — Sonnet saturates. |
| **Auditor-deep** (per-seat; deep bundles C/D/G + ruling-critical) | `claude-d2-auditor-deep` / `claude-d2-auditor-deep.md` | Opus 4.8 · high | Same law, reserved for architectural-layer / security / audit-meta seats where a missed or mis-severitied finding costs most. |
| **Final-reviewer** (per-cluster) | (auditor / auditor-deep defs) | per auditor tier | NO separate agent — the Auditor definitions at deliverable-wide scope. |
| **Implementer** | `claude-d2-implementer` / `claude-d2-implementer.md` | Opus 4.8 · high | Bounded code/test authorship per a brief; the hard design reasoning was done by Plan / orchestrator. Sweeping carve-out applies (below). |
| **Fixer** | `claude-d2-fixer` / `claude-d2-fixer.md` | Opus 4.8 · high | Root-cause remediation + regression test against a tight contract. Sweeping carve-out applies. |
| **Fixer-mechanical** | `claude-d2-fixer-mechanical` / `claude-d2-fixer-mechanical.md` | Sonnet 4.6 · medium | Enumerated behavior-preserving fixes (comment rewrites, re-points, renames, spelling, line-wraps); STOPs and hands back for the active-runtime Fixer role (`claude-d2-fixer` / `grok-d2-fixer` / `codex-d2-fixer`) on any judgment work. |
| **Investigator / Research** | `claude-d2-investigator` / `claude-d2-investigator.md` | Sonnet 4.6 · high | Bounded read-only investigation returning structured file:line reports; no synthesis. |

Grok spawn names replace the `claude-` prefix with `grok-`; Codex uses `codex-` and TOML definitions (for example, `codex-d2-implementer`). Full three-runtime table → [harness-runtimes.md](harness-runtimes.md).

**Why this allocation**: spend Fable where design judgment moves outcomes (high-leverage planning, plan-vs-code verification, the trust-but-verify orchestrator); spend Opus on deep bounded work that still needs strong reasoning (the K-partial cross-cluster merge, the judgment-heavy audit seats, code/test authorship, root-cause fixing); use Sonnet where capability saturates against a tight contract (mechanical-seat predicate walking, bounded investigation, enumerated mechanical fixes). Multi-seat Auditor dispatch is the highest-volume pattern, so its default tier is Sonnet — auditor-deep escalates judgment-heavy seats (bundles **C / D / G**) + any ruling-critical seat to Opus. The [orchestrator verification discipline](#orchestrator-verification-of-sub-agent-outputs) is the structural compensation that makes workhorse dispatch (Opus + Sonnet) safe.

**Sweeping carve-out** (Implementer / Fixer Fable escalation — codified bypass, no per-occurrence user approval needed): qualifies when it meets ≥1 criterion below; the dispatch brief MUST cite the triggering criterion + justification, and the return self-attestation MUST echo it.

1. **Atomic large-file-set** — touches >40 files atomically (can't split without breaking the build or producing audit-failing intermediate states).
2. **Multi-concern dispatch** — spans >3 distinct concerns where splitting creates coordination overhead exceeding the Fable premium (e.g. new handler + DI wiring + test + README + proto wiring).
3. **Cross-runtime refactor** — coordinated .NET + TS changes (naming sweep across both, cross-language rename, parity-test alignment).
4. **Cascading pipeline change** — changes a code-gen pipeline (or its input) and regenerates downstream consumer assemblies.

The carve-out applies ONLY to Implementer / Fixer (Opus → Fable). Escalating any other role ABOVE its pinned tier — an Auditor / Investigator / Fixer-mechanical to Opus or Fable (Claude), an Aggregator or a Plan role to a different model — requires explicit per-occurrence user approval per [rules.md §13.14](rules/13-permission-action-discipline.md#13-permission--action-discipline). The codified mechanical-auditor → auditor-deep split for bundles C / D / G + ruling-critical is a role CHOICE (Claude: Sonnet → Opus; Grok: same product model `grok-4.5`, different mission pin), not an escalation, and needs no approval.

**Pinned-definition overrides** — the active runtime's agent definitions (Claude: `.claude/agents/claude-d2-*.md`; Grok: `.grok/agents/grok-d2-*.md`; Codex: `.codex/agents/codex-d2-*.toml`) pin each role's model + effort + tool-access/sandbox + spawn `name:`. Overriding any pinned value on a specific dispatch (a different `model`, a different `effort`, relaxing a tool/sandbox fence, or using the wrong runtime's spawn name) requires the same §13.14-style per-occurrence user acknowledgment naming the pinned value bypassed — the codified Sweeping carve-out above is the ONE exception.

**Self-documentation requirement** — every sub-agent return summary opens with the model-attestation block (see [Dispatch-brief template](#dispatch-brief-template)); the orchestrator's per-step journal records per dispatch: the model, the role, and (if the pinned tier was overridden — e.g. an Implementer / Fixer escalated to Fable) the carve-out criterion + verbatim justification. This dual-channel attestation gives retroactive auditability for the self-learn loop (which dispatches needed re-do vs which could've run a tier lower).

**Cross-references:** [harness-runtimes.md](harness-runtimes.md) (Claude vs Grok vs Codex pins) · [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (predicate enforcement) · [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (K=1 discipline; composes with §24.0i) · [§4 orchestrator verification](#orchestrator-verification-of-sub-agent-outputs) · [AGENTS.md MANDATORY block 0](../../AGENTS.md#mandatory-block-0-orchestrator-only-main-thread) (its role table cross-links here for the model column).

### Every round = a NEW fresh sub-agent

A second audit round is a brand-new Auditor, NOT the same one "running again." A fix follow-up after a Fixer's first attempt is a brand-new Fixer. The fresh-context property prevents leniency / motivated-stopping / stale-memory — reusing context across roles defeats the whole pattern. The orchestrator never short-circuits this for "quick" work: a one-line typo fix still spawns Planner / Implementer / Auditor / (if findings) Fixer.

### The orchestrator cannot mark CLEAN

The orchestrator consumes Auditor verdicts; it cannot promote a step to CLEAN by judgment. CLEAN means "the latest Auditor's big table contained zero FINDING rows." To confirm closure it spawns a fresh Auditor — it does not eyeball.

### Auditor cluster partition (K=7 max)
<a name="auditor-cluster-partition-dual-mode"></a>
<a name="auditor-cluster-partition-canonical-k12"></a>

> **Canonical one-liner:** Default full audit partition is **K=7 concern bundles (A–G)**. **K=12 atomic dispatch is retired.** Targeted Y and dirty-only re-dispatch apply on per-step rounds. **FINAL-REVIEW open** is mode-selected: **FR_FULL** = full K=7 at deliverable scope (product default); **FR_LITE** = deliverable-scope Y ⊆ K=7 when eligibility gates pass; **FR_COLLAPSED** = pure-meta 1-step only (no separate FR journal). See [Audit wave policy](#audit-wave-policy).

The `rules.md` catalog (~24 categories, ~450 numbered subsections) has **one** full-partition dispatch size:

| Mode | K | When | Seats |
| --- | --- | --- | --- |
| **Full partition** (product Plan-Audit R1 in-scope, justified full code-audit, **FR_FULL**) | **K=7** | Complete catalog walk | Concern-first **bundles A–G** |
| **Targeted first code-audit / pure-meta Plan-Audit / FR_LITE** | **Y ⊆ K=7** | Step- or deliverable-relevant only | Journal-justified subset of A–G |
| **Re-round after findings** | dirty subset | Plan-Audit AND code-audit AND within FINAL-REVIEW after findings | Bundles with ≥1 finding + sister-blast |

**K=7 is the MAX and the only full-partition size.** There is no default or optional K=12 audit wave. **FR_FULL** uses the **same** bundles A–G at **whole-deliverable scope** — not dirty-only of the last step, not 12 atomic seats. **FR_LITE** / pure-meta Y still merge a **full-catalog** big table (non-dispatched seats N/A-coded).

**Atoms stay stable for provenance only.** Codes A1…E3 remain atomic §-ownership IDs in findings provenance / Y-maps / historical journals. They are **NOT** a parallel dispatch mode and **NOT** "FINAL K=12". K=7 bundles are the **only** dispatch seats. Prefer one partial per **bundle** (`r{N}-partial-A-correctness.md`, …) with section headers per atom if helpful. **Finding IDs:** seat/bundle code for the dispatch seat + **§-number** for predicate ownership (e.g. `PA-R2-A-1` with §1.x in the row) — do not invent a third ID space.

**K=1** still requires explicit per-round user permission (§24.0h). Never self-invoked. **Dirty-only re-dispatch** (subset of the active partition after findings) is **not** a K=1 carve-out.

#### K=7 concern bundles (universal full partition)

Grouping by **concern** beats perfectly equal runtime. Security, KEEP-docs, and audit-meta stay **pure** (not padded with unrelated heavy categories). E3 (temporal/codegen) rides with E1 (ops/quality) rather than polluting E2.

| Bundle | Atoms | rules.md §§ | Theme | Auditor tier |
| --- | --- | --- | --- | --- |
| **A** | A1+A2 | §1, §2, §4, §15, §18, §22 | Correctness (tests + races/disposal/degradation/idempotency) | mechanical |
| **B** | B1+B2+B3 | §5, §6, §7, §12, §16, §17 | Conventions + naming/i18n + shared-lib/D2Result | mechanical |
| **C** | C1+C2 | §3, §8, §9 | PII/logging/ops + architectural layer | **deep** (C2) |
| **D** | C3 | §10, §13 | Security + permissions — **PURE** | **deep** |
| **E** | D1 | §11, §14 | KEEP docs + verbiage — **PURE** (dominant finding surface) | mechanical |
| **F** | E1+E3 | §19, §20, §21, §23, §25, §26 | Operational quality + temporal/codegen | mechanical (deep only if ruling-critical codegen) |
| **G** | E2 | §24 | Audit-meta — **PURE** (process integrity) | **deep** |

**Per-bundle category-file reading list** (union of atom files). Each seat Auditor reads ONLY its files — not the whole catalog — plus the index-level [Deliverable completeness checklist](rules.md#deliverable-completeness-checklist-the-gate-before-user-review) (every seat).

| Bundle | Category files to read |
| --- | --- |
| **A** | [01](rules/01-test-discipline.md), [02](rules/02-bug-fix-regression-testing.md), [04](rules/04-concurrency-race-conditions.md), [15](rules/15-object-disposal-resource-lifetime.md), [18](rules/18-graceful-degradation-failure-modes.md), [22](rules/22-idempotency-exactly-once-semantics.md) |
| **B** | [05](rules/05-csharp-code-conventions.md), [06](rules/06-typescript-sveltekit-code-conventions.md), [07](rules/07-naming-file-headers-folder-casing.md), [12](rules/12-i18n-discipline.md), [16](rules/16-ootb-shared-lib-tooling-use-whats-there.md), [17](rules/17-d2result-usage-extensions.md) |
| **C** | [03](rules/03-pii-logging-safety.md), [08](rules/08-build-tooling-hygiene.md), [09](rules/09-architectural-layer-hygiene.md) |
| **D** | [10](rules/10-security-endpoints-auth-secrets-input.md), [13](rules/13-permission-action-discipline.md) |
| **E** | [11](rules/11-documentation-parity-best-practices.md), [14](rules/14-phase-audit-conversation-verbiage-hygiene.md) |
| **F** | [19](rules/19-user-experience-ux.md), [20](rules/20-developer-experience-dx.md), [21](rules/21-observability-completeness.md), [23](rules/23-configuration-hygiene.md), [25](rules/25-temporal-types-date-time-clock.md), [26](rules/26-codegen-discipline-spec-proto-schema-derived-types.md) |
| **G** | [24](rules/24-audit-evidence-discipline-meta-how-to-audit.md) |

**Why K=7 (universal max):** concern-first seats keep critical checks pure (D security, E docs, G audit-meta) so a deep seat is not diluted by unrelated volume; wall-clock still dominated by the slowest seat, not the sum; fewer seats cut orchestrator fan-out + Aggregator merge cost vs historical always-12 while preserving full § coverage. Targeted first code-audit may dispatch **Y ⊆ K=7** (step-relevant bundles) with journal justification; product Plan-Audit R1 defaults full K=7; pure-meta/docs Plan-Audit defaults **Y** (E+G); **FINAL-REVIEW** opens per mode (**FR_FULL** = full K=7; **FR_LITE** = Y when gates pass; **FR_COLLAPSED** = step Y-audit only) — not "always full K=7 with no exceptions."

**Dirty-only re-dispatch (Plan-Audit AND code-audit AND FINAL-REVIEW after findings):** after findings, re-dispatch **only dirty bundles** (those with ≥1 finding), plus any sister-blast seats the Fixer/Plan-amender touched (orchestrator cites file→bundle). Do **not** default re-dispatch full K=7. Dirty-only is a subset of the active partition — **not** a §24.0h K=1 carve-out. **Do not** open FINAL-REVIEW as dirty-only of the last step's seats (FR_FULL/FR_LITE open at deliverable scope under their mode rules). **Full-catalog still required every round:** Aggregator emits one row per catalog §; dirty seats re-walk independently; clean seats re-cited only via explicit dirty-only merge ([§24.0e / §24.0f / §24.6](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)). **CLEAN** = zero FINDING rows on that full-catalog table — not a dirty-slice-only verdict. Stale dual-law "always re-walk full K every re-round ignoring dirty-only" is retired.

#### Historical atom IDs (not a dispatch mode)
<a name="atomic-k12-partition-final-review-default"></a>

Atoms A1…E3 are **§-ownership / provenance IDs** for reading old journals and mapping findings to bundles. **They are not dispatched as seats.** Former "FINAL-REVIEW K=12" atomic dispatch is **retired**.

| Atom | Name | rules.md sections | ~preds | Theme |
| --- | --- | --- | --- | --- |
| **A1** | Tests / coverage + regression | §1, §2 | ~35 | Test discipline / coverage, bug-fix regression-pinning |
| **A2** | Races, disposal, degradation, idempotency | §4, §15, §18, §22 | ~45 | concurrency / races, object disposal / resource lifetime, graceful degradation, idempotency / exactly-once |
| **B1** | C# conventions | §5 | ~35 | C# conventions |
| **B2** | TS conventions + naming + i18n | §6, §7, §12 | ~45 | TypeScript / SvelteKit conventions, naming / file headers / folder casing, i18n / Paraglide / TK constants |
| **B3** | Shared-lib hygiene + D2Result | §16, §17 | ~15 | OOTB shared-lib catalog use, D2Result usage + extensions |
| **C1** | PII/logging + operations | §3, §8 | ~25 | PII / logging safety, build cleanliness + operational hygiene |
| **C2** | Architectural layer | §9 | ~45 | Architectural layer hygiene |
| **C3** | Security + permissions | §10, §13 | ~35 | Security (endpoints / auth / secrets / input), permission / action discipline |
| **D1** | KEEP doc parity + verbiage hygiene | §11, §14 | ~50 | KEEP-doc updates + forward-framing + per-lib README parity, no-phase-verbiage / no-conversation-scoped-IDs hygiene |
| **E1** | UX + DX + observability + config | §19, §20, §21, §23 | ~45 | UX, DX, observability completeness, configuration hygiene |
| **E2** | Audit-meta | §24 | ~50 | Audit evidence discipline (incl. self-audit per §24.12) |
| **E3** | Temporal + codegen | §25, §26 | ~35 | temporal-types discipline, codegen discipline |

**Per-atom category files** (for provenance / sister-sweep mapping only — dispatch reads the **bundle** reading list above):

| Atom | Category files |
| --- | --- |
| **A1** | [01](rules/01-test-discipline.md), [02](rules/02-bug-fix-regression-testing.md) |
| **A2** | [04](rules/04-concurrency-race-conditions.md), [15](rules/15-object-disposal-resource-lifetime.md), [18](rules/18-graceful-degradation-failure-modes.md), [22](rules/22-idempotency-exactly-once-semantics.md) |
| **B1** | [05](rules/05-csharp-code-conventions.md) |
| **B2** | [06](rules/06-typescript-sveltekit-code-conventions.md), [07](rules/07-naming-file-headers-folder-casing.md), [12](rules/12-i18n-discipline.md) |
| **B3** | [16](rules/16-ootb-shared-lib-tooling-use-whats-there.md), [17](rules/17-d2result-usage-extensions.md) |
| **C1** | [03](rules/03-pii-logging-safety.md), [08](rules/08-build-tooling-hygiene.md) |
| **C2** | [09](rules/09-architectural-layer-hygiene.md) |
| **C3** | [10](rules/10-security-endpoints-auth-secrets-input.md), [13](rules/13-permission-action-discipline.md) |
| **D1** | [11](rules/11-documentation-parity-best-practices.md), [14](rules/14-phase-audit-conversation-verbiage-hygiene.md) |
| **E1** | [19](rules/19-user-experience-ux.md), [20](rules/20-developer-experience-dx.md), [21](rules/21-observability-completeness.md), [23](rules/23-configuration-hygiene.md) |
| **E2** | [24](rules/24-audit-evidence-discipline-meta-how-to-audit.md) |
| **E3** | [25](rules/25-temporal-types-date-time-clock.md), [26](rules/26-codegen-discipline-spec-proto-schema-derived-types.md) |

**Cross-cutting concerns belong to the Aggregator**, not any one seat. When a predicate seems to straddle seats, the mapping is **§-number → atom → bundle** — the §-number wins; the Aggregator's cross-cluster verification ([Aggregator role](#aggregator-role-post-cluster-consolidation) step 3) resolves straddles.

**Atom → bundle map (quick):** A1+A2→A · B1+B2+B3→B · C1+C2→C · C3→D · D1→E · E1+E3→F · E2→G.

### Aggregator role (post-cluster consolidation)

A single sub-agent spawned per audit round AFTER all **K** seat Auditors return their partials (K ≤ 7 full partition, or the dirty-seat count on a dirty-only re-round). It is the journal's authoritative writer for the round — per-seat Auditors write disposable partials; the Aggregator alone writes the canonical journal sections. **Runs on the deep-workhorse tier (Opus / grok-4.5 / Sol; spawn `claude-d2-aggregator` / `grok-d2-aggregator` / `codex-d2-aggregator` per [harness-runtimes.md](harness-runtimes.md) + the [model policy](#sub-agent-model-policy-per-role))** for the deep bounded synthesis to consume K partials + do cross-cluster dedup + sister-sweep.

**Six responsibilities (in order):**

1. **Mechanical merge (full-catalog big table).** Read all K partials for the round (`r{N}-partial-{seat}-{name}.md` — bundles A–G, or Y subset). Seat partials are **seat-slice only**. The **canonical journal big table** under `## Latest sweep results` is always **one row per catalog §** (full rules catalog), sorted by § — including under Y / dirty-only / FR_LITE / FR_COLLAPSED. **Forbid** omitting non-Y §§. For non-dispatched seats on Y / FR_LITE / FR_COLLAPSED opens: Aggregator-synthesized `⚪ N/A` + closed reason-codes from the Y map (or lightweight N/A walk by those seats). Union Evidence ledgers (`E#`); on E# collision across seats, prefix by seat letter or renumber globally once. **On dirty-only re-rounds:** merge fresh dirty-seat (+ sister-blast) partials over the **prior clean seats' PASS/N/A rows** (lawful dirty-only re-cite per [§24.0e / §24.0f / §24.6](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — not silent inheritance outside dirty-only mode); re-walk clean seats only if the orchestrator re-dispatched full K / active Y. Anti-laziness preamble verbatim above the table. REPLACES the prior sweep's table (§24 sweep-replacement rule).
2. **Dedupe.** A finding surfaced by multiple Auditors collapses into one entry with combined provenance (all citation paths preserved).
3. **Cross-cutting verification.** Walk the deliverable's cross-step focus areas spanning multiple clusters (defined in the final-review journal's Plan section under FR_FULL/FR_LITE, or the step Plan under FR_COLLAPSED — e.g. "TYPE LIE FIX still verified end-to-end across .NET emitter + TS consumer", "wire-shape leakage class — does the originating sample class exist anywhere else in scope?", "spec-driven catalog parity — every cross-language wire identifier cataloged + parity-tested"). No per-seat Auditor could see these.
4. **Cross-cluster sister-sweep.** Seat Auditors sister-sweep WITHIN their §-scope (§24.13.3); the Aggregator runs sister-sweeps at CROSS-cluster scope — baseline commands in [§4 Cross-cluster sister-sweep checklist](#cross-cluster-sister-sweep-checklist-aggregator-baseline), run every round.
5. **Append findings log.** One `### Round N findings (<UTC>)` subsection under `## Sweep findings log (append-only)`: the consolidated finding list (steps 2-4), a closure-verification table for prior-round findings (CLOSED-by-absence in this round's big table OR STILL-PRESENT), and a regression-verification table (prior-round PASS rows spot-confirmed still PASS). Name dirty seats for the next re-dispatch.
6. **Return summary to orchestrator (short structured only).** Counts by severity (H/M/L), dirty seats, CLEAN? Y/N, 1–3 cross-cluster notes, recommendation (CLEAN → next phase, or findings → spawn Fixer with scope). **Do not** re-paste the big table into the orchestrator chat.

**Cannot:** flip a per-seat verdict unilaterally (add cross-cluster findings, yes; overrule an Auditor, no — escalate ties to the orchestrator for a tie-breaker Auditor); touch source / tests / configs (write access = journal + audit artifacts only); mark the step CLEAN (it RECOMMENDS clean; the big table must contain zero FINDING rows for CLEAN to be valid). **Why required:** with K>1 parallel Auditors no single Auditor sees the full picture — without an Aggregator the orchestrator would have to read all K partials (forbidden) or trust each slice without cross-validation (defeats the parallelism win). A multi-seat dispatch WITHOUT an Aggregator is incomplete; the round is not done until the `### Round N findings` subsection lands.

---

## 4. Audit-loop mechanics

The mechanical shape of every audit round. Predicate-of-record for evidence discipline: [rules.md §24 Audit Evidence Discipline](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

> ## ⚠️ MANDATORY ANTI-LAZINESS DIRECTIVE
>
> **DO NOT BE LAZY. WALK EVERY NUMBERED SUBSECTION IN rules.md. NO SKIPPING. NO ASSUMING IRRELEVANCE WITHOUT EVIDENCE. LEAVE NO STONE UNTURNED.**
>
> Short-circuiting the audit ("I checked the relevant ones, the rest don't apply") IS the failure mode this framework prevents. Most subsections WILL apply to most code. Be skeptical of your own urge to mark N/A. The audit table is the GATE: fewer rows than numbered subsections = INCOMPLETE; a "PASS" without a file:line = INCOMPLETE; an "N/A" without a step-scope-specific reason = INCOMPLETE. The cost of walking every predicate is minutes; skipping one is a future bug + audit round.

### Three-artifact journal model

> **Duplicated from [rules.md §24.0](rules/24-audit-evidence-discipline-meta-how-to-audit.md#three-artifact-journal-model-one-big-table--append-only-findings-log--append-only-fix-log) for process-protocol context — the canonical full version (all §24.0/§24.0a-h/§24.13.x predicates) lives in rules.md; update both in lockstep when either changes (per [rules.md §11.32](rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices)).**

Every step / final-review journal contains THREE artifacts under canonical headings — strictly separated, never collapsed:

| Artifact | Section heading | Behavior | Written by |
| --- | --- | --- | --- |
| **Big table** (latest sweep snapshot) | `## Latest sweep results` | REPLACED every sweep — reflects ONLY the most recent walk against current code. ~85+ rows, one per rules.md subsection. Anti-laziness preamble above it. | Sweep activity ONLY. Fix-applying agents NEVER touch this. Under multi-seat K the **Aggregator** (deep workhorse: Opus / grok-4.5 / Sol; `claude-d2-aggregator` / `grok-d2-aggregator` / `codex-d2-aggregator`) writes the merged table; per-seat Auditors write only their partials. |
| **Findings log** (per-round history) | `## Sweep findings log (append-only)` | APPEND-ONLY. Each sweep appends a `### Round N findings (timestamp)` subsection. Never deleted / re-ordered. | Sweep activity ONLY. Under multi-seat K the **Aggregator** writes the consolidated round subsection (K seats + cross-cluster). |
| **Fix log** (chronological fix activity) | `## Fix log (append-only)` | APPEND-ONLY. Each fix appends one entry: rules.md subsection + finding round + what changed + `file.cs:NN`. Never deleted / re-ordered. | Fix-applying agent ONLY. |

The big table is the canonical "what is true RIGHT NOW" snapshot — always **full-catalog** (one row per numbered subsection). Active / dirty seats contribute freshly walked PASS/FINDING. Under **dirty-only** re-rounds (default after findings), the Aggregator may **re-cite prior clean-seat PASS/N/A rows** into the replaced table via explicit dirty-only merge — lawful merge, **not** silent inheritance outside dirty-only mode (lockstep [rules.md §24.0e / §24.0f / §24.6](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)). **Closure is proven ONLY by zero FINDING rows on a full-catalog big table** from a real sweep. The fix log captures intent + action; it does NOT certify outcome.

### Mandatory round sequence

1. **Sweep**: walk every rules.md subsection against current code. REPLACE the big table. APPEND a `### Round N findings (timestamp)` subsection enumerating every FINDING.
2. **Fix work**: for each FINDING, apply the fix; APPEND one fix-log entry (rules.md subsection + finding round + what changed + `file.cs:NN`). **The big table is NOT touched between sweeps.**
3. **Sister-sweep mandatory** ([rules.md §24.13.3 / §24.13.3d](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — the Fixer dispatch brief MUST name the sister-sweep command + full applicability path-set + literal-output-paste requirement; the Fixer pastes literal stdout into the fix-log entry.
4. **Tamper-evident dispatch** ([rules.md §24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — when a finding previously claimed CLOSED resurfaced STILL_PRESENT, or is a user-flagged special-emphasis target, the Fixer brief MUST mandate BEFORE/AFTER literal-output pasting (predicate-grep + `git diff --stat`) — the four literal outputs become the fix-log entry's inline evidence.
4a. **Pattern-class scope expansion** ([rules.md §24.28](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — for any pattern-class violation (convention breach, leaked token, recurring anti-pattern), the Fixer brief MUST name the grep against the FULL deliverable diff scope + mandate fixing every instance, not only the cited file:lines. Partial fixes resurface as STILL-PRESENT.
4b. **Fixer self-grep before returning** ([rules.md §24.29](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — before returning, the Fixer runs `git diff HEAD` and greps its own added lines for new pattern-class instances + conversation-scope tokens / audit-process references / partial cross-links in doc edits; any self-introduced hit is fixed in-place. The fix-log entry includes a `"Self-grep"` section with command + literal output.
5. **Every finding gets fixed** — no silent carryover. If genuinely unresolvable this round, get EXPLICIT user permission to defer + append a deferral entry to the fix log (still append-only, never silent omission).
6. **Next sweep**: when all current-round findings have fix-log entries, run the NEXT sweep — default **dirty-only** seats with ≥1 finding (+ sister-blast), re-reading those seats' category files per the [K=7 reading lists](#auditor-cluster-partition-dual-mode); full K=7 only when justified (or when opening **FR_FULL**). REPLACE the big table; append `### Round N+1 findings`. A row that was a FINDING in Round N and is now PASS in Round N+1 = closed (proven by absence). Still a FINDING = fix didn't take; append more fix entries, run N+2.
7. **Loop terminates** when ONE sweep produces a big table with zero FINDING rows. No "convergence claimed" without a clean big table from a real sweep.

If iteration 11 is reached without convergence, STOP and escalate:

```
=================================================
[YYYY-MM-DD HH:MM] ESCALATION — 10-iteration ceiling reached
=================================================
Pattern of findings across rounds: <summary>
Suspected root cause: <agent's hypothesis>
Question for user: <specific ask>
```

### Plan currency before dispatch

> **Mid-deliverable architectural / scope / approach changes MUST update the deliverable's Plan synchronously — before the next sub-agent is dispatched. Conversation-only ("in MEMORY") decisions are explicitly INVALID as a state to dispatch from.**

The orchestrator carries conversation context across a deliverable; sub-agents do not — every sub-agent spawns fresh and reads ONLY the artifacts the brief names (journal, Plan file, rules.md, shared-context). The orchestrator's conversation memory is INVISIBLE to every sub-agent (the point of the fresh-context property, [§3](#why-this-is-structural-not-stylistic)), which is exactly why an architectural pivot living only in conversation makes the next sub-agent build against the OLD plan.

**The mandate** — any decision made DURING EXECUTE that contradicts / supersedes / amends the locked Plan MUST be written into the Plan artifacts before the next dispatch. This covers architectural pivots, naming changes, scope additions/removals, ordering changes, library-shape changes, decision reversals, cycle-resolution choices, and cross-cutting reminders that must fire at multiple later dispatch points — anything the next fresh-context sub-agent would otherwise build against a stale contract.

**The mechanism** — ALL THREE updates in the SAME orchestrator turn that locks the decision (not batched, not deferred to end-of-step):

1. **Journal amendment** — append `## Plan amendment N+1 (<UTC>)` to the step journal: (a) what changed, (b) what it supersedes/contradicts, (c) rationale, (d) user-quoted authorization if the decision required user permission per §13.5 / §13.14.
2. **Plan file update** — edit `docs/wip/<deliverable>/README.md` so the Living State / Status + relevant Step section + Cross-cutting decisions table all reflect the amended state; stale contradicting prose is removed / struck (future sub-agents must see ONE consistent state).
3. **Decisions table row** — append a row to the Cross-cutting decisions table citing the amendment number + choice + rejected alternatives + amendment-journal back-reference (`journal.md:NN`).

**The "before next dispatch" gate** — the orchestrator does NOT dispatch until all three land. Plan-currency verification is a precondition sitting ahead of every step in [Per-round dispatch protocol](#per-round-dispatch-protocol): if a brief is about to point at an out-of-date Plan, STOP, run the three-update mechanism, then write the brief against the AMENDED Plan.

**Failure mode this prevents** — a brief pointing at a stale Plan makes the Implementer build the OLD architecture (correctly — the Plan is the contract), cascading into a downstream Auditor finding + Fixer round + re-Implementer round, multiplied across every sub-agent that touched the stale Plan. Cost of the mechanism: one orchestrator turn (~minutes); cost of skipping: multiple wall-clock-hour re-cycles. *Canonical precedent: deliverable 0009-geo-libs Step 3a Plan amendment 41 — six architectural decisions locked in conversation while the Plan still described the pre-amendment architecture; a re-dispatch against the stale Plan would have rebuilt the prior split-shape with Option A naming; fixed via the three-update mechanism (referenced in [rules.md §24.17](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)).*

**Cross-references:** [rules.md §24.17](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (predicate enforcement) · [§13.5](rules/13-permission-action-discipline.md#13-permission--action-discipline) governs WHETHER to amend (the ASK gate), this section HOW to record it · [§13.13](rules/13-permission-action-discipline.md#13-permission--action-discipline) is the Implementer-side Plan-vs-reality reconciliation to this orchestrator-side currency gate · [§24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) audits the Plan ONCE at step entry; this keeps it honest thereafter.

### Dispatch-brief template

Every sub-agent dispatch brief follows one skeleton; roles differ only in the deltas below. Reused as the copy-paste source so briefs never restate boilerplate.

**Common skeleton (all roles):**

- **Role + seat** — the role + seat letter (if Auditor) + partial path. **Slim briefs:** briefs **MUST NOT** re-list the full path-set, re-paste pre-flight greps, or restate locked decisions already in shared-context — they name seat + partial path + "read `r{N}-shared-context.md`" + seat-only extras.
- **Reading list** — shared-context file first (SoT for scope / pre-flight / gates / mode), then journal Plan section, seat category files, rules.md index. A sub-agent reads ONLY what the brief names — it has NO conversation memory.
- **Spawn name + model + self-attestation** — dispatch the **active-runtime** pin only ([harness-runtimes.md](harness-runtimes.md)): IF Claude Code → `subagent_type: claude-d2-<role>` and pass `model` matching the pin; IF Grok Build → `subagent_type: grok-d2-<role>` and do not pass a separate model arg (`grok-4.5` frontmatter is the pin); IF Codex → `agent_type: codex-d2-<role>` and treat `.codex/agents/codex-d2-<role>.toml` (`gpt-5.6-sol` / `gpt-5.6-terra` + effort) as the intended pin **when the host applies it** (formal §24.0i-pinned waves only on hosts that honor pins — Claude/Grok until Codex pin application is proven; see harness-runtimes known limits). Every return summary MUST open with the model-attestation block (below).
- **Return format** — the structured summary shape for the role (files + tests + build state; or a partial big-table chunk; or the merged table + findings).
- **Journal-artifact requirement** — which of the three artifacts (big table / findings log / fix log) the role writes, if any: Auditors write disposable partials; the Aggregator writes the canonical big table + findings-log subsection; the Fixer writes fix-log entries; the orchestrator writes nothing domain-level.
- **Constraints** — READ-ONLY tools for Auditors / Aggregator; no nested sub-agent spawning; no commits; no touching another Auditor's partial.

**Model-attestation block (opens every return summary):**

```
Model: <runtime product model id — Claude e.g. claude-fable-5 / claude-opus-4-8 / claude-sonnet-4-6; Grok: grok-4.5 only; Codex: gpt-5.6-sol / gpt-5.6-terra when the host applies the role TOML pin — otherwise the actual child model>
Tier-override reason (if the pinned tier was overridden — e.g. an Implementer / Fixer escalated to planning tier / Fable): <criterion # + justification, verbatim from dispatch brief>
```

**Anti-laziness preamble (Auditor / Plan-Auditor / Final-reviewer briefs — verbatim, load-bearing):**

> WALK EVERY NUMBERED SUBSECTION in your cluster scope. NO SKIPPING, no assuming irrelevance without evidence. PASS rows require compact file:line (+ optional ≤8-word tag; essays illegal); N/A rows require a closed reason-code; FINDING rows require severity + file:line + description + fix; the Status column prepends a ✅ / ❌ / ⚪ / 🟡 emoji. Use Evidence ledger E# for commands once. Regex is a TOOL not source of truth (§24.13.2) — read the file. Sister-sweep at full predicate applicability (§24.13.3). The cost of walking a predicate is minutes; skipping one is a future bug + audit round.

**Shared-context reminders every Auditor / Final-reviewer brief carries** (predicate-of-record in parens):

- Read every modified `.cs` / `.ts` for the three tool-invisible lenses neither `dotnet build` nor `jb inspectcode` enforces: line length ≤ 100 + SA1519/SA1516 cascades; a blank line after any multi-line statement before the next statement; `var` for locals where the type is evident. Gate-green does NOT imply convention-clean. (§24.20)
- Gate-verify at FULL-solution scope: `dotnet build server/D2.slnx` (or the tests-csproj build) AND `jb inspectcode server/D2.slnx --severity=WARNING` — never a per-lib / per-project inspectcode (it hides test-file findings). (§24.21)
- Scan modified source xmldocs + `//` / `/* */` comments + `.csproj` XML comments (not just READMEs) for deliverable-step / phase / SHIP / forward-ref / rules-§ / AGENTS.md-§ framing. (§24.22)
- Read from the on-disk WORKING TREE, not `git diff HEAD` / `git show HEAD:` — the latest Implementer / Fixer output is uncommitted; a HEAD reader reports stale pre-change findings and misses post-change issues. (§24.19; omit once all step output is committed.)

**Per-role deltas:**

| Role | Model | Scope | Writes | Delta from skeleton |
| --- | --- | --- | --- | --- |
| **Planner** | Fable · max | one step | journal Plan section | Produce the Plan block (goal, files, decisions, pre-emptive gate checks); no audit artifacts. |
| **Implementer** | Opus (carve-out → Fable) | files-to-touch | source + tests | Write code + tests; run `check-baselines` if a consumable was touched; return the Implementation block. |
| **Auditor / Final-reviewer** | Sonnet (bundles C / D / G + ruling-critical → Opus via Auditor-deep) | one cluster's §-range | disposable partial | Carry the anti-laziness preamble + shared-context SoT (do not re-list scope); write the [3-layer partial-file template](#partial-file-template-per-auditor); Final-reviewer scopes to the whole deliverable under **FR_FULL** (full K=7) or **FR_LITE** (Y ⊆ K=7). |
| **Plan-Auditor** | Opus | Plan section, one cluster's §-range | disposable partial | Same partial shape scoped to the Plan section; verify the plan's claims against real code. |
| **Aggregator** | Opus | K partials + cross-cluster | full-catalog big table + findings-log subsection | Perform the responsibilities in [Aggregator role](#aggregator-role-post-cluster-consolidation) (full-catalog merge + short structured return); run the [cross-cluster sister-sweep baseline](#cross-cluster-sister-sweep-checklist-aggregator-baseline). |
| **Fixer** | Opus (carve-out → Fable) | consolidated finding list | own fix-log file | Apply fixes; sister-sweep + tamper-evident + pattern-class + self-grep per [round sequence](#mandatory-round-sequence) steps 3-4b; cannot mark CLEAN. |
| **Fixer-mechanical** | Sonnet / Grok 4.5 / Terra | enumerated mechanical finding list | own fix-log file | Apply behavior-preserving rewrites / re-points / renames / spelling / line-wraps; STOP and hand back for the Fixer (`claude-d2-fixer` / `grok-d2-fixer` / `codex-d2-fixer`) on judgment work. |
| **Plan-amender** | Fable | Plan-Audit finding list | journal Plan section + Plan-Audit fix log | Address each Plan-Audit finding; append Plan-Audit fix-log entries. |

### Per-round dispatch protocol

The orchestrator's workflow for one K≤7 + Aggregator audit round. Same shape for per-step rounds, final-review rounds, AND Plan-Audit rounds — the difference is **scope** and **mode** (full K=7 vs Y ⊆ K=7 vs dirty-only; FR_FULL / FR_LITE / FR_COLLAPSED): mid-step Plan-Audit / code-audit = step files; **FINAL-REVIEW** = whole deliverable under the selected FR mode ([Audit wave policy](#audit-wave-policy)); Plan-Audit = the journal's `## Plan` section + the codebase reality it claims to align with ([rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)), three-way open (Skip \| Y \| full K=7). After findings: **dirty-only** re-dispatch of seats with ≥1 finding (+ sister-blast) — not full K by default. **K=12 atomic dispatch is retired.**

**Plan-Audit specifics** (three-way — lockstep with [EXECUTE 1a](#execute) + [§24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) + [wave policy](#audit-wave-policy)): **Skip** when a narrow listed carve-out applies (cite which). **Y ⊆ K=7 + Aggregator** for pure-meta/docs that need Plan-Audit (default E+G; +B/+D by surface) — partials = \|Y\|; evidence does **not** require "7 bundle partials" when Y is authorized. **Full K=7 + Aggregator** for product §24.16 in-scope. When dispatching: write a `plan-audit-r{N}-shared-context.md` (same shape, mission-scoped to the Plan section + §24.16 seat verification questions + mode + Y), dispatch Plan-Auditors for the chosen seats on the **active pin-honoring host** (`claude-d2-plan-auditor` / `grok-d2-plan-auditor`; `codex-d2-plan-auditor` only when Codex applies TOML pins — inventory otherwise; Claude passes its pinned Opus model, Grok relies on frontmatter pins), then the Aggregator → `## Plan-Audit results` lands BEFORE the Implementer. On findings: Plan-amender → fresh **dirty-only** Plan-Audit Round N+1. Terminate on CLEAN → Implementer with the AMENDED Plan. K=1 Plan-Audit follows §24.0h.

**Step 1 — Orchestrator writes the per-round shared-context file** at `docs/wip/<deliverable>/<NN>-<step>/r{N}-shared-context.md` (or `final-review/r{N}-shared-context.md` under FR_FULL/FR_LITE). **Shared-context is SoT** for scope / pre-flight / gates / mode — briefs point at it; agents do not re-enumerate scope. **Mandatory sections:**

| Section | Content |
| --- | --- |
| **Mode** | `Plan-Audit \| Code-audit-R{N} \| FR_FULL \| FR_LITE \| FR_COLLAPSED` + active Y/dirty list |
| **Mission** | What this round audits and why |
| **Locked decisions** | So seats do not re-litigate |
| **Scope path-set** | Concrete paths or `git diff --name-only` recipe |
| **Special-emphasis** | User direction if any |
| **Active partition** | [K=7 bundles A–G](#auditor-cluster-partition-dual-mode) + dirty-seat list if re-round / Y list if targeted |
| **Pre-flight ledger** | Commands already run (for Auditor E# reuse; optional if seats re-run) |
| **Output format** | [3-layer Partial-file template](#partial-file-template-per-auditor) |
| **Aggregator merge note** | Full-catalog big table; non-Y = N/A-coded; short return |
| **Constraints** | Anti-laziness preamble + shared-context reminders (§24.19/§24.20/§24.21/§24.22) from the [Dispatch-brief template](#dispatch-brief-template) |

**Step 2 — Orchestrator dispatches K parallel Auditors in ONE message** (a single `Agent` / `spawn_subagent` / `spawn_agent` batch of K parallel invocations — full partition **K=7**, first code-audit **Y ⊆ K=7**, dirty re-rounds K = dirty seat count — each via **runtime-prefixed** pins only — mechanical: `claude-d2-auditor` / `grok-d2-auditor` / `codex-d2-auditor`; deep seats bundles **C/D/G** + ruling-critical: `claude-d2-auditor-deep` / `grok-d2-auditor-deep` / `codex-d2-auditor-deep`). **Slim brief:** seat + partial path + "read shared-context" + seat-only extras — **no** full path-set re-list, **no** grep re-paste. Each Auditor: read shared-context; read seat category files end-to-end (per the [K=7 reading lists](#auditor-cluster-partition-dual-mode)); skim other seats / the [index](rules.md) for cross-refs; walk YOUR seat against the scope; write the **3-layer partial**. Concurrent writes are safe (each Auditor owns its file). Run as background and let notifications return as each completes. **IF Claude Code:** every Auditor / Plan-Auditor invocation carries its pinned `model` explicitly — the pin file is authoritative. **IF Grok Build:** omit a separate model override; the runtime-prefixed frontmatter pin is authoritative. **IF Codex:** omit a separate model override and treat the role TOML as authoritative **only when the host applies that pin**; some builds treat spawn as label-only (child inherits parent model) and may cap concurrency (~4 slots) — **do not run formal §24.0i-pinned waves on Codex until pin application is proven** ([harness-runtimes.md](harness-runtimes.md) known limits); keep formal multi-seat waves on Claude or Grok. Every Implementer / Fixer dispatch escalated under the Sweeping carve-out MUST cite the triggering criterion in both the brief and the return self-attestation (predicate-of-record [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)).

**Step 3 — Orchestrator waits for all K partials.** When ALL K notifications return, dispatch the Aggregator with the list of partial paths — the orchestrator does NOT read partials directly.

**Step 4 — Orchestrator dispatches the Aggregator** (`claude-d2-aggregator` / `grok-d2-aggregator` / `codex-d2-aggregator`; deep-workhorse tier — Opus / Grok 4.5 / Sol; foreground OK, not parallelizable). Brief: read the K partials; read the deliverable's cross-cutting focus areas; perform the six responsibilities in [Aggregator role](#aggregator-role-post-cluster-consolidation); write the **full-catalog** canonical big table + `### Round N findings` subsection; return **short structured summary only** (counts, dirty seats, CLEAN?, 1–3 notes — not a big-table re-paste).

**Step 5 — Orchestrator routes on the recommendation:** **CLEAN** (zero FINDING rows + zero new cross-cluster findings) → advance to next phase (next step, or SHIP per FR mode). **FINDINGS present** → dispatch a fresh Fixer with the consolidated list; after it returns, dispatch round R+1 as a brand-new **dirty-only** (or full-K if justified) batch + brand-new Aggregator, fresh context across the board.

**Wall-clock:** a multi-seat batch's wall-clock is dominated by the slowest seat (typically G audit-meta, E docs, C arch, D security depending on scope), NOT the sum. K=7 lowers fan-out vs historical always-12 while keeping pure critical seats. 10-iteration ceiling per step (one iteration = one full round).

### Audit wave policy

**Mode before dispatch (required).** Journal or shared-context records, before any seat spawns: `Plan-Audit | Code-audit-R{N} | FR_FULL | FR_LITE | FR_COLLAPSED` + active Y/dirty list. Silent mode omission is incomplete process.

#### Wave matrix (open defaults)

| Work shape | Plan-Audit open | First code-audit open | Re-round after findings | FINAL-REVIEW open |
| --- | --- | --- | --- | --- |
| **Product step** (code/tests, §24.16 in-scope) | Full **K=7** (default); dirty-only after amender | **Y ⊆ K=7** journal-justified (default); full K=7 if multi-concern / security / wide blast | **Dirty-only** (+ sister-blast) | See FR mode selection |
| **Docs / pure-meta step** (multi-surface process/rules/skills/KEEP law — **not** skip) | **Y = E+G** (default) **+ Aggregator**; +B if convention/pin text; +D if permission-doc | **Y = E+G** (+B if pins) | Dirty-only | **FR_COLLAPSED** if 1-step pure-meta + README lock; else **FR_LITE** if gates pass else **FR_FULL** |
| **Trivial / Plan-Audit carve-out (Skip)** | **Skip** only when a **narrow listed** carve-out applies (**cite which**): trivial <5 net-new / no new types-patterns-public surface; Step 0 scaffolding; sub-dispatches under already-audited step Plan; optional **trivial single-KEEP-doc polish** only — **never** multi-surface pure-meta | Y by touch surface | Dirty-only | As above |
| **Post-ship polish / narrow fix deliverable** | Skip (if carve-out cites) **or** Y | **Y** | Dirty-only | Prefer **FR_LITE** if gates pass |

**Plan-Audit three-way:** Skip ≠ Y ≠ full K=7 (see [EXECUTE 1a](#execute) + [§24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)). Broad "pure-doc = skip" is **stale** for multi-surface process/rules pure-meta (those rows use **Y**, not Skip).

#### FR mode selection

| Mode | When | Dispatch |
| --- | --- | --- |
| **FR_FULL** | Default whenever FR_LITE / FR_COLLAPSED ineligible | Full **K=7** at **whole-deliverable** scope; own `final-review/journal.md` |
| **FR_LITE** | **All** eligibility gates pass (below) | Deliverable-scope **Y ⊆ K=7** + Aggregator; own FR journal; journal records mode + Y before dispatch |
| **FR_COLLAPSED** | Pure-meta **1-step** deliverable where step path-set ≡ deliverable; locked in deliverable README | **No** separate FR journal; step CLEAN multi-seat Y-audit is the deliverable gate; completeness FR boxes cite step journal + collapse; multi-seat Y ≠ K=1 |

**FR_LITE eligibility (ALL required):**

1. Every step already CLEAN with documented Y/full rounds.
2. No new multi-service wire / auth boundary / shared public API surface spanning steps without prior full-seat coverage.
3. Deliverable touch set is single-package **or** docs-only across packages **or** otherwise low cross-step integration risk (orchestrator cites why in shared-context).
4. Y for FR_LITE includes every bundle with applicability on the deliverable diff (no silent drop of a seat that owns touched surface).
5. Journal + shared-context record `FR_LITE` + Y map **before** dispatch.
6. User has not mandated `FR_FULL` for this deliverable.

#### Y map helpers (non-exhaustive)

| Touch surface | Include seats |
| --- | --- |
| Process / rules / skills / KEEP docs / verbiage | **E**, **G** |
| Agent pin frontmatter / naming conventions | **B** (+ E/G if docs) |
| Tests / correctness | **A** |
| PII / arch layers | **C** |
| Auth / permissions | **D** |
| Observability / config / codegen / temporal | **F** |
| Unsure | Full **K=7** |

**K=1:** still requires explicit per-round user permission (§24.0h). Dirty-only ≠ K=1. FR_LITE ≠ K=1. FR_COLLAPSED ≠ K=1.

**Coverage under Y:** seat **partials** = seat-slice; **canonical journal big table** = always one row per catalog §; non-dispatched seats = Aggregator-synthesized `⚪ N/A` + closed codes.

### Orchestrator verification of sub-agent outputs

> **Trust-but-verify discipline — the structural compensation for dispatching workhorse roles (Opus + Sonnet) per the [Sub-agent model policy per role](#sub-agent-model-policy-per-role) table. Predicate-of-record: [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).**

When the orchestrator dispatches workhorse sub-agents (Auditor / Auditor-deep / Final-reviewer / Aggregator / Implementer / Fixer / Fixer-mechanical / Investigator — Opus + Sonnet) it takes on additional verification responsibility — it cannot blindly accept a sub-agent's outputs as ground truth (the same as for Fable outputs). **The discipline is mandatory**: a workhorse dispatch without trust-but-verify follow-up is structurally weaker. Part of the Orchestrator's Fable context budget is reserved for it (part of why the Orchestrator role is Fable).

**Specific verification actions** (apply per dispatch type):

1. **Spot-sample partial evidence** (per multi-seat round) — random-sample 1-2 PASS rows per seat Auditor's partial; re-read the cited `file:line` to confirm the evidence is real (Auditors occasionally cite a line that no longer carries it, or synthesize an adjacent citation). ~K–2K sampling reads per round; cheap vs a missed FINDING.
2. **Re-run gate samples** (per Implementer / Fixer return) — occasionally re-run a build / test / grep the sub-agent claimed passed (reported zero-hit pre-flight greps, `dotnet build` if claimed warning-clean, `jb inspectcode` if claimed clean) — sub-agents occasionally report "build clean" against stale pre-edit state.
3. **Adversarial challenge on "all green" reports** — when a sub-agent reports zero findings, probe in the next dispatch ("did you exercise corner cases X / Y / Z?"), naming specific failure modes. "All green" without enumeration is the most common short-circuit; workhorse-tier returns are more prone to optimistic framing than Fable returns.
4. **Re-read changed files for high-stakes work** — security-touching (auth flows, JWT validation, secrets, IDOR-relevant resolvers), user-visible UI/UX (error messages, form validation, redirects), data-touching (migrations, dual-writes, rollbacks). Re-read the changed files directly; don't trust the summary alone. One Fable pass over a handful of files is dwarfed by a security regression / data-loss bug.
5. **Re-run verifying grep on Fixer BEFORE/AFTER claims** — tamper-evident dispatch ([rules.md §24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) requires literal BEFORE/AFTER grep + `git diff --stat`; re-run the verifying grep against current state (Fixers occasionally claim an AFTER-state that's adjacent-but-not-exact, e.g. zero hits from a regex typo not from the fix landing).
6. **Re-run environment-touching gate claims from a CLEAN state** ([rules.md §24.27](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — when an "all green" gate touches machine / OS / global state (OS trust store, installed trust roots, globally-installed tools, machine-wide config), independently re-run from a clean state (fresh checkout, clean container, or explicit teardown) before accepting convergence. A sub-agent's diagnostic work can mutate that state as a side effect (e.g. installing a trust root to make a handshake succeed, then reporting green) — the green is an artifact of the debugging environment, not the code. If the clean re-run fails, re-dispatch a Fixer to make the test self-provision its state via an isolated fixture ([rules.md §1.16](rules/01-test-discipline.md#1-test-discipline)).

**Dispatch-brief contracts that support trust-but-verify** — briefs to workhorse sub-agents EXPLICITLY DEMAND evidence-over-confidence: every PASS row cites file + line ([§24.2](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)); every FINDING cites the grep / check that surfaced it so the orchestrator can re-run it ([§24.4](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)); every "no bugs surfaced" return is challenged in the brief itself ("enumerate the failure modes you considered and ruled out"); every Fixer BEFORE/AFTER claim is tamper-evident (literal grep + `git diff --stat`, [§24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)); every return self-attests its model (per the [model policy](#sub-agent-model-policy-per-role) self-documentation requirement).

**When to escalate to full re-dispatch** — if verification surfaces that the output was substantially wrong (cited evidence doesn't exist, claimed-green gates fail, claimed-closed findings still present), re-dispatch a FRESH sub-agent (new context, possibly Fable-escalated under the Sweeping carve-out) with the verification findings as input. Do NOT prompt the same sub-agent to "fix the discrepancy" — its context is already polluted; fresh-context restart is correct. **Why structurally** — trust-but-verify closes the asymmetric risk of workhorse dispatch (real cost savings, but a quality floor depending on first-pass accuracy): verification reads are far cheaper than full Fable dispatch AND the quality floor is enforced by orchestrator spot-checks, so the combined economics dominate Fable-only dispatch for the high-volume workhorse roles.

**Cross-references:** [Sub-agent model policy per role](#sub-agent-model-policy-per-role) (which roles are workhorse) · [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) · [§24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (tamper-evident Fixer dispatch) · [§24.13.3](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (sister-sweep) · [§24.27](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (clean-env re-run) · [§24.28](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (pattern-class scope expansion) · [§24.29](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (Fixer self-grep).

### Cross-cluster sister-sweep checklist (Aggregator baseline)

The Aggregator MUST run the following baseline sweeps as part of step 4, regardless of what cluster Auditors found. Cluster Auditors sister-sweep WITHIN their §-scope (§24.13.3); the Aggregator's sweeps below run against the FULL DELIVERABLE DIFF SCOPE (typically `git diff --name-only nova` minus gitignored paths + `docs/dev/deliverables/` immutable snapshots).

| Sweep | Command (literal — substitute scope) | What it catches |
| --- | --- | --- |
| **Past-framing** (§11.19 / §11.20) | `grep -rEn 'previously\|formerly\|used to\|was consolidated\|migrated from\|prior versions\|Resolved the CRITICAL\|Fixed a latent' <scope>` | Historical-narration prose that drifted into KEEP docs / source comments across multiple clusters |
| **Forward-framing** (§11.28) | `grep -rEn 'will be\|going to\|upcoming\|planned\|pending\|awaiting\|transitional\|temporary\|eventually\|future-proof\|once X ships' <scope>` | Forward-framing prose describing what DOESN'T exist yet (KEEP docs describe current reality) |
| **Falsey/Truthy dogfood** (§5.1) | `grep -rEn 'string\.IsNullOrEmpty\|string\.IsNullOrWhiteSpace' <scope> --include='*.cs' \| grep -v '/Generated/' \| grep -v '/tests/'` | Hand-rolled null/empty checks where `Falsey()` / `Truthy()` applies |
| **Line-length** (§7.14) | `awk 'length > 100' <scope C# / TS files>` | Wide lines. **Em-dash byte-count awareness (§24.13.2)**: `awk length` measures BYTES, so em-dashes (3 bytes) inflate apparent length — re-confirm borderline hits by visual character count |
| **Hand-mirrored cross-language constants** (§11.30) | Manual: wire identifiers (header names, error codes, JSON property names, OTel tag names) appearing as string literals in BOTH .NET and TS source in scope, where a spec catalog should own them | Cross-language wire identifiers hand-duplicated instead of spec-cataloged + emitter-generated |

**Operating rules:** always full-diff scope, never narrowed (the Aggregator catches what fell between cluster boundaries). Paste literal command + output into the `### Round N findings` subsection under `#### Aggregator cross-cluster baseline sweeps` — zero hits = one line per sweep; non-zero = each a consolidated finding (severity + file:line + description + fix), classified per §24.13.3a. Augment, don't replace — the Aggregator MAY add deliverable-specific sweeps from the final-review Plan's cross-step focus areas; new recurring classes feed back into this table via the deliverable's distillation.

### K=1 carve-out usage policy

K=1 single-Auditor dispatch is a possible option for truly tiny scope (one-line config tweak / typo fix), but **the orchestrator MUST NEVER self-invoke K=1 without explicit per-round user permission.** Canonical predicate-of-record: [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). The "narrow scope" / "tamper-evident proof exists" / "mechanical change" / "I already verified the fix" reasoning patterns are NOT valid self-justifications — they are exactly the cheating failure mode this framework prevents (per [AGENTS.md MANDATORY block 0](../../AGENTS.md#mandatory-block-0-orchestrator-only-main-thread): "The ONLY bypass is an explicit user request"). **If you think K=1 is appropriate, ASK the user first** — write the proposed justification (scope, why partitioning offers no parallelism win, what coverage guarantees you forfeit) and wait for explicit `K=1 approved` before dispatching. Without that, default to the **multi-seat K=7 partition** (full K=7 / Y ⊆ K=7 / dirty-only) — every round, no exceptions.

**Dirty-only is NOT K=1.** Re-dispatching only dirty seats from the active partition (bundles with ≥1 finding + sister-blast) remains multi-angle coverage for those §-ranges + Aggregator sister-sweep — it does **not** require §24.0h approval.

**How to apply:**

1. **Default**: every audit round dispatches K≤7 (full K=7, Y ⊆ K=7, or dirty-only on re-rounds) per [Per-round dispatch protocol](#per-round-dispatch-protocol) step 2. No self-justification for collapsing to a single Auditor. **FR_FULL** opens at full K=7; **FR_LITE** opens multi-seat Y + Aggregator (not K=1); **FR_COLLAPSED** still requires multi-seat Y on the step audit (not K=1).
2. **K=1 candidate**: the orchestrator writes a proposed-K=1 message enumerating (a) exact scope, (b) why partitioning offers no parallelism win, (c) forfeited coverage guarantees (which seat perspectives won't be exercised), (d) why those forfeitures are acceptable for this scope.
3. **User approval**: explicit `K=1 approved` per occurrence — approvals do NOT carry forward.
4. **Without explicit approval**: dispatch multi-seat K≤7, even if K=1 was discussed or the prior round was K=1-approved.
5. **Verification rounds after Fixer** — an especially-tempting target for K=1 rationalization ("tamper-evident proof shows the change landed; I just need to confirm closure"). Post-Fixer verification rounds default to **dirty multi-seat** (or full K=7 if blast is broad); the Fixer's tamper-evident output speeds each seat Auditor's verification but does NOT eliminate independent angles + cross-cluster sister-sweep. Collapsing to one Auditor still needs §24.0h.

*Empirical (why codified): deliverable 0008-geo-data-pipeline final audit cycle (under K=5; today's baseline is K=7 max — the lesson applies to any K>1 baseline). After a K=5 batch + Fixer, the orchestrator self-invoked K=1 reasoning "changes are narrow and tamper-evident proof exists." That K=1 round surfaced 2 brand-new findings (a §14.3 conversation-scoped ID + §7.14 line-length residuals, both introduced by the Final Fixer's new test file) AND a §24.0 process gap (missing fix-log entries); the required K=5 re-dispatch then surfaced ONE FURTHER finding the K=1 missed (a cross-doc Tier-3 contradiction). Net: the self-invoked K=1 cost extra verification + re-dispatch rounds plus a process-integrity breach the user called out. Different cluster angles + cross-cluster sister-sweeps reveal what single-Auditor walks structurally cannot; collapsing to K=1 collapses the coverage guarantee.*

### Partial-file template (per Auditor)

Every seat Auditor writes a **3-layer partial** (coverage attestation + evidence ledger + rows). Seat ∈ {A…G}. **Partials are seat-slice** (one row per numbered subsection in that seat's partition). The Aggregator merges into a **full-catalog** journal big table. The orchestrator includes this template in shared-context so all K produce consistent mergeable output.

```markdown
# R{N} Partial — Seat {SEAT}: {Seat name}

**Auditor**: …
**Seat / atoms**: …
**Mode**: Code-audit-R{N} | Plan-Audit-R{N} | FR_*   (copy from shared-context)
**Sweep UTC**: …
**HEAD note**: working tree (uncommitted) | …

## 1. Coverage attestation
- Seat §§ walked: <list or "all numbered in <category files>">
- Row count: N (must equal seat numbered-subsection count)
- Scope path-set: see shared-context (do not re-enumerate unless delta)
- Anti-laziness: applied

## 2. Evidence ledger (commands once)
| E# | Command or read | Literal result (trimmed; keep enough to re-run) |
| --- | --- | --- |
| E1 | `rg …` | `0 matches` / paste |
| E2 | Read `process.md:L..` | confirmed heading X |

## 3. Rows (reference E#; no command re-paste)

> Anti-laziness preamble (verbatim from §24): WALK EVERY SUBSECTION in your seat scope.
> PASS rows require file:line citations. N/A rows require closed reason-codes.
> FINDING rows require severity + file:line + description + fix. Status column prepends
> ✅ / ❌ / ⚪ / 🟡 emoji indicator. NO SHORTCUTS. Per rules.md §24.13.2: regex is a TOOL not source
> of truth — manual reading required. Per rules.md §24.13.3: sister-sweep at full predicate applicability.

| § | Predicate | Status | Evidence |
| --- | --- | --- | --- |
| 11.1 | … | ✅ PASS | process.md:NN · tag · E2 |
| 1.1 | … | ⚪ N/A | META |
| 24.2 | … | ❌ FINDING-MEDIUM | process.md:NN — defect → fix |

## Seat findings
…

## Cross-cluster handoffs
…
```

### Why the table is sweep-only-replaceable

If a fix-applying agent could flip a row to PASS, the failure mode: fix doesn't actually take (typo, wrong line, partial replacement, cascade) → agent writes PASS anyway → next sweep "trusts" the PASS and skips re-walking dirty work → bug ships. Sweep-only-replacement bans **Fixer / silent** row flips and bans **silent PASS inheritance** outside Aggregator merge. **Dirty (+ sister-blast) seats always re-walk** their partitions independently. Under **dirty-only** re-rounds (default after findings), the Aggregator may **re-cite prior clean-seat PASS/N/A** into the replaced **full-catalog** table via **explicit dirty-only merge** only — lawful merge, not silent inheritance ([§24.0e / §24.0f / §24.6](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)). Full K / active Y / FR_FULL opens still require freshly walked active seats. **CLEAN** = zero FINDING rows on the full-catalog table every round.

### Why findings + fixes are append-only

The append-only logs preserve the audit trail table-replacement would lose. Anyone reading the journal can answer "what did Round 1 find? what changed in response? did Round 2 confirm closure?" An agent that could delete entries could quietly hide reversals — append-only forces every change (including reversals) into chronological visible order. Every round produces a STRUCTURED TABLE with one row per numbered subsection; the table is the gate — a step is not done until a complete-table round shows zero FINDING rows.

### Evidence requirements (mechanical, no exceptions)

> **Compact evidence is REQUIRED** — coverage is mandatory; verbosity is not evidence. Predicate teeth: [rules.md §24.2 / §24.3 / §24.4 / §24.10](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). Process holds the **canonical anti-pattern list + N/A code table** (SoT); §24 thin-cites here (§11.32 annotated duplication). Update both in lockstep when either changes.

#### Status + cell rules (canonical big table — Aggregator merge target)

| Status | Required cell content |
| --- | --- |
| `✅ PASS` | `path:line` (+ optional ≤8-word tag). May add `· E#` if ledger backs the claim. **No essays.** Essay PASS = incomplete evidence under **§24.2** (primary). |
| `⚪ N/A` | Closed reason-code (table below) or `CODE — short scope note` or `OTHER: <one sentence>`. |
| `❌ FINDING-{HIGH\|MEDIUM\|LOW}` | severity (in status) + `file:line` + defect + fix (all four). Compact but complete — not one word. |
| `🟡 …` | rare; still needs reason |

**Coverage rule:**

| Artifact | Row scope |
| --- | --- |
| **Seat partial** | Seat-slice only: one row per numbered subsection in that seat's partition. Fewer than seat numbered §§ = INCOMPLETE. |
| **Canonical journal big table** (Aggregator merge target) | **Always one row per catalog §** (full rules catalog), including under Y / dirty-only / FR_COLLAPSED. Non-dispatched seats: Aggregator-synthesized `⚪ N/A` + closed codes. **Forbid** omitting non-Y §§. |
| **FR_COLLAPSED / Y step audits** | Active seats *walk* their §§; non-active §§ appear as N/A-coded rows — not absent. |

Pre-flight greps live in the **Evidence ledger once** (`E#`); rows cite `E#` — do not re-paste full stdout per row. **Ledger alone ≠ PASS** (still need a row with `file:line` and/or `E#`).

Anti-laziness preamble **verbatim** above every partial chunk and every Aggregator big table. Dropping rows to "save tokens" remains illegal.

**MANDATORY: emoji-prefixed Status column** — every Status cell starts with a canonical emoji: `✅ PASS` / `⚪ N/A` / `❌ FINDING-HIGH` / `❌ FINDING-MEDIUM` / `❌ FINDING-LOW` / `🟡 <anything-else>` (e.g. `🟡 DEFERRED`). Bare status word (no emoji) = §24.10 violation.

#### Closed N/A reason-code set (canonical home — this section; §24.3 links)

| Code | When true for this step's authored surface |
| --- | --- |
| `NO_CS` | No C# production/test source modified |
| `NO_TS` | No TypeScript / Svelte / JS modified |
| `NO_TEST` | No test files modified |
| `NO_DI` | No DI / composition-root registration changes |
| `NO_PII` | No logging, redaction, or PII-bearing types |
| `NO_SEC` | No auth, secrets, endpoints, permission surfaces |
| `NO_DATA` | No EF, DB, migrations, persistence |
| `NO_CACHE` | No cache surfaces |
| `NO_MQ` | No messaging / broker surfaces |
| `NO_I18N` | No i18n / TK / locale catalogs |
| `NO_GEN` | No codegen, proto, spec, generated outputs |
| `NO_TIME` | No temporal / clock / NodaTime surfaces |
| `NO_CFG` | No Options / configuration plumbing |
| `NO_MOVE` | No renames / moves / index-sensitive path fixes |
| `META` | **Product** predicate has no authored product surface because this step is pure process/rules/skills/agent-brief meta (no product runtime) |
| `OTHER` | Free-text one sentence when no code fits (must be step-specific) |

Codes may combine: `NO_CS+NO_TS` or list primary only. **Invalid:** bare `N/A`, "doesn't apply", "irrelevant", inherited from prior step without re-check.

**META / surface-code discipline:** `META` and surface codes apply only to **product** predicates with no authored surface on this path-set. **§24.0–§24.x journal-discipline rows remain in-scope** for every audit that writes a journal — including pure-meta deliverables. **Invalid:** `24.2 ⚪ N/A META` because "meta deliverable". **Valid:** `1.1 ⚪ N/A META` / `NO_CS` when no product tests exist.

#### Explicit anti-patterns (canonical SoT — this section; §24.2 thin-cites)

1. Dropping rows from the **canonical journal big table** (or from seat partials relative to seat §§) to "save tokens".
2. PASS with prose and no `file:line` (essay PASS).
3. Silent PASS inherit / Fixer flip / re-cite **outside** dirty-only Aggregator merge (lawful **explicit dirty-only re-cite** of prior clean seats is permitted; dirty seats always re-walk).
4. Pre-flight ledger presented as the audit without per-§ rows (ledger alone ≠ PASS).
5. N/A without a valid code / OTHER sentence.
6. Briefs re-dumping shared-context greps into every seat prompt.
7. Silent K=1 (still §24.0h).
8. Using `META` to N/A **§24 journal-discipline** rows on meta deliverables.

```
| §    | Predicate                                         | Status            | Evidence / Reason / Finding                              |
|------|---------------------------------------------------|-------------------|----------------------------------------------------------|
| 1.1  | Test every public path first-pass                 | ✅ PASS           | tests/Jwks/HttpJwksProviderTests.cs:23 · E1 |
| 1.2  | Adversarial inputs in tests                       | ❌ FINDING-MEDIUM | tests/Jwks/HttpJwksProviderTests.cs:1 — missing oversized → add test_OversizedJwks |
| 1.3  | DI extensions tested via composition resolution   | ⚪ N/A            | NO_DI |
```

### Loop count expectations

Canonical: [rules.md index — Loop count expectations](rules.md#loop-count-expectations) (well-planned step 1-3 rounds; complex/poorly-planned 5-8; final-review typically 1-2). 10-iteration ceiling per step ([Mandatory round sequence](#mandatory-round-sequence)); iteration 11 = escalate — something is structurally wrong.

---

## 5. Self-improvement loop

The `rules.md` catalog grows over time. Every deliverable's distillation produces proposed predicate additions; approved additions land in `rules.md`. Over time the catalog approaches "every kind of miss we've ever made has a corresponding gate-check," and the audit loop converges in fewer rounds because predicates fire pre-emptively (the agent sees the predicate during PLAN's pre-emptive gate checks and avoids the miss in the first place).

**Per-step distillation** (after each step's audit terminates CLEAN) — the orchestrator spawns a fresh sub-agent to append to the step journal:

```
=================================================
[YYYY-MM-DD HH:MM] Per-step distillation — kinds of misses
=================================================
Misses surfaced this step (by category):
  - Category 1 (Test Discipline): N findings across N rounds
    Pattern: <what the misses had in common, e.g. "DI extensions shipped without resolution-smoke tests">
  - Category 5 (Convention Adherence): N findings across N rounds
    Pattern: <e.g. "hand-rolled string null+empty checks instead of Falsey">
  ...

Candidates for new rules.md predicates:
  - <proposed predicate> — origin: round N, finding M
  - ...
```

These candidates surface in the root README's Kinds-of-misses log so they're visible across steps.

**At SHIP** (FR-mode-aware — see [SHIP](#ship-handoff-to-user-review)): under **FR_FULL / FR_LITE**, after final-review's clean termination, aggregate proposed rule additions from all **step + final-review** distillations; under **FR_COLLAPSED**, after step CLEAN Y-audit + completeness, aggregate from **step** distillations only. Then: deduplicate; present the full list to the user in the root README; user approves / tweaks / rejects each; approved proposals land in `rules.md` as a committed change before the deliverable's code commit.

**Format for proposing a new predicate** (in the root README "Proposed rule additions to rules.md" section):

```
Category: <existing category number + name, or "NEW: <name>">
Predicate: <Y/N question with required evidence>
Origin:    <which deliverable / step / round surfaced the underlying miss>
Why permanent: <not a one-off; class of miss that will recur without a gate-check>
Examples:  <1-2 specific past instances>
```

Approved proposals get appended to `rules.md` as part of ship's commit batch. Rejected proposals (one-off mistakes not worth a permanent rule) get noted in the deliverable's final report so the reasoning survives.

---

## 6. Appendices

### Appendix A: How this addresses each empirical failure mode

| Failure mode (observed in 0002-auth-inbound) | How the framework prevents |
| --- | --- |
| Prose-as-evidence drift | rules.md §24.2 / §24.3 / §24.4 evidence-form predicates + Auditor adversarial framing. |
| Convergence illusion | Orchestrator never marks CLEAN — Auditor does. Fresh sub-agents have no investment in stopping. |
| Stale-memory shortcuts | Sub-agents have fresh context — no conversation summary to trust. |
| Scope-narrowing | rules.md §24.13 pre-flight greps + §24.9 anti-laziness preamble. |
| Self-review leniency bias | Auditor is a separate sub-agent invocation, not the main thread. Adversarial prompt framing. |
| Mid-execution tier audits adding cycles without value | Tier audits removed. Per-step audit suffices because Auditor scope includes all files the step touched (incl. prior-step files if modified). Per rules.md §24.7. |
| Implementer self-marking findings as fixed | Fixer is a separate role; cannot mark CLEAN; closure proven by the next round's verifier (per rules.md §24.0b, fixes are recorded EXCLUSIVELY in the append-only fix log, never as big-table edits). |

### Appendix C: Trial outcomes from deliverable 0002-auth-inbound

The orchestrator + adversarial sub-agent separation pattern (§3) was trialed across 0002-auth-inbound (8 steps + final-review + 3 polish rounds) before promotion to canonical status. Empirical outcomes that justified it:

- **Two production bugs caught that single-context implementation would have shipped** — (1) `JwtAuthInterceptor.ResolveMethodScopeMetadata` reading the wrong `UserState` slot, caught by an integration test the Implementer skipped as "thin glue" that a Fixer was forced to author; (2) `MalformedActorChainException` propagating uncaught from `ClaimsToContextMapper.Map` so JwtValidator returned UnhandledException-shaped failures instead of the canonical `act_chain_malformed` code — helper + constant + xmldoc + README all existed, but the validator never emitted the outcome; caught only by the deliverable-wide "documented vs actually emitted" enumeration a per-step Auditor structurally couldn't see.
- **Convergence in 1-3 rounds (mostly 2)** across all 8 steps; the 10-iteration ceiling was never approached.
- **Main-thread context stayed small** across the whole deliverable — domain detail lives in sub-agent contexts that die on return.
- **User feedback:** *"the subagents, while slower to complete work, are actually doing a cleanly better job."* The wall-clock overhead is real, but the production-bug-catch rate dominates.

Promotion to canonical removes the per-deliverable "should we use sub-agents this time?" decision and makes fresh-context adversarial separation the default execution shape.
