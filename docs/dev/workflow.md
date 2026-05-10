<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2-WORX Development Workflow

The loop that turns a design discussion into shipped, audited, regression-tested code without requiring the user to push the agent through audit rounds. Three phases: **PLAN → EXECUTE → REVIEW**. Convergence is autonomous — the agent loops until each step's audit terminates clean, then ships to user review.

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

---

## EXECUTE

For each step in order (respecting prerequisites):

### 1. Step plan entry

Append to `docs/wip/<deliverable>/<NN>-<step>/journal.md`:

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

The pre-emptive gate checks exist to push category-A/E/F catches to BEFORE the code is written, not after. This is where the loop count drops from 5 rounds to 1-2.

### 2. Implementation

Write the code + the corresponding tests. Append:

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

### 3. Audit loop (the core forcing function)

Walk every category in [rules.md](rules.md). For each predicate, produce evidence — grep results, file:line lists, "checked X by Y, found Z." Vibes ("looks fine") are not evidence. Findings get fixed in the same round; the next round runs against post-fix state.

Append per round:

```
=================================================
[YYYY-MM-DD HH:MM] Audit round N
=================================================
Categories walked: 1, 2, 3, 4, 5, 6, 7, 8

Category 1 — Test Discipline:
  Findings: 2
    1. <description, file:line>
    2. <description, file:line>
  Fixes:
    1. <change made> — <why>
    2. <change made> — <why>

Category 2 — Bug-Fix Regression Testing:
  Findings: 0 (clean)

Category 3 — PII / Logging Safety:
  Findings: 1
    1. ...
  Fixes:
    1. ...

...

Round summary: 3 findings, 3 fixed, post-fix build clean.
```

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

Once the step terminates clean, append to the step journal:

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

After the step distillation, the agent updates `docs/wip/<deliverable>/README.md`:
- Step status: ⏸ → 🔄 → ✅ (with iteration count: "✅ 02-service-identity-stack (3 audit rounds to clean)")
- Append to "Kinds-of-misses log" with the step's distillation summary
- If new cross-cutting decisions surfaced, append to that section

### 6. Move to next step

Steps run in prerequisite order. Step N can start when all listed prerequisites are ✅. The agent does NOT start a new step while the previous step has open audit findings.

---

## FINAL-REVIEW (the last "step" of every deliverable)

Same loop as EXECUTE, but scope = the whole deliverable. Catches integration / consistency bugs that no single-step audit would find: cross-step type drift, telemetry tag drift between two libs, README parity across all touched files, end-to-end integration paths.

Folder: `docs/wip/<deliverable>/final-review/journal.md`.

Same structure as a step:
1. Plan entry (what cross-step concerns to walk)
2. Implementation (any cross-cutting fixes)
3. Audit loop with the SAME `rules.md` ruleset, scope = whole deliverable
4. 10-iteration ceiling, escalate if hit
5. Distillation entry

When final-review terminates clean → deliverable is ready to SHIP.

---

## SHIP (handoff to user REVIEW)

Triggered by final-review's clean termination. Agent does:

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

---

## REVIEW (user phase)

User reviews the shipped deliverable. **REVIEW is observe-and-capture, not fix-on-sight.** When the user surfaces feedback:

1. Agent captures the feedback as a numbered list — does NOT fix anything yet.
2. Per item, agent confirms understanding + asks "fix? leave? discuss further?"
3. User decides per item.
4. Approved fixes get a fresh deliverable folder (or, for trivial single-item fixes, a small follow-up commit with a regression test).

If REVIEW finds bugs that should have been caught by the agent's audit rounds, the right response isn't just "fix the bug" — it's also "what category was this, and why didn't the predicate catch it?" That gap becomes a new predicate in `rules.md`. Without this feedback loop, the rule catalog stays static and the agent keeps making the same kinds of misses.

---

## Append-only discipline

Per-step `journal.md` files are append-only at the **substantive content** level:
- ✅ Fix typos / formatting / markdown rendering issues
- ❌ Rewrite an audit finding to make it look smaller in retrospect
- ❌ Delete entries from earlier rounds
- ❌ Edit a previous round's "Findings: 0 (clean)" to add the bug a later round found

The reason: the journal IS the evidence of process integrity. If round 3 missed something that round 5 caught, the journal must show that. Hiding the miss prevents the kind from feeding back into `rules.md`, and the agent will re-make the same miss next deliverable. **Honest journals are self-rewarding** — every honest miss becomes a future gate-check.

---

## Permission gates (must block, no inference allowed)

The following actions require explicit user permission **per occurrence**, not implied from prior turns:

- **Commit creation.** "go ahead and commit" approves the batch just discussed; the next commit needs fresh permission.
- **Bulk file operations** (sed across N files, mass rename, multi-file delete, bulk format-write). Agent declares scope (file count, glob, what changes) BEFORE executing; user has the chance to redirect.
- **Destructive git operations** (force push, hard reset, branch delete, checkout that overwrites uncommitted work).
- **Deferring planned work.** If a step turns out larger than expected, agent ASKS to defer — does not unilaterally skip.
- **Architectural decision changes mid-execution.** If implementation surfaces a reason to deviate from the locked PLAN, agent ASKS — does not silently rework.

---

## Scope of work shape

This workflow scales to deliverables of meaningfully different sizes. Two examples:

**Small deliverable** — one csproj, one logical feature. Step list: `01-<feature>` + `final-review`. Two journals. Most of the value is the first-pass discipline + the journal artifact for the user to review.

**Large deliverable** — multi-csproj refactor or build-out. Step list: `01-csproj-1` through `09-csproj-9` + `final-review`. Ten journals. Cross-cutting decisions surface in the root README; per-step journals carry per-csproj detail.

There's no "lightweight path" for trivial changes — even a typo fix benefits from "did you check whether this typo appears elsewhere in the same doc?" The cost of running the full ruleset on a small change is minutes; the cost of NOT running it (and missing the parallel typo) is a future audit round.

---

## What this workflow does NOT do

- **Doesn't replace CLAUDE.md.** CLAUDE.md still defines the conventions, patterns, and project-specific rules that shape the *content* of code. This workflow defines the *process* that ensures the conventions are actually followed.
- **Doesn't replace `docs/v2/`.** Phase / wave tracking continues to live in the `docs/v2/` set. This workflow is per-deliverable; `docs/v2/` is the long-arc roadmap.
- **Doesn't replace per-lib READMEs.** Each shared lib still has its own `README.md` documenting its public API. This workflow doesn't generate or maintain those.
- **Doesn't run scripts.** No pre-commit hook, no CI gate that fires `rules.md` mechanically. The discipline is the agent walking the rules each round and producing evidence — verifiable by inspecting the journal.

---

## When to invoke this workflow

Always, for any work substantial enough to warrant a deliverable folder. The user can override per-task ("just do this small thing, no journal needed"), but the default is "every meaningful unit of work uses the loop."

The forcing function for the agent: if there's no `docs/wip/<deliverable>/README.md` for the work in flight, the agent should ASK whether to create one before proceeding past PLAN.
