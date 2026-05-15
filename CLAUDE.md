<!--
Copyright (c) DCSV. All rights reserved.
-->

<a name="top"></a>

# CLAUDE.md — D²-WORX Development Guide

**D²-WORX** — Microservices SaaS framework. C# 14 / .NET 10 backend, SvelteKit BFF (TypeScript 5.9 / Svelte 5). Pre-Alpha. PolyForm Strict license (reference implementation, non-commercial).

## Table of contents

**Top-of-file mandatory blocks** (read these every session):

- [📍 Project state pointer](#claudemd--dworx-development-guide)
- [⚠️ MANDATORY — orchestrator-only main thread](#mandatory-block-0-orchestrator-only-main-thread)
- [⚠️ MANDATORY — applies to every code change](#mandatory-block-1-every-code-change)
- [⚠️ MANDATORY — audit evidence & proof discipline](#mandatory-block-2-audit-evidence--proof-discipline)
- [⚠️ Deliverable completeness checklist — gate before user review](#mandatory-block-3-deliverable-completeness-checklist)

**Sections:**

1. [§1. Development Workflow](#1-development-workflow)
   - [Phase summary](#phase-summary)
   - [Permission gates (must block — no inferred permission)](#permission-gates-must-block--no-inferred-permission)
   - [Self-improvement (the key insight)](#self-improvement-the-key-insight)
2. [§2. Commands](#2-commands)
3. [§3. Reference Documents](#3-reference-documents)
4. [§3.5. Doc Update Map](#35-doc-update-map)
5. [§4. Patterns & Architecture](#4-patterns--architecture)
   - [TLC/2LC/3LC Folder Convention](#tlc2lc3lc-folder-convention)
   - [CQRS Handler Categories](#cqrs-handler-categories)
   - [Verb Semantics](#verb-semantics)
   - [Handler Pattern](#handler-pattern)
   - [D2Result Pattern](#d2result-pattern)
   - [Other Established Patterns](#other-established-patterns)
   - [Key Architecture Decisions](#key-architecture-decisions)
6. [§5. Critical Reminders (top-of-mind for every change)](#5-critical-reminders-top-of-mind-for-every-change)
7. [§6. Code Conventions](#6-code-conventions)
8. [§7. Behavioral Guidelines (how to approach work)](#7-behavioral-guidelines-dispositional--how-to-approach-work)
   - [Code Intelligence Tools](#code-intelligence-tools)
   - [Windows LSP Workaround](#windows-lsp-workaround)
   - [Project Structure](#project-structure)
9. [§8. Local Secrets & Claude Deny Rules](#8-local-secrets--claude-deny-rules)

> This doc covers process, patterns, and code rules. Architectural decisions live distributed across the docs in `docs/` (PATTERNS.md, TESTS.md, MESSAGING.md, OPERATIONAL-GUARANTEES.md, AUDIT_CHECKLIST.md, etc.) and the per-lib / per-service `README.md` files.

> **📍 PROJECT STATE — READ FIRST**: The active project tracking doc is **[docs/v2/PHASE_0.md](docs/v2/PHASE_0.md)** (current phase, status, open questions, deferred work, resolved decisions). This pointer is the single source for "what's the project doing right now" — when this doc archives, the pointer here gets updated to its successor. A frozen v1 snapshot lives at `/old/v1/D2-WORX/` (read-only, reference for historical patterns not yet captured in current docs).

<a name="mandatory-block-0-orchestrator-only-main-thread"></a>

> # ⚠️⚠️⚠️ MANDATORY — ORCHESTRATOR-ONLY MAIN THREAD ⚠️⚠️⚠️
>
> **The main thread is an ORCHESTRATOR. It does NOT plan, implement, audit, or fix domain work itself. EVERY round of planning, implementation, auditing, and fixing is performed by a FRESH sub-agent spawned via the `Agent` tool. Reusing one sub-agent across roles, or letting the main thread do the domain work, defeats the entire pattern. This is the canonical workflow — not aspirational, not optional, not a "trial."**
>
> Empirical justification: deliverable 0002-auth-inbound trialed this pattern across 8 steps + final-review + 3 polish rounds. Per-step audits converged in 1-3 rounds (mostly 2). Two real production bugs were caught by adversarial separation that single-context implementation would have shipped:
> 1. `JwtAuthInterceptor.ResolveMethodScopeMetadata` reading the wrong `UserState` slot — caught by an integration test the Implementer skipped, that a Fixer was forced to add.
> 2. `act_chain_malformed` dead-letter chain: `MalformedActorChainException` propagating uncaught from `ClaimsToContextMapper.Map` — caught only by deliverable-wide enumeration ("AuthFailures helper exists + AuthErrorCodes constant exists + xmldoc enumerates the outcome + README documents it — but JwtValidator never emits it").
>
> The architectural justification is published research: Anthropic's [multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) (orchestrator-Opus + worker-Sonnet) outperforms single-agent Opus by 90.2% on internal evals; [adversarial code review research](https://asdlc.io/patterns/adversarial-code-review/) shows LLM self-review has systematic leniency bias and reviewer + generator share blind spots when run in the same context. "Most agent reviews agent implementations are one LLM with a clever prompt pretending to be three reviewers, where the model can rubber-stamp itself" — the only structural fix is SEPARATE sub-agent invocations with fresh contexts, not roleplay.
>
> **Allowed in main-thread context:**
> - `Agent` — spawn sub-agents (this is the primary orchestrator activity)
> - `TaskCreate` / `TaskUpdate` / `TaskList` / `TaskGet` / `TaskStop` — tracking
> - `Bash` for **git plumbing only** (`git status`, `git log`, `git commit -F <file>` when user-authorized, `git push` when user-authorized) — small non-domain output only
> - Reading sub-agent summaries (returned in `Agent` tool results)
> - `Read` / `Edit` / `Write` to the orchestrator's own decision log (`docs/wip/<deliverable>/orchestrator-log.md` or equivalent) and the deliverable root README's tracking sections
>
> **Forbidden in main-thread context:**
> - `Edit` / `Write` to ANY source file, test file, per-csproj README, per-service README, or framework doc
> - `Bash` for builds, tests, `jb inspectcode`, or any domain-level grep / inspection
> - `Read` on source files, test files, or per-lib READMEs — delegate to sub-agents (they have the fresh context to absorb domain detail)
> - Reading journal files mid-deliverable for content review — delegate state-checks to sub-agents that report back summary
> - Walking `rules.md` predicates — always done by Auditor sub-agents
> - Marking anything CLEAN / PASS / converged — those verdicts come from Auditor sub-agent output, never from main-thread judgment
>
> **Canonical sub-agent roles** (mirroring Anthropic's orchestrator-worker architecture; spec lives in [docs/dev/audit-framework.md §3](docs/dev/audit-framework.md#3-sub-agent-roles)):
>
> | Role | When spawned | Returns |
> |---|---|---|
> | **Planner** | Start of each step | Step Plan section appended to journal; rationale + decisions + pre-emptive gate checks |
> | **Implementer** | After Planner approval | Files-touched list + tests-added count + build/inspectcode status |
> | **Auditor** (parallel ×K) | After Implementer | Partial big-table chunk + per-category findings list (READ-ONLY tools — cannot edit source) |
> | **Aggregator** | After all Auditors | Canonical big table embedded in journal + per-category findings appended to findings log |
> | **Fixer** | When Auditor surfaces FINDING rows | Files-changed list + appended fix-log entries |
> | **Final-reviewer** (parallel ×K) | Before SHIP | Deliverable-wide partial big tables (cumulative output) |
>
> **EVERY ROUND of planning, implementation, auditing, and fixing requires a NEW fresh sub-agent.** A second audit round = a brand-new Auditor sub-agent, NOT the same Auditor "running again." A fix follow-up after a Fixer's first attempt = a brand-new Fixer. The fresh-context property is the entire point — it's what prevents the leniency / motivated-stopping / stale-memory failure modes.
>
> **The orchestrator NEVER short-circuits this for "quick" work.** A one-line typo fix still spawns a Planner, Implementer, Auditor, and (if findings) Fixer. The cost asymmetry is in your favor: sub-agent invocation is cheap, production regressions are expensive. The ONLY bypass is an explicit user request.
>
> **Detailed protocol → [docs/dev/workflow.md §EXECUTE](docs/dev/workflow.md#execute) and [docs/dev/audit-framework.md](docs/dev/audit-framework.md). The orchestrator + sub-agent separation is canonical now; the JSON-first / recipe-runner / validator-script tooling described in audit-framework.md is still aspirational (to-be-built).**
>
> **If you find yourself about to `Edit` a source file from the main thread, STOP. Spawn an Implementer sub-agent with the change spec. If you find yourself about to walk `rules.md` from the main thread, STOP. Spawn an Auditor. If you find yourself about to read a journal file to "check progress," STOP. Spawn a sub-agent to summarize state and report back. The discipline is structural, not optional.**

---

<a name="mandatory-block-1-every-code-change"></a>

> **⚠️ MANDATORY — APPLIES TO EVERY CODE CHANGE BY DEFAULT, NO EXCEPTIONS UNLESS THE USER EXPLICITLY ASKS FOR A BYPASS:**
>
> **EVERY code change — including "just change this one line", "rename this var", "fix this typo", "add this property", "tweak this config" — follows the Development Workflow (§1, detailed in [docs/dev/workflow.md](docs/dev/workflow.md)) AND adheres to every applicable predicate in [docs/dev/rules.md](docs/dev/rules.md).**
>
> **There is no "small change" carve-out.** A one-line edit IS a deliverable: it goes through PLAN (read rules.md, identify what categories apply, write a plan entry), EXECUTE (write the code, write the test, walk the audit loop until clean), and REVIEW (present to user for sign-off — DO NOT auto-commit). Skipping any of these because "it's just a small thing" is the failure mode this whole framework exists to prevent — small changes that bypass the process are how regressions ship, how PII leaks slip in, how docs drift, how conventions slip, how production breaks.
>
> **The ONLY way to bypass any part of this process is an explicit user request like "skip the journal for this", "no audit needed for this typo fix", "just commit it directly", or "don't write a test for this." Without that explicit bypass instruction, the process applies in full.**
>
> If a request seems too small to deserve the full process, ask the user: "this is a one-line change — should I do the full process or do you want to bypass [specific step]?" Default = full process.

---

<a name="mandatory-block-2-audit-evidence--proof-discipline"></a>

> # ⚠️⚠️⚠️ MANDATORY — AUDIT EVIDENCE & PROOF DISCIPLINE ⚠️⚠️⚠️
>
> **EVERY audit round, EVERY rule walk, EVERY PASS / N/A / FINDING / ACTION MUST be EMBEDDED — as a complete markdown evidence table — IN the journal markdown file (`docs/wip/<deliverable>/<NN>-<step>/journal.md`, or the final-review equivalent).**
>
> This is the SINGLE most important meta-requirement of this codebase's process. The whole framework exists because the user's trust depends on being able to OPEN A JOURNAL FILE and SEE the evidence — directly, completely, no scrolling agent transcripts, no "trust me I checked." If the journal contains only prose ("walked rules.md, fixed 3 findings, all green"), **the audit is INCOMPLETE — the step is NOT done — convergence has NOT been reached.**
>
> **What MUST appear in every step / final-review journal:**
>
> 1. A `## Latest sweep results` section containing the BIG TABLE (latest sweep's output — see lifecycle block below for the replace-on-each-sweep rule). Above the table: the anti-laziness preamble verbatim, plus a one-line `Last sweep: Round N (timestamp / commit ref)` marker.
> 2. The big table has **one row per numbered subsection in `rules.md`** (~85+ rows currently — every numbered subsection from §1.1 through §24.x).
> 3. Each row uses ONE of the canonical statuses ONLY, prepended with its visual emoji indicator: `✅ PASS` / `⚪ N/A` / `❌ FINDING-HIGH` / `❌ FINDING-MEDIUM` / `❌ FINDING-LOW`. Anything else (e.g. `DEFERRED` / `PENDING` / `UNVERIFIED` / `PASS-borderline` / `PASS (contract)` / `PARTIAL`) gets the `🟡` (yellow circle) prefix to flag "needs human attention." **The Status column MUST prepend the emoji indicator: ✅ PASS / ❌ FINDING-* / ⚪ N/A / 🟡 anything else. Visual scan-ability is the goal — operators reviewing the journal can spot findings instantly.** A row with a bare status word and no emoji prefix is a §24.10 violation.
> 4. **PASS rows** carry a `file.cs:NN` citation (no "verified ✓", no "checked it", no "looks good").
> 5. **N/A rows** carry a step-scope-specific REASON (no bare "doesn't apply" / "irrelevant").
> 6. **FINDING rows** carry severity + `file.cs:NN` + specific description + suggested fix — all four.
> 7. A `## Sweep findings log (append-only)` section containing per-round subsections (`### Round N findings (timestamp)`) — one for every sweep ever run on this step. Append-only.
> 8. A `## Fix log (append-only)` section containing chronological fix entries. Append-only.
>
> **Producing the table only in agent transcript output and writing a prose summary into the journal does NOT count.** The journal must be self-contained evidence — readable in isolation by anyone (the user during REVIEW, the agent in a future round, an auditor doing a post-mortem).
>
> ## ⚠️ ONE BIG TABLE = LATEST SWEEP RESULTS. APPEND-ONLY LOGS FOR FINDINGS AND FIXES. CLOSURE PROVEN BY ABSENCE IN NEXT SWEEP.
>
> Every step / final-review journal contains THREE artifacts — clearly separated, with strict roles, never collapsed:
>
> **Artifact 1: the BIG TABLE — latest-sweep snapshot (REPLACED every sweep).**
>
> A single markdown table under `## Latest sweep results` in the journal. One row per numbered subsection in `rules.md` (~85+ rows). The anti-laziness preamble appears verbatim above it. **The table reflects ONLY the most recent sweep's findings. When a new sweep runs, the entire table is REPLACED by the new sweep's output.** Earlier tables don't accumulate inside the journal — their content is preserved in the append-only findings log (Artifact 2). Treat the table as a `git checkout`-style overwrite each sweep, not as edit-in-place.
>
> The big table is the canonical "what is true RIGHT NOW" snapshot. Every PASS is a fresh file:line citation against the current code; every FINDING is the predicate failing against the current code. There is no inheritance of PASS from a previous sweep — PASS is earned by being walked AGAIN.
>
> **Artifact 2: the FINDINGS LOG — append-only, per-round.**
>
> Section under `## Sweep findings log (append-only)`. Each sweep appends a `### Round N findings (timestamp)` subsection containing every FINDING surfaced in that sweep — severity + file:line + description + suggested fix. Carries the full audit history across rounds: anyone can scroll back and see what was broken when, even after the big table got replaced. **Never deleted, never re-ordered, never reclassified.**
>
> **Artifact 3: the FIX LOG — append-only, chronological.**
>
> Section under `## Fix log (append-only)`. Each fix appends one entry: which finding it addresses (cite rules.md subsection + round it was found in), what was changed, the `file.cs:NN` of the change, the timestamp / commit reference. **Never deleted, never re-ordered.** Capturing intent and action — the trail of what the agent did between sweeps. The fix log does NOT certify outcome; certification comes from the next sweep's big table not surfacing the finding.
>
> **The three roles, never collapsed:**
>
> | Activity | Writes to |
> |---|---|
> | Sweep (rules.md walk) | REPLACES the big table; APPENDS to the findings log |
> | Fix work (code edits) | APPENDS to the fix log; **NEVER touches the big table** |
> | Closure verification | Reads next sweep's big table — finding ABSENT = closed; finding STILL PRESENT = re-fix |
>
> **Why the big table is REPLACED, not edited in-place by fixes:**
>
> If the fix-applying agent could flip a row to PASS, failure mode: fix doesn't actually take (typo, wrong line, partial replacement, cascade) → agent writes PASS anyway → next sweep "trusts" the PASS and skips re-walking the predicate → bug ships. With sweep-only-replacement of the big table, every PASS in every sweep's table is freshly walked against current code. There's no possibility of a stale PASS being inherited.
>
> **Why findings + fixes are APPEND-ONLY:**
>
> The append-only logs preserve history that the table-replacement model would otherwise lose. Anyone reading the journal can answer: "What did Round 1 find?" "What was changed in response?" "Did Round 2's sweep confirm the fix worked?" Without append-only logs, only the LATEST sweep's findings would be visible — the per-round audit history would be gone.
>
> ## ⚠️ MANDATORY ROUND SEQUENCE (do not skip steps, do not collapse roles):
>
> 1. **Sweep**: walk every rules.md subsection against the current state of the code. REPLACE the big table with the sweep's output. APPEND a `### Round N findings` subsection to the findings log with every FINDING the sweep surfaced.
> 2. **Fix work**: for each FINDING in the new big table, apply the fix. After each fix, APPEND one entry to the fix log citing the rules.md subsection + finding round + what changed + the `file.cs:NN` of the change. **The big table is NOT touched.**
> 3. **EVERY finding gets fixed**. No silent carryover. If a finding genuinely can't be resolved in this round, get EXPLICIT user permission to defer and append a deferral entry to the fix log (still append-only — never silent omission).
> 4. **Next sweep**: when all current-round findings have fix-log entries, run the NEXT sweep. Walk the full rules.md catalog again from scratch. REPLACE the big table with the new sweep's output. Append `### Round N+1 findings` to the findings log. A row that was a FINDING in Round N and is now PASS in Round N+1's table = closed. A row that's STILL a FINDING in Round N+1's table = fix didn't take; append more fix entries, run Round N+2.
> 5. **Loop terminates**: when ONE sweep produces a big table with zero FINDING rows. Until that happens, the step is not done. No "convergence claimed" without a clean big table from a real sweep.
>
> **Detailed protocol → [docs/dev/workflow.md §3](docs/dev/workflow.md#3-audit-loop-the-core-forcing-function). Predicate-level enforcement → [docs/dev/rules.md §24](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit), specifically §24.0 (three-artifact journal model) and §24.12 (self-audit compliance).**
>
> **Visual map of the full deliverable flow → [rules.md "Deliverable workflow chart — order of operations with loops"](docs/dev/rules.md#deliverable-workflow-chart--order-of-operations-with-loops).** Mermaid diagram + ASCII fallback + worked example showing PLAN → Step 1 (sweep+fix loops) → Step 2 → Step 3 → Final-review (sweep+fix loops) → SHIP.
>
> **If you find yourself about to claim "audit complete" without the 3-artifact journal model satisfied, STOP. Walk the model. Embed the artifacts. Then claim convergence.**

---

<a name="mandatory-block-3-deliverable-completeness-checklist"></a>

> # ⚠️⚠️⚠️ DELIVERABLE COMPLETENESS CHECKLIST — THE GATE BEFORE USER REVIEW ⚠️⚠️⚠️
>
> **Before declaring ANY deliverable "ready for REVIEW," walk the [Deliverable completeness checklist in rules.md](docs/dev/rules.md#deliverable-completeness-checklist-the-gate-before-user-review). Every box must be a YES with a citation. If any box is NO, the deliverable is NOT ready — go finish the gap and re-walk the checklist.**
>
> The checklist is the META-gate over the entire deliverable's process integrity. It covers:
> - **Per-step gates** — every step's journal has the 3 artifacts, clean big table, every PASS / N/A / FINDING with required evidence, every finding addressed via fix log, build clean, tests pass. Per-step audit scope explicitly includes EVERY file the step touched (including files modified from prior steps), so cross-step drift is caught at the per-step level — no separate tier-audit layer.
> - **Final-review gate** — deliverable-wide sweep walked the entire deliverable, final-review journal has the 3 artifacts, final big table clean.
> - **Cross-cutting doc gates** — per CLAUDE.md §3.5 Doc Update Map (PATTERNS / MESSAGING / TESTS / OPERATIONAL-GUARANTEES / RATE-LIMITING / SECURITY-RUNBOOKS / PARITY / AUDIT_CHECKLIST as relevant), parent README + Mermaid graph, tracking doc, no phase verbiage / Q-IDs / conversation-scoped IDs leaked into KEEP docs.
> - **Process-integrity gates** — no commits without permission, no destructive git, no silent deferrals, no mid-execution architectural deviations.
>
> **Final attestation**: before presenting for user REVIEW, write the attestation block from the checklist into the deliverable's root README (verbatim wording in rules.md). The attestation is YOUR signed claim that every box is honest YES — invalidating it is a process-integrity breach.
>
> **You MUST walk this checklist immediately before declaring any deliverable ready for user review. No exceptions, no "I'll skip it just this once," no "the steps were clean so the whole deliverable must be clean." Walk every box, write the attestation, then present.**


<sup>[↑ jump to top](#top)</sup>

---

## §1. Development Workflow

The agent reaches alignment with the user during PLAN, then **the main-thread orchestrator drives EXECUTE by spawning fresh sub-agents** for every round of planning, implementation, auditing, and fixing (per-step audit loop with 10-iteration ceiling, append-only journal capturing every round) and FINAL-REVIEW (same loop scoped to whole deliverable), then hands off to REVIEW. The user's value lives in PLAN (design decisions) and REVIEW (architectural feedback) — not in pushing the agent through audit rounds.

**Detailed protocol → [docs/dev/workflow.md](docs/dev/workflow.md).**
**Orchestrator + sub-agent architecture → [docs/dev/audit-framework.md](docs/dev/audit-framework.md).**
**Verbose audit rule catalog walked each round → [docs/dev/rules.md](docs/dev/rules.md).**
**Past deliverables' final reports + lessons → [docs/dev/deliverables/](docs/dev/deliverables/).**

### Phase summary

- **PLAN** — Discuss with user. Lock high-level goal + step breakdown (one csproj or equivalent per step) + cross-cutting decisions + risk analysis. Output: `docs/wip/<deliverable>/README.md` (gitignored workspace) populated with step list, decisions, prerequisites.
- **EXECUTE** — Per step, in prerequisite order, the **main-thread orchestrator** spawns fresh sub-agents for each phase (per the orchestrator-only main-thread block above):
  1. **Spawn Planner sub-agent** — given step description + applicable rules.md categories + relevant docs to read; appends Plan section to `docs/wip/<deliverable>/<NN>-<step>/journal.md` including pre-emptive gate checks (test coverage plan, convention check, PII check, layer check) to push catches BEFORE writing code, not after.
  2. **Spawn Implementer sub-agent** — given the journal Plan + applicable rules.md categories; writes code + tests; returns files-touched + build/inspectcode status.
  3. **Audit loop** — for EACH round, spawn fresh Auditor sub-agent(s) (READ-ONLY tools) that walk every category in `rules.md` and produce evidence per predicate via the **3-artifact journal model** (big table replaced each sweep + append-only findings log + append-only fix log; see the AUDIT EVIDENCE & PROOF DISCIPLINE block above + workflow.md §3 + rules.md §24). **DO NOT BE LAZY — walk EVERY subsection, no skipping, no assuming irrelevance without specific evidence; LEAVE NO STONE UNTURNED.** PASS rows require file:line citations; N/A rows require step-scope-specific reasons; FINDING rows require severity + file:line + description + fix. When findings exist, the orchestrator spawns a **fresh Fixer sub-agent** to apply fixes + append fix-log entries. **The journal must contain all three artifacts: the big table under `## Latest sweep results` (REPLACED each sweep with anti-laziness preamble above), the findings log under `## Sweep findings log (append-only)` (per-round subsections), and the fix log under `## Fix log (append-only)`. The big table is sweep-only — fixes go in the fix log, never in the table. Closure is proven by ABSENCE of the finding from the next sweep's big table.** Each next round = a NEW fresh Auditor sub-agent (no context reuse). Terminate on a complete-table CLEAN sweep (zero FINDING rows). 10-iteration ceiling per step; iteration 11 = escalate.
  4. Per-step distillation → root README's kinds-of-misses log + candidate predicate additions for `rules.md` (orchestrator may delegate distillation to a sub-agent if the journal is large).
- **FINAL-REVIEW** — Same orchestrator-driven audit loop, scope = whole deliverable. Fresh Final-reviewer sub-agent(s) per round. Catches integration / cross-step / consistency bugs.
- **SHIP** — Aggregate proposed rule additions FROM the per-step + final-review journals (they MUST still be readable at this point — they're the evidence behind every proposed rule). Present root README to user. Apply approved rules to `rules.md`. **Copy the root README as a snapshot** from `docs/wip/NNNN-<name>/README.md` to `docs/dev/deliverables/NNNN-<name>.md` (committed — single file; 4-digit index prefix so deliverables sort naturally in ship order). The per-step journals stay where they are in the gitignored `docs/wip/NNNN-<name>/` workspace — local-only artifacts that the workflow NEVER auto-deletes. User removes them manually whenever they want.
- **REVIEW** — User reviews shipped deliverable. Feedback is captured-and-confirmed first, NOT fixed-on-sight. Bugs that the audit should have caught become new predicates (self-improvement loop).

### Permission gates (must block — no inferred permission)

Per [workflow.md §Permission gates](docs/dev/workflow.md#permission-gates-must-block-no-inference-allowed):

- Commit creation — explicit user permission per occurrence
- Bulk file operations — declare scope before executing; user has chance to redirect
- Destructive git operations (force push, hard reset, branch delete) — explicit authorization
- Deferring planned work — ASK, not unilaterally skip
- Architectural decision changes mid-execution — ASK before deviating from locked PLAN

### Self-improvement (the key insight)

Every deliverable's distillation surfaces classes of miss. Approved misses become permanent predicates in `rules.md`. Future deliverables start with a stricter ruleset → audit loops converge in fewer rounds → deliverables ship faster → user spends less time pushing the agent through audit cycles.

The journal IS the evidence of process integrity. Honest journals are self-rewarding: every honest miss becomes a future gate-check.


<sup>[↑ jump to top](#top)</sup>

---

## §2. Commands

> ⚠️ **DO NOT START SERVICES MANUALLY** — Never run `dotnet run`, `pnpm dev`, `pnpm preview`, or any long-running server directly. Services are managed by Docker Compose.
> E2E tests that self-manage their infrastructure (Testcontainers, child processes with cleanup) ARE allowed — they start and stop their own services.

**Docker Compose (service lifecycle):**

```bash
make up                                                                    # Start all services (detached)
make down                                                                  # Stop all services
docker compose -f infra/compose/compose.yml --env-file .env.local --env-file .env.secrets up -d      # Direct invocation
```

**Build:**

```bash
dotnet build server/D2.slnx                                                # Full .NET solution
dotnet build server/services/{service}/api/{service}.API.csproj            # Single project
cd server/web && pnpm install && pnpm exec svelte-check                    # SvelteKit type check
```

**Rider/ReSharper Inspections (.NET):**

```bash
# Full solution (WARNING+ severity, text output, no build — run after dotnet build)
jb inspectcode server/D2.slnx --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log

# Single project (faster — use during focused work)
jb inspectcode server/D2.slnx --project="Edge.App" --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log
```

These catch warnings that `dotnet build` does NOT surface: `[MustDisposeResource]` misuse, captured variable/closure issues, object initialization suggestions, and other JetBrains-specific inspections. Must be zero warnings.

**Test:**

```bash
# .NET (xUnit)
dotnet test server/D2.slnx                                                 # Full solution
dotnet test server/D2.slnx --filter Category=Unit                          # Unit only
dotnet test server/services/edge/tests                                      # Specific service

# SvelteKit
cd server/web && pnpm exec vitest run                                       # Unit tests (browser mode)
cd server/web && pnpm exec playwright test                                  # Playwright (mocked by default)
```

**Lint/Style:**

```bash
cd server/web && pnpm exec eslint .                                         # ESLint
cd server/web && pnpm exec prettier --check .                               # Prettier check
```

**Versioning:**

```bash
dotnet tool restore                                                        # First-time setup
dotnet versionize --dry-run                                                # Preview bump (always do this first)
dotnet versionize                                                          # Bump version + update CHANGELOG + tag
git push --follow-tags
```

**Important:** When editing shared `.NET` libs in `server/shared/dotnet/`, run `dotnet build server/D2.slnx` to verify all consumers still compile. SvelteKit changes are isolated — `cd server/web && pnpm exec svelte-check`.


<sup>[↑ jump to top](#top)</sup>

---

## §3. Reference Documents

Read these docs BEFORE working in the relevant area. Each doc is the authority for its domain.

| Document | Summary | When to Read |
|---|---|---|
| [docs/dev/workflow.md](docs/dev/workflow.md) | The PLAN → EXECUTE → REVIEW loop protocol. Per-step audit loop with 10-iteration ceiling. Append-only journals. Self-improvement feedback loop. Permission gates. | Before starting ANY deliverable |
| [docs/dev/rules.md](docs/dev/rules.md) | The CENTRAL, VERBOSE, AUTHORITATIVE requirements catalog for any code change — security, race conditions, naming, object disposal, D2Result, OOTB shared libs, logging, PII, graceful degradation, UX, DX, observability, idempotency, configuration, conventions, more. Read end-to-end during PLAN; walk during every audit round. | Read end-to-end at PLAN; walked each audit round |
| [docs/dev/deliverables/](docs/dev/deliverables/) | Surviving root READMEs from shipped deliverables — final reports + lessons learned + origin trace for new rules.md predicates. | When researching a past deliverable's outcome |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Branch naming, conventional commits, PR process, license notice | PR preparation |
| [CHANGELOG.md](CHANGELOG.md) | Conventional-commits-driven (versionize). Don't hand-edit. | Reference only |
| [docs/PATTERNS.md](docs/PATTERNS.md) | TLC/2LC/3LC convention, handler, D2Result, middleware, repo, cache, RedactionSpec, i18n, configuration. The single biggest pattern reference. | Any handler / DI / repo / cache / middleware work |
| [docs/MESSAGING.md](docs/MESSAGING.md) | Proto-canonical JSON wire format, encryption, exchange naming, queue patterns, AMQP headers, DLQ inspection | Any RabbitMQ / async messaging work |
| [docs/TESTS.md](docs/TESTS.md) | 8-category adversarial Case Coverage Checklist, test categories, custom matchers | Adding or modifying tests |
| [docs/OPERATIONAL-GUARANTEES.md](docs/OPERATIONAL-GUARANTEES.md) | Idempotency, rate limiting, session consistency, RabbitMQ patterns, SAGA, multi-instance scaling | Any cross-service correctness work |
| [docs/AUDIT_CHECKLIST.md](docs/AUDIT_CHECKLIST.md) | Quality audit checklist — Security / Logic / Code Quality / Conventions / Cross-Service / Tests / Docs | Before merging substantial work |
| [docs/PARITY.md](docs/PARITY.md) | Cross-language parity tracking + "Why exclusive?" framework | Adding cross-language components |
| [docs/SECURITY-RUNBOOKS.md](docs/SECURITY-RUNBOOKS.md) | KeyCustodian compromise runbooks. | Security incident response |
| [docs/RATE-LIMITING.md](docs/RATE-LIMITING.md) | v2 rate-limiting design — `RateLimitTier` enum, 18-bucket model, cookie-shortcut, FP-too-common detection (option d hybrid), pub/sub session-invalidation backplane, runtime kill-switch hierarchy, per-class fail behavior. Phase 3 (Edge) implementation reference. | Any rate-limit / request-enrichment middleware work |
| **Active project tracking doc** (see header) | Current phase, status, open questions, deferred work, resolved decisions. | Before starting any task |
| `/old/v1/D2-WORX/` | Frozen v1 snapshot. Historical reference for any v1 patterns / decisions / docs that don't have v2 equivalents yet. **Read-only — never modify.** | When researching how v1 did something or hunting for tribal knowledge not yet extracted |

Per-service / per-library `README.md` files appear in `server/services/{service}/` and `server/shared/dotnet/{lib}/` as those are built/lib.


<sup>[↑ jump to top](#top)</sup>

---

## §3.5. Doc Update Map

**KEEP docs describe current reality, not the journey from v1.** Don't add v1-retrospective framing to PATTERNS / TESTS / MESSAGING / etc. — the v1→v2 journey lives in `docs/v2/` (V2.md, PHASE_*.md), and those tracking docs get archived once their phase ships.

When you change something, update the right doc. The map below is the routing table:

| If you change... | Update... |
|---|---|
| A handler / TLC pattern / DI registration / `D2Result` factory usage / RedactionSpec / mapper / repo pattern | [docs/PATTERNS.md](docs/PATTERNS.md) |
| AMQP headers, exchange/routing-key naming, encryption frame, queue topology, DLQ behavior | [docs/MESSAGING.md](docs/MESSAGING.md) |
| Test category, custom matcher, adversarial-coverage rule, fixture pattern | [docs/TESTS.md](docs/TESTS.md) |
| Idempotency / SAGA / migration locking / multi-instance correctness | [docs/OPERATIONAL-GUARANTEES.md](docs/OPERATIONAL-GUARANTEES.md) |
| Rate-limit middleware design / bucket math / kill-switch / FP-too-common detection / cookie shortcut | [docs/RATE-LIMITING.md](docs/RATE-LIMITING.md) |
| Audit checklist item (security / logic / code-quality / conventions / cross-service / tests / docs gate) | [docs/AUDIT_CHECKLIST.md](docs/AUDIT_CHECKLIST.md) |
| Anything cross-language (.NET ↔ SvelteKit ↔ future) | [docs/PARITY.md](docs/PARITY.md) |
| KeyCustodian, key rotation, secret handling, compromise runbook | [docs/SECURITY-RUNBOOKS.md](docs/SECURITY-RUNBOOKS.md) |
| Add/modify a public API on a lib or service | the relevant `README.md` (`server/services/{svc}/README.md` or `server/shared/dotnet/{lib}/README.md`) |
| Phase progression / wipe state / open Phase 0 questions / new tracked issue | [docs/v2/PHASE_0.md](docs/v2/PHASE_0.md) |
| Architectural decision that overrides prior v2 plan | [docs/v2/V2.md](docs/v2/V2.md) (and note in PHASE_0.md) |

If your change spans multiple categories, update each. If no entry fits, the change probably needs a new doc — ASK before creating one.


<sup>[↑ jump to top](#top)</sup>

---

## §4. Patterns & Architecture

**Rule: Follow existing patterns. Do not invent new ones when established patterns apply. If no pattern fits, ASK before inventing. Behavioral Guidelines (§7) apply to ALL work in this section — especially: ask when uncertain, research first, follow existing conventions.**

**Patterns are documented in detail in [docs/PATTERNS.md](docs/PATTERNS.md). This section summarizes the operational rules every D² engineer needs daily.**

### TLC/2LC/3LC Folder Convention

Three-tier folder hierarchy for all backend code. TLC = architectural concern, 2LC = implementation type, 3LC = operation type. **3LC verbiage varies by layer:**

| TLC | 3LC Verbiage | Meaning |
|---|---|---|
| **CQRS** | `C/` Commands, `Q/` Queries, `U/` Utilities, `X/` Complex | Business operation intent |
| **Messaging** | `Pub/` Publishers, `Sub/` Subscribers | Message direction |
| **Repository** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete | CRUD operation |
| **Caching** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete | CRUD operation |

Interfaces live in `Interfaces/{TLC}/Handlers/{3LC}/`. Implementations live in `Implementations/{TLC}/Handlers/{3LC}/` (app layer) or `{TLC}/Handlers/{3LC}/` (infra layer).

### CQRS Handler Categories

| Type | Distributed Cache | DB Write | External API | Message Publish | Key Test |
|---|---|---|---|---|---|
| **Query** | No | No | No | No | "If the process dies after, would state persist?" → **No** |
| **Command** | Yes | Yes | Yes | Yes | Primary intent = mutation of persistent/shared state |
| **Complex** | Yes | Yes | Yes | Yes | Primary intent = retrieval, but may mutate as side effect |

Local/in-memory caching is always OK (instance-scoped, ephemeral — doesn't affect other instances).

### Verb Semantics

- **Find** = "Resolve this for me" — may fetch from external source, may cache/persist. Example: `FindWhoIs`
- **Get** = "Give me this by ID" — direct lookup, read-only. Example: `GetWhoIsByIds`

### Handler Pattern

`.NET`: `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. Per-handler PII redaction via the `[RedactData]` attribute on data types (KEEP) + `DefaultOptions` overrides for proto-generated DTOs that can't carry the attribute.

### D2Result Pattern

Result objects replace exceptions for control flow. **Always use semantic factories** — never raw `Fail()` with manual status codes when a factory exists. Available: `Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Canceled`, `SomeFound`. Raw `Fail` only when no factory matches (e.g., re-mapping arbitrary upstream status codes).

Partial success: `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found).

### Interface organization

One handler interface per file under `Interfaces/{TLC}/Handlers/{3LC}/`. Consumers `using` the namespaces directly — no `partial` interface aggregation, no grouping aliases. The folder structure IS the discoverability mechanism.

### DI Registration

`.NET`: `services.AddTransient<IXxx, Xxx>()` via `Microsoft.Extensions.DependencyInjection`. Each layer exports `AddXxx(services)` extension method.

### Other Established Patterns

- **Options pattern**: `IOptions<T>` with defaults. Config section
- **Caching**: inject one of three marker interfaces from `D2.Shared.Caching.Abstractions` — `ILocalCache` (per-process; basic + atomic), `IDistributedCache` (cluster-wide; basic + atomic + broadcast + set), or `ITieredCache` (composed L1+L2; reads check L1 → fall through to L2 → populate L1; writes go L2-first; atomic ops route through L2). Cluster-wide L1 coherency uses the `ICacheInvalidationBackplane` (Redis pub/sub) — the `*AndBroadcast*` write variants publish on every send. Every op returns `D2Result<T>`; null/empty inputs return `ValidationFailed` (impls never throw). Key convention: `EntityName:{id}`. Full reference: [PATTERNS.md](docs/PATTERNS.md) cache section.
- **Content-addressable entities**: `Location` and `WhoIs` use SHA-256 hash IDs (64-char hex). Factory method computes hash. Enables dedup.
- **Mappers**: C# 14 extension members: `extension(Entity e) { public DTO ToDTO() { ... } }`. Live in `{Service}.App/Mappers/`.
- **Batch operations**: `input.HashIds.Chunk(_BATCH_SIZE)` via Options pattern (default 500).
- **Health checks must use the same code path as production** — DB health checks go through EF Core, not raw `pool.query()`. A check that bypasses the ORM won't detect ORM-layer issues.

### Key Architecture Decisions

- **Auth**: self-rolled .NET auth as a module within Edge. RFC 8693 token exchange + RFC 6749 §4.4 client_credentials for service identity. JWKS at the OIDC-canonical `/.well-known/jwks.json`.
- **JWT**: RS256 only. 15min expiry. Custom claims namespaced with `d2_` prefix (snake_case — avoids spec-collision with `:` punctuation used in scope strings).
- **KeyCustodian**: module within Auth — owns lifecycle of ALL long-lived secrets (JWKS, message payload encryption keys, cookie signing, service-identity client_secrets). State machine + JWKS-style overlap rotation.
- **SvelteKit BFF**: pure SSR. Browser → Edge directly for auth state mutations. Server-side route guards (`requireAuth`, `requireOrg`, etc.) live in the `@d2/headers` package (re-usable across any Node frontend); the SvelteKit BFF imports them and wires them through `hooks.server.ts` + per-route loaders. Browser-side `authClient` lives at `server/web/src/lib/client/auth/` (SvelteKit-internal module — not a separate package).
- **Sync**: gRPC between services (HTTP/2). **Async**: RabbitMQ for side effects (emails, events). Sensitive RMQ payloads encrypted via `D2.Shared.Encryption`.
- **Notifications**: ALL deliveries through D2.Courier → contact resolution. No direct emails/texts.
- **Sessions**: 3-tier (cookie cache 5min → Redis → PostgreSQL `auth_db` dual-write).
- **Database topology**: one PG server, many DBs (auth_db, files_db, courier_db, notifications_db, audit_db, seaweedfs_filer_db, plus per-service contacts DBs). Multi-replica migration safety via PG advisory locks.
- **Object storage**: SeaweedFS for user files. MinIO retained as backend for LGTM block storage.
- **Production deployment**: eventually Docker Swarm + Portainer; pre-launch is Compose on a VPS.


<sup>[↑ jump to top](#top)</sup>

---

## §5. Critical Reminders (top-of-mind for every change)

**The complete, verbose, authoritative rule catalog lives in [docs/dev/rules.md](docs/dev/rules.md) — security, race conditions, naming, object disposal, D2Result, OOTB shared libs, logging, PII redaction, graceful degradation, UX, DX, observability, idempotency, configuration, conventions, and more (~200 evidence-required predicates across 23 categories). READ IT END-TO-END DURING THE PLAN PHASE OF EVERY DELIVERABLE.**

**This section is the short list — the most critical rules to keep top-of-mind even before you've re-read rules.md.** They duplicate the most consequential predicates from rules.md so they're impossible to miss.

### Production-readiness mindset

- **D²-WORX is being built as an enterprise-level, production-ready, robust SaaS framework.** Every change must survive bad input, infrastructure failure, concurrency, hostile users, and future engineers. "Works on my machine" is not the bar. Don't optimize for short-term speed at the expense of robustness.

### Permission gates (NEVER bypass)

- **NEVER commit without explicit user permission** for THIS commit (not "go ahead" from earlier). [rules.md §13.1](docs/dev/rules.md#13-permission--action-discipline)
- **NEVER bulk-edit / sed across N files / mass-rename without first declaring scope** (file count, glob, what changes) and giving the user the chance to redirect. [rules.md §13.2]
- **NEVER use destructive git ops** (force push, hard reset, branch delete, `git stash` in sub-agents) without explicit authorization. [rules.md §13.3]
- **NEVER defer / skip planned work** without asking the user first. [rules.md §13.4]
- **NEVER start services manually** (`dotnet run`, `pnpm dev`, etc.) — Docker Compose manages services. [rules.md §8.1]
- **NEVER `Grep` `secrets/` or `.env.secrets` by name.** [rules.md §3.11]

### Ask when uncertain (the #1 rule)

- **ALWAYS ask when uncertain** — non-negotiable. Do not guess. Do not assume. If requirements / approach / tradeoffs are unclear, ASK. Every time. The failure mode isn't "didn't ask when I knew I was uncertain" — it's "didn't notice I should be uncertain." Stay alert.

### Test discipline (drives multi-pass audits when skipped)

- **Test every public path on first pass** — every `public` method (including DI extensions, gRPC plumbing, factory wrappers, "thin glue") gets ≥1 test BEFORE the feature is done. [rules.md §1.1]
- **Every bug fix lands with a regression test in the same change** — fails-without-fix, passes-with-fix. Behavior-descriptive name. **No fix without a test, no exceptions.** [rules.md §2]
- **Tests are adversarial** — happy path + garbage / null / empty / whitespace / oversized / malformed / wrong-type / cross-field deps / error propagation / idempotency / concurrency. [rules.md §1.2]

### Use OOTB shared libs (don't hand-roll)

- **Falsey() / Truthy() instead of `string.IsNullOrEmpty` / `coll == null || coll.Count == 0` / `guid == Guid.Empty`.** [rules.md §5.1]
- **D2.Shared.Utilities extensions instead of hand-rolled `TryParse` + null check** (`str.TryParseTruthyNull(out Guid? r)` / `str.TryParseTruthyNull<TEnum>(out var r)`). [rules.md §5.2]
- **D2Result semantic factories** (`Ok` / `NotFound` / `ValidationFailed` / `Conflict` / `ServiceUnavailable` / etc.) — never raw `Fail()` with manual statusCode when a factory exists. [rules.md §5.3]
- **Catalog of shared libs** to reach for first: [rules.md §16](docs/dev/rules.md#16-ootb-shared-lib-tooling--use-whats-there).

### PII / logging safety (the highest-risk class)

- **`[LoggerMessage]` MUST NOT accept `Exception`** — `ex.Message` leaks broker URI passwords, user input. Use `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` separately. [rules.md §3.1]
- **`[RedactData]` on PII types** — emails, phones, IPs, addresses, names, message content, filenames, presigned URLs, AMQP URIs. [rules.md §3.3]
- **Sensitive context in encrypted RMQ payload, NOT plaintext headers.** [rules.md §3.4]
- **Constant-time comparisons for API keys / tokens / secrets** (`CryptographicOperations.FixedTimeEquals`). [rules.md §3.9]

### Code quality (zero tolerance)

- **`dotnet build server/D2.slnx` zero StyleCop / CS / null-ref warnings.** Never suppress. [rules.md §5.21]
- **`jb inspectcode server/D2.slnx --severity=WARNING` zero JetBrains warnings.** [rules.md §5.22]
- **Fix ALL warnings/errors anywhere in the project** — never dismiss as "pre-existing." [rules.md §5.23]
- **i18n everywhere** — no hardcoded user-visible strings (UI, handler messages, input errors, notifications). All locale files in sync. [rules.md §12]

### Documentation parity

- **Doc edits in the SAME change as code edits** (not a separate commit). Telemetry tag enumerations, counter lists, config tables drift the moment you defer. [rules.md §11.1]
- **File headers on every source file you create or modify.** [rules.md §7.7]

### Convention slippage (memory of these = first-pass clean)

- **`string.Empty` not `""`** in C#. [rules.md §5.5]
- **`namespace` BEFORE `using` directives** in C#. [rules.md §5.10]
- **No `this.` qualifier** in C#. [rules.md §5.9]
- **Single-line `if` no braces; multi-line `if` WITH braces.** [rules.md §5.8]
- **C# 14 extension members syntax (`extension(T t) { ... }`)** — not old `this T` parameter style. [rules.md §5.6]
- **Sealed by default** on concrete classes / records / exceptions / attributes. [rules.md §5.7]
- **Field prefixes**: `_` (mutable), `r_` (readonly), `s_` (static), `sr_` (static readonly), `_UPPER` (private const), `UPPER` (public const). Primary-constructor params on handlers carry NO `r_` prefix. [rules.md §7.1]
- **American English only** — `behavior`/`color`/`analyze`/`honor`/`canceled`/`favorite`. [rules.md §5.12 — moved to §7 wording standards in rules.md]
- **Lines ≤ 100 chars** in C# / TS source.
- **No phase / sweep / audit verbiage** in source or KEEP docs. [rules.md §14]
- **Tests live next to the feature they cover** — no `Phase*Tests.cs` / `*Audit*Tests.cs` / `*Sweep*Tests.cs`. Behavior-descriptive method names. [rules.md §1.8, §1.9]

### Architectural layer hygiene

- **JWT validations at TRANSPORT layer (auth middleware), NOT per-handler `HandlerOptions`.** `RequiredScopes` IS per-handler; `ValidateAudience` is NOT. [rules.md §9.2]
- **Handlers validate input via `Domain.Create(input) → D2Result<Domain>` at the TOP of `ExecuteAsync`** — never let Redis / DB be the first to reject invalid data. [rules.md §9.4]
- **NEVER hand-write DB migrations** — use `dotnet ef migrations add <Name>`. [rules.md §9.10]
- **Never return `Ok()` after a branching operation unconditionally** — if a nested handler / provider can fail, check its result. [rules.md §9.20]

### Caching

- **Inject one of `ILocalCache` / `IDistributedCache` / `ITieredCache`** from `D2.Shared.Caching.Abstractions`. Use `*AndBroadcast*` write variants when other instances cache the same key. Every op returns `D2Result<T>`. [rules.md §16.3]

> **All language-specific predicates (C# Falsey/Truthy, sealed-by-default, extension members, regex bucket discipline, options records pattern, brace rules, namespace ordering, field prefixes, MustDisposeResource, ValueTask discipline, Random.Shared, etc.) live in [docs/dev/rules.md §5 (C#)](docs/dev/rules.md#5-c-code-conventions) and [§6 (TypeScript/SvelteKit)](docs/dev/rules.md#6-typescript--sveltekit-code-conventions).**
> **Architecture predicates (transport-vs-handler layer, IDOR, ValidateAudience placement, smart-constructor input validation, EF migration safety, etc.) live in [docs/dev/rules.md §9](docs/dev/rules.md#9-architectural-layer-hygiene) and [§10 Security](docs/dev/rules.md#10-security-endpoints--auth--secrets--input).**
> **Concurrency / race-condition predicates live in [docs/dev/rules.md §4](docs/dev/rules.md#4-concurrency--race-conditions).**
> **Object-disposal & resource-lifetime predicates live in [docs/dev/rules.md §15](docs/dev/rules.md#15-object-disposal--resource-lifetime).**
> **OOTB shared-lib catalog + when-to-reach-for-which lives in [docs/dev/rules.md §16](docs/dev/rules.md#16-ootb-shared-lib-tooling--use-whats-there).**
> **D2Result usage / extensions lives in [docs/dev/rules.md §17](docs/dev/rules.md#17-d2result-usage--extensions).**
> **Graceful-degradation / failure-mode predicates live in [docs/dev/rules.md §18](docs/dev/rules.md#18-graceful-degradation--failure-modes).**
> **UX predicates (loading states, empty states, error display, accessibility) live in [docs/dev/rules.md §19](docs/dev/rules.md#19-user-experience-ux).**
> **DX predicates (sensible defaults, no footguns, ergonomic call sites) live in [docs/dev/rules.md §20](docs/dev/rules.md#20-developer-experience-dx).**
> **Observability completeness predicates live in [docs/dev/rules.md §21](docs/dev/rules.md#21-observability-completeness).**
> **Idempotency / exactly-once predicates live in [docs/dev/rules.md §22](docs/dev/rules.md#22-idempotency--exactly-once-semantics).**
> **Configuration hygiene predicates live in [docs/dev/rules.md §23](docs/dev/rules.md#23-configuration-hygiene).**


<sup>[↑ jump to top](#top)</sup>

---

## §6. Code Conventions

### C# Naming

| Element | Convention | Example |
|---|---|---|
| Classes/Records/Interfaces | `PascalCase` | `GetReferenceData` |
| Methods/Properties | `PascalCase` | `HandleAsync` |
| Private instance fields | `_camelCase` | `_memoryCache` |
| Private readonly instance fields | `r_camelCase` | `r_getFromMem` |
| Private static fields | `s_camelCase` | `s_instance` |
| Private static readonly fields | `sr_camelCase` | `sr_activitySource` |
| Static readonly (non-private) | `SR_PascalCase` | `SR_ActivitySource` |
| Private constants | `_UPPER_CASE` | `_BATCH_SIZE` |
| Public/Internal constants | `UPPER_CASE` | `MAX_ATTEMPTS` |
| Local constants (tests) | `snake_case` | `expected_count` |
| Local variables | `camelCase` | `result` |

**Primary-constructor handlers**: Constructor parameters do NOT take the `r_` prefix — they're parameters, not fields, even though they're accessed like fields inside the class body. The carve-out applies ONLY to handler primary-constructor parameters; regular fields keep their prefixes.

> **The above C# Naming table is duplicated here from [docs/dev/rules.md §7.1](docs/dev/rules.md#7-naming-file-headers-folder-casing) for at-a-glance reference.**
>
> **All other convention details live in [docs/dev/rules.md §7](docs/dev/rules.md#7-naming-file-headers-folder-casing)**:
> - **TypeScript naming** (camelCase / PascalCase / kebab-case)
> - **Folder casing** (outside-project = lowercase, inside-project = PascalCase)
> - **File headers** (per-language full code blocks for `.cs`, `.ts`, `.css`, shebang-bearing files, markdown, XML/csproj/slnx, SQL, etc.)
> - **Translation key conventions** (`auth_*`, `webclient_*`, `common_*`)
> - **Scope vs Permission terminology** (use "scope" — JWT-canonical, OAuth-standard)
> - **Observability fields** (`traceId`, `correlationId`, `userId`, `orgId`, `service` — universal)
> - **Git conventions** (branch prefixes, conventional commits, no `Co-Authored-By`)


<sup>[↑ jump to top](#top)</sup>

---

## §7. Behavioral Guidelines (dispositional — how to approach work)

> **⚠️ These guidelines are MANDATORY — equally binding as §4 (Patterns) and the predicates in [docs/dev/rules.md](docs/dev/rules.md). They shape HOW you work; the rules.md predicates govern WHAT the work looks like.**

1. **ALWAYS ask when uncertain** — Non-negotiable. Do not guess. Do not assume. If requirements, approach, or tradeoffs are unclear — **ask**. Every time. The failure mode isn't "didn't ask when I knew I was uncertain" — it's "didn't notice I should be uncertain." Stay alert.
2. **Read freely** — Explore any files needed for context. Reading is cheap.
3. **Ask before changing** — Do not modify files without explicit user approval (per the workflow's PLAN gate).
4. **Research first** — Check related files (tests, interfaces, existing implementations) before proposing changes. Find similar existing implementations before inventing.
5. **Follow existing conventions** — the patterns docs are the source of truth for current code patterns. Don't invent new ones when established patterns apply. If no pattern fits, ASK before inventing.
6. **Check the project tracking doc** referenced in the header at the top of this file before starting work — for current phase, status, and resolved decisions.
7. **Provide options** — When multiple approaches exist, present them for user decision rather than picking one silently.
8. **Maximize parallelization** — Spawn as many sub-agents as makes sense to complete tasks as fast as possible. Independent work (file reads, doc updates, code fixes, test runs, audits) should run in parallel, not sequentially. Use background agents for non-blocking work. The user values speed — don't serialize work that can be parallelized.
9. **Effort asymmetry — fix small issues, don't defer them** — Your cost to read N files and apply M small fixes is minutes; the user's cost to prompt you to do it is dominated by typing speed. When you spot minor issues during your work (broken doc links, stale references, formatting nits, small test gaps, missed cleanup, unlinked cross-refs, drifted comments), the DEFAULT is to **fix them in the same turn**, not to report them as "consider doing this later." The asymmetry is the entire reason the user is delegating to you. Only report-without-fixing when (a) the user explicitly asked you to audit / report only, (b) the fix is non-trivial or destructive, (c) the fix would balloon scope beyond the current task, or (d) the fix changes behavior the user must approve. When unsure, fix it AND mention what you fixed in your end-of-turn summary so the user has the option to revert.

> **Predicates** (zero-tolerance for warnings, write tests, regression-pin every fix, never commit without permission, never defer without permission, etc.) live in [docs/dev/rules.md §13 Permission / Action Discipline](docs/dev/rules.md#13-permission--action-discipline) and elsewhere in rules.md. They're walked each audit round.

### Code Intelligence Tools

**TypeScript**: Use `mcp__cclsp__*` tools (`get_hover`, `find_definition`, `find_references`, `find_workspace_symbols`, `get_diagnostics`). The built-in `LSP` tool's `workspaceSymbol` works but `hover`/`documentSymbol` return empty results.

**C#**: `csharp-ls` via built-in `LSP` tool — `workspaceSymbol` works, diagnostics flow automatically, but `hover`/`documentSymbol` time out (30s limit on large solution). Fall back to Grep/Glob/Read.

Before renaming or changing a function signature, use `find_references` to find all call sites first. Use Grep/Glob for text/pattern searches (comments, strings, config values) where LSP doesn't help.

After writing or editing TS code, check `mcp__cclsp__get_diagnostics` before moving on. Fix type errors and missing imports immediately.

After writing or editing .NET code, run `dotnet build server/D2.slnx` (zero warnings) AND `jb inspectcode server/D2.slnx --severity=WARNING` (zero warnings). The two tools catch different issues — Roslyn analyzers vs JetBrains inspections. Both must be clean.

### Windows LSP Workaround

Edit `~/.claude/plugins/marketplaces/claude-plugins-official/.claude-plugin/marketplace.json`: Change `"command"` to `"cmd"` with `"args": ["/c", "<binary>", ...originalArgs]` for `typescript-language-server`, `csharp-ls`, `gopls`. **Must reapply after `claude plugin marketplace update`** — the update overwrites the file.

### Project Structure

Key roots in the tree:

- `contracts/` — proto source of truth + i18n message files + fixtures
- `server/` — all trusted code (.NET services + SvelteKit BFF + .NET shared libs)
- `infra/` — deployment + observability (compose, docker, observability)
- `tools/` — dev tooling (scripts, generators)
- `docs/` — project documentation (PATTERNS, TESTS, MESSAGING, OPERATIONAL-GUARANTEES, etc.)
- `secrets/` — gitignored + Claude-deny-ruled key material (root key, encryption keys, dev TLS certs). Populated by `tools/scripts/gen-dev-keys.sh`.
- `.claude/` — project-level Claude Code settings (`settings.json` with deny rules)


<sup>[↑ jump to top](#top)</sup>

---

## §8. Local Secrets & Claude Deny Rules

Environment configuration is split:

| File | Contents | Committed? | Claude can read? | Claude can edit? |
|---|---|---|---|---|
| `.env.local` | Non-secret config — service URLs, ports, log levels, feature flags, CORS origins | No (gitignored) | **Yes** | **Yes** |
| `.env.local.example` | Template with safe defaults | **Yes** | Yes | Yes |
| `.env.secrets` | Real third-party creds — Twilio, Resend, IPinfo, OAuth client secrets, prod-like DB passwords | No (gitignored) | **No (deny-ruled)** | **No (deny-ruled)** |
| `.env.secrets.example` | Template with placeholder values like `TWILIO_AUTH_TOKEN=replace_with_real_value` | **Yes** | Yes | Yes |
| `secrets/` | Key material — root key, dev encryption keys, dev TLS certs | No (gitignored, populated by `tools/scripts/gen-dev-keys.sh`) | **No (deny-ruled)** | **No (deny-ruled)** |

Compose loads both env files (`.env.local` first, `.env.secrets` second so secrets override placeholders if any collision):

```yaml
services:
  edge:
    env_file:
      - .env.local
      - .env.secrets
```

**Workflow when adding a new secret**:
1. Edit `.env.secrets.example` adding `NEW_THING_API_KEY=replace_with_real_value`
2. Update `infra/compose/compose.yml` to load it into the right service
3. Tell the operator: "Added `NEW_THING_API_KEY` — copy into `.env.secrets`, set the real value, restart the service"
4. Operator manually syncs (Claude cannot edit `.env.secrets` — deny rule)

Same pattern for encryption keys: update `tools/scripts/gen-dev-keys.sh` to generate keys for new domains; operator runs the script; output lands in `secrets/`.

The deny rules live in `.claude/settings.json` (committed). The exact-match `**/.env.secrets` deliberately does NOT match `.env.secrets.example` — the template file remains fully editable.

**Behavioral rule**: never `Grep` the `secrets/` directory or `.env.secrets` file by name. If a secret accidentally enters context (runtime output, grep match), STOP and tell the operator immediately so they can rotate the exposed value.
