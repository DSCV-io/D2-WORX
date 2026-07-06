<!--
Copyright (c) DCSV. All rights reserved.
-->

<a name="top"></a>

# D2-WORX Process — phase lifecycle + orchestrator-only main thread + audit-loop mechanics

The single source of truth for HOW work moves through D²-WORX — phase lifecycle (PLAN → EXECUTE → FINAL-REVIEW → SHIP → REVIEW), permission gates (when to pause for the user), sub-agent architecture (orchestrator + worker roles), audit-loop mechanics (K=12 cluster partition + Aggregator), and self-improvement loop (distillation → rules.md additions).

Predicate-level enforcement lives in [rules.md](rules.md); pattern reference lives in [../PATTERNS.md](../PATTERNS.md); CLAUDE.md is the agent-directive root that condenses this doc + rules.md for fast-access mental-model purposes.

> **Read [rules.md](rules.md) end-to-end at the start of every deliverable's PLAN phase.** It is the central requirements catalog — security, race conditions, naming, object disposal, D2Result usage, OOTB shared-lib tooling, logging, PII redaction, graceful degradation, UX, DX, observability, idempotency, configuration, and more. Knowing the rules upfront is what lets you write code that passes audit round 1 instead of round 5.

> **Author note**: this framework is designed for THE AGENT to find reliable to follow — it is designed for agent ergonomics first, human readability second.

## Table of contents

