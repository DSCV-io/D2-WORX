<!--
Copyright (c) DCSV. All rights reserved.
-->

# Audit Framework — orchestrator + sub-agents + JSON evidence + recipe-runner

> **Status**: design proposal, not yet implemented. This doc captures the architecture decisions made on `n/auth` for the next-generation audit framework that will replace the current self-policed model.
>
> **Author note**: written by the agent in collaboration with the user. The intent is for THE AGENT to find this framework reliable to follow — it is designed for agent ergonomics first, human readability second (with renderers bridging the gap).

---

## Table of contents

1. [Motivation — why the current model breaks](#1-motivation--why-the-current-model-breaks)
2. [Architecture — orchestrator-only main thread + sub-agents](#2-architecture--orchestrator-only-main-thread--sub-agents)
3. [Sub-agent roles](#3-sub-agent-roles)
4. [JSON-first audit artifacts](#4-json-first-audit-artifacts)
5. [Anti-cheat: validator + recipe-runners](#5-anti-cheat-validator--recipe-runners)
6. [`rules.md` transformation: recipe / judgment / process buckets](#6-rulesmd-transformation-recipe--judgment--process-buckets)
7. [Per-step flow (state machine)](#7-per-step-flow-state-machine)
8. [Tooling layout (`tools/audit/`)](#8-tooling-layout-toolsaudit)
9. [Implementation phases](#9-implementation-phases)
10. [Open decisions](#10-open-decisions)
11. [Research references](#11-research-references)

---

## 1. Motivation — why the current model breaks

The current framework relies on the agent self-policing adherence to ~200 predicates in `rules.md`. Empirically (multiple audit cycles in deliverable 0002-auth-inbound) this fails in predictable ways:

- **Prose-as-evidence drift**: agent writes "PASS — verified" without actually re-reading the file. File:line citation requirement helps but doesn't eliminate.
- **Convergence illusion**: agent declares CLEAN one round too early because the alternative (more rounds) feels like failure. Motivated stopping.
- **Stale-memory shortcuts**: agent trusts conversation summaries over fresh disk reads.
- **Scope-narrowing under cost**: 200+ predicates feels expensive → agent rationalizes "obviously N/A" without specific evidence.
- **Self-review leniency bias**: validated by [Anthropic-adjacent research](https://dev.to/rih0z/why-ai-agent-outputs-need-adversarial-review-and-how-to-add-it-in-one-api-call-1l92) — when an LLM reviews its own output it overwhelmingly approves.

Adding more verbal rules (CAPS, "DO NOT BE LAZY", longer prose) doesn't fix these — they're ignored when convenient. The fix is structural: separate roles, mechanical evidence, and tool-mediated verification.

Real bugs that slipped past per-step audits in the current model and were only caught because someone (user OR a fresh sub-agent) re-walked from scratch:
- `AuthOptions` `required init` incompatible with `Action<T>` configure pattern (Step 02 — masked by missing composition test)
- `JwksFetchDurationMs` histogram declared but never recorded (Step 03 — silent dead metric)
- HTTP 100s default timeout unset on OIDC discovery client (Step 03 — would have hung threads under Edge outage)
- Empty `BackplaneChannelKey` would silently drop every rotation event (Step 03)

These are the kind of failures that compound silently. The framework has to catch them BEFORE production, not "when an audit happens to be diligent."

---

## 2. Architecture — orchestrator-only main thread + sub-agents

Anthropic's [multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) uses orchestrator-Opus + worker-Sonnet and outperforms single-agent Opus by 90.2% on internal evals. The pattern: lead agent decomposes + delegates; workers execute in isolated context; results synthesize back.

Apply that pattern to the audit loop:

**Main thread = pure orchestrator.** Restricted tool access:
- ✅ `Agent` (spawn sub-agents)
- ✅ `Bash` — only for `git`, `dotnet build` summary, validator script invocations
- ✅ `Read` — only for plan files, deliverable README, journal review at decision points
- ✅ `TaskUpdate`
- ❌ `Edit` / `Write` to source / tests (sub-agents do this)
- ❌ `Edit` / `Write` to journal big-table or findings log (Aggregator sub-agent does this)
- ✅ `Edit` / `Write` to its own decision log (`docs/wip/<deliverable>/orchestrator-log.json`)

The main thread's job is decision-making, not implementation, not auditing. It reads sub-agent outputs and routes the next step. **It cannot mark anything CLEAN or PASS itself** — it only consumes those verdicts from sub-agents and validator script output.

This mirrors Claude Code's [sub-agent design](https://code.claude.com/docs/en/sub-agents): each sub-agent gets a fresh isolated context, encapsulates work, and returns only relevant output to the orchestrator. Context rot in the main thread becomes near-impossible because the main thread holds almost no state.

---

## 3. Sub-agent roles

Five distinct roles. Each is spawned with a fresh context and a tightly-scoped prompt.

| Role | Spawned when | Tool access | Returns |
|---|---|---|---|
| **Implementer** | Start of each step | All | JSON: `{ filesTouched: [...], testsAdded: N, testsTotal: N, buildStatus: "clean" }` |
| **Auditor** (parallel ×K) | After Implementer | Read, Grep, Glob, Bash (read-only), recipe-runner | Partial big-table chunk (JSON) for assigned categories |
| **Aggregator** | After all Auditors | Read, Edit (journal + audit JSON only) | Updated journal: replaces big table, appends per-category findings log |
| **Fixer** | When findings exist | All | JSON: `{ filesChanged: [...], fixLogEntries: [...] }` |
| **Final-reviewer** (parallel ×K, deliverable-end only) | Before SHIP | Same as Auditor | Deliverable-wide partial big tables |

**Key design decisions:**

- **Auditors cannot modify source.** They have read-only Bash. This makes "audit + fix in same session" structurally impossible — fixes always happen in a separate Fixer invocation, after the Aggregator has consolidated findings, which means findings get RECORDED before fixes are applied (no "I fixed it before recording it" sleight-of-hand).
- **Auditor adversarial framing.** Per [adversarial code review research](https://asdlc.io/patterns/adversarial-code-review/): the Auditor prompt explicitly states it's rewarded for finding issues, not for declaring CLEAN. Its role is hostile critic.
- **Effort-scaling rules in prompts** (per Anthropic guidance): each sub-agent prompt caps effort proportional to the step's surface area. Small step = "don't write 17 ctor variants for a 1-property record."
- **Aggregator is mechanical.** Cannot change verdicts — only combines K partial tables into one canonical table. If two Auditors disagree on the same row, escalate to orchestrator (which spawns a tie-breaker Auditor).

---

## 4. JSON-first audit artifacts

The audit output is JSON, not markdown. Markdown views are GENERATED from JSON by a renderer for human readability, but JSON is the source of truth.

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

Each numbered subsection in `rules.md` gets classified into one of three buckets:

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
│ Spawn K AUDITORS in parallel (fresh ctx each)                        │
│ - given: draft big-table.json + assigned chunk of judgment-required  │
│   predicates + filesTouched list                                     │
│ - K typically 4-6 for balanced load                                  │
│ - each returns: partial big-table.json chunk                         │
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

## 9. Implementation phases

| Phase | Task | Estimated effort | Order |
|---|---|---|---|
| 1 | Build `validate-schema.js` + `verify-evidence.js` + JSON schemas. Baseline anti-cheat infra. | 1-2 days | First |
| 2 | Convert `rules.md` predicates: classify into recipe/judgment/process buckets. | 2-3 days | Second |
| 3 | Author recipe files for the recipe-verifiable bucket (~70% of predicates). | 3-5 days | Third |
| 4 | Build `orchestrate.js` state machine + `run-recipes.js` + `render.js`. | 1-2 days | Fourth |
| 5 | Update `CLAUDE.md` / `workflow.md` to point at new flow. Strip self-policed verbiage. | 1 day | Fifth |
| 6 | Live-test on a real step (Step 06 or 07 of 0002-auth-inbound). Iterate. | ongoing | Sixth |
| 7 | Backfill recipes for predicates that prove flaky in live tests. | ongoing | Seventh |

**Recommended approach**: implement the framework AFTER deliverable 0002 ships using the simplified non-tier workflow. Use Steps 05-08 as instrumented live data — track every cheating-temptation moment, every false claim, every "I should have caught that earlier." Then build the framework with that ground truth in hand.

**Alternative**: ship Phase 1 (`validate-schema.js` + `verify-evidence.js`) early, even within the current loose framework. It would catch fabricated PASS rows immediately. Defer Phase 2-7 until 0002 ships.

---

## 10. Open decisions

1. **TypeScript vs vanilla JS** — TS gives schema definitions for free (zod) + better DX, but adds build step. Vanilla JS is simpler. **Lean TS** given the rest of the Node side is TS.
2. **Sub-agent partitioning strategy for K Auditors** — by category index? by file extension? by predicate count for load-balancing? **Lean by category index, K=6** (each takes 4 categories on average).
3. **Tie-breaker mechanism** when 2 Auditors disagree on a row — spawn 3rd Auditor as tie-breaker, OR escalate to user. **Lean tie-breaker first, escalate if tie-breaker also conflicts.**
4. **Recipe authoring incentive** — should recipe authors get to declare effort scaling (e.g., "this recipe is cheap, run it on every file" vs "this recipe is expensive, sample 10%")? **Probably yes for performance.**
5. **Backwards compat with current journal markdown** — should the renderer produce markdown identical to current journal layout, or break compat with cleaner format? **Lean break compat, optimize for new layout.**
6. **What happens if a recipe itself has a bug** — false positive? false negative? Need a recipe-test suite (see `tests/recipes.test.js` above).
7. **What happens at FINAL-REVIEW** — same architecture, just scoped to entire deliverable instead of one step. K Auditors each get cumulative-deliverable categories. Aggregator combines into deliverable-final big table.

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
