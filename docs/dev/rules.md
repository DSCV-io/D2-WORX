<!--
Copyright (c) DCSV. All rights reserved.
-->

<a name="top"></a>

# D2-WORX Rules

The complete, authoritative requirements for ANY code change in this repository. **Read it end-to-end during the PLAN phase of every deliverable**, then use it as the audit checklist after every step (looped until a sweep returns zero findings) and again at final-review (scope = whole deliverable).

---

> ## ⚠️ MISSION CONTEXT — READ FIRST
>
> **D²-WORX ships as an enterprise-grade, production-ready SaaS framework** — not "works on my machine," not "good enough for now," not "harden it later." Code that ships MUST:
>
> - Survive bad / hostile / malformed / oversized input without crashing or leaking.
> - Survive infrastructure failure (DB down, broker unreachable, cache miss + downstream timeout, slow JWKS, network partition) gracefully — degrade, retry, circuit-break, or fail-closed; never silently swallow signal.
> - Never leak PII (user input, broker URIs, presigned URLs, file names, IPs, emails, addresses, message bodies) into logs, metrics, traces, or broker headers.
> - Survive concurrent access without races, double-fetches, deadlocks, or torn writes.
> - Be testable, observable, and maintainable by future engineers who weren't in the room.
> - Follow THIS codebase's established patterns, not generic training-data best practices.
>
> **If a predicate feels like overkill, that's the discipline working** — reading it costs minutes; skipping it costs a production incident, a security disclosure, or a multi-week rework. The user's value is design + architectural review; the agent's is work that doesn't need user-side bug-hunting.

