<!--
Copyright (c) DCSV. All rights reserved.
-->

# Audit Framework — orchestrator + sub-agents + JSON evidence + recipe-runner

> **Status — split:**
> - **Orchestrator-only main thread + adversarial sub-agent role separation: CANONICAL workflow, in effect now.** §2 (architecture) + §3 (sub-agent roles) describe what we DO, not what we plan to do. Trialed across deliverable 0002-auth-inbound (8 steps + final-review + 3 polish rounds). Empirical results captured in [Appendix C: Trial outcomes](#appendix-c-trial-outcomes-from-deliverable-0002-auth-inbound). Cross-referenced from [CLAUDE.md (orchestrator-only block)](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread) and [workflow.md (Orchestrator-only main thread)](workflow.md#orchestrator-only-main-thread-canonical).
> - **JSON-first audit artifacts + recipe-runners + validator scripts + `rules.md` recipe/judgment/process bucketing: ASPIRATIONAL.** §4-§9 describe tooling not yet built. The current canonical workflow uses the markdown 3-artifact journal model (big table + findings log + fix log) per [workflow.md §3](workflow.md#3-audit-loop-the-core-forcing-function-fresh-auditor--fixer-sub-agents-per-round) and [rules.md §24](rules.md#24-audit-evidence-discipline-meta--how-to-audit). The tooling buildout is a future phase.
>
> **Author note**: written by the agent in collaboration with the user. The intent is for THE AGENT to find this framework reliable to follow — it is designed for agent ergonomics first, human readability second (with renderers bridging the gap).

---

## Table of contents

**Canonical (in effect now):**
1. [Motivation — why the prior self-policed model breaks](#1-motivation--why-the-current-model-breaks)
2. [Architecture — orchestrator-only main thread + sub-agents](#2-architecture--orchestrator-only-main-thread--sub-agents)
3. [Sub-agent roles](#3-sub-agent-roles)
3a. [Auditor cluster partition (canonical K=5)](#3a-auditor-cluster-partition-canonical-k5)
3b. [Aggregator role (post-cluster consolidation)](#3b-aggregator-role-post-cluster-consolidation)
3c. [Per-round dispatch protocol](#3c-per-round-dispatch-protocol)

**Aspirational (tooling buildout, not yet implemented):**

4. [JSON-first audit artifacts](#4-json-first-audit-artifacts)
5. [Anti-cheat: validator + recipe-runners](#5-anti-cheat-validator--recipe-runners)
6. [`rules.md` transformation: recipe / judgment / process buckets](#6-rulesmd-transformation-recipe--judgment--process-buckets)
7. [Per-step flow (state machine — full tooled version)](#7-per-step-flow-state-machine)
8. [Tooling layout (`tools/audit/`)](#8-tooling-layout-toolsaudit)
9. [Tooling buildout phases](#9-implementation-phases)
10. [Open decisions](#10-open-decisions)
11. [Research references](#11-research-references)

**Appendices:**

- [Appendix A: How this addresses each empirical failure mode](#appendix-a-how-this-addresses-each-empirical-failure-mode)
- [Appendix B: Mapping to Anthropic's five workflow patterns](#appendix-b-mapping-to-anthropics-five-workflow-patterns)
- [Appendix C: Trial outcomes from deliverable 0002-auth-inbound](#appendix-c-trial-outcomes-from-deliverable-0002-auth-inbound)

---

## 1. Motivation — why the prior self-policed model breaks

Before this framework, the workflow relied on the main-thread agent self-policing adherence to ~200 predicates in `rules.md`. Empirically (multiple audit cycles in deliverable 0002-auth-inbound) this failed in predictable ways:

- **Prose-as-evidence drift**: agent writes "PASS — verified" without actually re-reading the file. File:line citation requirement helps but doesn't eliminate.
- **Convergence illusion**: agent declares CLEAN one round too early because the alternative (more rounds) feels like failure. Motivated stopping.
- **Stale-memory shortcuts**: agent trusts conversation summaries over fresh disk reads.
- **Scope-narrowing under cost**: 200+ predicates feels expensive → agent rationalizes "obviously N/A" without specific evidence.
- **Self-review leniency bias**: validated by [Anthropic-adjacent research](https://dev.to/rih0z/why-ai-agent-outputs-need-adversarial-review-and-how-to-add-it-in-one-api-call-1l92) — when an LLM reviews its own output it overwhelmingly approves.

Adding more verbal rules (CAPS, "DO NOT BE LAZY", longer prose) does not fix these — they're ignored when convenient. The fix is structural: separate roles, mechanical evidence, and tool-mediated verification.

Real bugs that slipped past per-step audits in the prior self-policed model and were only caught because someone (user OR a fresh sub-agent) re-walked from scratch:
- `AuthOptions` `required init` incompatible with `Action<T>` configure pattern (Step 02 — masked by missing composition test)
- `JwksFetchDurationMs` histogram declared but never recorded (Step 03 — silent dead metric)
- HTTP 100s default timeout unset on OIDC discovery client (Step 03 — would have hung threads under Edge outage)
- Empty `BackplaneChannelKey` would silently drop every rotation event (Step 03)

These are the kind of failures that compound silently. The framework has to catch them BEFORE production, not "when an audit happens to be diligent." The orchestrator + adversarial sub-agent separation in §2-§3 is the structural fix and is in effect now; the JSON-first / recipe-runner / validator-script tooling in §4-§9 will mechanize what is currently a discipline-based workflow.

---

## 2. Architecture — orchestrator-only main thread + sub-agents

**Status: CANONICAL — in effect now.** This is the workflow.

Anthropic's [multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) uses orchestrator-Opus + worker-Sonnet and outperforms single-agent Opus by 90.2% on internal evals. The pattern: lead agent decomposes + delegates; workers execute in isolated context; results synthesize back. We apply that pattern to the audit loop.

**Main thread = pure orchestrator.** Restricted tool access:
- ✅ `Agent` (spawn sub-agents)
- ✅ `Bash` — only for git plumbing (`git status`, `git log`, `git commit -F <file>` when authorized, `git push` when authorized) and (in the aspirational tooled flow) validator script invocations
- ✅ `Read` — only for the deliverable root README and the orchestrator's own decision log; sub-agents handle source / test / journal content reads
- ✅ `TaskCreate` / `TaskUpdate` / `TaskList` / `TaskGet` / `TaskStop`
- ❌ `Edit` / `Write` to source / tests / per-lib READMEs / framework docs (sub-agents do this)
- ❌ `Edit` / `Write` to journal big-table or findings log (Auditor / Aggregator sub-agents do this)
- ✅ `Edit` / `Write` to the deliverable root README's tracking sections + its own decision log (`docs/wip/<deliverable>/orchestrator-log.md` if used)

The main thread's job is decision-making, not implementation, not auditing. It reads sub-agent summaries and routes the next step. **It cannot mark anything CLEAN or PASS itself** — it only consumes those verdicts from sub-agents (and, in the aspirational tooled flow, from validator script output).

This mirrors Claude Code's [sub-agent design](https://code.claude.com/docs/en/sub-agents): each sub-agent gets a fresh isolated context, encapsulates work, and returns only relevant output to the orchestrator. Context rot in the main thread is near-impossible because the main thread holds almost no domain state.

**Every round = a NEW fresh sub-agent.** A second audit round is a brand-new Auditor, not the same Auditor "running again." A fix follow-up is a brand-new Fixer. The fresh-context property is the entire point — reusing context defeats the adversarial separation.

Cross-references: [CLAUDE.md orchestrator-only block](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread), [workflow.md Orchestrator-only main thread (canonical)](workflow.md#orchestrator-only-main-thread-canonical).

---

## 3. Sub-agent roles

**Status: CANONICAL — in effect now.**

Six distinct roles (Final-reviewer added at deliverable end). Each is spawned with a fresh context and a tightly-scoped prompt. **No reuse across roles or across rounds.**

| Role | Spawned when | Tool access | Returns |
|---|---|---|---|
| **Planner** | Start of each step | Read, Grep, Glob, Edit (journal Plan section only) | Step Plan section appended to journal + summary |
| **Implementer** | After Planner | All | Files touched + tests added + build / inspectcode status |
| **Auditor** (parallel ×K=5, default) | After Implementer | Read, Grep, Glob, Bash (read-only) | Partial big-table chunk for assigned cluster (see [§3a](#3a-auditor-cluster-partition-canonical-k5)) written to a designated partial file |
| **Aggregator** (one per audit round) | After all 5 Auditors return | Read, Edit (journal + audit artifacts only) | Canonical merged big table embedded in journal + consolidated findings log entry + cross-cluster verification (see [§3b](#3b-aggregator-role-post-cluster-consolidation)) |
| **Fixer** | When findings exist | All | Files changed + appended fix-log entries |
| **Final-reviewer** (parallel ×K=5, deliverable-end only) | Before SHIP | Same as Auditor | Cluster-scoped partial big tables; Aggregator merges as above |

**Key design decisions:**

- **Planner is its own role.** Spawned at the start of each step with the step description + applicable rules.md categories + relevant docs to read. It writes the step's Plan section (goal, files to touch, decisions, pre-emptive gate checks) and returns. The Implementer then receives the Plan as input — fresh context, no exposure to whatever the orchestrator was discussing with the user.
- **Auditors cannot modify source.** Read-only Bash. This makes "audit + fix in same session" structurally impossible — fixes always happen in a separate Fixer invocation, after findings are RECORDED in the journal (no "I fixed it before recording it" sleight-of-hand).
- **Auditor adversarial framing.** Per [adversarial code review research](https://asdlc.io/patterns/adversarial-code-review/): the Auditor prompt explicitly states it's rewarded for finding issues, not for declaring CLEAN. Its role is hostile critic.
- **Parallel cluster dispatch is the default.** K=5 Auditors run concurrently per audit round, each scoped to one cluster of `rules.md` predicates (see [§3a](#3a-auditor-cluster-partition-canonical-k5)). The Aggregator (see [§3b](#3b-aggregator-role-post-cluster-consolidation)) merges the 5 partials into the canonical journal artifacts and performs cross-cluster verification. The orchestrator's dispatch workflow lives in [§3c](#3c-per-round-dispatch-protocol).
- **Effort-scaling rules in prompts** (per Anthropic guidance): each sub-agent prompt caps effort proportional to the step's surface area. Small step = "don't write 17 ctor variants for a 1-property record." Cluster scope already constrains per-Auditor effort to ~15-30 predicate rows.
- **Aggregator is load-bearing, never optional.** Whenever K>1 Auditors run, the Aggregator is what produces the canonical big table + consolidated findings log entry the journal commits to. It cannot change cluster Auditor verdicts unilaterally — only dedupes, merges, and adds cross-cluster sister-sweep findings the per-cluster Auditors couldn't see. If two Auditors disagree on the same row (rare; row ownership is partitioned by §-number), the Aggregator escalates to the orchestrator for a tie-breaker Auditor.
- **K=1 carve-out for trivial steps.** The orchestrator MAY run K=1 (Auditor doubles as Aggregator) when the step's surface area is truly tiny (one-line config tweak, single-line typo fix) and partitioning offers no parallelism win. The fresh-context property still holds — what's prohibited is reusing the Implementer's context for auditing, not running fewer parallel Auditors. K=5 is the default; K=1 requires a one-sentence justification in the orchestrator's dispatch note. **K=1 usage policy (CANONICAL): the orchestrator MUST NEVER self-invoke K=1 without explicit per-round user permission.** See [§3c K=1 carve-out usage policy](#k1-carve-out-usage-policy) for the full rationale + empirical justification.

---

## 3a. Auditor cluster partition (canonical K=5)

**Status: CANONICAL — in effect now.**

The `rules.md` catalog (~24 categories, ~145 numbered subsections) partitions into 5 thematic clusters. Each Auditor sub-agent owns exactly one cluster and walks every numbered subsection inside that cluster against the step's (or deliverable's, at final-review) file scope. The partition is fixed — orchestrator dispatch consistently sends the same §-range to the same cluster name across deliverables so accumulated muscle memory carries forward.

| Cluster | Name | rules.md sections | Connecting thread |
|---|---|---|---|
| **A** | Correctness + reliability | §1, §2, §4, §15, §18, §22 | "Does the code behave correctly across edge cases + failure modes." Tests, regression-pinning, concurrency / races, object disposal / resource lifetime, graceful degradation, idempotency / exactly-once. |
| **B** | Code style + idiom + shared-lib hygiene | §5, §6, §7, §16, §17 | "Is the code written per project idiom + leveraging the right tools." C# conventions, TS / SvelteKit conventions, naming / file headers / folder casing, OOTB shared-lib catalog use, D2Result usage + extensions. |
| **C** | Architecture + security + permission discipline | §3, §8, §9, §10, §13 | "Is the architecture sound + secure + safe to operate." PII / logging safety, build cleanliness, architectural layer hygiene, security (endpoints / auth / secrets / input), permission / action discipline. |
| **D** | Documentation + framing + i18n | §11, §12, §14 | "Is the project communicable + maintainable + free of conversation pollution." KEEP-doc updates + forward-framing, i18n / Paraglide / TK constants, no-phase-verbiage / no-conversation-scoped-IDs hygiene. |
| **E** | Operational outcomes + audit meta | §19, §20, §21, §23, §24 | "Is the system operable + the process self-improving." UX, DX, observability completeness, configuration hygiene, audit evidence discipline (incl. self-audit per §24.12). |

### Why this partition

- **Thematically cohesive**: each cluster's connecting thread is one coherent mental model — Auditor can stay in one frame of mind for the full walk instead of context-switching across orthogonal concerns.
- **Roughly balanced load**: each cluster carries ~15-30 numbered subsections, so wall-clock-per-Auditor is comparable. No cluster dominates.
- **Stable §-ownership**: the same §-range maps to the same cluster letter across every deliverable. A repeat finding's history can be threaded through past partials by cluster letter.
- **Cross-cutting concerns belong to the Aggregator**, not to any one cluster — the Aggregator's cross-cluster sister-sweep responsibility is what catches concerns that span clusters (e.g. a security predicate in §10 whose fix has style implications in §5, or a doc-framing concern in §11 that needs architectural verification in §9).

### When a predicate seems to straddle clusters

The cluster mapping is `rules.md` §-number → cluster letter, NOT topic → cluster letter. If a predicate's spirit feels like it belongs to two clusters, the §-number wins. The Aggregator's cross-cluster verification step (§3b step 4) is where straddle concerns get resolved — not in the per-cluster Auditor walk.

---

## 3b. Aggregator role (post-cluster consolidation)

**Status: CANONICAL — in effect now.**

The Aggregator is a single sub-agent spawned per audit round AFTER all K=5 cluster Auditors have returned their partials. It is the journal's authoritative writer for the round — the per-cluster Auditors write to disposable partial files; the Aggregator alone writes to the canonical journal sections.

### Six responsibilities (in order)

1. **Mechanical merge.** Read all 5 partial files (`r{N}-partial-{A|B|C|D|E}-{cluster-name}.md` in the round's working dir). Combine the 5 partial big-table chunks into ONE canonical sorted-by-§ big table. Write that table under `## Latest sweep results` in the step / final-review journal, REPLACING the prior sweep's table per the §24 sweep-replacement rule. Anti-laziness preamble appears verbatim above.
2. **Dedupe.** Same finding surfaced by multiple Auditors (e.g. a line-length violation Cluster B owns by predicate, but Cluster D also flagged from a doc-citation angle) collapses into a single entry with combined provenance. Dedupe preserves all citation paths in the entry's description.
3. **Cross-cutting verification.** Walk the deliverable's cross-step focus areas that span multiple clusters — defined per-deliverable in the Plan section of the final-review journal (e.g. "TYPE LIE FIX still verified end-to-end across .NET emitter + TS consumer", "β routing correctness across both consumers", "wire-shape leakage class — does the originating sample class exist anywhere else in scope?", "spec-driven catalog parity — every cross-language wire identifier cataloged + parity-tested"). These are the concerns no per-cluster Auditor could see because they cross §-ranges.
4. **Cross-cluster sister-sweep.** Per §24.13.3, cluster Auditors sister-sweep WITHIN their cluster's §-scope. The Aggregator runs sister-sweeps at the CROSS-cluster scope — e.g. a §14.1 phase-verbiage hit in Cluster D's scope should fire a sister-sweep that also covers Cluster B's recently-introduced shared-lib doc additions, since the same prose can leak phase verbiage AND violate Cluster B's idiom predicates simultaneously. The Aggregator owns this gap. See **Cross-cluster sister-sweep checklist** below for the concrete baseline commands the Aggregator runs every round regardless of cluster Auditor coverage.
5. **Append findings log.** Write a single `### Round N findings (<UTC>)` subsection under `## Sweep findings log (append-only)` in the journal containing: the consolidated finding list (from steps 2-4), a closure-verification table for prior-round findings (each prior-round finding annotated as CLOSED-by-absence in this round's big table OR STILL-PRESENT requiring another fix cycle), and a regression-verification table where applicable (prior-round PASS rows the Aggregator spot-confirmed are still PASS so cascading regressions are caught).
6. **Return summary to orchestrator.** A structured one-paragraph summary: total findings count by severity, list of fix-required §-rows, recommendation (CLEAN → next phase OR findings → spawn Fixer with specific scope).

### What the Aggregator cannot do

- **Cannot change per-cluster verdicts unilaterally.** A row Cluster B PASSed cannot be flipped to FINDING by the Aggregator without escalating to the orchestrator for a tie-breaker Auditor. The Aggregator can ADD findings (from steps 3-4 cross-cluster verification) but cannot OVERRULE Auditors.
- **Cannot touch source / tests / configs.** Write access is journal + audit artifacts only.
- **Cannot mark the step CLEAN.** It RECOMMENDS clean to the orchestrator; the orchestrator consumes the recommendation along with the big table itself (which must contain zero FINDING rows for CLEAN status to be valid).

### Why the Aggregator is load-bearing

When K>1 Auditors run in parallel, no single Auditor sees the full picture. Without an Aggregator, the orchestrator would need to either (a) read all 5 partials itself (forbidden per §2 main-thread restrictions), or (b) trust each Auditor's slice without cross-validation (defeats the parallelism win). The Aggregator is the structural fix: it consolidates, it cross-checks, and its output IS the journal's canonical record. A K=5 dispatch WITHOUT an Aggregator is incomplete; the round is not done until the Aggregator's `### Round N findings` subsection lands in the findings log.

### Cross-cluster sister-sweep checklist

The Aggregator MUST run the following baseline sweeps as part of step 4 (cross-cluster sister-sweep) — regardless of what cluster Auditors found in their partials. Cluster Auditors' sister-sweeps under §24.13.3 run WITHIN their cluster's predicate scope; the Aggregator's sweeps below run against the FULL DELIVERABLE DIFF SCOPE (typically the path-set from `git diff --name-only nova` minus gitignored paths + `docs/dev/deliverables/` immutable snapshots).

This checklist exists because the first live K=5 + Aggregator instantiation (deliverable 0007-wire-parity final-review R3) showed the cross-cluster sister-sweep was effective BUT the Aggregator had to derive the concrete commands from prose. Making them explicit removes the per-round derivation cost and guarantees a consistent baseline across deliverables.

| Sweep | Command (literal — substitute scope) | What it catches |
|---|---|---|
| **Past-framing** (§11.19 / §11.20) | `grep -rEn 'previously\|formerly\|used to\|was consolidated\|migrated from\|prior versions\|Resolved the CRITICAL\|Fixed a latent' <deliverable diff scope>` | Historical-narration prose that drifted into KEEP docs / source comments across multiple clusters at once |
| **Forward-framing** (§11.28) | `grep -rEn 'will be\|going to\|upcoming\|planned\|pending\|awaiting\|transitional\|temporary\|eventually\|future-proof\|once X ships' <deliverable diff scope>` | Forward-framing prose describing what DOESN'T exist yet (KEEP docs must describe current reality) |
| **Falsey/Truthy dogfood** (§5.1) | `grep -rEn 'string\.IsNullOrEmpty\|string\.IsNullOrWhiteSpace' <deliverable diff scope> --include='*.cs' \| grep -v '/Generated/' \| grep -v '/tests/'` | Hand-rolled null/empty checks where `Falsey()` / `Truthy()` applies (Cluster B predicate, but commonly co-occurs with Cluster A correctness fixes) |
| **Line-length** (§7.14) | `awk 'length > 100' <deliverable diff scope C# / TS files>` | Wide lines introduced anywhere in the deliverable. **Em-dash UTF-8 byte-counting artifact awareness per §24.13.2** — `awk length` measures BYTES not codepoints; em-dashes (3 bytes) inflate apparent length. Manually re-confirm any borderline hit by visual character count. |
| **Hand-mirrored cross-language constants** (§11.30) | Manual enumeration: identify wire identifiers (header names, error codes, JSON property names, OTel tag names) appearing as string literals in BOTH .NET and TS source within the diff scope, where a spec catalog should own them | Cross-language wire identifiers hand-duplicated instead of spec-cataloged + emitter-generated. Empirical: deliverable 0007 R3 surfaced 2 `"Authorization"` literals that should have referenced the `AUTHORIZATION` headers-spec constant. |

**Operating rules:**

- **Always full-diff scope, never narrowed.** The cluster Auditor sister-sweep is already cluster-scoped per §24.13.3. The Aggregator's job is to catch what fell between cluster boundaries — narrowing the Aggregator's sweep to one cluster's scope defeats the purpose.
- **Paste literal command + literal output into the Aggregator's `### Round N findings` subsection** under a `#### Aggregator cross-cluster baseline sweeps` heading. Zero hits = one line per sweep ("§14.1 past-framing: 0 hits"). Non-zero hits = each surfaced as its own consolidated finding with severity + file:line + description + suggested fix, classified per §24.13.3 dedup rule (originating-predicate classification + additional-predicate provenance).
- **Augment, do not replace.** This checklist is the BASELINE — the Aggregator MAY add deliverable-specific sweeps drawn from the per-deliverable cross-step focus areas defined in the Plan section of the final-review journal (e.g. "wire-shape leakage class — does the originating sample class exist anywhere else in scope?", "spec-driven catalog parity — every cross-language wire identifier cataloged + parity-tested"). The baseline runs every round; deliverable-specific sweeps run when applicable.
- **New recurring classes feed back into this checklist.** When a cross-cluster sister-sweep class proves valuable across multiple deliverables, propose adding it to the table above in the deliverable's distillation — keeping the checklist a living artifact rather than a static one.

---

## 3c. Per-round dispatch protocol

**Status: CANONICAL — in effect now.**

The orchestrator's workflow for one K=5 + Aggregator audit round. Same shape for per-step rounds and final-review rounds (the difference is scope: per-step = step's touched files; final-review = whole deliverable).

### Step 1 — Orchestrator writes the per-round shared-context file

Path: `docs/wip/<deliverable>/<NN>-<step>/r{N}-shared-context.md` for per-step rounds, or `docs/wip/<deliverable>/final-review/r{N}-shared-context.md` for final-review rounds.

Contents:
- Mission paragraph (what this round audits, why)
- Locked decisions (so cluster Auditors do not re-litigate)
- Deliverable scope (concrete path-set or `git diff --name-only` recipe)
- Special-emphasis user direction (if any user gave a focus area; e.g. "industry-standard naming alignment", "regression test adequacy for known bug classes")
- The K=5 cluster partition table (verbatim from §3a so every Auditor sees the canonical mapping)
- Output format spec (the partial-file template every Auditor writes against — see [§3a's partial template example](#partial-file-template-per-auditor) below)
- Aggregator role summary (so cluster Auditors know what their partials feed into and can flag cross-cluster handoffs explicitly)
- Critical constraints (READ-ONLY tools, no sub-agent spawning, no commits, no touching other auditors' partial files, sister-sweep per §24.13.3, self-grep per §24.13.4)

### Step 2 — Orchestrator dispatches 5 parallel Auditors in ONE message

All 5 spawned in a single `Agent` tool batch (one tool-call message containing 5 parallel `Agent` invocations). Each Auditor's brief is small:

- Read the shared-context file at the path above
- Read your cluster's §-range from `rules.md` end-to-end
- Skim other cluster ranges for cross-references
- Walk YOUR cluster against the deliverable scope
- Write to your designated partial file `r{N}-partial-{LETTER}-{cluster-name}.md` at the same directory

Concurrent writes are safe because each Auditor owns its own file. There is no shared mutable state between cluster Auditors. Run them as background `Agent` invocations (`run_in_background: true`) and let notifications return as each completes.

### Step 3 — Orchestrator waits for all 5 partials

When ALL 5 background notifications return, the orchestrator dispatches the Aggregator. The orchestrator does NOT read partials directly — it dispatches the Aggregator with the list of partial paths and lets the Aggregator do the merge.

### Step 4 — Orchestrator dispatches the Aggregator

A single `Agent` invocation (foreground OK; the Aggregator is not parallelizable). Brief:
- Read the 5 partials at the listed paths
- Read the deliverable's cross-cutting focus areas (named in the shared-context file)
- Perform the six responsibilities in §3b in order
- Write the canonical big table + `### Round N findings` subsection to the journal
- Return summary

### Step 5 — Orchestrator routes on the Aggregator's recommendation

- **CLEAN (zero FINDING rows + zero new cross-cluster findings)**: advance to next phase (next step, or SHIP for final-review).
- **FINDINGS present**: dispatch a fresh Fixer sub-agent with the consolidated finding list. After Fixer returns, dispatch the next round (R+1) — a brand-new K=5 batch + brand-new Aggregator, fresh context across the board.

### Wall-clock expectations

- A K=5 batch wall-clock is dominated by the slowest cluster, NOT the sum. Empirically the slowest cluster (typically Cluster B style / D docs depending on scope) determines round duration; clusters with thinner scope finish much sooner.
- A round = one K=5 dispatch + Aggregator + (if findings) one Fixer = ~1/4-1/5 of a sequential K=1 walk that covered the same predicate count.
- 10-iteration ceiling per step still applies — where ONE iteration = one full round of K=5 Auditors + Aggregator + (if findings) Fixer.

### K=1 carve-out usage policy

**Status: CANONICAL — in effect now.**

The K=1 single-Auditor dispatch is documented in [§3](#3-sub-agent-roles) as an option for truly tiny scope (one-line config tweak, single-line typo fix), but **the orchestrator MUST NEVER self-invoke K=1 without explicit per-round user permission.** The "narrow scope" / "tamper-evident proof exists" / "mechanical change" / "I already verified the fix" reasoning patterns are NOT valid self-justifications — they are exactly the cheating failure mode this framework exists to prevent (per [CLAUDE.md MANDATORY block 0](../../CLAUDE.md#mandatory-block-0-orchestrator-only-main-thread): "The ONLY bypass is an explicit user request").

**If you think K=1 is appropriate, ASK the user before dispatching.** Write the proposed K=1 justification in your message to the user (what the scope is, why partitioning offers no parallelism win, what coverage guarantees you're forfeiting) and wait for explicit `K=1 approved` before dispatching. Without that explicit per-round approval, the orchestrator defaults to K=5 per [§3a](#3a-auditor-cluster-partition-canonical-k5) — every audit round, no exceptions.

**Why** (empirical justification): deliverable 0008-geo-data-pipeline final audit cycle empirically validated this policy. After the R-final-1 K=5 round + Final Fixer round, the orchestrator self-invoked a K=1 verification pass (R-final-V) reasoning that "the Fixer changes are narrow and tamper-evident proof exists." That K=1 round surfaced 2 brand-new findings (R-final-V-1 HIGH §14.3 conversation-scoped ID + R-final-V-2 LOW §7.14 line-length residuals — both introduced by Final Fixer 3's new test file) AND a §24.0 process gap (Final Fixer 2 + Final Fixer 3 missing fix-log entries) that the orchestrator had not anticipated. The user then required full K=5 dispatch (R-final-2) per CLAUDE.md MANDATORY, which independently surfaced ONE FURTHER finding the K=1 had missed: R-final-3-D-F-1 MEDIUM (cross-doc Tier 3 contradiction in `tools/geo-data-pipeline/README.md` — sister-sweep gap inherited from R-final-1's D-F-3 fix). A second K=5 round (R-final-3) was then required to certify closure. Net outcome: the self-invoked K=1 cost an additional R-final-V round + R-final-2 K=5 round + R-final-3 K=5 round to fully certify SHIP-readiness, plus a process-integrity breach that the user explicitly called out.

**Why secondary K=5 passes are load-bearing even when prior closures look complete**: K=5 passes don't just verify prior closures — they also catch issues missed in initial passes because different cluster Auditor angles + different cross-cluster sister-sweeps reveal what single-Auditor walks structurally cannot. A K=1 Auditor sees their own §-range only; the 5 K=5 Auditors collectively walk the full catalog with 5 independent fresh-context perspectives, and the Aggregator's cross-cluster sister-sweep (per [§3b](#3b-aggregator-role-post-cluster-consolidation) step 4) catches drift classes that span clusters. The 5 partials + Aggregator structure IS the coverage guarantee; collapsing to K=1 collapses the guarantee.

**How to apply**:

1. **Default**: every audit round per [§3c step 2](#step-2--orchestrator-dispatches-5-parallel-auditors-in-one-message) dispatches K=5. No exceptions, no self-justification.
2. **K=1 candidate identification**: if the orchestrator believes K=1 is appropriate (e.g. step really is a single-line typo fix), the orchestrator writes a proposed-K=1 message to the user enumerating: (a) the exact scope (what's changed), (b) why partitioning offers no parallelism win, (c) what coverage guarantees are forfeited (which cluster perspectives won't be exercised), (d) why the orchestrator believes those forfeitures are acceptable for this scope.
3. **User approval**: the user responds with explicit `K=1 approved` (or equivalent unambiguous approval) per occurrence. Approvals do NOT carry forward to subsequent rounds — every K=1 round needs fresh per-occurrence approval.
4. **Without explicit approval**: dispatch K=5. Even if the orchestrator has previously discussed K=1 with the user, even if the prior round was K=1-approved, every NEW round defaults to K=5 unless freshly approved.
5. **Verification rounds after Fixer**: especially-important target for the policy. The post-Fixer verification round is exactly where the orchestrator is most tempted to rationalize K=1 ("the Fixer's tamper-evident proof shows the change landed; I just need to confirm closure"). That rationalization is the failure mode empirically demonstrated by deliverable 0008 R-final-V. Post-Fixer verification rounds default to K=5 per the standard policy; the Fixer's tamper-evident output (per §24.14) speeds up each cluster Auditor's verification but does NOT eliminate the need for K=5's independent angles + cross-cluster sister-sweep.

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
> ✅ / ❌ / ⚪ / 🟡 emoji indicator. NO SHORTCUTS. Per §24.13.2: regex is a TOOL not source
> of truth — manual reading required. Per §24.13.3: sister-sweep at full predicate applicability.

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

---

> # ⚠️ Sections 4-9 below: ASPIRATIONAL TOOLING BUILDOUT
>
> Everything from here through §9 describes tooling that is **not yet built**. The current canonical workflow uses the markdown 3-artifact journal model (`## Latest sweep results` big table + `## Sweep findings log (append-only)` + `## Fix log (append-only)`) per [workflow.md §3](workflow.md#3-audit-loop-the-core-forcing-function-fresh-auditor--fixer-sub-agents-per-round) and [rules.md §24](rules.md#24-audit-evidence-discipline-meta--how-to-audit). The orchestrator + sub-agent separation in §2-§3 is in effect now; the JSON-first artifacts + recipe-runner + validator-script + `rules.md` recipe/judgment/process bucketing described below are the future tooling buildout.

---

## 4. JSON-first audit artifacts

**Status: ASPIRATIONAL — not yet built.** Will replace the markdown journal artifacts when shipped.

The audit output will be JSON, not markdown. Markdown views are GENERATED from JSON by a renderer for human readability, but JSON is the source of truth.

### Directory layout per step

```
docs/wip/<deliverable>/<NN>-<step>/
├── README.md                          # plan, decisions, prerequisites (human-authored)
├── audit/
│   ├── round-1/
│   │   ├── implementer-report.json
│   │   ├── auditor-1-of-6.json       # partial big-table chunk
│   │   ├── auditor-2-of-6.json
│   │   ├── ...
│   │   ├── big-table.json             # aggregated canonical
│   │   └── validator-report.json      # output of validator script
│   ├── round-2/
│   │   └── ...
│   ├── findings/
│   │   ├── §1-tests.json              # category-indexed, append-only across rounds
│   │   ├── §3-pii.json
│   │   └── ...
│   ├── fix-log.json                   # chronological, append-only
│   └── README.md                      # rendered human-readable summary (auto-generated)
```

### Big table row schema (JSON)

```json
{
  "section": "§3.1",
  "predicate": "[LoggerMessage] no Exception parameter",
  "status": "PASS",
  "verification": {
    "type": "recipe",
    "recipeId": "no-exception-in-loggermessage",
    "evidenceLocator": {
      "file": "server/shared/dotnet/auth/Telemetry/AuthLog.cs",
      "lineRange": [30, 92]
    },
    "evidenceContent": "[LoggerMessage(EventId = 1001, Level = LogLevel.Warning, ...)]",
    "evidenceHash": "sha256:abc123..."
  },
  "reason": null,
  "finding": null
}
```

For N/A:
```json
{
  "section": "§1.4",
  "predicate": "gRPC reg helpers tested",
  "status": "N/A",
  "verification": {
    "type": "grep-zero-matches",
    "command": "grep -rln 'Grpc' server/shared/dotnet/auth/",
    "matches": []
  },
  "reason": "Step 03 has no gRPC surface — only ASP.NET Core hosted services + IHttpClientFactory.",
  "finding": null
}
```

For FINDING:
```json
{
  "section": "§16.5",
  "predicate": "ResilientPipeline for retryable network calls",
  "status": "FINDING-HIGH",
  "verification": {
    "type": "manual-inspection",
    "evidenceLocator": {
      "file": "server/shared/dotnet/auth/AuthServiceCollectionExtensions.cs",
      "lineRange": [83, 87]
    },
    "evidenceContent": "services.AddHttpClient(OIDC_DISCOVERY_HTTP_CLIENT_NAME, (sp, client) => {\n    var opts = ...\n    client.Timeout = opts.Jwks.HttpRequestTimeout;\n});"
  },
  "reason": null,
  "finding": {
    "severity": "HIGH",
    "description": "Named OIDC discovery HttpClient configures only Timeout; no ResilientPipeline / circuit-break.",
    "suggestedFix": "Wrap r_configManager.GetConfigurationAsync calls in D2.Shared.Resilience.CircuitBreaker<OpenIdConnectConfiguration>",
    "affectedFiles": ["server/shared/dotnet/auth/Jwks/HttpJwksProvider.cs"]
  }
}
```

### Schema enforcement

Schemas live in `tools/audit/schemas/` as JSON Schema (or zod TS definitions if we go TypeScript). Validator script re-validates every JSON artifact against its schema on every read. Sub-agents that produce malformed JSON get rejected before their output enters the journal.

---

## 5. Anti-cheat: validator + recipe-runners

**Status: ASPIRATIONAL — not yet built.**

Two layers of mechanical checking.

### Layer 1 — Schema validator

`tools/audit/validate-schema.js <json-file>` parses + schema-validates. Catches:
- Missing required fields
- Bad status enum values
- Bad severity enum values
- Malformed evidenceLocator
- Missing recipeId

This is cheap and fast. Run on every sub-agent JSON output.

### Layer 2 — Evidence verifier

`tools/audit/verify-evidence.js <big-table.json>` walks every row and:

- **PASS rows with `verification.type === "recipe"`**: re-runs the recipe; verifies the recipe still produces a passing result on current code state.
- **PASS rows with `verification.type === "manual-inspection"`**: reads the cited file at the cited line range; verifies the `evidenceContent` matches what's actually on disk. (Optionally: hash-compare via `evidenceHash`.)
- **N/A rows with `verification.type === "grep-zero-matches"`**: re-runs the cited command; verifies the result matches the claimed matches array.
- **FINDING rows**: verifies the cited file:line exists; verifies `evidenceContent` matches disk; severity is canonical.

**If verification fails, the entire audit round is rejected.** Orchestrator logs the failure and re-spawns Auditors with feedback ("rows X, Y, Z failed verification with these mismatches").

This is the critical anti-cheat. The agent literally cannot fabricate evidence because the script catches mismatches between claims and reality.

### Layer 3 — Recipe runners (proactive scanning)

`tools/audit/run-recipes.js --step <NN>` runs every recipe-verifiable predicate against the step's files and produces a draft big table. Auditors START with this auto-generated draft and only need to manually walk the judgment-required predicates. This decimates auditor cognitive load.

---

## 6. `rules.md` transformation: recipe / judgment / process buckets

**Status: ASPIRATIONAL — not yet applied to `rules.md`.** Currently every subsection is walked as judgment-required by the Auditor sub-agent.

Each numbered subsection in `rules.md` will be classified into one of three buckets:

### (i) Recipe-verifiable (~70% expected)

Things that can be checked by a script. Each gets a `recipeId` + recipe definition file.

Example: `tools/audit/recipes/no-exception-in-loggermessage.js`
```js
export const recipe = {
  id: "no-exception-in-loggermessage",
  section: "§3.1",
  predicate: "[LoggerMessage] no Exception parameter",
  origin: "PII leak — ex.Message can interpolate JWT bytes / passwords / user input",
  async run({ stepFiles }) {
    const declarations = await grepLoggerMessageDeclarations(stepFiles);
    const violations = declarations.filter(hasExceptionParameter);
    return {
      status: violations.length === 0 ? "PASS" : "FINDING",
      evidence: { type: "recipe", declarations },
      finding: violations.length > 0 ? {
        severity: "HIGH",
        description: `${violations.length} [LoggerMessage] delegate(s) accept Exception parameter`,
        affectedFiles: violations.map(v => v.file),
      } : null,
    };
  },
};
```

### (ii) Judgment-required (~25% expected)

Things requiring agent judgment that no script can check. Examples: "is this architectural fit right?", "does this naming convey intent?", "is the docstring accurate?"

These stay as agent-walked predicates but are FLAGGED in `rules.md` as `judgment-required` so the Auditor knows to scrutinize them harder. Auditor manually fills the row's `verification.type === "manual-inspection"` with cited file:line + evidenceContent.

### (iii) Process-discipline (~5% expected)

Things about HOW the audit runs, not WHAT the code looks like. Examples: "don't skip sweeps", "every fix lands with a regression test", "audit table contains all 3 artifacts."

These move OUT of the auditor's checklist and INTO the orchestrator's logic. The orchestrator enforces them structurally (e.g., it cannot advance to next step if the validator script reports the audit incomplete).

### Schema for `rules.md` post-transformation

```markdown
## §3.1 [LoggerMessage] no Exception parameter

**Bucket:** recipe-verifiable
**Recipe:** `no-exception-in-loggermessage` (in `tools/audit/recipes/`)
**Severity if violated:** HIGH
**Origin:** PII leak — ex.Message can interpolate JWT bytes, passwords, user input. See deliverable 0001 retro for the original incident that motivated this rule.
```

For judgment-required:
```markdown
## §9.4 Smart-constructor input validation at top of ExecuteAsync

**Bucket:** judgment-required
**What to check:** Handler entry-points validate input via `Domain.Create(input) → D2Result<Domain>` before any work. Reject input at the boundary; never let Redis / DB be the first to reject invalid data.
**Evidence to cite:** file:line of validation call + the `Domain.Create` signature it routes to
**Severity if violated:** MEDIUM
**Origin:** Dec 2025 incident where invalid input reached the cache layer and produced confusing 500s instead of clean 400s.
```

---

## 7. Per-step flow (state machine)

**Status: ASPIRATIONAL — describes the fully-tooled flow.** The current canonical flow follows the same shape (Implementer → Auditor → Aggregator → Fixer, looped) but uses markdown journal artifacts instead of JSON; see [workflow.md §EXECUTE](workflow.md#execute) for the in-effect version.

```
┌──────────────────────────────────────────────────────────────────────┐
│ Orchestrator: read PLAN, pick next pending step                      │
└──────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Spawn IMPLEMENTER (fresh ctx)                                        │
│ - given: step spec, applicable rules.md categories                   │
│ - returns: filesTouched, testsAdded, testsTotal, buildStatus         │
└──────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Run `tools/audit/run-recipes.js --step NN`                           │
│ - produces: draft big-table.json from recipe-verifiable predicates   │
└──────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Spawn K=5 AUDITORS in parallel (fresh ctx each; canonical partition │
│ per §3a — each takes one of clusters A/B/C/D/E)                      │
│ - given: draft big-table.json + assigned cluster's predicate range   │
│   + filesTouched list + shared-context file path                     │
│ - each returns: partial big-table.json chunk for its cluster         │
└──────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Spawn AGGREGATOR (fresh ctx)                                         │
│ - combines K partial tables into canonical big-table.json            │
│ - REPLACES `## Latest sweep results` view in journal                 │
│ - APPENDS per-category findings to findings/§*.json                  │
└──────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Run `tools/audit/verify-evidence.js big-table.json`                  │
└──────────────────────────────────────────────────────────────────────┘
                            │
              ┌─────────────┴─────────────┐
              ▼                           ▼
      [verification fails]        [verification passes]
              │                           │
              ▼                           ▼
   Orchestrator re-spawns      ┌─────────────────────────┐
   AUDITORS with feedback      │ Has FINDING rows?       │
              │                └─────────────────────────┘
              └──────────────┐                │
                             │      ┌─────────┴─────────┐
                             │      ▼                   ▼
                             │   [yes]               [no — CLEAN]
                             │      │                   │
                             │      ▼                   ▼
                             │  Spawn FIXER       Step done.
                             │  (fresh ctx)       Advance to next step.
                             │      │
                             │      ▼
                             │  Fixer applies fixes,
                             │  appends fix-log.json
                             │      │
                             └──────┘
                          (loop: next round of audit)
```

Key invariants:
- The Implementer never sees the Auditor's output. Fresh contexts.
- The Auditor never sees the Implementer's intent — only the files. This forces the Auditor to evaluate code on its own merits, not on the Implementer's narrative.
- The Aggregator never re-evaluates verdicts — purely combines.
- The Fixer never re-audits — purely applies findings. Closure proven by the NEXT round's verifier seeing the finding gone.
- The Orchestrator never marks CLEAN — that comes from the verifier reporting zero findings.

---

## 8. Tooling layout (`tools/audit/`)

**Status: ASPIRATIONAL — directory does not yet exist.**

```
tools/audit/
├── README.md                        # how to invoke + dev guide
├── package.json                     # Node deps (zod, glob, etc.)
├── tsconfig.json                    # if TypeScript
├── schemas/
│   ├── big-table-row.schema.json
│   ├── implementer-report.schema.json
│   ├── findings-entry.schema.json
│   └── fix-log-entry.schema.json
├── recipes/
│   ├── §3.1-no-exception-in-loggermessage.js
│   ├── §5.1-falsey-truthy.js
│   ├── §5.7-sealed-by-default.js
│   ├── §7.7-file-headers.js
│   ├── §7.14-line-length.js
│   ├── §7.15-american-english.js
│   ├── ...
│   └── _recipe-base.js              # shared helpers (grep, ast walk, etc.)
├── lib/
│   ├── grep.js                      # consistent grep wrapper
│   ├── ast-walker.js                # for recipes that need C# AST
│   └── line-content.js              # read file:line, return content + hash
├── orchestrate.js                   # state machine; main entry point
├── run-recipes.js                   # produces draft big-table.json
├── validate-schema.js               # JSON schema validation
├── verify-evidence.js               # re-runs recipes, checks evidence claims
├── render.js                        # JSON → markdown for human review
└── tests/                           # jest / vitest
    ├── recipes.test.js
    ├── verify-evidence.test.js
    └── ...
```

Invocation surface:
- `node tools/audit/orchestrate.js --deliverable 0002-auth-inbound --step 05` — runs full per-step loop
- `node tools/audit/run-recipes.js --step server/shared/dotnet/auth/Jwks/` — produces draft table
- `node tools/audit/verify-evidence.js docs/wip/0002-auth-inbound/05-jwt-validator/audit/round-1/big-table.json` — re-verifies
- `node tools/audit/render.js --step 0002-auth-inbound/05-jwt-validator` — produces human-readable journal markdown

---

## 9. Tooling buildout phases

**Status: ASPIRATIONAL — these phases describe the JSON-first / recipe-runner / validator-script tooling buildout, not the workflow itself.** The orchestrator + sub-agent separation (§2-§3) is already canonical and in effect now; this section is about mechanizing the markdown 3-artifact journal model into JSON-validated artifacts.

| Phase | Task | Estimated effort | Order |
|---|---|---|---|
| 1 | Build `validate-schema.js` + `verify-evidence.js` + JSON schemas. Baseline anti-cheat infra. | 1-2 days | First |
| 2 | Convert `rules.md` predicates: classify into recipe/judgment/process buckets. | 2-3 days | Second |
| 3 | Author recipe files for the recipe-verifiable bucket (~70% of predicates). | 3-5 days | Third |
| 4 | Build `orchestrate.js` state machine + `run-recipes.js` + `render.js`. | 1-2 days | Fourth |
| 5 | Update `CLAUDE.md` / `workflow.md` / this doc to reference JSON-tooled flow as canonical (right now §2-§3 are canonical against the markdown journal model). | 1 day | Fifth |
| 6 | Live-test on a real step. Iterate. | ongoing | Sixth |
| 7 | Backfill recipes for predicates that prove flaky in live tests. | ongoing | Seventh |

**Recommended approach**: ship Phase 1 (`validate-schema.js` + `verify-evidence.js`) opportunistically — it will catch fabricated PASS rows in the current markdown journals immediately, even before the rest of the JSON-first artifact model lands. Defer Phase 2-7 until there's a coherent block of agent-time to author recipes against the post-0002 rules.md.

---

## 10. Open decisions

1. **TypeScript vs vanilla JS** — TS gives schema definitions for free (zod) + better DX, but adds build step. Vanilla JS is simpler. **Lean TS** given the rest of the Node side is TS.
2. **Sub-agent partitioning strategy for K Auditors** — **DECIDED**: by `rules.md` §-number into 5 thematic clusters, canonical mapping in [§3a](#3a-auditor-cluster-partition-canonical-k5). K=5 with Aggregator load-bearing per [§3b](#3b-aggregator-role-post-cluster-consolidation).
3. **Tie-breaker mechanism** when 2 Auditors disagree on a row — spawn 3rd Auditor as tie-breaker, OR escalate to user. **Lean tie-breaker first, escalate if tie-breaker also conflicts.**
4. **Recipe authoring incentive** — should recipe authors get to declare effort scaling (e.g., "this recipe is cheap, run it on every file" vs "this recipe is expensive, sample 10%")? **Probably yes for performance.**
5. **Backwards compat with current journal markdown** — should the renderer produce markdown identical to current journal layout, or break compat with cleaner format? **Lean break compat, optimize for new layout.**
6. **What happens if a recipe itself has a bug** — false positive? false negative? Need a recipe-test suite (see `tests/recipes.test.js` above).
7. **What happens at FINAL-REVIEW** — **DECIDED**: same K=5 architecture per [§3a](#3a-auditor-cluster-partition-canonical-k5) + [§3c](#3c-per-round-dispatch-protocol), scoped to entire deliverable instead of one step. Each cluster Auditor walks its §-range against the cumulative deliverable scope; Aggregator merges + performs cross-cutting verification against deliverable-wide focus areas defined in the Plan section of the final-review journal.

---

## 11. Research references

- **[How we built our multi-agent research system — Anthropic engineering](https://www.anthropic.com/engineering/multi-agent-research-system)** — the orchestrator-worker pattern that 90.2%-outperformed single-agent Opus on internal evals. Validates the architectural shape.
- **[Building effective AI agents — Anthropic](https://www.anthropic.com/research/building-effective-agents)** — five composable workflow patterns (prompt chaining, routing, parallelization, orchestrator-workers, evaluator-optimizer). This framework uses orchestrator-workers + evaluator-optimizer.
- **[Building agents with the Claude Agent SDK — Anthropic](https://www.anthropic.com/engineering/building-agents-with-the-claude-agent-sdk)** — SDK ships sub-agents as first-class. Confirms isolated context windows.
- **[Create custom subagents — Claude Code Docs](https://code.claude.com/docs/en/sub-agents)** — fresh isolated context per sub-agent invocation. Context rot avoidance.
- **[Adversarial Code Review pattern — ASDLC](https://asdlc.io/patterns/adversarial-code-review/)** — Critic Agent reviews Builder Agent output. Breaks the self-validation echo chamber.
- **[Why AI Agent Outputs Need Adversarial Review — DEV Community](https://dev.to/rih0z/why-ai-agent-outputs-need-adversarial-review-and-how-to-add-it-in-one-api-call-1l92)** — quantifies LLM self-review leniency bias. Critical: "Most 'agent reviews agent' implementations are one LLM with a clever prompt pretending to be three reviewers, where the model can rubber-stamp itself" — argues for SEPARATE sub-agent invocations, not roleplay.
- **[The checklist manifesto — PMC](https://pmc.ncbi.nlm.nih.gov/articles/PMC4953332/)** — Gawande's surgical checklist research. 19 items, 2 minutes per checklist, 1/3rd reduction in inpatient complications. Lesson: short hierarchical checklists work; long flat ones get skipped. Applied here as 24 categories with recipe decomposition underneath, not 200 flat predicates.

---

## Appendix A: How this addresses each empirical failure mode

| Failure mode (observed in 0002-auth-inbound) | How the new framework prevents |
|---|---|
| Prose-as-evidence drift | Schema validator rejects rows without structured evidence; evidence verifier re-runs recipe + diff-checks line content |
| Convergence illusion | Orchestrator never marks CLEAN — verifier does. Fresh sub-agents have no investment in stopping. |
| Stale-memory shortcuts | Sub-agents have fresh context — no conversation summary to trust |
| Scope-narrowing | Recipe runner auto-fills ~70% of predicates; remaining ~30% scoped to judgment-required only — smaller load = harder to rationalize skipping |
| Self-review leniency bias | Auditor is separate sub-agent invocation, not main thread. Adversarial prompt framing. |
| Mid-execution tier audits adding cycles without value | Tier audits removed entirely. Per-step audit sufficient because Auditor scope explicitly includes all files step touched (incl. files from prior steps if modified). |
| Implementer self-marking findings as fixed | Fixer is separate role; cannot mark anything CLEAN; closure proven by next round's verifier |

---

## Appendix B: Mapping to Anthropic's five workflow patterns

| Pattern | Use in this framework |
|---|---|
| Prompt chaining | Implementer → Auditor → Aggregator → Fixer is a chain |
| Routing | Orchestrator routes based on validator output (CLEAN vs FINDINGS vs validation-fail) |
| Parallelization | K Auditors in parallel; per-category recipe runs in parallel |
| Orchestrator-workers | Main thread is orchestrator; sub-agents are workers |
| Evaluator-optimizer | Auditor evaluates, Fixer optimizes — looped until clean |

All five patterns compose into one framework. This is what Anthropic's research system achieves at scale; this design applies the same pattern to the audit loop specifically.

---

## Appendix C: Trial outcomes from deliverable 0002-auth-inbound

The orchestrator + adversarial sub-agent separation pattern (§2-§3) was trialed across deliverable 0002-auth-inbound (8 steps + final-review + 3 polish rounds) before being promoted to canonical workflow status. This appendix captures empirical outcomes that justified the promotion.

### Two production bugs caught by adversarial separation that single-context implementation would have shipped

**Bug 1 — wrong UserState slot in JwtAuthInterceptor**

`JwtAuthInterceptor.ResolveMethodScopeMetadata` was reading the wrong `UserState` slot — code path that compiled clean and passed the Implementer's unit tests. The miss was caught by an integration test the Implementer had skipped (judging it "thin glue, no logic to test"). A separate Fixer sub-agent was forced to author the integration test as part of resolving the finding; the test then surfaced the wrong-slot read. Single-context implementation would have shipped this — the test that caught it only existed because adversarial separation forced its creation.

**Bug 2 — `act_chain_malformed` dead-letter chain**

`MalformedActorChainException` propagated uncaught from `ClaimsToContextMapper.Map` → JwtValidator was returning UnhandledException-shaped failures instead of the canonical `act_chain_malformed` AuthErrorCode. The miss was structural: the `AuthFailures.ActChainMalformed` helper existed, the `AuthErrorCodes.ActChainMalformed` constant existed, the `JwtValidator` xmldoc enumerated the outcome, the README documented it — but the validator implementation never emitted the outcome. The mismatch surfaced only at deliverable-wide final-review, when a fresh Final-reviewer sub-agent enumerated "what is documented vs what is actually emitted." A per-step Auditor would have walked just the JwtValidator step and seen consistent code+docs+tests; the cross-cutting gap required the deliverable-wide adversarial walk.

### Convergence in 1-3 rounds (mostly 2)

Per-step audit loops converged in 1-3 rounds across all 8 steps, with 2 rounds being the modal case. This is the empirical validation that the pattern works at scale — predicate satisfaction can be reached through fresh-context iteration without runaway round counts. The 10-iteration ceiling was never approached.

### Main-thread context stayed small

Across the whole deliverable (8 step-level audits + final-review + 3 rounds of polish + cross-deliverable design discussions about the auth-outbound stack), the main-thread context remained well under capacity. This is the key win of orchestrator-only main-thread: domain detail lives in sub-agent contexts that die on return, leaving the orchestrator free to handle long-arc decision-making across many steps.

### User feedback after the trial

> "the subagents, while slower to complete work, are actually doing a cleanly better job"

The wall-clock-time tradeoff is real — sub-agent spawning adds overhead per round. But the quality differential in resulting code is the dominant factor; production-bug-catch rate is what justifies the workflow.

### Why this promotes from "trial" to "canonical"

The trial established three things simultaneously:
1. The pattern catches bugs that single-context implementation ships (concrete: the two bugs above).
2. Convergence is achievable in practice (concrete: 1-3 rounds per step, 8/8 steps reached CLEAN).
3. Main-thread context stays small enough that the orchestrator can drive long deliverables (concrete: 8-step deliverable + 3-round polish + cross-deliverable discussion fit comfortably).

All three together = the pattern is fit-for-purpose for D²-WORX's enterprise-readiness bar. Promotion to canonical removes the per-deliverable "should we use sub-agents this time?" decision and makes adversarial separation the default execution shape.
