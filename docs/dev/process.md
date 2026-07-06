<!--
Copyright (c) DCSV. All rights reserved.
-->

<a name="top"></a>

# D2-WORX Process — phase lifecycle + orchestrator-only main thread + audit-loop mechanics

The single source of truth for HOW work moves through D²-WORX — phase lifecycle (PLAN → EXECUTE → FINAL-REVIEW → SHIP → REVIEW), permission gates, sub-agent architecture (orchestrator + worker roles), audit-loop mechanics (K=12 cluster partition + Aggregator), and the self-improvement loop (distillation → rules.md additions).

Predicate-level enforcement lives in [rules.md](rules.md); pattern reference in [../PATTERNS.md](../PATTERNS.md); CLAUDE.md condenses this doc + rules.md.

> **Read [rules.md](rules.md) end-to-end at the start of every deliverable's PLAN phase.** It is the central requirements catalog (security, races, naming, disposal, D2Result, OOTB libs, logging, PII, degradation, UX, DX, observability, idempotency, config, and more). Knowing the rules upfront is what lets code pass audit round 1 instead of round 5. Designed for AGENT ergonomics first, human readability second.

## Table of contents

1. [Phase lifecycle](#1-phase-lifecycle) — [Glossary](#glossary) · [Folder shape](#folder-shape) · [PLAN](#plan) · [EXECUTE](#execute) · [FINAL-REVIEW](#final-review) · [SHIP](#ship-handoff-to-user-review) · [REVIEW](#review-user-phase) · [Append-only discipline](#append-only-discipline) · [Scope of work shape](#scope-of-work-shape) · [What this does NOT do](#what-this-process-does-not-do) · [When to invoke](#when-to-invoke-this-process)
2. [Permission gates (when to pause for the user)](#2-permission-gates-when-to-pause-for-the-user)
3. [Sub-agent architecture](#3-sub-agent-architecture) — [Why structural](#why-this-is-structural-not-stylistic) · [Allowed](#allowed-in-main-thread-context) · [Forbidden](#forbidden-in-main-thread-context) · [Canonical roles](#canonical-sub-agent-roles) · [Model policy per role](#sub-agent-model-policy-per-role) · [Every round = fresh](#every-round--a-new-fresh-sub-agent) · [Orchestrator cannot mark CLEAN](#the-orchestrator-cannot-mark-clean) · [Cluster partition (K=12)](#auditor-cluster-partition-canonical-k12) · [Aggregator role](#aggregator-role-post-cluster-consolidation)
4. [Audit-loop mechanics](#4-audit-loop-mechanics) — [Three-artifact model](#three-artifact-journal-model) · [Round sequence](#mandatory-round-sequence) · [Plan currency](#plan-currency-before-dispatch) · [Dispatch-brief template](#dispatch-brief-template) · [Per-round dispatch](#per-round-dispatch-protocol) · [Orchestrator verification](#orchestrator-verification-of-sub-agent-outputs) · [Sister-sweep checklist](#cross-cluster-sister-sweep-checklist-aggregator-baseline) · [K=1 carve-out policy](#k1-carve-out-usage-policy) · [Partial-file template](#partial-file-template-per-auditor) · [Why sweep-only-replaceable](#why-the-table-is-sweep-only-replaceable) · [Why append-only](#why-findings--fixes-are-append-only) · [Evidence requirements](#evidence-requirements-mechanical-no-exceptions) · [Loop count](#loop-count-expectations)
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
- **Step** — one project's worth of work within a deliverable (default: one `csproj`, or one logical bundle for docs / config / SvelteKit work). Ordered; may declare prerequisites on earlier steps.
- **Audit round** — one pass through every category in `rules.md`, producing per-predicate evidence. Findings are fixed inside the same round; the next round runs against the post-fix state.
- **Clean round** — an audit round producing zero findings across every category. The termination signal.
- **Iteration ceiling** — 10 audit rounds per step (and 10 at final review). Hitting 11 = escalate to the user; the mental model is wrong, not the execution.
- **Self-improvement** — at each step's audit termination AND at ship, the agent distills the kinds of misses into proposed `rules.md` additions. User approves; rules are appended; future deliverables start stricter.
- **Orchestrator** — the main-thread agent. Decision-making + delegation only. Cannot edit / write / read source code; cannot walk `rules.md`; cannot mark anything CLEAN. Spawns sub-agents for everything domain-level.
- **Sub-agent** — a fresh-context worker spawned via `Agent` for one role (Planner / Implementer / Auditor / Aggregator / Fixer / Final-reviewer). Returns a structured summary; its context dies on return.
- **Cluster** — one of twelve thematic groupings of `rules.md` predicates. Canonical K=12 partition: [§3 Auditor cluster partition](#auditor-cluster-partition-canonical-k12).
- **Audit round (K=12)** — one full audit pass = 12 parallel cluster Auditors + 1 Aggregator + (if findings) 1 Fixer. The default unit; sequential K=1 is a carve-out requiring explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

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
3. **Break into steps.** A step = one csproj or equivalent shippable bundle. Loop until step list + ordering + prerequisites are agreed.
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

**1a. Plan-Audit (when required per [rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)).** If the step introduces new types / new patterns / >50-file scope, dispatch a **K=12 Plan-Audit batch + Aggregator** AFTER the Planner returns but BEFORE the Implementer — same cluster partition, scoped to the Plan section (reality alignment + naming + rules.md compliance + cross-language parity + existing-pattern consistency + stale assumptions + §26 spec-mirror anti-pattern). Aggregator merges → `## Plan-Audit results`. On findings, dispatch a **Plan-amender** (tools = journal Plan-section + Plan-Audit fix log only) → fresh K=12 Plan-Audit Round 2 to verify closure. Terminate on CLEAN; the Implementer then receives the AMENDED Plan. **Carve-outs** (Plan-Audit NOT required): trivial single-file edits (<5 net-new files, no new types/patterns/public surface), pure-doc deliverables, Step 0 branch-checkout / scaffolding, sub-dispatches within a step that already had upfront Plan-Audit — the orchestrator log cites which carve-out applies. *Empirical: `n/geo-libs` Step 2 Plan-Audit returned 35 findings (13 HIGH + 13 MEDIUM + 9 LOW) incl. stale assumptions, wrong tsconfig paths, wrong locale counts, AND one security flaw — caught before the Implementer built on them.*

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

**3. Audit loop (the core forcing function).** Per round the orchestrator dispatches a **K=12 batch of fresh Auditors** in parallel (READ-ONLY — cannot edit source), then a **fresh Aggregator** once all 12 partials return. Each cluster Auditor walks its slice per the [§3 cluster partition](#auditor-cluster-partition-canonical-k12), produces per-predicate evidence (grep results, file:line, "checked X by Y, found Z" — vibes are not evidence), and writes its own partial (`r{N}-partial-{CLUSTER}-{cluster-name}.md`, CLUSTER ∈ {A1, A2, B1, B2, B3, C1, C2, C3, D1, D2, E1, E2}). The Aggregator merges the 12 into the canonical big table (REPLACES `## Latest sweep results`) + appends one `### Round N findings` subsection (per [§3 Aggregator role](#aggregator-role-post-cluster-consolidation)). Workflow: [§4 Per-round dispatch protocol](#per-round-dispatch-protocol).

On FINDING rows, spawn a **fresh Fixer** with the consolidated list. The Fixer applies fixes + appends fix-log entries — it cannot mark anything CLEAN; closure is proven only by the NEXT round's fresh K=12 batch + Aggregator not surfacing the finding. **A second audit round is a BRAND-NEW K=12 batch + brand-new Aggregator, never the same ones re-running** — the fresh-context property is non-negotiable. **K=1 carve-out** requires explicit per-round user permission per [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy) + [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit); NEVER self-invoked. Detailed mechanics: [§4](#4-audit-loop-mechanics).

**4. Per-step distillation.** Once the step terminates clean (a fresh Auditor's big table came back with zero FINDING rows), the orchestrator spawns a sub-agent to append the distillation block (template in [§5](#5-self-improvement-loop)) — kinds of misses by category + candidate rules.md predicates. These surface in the root README's Kinds-of-misses log; final merge into `rules.md` happens at ship.

**5. Update root README.** After distillation the **orchestrator** updates `docs/wip/<deliverable>/README.md` (one of the few `Edit` activities it may do itself, since the root README is its tracking artifact): step status ⏸ → 🔄 → ✅ (with round count), append to Kinds-of-misses log, append any new cross-cutting decisions.

**6. Move to next step.** Step N starts when all prerequisites are ✅. The orchestrator does NOT spawn a new Planner for step N while the previous step has open audit findings.

### FINAL-REVIEW

Same orchestrator-driven loop as EXECUTE, scope = the whole deliverable. Catches integration / consistency bugs no single-step audit finds: cross-step type drift, telemetry-tag drift between two libs, README parity, end-to-end integration paths. Folder: `docs/wip/<deliverable>/final-review/journal.md`. Fresh sub-agents per phase:

1. Fresh **Planner** defines the deliverable-wide cross-cutting focus areas (the Aggregator verifies these in [§3 Aggregator role](#aggregator-role-post-cluster-consolidation) step 3).
2. Fresh **Implementer** for cross-cutting fixes (only if planning surfaces work).
3. Fresh **K=12 Final-reviewer batch** per round (READ-ONLY) per the [§3 cluster partition](#auditor-cluster-partition-canonical-k12), scope = whole deliverable; fresh **Aggregator** merges. Each round = a brand-new K=12 batch + Aggregator.
4. Fresh **Fixer** when the Aggregator surfaces findings.
5. 10-iteration ceiling (one iteration = one K=12 batch + Aggregator + Fixer); escalate if hit.
6. Distillation entry.

Zero FINDING rows in the latest Aggregator's big table → ready to SHIP.

### SHIP (handoff to user REVIEW)

Triggered by final-review's clean termination:

0. **Walk the [Deliverable completeness checklist](rules.md#deliverable-completeness-checklist-the-gate-before-user-review) BEFORE anything else.** Every box must be an honest YES with a citation; if any is NO, go back into fix-loops and re-walk. Then write the verbatim attestation block (from rules.md) into the root README — without it, SHIP cannot proceed.
1. **Aggregate proposed rule additions** from all step + final-review distillations; deduplicate; append to the root README's Proposed-rule-additions section.
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

- **Doesn't replace CLAUDE.md** — CLAUDE.md is the agent-directive root + conventions references; this doc defines the _process_ ensuring conventions are followed.
- **Doesn't replace `docs/v2/`** — phase / wave tracking lives there; this process is per-deliverable, `docs/v2/` is the long-arc roadmap.
- **Doesn't replace per-lib READMEs** — each lib documents its own public API.
- **Doesn't run scripts** — no pre-commit hook fires `rules.md` mechanically; the discipline is the agent walking the rules each round and producing journal-verifiable evidence.

### When to invoke this process

Always, for any work substantial enough to warrant a deliverable folder. The user can override per-task ("just do this small thing, no journal needed" — per [rules.md §13.14](rules/13-permission-action-discipline.md#13-permission--action-discipline)), but the default is the loop. Forcing function: if there's no `docs/wip/<deliverable>/README.md` for the work in flight, the agent ASKS whether to create one before proceeding past PLAN.

---

## 2. Permission gates (when to pause for the user)

The following require explicit user permission **per occurrence**, not implied from prior turns. Predicate-of-record: [rules.md §13 Permission / Action Discipline](rules/13-permission-action-discipline.md#13-permission--action-discipline).

> **Duplicated from [rules.md §13](rules/13-permission-action-discipline.md#13-permission--action-discipline) for at-a-glance protocol context — the canonical full version (Evidence + Why + How per predicate) lives in rules.md; update both in lockstep when either changes (per [rules.md §11.32](rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices)).**

- **Commit creation** — "go ahead and commit" approves the batch just discussed; the next commit needs fresh permission. (§13.1)
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

**The main thread is an ORCHESTRATOR. It does not plan, implement, audit, or fix domain work itself. EVERY round of planning, implementation, auditing, and fixing is performed by a FRESH sub-agent spawned via `Agent`.** Canonical workflow, not optional.

### Why this is structural, not stylistic

Anthropic's multi-agent research system (orchestrator-Opus + worker-Sonnet) outperforms single-agent Opus by 90.2% on internal evals — the orchestrator-worker pattern is empirically validated for adversarial separation of concerns. Adversarial code-review research shows LLM self-review has systematic leniency bias, and that a reviewer + generator sharing context share blind spots ("most agent-reviews-agent implementations are one LLM pretending to be three reviewers, rubber-stamping itself"). The structural fix is SEPARATE sub-agent invocations with fresh contexts, not roleplay. Empirically (0002-auth-inbound trial): per-step audits converged in 1-3 rounds, main-thread context stayed small, and two production bugs were caught that single-context implementation would have shipped (full writeup: [Appendix C](#appendix-c-trial-outcomes-from-deliverable-0002-auth-inbound)). This mirrors Claude Code's sub-agent design: each sub-agent gets a fresh isolated context and returns only relevant output — context rot in the main thread is near-impossible because it holds almost no domain state.

### Allowed in main-thread context

- ✅ `Agent` (spawn sub-agents — the primary orchestrator activity)
- ✅ `Bash` — git plumbing ONLY (`git status`, `git log`, authorized `git commit -F <file>` / `git push`)
- ✅ `Read` — ONLY the deliverable root README + the orchestrator's own decision log; sub-agents handle source / test / journal reads
- ✅ `TaskCreate` / `TaskUpdate` / `TaskList` / `TaskGet` / `TaskStop`
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

Six roles (Final-reviewer added at deliverable end). Each spawned with fresh context + a tightly-scoped prompt. **No reuse across roles or across rounds.**

| Role | Spawned when | Tool access | Returns |
| --- | --- | --- | --- |
| **Planner** | Start of each step | Read, Grep, Glob, Edit (journal Plan section only) | Step Plan section + summary |
| **Plan-Auditor** (parallel ×K=12) | After Planner (new types / patterns / >50-file scope per §24.16) | Read, Grep, Glob, Bash (read-only) | Partial big-table chunk auditing the Plan section for its cluster |
| **Plan-amender** | When Plan-Audit Aggregator surfaces findings | Read, Grep, Glob, Edit (journal Plan section + Plan-Audit fix log only) | Plan-section edits + appended Plan-Audit fix-log entries |
| **Implementer** | After Planner (carve-out steps) OR after Plan-Audit CLEAN | All | Files touched + tests added + build / inspectcode status |
| **Auditor** (parallel ×K=12) | After Implementer | Read, Grep, Glob, Bash (read-only) | Partial big-table chunk for its cluster ([partition](#auditor-cluster-partition-canonical-k12)) |
| **Aggregator** (one per audit round) | After all 12 Auditors (or 12 Plan-Auditors) return | Read, Edit (journal + audit artifacts only) | Merged canonical big table + consolidated findings-log entry + cross-cluster verification |
| **Fixer** | When findings exist | All | Files changed + appended fix-log entries |
| **Final-reviewer** (parallel ×K=12) | Before SHIP | Same as Auditor | Cluster-scoped partial big tables; Aggregator merges |

**Key design decisions:**

- **Planner is its own role** — writes the step's Plan (goal, files, decisions, pre-emptive gate checks) and returns; the Implementer receives the Plan as input, fresh context.
- **Auditors cannot modify source** (read-only Bash) — "audit + fix in same session" is structurally impossible; fixes happen in a separate Fixer invocation AFTER findings are RECORDED (no "I fixed it before recording it" sleight-of-hand).
- **Auditor adversarial framing** — the prompt states it's rewarded for finding issues, not for declaring CLEAN; its role is hostile critic.
- **Parallel cluster dispatch is the default** — K=12 Auditors run concurrently per round, each scoped to one [cluster](#auditor-cluster-partition-canonical-k12); the [Aggregator](#aggregator-role-post-cluster-consolidation) merges + cross-verifies.
- **Effort-scaling in prompts** — each prompt caps effort proportional to the step's surface area; cluster scope already constrains per-Auditor effort to ~10-40 rows.
- **Aggregator is required whenever K>1** — it produces the canonical big table + consolidated findings entry; it dedupes / merges / adds cross-cluster findings but cannot flip a per-cluster verdict unilaterally (escalates ties to the orchestrator). Runs on Fable per the [model policy](#sub-agent-model-policy-per-role).
- **K=1 carve-out requires explicit user permission** (§24.0h + [§4 K=1 carve-out usage policy](#k1-carve-out-usage-policy)); NEVER self-invoked.
- **Plan-Audit is mandatory before Implementer dispatch for non-trivial steps** (§24.16) — details + carve-outs in [EXECUTE step 1a](#execute).

### Sub-agent model policy per role

**SINGLE CANONICAL location** for which Claude model each role runs on. All other references (process.md, rules.md, CLAUDE.md) cross-link here. Predicate-of-record (walked every audit round): [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

| Role | Default model | Why this model |
| --- | --- | --- |
| **Orchestrator** (main thread) | Fable 5 | Judgment + delegation + trust-but-verify discipline ([§4](#orchestrator-verification-of-sub-agent-outputs)); catches sub-agent hallucinations / short-circuits. |
| **Planner** | Fable 5 | Plan quality drives every downstream sub-agent; high-leverage low-volume — a missed gate cascades into Implementer + Auditor + Fixer cycles. |
| **Plan-amender** | Fable 5 | Writes to the same canonical Plan artifact; amendments must stay coherent with locked decisions. |
| **Aggregator** (one per audit round) | Fable 5 | Merges 12 cluster partials + cross-cluster sister-sweep; large context window + Fable-class cross-cluster reasoning. |
| **Auditor** (per-cluster, K=12) | Opus 4.8 | Predicate pattern-matching + grep + file:line citations; bounded scope, structured output, no synthesis. Workhorse-shape; Fable over-specified. |
| **Plan-Auditor** (per-cluster, K=12) | Opus 4.8 | Same shape as Auditor; design-phase scope. |
| **Final-reviewer** (per-cluster, K=12) | Opus 4.8 | Same shape as Auditor; deliverable-wide scope. |
| **Implementer** | Opus 4.8 | Bounded code/test authorship per a brief; the hard reasoning was done by Plan / Aggregator / orchestrator. Sweeping carve-out applies (below). |
| **Fixer** | Opus 4.8 | Mechanical application of pre-specified fix scope against a tight contract. Sweeping carve-out applies. |
| **Investigator / Research** | Opus 4.8 | Bounded investigation returning structured reports (paths, grep counts, citations). |

**Why this allocation**: spend Fable where capability moves outcomes (synthesis, high-leverage planning, the trust-but-verify orchestrator); use Opus where capability already saturates against a tight contract (predicate walking, bounded code/test, bounded investigation). Fable availability is finite — the K=12 Auditor dispatch is the highest-volume pattern, so spending Fable there would starve the synthesis/planning roles. The [orchestrator verification discipline](#orchestrator-verification-of-sub-agent-outputs) is the structural compensation that makes workhorse dispatch safe.

**Sweeping carve-out** (Implementer / Fixer Fable escalation — codified bypass, no per-occurrence user approval needed): qualifies when it meets ≥1 criterion below; the dispatch brief MUST cite the triggering criterion + justification, and the return self-attestation MUST echo it.

1. **Atomic large-file-set** — touches >40 files atomically (can't split without breaking the build or producing audit-failing intermediate states).
2. **Multi-concern dispatch** — spans >3 distinct concerns where splitting creates coordination overhead exceeding the Fable premium (e.g. new handler + DI wiring + test + README + proto wiring).
3. **Cross-runtime refactor** — coordinated .NET + TS changes (naming sweep across both, cross-language rename, parity-test alignment).
4. **Cascading pipeline change** — changes a code-gen pipeline (or its input) and regenerates downstream consumer assemblies.

The carve-out applies ONLY to Implementer / Fixer. Auditor / Plan-Auditor / Final-reviewer / Investigator escalations to Fable require explicit per-occurrence user approval per [rules.md §13.14](rules/13-permission-action-discipline.md#13-permission--action-discipline).

**Self-documentation requirement** — every sub-agent return summary opens with the model-attestation block (see [Dispatch-brief template](#dispatch-brief-template)); the orchestrator's per-step journal records per dispatch: the model, the role, and (if Fable from an Opus-default role) the carve-out criterion + verbatim justification. This dual-channel attestation gives retroactive auditability for the self-learn loop (which Opus dispatches needed re-do vs which Fable dispatches could've been Opus).

**Cross-references:** [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (predicate enforcement) · [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (K=1 discipline; composes with §24.0i) · [§4 orchestrator verification](#orchestrator-verification-of-sub-agent-outputs) · [CLAUDE.md MANDATORY block 0](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread) (its role table cross-links here for the model column).

### Every round = a NEW fresh sub-agent

A second audit round is a brand-new Auditor, NOT the same one "running again." A fix follow-up after a Fixer's first attempt is a brand-new Fixer. The fresh-context property prevents leniency / motivated-stopping / stale-memory — reusing context across roles defeats the whole pattern. The orchestrator never short-circuits this for "quick" work: a one-line typo fix still spawns Planner / Implementer / Auditor / (if findings) Fixer.

### The orchestrator cannot mark CLEAN

The orchestrator consumes Auditor verdicts; it cannot promote a step to CLEAN by judgment. CLEAN means "the latest Auditor's big table contained zero FINDING rows." To confirm closure it spawns a fresh Auditor — it does not eyeball.

### Auditor cluster partition (canonical K=12)

The `rules.md` catalog (~24 categories, ~450 numbered subsections) partitions into 12 thematic clusters. Each Auditor owns exactly one cluster and walks every numbered subsection inside it — reading only that cluster's category files under [`rules/`](rules/) (mapped below) against the step's (or deliverable's) file scope. The partition is fixed — the orchestrator sends the same §-range to the same cluster code across deliverables so muscle memory carries forward.

| Cluster | Name | rules.md sections | ~preds | Theme |
| --- | --- | --- | --- | --- |
| **A1** | Tests / coverage | §1 | ~30 | Tests / coverage |
| **A2** | Regression, races, disposal, degradation, idempotency | §2, §4, §15, §18, §22 | ~25 | Regression-pinning, concurrency / races, object disposal / resource lifetime, graceful degradation, idempotency / exactly-once |
| **B1** | C# conventions | §5 | ~25 | C# conventions |
| **B2** | TS conventions + naming | §6, §7 | ~20 | TypeScript / SvelteKit conventions, naming / file headers / folder casing |
| **B3** | Shared-lib hygiene + D2Result | §16, §17 | ~15 | OOTB shared-lib catalog use, D2Result usage + extensions |
| **C1** | PII/logging + operations | §3, §8 | ~20 | PII / logging safety, build cleanliness + operational hygiene |
| **C2** | Architectural layer | §9 | ~45 | Architectural layer hygiene |
| **C3** | Security + permissions | §10, §13 | ~25 | Security (endpoints / auth / secrets / input), permission / action discipline |
| **D1** | KEEP doc parity | §11 | ~40 | KEEP-doc updates + forward-framing + per-lib README parity |
| **D2** | i18n + no-phase verbiage | §12, §14 | ~10 | i18n / Paraglide / TK constants, no-phase-verbiage / no-conversation-scoped-IDs hygiene |
| **E1** | UX + DX + observability + config | §19, §20, §21, §23 | ~25 | UX, DX, observability completeness, configuration hygiene |
| **E2** | Audit-meta + temporal + codegen | §24, §25, §26 | ~35 | Audit evidence discipline (incl. self-audit per §24.12), temporal-types discipline, codegen discipline |

**Per-cluster category-file reading list.** `rules.md` is split into one file per category under [`rules/`](rules/) with `rules.md` retained as the index. Each cluster Auditor reads ONLY its own category files below — not the whole catalog — plus the index-level [Deliverable completeness checklist](rules.md#deliverable-completeness-checklist-the-gate-before-user-review) (read by every cluster). This is the context cut the split buys: ~one cluster's worth of predicates instead of the full ~710 KB catalog.

| Cluster | Category files to read |
| --- | --- |
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

**Why this partition (K=12):** D1 (§11 alone, the densest section, ~40 predicates) gets dedicated focus separate from lighter §12/§14; E1 (operational quality §19/§20/§21/§23) splits from E2 (process integrity §24/§25/§26) — no predicate overlap, separate mental frames; A/B/C split along natural §-boundaries keeping §9 (largest architectural section) standalone. K=12 gives ~35-45% wall-clock reduction vs prior smaller-K splits + tighter per-Auditor focus; the higher Aggregator dedup cost is absorbed by running the Aggregator on Fable. Stable §-ownership threads a repeat finding's history through past partials by cluster code. **Cross-cutting concerns belong to the Aggregator**, not any one cluster. When a predicate seems to straddle clusters, the mapping is §-number → cluster (NOT topic → cluster) — the §-number wins; the Aggregator's cross-cluster verification ([Aggregator role](#aggregator-role-post-cluster-consolidation) step 3) resolves straddle concerns.

### Aggregator role (post-cluster consolidation)

A single sub-agent spawned per audit round AFTER all K=12 cluster Auditors return their partials. It is the journal's authoritative writer for the round — per-cluster Auditors write disposable partials; the Aggregator alone writes the canonical journal sections. **Runs on Fable** (per the [model policy](#sub-agent-model-policy-per-role)) for the context budget to consume 12 partials + the reasoning to do cross-cluster dedup + sister-sweep.

**Six responsibilities (in order):**

1. **Mechanical merge.** Read all 12 partials (`r{N}-partial-{A1|…|E2}-{cluster-name}.md`). Combine the 12 big-table chunks into ONE canonical sorted-by-§ big table under `## Latest sweep results`, REPLACING the prior sweep's table (§24 sweep-replacement rule). Anti-laziness preamble verbatim above it.
2. **Dedupe.** A finding surfaced by multiple Auditors collapses into one entry with combined provenance (all citation paths preserved).
3. **Cross-cutting verification.** Walk the deliverable's cross-step focus areas spanning multiple clusters (defined in the final-review journal's Plan section — e.g. "TYPE LIE FIX still verified end-to-end across .NET emitter + TS consumer", "wire-shape leakage class — does the originating sample class exist anywhere else in scope?", "spec-driven catalog parity — every cross-language wire identifier cataloged + parity-tested"). No per-cluster Auditor could see these.
4. **Cross-cluster sister-sweep.** Cluster Auditors sister-sweep WITHIN their §-scope (§24.13.3); the Aggregator runs sister-sweeps at CROSS-cluster scope — baseline commands in [§4 Cross-cluster sister-sweep checklist](#cross-cluster-sister-sweep-checklist-aggregator-baseline), run every round.
5. **Append findings log.** One `### Round N findings (<UTC>)` subsection under `## Sweep findings log (append-only)`: the consolidated finding list (steps 2-4), a closure-verification table for prior-round findings (CLOSED-by-absence in this round's big table OR STILL-PRESENT), and a regression-verification table (prior-round PASS rows spot-confirmed still PASS).
6. **Return summary to orchestrator.** Structured one-paragraph: total findings by severity, fix-required §-rows, recommendation (CLEAN → next phase, or findings → spawn Fixer with scope).

**Cannot:** flip a per-cluster verdict unilaterally (add cross-cluster findings, yes; overrule an Auditor, no — escalate ties to the orchestrator for a tie-breaker Auditor); touch source / tests / configs (write access = journal + audit artifacts only); mark the step CLEAN (it RECOMMENDS clean; the big table must contain zero FINDING rows for CLEAN to be valid). **Why required:** with K>1 parallel Auditors no single Auditor sees the full picture — without an Aggregator the orchestrator would have to read all 12 partials (forbidden) or trust each slice without cross-validation (defeats the parallelism win). A K=12 dispatch WITHOUT an Aggregator is incomplete; the round is not done until the `### Round N findings` subsection lands.

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
| **Big table** (latest sweep snapshot) | `## Latest sweep results` | REPLACED every sweep — reflects ONLY the most recent walk against current code. ~85+ rows, one per rules.md subsection. Anti-laziness preamble above it. | Sweep activity ONLY. Fix-applying agents NEVER touch this. Under K=12 the **Aggregator** (Fable) writes the merged table; per-cluster Auditors write only their partials. |
| **Findings log** (per-round history) | `## Sweep findings log (append-only)` | APPEND-ONLY. Each sweep appends a `### Round N findings (timestamp)` subsection. Never deleted / re-ordered. | Sweep activity ONLY. Under K=12 the **Aggregator** writes the consolidated round subsection (12 clusters + cross-cluster). |
| **Fix log** (chronological fix activity) | `## Fix log (append-only)` | APPEND-ONLY. Each fix appends one entry: rules.md subsection + finding round + what changed + `file.cs:NN`. Never deleted / re-ordered. | Fix-applying agent ONLY. |

The big table is the canonical "what is true RIGHT NOW" snapshot — every PASS is a fresh file:line citation against current code, with NO inheritance of PASS from earlier sweeps. **Closure is proven ONLY by the absence of a FINDING from the next sweep's big table.** The fix log captures intent + action; it does NOT certify outcome.

### Mandatory round sequence

1. **Sweep**: walk every rules.md subsection against current code. REPLACE the big table. APPEND a `### Round N findings (timestamp)` subsection enumerating every FINDING.
2. **Fix work**: for each FINDING, apply the fix; APPEND one fix-log entry (rules.md subsection + finding round + what changed + `file.cs:NN`). **The big table is NOT touched between sweeps.**
3. **Sister-sweep mandatory** ([rules.md §24.13.3 / §24.13.3d](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — the Fixer dispatch brief MUST name the sister-sweep command + full applicability path-set + literal-output-paste requirement; the Fixer pastes literal stdout into the fix-log entry.
4. **Tamper-evident dispatch** ([rules.md §24.14](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — when a finding previously claimed CLOSED resurfaced STILL_PRESENT, or is a user-flagged special-emphasis target, the Fixer brief MUST mandate BEFORE/AFTER literal-output pasting (predicate-grep + `git diff --stat`) — the four literal outputs become the fix-log entry's inline evidence.
4a. **Pattern-class scope expansion** ([rules.md §24.28](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — for any pattern-class violation (convention breach, leaked token, recurring anti-pattern), the Fixer brief MUST name the grep against the FULL deliverable diff scope + mandate fixing every instance, not only the cited file:lines. Partial fixes resurface as STILL-PRESENT.
4b. **Fixer self-grep before returning** ([rules.md §24.29](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)) — before returning, the Fixer runs `git diff HEAD` and greps its own added lines for new pattern-class instances + conversation-scope tokens / audit-process references / partial cross-links in doc edits; any self-introduced hit is fixed in-place. The fix-log entry includes a `"Self-grep"` section with command + literal output.
5. **Every finding gets fixed** — no silent carryover. If genuinely unresolvable this round, get EXPLICIT user permission to defer + append a deferral entry to the fix log (still append-only, never silent omission).
6. **Next sweep**: when all current-round findings have fix-log entries, run the NEXT sweep — walk the full catalog again from scratch (under K=12 each cluster Auditor re-reads its category files per the [per-cluster reading list](#auditor-cluster-partition-canonical-k12)). REPLACE the big table; append `### Round N+1 findings`. A row that was a FINDING in Round N and is now PASS in Round N+1 = closed (proven by absence). Still a FINDING = fix didn't take; append more fix entries, run N+2.
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

- **Role + scope** — the role + the file/predicate scope (per-step touched files / whole deliverable / one cluster's §-range / one Plan section).
- **Reading list** — the exact artifacts to read (shared-context file, journal Plan section, cluster category files, rules.md index). A sub-agent reads ONLY what the brief names — it has NO conversation memory.
- **Model + self-attestation** — dispatched model per the [Sub-agent model policy per role](#sub-agent-model-policy-per-role) table (`model: "opus"` for workhorse roles; `model: "fable"` for Planner / Plan-amender / Aggregator / Sweeping-carve-out). Every return summary MUST open with the model-attestation block (below).
- **Return format** — the structured summary shape for the role (files + tests + build state; or a partial big-table chunk; or the merged table + findings).
- **Journal-artifact requirement** — which of the three artifacts (big table / findings log / fix log) the role writes, if any: Auditors write disposable partials; the Aggregator writes the canonical big table + findings-log subsection; the Fixer writes fix-log entries; the orchestrator writes nothing domain-level.
- **Constraints** — READ-ONLY tools for Auditors / Aggregator; no sub-agent spawning; no commits; no touching another Auditor's partial.

**Model-attestation block (opens every return summary):**

```
Model: <claude-model-id — e.g. claude-fable-5 or claude-opus-4-8>
Fable carve-out reason (if Fable-dispatched from an Opus-default role): <criterion # + justification, verbatim from dispatch brief>
```

**Anti-laziness preamble (Auditor / Plan-Auditor / Final-reviewer briefs — verbatim, load-bearing):**

> WALK EVERY NUMBERED SUBSECTION in your cluster scope. NO SKIPPING, no assuming irrelevance without evidence. PASS rows require file:line; N/A rows require a step-scope-specific reason; FINDING rows require severity + file:line + description + fix; the Status column prepends a ✅ / ❌ / ⚪ / 🟡 emoji. Regex is a TOOL not source of truth (§24.13.2) — read the file. Sister-sweep at full predicate applicability (§24.13.3). The cost of walking a predicate is minutes; skipping one is a future bug + audit round.

**Shared-context reminders every Auditor / Final-reviewer brief carries** (predicate-of-record in parens):

- Read every modified `.cs` / `.ts` for the three tool-invisible lenses neither `dotnet build` nor `jb inspectcode` enforces: line length ≤ 100 + SA1519/SA1516 cascades; a blank line after any multi-line statement before the next statement; `var` for locals where the type is evident. Gate-green does NOT imply convention-clean. (§24.20)
- Gate-verify at FULL-solution scope: `dotnet build server/D2.slnx` (or the tests-csproj build) AND `jb inspectcode server/D2.slnx --severity=WARNING` — never a per-lib / per-project inspectcode (it hides test-file findings). (§24.21)
- Scan modified source xmldocs + `//` / `/* */` comments + `.csproj` XML comments (not just READMEs) for deliverable-step / phase / SHIP / forward-ref / rules-§ / CLAUDE.md-§ framing. (§24.22)
- Read from the on-disk WORKING TREE, not `git diff HEAD` / `git show HEAD:` — the latest Implementer / Fixer output is uncommitted; a HEAD reader reports stale pre-change findings and misses post-change issues. (§24.19; omit once all step output is committed.)

**Per-role deltas:**

| Role | Model | Scope | Writes | Delta from skeleton |
| --- | --- | --- | --- | --- |
| **Planner** | Fable | one step | journal Plan section | Produce the Plan block (goal, files, decisions, pre-emptive gate checks); no audit artifacts. |
| **Implementer** | Opus (carve-out → Fable) | files-to-touch | source + tests | Write code + tests; run `check-baselines` if a consumable was touched; return the Implementation block. |
| **Auditor / Plan-Auditor / Final-reviewer** | Opus | one cluster's §-range | disposable partial | Carry the anti-laziness preamble + shared-context reminders; write the [partial-file template](#partial-file-template-per-auditor); Plan-Auditor scopes to the Plan section, Final-reviewer to the whole deliverable. |
| **Aggregator** | Fable | 12 partials + cross-cluster | canonical big table + findings-log subsection | Perform the six responsibilities in [Aggregator role](#aggregator-role-post-cluster-consolidation); run the [cross-cluster sister-sweep baseline](#cross-cluster-sister-sweep-checklist-aggregator-baseline). |
| **Fixer** | Opus (carve-out → Fable) | consolidated finding list | fix-log entries | Apply fixes; sister-sweep + tamper-evident + pattern-class + self-grep per [round sequence](#mandatory-round-sequence) steps 3-4b; cannot mark CLEAN. |
| **Plan-amender** | Fable | Plan-Audit finding list | journal Plan section + Plan-Audit fix log | Address each Plan-Audit finding; append Plan-Audit fix-log entries. |

### Per-round dispatch protocol

The orchestrator's workflow for one K=12 + Aggregator audit round. Same shape for per-step rounds, final-review rounds, AND Plan-Audit rounds — the difference is scope: per-step code-audit = the step's touched files; final-review = whole deliverable; Plan-Audit = the journal's `## Plan` section + the codebase reality it claims to align with ([rules.md §24.16](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)).

**Plan-Audit specifics** (when §24.16 applies): write a `plan-audit-r{N}-shared-context.md` (same shape, mission-scoped to the Plan section + §24.16 cluster verification questions), dispatch K=12 Plan-Auditors (`model: "opus"`), then the Aggregator (Fable) → `## Plan-Audit results` lands BEFORE the Implementer. On findings: Plan-amender (Fable) addresses each + appends fix-log entries; then a fresh K=12 Plan-Audit Round 2 verifies closure. Terminate on CLEAN → dispatch the Implementer with the AMENDED Plan. K=1 Plan-Audit follows §24.0h. Carve-outs skip Plan-Audit entirely; the orchestrator log cites the carve-out per occurrence.

**Step 1 — Orchestrator writes the per-round shared-context file** at `docs/wip/<deliverable>/<NN>-<step>/r{N}-shared-context.md` (or `final-review/r{N}-shared-context.md`). Contents: mission paragraph (what this round audits, why); locked decisions (so cluster Auditors don't re-litigate); deliverable scope (concrete path-set or `git diff --name-only` recipe); special-emphasis user direction (if any); the K=12 [cluster partition table](#auditor-cluster-partition-canonical-k12) verbatim; output format spec (the [Partial-file template](#partial-file-template-per-auditor)); Aggregator role summary (so Auditors flag cross-cluster handoffs); critical constraints + the anti-laziness preamble + shared-context reminders (§24.19/§24.20/§24.21/§24.22) from the [Dispatch-brief template](#dispatch-brief-template).

**Step 2 — Orchestrator dispatches 12 parallel Auditors in ONE message** (a single `Agent` batch of 12 parallel invocations, each `model: "opus"`). Each brief: read the shared-context file; read your cluster's category files end-to-end (per the [per-cluster reading list](#auditor-cluster-partition-canonical-k12)); skim other clusters / the [index](rules.md) for cross-refs; walk YOUR cluster against the scope; write to your `r{N}-partial-{CLUSTER}-{cluster-name}.md`. Concurrent writes are safe (each Auditor owns its file). Run as background (`run_in_background: true`) and let notifications return as each completes. Every K=N Auditor / Plan-Auditor / Final-reviewer invocation MUST include `model: "opus"` explicitly; every Fable dispatch under the Sweeping carve-out MUST cite the triggering criterion in both the brief and the return self-attestation (predicate-of-record [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)).

**Step 3 — Orchestrator waits for all 12 partials.** When ALL 12 notifications return, dispatch the Aggregator with the list of partial paths — the orchestrator does NOT read partials directly.

**Step 4 — Orchestrator dispatches the Aggregator** (Fable; foreground OK, not parallelizable). Brief: read the 12 partials; read the deliverable's cross-cutting focus areas; perform the six responsibilities in [Aggregator role](#aggregator-role-post-cluster-consolidation); write the canonical big table + `### Round N findings` subsection; return summary.

**Step 5 — Orchestrator routes on the recommendation:** **CLEAN** (zero FINDING rows + zero new cross-cluster findings) → advance to next phase (next step, or SHIP for final-review). **FINDINGS present** → dispatch a fresh Fixer with the consolidated list; after it returns, dispatch round R+1 (brand-new K=12 batch + brand-new Aggregator, fresh context across the board).

**Wall-clock:** a K=12 batch's wall-clock is dominated by the slowest cluster (typically D1 / C2 / E2 depending on scope), NOT the sum. A round (one K=12 + Aggregator + optional Fixer) ≈ 1/4-1/5 of a sequential K=1 walk covering the same predicate count (~35-45% reduction vs prior smaller-K splits). 10-iteration ceiling per step (one iteration = one full round).

### Orchestrator verification of sub-agent outputs

> **Trust-but-verify discipline — the structural compensation for dispatching Opus-default workhorse roles per the [Sub-agent model policy per role](#sub-agent-model-policy-per-role) table. Predicate-of-record: [rules.md §24.0i](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).**

When the orchestrator dispatches Opus-default workhorse sub-agents (Auditor / Plan-Auditor / Final-reviewer / Implementer / Fixer / Investigator) it takes on additional verification responsibility — it cannot blindly accept a sub-agent's outputs as ground truth (the same as for Fable outputs). **The discipline is mandatory**: a workhorse dispatch without trust-but-verify follow-up is structurally weaker. Part of the Orchestrator's Fable context budget is reserved for it (part of why the Orchestrator role is Fable).

**Specific verification actions** (apply per dispatch type):

1. **Spot-sample partial evidence** (per K=12 round) — random-sample 1-2 PASS rows per cluster Auditor's partial; re-read the cited `file:line` to confirm the evidence is real (Auditors occasionally cite a line that no longer carries it, or synthesize an adjacent citation). ~12-24 sampling reads per round; cheap vs a missed FINDING.
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

K=1 single-Auditor dispatch is a possible option for truly tiny scope (one-line config tweak / typo fix), but **the orchestrator MUST NEVER self-invoke K=1 without explicit per-round user permission.** Canonical predicate-of-record: [rules.md §24.0h](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). The "narrow scope" / "tamper-evident proof exists" / "mechanical change" / "I already verified the fix" reasoning patterns are NOT valid self-justifications — they are exactly the cheating failure mode this framework prevents (per [CLAUDE.md MANDATORY block 0](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread): "The ONLY bypass is an explicit user request"). **If you think K=1 is appropriate, ASK the user first** — write the proposed justification (scope, why partitioning offers no parallelism win, what coverage guarantees you forfeit) and wait for explicit `K=1 approved` before dispatching. Without that, default to K=12 — every round, no exceptions.

**How to apply:**

1. **Default**: every audit round dispatches K=12 per [Per-round dispatch protocol](#per-round-dispatch-protocol) step 2. No self-justification.
2. **K=1 candidate**: the orchestrator writes a proposed-K=1 message enumerating (a) exact scope, (b) why partitioning offers no parallelism win, (c) forfeited coverage guarantees (which cluster perspectives won't be exercised), (d) why those forfeitures are acceptable for this scope.
3. **User approval**: explicit `K=1 approved` per occurrence — approvals do NOT carry forward.
4. **Without explicit approval**: dispatch K=12, even if K=1 was discussed or the prior round was K=1-approved.
5. **Verification rounds after Fixer** — an especially-tempting target for K=1 rationalization ("tamper-evident proof shows the change landed; I just need to confirm closure"). Post-Fixer verification rounds default to K=12; the Fixer's tamper-evident output speeds each cluster Auditor's verification but does NOT eliminate the need for K=12's independent angles + cross-cluster sister-sweep.

*Empirical (why codified): deliverable 0008-geo-data-pipeline final audit cycle (under K=5; today's default is K=12 — the lesson applies to any K>1 baseline). After a K=5 batch + Fixer, the orchestrator self-invoked K=1 reasoning "changes are narrow and tamper-evident proof exists." That K=1 round surfaced 2 brand-new findings (a §14.3 conversation-scoped ID + §7.14 line-length residuals, both introduced by the Final Fixer's new test file) AND a §24.0 process gap (missing fix-log entries); the required K=5 re-dispatch then surfaced ONE FURTHER finding the K=1 missed (a cross-doc Tier-3 contradiction). Net: the self-invoked K=1 cost extra verification + re-dispatch rounds plus a process-integrity breach the user called out. Different cluster angles + cross-cluster sister-sweeps reveal what single-Auditor walks structurally cannot; collapsing to K=1 collapses the coverage guarantee.*

### Partial-file template (per Auditor)

Every cluster Auditor writes to its partial file with this structure (cluster code / name / §-range substituted). The orchestrator includes it in the shared-context file so all 12 produce consistent output the Aggregator can mechanically merge.

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

### Why the table is sweep-only-replaceable

If a fix-applying agent could flip a row to PASS, the failure mode: fix doesn't actually take (typo, wrong line, partial replacement, cascade) → agent writes PASS anyway → next sweep "trusts" the PASS and skips re-walking → bug ships. With sweep-only-replacement, every PASS in every sweep's table is freshly walked against current code — no stale PASS can be inherited.

### Why findings + fixes are append-only

The append-only logs preserve the audit trail table-replacement would lose. Anyone reading the journal can answer "what did Round 1 find? what changed in response? did Round 2 confirm closure?" An agent that could delete entries could quietly hide reversals — append-only forces every change (including reversals) into chronological visible order. Every round produces a STRUCTURED TABLE with one row per numbered subsection; the table is the gate — a step is not done until a complete-table round shows zero FINDING rows.

### Evidence requirements (mechanical, no exceptions)

> **Duplicated from [rules.md §24.2 / §24.3 / §24.4](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) for protocol-context reference — the canonical full version (all evidence-form predicates + emoji-prefix mandate §24.10) lives in rules.md; update both in lockstep when either changes.**

- **PASS** requires a `file:line` citation pointing to code/test/doc satisfying the predicate. "Verified ✓" / "looks good" / "checked it" are NOT evidence.
- **N/A** requires a one-sentence REASON specific to the step's scope ("no TS code in this step", "no DI extensions added", "no Redis interaction"). "Doesn't apply" / "irrelevant" are NOT reasons.
- **FINDING** requires all four: (severity HIGH/MEDIUM/LOW) + (file:line) + (specific description) + (suggested fix). Fixed in the same round; the next round runs against post-fix state.

**MANDATORY: emoji-prefixed Status column** — every Status cell starts with a canonical emoji: `✅ PASS` / `⚪ N/A` / `❌ FINDING-HIGH` / `❌ FINDING-MEDIUM` / `❌ FINDING-LOW` / `🟡 <anything-else>` (e.g. `🟡 DEFERRED`). Visual scan-ability is the goal; a bare status word (no emoji prefix) is a §24.10 violation.

```
| §    | Predicate                                         | Status            | Evidence / Reason / Finding                              |
|------|---------------------------------------------------|-------------------|----------------------------------------------------------|
| 1.1  | Test every public path first-pass                 | ✅ PASS           | HttpJwksProvider.GetKeysAsync → tests/Jwks/HttpJwksProviderTests.cs:23 |
| 1.2  | Adversarial inputs in tests                       | ❌ FINDING-MEDIUM | tests/Jwks/HttpJwksProviderTests.cs missing oversized-payload case → add test_OversizedJwks_ReturnsServiceUnavailable |
| 1.3  | DI extensions tested via composition resolution   | ⚪ N/A            | No DI extensions added in this step |
```

### Loop count expectations

- A WELL-PLANNED step typically converges in 1-3 sweep rounds.
- A POORLY-PLANNED step (or one introducing complex new patterns) may need 5-8 rounds.
- 10-iteration ceiling per step (per [Mandatory round sequence](#mandatory-round-sequence)). Iteration 11 = escalate — something is structurally wrong.
- Final-review surfaces 0-2 deliverable-wide consistency findings — typically 1-2 rounds.

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

**At SHIP** (after final-review's clean termination): (1) aggregate proposed rule additions from all step + final-review distillations, deduplicate; (2) present the full list to the user in the root README; (3) user approves / tweaks / rejects each; (4) approved proposals land in `rules.md` as a committed change before the deliverable's code commit.

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