<sup>[↑ jump to top](#top)</sup>

---

## How to use this doc

1. **PLAN phase** — read end-to-end. Understanding requirements upfront prevents architectural mistakes that cost rework.
2. **Pre-execute pass** — before each step's code, walk the categories: which predicates apply? Surface them in the step journal's "Pre-emptive gate checks" so the code passes the audit on round 1.
3. **Audit loop** — after writing code, walk every category, every predicate. Answer Y/N with evidence (grep results, `file:line` lists, "checked X by Y, found Z"). Vibes are not evidence. Fix findings in the same round; the next round runs post-fix. Loop until a round produces zero findings across every category. 10-iteration ceiling per scope; iteration 11 = escalate to user.
4. **Final-review** — same loop, scope = whole deliverable. Catches cross-step inconsistencies.

> **Verbose by design.** Every predicate exists because of a real past failure — reading the catalog costs minutes per round; skipping a predicate costs a future audit round (or a shipped bug). New predicates append at ship via the self-improvement loop ([process.md §5](process.md#5-self-improvement-loop)).

> **Companion docs**: [process.md](process.md) (loop protocol + sub-agent architecture + audit-loop mechanics), [deliverables/](deliverables/README.md) (past final reports + lessons), [../PATTERNS.md](../PATTERNS.md) (what each pattern IS — this doc enforces THAT they're followed).

## Table of contents

**Glossary**: [Project-specific terms used throughout this catalog](#glossary--project-specific-terms-used-throughout-this-catalog) — define-once reference (KEEP doc, big table, sweep, round, sister-sweep, K=12 + Aggregator, smart-constructor, Plan-Audit, behavioral interface, meta-doc, …). Read it first if a term-of-art isn't self-evident.

This catalog is split into one file per category under [`rules/`](rules/). Read the category files that apply to the work in front of you; read all of them end-to-end at PLAN. Each § below links to its file with a one-line scope. The per-§ anchor stubs further down keep every historical `rules.md#<section>` deep link resolving — each points onward to the moved file.

| § | Category | Scope |
| --- | --- | --- |
| 1 | [Test Discipline](rules/01-test-discipline.md) | Test every public path first pass; make tests adversarial. |
| 2 | [Bug-Fix Regression Testing](rules/02-bug-fix-regression-testing.md) | Every bug fix ships a regression test that fails without the fix. |
| 3 | [PII / Logging Safety](rules/03-pii-logging-safety.md) | Keep personal data and secrets out of logs, metrics, traces, headers. |
| 4 | [Concurrency / Race Conditions](rules/04-concurrency-race-conditions.md) | Survive concurrent access — no races, double-fetches, or torn writes. |
| 5 | [C# Code Conventions](rules/05-csharp-code-conventions.md) | Helpers, syntax, records, async, public API, build cleanliness, global usings. |
| 6 | [TypeScript / SvelteKit Code Conventions](rules/06-typescript-sveltekit-code-conventions.md) | Node and SvelteKit BFF conventions. |
| 7 | [Naming, File Headers, Folder Casing](rules/07-naming-file-headers-folder-casing.md) | Naming tables, file headers, folder casing, translation keys, git, style. |
| 8 | [Build & Tooling Hygiene](rules/08-build-tooling-hygiene.md) | Keep build and tooling clean; never start services by hand. |
| 9 | [Architectural Layer Hygiene](rules/09-architectural-layer-hygiene.md) | Layer boundaries, handler patterns, EF-as-DDD persistence, where auth checks belong. |
| 10 | [Security (Endpoints / Auth / Secrets / Input)](rules/10-security-endpoints-auth-secrets-input.md) | Endpoint, auth, secret-handling, input-validation rules. |
| 11 | [Documentation Parity & Best Practices](rules/11-documentation-parity-best-practices.md) | Docs in step with code in the same change; doc style, structure, brevity. |
| 12 | [i18n Discipline](rules/12-i18n-discipline.md) | Translation-key and localization rules across all locales. |
| 13 | [Permission / Action Discipline](rules/13-permission-action-discipline.md) | When to pause for the user; sub-agent, audit-technique, deferral posture. |
| 14 | [Phase / Audit / Conversation Verbiage Hygiene](rules/14-phase-audit-conversation-verbiage-hygiene.md) | Keep phase, audit-round, and conversation IDs out of shipped surfaces. |
| 15 | [Object Disposal & Resource Lifetime](rules/15-object-disposal-resource-lifetime.md) | Dispose resources correctly; own object lifetimes. |
| 16 | [OOTB Shared-Lib Tooling — Use What's There](rules/16-ootb-shared-lib-tooling-use-whats-there.md) | Reach for existing shared libraries before hand-rolling. |
| 17 | [D2Result Usage & Extensions](rules/17-d2result-usage-extensions.md) | Use the semantic D2Result factories; result-object control flow. |
| 18 | [Graceful Degradation & Failure Modes](rules/18-graceful-degradation-failure-modes.md) | Degrade, retry, circuit-break, or fail closed under failure. |
| 19 | [User Experience (UX)](rules/19-user-experience-ux.md) | User-facing behavior and copy rules. |
| 20 | [Developer Experience (DX)](rules/20-developer-experience-dx.md) | Make the code easy for the next engineer. |
| 21 | [Observability Completeness](rules/21-observability-completeness.md) | Complete metrics, traces, logs with the correct tags. |
| 22 | [Idempotency & Exactly-Once Semantics](rules/22-idempotency-exactly-once-semantics.md) | Safe retries and exactly-once handling. |
| 23 | [Configuration Hygiene](rules/23-configuration-hygiene.md) | Options-pattern config, indexed env vars, no manual env plumbing. |
| 24 | [Audit Evidence Discipline (meta — how to audit)](rules/24-audit-evidence-discipline-meta-how-to-audit.md) | Three-artifact journal, sweep/round lifecycle, closure-by-absence. |
| 25 | [Temporal Types (date / time / clock)](rules/25-temporal-types-date-time-clock.md) | NodaTime type selection, clock injection, DST, timestamps. |
| 26 | [Codegen Discipline (spec / proto / schema-derived types)](rules/26-codegen-discipline-spec-proto-schema-derived-types.md) | Never hand-edit generated files; spec-driven codegen, per-package versioning. |

**Index-local sections** (kept here, not split out):

- [Deliverable workflow chart — order of operations with loops](#deliverable-workflow-chart--order-of-operations-with-loops)
- [Deliverable completeness checklist (the gate before user review)](#deliverable-completeness-checklist-the-gate-before-user-review)
- [Loop count expectations](#loop-count-expectations)
- [Self-improvement loop](#self-improvement-loop)
- [Final reminder](#final-reminder)

<sup>[↑ jump to top](#top)</sup>

---

## Glossary — project-specific terms used throughout this catalog

Project-specific terms-of-art used across the predicates below. First-use in a predicate should be self-contained or link here; this is the single source of truth.

- **KEEP doc** — long-lived documentation shipped with the code, read by developers consuming/maintaining it. KEEP docs describe **current reality** (present tense, no forward-framing, no historical narration). Authoritative surface + NON-KEEP allowlist: §11 "Definition — 'KEEP doc'".
- **Big table** — the per-step / per-final-review evidence table under the journal's `## Latest sweep results` heading; one row per numbered subsection, replaced wholesale each sweep (never appended). See §24.0 / §24.0f.
- **Sweep** — one complete walk of every numbered subsection against current code by a fresh Auditor; produces a big table + appends a findings-log entry. See §24.0a / §24.0e.
- **Round** — one Auditor sweep + (if findings) one Fixer pass; numbered, each dispatching FRESH sub-agents. See §24.0e.
- **Findings log** — the journal's `## Sweep findings log (append-only)`; per-round `### Round N findings (timestamp)` subsections preserve every FINDING verbatim. Append-only — never deleted, re-ordered, or reclassified. See §24.0a / §24.0c.
- **Fix log** — the journal's `## Fix log (append-only)`; chronological per-fix entries citing subsection + finding round + what changed + `file.ext:NN` + timestamp. Append-only. See §24.0b / §24.0g.
- **Sister-sweep** — a Fixer's supplementary scan over the predicate's FULL applicability scope (NOT just the originating file's directory) to surface adjacent sister occurrences before handoff. See §24.13.3 / §24.13.3a-d.
- **Tamper-evident** — Fixer protocol: literal-quote the output BEFORE + AFTER the fix + `git diff --stat` BEFORE + AFTER, all four pasted into the fix-log entry. For previously-false-closed or user-emphasized findings. See §24.14.
- **K=12 + Aggregator** — the canonical audit-round dispatch: 12 parallel cluster Auditors (one §-range cluster each, per process.md §3) + 1 Aggregator (Fable per the [Sub-agent model policy per role](process.md#sub-agent-model-policy-per-role) table) merging the 12 partials into the big table. See §24.0h + §24.0i.
- **K=1 carve-out** — single-Auditor dispatch instead of K=12 + Aggregator; requires explicit per-round user permission per §13.14 / §24.0h. Never self-invoked by the orchestrator.
- **Cluster A1 / A2 / B1 / B2 / B3 / C1 / C2 / C3 / D1 / D2 / E1 / E2** — the 12-way partition of predicates for parallel Auditor dispatch; boundaries in process.md §3 "Auditor cluster partition (canonical K=12)". Used at §24.16 for per-cluster Plan-Audit verifications.
- **Smart-constructor** — domain-validation pattern `Domain.Create(input) → D2Result<Domain>` (returns a result rather than throwing); the handler calls `Create` at the top of `ExecuteAsync` and bubbles failure. See §9.4.
- **Plan-Audit** — the K=12 + Aggregator audit of a step's PLAN section BEFORE the Implementer is dispatched — catches design errors at the cheapest moment. See §24.16.
- **Plan-amender** — Fixer-analogous role scoped to editing the journal's `## Plan` section in response to Plan-Audit findings. See §24.16.
- **Meta-record** — small hand-coded type the source-gen pipeline uses to surface generated-catalog metadata to consumers (e.g., `SpecMetadata`, `EmitResult`); carved out from the §26.1 spec-mirror-DTO ban because its shape is NOT a spec mirror. See §26.1 "Allowed".
- **Behavioral interface** — interface defining API surface (methods consumers call) rather than data shape (fields a spec declares); NOT a §26.1 spec-mirror violation even alongside spec-derived data. See §26.1 "Allowed".
- **Source-gen destination assembly** — any csproj/package that ships to consumers (anything a consumer can `using`/`import`), distinct from a source-gen INTERNAL csproj (Roslyn analyzer, `IsRoslynComponent=true`) whose types never leak. The §26.1 ban applies to destination assemblies only; §26.2 carves out internals. See §26.1 / §26.2.
- **Meta-doc** — a doc that DIRECTS the work (process, predicates, orchestration) vs a KEEP doc that DESCRIBES the code. Canonical set: `docs/dev/rules.md`, `docs/dev/process.md`, `CLAUDE.md`, `.github/copilot-instructions.md`. Cross-refs between meta-docs (and to `docs/v2/`) are exempt from the §11.9 KEEP-doc citation ban. See §11.9 META-DOC ALLOWLIST + §14.1 meta-doc empirical-citation allowlist + §24.15.
- **PASS-borderline** — big-table status for a row that passes the literal check but the Auditor flags for orchestrator review (e.g., a defensible-but-worth-surfacing carve-out); counts as PASS for convergence; emoji prefix `🟡`. See §24.10.

<sup>[↑ jump to top](#top)</sup>

---

## 1. Test Discipline

Moved to [rules/01-test-discipline.md](rules/01-test-discipline.md).

## 2. Bug-Fix Regression Testing

Moved to [rules/02-bug-fix-regression-testing.md](rules/02-bug-fix-regression-testing.md).

## 3. PII / Logging Safety

Moved to [rules/03-pii-logging-safety.md](rules/03-pii-logging-safety.md).

## 4. Concurrency / Race Conditions

Moved to [rules/04-concurrency-race-conditions.md](rules/04-concurrency-race-conditions.md).

## 5. C# Code Conventions

Moved to [rules/05-csharp-code-conventions.md](rules/05-csharp-code-conventions.md).

## 6. TypeScript / SvelteKit Code Conventions

Moved to [rules/06-typescript-sveltekit-code-conventions.md](rules/06-typescript-sveltekit-code-conventions.md).

## 7. Naming, File Headers, Folder Casing

Moved to [rules/07-naming-file-headers-folder-casing.md](rules/07-naming-file-headers-folder-casing.md).

## 8. Build & Tooling Hygiene

Moved to [rules/08-build-tooling-hygiene.md](rules/08-build-tooling-hygiene.md).

## 9. Architectural Layer Hygiene

Moved to [rules/09-architectural-layer-hygiene.md](rules/09-architectural-layer-hygiene.md).

## 10. Security (Endpoints / Auth / Secrets / Input)

Moved to [rules/10-security-endpoints-auth-secrets-input.md](rules/10-security-endpoints-auth-secrets-input.md).

## 11. Documentation Parity & Best Practices

Moved to [rules/11-documentation-parity-best-practices.md](rules/11-documentation-parity-best-practices.md).

## 12. i18n Discipline

Moved to [rules/12-i18n-discipline.md](rules/12-i18n-discipline.md).

## 13. Permission / Action Discipline

Moved to [rules/13-permission-action-discipline.md](rules/13-permission-action-discipline.md).

## 14. Phase / Audit / Conversation Verbiage Hygiene

Moved to [rules/14-phase-audit-conversation-verbiage-hygiene.md](rules/14-phase-audit-conversation-verbiage-hygiene.md).

## 15. Object Disposal & Resource Lifetime

Moved to [rules/15-object-disposal-resource-lifetime.md](rules/15-object-disposal-resource-lifetime.md).

## 16. OOTB Shared-Lib Tooling — Use What's There

Moved to [rules/16-ootb-shared-lib-tooling-use-whats-there.md](rules/16-ootb-shared-lib-tooling-use-whats-there.md).

## 17. D2Result Usage & Extensions

Moved to [rules/17-d2result-usage-extensions.md](rules/17-d2result-usage-extensions.md).

## 18. Graceful Degradation & Failure Modes

Moved to [rules/18-graceful-degradation-failure-modes.md](rules/18-graceful-degradation-failure-modes.md).

## 19. User Experience (UX)

Moved to [rules/19-user-experience-ux.md](rules/19-user-experience-ux.md).

## 20. Developer Experience (DX)

Moved to [rules/20-developer-experience-dx.md](rules/20-developer-experience-dx.md).

## 21. Observability Completeness

Moved to [rules/21-observability-completeness.md](rules/21-observability-completeness.md).

## 22. Idempotency & Exactly-Once Semantics

Moved to [rules/22-idempotency-exactly-once-semantics.md](rules/22-idempotency-exactly-once-semantics.md).

## 23. Configuration Hygiene

Moved to [rules/23-configuration-hygiene.md](rules/23-configuration-hygiene.md).

## 24. Audit Evidence Discipline (meta — how to audit)

Moved to [rules/24-audit-evidence-discipline-meta-how-to-audit.md](rules/24-audit-evidence-discipline-meta-how-to-audit.md).

## 25. Temporal Types (date / time / clock)

Moved to [rules/25-temporal-types-date-time-clock.md](rules/25-temporal-types-date-time-clock.md).

## 26. Codegen Discipline (spec / proto / schema-derived types)

Moved to [rules/26-codegen-discipline-spec-proto-schema-derived-types.md](rules/26-codegen-discipline-spec-proto-schema-derived-types.md).

## Deliverable workflow chart — order of operations with loops

**Per-step audit scope includes every file the step touched (incl. files modified in prior steps), so cross-step drift is caught per-step — no separate tier-audit layer.**

```
PLAN — read rules.md end-to-end · lock cross-cutting decisions · create docs/wip/<deliverable>/ · plan steps + dependencies
→ EXECUTE, per step in prerequisite order:
    implement code + tests
    → SWEEP (fresh Auditor round) — walk rules.md against EVERY file the step created or modified;
      REPLACE the step journal's big table; APPEND "### Round N findings" to the findings log
    → findings in big table?
        yes → APPLY fixes (fresh Fixer) — per finding: edit code + APPEND fix-log entry (big table NOT touched)
              → re-sweep (loop until a sweep yields a zero-FINDING big table; 10-iteration ceiling, iteration 11 = escalate to user)
        no  → clean big table (closure by absence) → next step
→ FINAL-REVIEW — sweep the ENTIRE deliverable (own journal, same 3-artifact model; catches cross-cutting
  integration concerns no individual step would surface) → same fix/re-sweep loop until clean
→ SHIP — snapshot deliverable README to docs/dev/deliverables/NNNN-<name>.md · apply approved rule additions to rules.md
→ REVIEW — user reviews the shipped deliverable
```

### Deliverable completeness checklist (the gate before user review)

**Before declaring a deliverable "ready for REVIEW," walk this entire checklist. Every box must be a YES with a citation. If any box is NO, the deliverable is NOT ready — go finish the gap and re-walk the checklist.**

This META-checklist covers the whole deliverable's process integrity — distinct from the per-step rules.md walks. Walk it ONCE, immediately before presenting for user review.

#### Per-step gates (walk for EACH step in the deliverable)

For each step `NN-<step-name>` in `docs/wip/<deliverable>/`:

- [ ] **Journal exists** at `docs/wip/<deliverable>/<NN>-<step>/journal.md`?
- [ ] **Big table present** under `## Latest sweep results`, with one row per rules.md numbered subsection (~280 rows currently — the gate is one-row-per-subsection, not a fixed integer)?
- [ ] **Anti-laziness preamble** verbatim above the big table?
- [ ] **Big table has zero FINDING rows** (clean sweep)? If not, step is not done.
- [ ] **Every PASS row** carries a `file.cs:NN` citation (no "verified ✓", no "looks good")?
- [ ] **Every N/A row** carries a step-scope-specific reason (no bare "doesn't apply")?
- [ ] **Findings log** under `## Sweep findings log (append-only)` with at least one `### Round N findings (timestamp)` subsection per sweep that ran?
- [ ] **Fix log** under `## Fix log (append-only)` with chronological entries for every fix that landed?
- [ ] **For every FINDING in any round's findings log**, is there a corresponding fix-log entry (or explicit user-approved deferral entry)? No silent carryover.
- [ ] **Final round of sweep** in the findings log shows zero FINDINGs (closure proven by absence)?
- [ ] **Self-audit rows §24.0 through §24.16** (incl. §24.0a-i + §24.13.1-4 + §24.13.3a-d) present in the latest big table, each PASS-cited against the journal file itself?
- [ ] **Step's code change** has corresponding test coverage (per §1.x predicates)?
- [ ] **Build clean**: `dotnet build server/D2.slnx` zero StyleCop / CS warnings against current state?
- [ ] **JetBrains inspect clean**: `jb inspectcode server/D2.slnx --severity=WARNING` zero warnings?
- [ ] **Test suite passes** at the most recent test run citation in the journal?

#### Final-review gate (the deliverable-wide sweep)

- [ ] **Final-review journal exists** at `docs/wip/<deliverable>/final-review/journal.md`?
- [ ] **Final-review SWEEPS the ENTIRE deliverable** (every step's output, every modified shared lib, every modified doc)?
- [ ] **Final-review journal carries the same 3-artifact model** (big table + findings log + fix log)?
- [ ] **Final-review big table is clean** (zero FINDINGs)?
- [ ] **Final-review surfaces and records** any deliverable-wide consistency findings (e.g. PATTERNS.md / PARITY.md / TESTS.md drift, parent README update misses, Mermaid graph drift)?

#### Deliverable-wide doc gates

- [ ] **Root README** at `docs/wip/<deliverable>/README.md` updated with the final report (kinds-of-misses log, candidate rule additions, summary)?
- [ ] **Cross-cutting docs** updated per CLAUDE.md §3.5 Doc Update Map (PATTERNS.md / TESTS.md / PARITY.md / SRC_GEN.md as relevant)?
- [ ] **Per-lib / per-service READMEs** updated for new public APIs?
- [ ] **Parent `server/shared/dotnet/README.md`** updated for any new lib (status row + Mermaid graph + redundant-edges enumeration)?
- [ ] **Tracking doc** `docs/v2/PHASE_*.md` updated (or successor) with the deliverable's status?
- [ ] **No phase / sweep / audit verbiage** leaked into KEEP docs or source code (per §14.x)?
- [ ] **No conversation-scoped IDs** (Q-IDs, F#-IDs, R# refs) leaked into KEEP docs or source code?

#### Process-integrity gates

- [ ] **No commit was made** without explicit user permission per occurrence?
- [ ] **No bulk file ops** without scope declared first?
- [ ] **No destructive git ops** without explicit authorization?
- [ ] **No deferred work** without user permission (every deferral has a fix-log entry referencing user approval)?
- [ ] **No mid-execution architectural deviation** from the locked PLAN without ASK?

#### Final attestation (the agent writes this in the deliverable's root README before user review)

> "I attest that this deliverable's process integrity has been verified against the deliverable completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is YES. The deliverable is ready for user REVIEW."
>
> Followed by per-step / final-review journal links so the user can spot-check.

**If the agent cannot honestly attest every box as YES, the deliverable is NOT ready. Go fix the gap, re-walk the checklist, and only present for user review when every box is honestly YES.**

---

### Loop count expectations

- WELL-PLANNED step: typically converges in 1-3 sweep rounds.
- POORLY-PLANNED step (or one introducing complex new patterns): 5-8 rounds.
- 10-iteration ceiling per step (process.md §4). Iteration 11 = escalate to user — something is structurally wrong.
- Final-review: 0-2 deliverable-wide consistency findings, typically 1-2 sweep rounds.

### Worked example (Step 1 of a hypothetical deliverable)

Step 1 implements a new `FooHandler`:

1. Code + tests written.
2. **Sweep round 1** walks rules.md → REPLACES the big table in `01-foo-handler/journal.md`; appends `### Round 1 findings (2026-05-10 14:00)` with 5 FINDINGs (1H + 3M + 1L).
3. Fix work: per FINDING, edit code + APPEND one `## Fix log` line (e.g. `- 2026-05-10 14:15 §3.1 (R1): SanitizedExceptionRender used in FooHandler.cs:42 to replace Exception param`).
4. **Sweep round 2** REPLACES the big table; appends `### Round 2 findings` (1 LOW cascaded from the R1 §3.1 fix; the 5 R1 findings are now PASS = closed by absence).
5. Fix the R2 LOW; append a fix-log entry.
6. **Sweep round 3**: big table has zero FINDINGs → Step 1 is done.

The journal then shows: (a) latest state (R3 clean big table), (b) what each round found (findings log R1 + R2 + R3), (c) what changed (fix log R1 + R2), (d) closure proven by absence in the next sweep.

<sup>[↑ jump to top](#top)</sup>

---

## Self-improvement loop

This catalog grows. Per [process.md §5 Self-improvement loop](process.md#5-self-improvement-loop), every deliverable's distillation proposes predicate additions; approved ones land here. Over time it approaches "every kind of miss we've ever made has a gate-check," and audit loops converge faster because predicates fire pre-emptively during PLAN's pre-emptive gate checks.

### Format for proposing a new predicate

In the deliverable's root README "Proposed rule additions to rules.md" section:

```
Category: <existing category number + name, or "NEW: <name>">
Predicate: <Y/N question with required evidence>
Origin:    <which deliverable / step / round surfaced the underlying miss>
Why permanent: <not a one-off; class of miss that will recur without a gate-check>
Examples:  <1-2 specific past instances>
```

User approves / tweaks / rejects per proposal; approved ones append to this doc as part of ship's commit batch. Rejected proposals (one-off mistakes not worth a permanent rule) get noted in the deliverable's final report so the reasoning survives.

<sup>[↑ jump to top](#top)</sup>

---

## Final reminder

**This catalog exists because D²-WORX ships to production with real users, real money, and real consequences for failure.** The verbose discipline is the cost of robustness; the alternative is shipping bugs and burning trust. When in doubt about a predicate, default to applying it — reading costs minutes, skipping costs incidents.
