<!--
Copyright (c) DCSV. All rights reserved.
-->

<a name="top"></a>

# D2-WORX Development Workflow

The loop that turns a design discussion into shipped, audited, regression-tested code without requiring the user to push the agent through audit rounds. Three phases: **PLAN → EXECUTE → REVIEW**. Convergence is autonomous — the **main-thread orchestrator** spawns fresh sub-agents for every round of planning, implementation, auditing, and fixing, and loops until each step's audit terminates clean, then ships to user review.

## Table of contents

- [Mission context](#-mission-context)
- [Glossary](#glossary)
- [Orchestrator-only main thread (canonical)](#orchestrator-only-main-thread-canonical)
- [Folder shape](#folder-shape)
- [PLAN](#plan)
  - [Steps](#steps)
  - [`docs/wip/<deliverable>/README.md` template](#docswipdeliverablereadmemd-template-initial-form-populated-during-plan)
- [EXECUTE](#execute)
  - [1. Spawn Planner sub-agent (step plan entry)](#1-spawn-planner-sub-agent-step-plan-entry)
  - [2. Spawn Implementer sub-agent](#2-spawn-implementer-sub-agent)
  - [3. Audit loop (the core forcing function — fresh Auditor + Fixer sub-agents per round)](#3-audit-loop-the-core-forcing-function)
    - [Three-artifact journal model](#three-artifact-journal-model-mirror-of-claudemd--rulesmd-24)
    - [Mandatory round sequence](#mandatory-round-sequence-do-not-skip-steps-do-not-collapse-roles)
    - [Why the table is sweep-only-replaceable](#why-the-table-is-sweep-only-replaceable)
    - [Why findings + fixes are append-only](#why-findings--fixes-are-append-only)
  - [4. Per-step distillation](#4-per-step-distillation)
  - [5. Update root README](#5-update-root-readme)
  - [6. Move to next step](#6-move-to-next-step)
- [FINAL-REVIEW](#final-review-the-last-step-of-every-deliverable)
- [SHIP (handoff to user REVIEW)](#ship-handoff-to-user-review)
- [REVIEW (user phase)](#review-user-phase)
- [Append-only discipline](#append-only-discipline)
- [Permission gates (must block, no inference allowed)](#permission-gates-must-block-no-inference-allowed)
- [Scope of work shape](#scope-of-work-shape)
- [What this workflow does NOT do](#what-this-workflow-does-not-do)
- [When to invoke this workflow](#when-to-invoke-this-workflow)

> ## ⚠️ MISSION CONTEXT
>
> **D²-WORX is being built as an enterprise-level, production-ready, robust SaaS framework.** This workflow exists to enforce that standard at the process level. The PLAN phase locks design rigor; the EXECUTE phase locks autonomous convergence on quality; the REVIEW phase preserves architectural feedback. Every loop, every audit, every artifact is in service of shipping production-ready code without requiring the user to push the agent through bug-hunting cycles.
>
> **Read [rules.md](rules.md) end-to-end at the start of every deliverable's PLAN phase.** It is the central requirements catalog — security, race conditions, naming, object disposal, D2Result usage, OOTB shared-lib tooling, logging, PII redaction, graceful degradation, UX, DX, observability, idempotency, configuration, and more. Knowing the rules upfront is what lets you write code that passes audit round 1 instead of round 5.

> Companion docs:
> - [rules.md](rules.md) — the central, verbose, authoritative requirements catalog. Read end-to-end during PLAN; walk during EXECUTE audit loop and final-review.
> - [deliverables/](deliverables/) — surviving root READMEs for shipped deliverables (lessons learned + final report). Committed.

## Glossary

- **Deliverable** — a coherent unit of shipped work (one feature, one library set, one cross-cutting refactor). Has a name, a branch, a folder under `docs/wip/<deliverable>/`, and a final committed report at `docs/dev/deliverables/<deliverable>.md`.
- **Step** — one project's worth of work within a deliverable. Default unit is one `csproj` (or one logical bundle for non-csproj work like docs / config / SvelteKit features). Steps have order and may declare prerequisites on earlier steps.
- **Audit round** — one pass through every category in `rules.md`, producing per-predicate evidence. Findings are fixed inside the same round; the round ends, the next round runs against the post-fix state.
- **Clean round** — an audit round that produces zero findings across every category. The termination signal.
- **Iteration ceiling** — 10 audit rounds per step (and 10 at final review). Hitting 11 means escalate to the user; the agent's mental model is wrong, not its execution.
- **Self-improvement** — at each step's audit termination AND at deliverable ship, the agent distills the kinds of misses surfaced into proposed additions to `rules.md`. User approves; rules are appended; future deliverables start with a stricter ruleset.
- **Orchestrator** — the main-thread agent. Decision-making + delegation only. Cannot edit / write / read source code; cannot walk `rules.md`; cannot mark anything CLEAN. Spawns sub-agents for everything domain-level.
- **Sub-agent** — a fresh-context worker spawned via the `Agent` tool for one specific role (Planner / Implementer / Auditor / Aggregator / Fixer / Final-reviewer). Returns a structured summary; its context dies on return.
- **Cluster** — one of five thematic groupings of `rules.md` predicates (A: correctness + reliability, B: code style + idiom, C: architecture + security, D: documentation + framing, E: operational outcomes + audit meta). The canonical partition for K=5 parallel Auditor dispatch lives in [audit-framework.md §3a](audit-framework.md#3a-auditor-cluster-partition-canonical-k5).
- **Audit round (K=5)** — one full audit pass = 5 parallel cluster Auditors + 1 Aggregator + (if findings) 1 Fixer. The default unit of audit work; sequential K=1 is a carve-out for trivial scope only. Dispatch protocol in [audit-framework.md §3c](audit-framework.md#3c-per-round-dispatch-protocol).


<sup>[↑ jump to top](#top)</sup>

---

## Orchestrator-only main thread (canonical)

**The main thread is an ORCHESTRATOR. It does not plan, implement, audit, or fix domain work itself. EVERY round of planning, implementation, auditing, and fixing is performed by a FRESH sub-agent spawned via the `Agent` tool.** This is the canonical workflow, not aspirational and not optional.

### Why this is structural, not stylistic

[Anthropic's multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) (orchestrator-Opus + worker-Sonnet) outperforms single-agent Opus by 90.2% on internal evals — the orchestrator-worker pattern is empirically validated for tasks that involve adversarial separation of concerns. [Adversarial code review research](https://asdlc.io/patterns/adversarial-code-review/) shows that LLM self-review has systematic leniency bias, and that reviewer + generator sharing the same context share blind spots. "Most agent reviews agent implementations are one LLM with a clever prompt pretending to be three reviewers, where the model can rubber-stamp itself" — the structural fix is SEPARATE sub-agent invocations with fresh contexts, not roleplay.

Empirical justification from the deliverable 0002-auth-inbound trial: per-step audits converged in 1-3 rounds (mostly 2), and two real production bugs were caught by adversarial separation that single-context implementation would have shipped:
1. `JwtAuthInterceptor.ResolveMethodScopeMetadata` reading the wrong `UserState` slot — caught by an integration test the Implementer skipped, that a Fixer was forced to add.
2. `act_chain_malformed` dead-letter chain: `MalformedActorChainException` propagating uncaught from `ClaimsToContextMapper.Map` — caught only by the deliverable-wide enumeration that surfaced "AuthFailures helper exists + AuthErrorCodes constant exists + xmldoc enumerates the outcome + README documents it — but JwtValidator never emits it."

Main-thread context stayed small across the whole deliverable (8 step-level audits + final-review + 3 rounds of polish + cross-deliverable design discussions all fit comfortably). User feedback after the trial: "the subagents, while slower to complete work, are actually doing a cleanly better job."

### Allowed in main-thread context

- `Agent` — spawn sub-agents (the primary orchestrator activity)
- `TaskCreate` / `TaskUpdate` / `TaskList` / `TaskGet` / `TaskStop` — tracking
- `Bash` for **git plumbing only** (`git status`, `git log`, `git commit -F <file>` when user-authorized, `git push` when user-authorized) — small non-domain output only
- Reading sub-agent summaries (returned in `Agent` tool results)
- `Read` / `Edit` / `Write` to the orchestrator's own decision log (`docs/wip/<deliverable>/orchestrator-log.md` if used) and the deliverable root README's tracking sections (status flips, kinds-of-misses log appends, proposed-rules accumulator)

### Forbidden in main-thread context

- `Edit` / `Write` to ANY source file, test file, per-csproj README, per-service README, or framework doc
- `Bash` for builds, tests, `jb inspectcode`, or any domain-level grep / inspection
- `Read` on source files, test files, or per-lib READMEs — delegate to sub-agents (they have the fresh context to absorb domain detail)
- Reading journal files mid-deliverable for content review — delegate state-checks to sub-agents that report back summary
- Walking `rules.md` predicates — always done by Auditor sub-agents
- Marking anything CLEAN / PASS / converged from main-thread judgment — those verdicts come from Auditor sub-agent output

### Canonical sub-agent roles

Spec lives in [docs/dev/audit-framework.md §3](audit-framework.md#3-sub-agent-roles); K=5 cluster partition in [§3a](audit-framework.md#3a-auditor-cluster-partition-canonical-k5); Aggregator spec in [§3b](audit-framework.md#3b-aggregator-role-post-cluster-consolidation); orchestrator dispatch protocol in [§3c](audit-framework.md#3c-per-round-dispatch-protocol). Summary:

| Role | When spawned | Tool access | Returns |
|---|---|---|---|
| **Planner** | Start of each step | All | Step Plan section appended to journal |
| **Implementer** | After Planner approval | All | Files-touched + tests-added + build/inspectcode status |
| **Auditor** (parallel ×K=5, default) | After Implementer | READ-ONLY (Read, Grep, Glob, Bash read-only) | Cluster-scoped partial big-table chunk + cluster findings written to designated partial file |
| **Aggregator** (one per round) | After all 5 Auditors return | Edit (journal + audit artifacts only) | Canonical merged big table embedded in journal + consolidated `### Round N findings` subsection + cross-cluster sister-sweep + cross-cutting verification |
| **Fixer** | When Aggregator surfaces FINDINGs | All | Files-changed + appended fix-log entries |
| **Final-reviewer** (parallel ×K=5) | Before SHIP | READ-ONLY | Cluster-scoped partials; Aggregator merges deliverable-wide |

### Every round = a NEW fresh sub-agent

A second audit round is a brand-new Auditor sub-agent, NOT the same Auditor "running again." A fix follow-up after a Fixer's first attempt is a brand-new Fixer. The fresh-context property is the entire point — it's what prevents leniency / motivated-stopping / stale-memory failure modes. Reusing context across roles defeats the whole pattern.

The orchestrator never short-circuits this for "quick" work. A one-line typo fix still spawns a Planner, Implementer, Auditor, and (if findings) Fixer. Sub-agent invocation cost is small; production regression cost is large.

### The orchestrator cannot mark CLEAN

The orchestrator consumes Auditor verdicts; it cannot promote a step to CLEAN by judgment. CLEAN means "the latest Auditor sub-agent's big table contained zero FINDING rows." If the orchestrator wants to confirm closure, it spawns a fresh Auditor — it does not eyeball.


<sup>[↑ jump to top](#top)</sup>

---

## Folder shape

```
docs/
├── dev/
│   ├── workflow.md                          ← this file (committed)
│   ├── rules.md                       ← the rule catalog (committed)
│   └── deliverables/                        ← surviving root READMEs (committed snapshots)
│       ├── README.md                        ← what lives here
│       ├── 0001-auth-outbound.md
│       └── 0002-handler-stack.md
└── wip/                                     ← gitignored; per-deliverable local workspace
    └── NNNN-<deliverable>/                  ← 4-digit deliverable index, e.g. `0001-shared-libs-review/`
        ├── README.md                        ← progress tracker + final report (snapshot copied to deliverables/ at ship)
        ├── 01-<step-name>/
        │   └── journal.md                   ← append-only; LOCAL-ONLY, never committed; never auto-deleted
        ├── 02-<step-name>/
        │   └── journal.md
        ├── ...
        └── final-review/
            └── journal.md
```

**Naming convention**: deliverables use a 4-digit index prefix (`0001-`, `0002-`, ...) so they sort naturally in directory listings and the order reflects ship sequence. Both the local workspace folder (`docs/wip/NNNN-<name>/`) and the committed snapshot (`docs/dev/deliverables/NNNN-<name>.md`) share the same index — matching prefixes make it trivial to find the local workspace for a past committed snapshot (if the journals still exist locally). Pick the next free index at PLAN time by `ls docs/dev/deliverables/` + incrementing the highest.

At SHIP, **only the root README is copied** out of `wip/NNNN-<name>/` to `docs/dev/deliverables/NNNN-<name>.md` (committed snapshot — single file). The per-step journals stay where they are in `docs/wip/NNNN-<name>/` — gitignored, local-only artifacts. They are NEVER auto-deleted by the workflow; the user removes them manually whenever they want. Locally-preserved journals remain available as evidence that future deliverables can spot-check, but they don't cross the commit boundary — only the distilled README does. See SHIP step 4 below.


<sup>[↑ jump to top](#top)</sup>

---

## PLAN

The user and agent reach alignment on what's being built. Output: a fully-populated `docs/wip/<deliverable>/README.md` plus the empty step folders.

### Steps

0. **READ [rules.md](rules.md) END-TO-END.** Mandatory before any other PLAN activity. The catalog is the requirements you'll be held to during EXECUTE — knowing them upfront is what lets you write code that passes the audit on round 1 instead of round 5. Skipping this step means architectural mistakes get baked in at design time; "I'll just check the rules during audit" is what creates multi-pass loops.
1. **Discuss + lock high-level goal.** Loop until the user agrees on what success looks like. The agent captures this as the first journal entry under the soon-to-be-created `docs/wip/<deliverable>/`.
2. **Create the deliverable workspace.** `docs/wip/<deliverable>/README.md` is created with the populated tracking sections (see template below). Each step gets a numbered folder (`01-<short-name>/`, `02-<short-name>/`, etc.) with an empty `journal.md`.
3. **Break into steps.** A step = one csproj or equivalent shippable bundle. Loop with the user until step list + ordering + prerequisites are agreed.
4. **Lock detailed design per step.** Discuss trade-offs, alternatives considered, layer choices (which ctor, which interface, which transport). Document the rejected alternatives — these are the most valuable thing the journal carries forward when architectural mistakes at design time need to be diagnosed later.
5. **Risk pass — walk every rules.md category against the design.** Security, race conditions, PII, graceful degradation, layer hygiene, observability, idempotency, configuration, failure modes. For each category, ask: "what predicates apply to this design? does the design satisfy them upfront?" Refine the design. Loop until agreed.
6. **PLAN exit.** Root README has populated step list + cross-cutting decisions + open questions = empty. Step folders exist with empty journals. Agent has confirmed end-to-end read of rules.md in the journal. Agent now enters EXECUTE.

### `docs/wip/<deliverable>/README.md` template (initial form, populated during PLAN)

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

---

## EXECUTE

For each step in order (respecting prerequisites), the **main-thread orchestrator** drives the per-step loop by spawning fresh sub-agents (per [Orchestrator-only main thread](#orchestrator-only-main-thread-canonical) above). The orchestrator itself never edits source, never walks `rules.md`, never marks anything CLEAN.

### 1. Spawn Planner sub-agent (step plan entry)

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

### 2. Spawn Implementer sub-agent

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

### 3. Audit loop (the core forcing function)

For EACH round, the orchestrator dispatches a **K=5 batch of fresh Auditor sub-agents** in parallel (READ-ONLY tools — cannot edit source), then a **fresh Aggregator sub-agent** once all 5 partials return. Each cluster Auditor walks its slice of [rules.md](rules.md) per the canonical 5-cluster partition in [audit-framework.md §3a](audit-framework.md#3a-auditor-cluster-partition-canonical-k5) and produces evidence per predicate — grep results, file:line lists, "checked X by Y, found Z." Vibes ("looks fine") are not evidence. Each Auditor writes to its own partial file (`r{N}-partial-{LETTER}-{cluster-name}.md`); the **Aggregator** merges the 5 partials into the canonical big table (REPLACES `## Latest sweep results`) and appends a single `### Round N findings` subsection covering all 5 clusters + cross-cluster verification (per [audit-framework.md §3b](audit-framework.md#3b-aggregator-role-post-cluster-consolidation)).

The orchestrator's per-round dispatch workflow (shared-context file, parallel `Agent` batch in one tool-call message, post-batch Aggregator dispatch, route on Aggregator's recommendation) is canonical in [audit-framework.md §3c](audit-framework.md#3c-per-round-dispatch-protocol).

When the Aggregator surfaces FINDING rows, the orchestrator spawns a **fresh Fixer sub-agent** with the consolidated findings list. The Fixer applies fixes + appends fix-log entries — it cannot mark anything CLEAN; closure is proven only by the NEXT round's fresh K=5 Auditor batch + Aggregator walking the predicates again and not surfacing the finding.

A second audit round is a BRAND-NEW K=5 Auditor batch + brand-new Aggregator, not the same ones re-running. The fresh-context property is non-negotiable.

**Wall-clock**: a K=5 batch's wall-clock is dominated by the slowest cluster (not the sum of 5). Empirically ~1/4-1/5 of a sequential K=1 walk against the same predicate count, since parallel Auditors stay in their cluster's mental frame instead of context-switching across all 24 categories.

**K=1 carve-out**: orchestrator MAY run K=1 (single Auditor doubles as Aggregator) for truly tiny steps where partitioning offers no parallelism win (one-line config tweak, single-line typo). Requires a one-sentence justification in the orchestrator's dispatch note. K=5 is the default.

#### MANDATORY: per-round evidence table embedded in the journal (no exceptions)

> **DO NOT BE LAZY. WALK EVERY NUMBERED SUBSECTION IN rules.md. NO SKIPPING. NO ASSUMING IRRELEVANCE WITHOUT EVIDENCE. LEAVE NO STONE UNTURNED.**
>
> The whole point of the audit loop is to catch what mechanical gates (build / test / inspectcode) miss. Short-circuiting ("I checked the relevant ones, the rest don't apply") IS the failure mode this whole framework exists to prevent. Most rules.md subsections WILL apply to most code. Be skeptical of your own urge to mark N/A.

### Three-artifact journal model (mirror of CLAUDE.md / rules.md §24)

Every step / final-review journal contains THREE artifacts under canonical headings — strictly separated, never collapsed:

| Artifact | Section heading | Behavior | Written by |
|---|---|---|---|
| **Big table** (latest sweep snapshot) | `## Latest sweep results` | REPLACED on every sweep — table reflects ONLY the most recent walk's findings against current code. ~85+ rows, one per rules.md subsection. Anti-laziness preamble above it. | Sweep activity ONLY. Fix-applying agents NEVER touch this. |
| **Findings log** (per-round audit history) | `## Sweep findings log (append-only)` | APPEND-ONLY. Each sweep appends a `### Round N findings (timestamp)` subsection enumerating every FINDING the sweep surfaced. Never deleted, never re-ordered. | Sweep activity ONLY. |
| **Fix log** (chronological fix activity) | `## Fix log (append-only)` | APPEND-ONLY. Each fix appends one entry citing rules.md subsection + finding round + what changed + `file.cs:NN` of the change. Never deleted, never re-ordered. | Fix-applying agent ONLY. |

The big table is the canonical "what is true RIGHT NOW" snapshot. Every PASS in it is a fresh file:line citation against current code, freshly walked in the latest sweep. There is NO inheritance of PASS from earlier sweeps — every PASS is earned fresh each sweep.

Closure is proven ONLY by the absence of a FINDING from the next sweep's big table. The fix log captures intent + action; it does NOT certify outcome.

### Mandatory round sequence (do not skip steps, do not collapse roles)

1. **Sweep**: walk every rules.md subsection against current code. REPLACE the big table with the sweep's output. APPEND a `### Round N findings (timestamp)` subsection to the findings log enumerating every FINDING the sweep surfaced.
2. **Fix work**: for each FINDING in the new big table, apply the fix. After each fix, APPEND one entry to the fix log citing the rules.md subsection + finding round + what changed + the `file.cs:NN` of the change. **The big table is NOT touched between sweeps.**
3. **Every finding gets fixed**: no silent carryover. If a finding genuinely can't be resolved in this round, get EXPLICIT user permission to defer and append a deferral entry to the fix log (still append-only — never silent omission).
4. **Next sweep**: when all current-round findings have fix-log entries, run the NEXT sweep. Walk the full rules.md catalog again from scratch. REPLACE the big table with the new sweep's output. Append `### Round N+1 findings` to the findings log. A row that was a FINDING in Round N's findings log and is now PASS in Round N+1's big table = closed (proven by absence). A row STILL a FINDING in Round N+1's table = fix didn't take; append more fix entries, run Round N+2.
5. **Loop terminates** when ONE sweep produces a big table with zero FINDING rows. Until that happens, the step is not done. No "convergence claimed" without a clean big table from a real sweep.

### Why the table is sweep-only-replaceable

If the fix-applying agent could flip a row to PASS, failure mode: fix doesn't actually take (typo, wrong line, partial replacement, cascade) → agent writes PASS anyway → next sweep "trusts" the PASS and skips re-walking the predicate → bug ships. With sweep-only-replacement of the big table, every PASS in every sweep's table is freshly walked against current code. There's no possibility of a stale PASS being inherited.

### Why findings + fixes are append-only

The append-only logs preserve the audit trail that the table-replacement model would otherwise lose. Anyone reading the journal can answer: "What did Round 1 find?" "What was changed in response?" "Did Round 2's sweep confirm closure?" An agent that could delete entries could quietly hide reversals or corrections — append-only forces every change (including reversals) into chronological visible order.

Every audit round produces a STRUCTURED TABLE with one row per numbered subsection in `rules.md`. The table is the gate — a step is not done until a complete-table round shows zero FINDING rows.

```
=================================================
[YYYY-MM-DD HH:MM] Audit round N
=================================================

MANDATORY PREAMBLE: This audit walks EVERY numbered subsection in rules.md.
No skipping. No assuming irrelevance without specific evidence. PASS requires
file:line citation. N/A requires one-sentence reason. FINDING requires
severity + file:line + description + fix. The step is NOT done until a
complete-table round shows zero FINDING rows. DO NOT BE LAZY. LEAVE NO STONE
UNTURNED.

| §    | Predicate                                         | Status            | Evidence / Reason / Finding                              |
|------|---------------------------------------------------|-------------------|----------------------------------------------------------|
| 1.1  | Test every public path first-pass                 | ✅ PASS           | HttpJwksProvider.GetKeysAsync → tests/Jwks/HttpJwksProviderTests.cs:23 |
| 1.2  | Adversarial inputs in tests                       | ❌ FINDING-MEDIUM | tests/Jwks/HttpJwksProviderTests.cs missing oversized-payload case → add test_OversizedJwks_ReturnsServiceUnavailable |
| 1.3  | DI extension methods tested via composition resolution | ⚪ N/A         | No DI extensions added in this step (existing AddD2Auth stub from Step 02 unchanged) |
| 1.4  | gRPC client/server registration helpers tested    | ⚪ N/A            | No gRPC code in this step |
| ...  | (every numbered subsection in rules.md, in order) | ...               | ...                                                      |
| 23.7 | Config validations at startup, not on first use   | ✅ PASS           | AuthOptions validation runs at AddD2Auth time → AuthServiceCollectionExtensions.cs:46 |

Round summary: 1 HIGH, 2 MEDIUM, 0 LOW findings | 3 fixes applied | post-fix
build clean | re-running round N+1 against post-fix state.
```

#### Evidence requirements (mechanical — no exceptions)

- **PASS** requires a `file:line` citation pointing to code/test/doc that satisfies the predicate. "Verified ✓" / "looks good" / "checked it" are NOT evidence.
- **N/A** requires a one-sentence REASON specific to the step's scope. "Doesn't apply" / "irrelevant" are NOT reasons. Acceptable reason shapes: "no TS code in this step", "no DI extensions added", "no Redis interaction", etc.
- **FINDING** requires all four: (severity: HIGH/MEDIUM/LOW) + (file:line) + (specific description of the violation) + (suggested fix). Fix is applied in the same round; the next round runs against post-fix state.

#### MANDATORY: emoji-prefixed Status column (no exceptions)

**The Status column MUST prepend the emoji indicator: ✅ PASS / ❌ FINDING-* / ⚪ N/A / 🟡 anything else. Visual scan-ability is the goal — operators reviewing the journal can spot findings instantly.**

Emoji mapping (the FOUR canonical visual indicators):

- `✅` (green checkmark) — for `PASS` (the canonical clean state)
- `❌` (red X) — for any `FINDING-*` row (`FINDING-HIGH` / `FINDING-MEDIUM` / `FINDING-LOW`)
- `⚪` (white circle) — for `N/A` (predicate doesn't apply to this step's scope)
- `🟡` (yellow circle) — for ANY non-canonical status (`DEFERRED` / `PENDING` / `UNVERIFIED` / `PASS-borderline` / `PASS (contract)` / `PARTIAL` / etc. — anything that isn't strictly `PASS` / `N/A` / `FINDING-*`). Yellow flags "needs human attention" — a borderline PASS, a deferred fix, a pending re-walk, a partial.

Format: emoji + single space + status word — e.g. `| 1.1 | ... | ✅ PASS | <evidence> |` or `| 11.22 | ... | ❌ FINDING-LOW | <description> |` or `| 1.3 | ... | ⚪ N/A | <reason> |` or `| 24.5 | ... | 🟡 DEFERRED | <reason> |`.

A row with a bare `PASS` / `N/A` / `FINDING-*` (no emoji prefix) is a §24.10 violation. The sweep that produced such a row is INCOMPLETE.

#### Loop until zero findings (mechanical)

A step is NOT complete until an audit round produces ZERO FINDING rows across the COMPLETE TABLE. After fixing findings in round N, run round N+1 → re-walk the FULL table against the post-fix state. **DO NOT assume a single round of fixes resolved everything** — fixes can introduce new issues. The loop terminates only when a complete-table walk shows zero FINDING rows.

#### FINAL-REVIEW uses the same table

The deliverable-wide FINAL-REVIEW produces the same complete-table format against the entire deliverable's cumulative output. **No tier-audit layer between per-step and final-review** — per-step audit scope explicitly includes every file the step touched (incl. files modified from prior steps), so cross-step drift is caught at the per-step level. The final-review then walks the deliverable as a whole for cross-cutting integration concerns no individual step would surface.

Continue rounds. Termination: a round produces 0 findings across every category. Append final entry:

```
=================================================
[YYYY-MM-DD HH:MM] Audit round K — TERMINATED CLEAN
=================================================
All 8 categories returned 0 findings. Step audit complete.
Total rounds to clean: K (within 10-iteration ceiling)
```

If iteration 11 is reached without convergence, STOP and escalate:

```
=================================================
[YYYY-MM-DD HH:MM] ESCALATION — 10-iteration ceiling reached
=================================================
Pattern of findings across rounds: <summary>
Suspected root cause: <agent's hypothesis>
Question for user: <specific ask>
```

### 4. Per-step distillation

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

### 5. Update root README

After the step distillation, the **orchestrator** updates `docs/wip/<deliverable>/README.md` (this is one of the few `Edit` activities the orchestrator may perform itself, since the root README is the orchestrator's tracking artifact):
- Step status: ⏸ → 🔄 → ✅ (with iteration count: "✅ 02-service-identity-stack (3 audit rounds to clean)")
- Append to "Kinds-of-misses log" with the step's distillation summary
- If new cross-cutting decisions surfaced, append to that section

### 6. Move to next step

Steps run in prerequisite order. Step N can start when all listed prerequisites are ✅. The orchestrator does NOT spawn a new Planner sub-agent for step N while the previous step has open audit findings.


<sup>[↑ jump to top](#top)</sup>

---

## FINAL-REVIEW (the last "step" of every deliverable)

Same orchestrator-driven loop as EXECUTE, but scope = the whole deliverable. Catches integration / consistency bugs that no single-step audit would find: cross-step type drift, telemetry tag drift between two libs, README parity across all touched files, end-to-end integration paths.

Folder: `docs/wip/<deliverable>/final-review/journal.md`.

Same structure as a step — the orchestrator spawns fresh sub-agents per phase:
1. Spawn fresh **Planner** for cross-step concerns to walk (defines the deliverable-wide cross-cutting focus areas the Aggregator verifies in step 3b/4)
2. Spawn fresh **Implementer** for any cross-cutting fixes (only if planning surfaces work)
3. Dispatch fresh **K=5 Final-reviewer batch** per round (READ-ONLY) per the canonical cluster partition in [audit-framework.md §3a](audit-framework.md#3a-auditor-cluster-partition-canonical-k5) — same `rules.md` ruleset, scope = whole deliverable. After all 5 partials return, dispatch fresh **Aggregator** to merge per [audit-framework.md §3b](audit-framework.md#3b-aggregator-role-post-cluster-consolidation). Each round = a brand-new K=5 batch + brand-new Aggregator.
4. Spawn fresh **Fixer** when Aggregator surfaces findings
5. 10-iteration ceiling (where ONE iteration = one K=5 batch + Aggregator + Fixer); escalate if hit
6. Distillation entry

When the latest Aggregator's big table comes back with zero FINDING rows → deliverable is ready to SHIP.


<sup>[↑ jump to top](#top)</sup>

---

## SHIP (handoff to user REVIEW)

Triggered by final-review's clean termination. Agent does:

0. **Walk the [Deliverable completeness checklist](rules.md#deliverable-completeness-checklist-the-gate-before-user-review) BEFORE anything else in SHIP.** Every box must be honest YES with a citation. If any box is NO, SHIP is not ready — go back into fix-loops, re-walk the checklist, only proceed when every box is honestly YES. Then write the verbatim attestation block (from rules.md) into the deliverable's root README. Without the attestation, SHIP cannot proceed.
1. **Aggregate proposed rule additions** from all step distillations + final-review distillation. Deduplicate. Append the full proposed list to the root README's "Proposed rule additions" section.
2. **Present the root README to the user**. The user reviews:
   - Did the agent's audit catch what the user would have caught? (Implicit: spot-check 1-2 step journals, see if any obvious miss got past.)
   - Approve / tweak each proposed rule addition.
   - Approve the deliverable to merge.
3. **Apply approved rule additions** to `docs/dev/rules.md` (committed change).
4. **Copy the root README as a snapshot** from `docs/wip/NNNN-<name>/README.md` to `docs/dev/deliverables/NNNN-<name>.md` (committed — single file). The "Status" line flips to `SHIPPED YYYY-MM-DD`; the final-report section is populated; references to per-step journals get rephrased as prose since the journals don't cross the commit boundary.
5. **Leave the wip/ workspace untouched.** The per-step journals + root README + final-review journal stay where they are in `docs/wip/NNNN-<name>/` — gitignored, local-only. The workflow does NOT auto-delete them. The user removes them manually whenever they want (e.g. when freeing local disk space, when archiving the project). Until then, they remain available locally as audit-trail evidence.
6. **Commit** in this order, separately:
   - Approved `rules.md` additions
   - The shipped deliverable code (squash-merge from feature branch)
   - The new `docs/dev/deliverables/NNNN-<name>.md` snapshot

Each commit needs explicit user permission (no auto-commit).


<sup>[↑ jump to top](#top)</sup>

---

## REVIEW (user phase)

User reviews the shipped deliverable. **REVIEW is observe-and-capture, not fix-on-sight.** When the user surfaces feedback:

1. Agent captures the feedback as a numbered list — does NOT fix anything yet.
2. Per item, agent confirms understanding + asks "fix? leave? discuss further?"
3. User decides per item.
4. Approved fixes get a fresh deliverable folder (or, for trivial single-item fixes, a small follow-up commit with a regression test).

If REVIEW finds bugs that should have been caught by the agent's audit rounds, the right response isn't just "fix the bug" — it's also "what category was this, and why didn't the predicate catch it?" That gap becomes a new predicate in `rules.md`. Without this feedback loop, the rule catalog stays static and the agent keeps making the same kinds of misses.


<sup>[↑ jump to top](#top)</sup>

---

## Append-only discipline

Per-step `journal.md` files are append-only at the **substantive content** level:
- ✅ Fix typos / formatting / markdown rendering issues
- ❌ Rewrite an audit finding to make it look smaller in retrospect
- ❌ Delete entries from earlier rounds
- ❌ Edit a previous round's "Findings: 0 (clean)" to add the bug a later round found

The reason: the journal IS the evidence of process integrity. If round 3 missed something that round 5 caught, the journal must show that. Hiding the miss prevents the kind from feeding back into `rules.md`, and the agent will re-make the same miss next deliverable. **Honest journals are self-rewarding** — every honest miss becomes a future gate-check.


<sup>[↑ jump to top](#top)</sup>

---

## Permission gates (must block, no inference allowed)

The following actions require explicit user permission **per occurrence**, not implied from prior turns:

- **Commit creation.** "go ahead and commit" approves the batch just discussed; the next commit needs fresh permission.
- **Bulk file operations** (sed across N files, mass rename, multi-file delete, bulk format-write). Agent declares scope (file count, glob, what changes) BEFORE executing; user has the chance to redirect.
- **Destructive git operations** (force push, hard reset, branch delete, checkout that overwrites uncommitted work).
- **Deferring planned work.** If a step turns out larger than expected, agent ASKS to defer — does not unilaterally skip.
- **Architectural decision changes mid-execution.** If implementation surfaces a reason to deviate from the locked PLAN, agent ASKS — does not silently rework.


<sup>[↑ jump to top](#top)</sup>

---

## Scope of work shape

This workflow scales to deliverables of meaningfully different sizes. Two examples:

**Small deliverable** — one csproj, one logical feature. Step list: `01-<feature>` + `final-review`. Two journals. Most of the value is the first-pass discipline + the journal artifact for the user to review.

**Large deliverable** — multi-csproj refactor or build-out. Step list: `01-csproj-1` through `09-csproj-9` + `final-review`. Ten journals. Cross-cutting decisions surface in the root README; per-step journals carry per-csproj detail.

There's no "lightweight path" for trivial changes — even a typo fix benefits from "did you check whether this typo appears elsewhere in the same doc?" The cost of running the full ruleset on a small change is minutes; the cost of NOT running it (and missing the parallel typo) is a future audit round. **The orchestrator-only-main-thread + fresh-sub-agent-per-round pattern (see [Orchestrator-only main thread](#orchestrator-only-main-thread-canonical) above) applies at every scope: a one-line typo fix still spawns a Planner / Implementer / Auditor / (if findings) Fixer chain. Sub-agent invocation cost is small; production regression cost is large.**


<sup>[↑ jump to top](#top)</sup>

---

## What this workflow does NOT do

- **Doesn't replace CLAUDE.md.** CLAUDE.md still defines the conventions, patterns, and project-specific rules that shape the *content* of code. This workflow defines the *process* that ensures the conventions are actually followed.
- **Doesn't replace `docs/v2/`.** Phase / wave tracking continues to live in the `docs/v2/` set. This workflow is per-deliverable; `docs/v2/` is the long-arc roadmap.
- **Doesn't replace per-lib READMEs.** Each shared lib still has its own `README.md` documenting its public API. This workflow doesn't generate or maintain those.
- **Doesn't run scripts.** No pre-commit hook, no CI gate that fires `rules.md` mechanically. The discipline is the agent walking the rules each round and producing evidence — verifiable by inspecting the journal.


<sup>[↑ jump to top](#top)</sup>

---

## When to invoke this workflow

Always, for any work substantial enough to warrant a deliverable folder. The user can override per-task ("just do this small thing, no journal needed"), but the default is "every meaningful unit of work uses the loop."

The forcing function for the agent: if there's no `docs/wip/<deliverable>/README.md` for the work in flight, the agent should ASK whether to create one before proceeding past PLAN.
