<!--
Copyright (c) DCSV. All rights reserved.
-->

<a name="top"></a>

# D2-WORX Process — phase lifecycle + orchestrator-only main thread + audit-loop mechanics

The single source of truth for HOW work moves through D²-WORX — phase lifecycle (PLAN → EXECUTE → FINAL-REVIEW → SHIP → REVIEW), permission gates (when to pause for the user), sub-agent architecture (orchestrator + worker roles), audit-loop mechanics (K=5 cluster partition + Aggregator), and self-improvement loop (distillation → rules.md additions).

This doc absorbs the previously-separate `workflow.md` (phase protocol) and `audit-framework.md` (architecture + tooling) into one coherent process reference. Predicate-level enforcement lives in [rules.md](rules.md); pattern reference lives in [../PATTERNS.md](../PATTERNS.md); CLAUDE.md is the agent-directive root that condenses this doc + rules.md for fast-access mental-model purposes.

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
   - [Every round = a NEW fresh sub-agent](#every-round--a-new-fresh-sub-agent)
   - [The orchestrator cannot mark CLEAN](#the-orchestrator-cannot-mark-clean)
   - [Auditor cluster partition (canonical K=5)](#auditor-cluster-partition-canonical-k5)
   - [Aggregator role (post-cluster consolidation)](#aggregator-role-post-cluster-consolidation)
4. [Audit-loop mechanics](#4-audit-loop-mechanics)
   - [Three-artifact journal model](#three-artifact-journal-model)
   - [Mandatory round sequence](#mandatory-round-sequence)
   - [Per-round dispatch protocol](#per-round-dispatch-protocol)
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
- **Cluster** — one of five thematic groupings of `rules.md` predicates (A: correctness + reliability, B: code style + idiom, C: architecture + security, D: documentation + framing, E: operational outcomes + audit meta). The canonical partition for K=5 parallel Auditor dispatch lives in [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k5).
- **Audit round (K=5)** — one full audit pass = 5 parallel cluster Auditors + 1 Aggregator + (if findings) 1 Fixer. The default unit of audit work; sequential K=1 is a carve-out requiring explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules.md#24-audit-evidence-discipline-meta--how-to-audit).


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
```

The Implementer returns a structured files-touched + tests-added + build status summary; its context dies on return. The orchestrator does NOT read the source files itself — it consumes the summary.

**3. Audit loop (the core forcing function):**

For EACH round, the orchestrator dispatches a **K=5 batch of fresh Auditor sub-agents** in parallel (READ-ONLY tools — cannot edit source), then a **fresh Aggregator sub-agent** once all 5 partials return. Each cluster Auditor walks its slice of [rules.md](rules.md) per the canonical 5-cluster partition in [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k5) and produces evidence per predicate — grep results, file:line lists, "checked X by Y, found Z." Vibes ("looks fine") are not evidence. Each Auditor writes to its own partial file (`r{N}-partial-{LETTER}-{cluster-name}.md`); the **Aggregator** merges the 5 partials into the canonical big table (REPLACES `## Latest sweep results`) and appends a single `### Round N findings` subsection covering all 5 clusters + cross-cluster verification (per [§3 Aggregator role](#aggregator-role-post-cluster-consolidation)).

The orchestrator's per-round dispatch workflow lives in [§4 Per-round dispatch protocol](#per-round-dispatch-protocol).

When the Aggregator surfaces FINDING rows, the orchestrator spawns a **fresh Fixer sub-agent** with the consolidated findings list. The Fixer applies fixes + appends fix-log entries — it cannot mark anything CLEAN; closure is proven only by the NEXT round's fresh K=5 Auditor batch + Aggregator walking the predicates again and not surfacing the finding.

A second audit round is a BRAND-NEW K=5 Auditor batch + brand-new Aggregator, not the same ones re-running. The fresh-context property is non-negotiable.

**Wall-clock**: a K=5 batch's wall-clock is dominated by the slowest cluster (not the sum of 5). Empirically ~1/4-1/5 of a sequential K=1 walk against the same predicate count, since parallel Auditors stay in their cluster's mental frame instead of context-switching across all 24 categories.

**K=1 carve-out**: requires explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules.md#24-audit-evidence-discipline-meta--how-to-audit). NEVER self-invoked by the orchestrator.

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
3. Dispatch fresh **K=5 Final-reviewer batch** per round (READ-ONLY) per the canonical cluster partition in [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k5) — same `rules.md` ruleset, scope = whole deliverable. After all 5 partials return, dispatch fresh **Aggregator** to merge. Each round = a brand-new K=5 batch + brand-new Aggregator.
4. Spawn fresh **Fixer** when Aggregator surfaces findings
5. 10-iteration ceiling (where ONE iteration = one K=5 batch + Aggregator + Fixer); escalate if hit
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
6. **Commit** in this order, separately:
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

- **Doesn't replace CLAUDE.md.** CLAUDE.md still defines the agent-directive root + conventions catalog references. This process doc defines the *process* that ensures the conventions are actually followed.
- **Doesn't replace `docs/v2/`.** Phase / wave tracking continues to live in the `docs/v2/` set. This process is per-deliverable; `docs/v2/` is the long-arc roadmap.
- **Doesn't replace per-lib READMEs.** Each shared lib still has its own `README.md` documenting its public API. This process doesn't generate or maintain those.
- **Doesn't run scripts.** No pre-commit hook, no CI gate that fires `rules.md` mechanically. The discipline is the agent walking the rules each round and producing evidence — verifiable by inspecting the journal.


<sup>[↑ jump to top](#top)</sup>

### When to invoke this process

Always, for any work substantial enough to warrant a deliverable folder. The user can override per-task ("just do this small thing, no journal needed" — per [rules.md §13.14](rules.md#13-permission--action-discipline) process-bypass-requires-explicit-naming), but the default is "every meaningful unit of work uses the loop."

The forcing function for the agent: if there's no `docs/wip/<deliverable>/README.md` for the work in flight, the agent should ASK whether to create one before proceeding past PLAN.


<sup>[↑ jump to top](#top)</sup>

---

## 2. Permission gates (when to pause for the user)

The following actions require explicit user permission **per occurrence**, not implied from prior turns. Predicate-of-record: [rules.md §13 Permission / Action Discipline](rules.md#13-permission--action-discipline).

> **Duplicated from [rules.md §13](rules.md#13-permission--action-discipline) for at-a-glance protocol context. The canonical full version with Evidence + Why + How blocks for each predicate lives in rules.md — update both in lockstep when either changes. Annotation per [rules.md §11.32](rules.md#11-documentation-parity--best-practices).**

- **Commit creation** — "go ahead and commit" approves the batch just discussed; the next commit needs fresh permission. (rules.md §13.1)
- **Bulk file operations** (sed across N files, mass rename, multi-file delete, bulk format-write) — agent declares scope (file count, glob, what changes) BEFORE executing; user has the chance to redirect. (rules.md §13.2)
- **Destructive git operations** (force push, hard reset, branch delete, checkout that overwrites uncommitted work) — explicit authorization required. (rules.md §13.3)
- **Deferring planned work** — if a step turns out larger than expected, agent ASKS to defer — does not unilaterally skip. (rules.md §13.4)
- **Architectural decision changes mid-execution** — if implementation surfaces a reason to deviate from the locked PLAN, agent ASKS — does not silently rework. (rules.md §13.5)
- **Process-bypass naming** — every bypass requires per-occurrence user-quoted authorization NAMING the specific rule / step being skipped. Verbal "go ahead" / "looks good" / implicit consent from prior conversation does NOT qualify. (rules.md §13.14)
- **K=1 audit-round dispatch** — never self-invoked; requires explicit per-round user permission with quoted authorization in the orchestrator log. (rules.md §24.0h + cross-ref [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy))


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

| Role | Spawned when | Tool access | Returns |
|---|---|---|---|
| **Planner** | Start of each step | Read, Grep, Glob, Edit (journal Plan section only) | Step Plan section appended to journal + summary |
| **Implementer** | After Planner | All | Files touched + tests added + build / inspectcode status |
| **Auditor** (parallel ×K=5, default) | After Implementer | Read, Grep, Glob, Bash (read-only) | Partial big-table chunk for assigned cluster (see [Auditor cluster partition](#auditor-cluster-partition-canonical-k5)) written to a designated partial file |
| **Aggregator** (one per audit round) | After all 5 Auditors return | Read, Edit (journal + audit artifacts only) | Canonical merged big table embedded in journal + consolidated findings log entry + cross-cluster verification (see [Aggregator role](#aggregator-role-post-cluster-consolidation)) |
| **Fixer** | When findings exist | All | Files changed + appended fix-log entries |
| **Final-reviewer** (parallel ×K=5, deliverable-end only) | Before SHIP | Same as Auditor | Cluster-scoped partial big tables; Aggregator merges as above |

**Key design decisions:**

- **Planner is its own role.** Spawned at the start of each step with the step description + applicable rules.md categories + relevant docs to read. It writes the step's Plan section (goal, files to touch, decisions, pre-emptive gate checks) and returns. The Implementer then receives the Plan as input — fresh context, no exposure to whatever the orchestrator was discussing with the user.
- **Auditors cannot modify source.** Read-only Bash. This makes "audit + fix in same session" structurally impossible — fixes always happen in a separate Fixer invocation, after findings are RECORDED in the journal (no "I fixed it before recording it" sleight-of-hand).
- **Auditor adversarial framing.** Per [adversarial code review research](https://asdlc.io/patterns/adversarial-code-review/): the Auditor prompt explicitly states it's rewarded for finding issues, not for declaring CLEAN. Its role is hostile critic.
- **Parallel cluster dispatch is the default.** K=5 Auditors run concurrently per audit round, each scoped to one cluster of `rules.md` predicates (see [Auditor cluster partition](#auditor-cluster-partition-canonical-k5)). The Aggregator (see [Aggregator role](#aggregator-role-post-cluster-consolidation)) merges the 5 partials into the canonical journal artifacts and performs cross-cluster verification. The orchestrator's dispatch workflow lives in [§4 Per-round dispatch protocol](#per-round-dispatch-protocol).
- **Effort-scaling rules in prompts** (per Anthropic guidance): each sub-agent prompt caps effort proportional to the step's surface area. Small step = "don't write 17 ctor variants for a 1-property record." Cluster scope already constrains per-Auditor effort to ~15-30 predicate rows.
- **Aggregator is load-bearing, never optional.** Whenever K>1 Auditors run, the Aggregator is what produces the canonical big table + consolidated findings log entry the journal commits to. It cannot change cluster Auditor verdicts unilaterally — only dedupes, merges, and adds cross-cluster sister-sweep findings the per-cluster Auditors couldn't see. If two Auditors disagree on the same row (rare; row ownership is partitioned by §-number), the Aggregator escalates to the orchestrator for a tie-breaker Auditor.
- **K=1 carve-out for trivial steps requires explicit user permission.** Per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules.md#24-audit-evidence-discipline-meta--how-to-audit). NEVER self-invoked.


<sup>[↑ jump to top](#top)</sup>

### Every round = a NEW fresh sub-agent

A second audit round is a brand-new Auditor sub-agent, NOT the same Auditor "running again." A fix follow-up after a Fixer's first attempt is a brand-new Fixer. The fresh-context property is the entire point — it's what prevents leniency / motivated-stopping / stale-memory failure modes. Reusing context across roles defeats the whole pattern.

The orchestrator never short-circuits this for "quick" work. A one-line typo fix still spawns a Planner, Implementer, Auditor, and (if findings) Fixer. Sub-agent invocation cost is small; production regression cost is large.


<sup>[↑ jump to top](#top)</sup>

### The orchestrator cannot mark CLEAN

The orchestrator consumes Auditor verdicts; it cannot promote a step to CLEAN by judgment. CLEAN means "the latest Auditor sub-agent's big table contained zero FINDING rows." If the orchestrator wants to confirm closure, it spawns a fresh Auditor — it does not eyeball.


<sup>[↑ jump to top](#top)</sup>

### Auditor cluster partition (canonical K=5)

The `rules.md` catalog (~24 categories, ~145 numbered subsections) partitions into 5 thematic clusters. Each Auditor sub-agent owns exactly one cluster and walks every numbered subsection inside that cluster against the step's (or deliverable's, at final-review) file scope. The partition is fixed — orchestrator dispatch consistently sends the same §-range to the same cluster name across deliverables so accumulated muscle memory carries forward.

| Cluster | Name | rules.md sections | Connecting thread |
|---|---|---|---|
| **A** | Correctness + reliability | §1, §2, §4, §15, §18, §22 | "Does the code behave correctly across edge cases + failure modes." Tests, regression-pinning, concurrency / races, object disposal / resource lifetime, graceful degradation, idempotency / exactly-once. |
| **B** | Code style + idiom + shared-lib hygiene | §5, §6, §7, §16, §17 | "Is the code written per project idiom + leveraging the right tools." C# conventions, TS / SvelteKit conventions, naming / file headers / folder casing, OOTB shared-lib catalog use, D2Result usage + extensions. |
| **C** | Architecture + security + permission discipline | §3, §8, §9, §10, §13 | "Is the architecture sound + secure + safe to operate." PII / logging safety, build cleanliness, architectural layer hygiene, security (endpoints / auth / secrets / input), permission / action discipline. |
| **D** | Documentation + framing + i18n | §11, §12, §14 | "Is the project communicable + maintainable + free of conversation pollution." KEEP-doc updates + forward-framing, i18n / Paraglide / TK constants, no-phase-verbiage / no-conversation-scoped-IDs hygiene. |
| **E** | Operational outcomes + audit meta | §19, §20, §21, §23, §24 | "Is the system operable + the process self-improving." UX, DX, observability completeness, configuration hygiene, audit evidence discipline (incl. self-audit per §24.12). |

**Why this partition:**

- **Thematically cohesive**: each cluster's connecting thread is one coherent mental model — Auditor can stay in one frame of mind for the full walk instead of context-switching across orthogonal concerns.
- **Roughly balanced load**: each cluster carries ~15-30 numbered subsections, so wall-clock-per-Auditor is comparable. No cluster dominates.
- **Stable §-ownership**: the same §-range maps to the same cluster letter across every deliverable. A repeat finding's history can be threaded through past partials by cluster letter.
- **Cross-cutting concerns belong to the Aggregator**, not to any one cluster — the Aggregator's cross-cluster sister-sweep responsibility is what catches concerns that span clusters (e.g. a security predicate in §10 whose fix has style implications in §5, or a doc-framing concern in §11 that needs architectural verification in §9).

**When a predicate seems to straddle clusters:** the cluster mapping is `rules.md` §-number → cluster letter, NOT topic → cluster letter. If a predicate's spirit feels like it belongs to two clusters, the §-number wins. The Aggregator's cross-cluster verification step (see [Aggregator role](#aggregator-role-post-cluster-consolidation) step 4) is where straddle concerns get resolved — not in the per-cluster Auditor walk.


<sup>[↑ jump to top](#top)</sup>

### Aggregator role (post-cluster consolidation)

The Aggregator is a single sub-agent spawned per audit round AFTER all K=5 cluster Auditors have returned their partials. It is the journal's authoritative writer for the round — the per-cluster Auditors write to disposable partial files; the Aggregator alone writes to the canonical journal sections.

**Six responsibilities (in order):**

1. **Mechanical merge.** Read all 5 partial files (`r{N}-partial-{A|B|C|D|E}-{cluster-name}.md` in the round's working dir). Combine the 5 partial big-table chunks into ONE canonical sorted-by-§ big table. Write that table under `## Latest sweep results` in the step / final-review journal, REPLACING the prior sweep's table per the §24 sweep-replacement rule. Anti-laziness preamble appears verbatim above.
2. **Dedupe.** Same finding surfaced by multiple Auditors (e.g. a line-length violation Cluster B owns by predicate, but Cluster D also flagged from a doc-citation angle) collapses into a single entry with combined provenance. Dedupe preserves all citation paths in the entry's description.
3. **Cross-cutting verification.** Walk the deliverable's cross-step focus areas that span multiple clusters — defined per-deliverable in the Plan section of the final-review journal (e.g. "TYPE LIE FIX still verified end-to-end across .NET emitter + TS consumer", "β routing correctness across both consumers", "wire-shape leakage class — does the originating sample class exist anywhere else in scope?", "spec-driven catalog parity — every cross-language wire identifier cataloged + parity-tested"). These are the concerns no per-cluster Auditor could see because they cross §-ranges.
4. **Cross-cluster sister-sweep.** Per rules.md §24.13.3, cluster Auditors sister-sweep WITHIN their cluster's §-scope. The Aggregator runs sister-sweeps at the CROSS-cluster scope. See [§4 Cross-cluster sister-sweep checklist](#cross-cluster-sister-sweep-checklist-aggregator-baseline) for the concrete baseline commands the Aggregator runs every round regardless of cluster Auditor coverage.
5. **Append findings log.** Write a single `### Round N findings (<UTC>)` subsection under `## Sweep findings log (append-only)` in the journal containing: the consolidated finding list (from steps 2-4), a closure-verification table for prior-round findings (each prior-round finding annotated as CLOSED-by-absence in this round's big table OR STILL-PRESENT requiring another fix cycle), and a regression-verification table where applicable (prior-round PASS rows the Aggregator spot-confirmed are still PASS so cascading regressions are caught).
6. **Return summary to orchestrator.** A structured one-paragraph summary: total findings count by severity, list of fix-required §-rows, recommendation (CLEAN → next phase OR findings → spawn Fixer with specific scope).

**What the Aggregator cannot do:**

- **Cannot change per-cluster verdicts unilaterally.** A row Cluster B PASSed cannot be flipped to FINDING by the Aggregator without escalating to the orchestrator for a tie-breaker Auditor. The Aggregator can ADD findings (from steps 3-4 cross-cluster verification) but cannot OVERRULE Auditors.
- **Cannot touch source / tests / configs.** Write access is journal + audit artifacts only.
- **Cannot mark the step CLEAN.** It RECOMMENDS clean to the orchestrator; the orchestrator consumes the recommendation along with the big table itself (which must contain zero FINDING rows for CLEAN status to be valid).

**Why the Aggregator is load-bearing:** when K>1 Auditors run in parallel, no single Auditor sees the full picture. Without an Aggregator, the orchestrator would need to either (a) read all 5 partials itself (forbidden per the main-thread restrictions above), or (b) trust each Auditor's slice without cross-validation (defeats the parallelism win). The Aggregator is the structural fix: it consolidates, it cross-checks, and its output IS the journal's canonical record. A K=5 dispatch WITHOUT an Aggregator is incomplete; the round is not done until the Aggregator's `### Round N findings` subsection lands in the findings log.


<sup>[↑ jump to top](#top)</sup>

---

## 4. Audit-loop mechanics

The mechanical shape of every audit round. Predicate-of-record for evidence discipline: [rules.md §24 Audit Evidence Discipline](rules.md#24-audit-evidence-discipline-meta--how-to-audit).

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

> **Duplicated from [rules.md §24.0 three-artifact journal model](rules.md#three-artifact-journal-model-one-big-table--append-only-findings-log--append-only-fix-log) for process-protocol context. The canonical full version with all §24.0/§24.0a-h/§24.13.x predicates lives in rules.md — update both in lockstep when either changes. Annotation per [rules.md §11.32](rules.md#11-documentation-parity--best-practices).**

Every step / final-review journal contains THREE artifacts under canonical headings — strictly separated, never collapsed:

| Artifact | Section heading | Behavior | Written by |
|---|---|---|---|
| **Big table** (latest sweep snapshot) | `## Latest sweep results` | REPLACED on every sweep — table reflects ONLY the most recent walk's findings against current code. ~85+ rows, one per rules.md subsection. Anti-laziness preamble above it. | Sweep activity ONLY. Fix-applying agents NEVER touch this. Under K=5 dispatch, the **Aggregator** writes the merged canonical table; per-cluster Auditors only write to their disposable partial files. |
| **Findings log** (per-round audit history) | `## Sweep findings log (append-only)` | APPEND-ONLY. Each sweep appends a `### Round N findings (timestamp)` subsection enumerating every FINDING the sweep surfaced. Never deleted, never re-ordered. | Sweep activity ONLY. Under K=5 dispatch, the **Aggregator** writes the consolidated round subsection covering all 5 clusters + cross-cluster findings. |
| **Fix log** (chronological fix activity) | `## Fix log (append-only)` | APPEND-ONLY. Each fix appends one entry citing rules.md subsection + finding round + what changed + `file.cs:NN` of the change. Never deleted, never re-ordered. | Fix-applying agent ONLY. |

The big table is the canonical "what is true RIGHT NOW" snapshot. Every PASS in it is a fresh file:line citation against current code, freshly walked in the latest sweep. There is NO inheritance of PASS from earlier sweeps — every PASS is earned fresh each sweep.

Closure is proven ONLY by the absence of a FINDING from the next sweep's big table. The fix log captures intent + action; it does NOT certify outcome.


<sup>[↑ jump to top](#top)</sup>

### Mandatory round sequence

1. **Sweep**: walk every rules.md subsection against current code. REPLACE the big table with the sweep's output. APPEND a `### Round N findings (timestamp)` subsection to the findings log enumerating every FINDING the sweep surfaced.
2. **Fix work**: for each FINDING in the new big table, apply the fix. After each fix, APPEND one entry to the fix log citing the rules.md subsection + finding round + what changed + the `file.cs:NN` of the change. **The big table is NOT touched between sweeps.**
3. **Sister-sweep mandatory** per [rules.md §24.13.3 / §24.13.3d](rules.md#24-audit-evidence-discipline-meta--how-to-audit) — the orchestrator's Fixer dispatch brief MUST name the sister-sweep command + the full applicability path-set + the literal-output-paste requirement. Fixer pastes literal stdout into the fix-log entry as evidence.
4. **Tamper-evident dispatch** per [rules.md §24.14](rules.md#24-audit-evidence-discipline-meta--how-to-audit) — when a finding was previously claimed CLOSED but a subsequent Auditor surfaced it as STILL_PRESENT, OR is a user-flagged special-emphasis target, the Fixer dispatch brief MUST mandate BEFORE/AFTER literal-output pasting (predicate-grep + `git diff --stat`) — the four literal outputs become the fix-log entry's inline evidence.
5. **Every finding gets fixed**: no silent carryover. If a finding genuinely can't be resolved in this round, get EXPLICIT user permission to defer and append a deferral entry to the fix log (still append-only — never silent omission).
6. **Next sweep**: when all current-round findings have fix-log entries, run the NEXT sweep. Walk the full rules.md catalog again from scratch. REPLACE the big table with the new sweep's output. Append `### Round N+1 findings` to the findings log. A row that was a FINDING in Round N's findings log and is now PASS in Round N+1's big table = closed (proven by absence). A row STILL a FINDING in Round N+1's table = fix didn't take; append more fix entries, run Round N+2.
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

### Per-round dispatch protocol

The orchestrator's workflow for one K=5 + Aggregator audit round. Same shape for per-step rounds and final-review rounds (the difference is scope: per-step = step's touched files; final-review = whole deliverable).

**Step 1 — Orchestrator writes the per-round shared-context file:**

Path: `docs/wip/<deliverable>/<NN>-<step>/r{N}-shared-context.md` for per-step rounds, or `docs/wip/<deliverable>/final-review/r{N}-shared-context.md` for final-review rounds.

Contents:

- Mission paragraph (what this round audits, why)
- Locked decisions (so cluster Auditors do not re-litigate)
- Deliverable scope (concrete path-set or `git diff --name-only` recipe)
- Special-emphasis user direction (if any user gave a focus area; e.g. "industry-standard naming alignment", "regression test adequacy for known bug classes")
- The K=5 cluster partition table (verbatim from [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k5) so every Auditor sees the canonical mapping)
- Output format spec (the partial-file template every Auditor writes against — see [Partial-file template](#partial-file-template-per-auditor) below)
- Aggregator role summary (so cluster Auditors know what their partials feed into and can flag cross-cluster handoffs explicitly)
- Critical constraints (READ-ONLY tools, no sub-agent spawning, no commits, no touching other auditors' partial files, sister-sweep per rules.md §24.13.3, self-grep per rules.md §24.13.4)

**Step 2 — Orchestrator dispatches 5 parallel Auditors in ONE message:**

All 5 spawned in a single `Agent` tool batch (one tool-call message containing 5 parallel `Agent` invocations). Each Auditor's brief is small:

- Read the shared-context file at the path above
- Read your cluster's §-range from `rules.md` end-to-end
- Skim other cluster ranges for cross-references
- Walk YOUR cluster against the deliverable scope
- Write to your designated partial file `r{N}-partial-{LETTER}-{cluster-name}.md` at the same directory

Concurrent writes are safe because each Auditor owns its own file. There is no shared mutable state between cluster Auditors. Run them as background `Agent` invocations (`run_in_background: true`) and let notifications return as each completes.

**Audit dispatch model discipline.** Auditor sub-agents are dispatched with `model: "sonnet"` to preserve Opus context for synthesis + execution roles. Aggregator + Planner + Implementer + Fixer inherit the parent's default model (typically Opus) — no override needed. Why: predicate-walking + grep-verification is Sonnet-shaped work; Opus is over-spec'd for it; the orchestrator's Opus context is best spent on synthesis (Aggregator) and execution (Implementer / Fixer). Every K=N Auditor `Agent` invocation MUST include `model: "sonnet"` explicitly. Canonical predicate-of-record: [rules.md §24.0i](rules.md#24-audit-evidence-discipline-meta--how-to-audit).

**Step 3 — Orchestrator waits for all 5 partials:**

When ALL 5 background notifications return, the orchestrator dispatches the Aggregator. The orchestrator does NOT read partials directly — it dispatches the Aggregator with the list of partial paths and lets the Aggregator do the merge.

**Step 4 — Orchestrator dispatches the Aggregator:**

A single `Agent` invocation (foreground OK; the Aggregator is not parallelizable). Brief:

- Read the 5 partials at the listed paths
- Read the deliverable's cross-cutting focus areas (named in the shared-context file)
- Perform the six responsibilities in [§3 Aggregator role](#aggregator-role-post-cluster-consolidation) in order
- Write the canonical big table + `### Round N findings` subsection to the journal
- Return summary

**Step 5 — Orchestrator routes on the Aggregator's recommendation:**

- **CLEAN (zero FINDING rows + zero new cross-cluster findings)**: advance to next phase (next step, or SHIP for final-review).
- **FINDINGS present**: dispatch a fresh Fixer sub-agent with the consolidated finding list. After Fixer returns, dispatch the next round (R+1) — a brand-new K=5 batch + brand-new Aggregator, fresh context across the board.

**Wall-clock expectations:**

- A K=5 batch wall-clock is dominated by the slowest cluster, NOT the sum. Empirically the slowest cluster (typically Cluster B style / D docs depending on scope) determines round duration; clusters with thinner scope finish much sooner.
- A round = one K=5 dispatch + Aggregator + (if findings) one Fixer = ~1/4-1/5 of a sequential K=1 walk that covered the same predicate count.
- 10-iteration ceiling per step still applies — where ONE iteration = one full round of K=5 Auditors + Aggregator + (if findings) Fixer.


<sup>[↑ jump to top](#top)</sup>

### Cross-cluster sister-sweep checklist (Aggregator baseline)

The Aggregator MUST run the following baseline sweeps as part of step 4 (cross-cluster sister-sweep) — regardless of what cluster Auditors found in their partials. Cluster Auditors' sister-sweeps under rules.md §24.13.3 run WITHIN their cluster's predicate scope; the Aggregator's sweeps below run against the FULL DELIVERABLE DIFF SCOPE (typically the path-set from `git diff --name-only nova` minus gitignored paths + `docs/dev/deliverables/` immutable snapshots).

| Sweep | Command (literal — substitute scope) | What it catches |
|---|---|---|
| **Past-framing** (§11.19 / §11.20) | `grep -rEn 'previously\|formerly\|used to\|was consolidated\|migrated from\|prior versions\|Resolved the CRITICAL\|Fixed a latent' <deliverable diff scope>` | Historical-narration prose that drifted into KEEP docs / source comments across multiple clusters at once |
| **Forward-framing** (§11.28) | `grep -rEn 'will be\|going to\|upcoming\|planned\|pending\|awaiting\|transitional\|temporary\|eventually\|future-proof\|once X ships' <deliverable diff scope>` | Forward-framing prose describing what DOESN'T exist yet (KEEP docs must describe current reality) |
| **Falsey/Truthy dogfood** (§5.1) | `grep -rEn 'string\.IsNullOrEmpty\|string\.IsNullOrWhiteSpace' <deliverable diff scope> --include='*.cs' \| grep -v '/Generated/' \| grep -v '/tests/'` | Hand-rolled null/empty checks where `Falsey()` / `Truthy()` applies (Cluster B predicate, but commonly co-occurs with Cluster A correctness fixes) |
| **Line-length** (§7.14) | `awk 'length > 100' <deliverable diff scope C# / TS files>` | Wide lines introduced anywhere in the deliverable. **Em-dash UTF-8 byte-counting artifact awareness per rules.md §24.13.2** — `awk length` measures BYTES not codepoints; em-dashes (3 bytes) inflate apparent length. Manually re-confirm any borderline hit by visual character count. |
| **Hand-mirrored cross-language constants** (§11.30) | Manual enumeration: identify wire identifiers (header names, error codes, JSON property names, OTel tag names) appearing as string literals in BOTH .NET and TS source within the diff scope, where a spec catalog should own them | Cross-language wire identifiers hand-duplicated instead of spec-cataloged + emitter-generated. |

**Operating rules:**

- **Always full-diff scope, never narrowed.** The cluster Auditor sister-sweep is already cluster-scoped per rules.md §24.13.3. The Aggregator's job is to catch what fell between cluster boundaries — narrowing the Aggregator's sweep to one cluster's scope defeats the purpose.
- **Paste literal command + literal output into the Aggregator's `### Round N findings` subsection** under a `#### Aggregator cross-cluster baseline sweeps` heading. Zero hits = one line per sweep ("§14.1 past-framing: 0 hits"). Non-zero hits = each surfaced as its own consolidated finding with severity + file:line + description + suggested fix, classified per rules.md §24.13.3a dedup rule (originating-predicate classification + additional-predicate provenance).
- **Augment, do not replace.** This checklist is the BASELINE — the Aggregator MAY add deliverable-specific sweeps drawn from the per-deliverable cross-step focus areas defined in the Plan section of the final-review journal. The baseline runs every round; deliverable-specific sweeps run when applicable.
- **New recurring classes feed back into this checklist.** When a cross-cluster sister-sweep class proves valuable across multiple deliverables, propose adding it to the table above in the deliverable's distillation — keeping the checklist a living artifact rather than a static one.


<sup>[↑ jump to top](#top)</sup>

### K=1 carve-out usage policy

The K=1 single-Auditor dispatch is documented in [§3 Sub-agent architecture](#3-sub-agent-architecture) as a possible option for truly tiny scope (one-line config tweak, single-line typo fix), but **the orchestrator MUST NEVER self-invoke K=1 without explicit per-round user permission.** Canonical predicate-of-record: [rules.md §24.0h](rules.md#24-audit-evidence-discipline-meta--how-to-audit).

The "narrow scope" / "tamper-evident proof exists" / "mechanical change" / "I already verified the fix" reasoning patterns are NOT valid self-justifications — they are exactly the cheating failure mode this framework exists to prevent (per [CLAUDE.md MANDATORY block 0](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread): "The ONLY bypass is an explicit user request").

**If you think K=1 is appropriate, ASK the user before dispatching.** Write the proposed K=1 justification in your message to the user (what the scope is, why partitioning offers no parallelism win, what coverage guarantees you're forfeiting) and wait for explicit `K=1 approved` before dispatching. Without that explicit per-round approval, the orchestrator defaults to K=5 per [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k5) — every audit round, no exceptions.

**Empirical justification** (the "why" this policy was codified): deliverable 0008-geo-data-pipeline final audit cycle. After R-final-1 K=5 + Final Fixer, the orchestrator self-invoked K=1 verification reasoning "Fixer changes are narrow and tamper-evident proof exists." That K=1 round surfaced 2 brand-new findings (R-final-V-1 HIGH §14.3 conversation-scoped ID + R-final-V-2 LOW §7.14 line-length residuals — both introduced by Final Fixer 3's new test file) AND a §24.0 process gap (Final Fixer 2 + Final Fixer 3 missing fix-log entries) that the orchestrator had not anticipated. The user then required full K=5 dispatch (R-final-2) per CLAUDE.md MANDATORY, which independently surfaced ONE FURTHER finding the K=1 had missed: R-final-3-D-F-1 MEDIUM (cross-doc Tier 3 contradiction in `tools/geo-data-pipeline/README.md` — sister-sweep gap inherited from R-final-1's D-F-3 fix). A second K=5 round (R-final-3) was then required to certify closure. Net outcome: the self-invoked K=1 cost an additional R-final-V round + R-final-2 K=5 round + R-final-3 K=5 round to fully certify SHIP-readiness, plus a process-integrity breach that the user explicitly called out.

**Why secondary K=5 passes are load-bearing even when prior closures look complete:** K=5 passes don't just verify prior closures — they also catch issues missed in initial passes because different cluster Auditor angles + different cross-cluster sister-sweeps reveal what single-Auditor walks structurally cannot. A K=1 Auditor sees their own §-range only; the 5 K=5 Auditors collectively walk the full catalog with 5 independent fresh-context perspectives, and the Aggregator's cross-cluster sister-sweep (per [Aggregator role](#aggregator-role-post-cluster-consolidation) step 4) catches drift classes that span clusters. The 5 partials + Aggregator structure IS the coverage guarantee; collapsing to K=1 collapses the guarantee.

**How to apply:**

1. **Default**: every audit round per [Per-round dispatch protocol](#per-round-dispatch-protocol) step 2 dispatches K=5. No exceptions, no self-justification.
2. **K=1 candidate identification**: if the orchestrator believes K=1 is appropriate (e.g. step really is a single-line typo fix), the orchestrator writes a proposed-K=1 message to the user enumerating: (a) the exact scope (what's changed), (b) why partitioning offers no parallelism win, (c) what coverage guarantees are forfeited (which cluster perspectives won't be exercised), (d) why the orchestrator believes those forfeitures are acceptable for this scope.
3. **User approval**: the user responds with explicit `K=1 approved` (or equivalent unambiguous approval) per occurrence. Approvals do NOT carry forward to subsequent rounds — every K=1 round needs fresh per-occurrence approval.
4. **Without explicit approval**: dispatch K=5. Even if the orchestrator has previously discussed K=1 with the user, even if the prior round was K=1-approved, every NEW round defaults to K=5 unless freshly approved.
5. **Verification rounds after Fixer**: especially-important target for the policy. The post-Fixer verification round is exactly where the orchestrator is most tempted to rationalize K=1 ("the Fixer's tamper-evident proof shows the change landed; I just need to confirm closure"). That rationalization is the failure mode empirically demonstrated by deliverable 0008 R-final-V. Post-Fixer verification rounds default to K=5 per the standard policy; the Fixer's tamper-evident output (per rules.md §24.14) speeds up each cluster Auditor's verification but does NOT eliminate the need for K=5's independent angles + cross-cluster sister-sweep.

### Partial-file template (per Auditor)

Every cluster Auditor writes to its partial file with this structure (cluster letter / name / §-range substituted from the partition table). The orchestrator includes this template in the shared-context file so all 5 Auditors produce consistent output the Aggregator can mechanically merge.

```markdown
# R{N} Partial — Cluster {LETTER}: {Cluster name}

**Auditor agent**: <agent ID if known>
**Predicate scope**: §{A}–§{B} ({list cluster sections})
**Sweep timestamp**: <UTC>
**Deliverable HEAD**: `git rev-parse HEAD` + any uncommitted changes from prior Fixer round

## Partial big-table chunk

> Anti-laziness preamble (verbatim from §24): WALK EVERY SUBSECTION in your cluster scope.
> PASS rows require file:line citations. N/A rows require deliverable-scope-specific reasons.
> FINDING rows require severity + file:line + description + fix. Status column prepends
> ✅ / ❌ / ⚪ / 🟡 emoji indicator. NO SHORTCUTS. Per rules.md §24.13.2: regex is a TOOL not source
> of truth — manual reading required. Per rules.md §24.13.3: sister-sweep at full predicate applicability.

| § | Subsection | Status | Evidence |
|---|---|---|---|
| <cluster-scoped rows; ~15-30 per cluster> | ... | ... | ... |

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

> **Duplicated from [rules.md §24.2 / §24.3 / §24.4](rules.md#24-audit-evidence-discipline-meta--how-to-audit) for protocol-context reference. The canonical full version with all evidence-form predicates + emoji-prefix mandate (§24.10) lives in rules.md — update both in lockstep when either changes.**

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

| Failure mode (observed in 0002-auth-inbound) | How the framework prevents |
|---|---|
| Prose-as-evidence drift | rules.md §24.2 / §24.3 / §24.4 evidence-form predicates + Auditor adversarial framing. |
| Convergence illusion | Orchestrator never marks CLEAN — Auditor does. Fresh sub-agents have no investment in stopping. |
| Stale-memory shortcuts | Sub-agents have fresh context — no conversation summary to trust. |
| Scope-narrowing | rules.md §24.13 pre-flight greps + §24.9 anti-laziness preamble. |
| Self-review leniency bias | Auditor is separate sub-agent invocation, not main thread. Adversarial prompt framing. |
| Mid-execution tier audits adding cycles without value | Tier audits removed entirely. Per-step audit sufficient because Auditor scope explicitly includes all files step touched (incl. files from prior steps if modified). Per rules.md §24.7. |
| Implementer self-marking findings as fixed | Fixer is separate role; cannot mark anything CLEAN; closure proven by next round's verifier (and per rules.md §24.0b, fixes are recorded EXCLUSIVELY in the append-only fix log, never as edits to the big table). |

### Appendix B: Mapping to Anthropic's five workflow patterns

| Pattern | Use in this framework |
|---|---|
| Prompt chaining | Implementer → Auditor → Aggregator → Fixer is a chain |
| Routing | Orchestrator routes based on Aggregator output (CLEAN vs FINDINGS) |
| Parallelization | K=5 Auditors in parallel |
| Orchestrator-workers | Main thread is orchestrator; sub-agents are workers |
| Evaluator-optimizer | Auditor evaluates, Fixer optimizes — looped until clean |

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