1. [Phase lifecycle](#1-phase-lifecycle)
   - [Glossary](#glossary)
   - [Folder shape](#folder-shape)
   - [PLAN](#plan)
   - [EXECUTE](#execute)
   - [FINAL-REVIEW](#final-review)
   - [SHIP](#ship-handoff-to-user-review)
   - [REVIEW](#review-user-phase)
   - [Append-only discipline](#append-only-discipline)
   - [Scope of work shape](#scope-of-work-shape)
   - [What this process does NOT do](#what-this-process-does-not-do)
   - [When to invoke this process](#when-to-invoke-this-process)
2. [Permission gates (when to pause for the user)](#2-permission-gates-when-to-pause-for-the-user)
3. [Sub-agent architecture (orchestrator + worker model + roles)](#3-sub-agent-architecture)
   - [Why this is structural, not stylistic](#why-this-is-structural-not-stylistic)
   - [Allowed in main-thread context](#allowed-in-main-thread-context)
   - [Forbidden in main-thread context](#forbidden-in-main-thread-context)
   - [Canonical sub-agent roles](#canonical-sub-agent-roles)
   - [Sub-agent model policy per role](#sub-agent-model-policy-per-role)
   - [Every round = a NEW fresh sub-agent](#every-round--a-new-fresh-sub-agent)
   - [The orchestrator cannot mark CLEAN](#the-orchestrator-cannot-mark-clean)
   - [Auditor cluster partition (canonical K=12)](#auditor-cluster-partition-canonical-k12)
   - [Aggregator role (post-cluster consolidation)](#aggregator-role-post-cluster-consolidation)
4. [Audit-loop mechanics](#4-audit-loop-mechanics)
   - [Three-artifact journal model](#three-artifact-journal-model)
   - [Mandatory round sequence](#mandatory-round-sequence)
   - [Plan currency before dispatch](#plan-currency-before-dispatch)
   - [Per-round dispatch protocol](#per-round-dispatch-protocol)
   - [Orchestrator verification of sub-agent outputs](#orchestrator-verification-of-sub-agent-outputs)
   - [Cross-cluster sister-sweep checklist (Aggregator baseline)](#cross-cluster-sister-sweep-checklist-aggregator-baseline)
   - [K=1 carve-out usage policy](#k1-carve-out-usage-policy)
   - [Why the table is sweep-only-replaceable](#why-the-table-is-sweep-only-replaceable)
   - [Why findings + fixes are append-only](#why-findings--fixes-are-append-only)
   - [Evidence requirements (mechanical, no exceptions)](#evidence-requirements-mechanical-no-exceptions)
   - [Loop count expectations](#loop-count-expectations)
5. [Self-improvement loop](#5-self-improvement-loop)
6. [Appendices](#6-appendices)
   - [Appendix A: How this addresses each empirical failure mode](#appendix-a-how-this-addresses-each-empirical-failure-mode)
   - [Appendix B: Mapping to Anthropic's five workflow patterns](#appendix-b-mapping-to-anthropics-five-workflow-patterns)
   - [Appendix C: Trial outcomes from deliverable 0002-auth-inbound](#appendix-c-trial-outcomes-from-deliverable-0002-auth-inbound)
   - [Appendix D: Research references](#appendix-d-research-references)

> ## ⚠️ MISSION CONTEXT
>
> **D²-WORX is being built as an enterprise-level, production-ready, robust SaaS framework.** This process exists to enforce that standard at the process level. The PLAN phase locks design rigor; the EXECUTE phase locks autonomous convergence on quality; the REVIEW phase preserves architectural feedback. Every loop, every audit, every artifact is in service of shipping production-ready code without requiring the user to push the agent through bug-hunting cycles.

> Companion docs:
>
> - [rules.md](rules.md) — the central, verbose, authoritative requirements catalog. Read end-to-end during PLAN; walk during EXECUTE audit loop and final-review.
> - [deliverables/](deliverables/README.md) — surviving root READMEs for shipped deliverables (lessons learned + final report). Committed.

<sup>[↑ jump to top](#top)</sup>

---

## 1. Phase lifecycle

Three phases: **PLAN → EXECUTE → REVIEW**, with a deliverable-wide **FINAL-REVIEW** sub-step before **SHIP** hands off to the user's REVIEW. Convergence is autonomous — the **main-thread orchestrator** spawns fresh sub-agents for every round of planning, implementation, auditing, and fixing, and loops until each step's audit terminates clean, then ships to user review.

### Glossary

- **Deliverable** — a coherent unit of shipped work (one feature, one library set, one cross-cutting refactor). Has a name, a branch, a folder under `docs/wip/<deliverable>/`, and a final committed report at `docs/dev/deliverables/<deliverable>.md`.
- **Step** — one project's worth of work within a deliverable. Default unit is one `csproj` (or one logical bundle for non-csproj work like docs / config / SvelteKit features). Steps have order and may declare prerequisites on earlier steps.
- **Audit round** — one pass through every category in `rules.md`, producing per-predicate evidence. Findings are fixed inside the same round; the round ends, the next round runs against the post-fix state.
- **Clean round** — an audit round that produces zero findings across every category. The termination signal.
- **Iteration ceiling** — 10 audit rounds per step (and 10 at final review). Hitting 11 means escalate to the user; the agent's mental model is wrong, not its execution.
- **Self-improvement** — at each step's audit termination AND at deliverable ship, the agent distills the kinds of misses surfaced into proposed additions to `rules.md`. User approves; rules are appended; future deliverables start with a stricter ruleset.
- **Orchestrator** — the main-thread agent. Decision-making + delegation only. Cannot edit / write / read source code; cannot walk `rules.md`; cannot mark anything CLEAN. Spawns sub-agents for everything domain-level.
- **Sub-agent** — a fresh-context worker spawned via the `Agent` tool for one specific role (Planner / Implementer / Auditor / Aggregator / Fixer / Final-reviewer). Returns a structured summary; its context dies on return.
- **Cluster** — one of twelve thematic groupings of `rules.md` predicates (A1: tests/coverage, A2: regression/races/disposal/degradation/idempotency, B1: C# conventions, B2: TS conventions + naming, B3: shared-lib + D2Result, C1: PII/logging + operations, C2: architectural layer, C3: security + permissions, D1: KEEP doc parity, D2: i18n + no-phase verbiage, E1: UX/DX/observability/config, E2: audit-meta/temporal/codegen). The canonical partition for K=12 parallel Auditor dispatch lives in [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k12).
- **Audit round (K=12)** — one full audit pass = 12 parallel cluster Auditors + 1 Aggregator + (if findings) 1 Fixer. The default unit of audit work; sequential K=1 is a carve-out requiring explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

<sup>[↑ jump to top](#top)</sup>

### Folder shape

```
docs/
├── dev/
│   ├── process.md                          ← this file (committed)
│   ├── rules.md                            ← the rule catalog (committed)
│   └── deliverables/                       ← surviving root READMEs (committed snapshots)
│       ├── README.md                       ← what lives here
│       ├── 0001-auth-outbound.md
│       └── 0002-handler-stack.md
└── wip/                                    ← gitignored; per-deliverable local workspace
    └── NNNN-<deliverable>/                 ← 4-digit deliverable index, e.g. `0001-shared-libs-review/`
        ├── README.md                       ← progress tracker + final report (snapshot copied to deliverables/ at ship)
        ├── 01-<step-name>/
        │   └── journal.md                  ← append-only; LOCAL-ONLY, never committed; never auto-deleted
        ├── 02-<step-name>/
        │   └── journal.md
        ├── ...
        └── final-review/
            └── journal.md
```

**Naming convention**: deliverables use a 4-digit index prefix (`0001-`, `0002-`, ...) so they sort naturally in directory listings and the order reflects ship sequence. Both the local workspace folder (`docs/wip/NNNN-<name>/`) and the committed snapshot (`docs/dev/deliverables/NNNN-<name>.md`) share the same index — matching prefixes make it trivial to find the local workspace for a past committed snapshot (if the journals still exist locally). Pick the next free index at PLAN time by `ls docs/dev/deliverables/` + incrementing the highest.

At SHIP, **only the root README is copied** out of `wip/NNNN-<name>/` to `docs/dev/deliverables/NNNN-<name>.md` (committed snapshot — single file). The per-step journals stay where they are in `docs/wip/NNNN-<name>/` — gitignored, local-only artifacts. They are NEVER auto-deleted by the process; the user removes them manually whenever they want. Locally-preserved journals remain available as evidence that future deliverables can spot-check, but they don't cross the commit boundary — only the distilled README does.

<sup>[↑ jump to top](#top)</sup>

### PLAN

The user and agent reach alignment on what's being built. Output: a fully-populated `docs/wip/<deliverable>/README.md` plus the empty step folders.

**Steps:**

0. **READ [rules.md](rules.md) END-TO-END.** Mandatory before any other PLAN activity. The catalog is the requirements you'll be held to during EXECUTE — knowing them upfront is what lets you write code that passes the audit on round 1 instead of round 5. Skipping this step means architectural mistakes get baked in at design time; "I'll just check the rules during audit" is what creates multi-pass loops.
1. **Discuss + lock high-level goal.** Loop until the user agrees on what success looks like. The agent captures this as the first journal entry under the soon-to-be-created `docs/wip/<deliverable>/`.
2. **Create the deliverable workspace.** `docs/wip/<deliverable>/README.md` is created with the populated tracking sections (see template below). Each step gets a numbered folder (`01-<short-name>/`, `02-<short-name>/`, etc.) with an empty `journal.md`.
3. **Break into steps.** A step = one csproj or equivalent shippable bundle. Loop with the user until step list + ordering + prerequisites are agreed.
4. **Lock detailed design per step.** Discuss trade-offs, alternatives considered, layer choices (which ctor, which interface, which transport). Document the rejected alternatives — these are the most valuable thing the journal carries forward when architectural mistakes at design time need to be diagnosed later.
5. **Risk pass — walk every rules.md category against the design.** Security, race conditions, PII, graceful degradation, layer hygiene, observability, idempotency, configuration, failure modes. For each category, ask: "what predicates apply to this design? does the design satisfy them upfront?" Refine the design. Loop until agreed.
6. **PLAN exit.** Root README has populated step list + cross-cutting decisions + open questions = empty. Step folders exist with empty journals. Agent has confirmed end-to-end read of rules.md in the journal. Agent now enters EXECUTE.

**`docs/wip/<deliverable>/README.md` template (initial form, populated during PLAN):**

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
- ⏸  03-<step-name>    (prereqs: 01, 02)
- ...
- ⏸  final-review

## Cross-cutting decisions (during PLAN)
- <decision>: <choice> — alternatives rejected: <list, why>
- ...

## Open / escalated to user
- (none) | <question, blocked since YYYY-MM-DD>

## Kinds-of-misses log (populated during EXECUTE per-step + final-review)
<empty initially; grows append-only>

## Proposed rule additions to rules.md (populated at ship)
<empty initially; finalized at final-review termination>
```

<sup>[↑ jump to top](#top)</sup>

### EXECUTE

For each step in order (respecting prerequisites), the **main-thread orchestrator** drives the per-step loop by spawning fresh sub-agents (per [§3 Sub-agent architecture](#3-sub-agent-architecture)). The orchestrator itself never edits source, never walks `rules.md`, never marks anything CLEAN.

**1. Spawn Planner sub-agent (step plan entry):**

The orchestrator spawns a fresh **Planner** sub-agent with: step description, prerequisites, applicable rules.md categories, and references to relevant docs. The Planner reads what it needs, then appends to `docs/wip/<deliverable>/<NN>-<step>/journal.md`:

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

The Planner returns a summary; its context dies on return. The pre-emptive gate checks exist to push category-A/E/F catches to BEFORE the code is written, not after. This is where the loop count drops from 5 rounds to 1-2.

**1a. Plan-Audit (when required per [rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)):**

If the step introduces new types / new patterns / has >50-file scope (per §24.16 scope criteria), the orchestrator dispatches a **K=12 Plan-Audit batch + Aggregator** AFTER the Planner returns BUT BEFORE the Implementer is dispatched. Same K=12 cluster partition as code audits (per [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k12)); Plan-Auditors are scoped to the Plan section content (verifying reality alignment + naming convention compliance + rules.md predicate compliance + cross-language parity + existing pattern consistency + stale assumptions + §26 spec-mirror anti-pattern + §26.5 generated-file-fixes-must-target-generator-not-output). The Plan-Audit Aggregator merges 12 partials → `## Plan-Audit results` section appended to the journal (same three-artifact model as code-audit per [rules.md §24.0](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit), scoped to the Plan).

When the Plan-Audit Aggregator surfaces FINDING rows, the orchestrator dispatches a fresh **Plan-amender** sub-agent (Fixer-equivalent role, tool access limited to journal Plan-section + Plan-Audit fix log edits only). The Plan-amender addresses each finding by editing the Plan section + appending Plan-Audit fix-log entries. After Plan-amender returns, the orchestrator dispatches a brand-new K=12 Plan-Audit Round 2 to verify closure. Loop terminates on a CLEAN Plan-Audit (zero FINDING rows in the Plan-Audit big table); the orchestrator then proceeds to step 2 (Implementer dispatch) with the AMENDED Plan as input.

**Carve-outs** (Plan-Audit NOT required, per §24.16): trivial single-file edits (<5 net-new files, no new types / patterns / public surface), pure-doc deliverables, Step 0 branch-checkout / scaffolding-only steps, sub-dispatches within a step that already had upfront Plan-Audit. For carve-out steps, the orchestrator log explicitly cites which carve-out applies before dispatching the Implementer directly.

**Empirical justification**: deliverable `n/geo-libs` Step 2 Plan-Audit (the first canonical exercise) returned 35 findings (13 HIGH + 13 MEDIUM + 9 LOW) including naming-convention violations, stale assumptions inherited from a prior journal, wrong tsconfig paths, wrong locale counts, AND ONE security flaw. Without the Plan-Audit, the Implementer would have built directly against those bugs — producing code the subsequent code-Audit would then catch round-by-round, costing multiple Implementer + Fixer cycles plus the risk of the security flaw landing on a commit. A 10-minute Plan-Audit's cost is dramatically dominated by the saved Fixer cycles AND the risk reduction.

**2. Spawn Implementer sub-agent:**

The orchestrator spawns a fresh **Implementer** sub-agent with: the journal Plan section + the applicable rules.md categories + files-to-touch list. The Implementer writes the code + the corresponding tests, then appends:

```
=================================================
[YYYY-MM-DD HH:MM] Implementation
=================================================
Files: <list with brief purpose>
Approach notes: <anything not in the plan>
Tests written:
  Per-public-method coverage: N/N
  <method> -> <test file:line>
  ...
Adversarial coverage: <count, summary>
Build state: clean | <warnings to address>
Baseline currency: PASS | <packages needing re-seed>
```

If any consumable shared package's source was modified, the Implementer runs `pnpm --filter release-runner check-baselines` before declaring implementation complete. If the gate reports stale baselines, the Implementer re-runs the seed scripts, re-stages the baseline files, and records `Baseline currency: PASS` only after the gate exits 0. A stale baseline left for "later" is FINDING-HIGH at audit (§26.20).

The Implementer returns a structured files-touched + tests-added + build status summary; its context dies on return. The orchestrator does NOT read the source files itself — it consumes the summary.

**3. Audit loop (the core forcing function):**

For EACH round, the orchestrator dispatches a **K=12 batch of fresh Auditor sub-agents** in parallel (READ-ONLY tools — cannot edit source), then a **fresh Aggregator sub-agent** once all 12 partials return. Each cluster Auditor walks its slice of [rules.md](rules.md) per the canonical 12-cluster partition in [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k12) and produces evidence per predicate — grep results, file:line lists, "checked X by Y, found Z." Vibes ("looks fine") are not evidence. Each Auditor writes to its own partial file (`r{N}-partial-{CLUSTER}-{cluster-name}.md` where CLUSTER ∈ {A1, A2, B1, B2, B3, C1, C2, C3, D1, D2, E1, E2}); the **Aggregator** merges the 12 partials into the canonical big table (REPLACES `## Latest sweep results`) and appends a single `### Round N findings` subsection covering all 12 clusters + cross-cluster verification (per [§3 Aggregator role](#aggregator-role-post-cluster-consolidation)).

The orchestrator's per-round dispatch workflow lives in [§4 Per-round dispatch protocol](#per-round-dispatch-protocol).

When the Aggregator surfaces FINDING rows, the orchestrator spawns a **fresh Fixer sub-agent** with the consolidated findings list. The Fixer applies fixes + appends fix-log entries — it cannot mark anything CLEAN; closure is proven only by the NEXT round's fresh K=12 Auditor batch + Aggregator walking the predicates again and not surfacing the finding.

A second audit round is a BRAND-NEW K=12 Auditor batch + brand-new Aggregator, not the same ones re-running. The fresh-context property is non-negotiable.

**Wall-clock**: a K=12 batch's wall-clock is dominated by the slowest cluster (not the sum of 12). Empirically ~1/4-1/5 of a sequential K=1 walk against the same predicate count (K=12 gives ~35-45% wall-clock reduction relative to prior smaller-K splits), since parallel Auditors stay in their cluster's mental frame instead of context-switching across all 24 categories.

**K=1 carve-out**: requires explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). NEVER self-invoked by the orchestrator.

Detailed audit-loop mechanics (3-artifact journal model + mandatory round sequence + evidence requirements + emoji-prefixed Status column rule) live in [§4 Audit-loop mechanics](#4-audit-loop-mechanics).

**4. Per-step distillation:**

Once the step terminates clean (a fresh Auditor's big table came back with zero FINDING rows), the orchestrator spawns a fresh sub-agent (or reuses the last Auditor's summary) to append the distillation to the step journal:

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

These candidates surface in the root README's "Kinds-of-misses log" so they're visible across steps. Final approval / merge into `rules.md` happens at deliverable ship (see SHIP below).

**5. Update root README:**

After the step distillation, the **orchestrator** updates `docs/wip/<deliverable>/README.md` (this is one of the few `Edit` activities the orchestrator may perform itself, since the root README is the orchestrator's tracking artifact):

- Step status: ⏸ → 🔄 → ✅ (with iteration count: "✅ 02-service-identity-stack (3 audit rounds to clean)")
- Append to "Kinds-of-misses log" with the step's distillation summary
- If new cross-cutting decisions surfaced, append to that section

**6. Move to next step:**

Steps run in prerequisite order. Step N can start when all listed prerequisites are ✅. The orchestrator does NOT spawn a new Planner sub-agent for step N while the previous step has open audit findings.

<sup>[↑ jump to top](#top)</sup>

### FINAL-REVIEW

Same orchestrator-driven loop as EXECUTE, but scope = the whole deliverable. Catches integration / consistency bugs that no single-step audit would find: cross-step type drift, telemetry tag drift between two libs, README parity across all touched files, end-to-end integration paths.

Folder: `docs/wip/<deliverable>/final-review/journal.md`.

Same structure as a step — the orchestrator spawns fresh sub-agents per phase:

1. Spawn fresh **Planner** for cross-step concerns to walk (defines the deliverable-wide cross-cutting focus areas the Aggregator verifies in [§3 Aggregator role](#aggregator-role-post-cluster-consolidation) step 4)
2. Spawn fresh **Implementer** for any cross-cutting fixes (only if planning surfaces work)
3. Dispatch fresh **K=12 Final-reviewer batch** per round (READ-ONLY) per the canonical cluster partition in [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k12) — same `rules.md` ruleset, scope = whole deliverable. After all 12 partials return, dispatch fresh **Aggregator** to merge. Each round = a brand-new K=12 batch + brand-new Aggregator.
4. Spawn fresh **Fixer** when Aggregator surfaces findings
5. 10-iteration ceiling (where ONE iteration = one K=12 batch + Aggregator + Fixer); escalate if hit
6. Distillation entry

When the latest Aggregator's big table comes back with zero FINDING rows → deliverable is ready to SHIP.

<sup>[↑ jump to top](#top)</sup>

### SHIP (handoff to user REVIEW)

Triggered by final-review's clean termination. Agent does:

0. **Walk the [Deliverable completeness checklist](rules.md#deliverable-completeness-checklist-the-gate-before-user-review) BEFORE anything else in SHIP.** Every box must be honest YES with a citation. If any box is NO, SHIP is not ready — go back into fix-loops, re-walk the checklist, only proceed when every box is honestly YES. Then write the verbatim attestation block (from rules.md) into the deliverable's root README. Without the attestation, SHIP cannot proceed.
1. **Aggregate proposed rule additions** from all step distillations + final-review distillation. Deduplicate. Append the full proposed list to the root README's "Proposed rule additions" section.
2. **Present the root README to the user**. The user reviews:
   - Did the agent's audit catch what the user would have caught? (Implicit: spot-check 1-2 step journals, see if any obvious miss got past.)
   - Approve / tweak each proposed rule addition.
   - Approve the deliverable to merge.
3. **Apply approved rule additions** to `docs/dev/rules.md` (committed change).
4. **Copy the root README as a snapshot** from `docs/wip/NNNN-<name>/README.md` to `docs/dev/deliverables/NNNN-<name>.md` (committed — single file). The "Status" line flips to `SHIPPED YYYY-MM-DD`; the final-report section is populated; references to per-step journals get rephrased as prose since the journals don't cross the commit boundary.
5. **Leave the wip/ workspace untouched.** The per-step journals + root README + final-review journal stay where they are in `docs/wip/NNNN-<name>/` — gitignored, local-only. The process does NOT auto-delete them. The user removes them manually whenever they want (e.g. when freeing local disk space, when archiving the project). Until then, they remain available locally as audit-trail evidence.
6. **Consumable-lib changes carry the conventional-commit footer** the `tools/release-runner` reads at release time (per `rules.md §26.19`); SHIP itself does not bump package versions — the runner runs separately, after the deliverable merges.
7. **Commit** in this order, separately:
   - Approved `rules.md` additions
   - The shipped deliverable code (squash-merge from feature branch)
   - The new `docs/dev/deliverables/NNNN-<name>.md` snapshot

Each commit needs explicit user permission (no auto-commit).

<sup>[↑ jump to top](#top)</sup>

### REVIEW (user phase)

User reviews the shipped deliverable. **REVIEW is observe-and-capture, not fix-on-sight.** When the user surfaces feedback:

1. Agent captures the feedback as a numbered list — does NOT fix anything yet.
2. Per item, agent confirms understanding + asks "fix? leave? discuss further?"
3. User decides per item.
4. Approved fixes get a fresh deliverable folder (or, for trivial single-item fixes, a small follow-up commit with a regression test).

If REVIEW finds bugs that should have been caught by the agent's audit rounds, the right response isn't just "fix the bug" — it's also "what category was this, and why didn't the predicate catch it?" That gap becomes a new predicate in `rules.md`. Without this feedback loop, the rule catalog stays static and the agent keeps making the same kinds of misses.

<sup>[↑ jump to top](#top)</sup>

### Append-only discipline

Per-step `journal.md` files are append-only at the **substantive content** level:

- ✅ Fix typos / formatting / markdown rendering issues
- ❌ Rewrite an audit finding to make it look smaller in retrospect
- ❌ Delete entries from earlier rounds
- ❌ Edit a previous round's "Findings: 0 (clean)" to add the bug a later round found

The reason: the journal IS the evidence of process integrity. If round 3 missed something that round 5 caught, the journal must show that. Hiding the miss prevents the kind from feeding back into `rules.md`, and the agent will re-make the same miss next deliverable. **Honest journals are self-rewarding** — every honest miss becomes a future gate-check.

<sup>[↑ jump to top](#top)</sup>

### Scope of work shape

This process scales to deliverables of meaningfully different sizes. Two examples:

**Small deliverable** — one csproj, one logical feature. Step list: `01-<feature>` + `final-review`. Two journals. Most of the value is the first-pass discipline + the journal artifact for the user to review.

**Large deliverable** — multi-csproj refactor or build-out. Step list: `01-csproj-1` through `09-csproj-9` + `final-review`. Ten journals. Cross-cutting decisions surface in the root README; per-step journals carry per-csproj detail.

There's no "lightweight path" for trivial changes — even a typo fix benefits from "did you check whether this typo appears elsewhere in the same doc?" The cost of running the full ruleset on a small change is minutes; the cost of NOT running it (and missing the parallel typo) is a future audit round. **The orchestrator-only-main-thread + fresh-sub-agent-per-round pattern (see [§3 Sub-agent architecture](#3-sub-agent-architecture)) applies at every scope: a one-line typo fix still spawns a Planner / Implementer / Auditor / (if findings) Fixer chain. Sub-agent invocation cost is small; production regression cost is large.**

<sup>[↑ jump to top](#top)</sup>

### What this process does NOT do

- **Doesn't replace CLAUDE.md.** CLAUDE.md still defines the agent-directive root + conventions catalog references. This process doc defines the _process_ that ensures the conventions are actually followed.
- **Doesn't replace `docs/v2/`.** Phase / wave tracking continues to live in the `docs/v2/` set. This process is per-deliverable; `docs/v2/` is the long-arc roadmap.
- **Doesn't replace per-lib READMEs.** Each shared lib still has its own `README.md` documenting its public API. This process doesn't generate or maintain those.
- **Doesn't run scripts.** No pre-commit hook, no CI gate that fires `rules.md` mechanically. The discipline is the agent walking the rules each round and producing evidence — verifiable by inspecting the journal.

<sup>[↑ jump to top](#top)</sup>

### When to invoke this process

Always, for any work substantial enough to warrant a deliverable folder. The user can override per-task ("just do this small thing, no journal needed" — per [rules.md §13.14](rules/13-permission-action-discipline.md#13-permission--action-discipline) process-bypass-requires-explicit-naming), but the default is "every meaningful unit of work uses the loop."

The forcing function for the agent: if there's no `docs/wip/<deliverable>/README.md` for the work in flight, the agent should ASK whether to create one before proceeding past PLAN.

<sup>[↑ jump to top](#top)</sup>

---

## 2. Permission gates (when to pause for the user)

The following actions require explicit user permission **per occurrence**, not implied from prior turns. Predicate-of-record: [rules.md §13 Permission / Action Discipline](rules/13-permission-action-discipline.md#13-permission--action-discipline).

> **Duplicated from [rules.md §13](rules/13-permission-action-discipline.md#13-permission--action-discipline) for at-a-glance protocol context. The canonical full version with Evidence + Why + How blocks for each predicate lives in rules.md — update both in lockstep when either changes. Annotation per [rules.md §11.32](rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices).**

- **Commit creation** — "go ahead and commit" approves the batch just discussed; the next commit needs fresh permission. (rules.md §13.1)
- **Bulk file operations** (sed across N files, mass rename, multi-file delete, bulk format-write) — agent declares scope (file count, glob, what changes) BEFORE executing; user has the chance to redirect. (rules.md §13.2)
- **Destructive git operations** (force push, hard reset, branch delete, checkout that overwrites uncommitted work) — explicit authorization required. (rules.md §13.3)
- **Deferring planned work** — if a step turns out larger than expected, agent ASKS to defer — does not unilaterally skip. (rules.md §13.4)
- **Architectural decision changes mid-execution** — if implementation surfaces a reason to deviate from the locked PLAN, agent ASKS — does not silently rework. (rules.md §13.5)
- **Process-bypass naming** — every bypass requires per-occurrence user-quoted authorization NAMING the specific rule / step being skipped. Verbal "go ahead" / "looks good" / implicit consent from prior conversation does NOT qualify. (rules.md §13.14)
- **K=1 audit-round dispatch** — never self-invoked; requires explicit per-round user permission with quoted authorization in the orchestrator log. (rules.md §24.0h + cross-ref [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy))

### Deferral posture — do-it-now is the default

Reflexive deferral is a recurring failure mode to actively guard against. The recurring form it takes: marking work "inert until a consumer", "ready but deferred to the live-wiring step", or "track it for follow-on" when the work was fully buildable and provable in isolation — and there was no missing build dependency, only the absence of a live downstream caller.

The operative test for whether deferral is legitimate is whether a **build dependency is genuinely missing** — something that must exist before the work can be built AND proven in isolation: an unbuilt collaborator with no faithful §1.32 test-double, an undesigned decision whose outcome changes the work's shape, missing infrastructure, or a running host/process needed to register LIVE wiring into. "No consumer exists yet", "not wired into the live host yet", "not exercised cross-process yet", "a fixture or tracker row labels it deferred", and "the real config or domain values don't exist yet" are NOT build dependencies. Proving in isolation (Testcontainers, an in-memory transport TestServer, faithful §1.32 doubles) needs no live host and no real consumer.

The default is therefore: **if the work is in-scope and no build dependency is missing, build it and prove it in isolation now** — do not wait for the first consumer; waiting is exactly how no-dependency work gets silently forgotten. Correct and complete work is preferred over fast and partial work even when it takes substantially longer. A genuine blocker gets a committed tracker row (not a comment- or journal-only TODO) and is surfaced to the user per §13.4. YAGNI applies only to work that is NOT known-needed.

Predicate-of-record: [rules.md §13.15](rules/13-permission-action-discipline.md#13-permission--action-discipline). Annotation per [rules.md §11.32](rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices).

<sup>[↑ jump to top](#top)</sup>

---

## 3. Sub-agent architecture

**The main thread is an ORCHESTRATOR. It does not plan, implement, audit, or fix domain work itself. EVERY round of planning, implementation, auditing, and fixing is performed by a FRESH sub-agent spawned via the `Agent` tool.** This is the canonical workflow, not optional.

### Why this is structural, not stylistic

[Anthropic's multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) (orchestrator-Opus + worker-Sonnet) outperforms single-agent Opus by 90.2% on internal evals — the orchestrator-worker pattern is empirically validated for tasks that involve adversarial separation of concerns. [Adversarial code review research](https://asdlc.io/patterns/adversarial-code-review/) shows that LLM self-review has systematic leniency bias, and that reviewer + generator sharing the same context share blind spots. "Most agent reviews agent implementations are one LLM with a clever prompt pretending to be three reviewers, where the model can rubber-stamp itself" — the structural fix is SEPARATE sub-agent invocations with fresh contexts, not roleplay.

Empirical justification from the deliverable 0002-auth-inbound trial: per-step audits converged in 1-3 rounds (mostly 2), and two real production bugs were caught by adversarial separation that single-context implementation would have shipped:

1. `JwtAuthInterceptor.ResolveMethodScopeMetadata` reading the wrong `UserState` slot — caught by an integration test the Implementer skipped, that a Fixer was forced to add.
2. `act_chain_malformed` dead-letter chain: `MalformedActorChainException` propagating uncaught from `ClaimsToContextMapper.Map` — caught only by the deliverable-wide enumeration that surfaced "AuthFailures helper exists + AuthErrorCodes constant exists + xmldoc enumerates the outcome + README documents it — but JwtValidator never emits it."

Main-thread context stayed small across the whole deliverable (8 step-level audits + final-review + 3 rounds of polish + cross-deliverable design discussions all fit comfortably). User feedback after the trial: "the subagents, while slower to complete work, are actually doing a cleanly better job."

This mirrors Claude Code's [sub-agent design](https://code.claude.com/docs/en/sub-agents): each sub-agent gets a fresh isolated context, encapsulates work, and returns only relevant output to the orchestrator. Context rot in the main thread is near-impossible because the main thread holds almost no domain state.

### Allowed in main-thread context

- ✅ `Agent` (spawn sub-agents — the primary orchestrator activity)
- ✅ `Bash` — only for git plumbing (`git status`, `git log`, `git commit -F <file>` when authorized, `git push` when authorized)
- ✅ `Read` — only for the deliverable root README and the orchestrator's own decision log; sub-agents handle source / test / journal content reads
- ✅ `TaskCreate` / `TaskUpdate` / `TaskList` / `TaskGet` / `TaskStop`
- ✅ `Edit` / `Write` to the deliverable root README's tracking sections + its own decision log (`docs/wip/<deliverable>/orchestrator-log.md` if used)
- ❌ `Edit` / `Write` to source / tests / per-lib READMEs / framework docs (sub-agents do this)
- ❌ `Edit` / `Write` to journal big-table or findings log (Auditor / Aggregator sub-agents do this)

The main thread's job is decision-making, not implementation, not auditing. It reads sub-agent summaries and routes the next step. **It cannot mark anything CLEAN or PASS itself** — it only consumes those verdicts from sub-agents.

### Forbidden in main-thread context

- `Edit` / `Write` to ANY source file, test file, per-csproj README, per-service README, or framework doc
- `Bash` for builds, tests, `jb inspectcode`, or any domain-level grep / inspection
- `Read` on source files, test files, or per-lib READMEs — delegate to sub-agents (they have the fresh context to absorb domain detail)
- Reading journal files mid-deliverable for content review — delegate state-checks to sub-agents that report back summary
- Walking `rules.md` predicates — always done by Auditor sub-agents
- Marking anything CLEAN / PASS / converged from main-thread judgment — those verdicts come from Auditor sub-agent output

<sup>[↑ jump to top](#top)</sup>

### Canonical sub-agent roles

Six distinct roles (Final-reviewer added at deliverable end). Each is spawned with a fresh context and a tightly-scoped prompt. **No reuse across roles or across rounds.**

| Role                                                     | Spawned when                                                                                 | Tool access                                        | Returns                                                                                                                                                                            |
| -------------------------------------------------------- | -------------------------------------------------------------------------------------------- | -------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Planner**                                              | Start of each step                                                                           | Read, Grep, Glob, Edit (journal Plan section only) | Step Plan section appended to journal + summary                                                                                                                                    |
| **Plan-Auditor** (parallel ×K=12, default)               | After Planner (when step introduces new types / new patterns / >50-file scope per §24.16)    | Read, Grep, Glob, Bash (read-only)                 | Partial big-table chunk auditing the Plan section against assigned cluster's §-range (same cluster partition as Auditor); written to a designated partial file                     |
| **Plan-amender**                                         | When Plan-Audit Aggregator surfaces findings                                                 | Read, Grep, Glob, Edit (journal Plan section + Plan-Audit fix log only) | Plan section edits + appended Plan-Audit fix-log entries                                                                                                                          |
| **Implementer**                                          | After Planner (carve-out steps) OR after Plan-Audit CLEAN (Plan-Audit-required steps)        | All                                                | Files touched + tests added + build / inspectcode status                                                                                                                           |
| **Auditor** (parallel ×K=12, default)                    | After Implementer                                                                            | Read, Grep, Glob, Bash (read-only)                 | Partial big-table chunk for assigned cluster (see [Auditor cluster partition](#auditor-cluster-partition-canonical-k12)) written to a designated partial file                      |
| **Aggregator** (one per audit round)                     | After all 12 Auditors return (also: after all 12 Plan-Auditors return for Plan-Audit rounds) | Read, Edit (journal + audit artifacts only)        | Canonical merged big table embedded in journal + consolidated findings log entry + cross-cluster verification (see [Aggregator role](#aggregator-role-post-cluster-consolidation)) |
| **Fixer**                                                | When findings exist                                                                          | All                                                | Files changed + appended fix-log entries                                                                                                                                           |
| **Final-reviewer** (parallel ×K=12, deliverable-end only) | Before SHIP                                                                                 | Same as Auditor                                    | Cluster-scoped partial big tables; Aggregator merges as above                                                                                                                      |

**Key design decisions:**

- **Planner is its own role.** Spawned at the start of each step with the step description + applicable rules.md categories + relevant docs to read. It writes the step's Plan section (goal, files to touch, decisions, pre-emptive gate checks) and returns. The Implementer then receives the Plan as input — fresh context, no exposure to whatever the orchestrator was discussing with the user.
- **Auditors cannot modify source.** Read-only Bash. This makes "audit + fix in same session" structurally impossible — fixes always happen in a separate Fixer invocation, after findings are RECORDED in the journal (no "I fixed it before recording it" sleight-of-hand).
- **Auditor adversarial framing.** Per [adversarial code review research](https://asdlc.io/patterns/adversarial-code-review/): the Auditor prompt explicitly states it's rewarded for finding issues, not for declaring CLEAN. Its role is hostile critic.
- **Parallel cluster dispatch is the default.** K=12 Auditors run concurrently per audit round, each scoped to one cluster of `rules.md` predicates (see [Auditor cluster partition](#auditor-cluster-partition-canonical-k12)). The Aggregator (see [Aggregator role](#aggregator-role-post-cluster-consolidation)) merges the 12 partials into the canonical journal artifacts and performs cross-cluster verification. The orchestrator's dispatch workflow lives in [§4 Per-round dispatch protocol](#per-round-dispatch-protocol).
- **Effort-scaling rules in prompts** (per Anthropic guidance): each sub-agent prompt caps effort proportional to the step's surface area. Small step = "don't write 17 ctor variants for a 1-property record." Cluster scope already constrains per-Auditor effort to ~10-40 predicate rows.
- **Aggregator is required, never optional.** Whenever K>1 Auditors run, the Aggregator is what produces the canonical big table + consolidated findings log entry the journal commits to. It cannot change cluster Auditor verdicts unilaterally — only dedupes, merges, and adds cross-cluster sister-sweep findings the per-cluster Auditors couldn't see. If two Auditors disagree on the same row (rare; row ownership is partitioned by §-number), the Aggregator escalates to the orchestrator for a tie-breaker Auditor. **The Aggregator runs on Fable** (per the canonical [Sub-agent model policy per role](#sub-agent-model-policy-per-role) table below; Fable's large context window accommodates the 12-partial merge) because merging 12 partials' worth of context — each cluster's full big-table chunk plus partial findings — pushes well past smaller-context budgets, and cross-cluster dedup / sister-sweep reasoning benefits from Fable-class capability.
- **K=1 carve-out for trivial steps requires explicit user permission.** Per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). NEVER self-invoked.
- **Plan-Audit is mandatory before Implementer dispatch for non-trivial steps.** Per [rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit), any step's PLAN that introduces new types / new patterns / >50-file scope gets a K=12 Plan-Audit (same cluster partition as code Auditors) + Aggregator BEFORE the Implementer is dispatched. Plan-Auditors verify reality alignment (Plan claims match actual codebase state), naming conventions, rules.md predicate compliance, cross-language parity (if multi-lang), existing pattern consistency, stale assumptions, and §26 spec-mirror anti-pattern. Plan-amender (Fixer-equivalent role scoped to journal Plan-section + Plan-Audit fix-log only) addresses surfaced findings; Round 2 Plan-Audit verifies closure. Loop terminates on CLEAN Plan-Audit; Implementer then receives the AMENDED Plan as input. Carve-outs (per §24.16): trivial single-file edits (<5 net-new files, no new types/patterns), pure-doc deliverables, Step 0 branch-checkout / scaffolding, sub-dispatches within a step that already had upfront Plan-Audit. Empirical justification: deliverable `n/geo-libs` Step 2 Plan-Audit returned 35 findings (13 HIGH + 13 MEDIUM + 9 LOW) including stale assumptions, wrong tsconfig paths, wrong locale counts, AND one security flaw — without Plan-Audit, the Implementer would have built on those bugs, costing multiple downstream Fixer cycles + the security flaw's commit-time risk.

<sup>[↑ jump to top](#top)</sup>

### Sub-agent model policy per role

This subsection is the SINGLE CANONICAL location for which Claude model each sub-agent role runs on. All other references in process.md, rules.md, CLAUDE.md cross-link here. Predicate-of-record (the enforcement gate walked every audit round): [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

**Canonical role-to-model table:**

| Role                                    | Default model | Why this model                                                                                                                                                                                                                                                                                          |
| --------------------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Orchestrator** (main thread)          | Fable 5       | Judgment + delegation + trust-but-verify discipline (per [§4 Orchestrator verification of sub-agent outputs](#orchestrator-verification-of-sub-agent-outputs)). Cannot outsource the spot-check / re-run / adversarial-challenge diligence that catches sub-agent hallucinations or short-circuits.       |
| **Planner**                             | Fable 5       | Plan section quality drives every downstream sub-agent's output; high-leverage low-volume artifact. A missed gate-check in the Plan cascades into Implementer + Auditor + Fixer cycles post-hoc — Fable-class reasoning at Plan-authoring time prevents the cascade.                                     |
| **Plan-amender**                        | Fable 5       | Writes to the same canonical Plan artifact the Planner authored; same high-leverage low-volume rationale. Mid-execution amendments must be coherent with the original Plan's framing + locked decisions; a workhorse-tier model here risks introducing inconsistency the next Implementer builds against. |
| **Aggregator** (one per audit round)    | Fable 5       | Merges 12 cluster Auditor partials + cross-cluster sister-sweep + cross-cutting verification. Fable's large context window is sufficient for the 12-partial merge. Cross-cluster reasoning across 12 thematic perspectives benefits from Fable-class capability.                                          |
| **Auditor** (per-cluster, K=12)         | Opus 4.8      | Predicate pattern-matching + grep verification + file:line citations — bounded predicate scope per cluster, structured output format (the partial-file template), no synthesis (the Aggregator owns synthesis). Workhorse-shape work; Fable over-specified.                                              |
| **Plan-Auditor** (per-cluster, K=12)    | Opus 4.8      | Same shape as Auditor; design-phase scope (audits the Plan section, not the code). Same workhorse-shape rationale.                                                                                                                                                                                       |
| **Final-reviewer** (per-cluster, K=12)  | Opus 4.8      | Same shape as Auditor; deliverable-wide scope (audits the full deliverable, not one step). Same workhorse-shape rationale.                                                                                                                                                                               |
| **Implementer**                         | Opus 4.8      | Bounded code/test authorship per a specific brief; Plan / Aggregator / orchestrator have already done the hard reasoning. Opus handles this well. Sweeping carve-out applies (see below) for unusually large briefs.                                                                                    |
| **Fixer**                               | Opus 4.8      | Mechanical application of pre-specified fix scope; sister-sweep + tamper-evident discipline executes against a tight contract. Opus handles this well. Sweeping carve-out applies for unusually large fix sets.                                                                                         |
| **Investigator / Research**             | Opus 4.8      | Bounded-scope investigation tasks returning structured reports (file paths, grep counts, line citations, summaries). Opus's bread and butter.                                                                                                                                                            |

**Why this allocation** (cost-quality trade-off + workhorse-shape vs premium-shape work):

- **Spend Fable where capability moves outcomes** — synthesis (Aggregator merge + cross-cluster sister-sweep), high-leverage low-volume planning (Planner / Plan-amender), and the trust-but-verify discipline that compensates for workhorse dispatch (Orchestrator). These roles produce artifacts every downstream sub-agent reads + acts on; Fable-class reasoning at these points prevents cascading errors.
- **Use Opus where capability already saturates** — predicate walking + structured output (Auditor / Plan-Auditor / Final-reviewer), bounded code/test execution per a contract (Implementer / Fixer), bounded investigation with structured deliverables (Investigator). These roles operate against tight contracts that constrain output space; Fable would burn budget without moving outcomes.
- **Fable availability is finite** — the K=12 Auditor dispatch is the highest-volume sub-agent invocation pattern in the framework (12 parallel × multiple rounds per step × multiple steps per deliverable). Spending Fable on the workhorse-shape roles depletes Fable availability for the synthesis + planning roles where capability actually matters.
- **The verification discipline is the structural compensation** — when dispatching to Opus-default workhorse roles, the orchestrator takes on additional spot-check / re-run / adversarial-challenge responsibility (see [§4 Orchestrator verification of sub-agent outputs](#orchestrator-verification-of-sub-agent-outputs)). Workhorse dispatch + trust-but-verify discipline > Fable dispatch alone for predicate-walking work, because the verification catches the small fraction of workhorse outputs that misfire while the cost savings free up Fable for synthesis.

**Sweeping carve-out** (Implementer / Fixer Fable escalation — codified bypass requiring no per-occurrence user approval):

An Implementer or Fixer dispatch qualifies for Fable escalation when it meets ≥1 of the following criteria. The orchestrator's dispatch brief MUST cite the triggering criterion + a brief justification; the sub-agent's return self-attestation MUST echo the same citation.

1. **Atomic large-file-set** — the dispatch touches >40 files atomically (cannot be split into smaller dispatches without breaking the build between commits, or without producing intermediate states that fail audit).
2. **Multi-concern dispatch** — the dispatch spans >3 distinct concerns where splitting would create coordination overhead exceeding the Fable premium (e.g., a single Implementer brief covers a new handler + its DI wiring + its test file + its README + its proto wiring).
3. **Cross-runtime refactor** — the dispatch requires coordinated changes across .NET + TS runtimes (e.g., naming-convention sweep across both runtimes, cross-language type rename, parity-test alignment after a spec change).
4. **Cascading pipeline change** — the dispatch changes a code-generation pipeline (or pipeline-input) and requires regenerating downstream consumer assemblies that depend on the pipeline output.

The carve-out applies ONLY to Implementer / Fixer (not to Auditor / Plan-Auditor / Final-reviewer / Investigator); Auditor-shaped work does not exceed Opus's comfort zone at any K count or scope. Auditor / Plan-Auditor / Final-reviewer / Investigator escalations to Fable require explicit per-occurrence user approval per [rules.md §13.14](rules/13-permission-action-discipline.md#13-permission--action-discipline).

**Self-documentation requirement** (every sub-agent return summary):

Every sub-agent's return summary MUST include at the top, before any other content, a model-attestation block:

```
Model: <claude-model-id — e.g., claude-fable-5 or claude-opus-4-8>
Fable carve-out reason (if dispatched to Fable from an Opus-default role): <criterion # + justification, verbatim from dispatch brief>
```

The orchestrator's per-step journal MUST record per dispatch — (a) the dispatched model, (b) the role being filled, (c) if Fable dispatched from an Opus-default role: the carve-out criterion invoked + the verbatim justification. This dual-channel attestation (orchestrator dispatch log + sub-agent return self-attestation) gives retroactive auditability for self-learn loops: after N deliverables, a sweep can identify which Opus dispatches needed re-do (role-to-task fit was wrong) vs which Fable dispatches could've been Opus (carve-out justification was weak). The self-learn loop tunes the policy over time.

**Cross-references:**

- [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — the predicate-level enforcement of this policy (walked every audit round, including the carve-out and self-documentation sub-blocks).
- [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — K=1 discipline (the parallelism gate; composes with §24.0i model gate per the Combined K=1 + Opus authorization clause).
- [§4 Orchestrator verification of sub-agent outputs](#orchestrator-verification-of-sub-agent-outputs) — trust-but-verify discipline that compensates for workhorse dispatch (the structural reason workhorse dispatch is safe).
- [CLAUDE.md MANDATORY block 0](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread) — the sub-agent role table in CLAUDE.md cross-links here for the model column.

<sup>[↑ jump to top](#top)</sup>

### Every round = a NEW fresh sub-agent

A second audit round is a brand-new Auditor sub-agent, NOT the same Auditor "running again." A fix follow-up after a Fixer's first attempt is a brand-new Fixer. The fresh-context property is the entire point — it's what prevents leniency / motivated-stopping / stale-memory failure modes. Reusing context across roles defeats the whole pattern.

The orchestrator never short-circuits this for "quick" work. A one-line typo fix still spawns a Planner, Implementer, Auditor, and (if findings) Fixer. Sub-agent invocation cost is small; production regression cost is large.

<sup>[↑ jump to top](#top)</sup>

### The orchestrator cannot mark CLEAN

The orchestrator consumes Auditor verdicts; it cannot promote a step to CLEAN by judgment. CLEAN means "the latest Auditor sub-agent's big table contained zero FINDING rows." If the orchestrator wants to confirm closure, it spawns a fresh Auditor — it does not eyeball.

<sup>[↑ jump to top](#top)</sup>

### Auditor cluster partition (canonical K=12)

The `rules.md` catalog (~24 categories, ~450 numbered subsections) partitions into 12 thematic clusters. Each Auditor sub-agent owns exactly one cluster and walks every numbered subsection inside that cluster — reading only that cluster's category files under [`rules/`](rules/) (mapped in the [per-cluster reading list](#auditor-cluster-partition-canonical-k12) below) — against the step's (or deliverable's, at final-review) file scope. The partition is fixed — orchestrator dispatch consistently sends the same §-range to the same cluster code across deliverables so accumulated muscle memory carries forward.

| Cluster  | Name                                                                   | rules.md sections        | ~predicates | Theme                                                                                                |
| -------- | ---------------------------------------------------------------------- | ------------------------ | ----------- | ---------------------------------------------------------------------------------------------------- |
| **A1**   | Tests / coverage                                                       | §1                       | ~30         | Tests / coverage                                                                                     |
| **A2**   | Regression, races, disposal, degradation, idempotency                  | §2, §4, §15, §18, §22    | ~25         | Regression-pinning, concurrency / races, object disposal / resource lifetime, graceful degradation, idempotency / exactly-once |
| **B1**   | C# conventions                                                         | §5                       | ~25         | C# conventions                                                                                       |
| **B2**   | TS conventions + naming                                                | §6, §7                   | ~20         | TypeScript / SvelteKit conventions, naming / file headers / folder casing                            |
| **B3**   | Shared-lib hygiene + D2Result                                          | §16, §17                 | ~15         | OOTB shared-lib catalog use, D2Result usage + extensions                                             |
| **C1**   | PII/logging + operations                                               | §3, §8                   | ~20         | PII / logging safety, build cleanliness + operational hygiene                                        |
| **C2**   | Architectural layer                                                    | §9                       | ~45         | Architectural layer hygiene                                                                          |
| **C3**   | Security + permissions                                                 | §10, §13                 | ~25         | Security (endpoints / auth / secrets / input), permission / action discipline                        |
| **D1**   | KEEP doc parity                                                        | §11                      | ~40         | KEEP-doc updates + forward-framing + per-lib README parity                                           |
| **D2**   | i18n + no-phase verbiage                                               | §12, §14                 | ~10         | i18n / Paraglide / TK constants, no-phase-verbiage / no-conversation-scoped-IDs hygiene              |
| **E1**   | UX + DX + observability + config                                       | §19, §20, §21, §23       | ~25         | UX, DX, observability completeness, configuration hygiene                                            |
| **E2**   | Audit-meta + temporal + codegen                                        | §24, §25, §26            | ~35         | Audit evidence discipline (incl. self-audit per §24.12), temporal-types discipline, codegen discipline |

**Per-cluster category-file reading list.** `rules.md` is split into one file per category under [`rules/`](rules/) with `rules.md` retained as the index. Each cluster Auditor reads ONLY its own category files below — not the whole catalog — plus the index-level [Deliverable completeness checklist](rules.md#deliverable-completeness-checklist-the-gate-before-user-review) (read by every cluster). This is the context cut the split buys: an Auditor loads ~one cluster's worth of predicates instead of the full ~710 KB catalog.

| Cluster | Category files to read |
| ------- | ---------------------- |
| **A1** | [rules/01-test-discipline.md](rules/01-test-discipline.md) |
| **A2** | [rules/02-bug-fix-regression-testing.md](rules/02-bug-fix-regression-testing.md), [rules/04-concurrency-race-conditions.md](rules/04-concurrency-race-conditions.md), [rules/15-object-disposal-resource-lifetime.md](rules/15-object-disposal-resource-lifetime.md), [rules/18-graceful-degradation-failure-modes.md](rules/18-graceful-degradation-failure-modes.md), [rules/22-idempotency-exactly-once-semantics.md](rules/22-idempotency-exactly-once-semantics.md) |
| **B1** | [rules/05-csharp-code-conventions.md](rules/05-csharp-code-conventions.md) |
| **B2** | [rules/06-typescript-sveltekit-code-conventions.md](rules/06-typescript-sveltekit-code-conventions.md), [rules/07-naming-file-headers-folder-casing.md](rules/07-naming-file-headers-folder-casing.md) |
| **B3** | [rules/16-ootb-shared-lib-tooling-use-whats-there.md](rules/16-ootb-shared-lib-tooling-use-whats-there.md), [rules/17-d2result-usage-extensions.md](rules/17-d2result-usage-extensions.md) |
| **C1** | [rules/03-pii-logging-safety.md](rules/03-pii-logging-safety.md), [rules/08-build-tooling-hygiene.md](rules/08-build-tooling-hygiene.md) |
| **C2** | [rules/09-architectural-layer-hygiene.md](rules/09-architectural-layer-hygiene.md) |
| **C3** | [rules/10-security-endpoints-auth-secrets-input.md](rules/10-security-endpoints-auth-secrets-input.md), [rules/13-permission-action-discipline.md](rules/13-permission-action-discipline.md) |
| **D1** | [rules/11-documentation-parity-best-practices.md](rules/11-documentation-parity-best-practices.md) |
| **D2** | [rules/12-i18n-discipline.md](rules/12-i18n-discipline.md), [rules/14-phase-audit-conversation-verbiage-hygiene.md](rules/14-phase-audit-conversation-verbiage-hygiene.md) |
| **E1** | [rules/19-user-experience-ux.md](rules/19-user-experience-ux.md), [rules/20-developer-experience-dx.md](rules/20-developer-experience-dx.md), [rules/21-observability-completeness.md](rules/21-observability-completeness.md), [rules/23-configuration-hygiene.md](rules/23-configuration-hygiene.md) |
| **E2** | [rules/24-audit-evidence-discipline-meta-how-to-audit.md](rules/24-audit-evidence-discipline-meta-how-to-audit.md), [rules/25-temporal-types-date-time-clock.md](rules/25-temporal-types-date-time-clock.md), [rules/26-codegen-discipline-spec-proto-schema-derived-types.md](rules/26-codegen-discipline-spec-proto-schema-derived-types.md) |

**Why this partition** (K=12):

- **D split into D1 (§11 alone) and D2 (§12 + §14)**: §11 is the densest section in the catalog (~40 predicates around KEEP-doc parity + per-lib README sweep + framing discipline) — separating it from the lighter §12 / §14 sections lets D1 get dedicated focus instead of crowding the i18n + no-phase-verbiage concerns.
- **E split into E1 (§19/§20/§21/§23 — operational quality) and E2 (§24/§25/§26 — process integrity)**: E1 covers the "is the system operable" axis (UX, DX, observability, config); E2 covers the "is the process trustworthy" axis (audit meta, temporal, codegen). The two clusters share no predicate overlap and benefit from separate mental frames.
- **A/B/C clusters split along natural §-boundaries.** A1 (§1 tests) and A2 (§2/§4/§15/§18/§22 — the remainder of correctness/reliability) sit at a natural break inside the correctness/reliability theme. B1 (§5 C#) / B2 (§6+§7 TS+naming) / B3 (§16+§17 shared-libs+D2Result) split along language + concern boundaries (per-language conventions plus shared-lib hygiene as its own cluster). C1 (§3+§8 PII+ops) / C2 (§9 layer) / C3 (§10+§13 security+permissions) keep §9 (the largest of the architectural sections, ~30 predicates) standalone so its predicates don't crowd the surrounding security / PII / permission sections.
- **Trade-off**: K=12 gives ~35-45% wall-clock reduction relative to prior smaller-K splits (vs ~30-40% for a K=10 alternative) and tighter per-Auditor focus (each Auditor stays in ~10-40 predicates). Cost: slightly higher Aggregator dedup complexity — handled by running the Aggregator on Fable (per [Sub-agent model policy per role](#sub-agent-model-policy-per-role)) so the merge has the context budget to consolidate 12 partials and the capability to do cross-cluster sister-sweep reasoning across them.
- **Thematically cohesive**: each cluster's theme is one coherent mental model — Auditor can stay in one frame of mind for the full walk instead of context-switching across orthogonal concerns.
- **Stable §-ownership**: the same §-range maps to the same cluster code (A1, A2, B1, B2, B3, C1, C2, C3, D1, D2, E1, E2) across every deliverable. A repeat finding's history can be threaded through past partials by cluster code.
- **Cross-cutting concerns belong to the Aggregator**, not to any one cluster — the Aggregator's cross-cluster sister-sweep responsibility (run on Fable per [Sub-agent model policy per role](#sub-agent-model-policy-per-role) for adequate context + reasoning budget) is what catches concerns that span clusters (e.g. a security predicate in §10 whose fix has style implications in §5, or a doc-framing concern in §11 that needs architectural verification in §9).

**When a predicate seems to straddle clusters:** the cluster mapping is `rules.md` §-number → cluster code, NOT topic → cluster code. If a predicate's spirit feels like it belongs to two clusters, the §-number wins. The Aggregator's cross-cluster verification step (see [Aggregator role](#aggregator-role-post-cluster-consolidation) step 4) is where straddle concerns get resolved — not in the per-cluster Auditor walk.

<sup>[↑ jump to top](#top)</sup>

### Aggregator role (post-cluster consolidation)

The Aggregator is a single sub-agent spawned per audit round AFTER all K=12 cluster Auditors have returned their partials. It is the journal's authoritative writer for the round — the per-cluster Auditors write to disposable partial files; the Aggregator alone writes to the canonical journal sections. **The Aggregator runs on Fable** (per the canonical [Sub-agent model policy per role](#sub-agent-model-policy-per-role) table; Fable's large context window is sufficient for the 12-partial merge) so it has the context budget to consume 12 partials' worth of big-table chunks + findings lists and the reasoning capability to perform cross-cluster dedup + sister-sweep verification across them.

**Six responsibilities (in order):**

1. **Mechanical merge.** Read all 12 partial files (`r{N}-partial-{A1|A2|B1|B2|B3|C1|C2|C3|D1|D2|E1|E2}-{cluster-name}.md` in the round's working dir). Combine the 12 partial big-table chunks into ONE canonical sorted-by-§ big table. Write that table under `## Latest sweep results` in the step / final-review journal, REPLACING the prior sweep's table per the §24 sweep-replacement rule. Anti-laziness preamble appears verbatim above.
2. **Dedupe.** Same finding surfaced by multiple Auditors (e.g. a line-length violation Cluster B owns by predicate, but Cluster D also flagged from a doc-citation angle) collapses into a single entry with combined provenance. Dedupe preserves all citation paths in the entry's description.
3. **Cross-cutting verification.** Walk the deliverable's cross-step focus areas that span multiple clusters — defined per-deliverable in the Plan section of the final-review journal (e.g. "TYPE LIE FIX still verified end-to-end across .NET emitter + TS consumer", "β routing correctness across both consumers", "wire-shape leakage class — does the originating sample class exist anywhere else in scope?", "spec-driven catalog parity — every cross-language wire identifier cataloged + parity-tested"). These are the concerns no per-cluster Auditor could see because they cross §-ranges.
4. **Cross-cluster sister-sweep.** Per rules.md §24.13.3, cluster Auditors sister-sweep WITHIN their cluster's §-scope. The Aggregator runs sister-sweeps at the CROSS-cluster scope. See [§4 Cross-cluster sister-sweep checklist](#cross-cluster-sister-sweep-checklist-aggregator-baseline) for the concrete baseline commands the Aggregator runs every round regardless of cluster Auditor coverage.
5. **Append findings log.** Write a single `### Round N findings (<UTC>)` subsection under `## Sweep findings log (append-only)` in the journal containing: the consolidated finding list (from steps 2-4), a closure-verification table for prior-round findings (each prior-round finding annotated as CLOSED-by-absence in this round's big table OR STILL-PRESENT requiring another fix cycle), and a regression-verification table where applicable (prior-round PASS rows the Aggregator spot-confirmed are still PASS so cascading regressions are caught).
6. **Return summary to orchestrator.** A structured one-paragraph summary: total findings count by severity, list of fix-required §-rows, recommendation (CLEAN → next phase OR findings → spawn Fixer with specific scope).

**What the Aggregator cannot do:**

- **Cannot change per-cluster verdicts unilaterally.** A row Cluster B PASSed cannot be flipped to FINDING by the Aggregator without escalating to the orchestrator for a tie-breaker Auditor. The Aggregator can ADD findings (from steps 3-4 cross-cluster verification) but cannot OVERRULE Auditors.
- **Cannot touch source / tests / configs.** Write access is journal + audit artifacts only.
- **Cannot mark the step CLEAN.** It RECOMMENDS clean to the orchestrator; the orchestrator consumes the recommendation along with the big table itself (which must contain zero FINDING rows for CLEAN status to be valid).

**Why the Aggregator is required:** when K>1 Auditors run in parallel, no single Auditor sees the full picture. Without an Aggregator, the orchestrator would need to either (a) read all 12 partials itself (forbidden per the main-thread restrictions above), or (b) trust each Auditor's slice without cross-validation (defeats the parallelism win). The Aggregator is the structural fix: it consolidates, it cross-checks, and its output IS the journal's canonical record. A K=12 dispatch WITHOUT an Aggregator is incomplete; the round is not done until the Aggregator's `### Round N findings` subsection lands in the findings log.

<sup>[↑ jump to top](#top)</sup>

---

## 4. Audit-loop mechanics

The mechanical shape of every audit round. Predicate-of-record for evidence discipline: [rules.md §24 Audit Evidence Discipline](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

> ## ⚠️ MANDATORY ANTI-LAZINESS DIRECTIVE
>
> **DO NOT BE LAZY. WALK EVERY NUMBERED SUBSECTION IN rules.md. NO SKIPPING. NO ASSUMING IRRELEVANCE WITHOUT EVIDENCE. LEAVE NO STONE UNTURNED.**
>
> Short-circuiting the audit ("I checked the relevant ones, the rest don't apply") IS the failure mode this whole framework exists to prevent. Most subsections in rules.md WILL apply to most code. Be skeptical of your own urge to mark N/A. When in doubt, walk the predicate, find the evidence, cite it.
>
> The audit table in each step / final-review journal is the GATE. If the table has fewer rows than there are numbered subsections in rules.md, the audit is INCOMPLETE — the step is NOT done. If a row says "PASS" without a file:line citation, the row is INCOMPLETE. If a row says "N/A" without a step-scope-specific reason, the row is INCOMPLETE.
>
> The cost of walking every predicate is minutes; the cost of skipping one is a future bug + a future audit round.

### Three-artifact journal model

> **Duplicated from [rules.md §24.0 three-artifact journal model](rules/24-audit-evidence-discipline-meta-how-to-audit.md#three-artifact-journal-model-one-big-table--append-only-findings-log--append-only-fix-log) for process-protocol context. The canonical full version with all §24.0/§24.0a-h/§24.13.x predicates lives in rules.md — update both in lockstep when either changes. Annotation per [rules.md §11.32](rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices).**

Every step / final-review journal contains THREE artifacts under canonical headings — strictly separated, never collapsed:

| Artifact                                   | Section heading                       | Behavior                                                                                                                                                                     | Written by                                                                                                                                                                                              |
| ------------------------------------------ | ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Big table** (latest sweep snapshot)      | `## Latest sweep results`             | REPLACED on every sweep — table reflects ONLY the most recent walk's findings against current code. ~85+ rows, one per rules.md subsection. Anti-laziness preamble above it. | Sweep activity ONLY. Fix-applying agents NEVER touch this. Under K=12 dispatch, the **Aggregator** (Fable per [Sub-agent model policy per role](#sub-agent-model-policy-per-role)) writes the merged canonical table; per-cluster Auditors only write to their disposable partial files. |
| **Findings log** (per-round audit history) | `## Sweep findings log (append-only)` | APPEND-ONLY. Each sweep appends a `### Round N findings (timestamp)` subsection enumerating every FINDING the sweep surfaced. Never deleted, never re-ordered.               | Sweep activity ONLY. Under K=12 dispatch, the **Aggregator** (Fable per [Sub-agent model policy per role](#sub-agent-model-policy-per-role)) writes the consolidated round subsection covering all 12 clusters + cross-cluster findings.                                                  |
| **Fix log** (chronological fix activity)   | `## Fix log (append-only)`            | APPEND-ONLY. Each fix appends one entry citing rules.md subsection + finding round + what changed + `file.cs:NN` of the change. Never deleted, never re-ordered.             | Fix-applying agent ONLY.                                                                                                                                                                                |

The big table is the canonical "what is true RIGHT NOW" snapshot. Every PASS in it is a fresh file:line citation against current code, freshly walked in the latest sweep. There is NO inheritance of PASS from earlier sweeps — every PASS is earned fresh each sweep.

Closure is proven ONLY by the absence of a FINDING from the next sweep's big table. The fix log captures intent + action; it does NOT certify outcome.

<sup>[↑ jump to top](#top)</sup>

### Mandatory round sequence

1. **Sweep**: walk every rules.md subsection against current code. REPLACE the big table with the sweep's output. APPEND a `### Round N findings (timestamp)` subsection to the findings log enumerating every FINDING the sweep surfaced.
2. **Fix work**: for each FINDING in the new big table, apply the fix. After each fix, APPEND one entry to the fix log citing the rules.md subsection + finding round + what changed + the `file.cs:NN` of the change. **The big table is NOT touched between sweeps.**
3. **Sister-sweep mandatory** per [rules.md §24.13.3 / §24.13.3d](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — the orchestrator's Fixer dispatch brief MUST name the sister-sweep command + the full applicability path-set + the literal-output-paste requirement. Fixer pastes literal stdout into the fix-log entry as evidence.
4. **Tamper-evident dispatch** per [rules.md §24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — when a finding was previously claimed CLOSED but a subsequent Auditor surfaced it as STILL_PRESENT, OR is a user-flagged special-emphasis target, the Fixer dispatch brief MUST mandate BEFORE/AFTER literal-output pasting (predicate-grep + `git diff --stat`) — the four literal outputs become the fix-log entry's inline evidence.
4a. **Pattern-class scope expansion** per [rules.md §24.28](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — for any finding classified as a pattern-class violation (a convention breach, a leaked token, an anti-pattern that can recur across files), the Fixer dispatch brief MUST name the grep command to run against the FULL deliverable diff scope AND mandate fixing every instance found, not only the auditor-cited file:lines. Partial fixes resurface as STILL-PRESENT.
4b. **Fixer self-grep before returning** per [rules.md §24.29](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — before returning, the Fixer MUST run `git diff HEAD` and grep its own added lines for new instances of the pattern class being fixed, and for conversation-scope tokens / audit-process references / partial cross-links in any doc edits. Any self-introduced hit is fixed in-place before the return. The Fixer's fix-log entry includes a `"Self-grep"` section with the command + literal output.
5. **Every finding gets fixed**: no silent carryover. If a finding genuinely can't be resolved in this round, get EXPLICIT user permission to defer and append a deferral entry to the fix log (still append-only — never silent omission).
6. **Next sweep**: when all current-round findings have fix-log entries, run the NEXT sweep. Walk the full rules.md catalog again from scratch — under K=12, each cluster Auditor re-reads its cluster's category files (per the [§3 per-cluster reading list](#auditor-cluster-partition-canonical-k12)); the 12 clusters together cover the whole catalog. REPLACE the big table with the new sweep's output. Append `### Round N+1 findings` to the findings log. A row that was a FINDING in Round N's findings log and is now PASS in Round N+1's big table = closed (proven by absence). A row STILL a FINDING in Round N+1's table = fix didn't take; append more fix entries, run Round N+2.
7. **Loop terminates** when ONE sweep produces a big table with zero FINDING rows. Until that happens, the step is not done. No "convergence claimed" without a clean big table from a real sweep.

If iteration 11 is reached without convergence, STOP and escalate:

```
=================================================
[YYYY-MM-DD HH:MM] ESCALATION — 10-iteration ceiling reached
=================================================
Pattern of findings across rounds: <summary>
Suspected root cause: <agent's hypothesis>
Question for user: <specific ask>
```

<sup>[↑ jump to top](#top)</sup>

### Plan currency before dispatch

> **Mid-deliverable architectural / scope / approach changes MUST update the deliverable's Plan synchronously — before the next sub-agent is dispatched. Conversation-only ("in MEMORY") decisions are explicitly INVALID as a state to dispatch from.**

The orchestrator carries conversation context across an entire deliverable; sub-agents do not. Every Planner / Implementer / Auditor / Aggregator / Fixer / Plan-amender / Final-reviewer spawns with a fresh context and reads ONLY the artifacts the orchestrator points it at — the journal, the Plan file, rules.md, the shared-context file, and whatever paths the dispatch brief enumerates. The orchestrator's conversation memory is INVISIBLE to every sub-agent. This is the entire point of the fresh-context property (per [§3 Why this is structural, not stylistic](#why-this-is-structural-not-stylistic)) — and it's exactly why architectural pivots that live only in the orchestrator's conversation context will cause the next sub-agent to build against the OLD plan.

**The mandate** — any decision made DURING EXECUTE that contradicts, supersedes, or amends the Plan locked at PLAN exit (or amended in a prior round) MUST be written into the deliverable's Plan artifacts before the orchestrator dispatches the next Planner / Implementer / Auditor / Plan-amender / Fixer / Final-reviewer. This includes:

- Architectural pivots (single-shape vs split-shape, transport vs handler placement, cache topology, sync vs async)
- Naming changes (Option A vs Option B, lib names, type names, interface names, file paths, conventional vocabulary)
- Scope additions / removals (a step grows to include a new file-set, a step sheds work to the next deliverable)
- Ordering changes (step prerequisites shift; sub-steps merge or split)
- Library shape changes (collection shape, public API surface, dependency layout)
- Decision reversals (Option A picked at PLAN, swapped to Option C mid-EXECUTE)
- Cycle-resolution choices (Approach A vs Approach B when a circular-dependency was discovered)
- Cross-cutting reminders that need to fire at multiple later dispatch points (e.g. "remember cache-aside in three places")

**The mechanism** — Plan currency requires ALL THREE updates in the SAME orchestrator turn that locks the decision (not batched, not deferred to "end of step" cleanup):

1. **Journal amendment** — append a new `## Plan amendment N+1 (<UTC timestamp>)` subsection to `docs/wip/<deliverable>/<NN>-<step>/journal.md` enumerating: (a) what changed, (b) what it supersedes / contradicts in the prior Plan, (c) the rationale, (d) the user-quoted authorization if the decision required user permission per §13.5 / §13.14.
2. **Plan file update** — edit `docs/wip/<deliverable>/README.md` so the Living State / Status section + the relevant Step section + the Cross-cutting decisions table all reflect the amended state. Stale prose that contradicts the amendment must be removed or struck-through; future sub-agents reading the Plan must see ONE consistent state, not a Plan that says X while the amendment says Y.
3. **Decisions table row** — append a row to the deliverable's Cross-cutting decisions table (in the root README) citing the amendment number + summarized choice + rejected alternatives + amendment-journal back-reference (`journal.md:NN`). This is the at-a-glance index a future sub-agent uses to find the amendment without re-reading the entire journal.

**The "before next dispatch" gate** — the orchestrator does NOT dispatch the next sub-agent until all three updates above land. Plan-currency verification is a precondition gate that sits ahead of every dispatch protocol step in [Per-round dispatch protocol](#per-round-dispatch-protocol). If the orchestrator is about to write a Planner / Implementer / Auditor / Plan-amender / Fixer / Final-reviewer dispatch brief and the conversation has surfaced decisions that aren't in the Plan yet, the orchestrator STOPS, runs the three-update mechanism, and only then writes the dispatch brief. The brief references the AMENDED Plan as input.

**The failure mode this prevents** — sub-agents have no conversation context; they only see artifacts. When the orchestrator dispatches an Implementer with a brief that points at an out-of-date Plan, the Implementer builds against the OLD architecture — and the Implementer is RIGHT to do so, because the Plan is the contract. The build-the-wrong-thing failure cascades into a downstream Auditor finding ("Implementation doesn't match the architectural pivot we discussed"), a downstream Fixer round, and a downstream re-Implementer round — multiplied across however many sub-agents touched the stale Plan. The cost of the three-update mechanism is one orchestrator turn (~minutes); the cost of skipping it is multiple wall-clock-hour Implementer + Fixer + re-Audit cycles plus the cognitive cost of unwinding partial work that was correct-against-stale-Plan but wrong-against-real-intent.

**Worked example — deliverable 0009-geo-libs Step 3a, Plan amendment 41** (the canonical precedent that codified this rule). During execution, the orchestrator locked six architectural decisions in conversation: single-shape architecture (collapsed split-shape variants), Option B naming convention, Approach A cycle resolution for the IGeoReference cross-package dependency, Option (c) collection shape, two-pass populate pattern, and a cross-cutting cache-aside reminder firing at three later dispatch points. The orchestrator was about to re-dispatch the Implementer when a docs-update pass surfaced that the Plan (`docs/wip/0009-geo-libs/README.md`) still described the pre-amendment architecture. Had the re-dispatch fired against the stale Plan, the Implementer would have rebuilt the prior split-shape architecture with Option A naming — the exact failure mode this rule prevents. The fix was the three-update mechanism above: journal Amendment 41 appended, Plan file's Living State + Step 3a section + Decisions table updated, then the Implementer re-dispatched against the amended Plan. The amendment is referenced as the canonical precedent in [rules.md §24.17](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

**Cross-references:**

- [rules.md §24.17](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — the predicate-level enforcement of this rule (walked every audit round).
- [rules.md §13.5](rules/13-permission-action-discipline.md#13-permission--action-discipline) — the ASK gate that precedes mid-execution Plan amendments (when a deviation surfaces, ASK before deciding). §13.5 governs WHETHER to amend; this section governs HOW to record the amendment once decided. Both apply independently.
- [rules.md §13.13](rules/13-permission-action-discipline.md#13-permission--action-discipline) — Plan-vs-reality reconciliation when runtime / library behavior diverges from a Plan claim. §13.13 is the Implementer-side reconciliation; this section is the orchestrator-side currency gate that ensures the next dispatch sees the reconciled Plan.
- [rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — Plan-Audit before initial Implementer dispatch. §24.16 audits the Plan ONCE at step entry; this section keeps the Plan honest across the rest of EXECUTE.

<sup>[↑ jump to top](#top)</sup>

### Per-round dispatch protocol

The orchestrator's workflow for one K=12 + Aggregator audit round. Same shape for per-step rounds, final-review rounds, AND Plan-Audit rounds (the difference is scope: per-step code-audit = step's touched files; final-review = whole deliverable; Plan-Audit = the journal's `## Plan` section content + the codebase reality the Plan claims to align with — see [rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)).

**Plan-Audit dispatch specifics** (when §24.16 applies): the orchestrator writes a `plan-audit-r{N}-shared-context.md` file (same shape as the code-audit shared-context but mission-scoped to the Plan section + cluster verification questions enumerated in §24.16). Dispatches K=12 Plan-Auditors in parallel (each with `model: "opus"` per §24.0i + the [Sub-agent model policy per role](#sub-agent-model-policy-per-role) canonical table), each scoped to its cluster against the Plan section. After all 12 partials return, dispatches the Aggregator (Fable per the same policy table) to merge → `## Plan-Audit results` lands in the journal BEFORE the Implementer is dispatched. If findings: dispatches Plan-amender (Fable per the policy table — same high-leverage Plan-authoring rationale as Planner) to address each finding + append fix-log entries; then dispatches a fresh K=12 Plan-Audit Round 2 to verify closure. Loop terminates on CLEAN; orchestrator then dispatches the Implementer with the AMENDED Plan as input. K=1 Plan-Audit follows the same explicit-per-round-user-approval discipline as §24.0h. Carve-outs (per §24.16) skip Plan-Audit entirely; the orchestrator log cites the applicable carve-out per occurrence.

**Step 1 — Orchestrator writes the per-round shared-context file:**

Path: `docs/wip/<deliverable>/<NN>-<step>/r{N}-shared-context.md` for per-step rounds, or `docs/wip/<deliverable>/final-review/r{N}-shared-context.md` for final-review rounds.

Contents:

- Mission paragraph (what this round audits, why)
- Locked decisions (so cluster Auditors do not re-litigate)
- Deliverable scope (concrete path-set or `git diff --name-only` recipe)
- Special-emphasis user direction (if any user gave a focus area; e.g. "industry-standard naming alignment", "regression test adequacy for known bug classes")
- The K=12 cluster partition table (verbatim from [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k12) so every Auditor sees the canonical mapping)
- Output format spec (the partial-file template every Auditor writes against — see [Partial-file template](#partial-file-template-per-auditor) below)
- Aggregator role summary (so cluster Auditors know what their partials feed into and can flag cross-cluster handoffs explicitly)
- Critical constraints (READ-ONLY tools, no sub-agent spawning, no commits, no touching other auditors' partial files, sister-sweep per rules.md §24.13.3, self-grep per rules.md §24.13.4)
- **Tool-invisible convention reminder** (include in every shared-context file): "Every Auditor MUST read each modified `.cs` / `.ts` file in scope to scan for the three tool-invisible convention lenses that neither `dotnet build` nor `jb inspectcode` enforce: (a) line length ≤ 100 chars + SA1519/SA1516 cascades; (b) blank line after any multi-line statement before the next statement; (c) `var` for locals where the type is evident. Gate-green does NOT imply convention-clean. Predicate-of-record: rules.md §24.20."
- **Gate-verify scope reminder** (include in every shared-context file): "Gate verification MUST use `dotnet build server/D2.slnx` (or tests-csproj build) AND `jb inspectcode server/D2.slnx --severity=WARNING` at full-solution scope — NOT a per-lib or per-project inspectcode. Lib-scoped inspectcode hides test-file findings. Predicate-of-record: rules.md §24.21."
- **Source xmldoc + comment scan reminder** (include in every shared-context file): "Every Auditor MUST scan modified source files' xmldocs and code comments (in addition to READMEs) for deliverable-step / phase / SHIP / forward-ref / rules-§ / CLAUDE.md-§ framing. Extend §14.1/§14.3/§11.x scope from READMEs to `///` xmldoc, `//` comments, `/* */` blocks, and `.csproj` XML comments. Predicate-of-record: rules.md §24.22."
- **Working-tree note** (include when the deliverable's latest Implementer / Fixer output is uncommitted): "Read all files in this deliverable's scope from the on-disk working tree — the actual source files on disk. Do NOT rely on `git diff HEAD`, `git show HEAD:path`, or any git operation that reads committed content. The deliverable's latest changes are uncommitted and are not present at HEAD. A sub-agent that reads HEAD reports stale pre-change findings and misses genuine post-change issues." Omit this note once every step's Implementer + Fixer output has been committed (working tree and HEAD agree). Predicate-of-record: [rules.md §24.19](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

**Step 2 — Orchestrator dispatches 12 parallel Auditors in ONE message:**

All 12 spawned in a single `Agent` tool batch (one tool-call message containing 12 parallel `Agent` invocations). Each Auditor's brief is small:

- Read the shared-context file at the path above
- Read your cluster's category files from [`rules/`](rules/) end-to-end (the specific files are mapped in the [§3 per-cluster reading list](#auditor-cluster-partition-canonical-k12))
- Skim other clusters' category files (or the [index](rules.md)) for cross-references
- Walk YOUR cluster against the deliverable scope
- Write to your designated partial file `r{N}-partial-{CLUSTER}-{cluster-name}.md` (CLUSTER ∈ {A1, A2, B1, B2, B3, C1, C2, C3, D1, D2, E1, E2}) at the same directory

Concurrent writes are safe because each Auditor owns its own file. There is no shared mutable state between cluster Auditors. Run them as background `Agent` invocations (`run_in_background: true`) and let notifications return as each completes.

**Audit dispatch model discipline.** Auditor sub-agents are dispatched with `model: "opus"` per the canonical [Sub-agent model policy per role](#sub-agent-model-policy-per-role) table (and [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)). **Aggregator runs on Fable** (explicit `model: "fable"` override; Fable's large context window accommodates the 12-partial merge) so it has the context budget to consume 12 partials and the reasoning capability for cross-cluster dedup + sister-sweep. Planner + Plan-amender are also Fable per the policy table (high-leverage low-volume Plan authoring). Implementer + Fixer + Investigator are Opus by default; the Sweeping carve-out (see [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) Sweeping carve-out sub-block) permits Fable escalation when ≥1 of four criteria is met (atomic >40-file dispatch / >3-concern dispatch / cross-runtime refactor / cascading pipeline change). Every K=N Auditor / Plan-Auditor / Final-reviewer `Agent` invocation MUST include `model: "opus"` explicitly; every Fable dispatch under the Sweeping carve-out MUST cite the triggering criterion in both the dispatch brief and the sub-agent's return self-attestation. Canonical predicate-of-record: [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). Verification discipline that compensates for workhorse dispatch: [Orchestrator verification of sub-agent outputs](#orchestrator-verification-of-sub-agent-outputs).

**Step 3 — Orchestrator waits for all 12 partials:**

When ALL 12 background notifications return, the orchestrator dispatches the Aggregator. The orchestrator does NOT read partials directly — it dispatches the Aggregator with the list of partial paths and lets the Aggregator do the merge.

**Step 4 — Orchestrator dispatches the Aggregator (Fable per [Sub-agent model policy per role](#sub-agent-model-policy-per-role)):**

A single `Agent` invocation (foreground OK; the Aggregator is not parallelizable). Brief:

- Read the 12 partials at the listed paths
- Read the deliverable's cross-cutting focus areas (named in the shared-context file)
- Perform the six responsibilities in [§3 Aggregator role](#aggregator-role-post-cluster-consolidation) in order
- Write the canonical big table + `### Round N findings` subsection to the journal
- Return summary

**Step 5 — Orchestrator routes on the Aggregator's recommendation:**

- **CLEAN (zero FINDING rows + zero new cross-cluster findings)**: advance to next phase (next step, or SHIP for final-review).
- **FINDINGS present**: dispatch a fresh Fixer sub-agent with the consolidated finding list. After Fixer returns, dispatch the next round (R+1) — a brand-new K=12 batch + brand-new Aggregator, fresh context across the board.

**Wall-clock expectations:**

- A K=12 batch wall-clock is dominated by the slowest cluster, NOT the sum. Empirically the slowest cluster (typically D1 docs / C2 architectural-layer / E2 audit-meta depending on scope) determines round duration; clusters with thinner scope finish much sooner.
- A round = one K=12 dispatch + Aggregator + (if findings) one Fixer = ~1/4-1/5 of a sequential K=1 walk that covered the same predicate count (K=12 gives ~35-45% wall-clock reduction relative to prior smaller-K splits thanks to tighter per-Auditor focus).
- 10-iteration ceiling per step still applies — where ONE iteration = one full round of K=12 Auditors + Aggregator + (if findings) Fixer.

<sup>[↑ jump to top](#top)</sup>

### Orchestrator verification of sub-agent outputs

> **Trust-but-verify discipline — the structural compensation for dispatching Opus-default workhorse roles per the [Sub-agent model policy per role](#sub-agent-model-policy-per-role) canonical table. Predicate-of-record: [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).**

When the orchestrator dispatches Opus-default workhorse sub-agents (Auditor / Plan-Auditor / Final-reviewer / Implementer / Fixer / Investigator), it takes on additional verification responsibility. Opus is the right capability for predicate-walking + grep verification + structured-brief execution, but the orchestrator cannot blindly accept a sub-agent's outputs as ground truth — the same way it cannot accept Fable outputs as ground truth either. The discipline below IS the structural compensation that makes workhorse dispatch safe: spot-sampling, re-running gate samples, adversarially challenging "all green" reports, re-reading changed files for high-stakes work.

**The discipline is mandatory.** A workhorse dispatch without trust-but-verify follow-up is structurally weaker than the same dispatch followed through. The orchestrator's Fable context budget is partly reserved for the verification work — that's part of why the Orchestrator role itself is Fable per the policy table.

**Specific verification actions** (apply per dispatch type):

1. **Spot-sample partial evidence** (per K=12 audit round) — random-sample 1-2 PASS rows from each cluster Auditor's partial; re-read the cited `file:line` to confirm the evidence is real. Auditors occasionally cite a file:line that doesn't actually carry the cited evidence (the line may have been edited since the cited grep, OR the Auditor synthesized a citation that's adjacent-but-not-exact). Spot-checking 1-2 rows per partial × 12 clusters = 12-24 sampling reads per round; cheap relative to the cost of a missed FINDING shipping.
2. **Re-run gate samples** (per Implementer / Fixer return) — occasionally re-run a build / test / grep the sub-agent claimed passed. Particularly: re-run the pre-flight Evidence greps the sub-agent reported zero-hit; re-run `dotnet build server/D2.slnx` if the sub-agent claimed warning-clean; re-run `jb inspectcode` if claimed clean. Sub-agents occasionally report "build clean" against stale state (the build ran successfully BEFORE their last edit) — re-running locks in the post-edit state.
3. **Adversarial challenge on "all green" reports** — when a sub-agent reports zero findings or "no real bugs surfaced", probe in the next dispatch: "did you exercise corner cases X / Y / Z?" Name specific failure modes the sub-agent should have considered. "All green" without enumeration of what was checked is the most common short-circuit pattern; the adversarial follow-up either surfaces the missed corners or confirms genuine coverage. This is also a workhorse-tier tuning aid — Opus-default returns are more prone to optimistic framing than Fable returns.
4. **Re-read changed files for high-stakes work** — security-touching code (auth flows, JWT validation, secrets handling, IDOR-relevant resolvers), user-visible UI/UX flows (error messages, form validation, redirects), data-touching paths (migrations, dual-writes, rollbacks). For these classes, the orchestrator re-reads the sub-agent's changed files directly — don't trust the sub-agent's summary alone. The cost of one Fable context-window pass over a handful of changed files is dwarfed by the cost of a security regression or data-loss bug shipping.
5. **Re-run verifying grep on Fixer BEFORE/AFTER claims** — Fixer tamper-evident dispatch (per [rules.md §24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) requires the Fixer to paste literal BEFORE-grep + AFTER-grep + BEFORE `git diff --stat` + AFTER `git diff --stat`. The orchestrator occasionally re-runs the verifying grep against current state to confirm the Fixer's AFTER claim matches reality. Fixers occasionally claim AFTER-state that's adjacent-but-not-exact (e.g., the grep returned zero hits because of a regex typo, not because the fix landed).
6. **Re-run environment-touching gate claims from a CLEAN state** (per [rules.md §24.27](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — when a sub-agent reports an "all green" gate whose tests touch machine / OS / global state (the OS certificate / trust store, installed trust roots, globally-installed tools, a machine-wide config), the orchestrator independently re-runs the gate from a clean state (fresh checkout, clean container, or explicit teardown of the candidate mutations) before accepting convergence. A sub-agent's diagnostic work can mutate that state as a side effect — installing a trust root into the OS store to make a handshake succeed, then reporting the suite green — and the pass does not reproduce on a clean checkout. The green is an artifact of the debugging environment, not the code. If the clean re-run fails, re-dispatch a Fixer to make the test self-provision its own state via an isolated fixture (per [rules.md §1.16](rules/01-test-discipline.md#1-test-discipline)).

**Dispatch-brief contracts that support trust-but-verify** — the orchestrator's dispatch briefs to Opus-default workhorse sub-agents EXPLICITLY DEMAND evidence-trail-over-confidence. Specific requirements the brief MUST include:

- **Every PASS row cites file + line** (not "looks good", not "verified ✓") — per [rules.md §24.2](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).
- **Every FINDING row cites the grep / check** that surfaced it (so the orchestrator can re-run the same grep to confirm) — per [rules.md §24.4](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).
- **Every "no bugs surfaced" return is challenged** in the dispatch brief itself: "if you found zero bugs, enumerate the specific failure modes you considered and ruled out" — adversarial framing per [research on adversarial code review](https://asdlc.io/patterns/adversarial-code-review/).
- **Every Fixer BEFORE/AFTER claim is tamper-evident** — literal grep output + `git diff --stat`, not paraphrased — per [rules.md §24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).
- **Every sub-agent return self-attests its model** — per the [Sub-agent model policy per role](#sub-agent-model-policy-per-role) Self-documentation requirement.

**When to escalate to full re-dispatch** — if a verification action surfaces that the sub-agent's output was substantially wrong (cited evidence doesn't exist, claimed-green gates actually fail, claimed-closed findings are still present), the orchestrator re-dispatches a fresh sub-agent (NEW context, possibly Fable-escalated under the Sweeping carve-out if scope justifies) with the verification findings as input. Do NOT prompt the same sub-agent to "fix the discrepancy" — its context is already polluted by the original misfire. Fresh-context restart is the correct response.

**Why this discipline exists structurally** — without trust-but-verify, dispatching a workhorse-tier model to predicate-walking work creates an asymmetric risk: cost savings are real (Opus is materially cheaper per token + materially more available in the user's weekly budget than Fable), but the quality floor depends entirely on the workhorse's first-pass accuracy. Trust-but-verify closes the asymmetry: cost savings are still real (verification reads are dramatically cheaper than full Fable dispatch), AND the quality floor is enforced by orchestrator-side spot-checks. The combined economics dominate Fable-only dispatch for the high-volume workhorse-shape roles.

**Cross-references:**

- [Sub-agent model policy per role](#sub-agent-model-policy-per-role) — the canonical table specifying which roles are Opus-default workhorse roles (and therefore subject to this discipline).
- [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — the predicate-level enforcement of the model policy + the trust-but-verify discipline.
- [rules.md §24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — tamper-evident Fixer dispatch (one specific instance of trust-but-verify, codified as a predicate).
- [rules.md §24.13.3](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — sister-sweep discipline (the Fixer-side mandate the orchestrator's brief enforces).
- [rules.md §24.27](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — clean-environment re-run for environment-touching gate claims (verification action 6 above, codified as a predicate).
- [rules.md §24.28](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — pattern-class scope expansion: Fixer greps full deliverable scope for the class before fixing; partial fixes resurface as STILL-PRESENT.
- [rules.md §24.29](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — Fixer self-grep discipline: before returning, Fixer greps its own diff for self-introduced instances of the pattern class + conversation-scope tokens in doc edits.

<sup>[↑ jump to top](#top)</sup>

### Cross-cluster sister-sweep checklist (Aggregator baseline)

The Aggregator MUST run the following baseline sweeps as part of step 4 (cross-cluster sister-sweep) — regardless of what cluster Auditors found in their partials. Cluster Auditors' sister-sweeps under rules.md §24.13.3 run WITHIN their cluster's predicate scope; the Aggregator's sweeps below run against the FULL DELIVERABLE DIFF SCOPE (typically the path-set from `git diff --name-only nova` minus gitignored paths + `docs/dev/deliverables/` immutable snapshots).

| Sweep                                               | Command (literal — substitute scope)                                                                                                                                                                                               | What it catches                                                                                                                                                                                                                                                                          |
| --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Past-framing** (§11.19 / §11.20)                  | `grep -rEn 'previously\|formerly\|used to\|was consolidated\|migrated from\|prior versions\|Resolved the CRITICAL\|Fixed a latent' <deliverable diff scope>`                                                                       | Historical-narration prose that drifted into KEEP docs / source comments across multiple clusters at once                                                                                                                                                                                |
| **Forward-framing** (§11.28)                        | `grep -rEn 'will be\|going to\|upcoming\|planned\|pending\|awaiting\|transitional\|temporary\|eventually\|future-proof\|once X ships' <deliverable diff scope>`                                                                    | Forward-framing prose describing what DOESN'T exist yet (KEEP docs must describe current reality)                                                                                                                                                                                        |
| **Falsey/Truthy dogfood** (§5.1)                    | `grep -rEn 'string\.IsNullOrEmpty\|string\.IsNullOrWhiteSpace' <deliverable diff scope> --include='*.cs' \| grep -v '/Generated/' \| grep -v '/tests/'`                                                                            | Hand-rolled null/empty checks where `Falsey()` / `Truthy()` applies (Cluster B predicate, but commonly co-occurs with Cluster A correctness fixes)                                                                                                                                       |
| **Line-length** (§7.14)                             | `awk 'length > 100' <deliverable diff scope C# / TS files>`                                                                                                                                                                        | Wide lines introduced anywhere in the deliverable. **Em-dash UTF-8 byte-counting artifact awareness per rules.md §24.13.2** — `awk length` measures BYTES not codepoints; em-dashes (3 bytes) inflate apparent length. Manually re-confirm any borderline hit by visual character count. |
| **Hand-mirrored cross-language constants** (§11.30) | Manual enumeration: identify wire identifiers (header names, error codes, JSON property names, OTel tag names) appearing as string literals in BOTH .NET and TS source within the diff scope, where a spec catalog should own them | Cross-language wire identifiers hand-duplicated instead of spec-cataloged + emitter-generated.                                                                                                                                                                                           |

**Operating rules:**

- **Always full-diff scope, never narrowed.** The cluster Auditor sister-sweep is already cluster-scoped per rules.md §24.13.3. The Aggregator's job is to catch what fell between cluster boundaries — narrowing the Aggregator's sweep to one cluster's scope defeats the purpose.
- **Paste literal command + literal output into the Aggregator's `### Round N findings` subsection** under a `#### Aggregator cross-cluster baseline sweeps` heading. Zero hits = one line per sweep ("§14.1 past-framing: 0 hits"). Non-zero hits = each surfaced as its own consolidated finding with severity + file:line + description + suggested fix, classified per rules.md §24.13.3a dedup rule (originating-predicate classification + additional-predicate provenance).
- **Augment, do not replace.** This checklist is the BASELINE — the Aggregator MAY add deliverable-specific sweeps drawn from the per-deliverable cross-step focus areas defined in the Plan section of the final-review journal. The baseline runs every round; deliverable-specific sweeps run when applicable.
- **New recurring classes feed back into this checklist.** When a cross-cluster sister-sweep class proves valuable across multiple deliverables, propose adding it to the table above in the deliverable's distillation — keeping the checklist a living artifact rather than a static one.

<sup>[↑ jump to top](#top)</sup>

### K=1 carve-out usage policy

The K=1 single-Auditor dispatch is documented in [§3 Sub-agent architecture](#3-sub-agent-architecture) as a possible option for truly tiny scope (one-line config tweak, single-line typo fix), but **the orchestrator MUST NEVER self-invoke K=1 without explicit per-round user permission.** Canonical predicate-of-record: [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

The "narrow scope" / "tamper-evident proof exists" / "mechanical change" / "I already verified the fix" reasoning patterns are NOT valid self-justifications — they are exactly the cheating failure mode this framework exists to prevent (per [CLAUDE.md MANDATORY block 0](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread): "The ONLY bypass is an explicit user request").

**If you think K=1 is appropriate, ASK the user before dispatching.** Write the proposed K=1 justification in your message to the user (what the scope is, why partitioning offers no parallelism win, what coverage guarantees you're forfeiting) and wait for explicit `K=1 approved` before dispatching. Without that explicit per-round approval, the orchestrator defaults to K=12 per [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k12) — every audit round, no exceptions.

**Empirical justification** (the "why" this policy was codified): deliverable 0008-geo-data-pipeline final audit cycle (shipped under K=5; today's canonical default is K=12 — the lesson applies identically to any K>1 baseline). After R-final-1 K=5 batch + Final Fixer, the orchestrator self-invoked K=1 verification reasoning "Fixer changes are narrow and tamper-evident proof exists." That K=1 round surfaced 2 brand-new findings (R-final-V-1 HIGH §14.3 conversation-scoped ID + R-final-V-2 LOW §7.14 line-length residuals — both introduced by Final Fixer 3's new test file) AND a §24.0 process gap (Final Fixer 2 + Final Fixer 3 missing fix-log entries) that the orchestrator had not anticipated. The user then required K=5 dispatch (R-final-2) per CLAUDE.md MANDATORY, which independently surfaced ONE FURTHER finding the K=1 had missed: R-final-3-D-F-1 MEDIUM (cross-doc Tier 3 contradiction in `tools/geo-data-pipeline/README.md` — sister-sweep gap inherited from R-final-1's D-F-3 fix). A second K=5 round (R-final-3) was then required to certify closure. Net outcome: the self-invoked K=1 cost an additional R-final-V round + R-final-2 K=5 round + R-final-3 K=5 round to fully certify SHIP-readiness, plus a process-integrity breach that the user explicitly called out.

**Why secondary K=12 passes are necessary even when prior closures look complete:** K=12 passes don't just verify prior closures — they also catch issues missed in initial passes because different cluster Auditor angles + different cross-cluster sister-sweeps reveal what single-Auditor walks structurally cannot. A K=1 Auditor sees their own §-range only; the 12 K=12 Auditors collectively walk the full catalog with 12 independent fresh-context perspectives, and the Aggregator's cross-cluster sister-sweep (per [Aggregator role](#aggregator-role-post-cluster-consolidation) step 4, run on Fable per [Sub-agent model policy per role](#sub-agent-model-policy-per-role)) catches drift classes that span clusters. The 12 partials + Aggregator structure IS the coverage guarantee; collapsing to K=1 collapses the guarantee.

**How to apply:**

1. **Default**: every audit round per [Per-round dispatch protocol](#per-round-dispatch-protocol) step 2 dispatches K=12. No exceptions, no self-justification.
2. **K=1 candidate identification**: if the orchestrator believes K=1 is appropriate (e.g. step really is a single-line typo fix), the orchestrator writes a proposed-K=1 message to the user enumerating: (a) the exact scope (what's changed), (b) why partitioning offers no parallelism win, (c) what coverage guarantees are forfeited (which cluster perspectives won't be exercised), (d) why the orchestrator believes those forfeitures are acceptable for this scope.
3. **User approval**: the user responds with explicit `K=1 approved` (or equivalent unambiguous approval) per occurrence. Approvals do NOT carry forward to subsequent rounds — every K=1 round needs fresh per-occurrence approval.
4. **Without explicit approval**: dispatch K=12. Even if the orchestrator has previously discussed K=1 with the user, even if the prior round was K=1-approved, every NEW round defaults to K=12 unless freshly approved.
5. **Verification rounds after Fixer**: especially-important target for the policy. The post-Fixer verification round is exactly where the orchestrator is most tempted to rationalize K=1 ("the Fixer's tamper-evident proof shows the change landed; I just need to confirm closure"). That rationalization is the failure mode empirically demonstrated by deliverable 0008 R-final-V (shipped under K=5). Post-Fixer verification rounds default to K=12 per the standard policy; the Fixer's tamper-evident output (per rules.md §24.14) speeds up each cluster Auditor's verification but does NOT eliminate the need for K=12's independent angles + cross-cluster sister-sweep.

### Partial-file template (per Auditor)

Every cluster Auditor writes to its partial file with this structure (cluster code / name / §-range substituted from the partition table). The orchestrator includes this template in the shared-context file so all 12 Auditors produce consistent output the Aggregator can mechanically merge.

```markdown
# R{N} Partial — Cluster {CLUSTER}: {Cluster name}

**Auditor agent**: <agent ID if known>
**Cluster code**: one of A1, A2, B1, B2, B3, C1, C2, C3, D1, D2, E1, E2
**Predicate scope**: §{A}–§{B} ({list cluster sections})
**Sweep timestamp**: <UTC>
**Deliverable HEAD**: `git rev-parse HEAD` + any uncommitted changes from prior Fixer round

## Partial big-table chunk

> Anti-laziness preamble (verbatim from §24): WALK EVERY SUBSECTION in your cluster scope.
> PASS rows require file:line citations. N/A rows require deliverable-scope-specific reasons.
> FINDING rows require severity + file:line + description + fix. Status column prepends
> ✅ / ❌ / ⚪ / 🟡 emoji indicator. NO SHORTCUTS. Per rules.md §24.13.2: regex is a TOOL not source
> of truth — manual reading required. Per rules.md §24.13.3: sister-sweep at full predicate applicability.

| §                                         | Subsection | Status | Evidence |
| ----------------------------------------- | ---------- | ------ | -------- |
| <cluster-scoped rows; ~10-40 per cluster> | ...        | ...    | ...      |

## Cluster-scoped findings

<list every FINDING surfaced by your cluster sweep with severity + file:line + description + fix,
OR "(none — clean cluster sweep)">

## Special-emphasis observations relevant to your cluster

- <observations specific to the user's special-emphasis direction, scoped to your cluster>

## Cross-cluster handoffs to Aggregator

<concerns that span beyond your cluster's predicate scope; e.g. "I noticed something
that's not in §X-§Y but seems like §Z's concern — flagging for Aggregator">
```

<sup>[↑ jump to top](#top)</sup>

### Why the table is sweep-only-replaceable

If the fix-applying agent could flip a row to PASS, failure mode: fix doesn't actually take (typo, wrong line, partial replacement, cascade) → agent writes PASS anyway → next sweep "trusts" the PASS and skips re-walking the predicate → bug ships. With sweep-only-replacement of the big table, every PASS in every sweep's table is freshly walked against current code. There's no possibility of a stale PASS being inherited.

<sup>[↑ jump to top](#top)</sup>

### Why findings + fixes are append-only

The append-only logs preserve the audit trail that the table-replacement model would otherwise lose. Anyone reading the journal can answer: "What did Round 1 find?" "What was changed in response?" "Did Round 2's sweep confirm closure?" An agent that could delete entries could quietly hide reversals or corrections — append-only forces every change (including reversals) into chronological visible order.

Every audit round produces a STRUCTURED TABLE with one row per numbered subsection in `rules.md`. The table is the gate — a step is not done until a complete-table round shows zero FINDING rows.

<sup>[↑ jump to top](#top)</sup>

### Evidence requirements (mechanical, no exceptions)

> **Duplicated from [rules.md §24.2 / §24.3 / §24.4](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) for protocol-context reference. The canonical full version with all evidence-form predicates + emoji-prefix mandate (§24.10) lives in rules.md — update both in lockstep when either changes.**

- **PASS** requires a `file:line` citation pointing to code/test/doc that satisfies the predicate. "Verified ✓" / "looks good" / "checked it" are NOT evidence.
- **N/A** requires a one-sentence REASON specific to the step's scope. "Doesn't apply" / "irrelevant" are NOT reasons. Acceptable reason shapes: "no TS code in this step", "no DI extensions added", "no Redis interaction", etc.
- **FINDING** requires all four: (severity: HIGH/MEDIUM/LOW) + (file:line) + (specific description of the violation) + (suggested fix). Fix is applied in the same round; the next round runs against post-fix state.

**MANDATORY: emoji-prefixed Status column** — every Status cell starts with one of the canonical emoji indicators: `✅ PASS` / `⚪ N/A` / `❌ FINDING-HIGH` / `❌ FINDING-MEDIUM` / `❌ FINDING-LOW` / `🟡 <anything-else>` (e.g. `🟡 DEFERRED` / `🟡 PENDING` / `🟡 PARTIAL`). Visual scan-ability is the goal — operators reviewing the journal can spot findings instantly. A row with a bare status word (no emoji prefix) is a rules.md §24.10 violation.

Example big-table row format:

```
| §    | Predicate                                         | Status            | Evidence / Reason / Finding                              |
|------|---------------------------------------------------|-------------------|----------------------------------------------------------|
| 1.1  | Test every public path first-pass                 | ✅ PASS           | HttpJwksProvider.GetKeysAsync → tests/Jwks/HttpJwksProviderTests.cs:23 |
| 1.2  | Adversarial inputs in tests                       | ❌ FINDING-MEDIUM | tests/Jwks/HttpJwksProviderTests.cs missing oversized-payload case → add test_OversizedJwks_ReturnsServiceUnavailable |
| 1.3  | DI extensions tested via composition resolution   | ⚪ N/A            | No DI extensions added in this step |
```

<sup>[↑ jump to top](#top)</sup>

### Loop count expectations

- A WELL-PLANNED step typically converges in 1-3 sweep rounds.
- A POORLY-PLANNED step (or one introducing complex new patterns) may need 5-8 rounds.
- 10-iteration ceiling per step (per [Mandatory round sequence](#mandatory-round-sequence) above). Iteration 11 = escalate to user — something is structurally wrong.
- Final-review surfaces 0-2 deliverable-wide consistency findings — typically 1-2 sweep rounds.

<sup>[↑ jump to top](#top)</sup>

---

## 5. Self-improvement loop

The catalog of predicates in [rules.md](rules.md) grows over time. Every deliverable's distillation produces proposed predicate additions. Approved additions land in `rules.md`. Over time the catalog approaches "every kind of miss we've ever made has a corresponding gate-check," and the audit loop converges in fewer rounds because predicates fire pre-emptively (the agent sees the predicate during PLAN's pre-emptive gate checks and avoids the miss in the first place).

**Per-step distillation** (after each step's audit terminates CLEAN):

Once the step terminates clean (a fresh Auditor's big table came back with zero FINDING rows), the orchestrator spawns a fresh sub-agent to append the distillation to the step journal:

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

These candidates surface in the root README's "Kinds-of-misses log" so they're visible across steps.

**At SHIP** (after final-review's clean termination):

1. Aggregate proposed rule additions from all step distillations + final-review distillation. Deduplicate.
2. Present the full proposed list to the user as part of the deliverable's root README.
3. User approves / tweaks / rejects each.
4. Approved proposals land in `rules.md` as a committed change before the deliverable's code commit.

**Format for proposing a new predicate** (used in the deliverable's root README "Proposed rule additions to rules.md" section):

```
Category: <existing category number + name, or "NEW: <name>">
Predicate: <Y/N question with required evidence>
Origin:    <which deliverable / step / round surfaced the underlying miss>
Why permanent: <not a one-off; class of miss that will recur without a gate-check>
Examples:  <1-2 specific past instances>
```

User approves / tweaks / rejects per proposal. Approved proposals get appended to `rules.md` as part of ship's commit batch.

Rejected proposals (one-off mistakes not worth a permanent rule) get noted in the deliverable's final report so the reasoning survives.

<sup>[↑ jump to top](#top)</sup>

---

## 6. Appendices

### Appendix A: How this addresses each empirical failure mode

| Failure mode (observed in 0002-auth-inbound)          | How the framework prevents                                                                                                                                                                                         |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Prose-as-evidence drift                               | rules.md §24.2 / §24.3 / §24.4 evidence-form predicates + Auditor adversarial framing.                                                                                                                             |
| Convergence illusion                                  | Orchestrator never marks CLEAN — Auditor does. Fresh sub-agents have no investment in stopping.                                                                                                                    |
| Stale-memory shortcuts                                | Sub-agents have fresh context — no conversation summary to trust.                                                                                                                                                  |
| Scope-narrowing                                       | rules.md §24.13 pre-flight greps + §24.9 anti-laziness preamble.                                                                                                                                                   |
| Self-review leniency bias                             | Auditor is separate sub-agent invocation, not main thread. Adversarial prompt framing.                                                                                                                             |
| Mid-execution tier audits adding cycles without value | Tier audits removed entirely. Per-step audit sufficient because Auditor scope explicitly includes all files step touched (incl. files from prior steps if modified). Per rules.md §24.7.                           |
| Implementer self-marking findings as fixed            | Fixer is separate role; cannot mark anything CLEAN; closure proven by next round's verifier (and per rules.md §24.0b, fixes are recorded EXCLUSIVELY in the append-only fix log, never as edits to the big table). |

### Appendix B: Mapping to Anthropic's five workflow patterns

| Pattern              | Use in this framework                                              |
| -------------------- | ------------------------------------------------------------------ |
| Prompt chaining      | Implementer → Auditor → Aggregator → Fixer is a chain              |
| Routing              | Orchestrator routes based on Aggregator output (CLEAN vs FINDINGS) |
| Parallelization      | K=12 Auditors in parallel                                          |
| Orchestrator-workers | Main thread is orchestrator; sub-agents are workers                |
| Evaluator-optimizer  | Auditor evaluates, Fixer optimizes — looped until clean            |

All five patterns compose into one framework. This is what Anthropic's research system achieves at scale; this design applies the same pattern to the audit loop specifically.

### Appendix C: Trial outcomes from deliverable 0002-auth-inbound

The orchestrator + adversarial sub-agent separation pattern (§3) was trialed across deliverable 0002-auth-inbound (8 steps + final-review + 3 polish rounds) before being promoted to canonical workflow status. This appendix captures empirical outcomes that justified the promotion.

**Two production bugs caught by adversarial separation that single-context implementation would have shipped:**

**Bug 1 — wrong UserState slot in JwtAuthInterceptor:**

`JwtAuthInterceptor.ResolveMethodScopeMetadata` was reading the wrong `UserState` slot — code path that compiled clean and passed the Implementer's unit tests. The miss was caught by an integration test the Implementer had skipped (judging it "thin glue, no logic to test"). A separate Fixer sub-agent was forced to author the integration test as part of resolving the finding; the test then surfaced the wrong-slot read. Single-context implementation would have shipped this — the test that caught it only existed because adversarial separation forced its creation.

**Bug 2 — `act_chain_malformed` dead-letter chain:**

`MalformedActorChainException` propagated uncaught from `ClaimsToContextMapper.Map` → JwtValidator was returning UnhandledException-shaped failures instead of the canonical `act_chain_malformed` AuthErrorCode. The miss was structural: the `AuthFailures.ActChainMalformed` helper existed, the `AuthErrorCodes.ActChainMalformed` constant existed, the `JwtValidator` xmldoc enumerated the outcome, the README documented it — but the validator implementation never emitted the outcome. The mismatch surfaced only at deliverable-wide final-review, when a fresh Final-reviewer sub-agent enumerated "what is documented vs what is actually emitted." A per-step Auditor would have walked just the JwtValidator step and seen consistent code+docs+tests; the cross-cutting gap required the deliverable-wide adversarial walk.

**Convergence in 1-3 rounds (mostly 2):**

Per-step audit loops converged in 1-3 rounds across all 8 steps, with 2 rounds being the modal case. This is the empirical validation that the pattern works at scale — predicate satisfaction can be reached through fresh-context iteration without runaway round counts. The 10-iteration ceiling was never approached.

**Main-thread context stayed small:**

Across the whole deliverable (8 step-level audits + final-review + 3 rounds of polish + cross-deliverable design discussions about the auth-outbound stack), the main-thread context remained well under capacity. This is the key win of orchestrator-only main-thread: domain detail lives in sub-agent contexts that die on return, leaving the orchestrator free to handle long-arc decision-making across many steps.

**User feedback after the trial:**

> "the subagents, while slower to complete work, are actually doing a cleanly better job"

The wall-clock-time tradeoff is real — sub-agent spawning adds overhead per round. But the quality differential in resulting code is the dominant factor; production-bug-catch rate is what justifies the workflow.

**Why this promoted from "trial" to "canonical":**

The trial established three things simultaneously:

1. The pattern catches bugs that single-context implementation ships (concrete: the two bugs above).
2. Convergence is achievable in practice (concrete: 1-3 rounds per step, 8/8 steps reached CLEAN).
3. Main-thread context stays small enough that the orchestrator can drive long deliverables (concrete: 8-step deliverable + 3-round polish + cross-deliverable discussion fit comfortably).

All three together = the pattern is fit-for-purpose for D²-WORX's enterprise-readiness bar. Promotion to canonical removes the per-deliverable "should we use sub-agents this time?" decision and makes adversarial separation the default execution shape.

### Appendix D: Research references

- **[How we built our multi-agent research system — Anthropic engineering](https://www.anthropic.com/engineering/multi-agent-research-system)** — the orchestrator-worker pattern that 90.2%-outperformed single-agent Opus on internal evals. Validates the architectural shape.
- **[Building effective AI agents — Anthropic](https://www.anthropic.com/research/building-effective-agents)** — five composable workflow patterns (prompt chaining, routing, parallelization, orchestrator-workers, evaluator-optimizer). This framework uses orchestrator-workers + evaluator-optimizer.
- **[Building agents with the Claude Agent SDK — Anthropic](https://www.anthropic.com/engineering/building-agents-with-the-claude-agent-sdk)** — SDK ships sub-agents as first-class. Confirms isolated context windows.
- **[Create custom subagents — Claude Code Docs](https://code.claude.com/docs/en/sub-agents)** — fresh isolated context per sub-agent invocation. Context rot avoidance.
- **[Adversarial Code Review pattern — ASDLC](https://asdlc.io/patterns/adversarial-code-review/)** — Critic Agent reviews Builder Agent output. Breaks the self-validation echo chamber.
- **[Why AI Agent Outputs Need Adversarial Review — DEV Community](https://dev.to/rih0z/why-ai-agent-outputs-need-adversarial-review-and-how-to-add-it-in-one-api-call-1l92)** — quantifies LLM self-review leniency bias. Critical: "Most 'agent reviews agent' implementations are one LLM with a clever prompt pretending to be three reviewers, where the model can rubber-stamp itself" — argues for SEPARATE sub-agent invocations, not roleplay.
- **[The checklist manifesto — PMC](https://pmc.ncbi.nlm.nih.gov/articles/PMC4953332/)** — Gawande's surgical checklist research. 19 items, 2 minutes per checklist, 1/3rd reduction in inpatient complications. Lesson: short hierarchical checklists work; long flat ones get skipped. Applied here as 24 categories with recipe decomposition underneath, not 200 flat predicates.

<sup>[↑ jump to top](#top)</sup>
