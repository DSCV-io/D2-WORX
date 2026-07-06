<!--
Copyright (c) DCSV. All rights reserved.
-->

## 13. Permission / Action Discipline
<a name="top"></a>
_[← rules index](../rules.md) · §13 of the D2-WORX rules catalog._

Inferring permission from prior turns is a class of bug that compounds quickly. Each occurrence of a high-blast-radius action needs explicit fresh permission.

### Predicates — §13 permission / action discipline

- **13.1** Was any commit created during this scope without explicit user permission for THIS commit (not "go ahead" from earlier)?
  - Evidence: `git log` of commits in scope → cross-reference with user messages → confirm explicit ask + approval per commit.

- **13.2** Was any bulk file operation (sed across N files, mass rename, multi-file delete, bulk format-write) executed without first declaring scope (file count, glob, what changes) and giving the user the chance to redirect?
  - Evidence: per bulk op → journal entry with pre-execution scope statement.

- **13.3** Was any destructive git operation (force push, hard reset, branch delete, checkout that overwrites uncommitted work) used without explicit user authorization? Was `git stash` used by a sub-agent? (Sub-agents must NEVER use stash or other destructive git ops.)
  - Evidence: per destructive op → journal entry with authorization quote.

- **13.4** Was any planned work deferred / skipped without explicit user permission? (Default is to ASK, not unilaterally skip.)
  - Evidence: per planned-but-not-shipped item → journal entry with "asked, user said skip."

- **13.5** Was any architectural decision change made mid-execution without ASKING (when implementation surfaced a reason to deviate from the locked PLAN)?
  - Evidence: per deviation → journal entry with question + user response.

- **13.6** When the user gave feedback during REVIEW, was each item captured first and confirmed before fixing (not fixed-on-sight)?
  - Evidence: per review feedback → capture entry → user confirmation → fix.

- **13.7** Were ALL errors / warnings encountered anywhere in the project fixed (not just in branch-modified files)? (Zero-tolerance rule — never dismiss as "pre-existing.")
  - Evidence: per error/warning seen → fix or escalation note.

### Sub-agent discipline (when delegating work to spawned agents)

- **13.8** Did sub-agents avoid running tests during their work? (Tests run ONLY at the end from the main thread, after all sub-agent changes complete.)
  - **Why**: parallel agents running tests cause file-lock conflicts + spurious failures + wasted compute.
  - Evidence: per sub-agent prompt → "Do NOT run tests" instruction included; agent's tool history shows no test invocations.

- **13.9** Did sub-agents build only their specific project (`dotnet build ProjectName.csproj`), NOT the full solution?
  - **Why**: full-solution builds from parallel agents cause `obj/` file-lock conflicts and cascade failures across other agents' work.
  - Evidence: per sub-agent prompt → per-project build instruction; agent's tool history shows scoped builds only.

- **13.10** Were sub-agents launched with `run_in_background: true` so the main thread stays responsive to user communication?
  - **Why**: blocking the main thread on a long-running agent prevents the user from communicating, providing course corrections, or seeing progress. Real incident: hour-long block during CA1848 fix.
  - Evidence: per sub-agent launch → `run_in_background: true` confirmed.

- **13.11** Did sub-agents avoid ALL destructive git operations (`git stash`, `git checkout --`, `git restore`, `git clean`, `git reset`, etc.)?
  - **Why**: parallel agents using `git stash` nuked other agents' completed work. Real incident cost an hour of work.
  - Evidence: per sub-agent prompt → "Only use Read/Edit/Write tools, no git commands" instruction; agent's tool history confirms.

### Audit / sweep technique discipline

- **13.12** When sweeping docs for cross-cutting patterns (phase refs, V2 mentions, transitional framing, deprecated concepts, doc-cleanup tasks), was each file READ individually (via the Read tool) — NOT primarily discovered by Grep?
  - **Why**: grep matches exact patterns and misses oblique references ("see the phase 5 reference doc" without `.md`), section-style refs, prose mentions, and context that changes whether a match is problematic. User's words: "don't grep, manually look thru these docs - you keep missing shit."
  - **How**: enumerate files via Glob → Read each one individually (batch in parallel for speed) → use Grep ONLY as a final verification pass after manual reads, never as the source of truth for content audits.
  - Evidence: per content-sweep task → tool history shows Read calls per file, Grep only as verification.

- **13.13** When Implementation discovers that the Plan's hypothesis is WRONG (runtime / library / framework behavior differs from what the Plan claimed), did the Implementer (a) DOCUMENT the discovered behavior in the Implementation journal section under a "Plan-vs-reality reconciliation" subsection, (b) PIN the discovered behavior via a regression test (§2.1 cross-ref), AND (c) UPDATE the per-lib README + xmldoc + journal to reflect REALITY — never force-fit the implementation to the wrong Plan claim, never silently narrow the contract to "what works"?
  - Evidence: per Implementation that diverges from a Plan claim → journal "Plan-vs-reality reconciliation" subsection citing the Plan claim + the discovered reality + the test that pins the reality + the README / xmldoc lines updated to reflect it.
  - **Why**: silent narrowing ("the contract now says X because that's what the runtime does") is HONEST about behavior but DISHONEST about the discovery process — future readers gain the entire failure-mode-prevention value from an explicit reconciliation note. Force-fitting (making the implementation match the wrong Plan claim) is the extreme failure mode this prevents. Distinct from §13.5: §13.5 is whether to ASK before deviating; §13.13 is how to DOCUMENT the discovery once the deviation is in the code.
  - **How**: when a runtime behavior forces a Plan deviation, add a "Plan-vs-reality reconciliation" subsection: (1) Plan claimed: <quote>; (2) Reality: <discovered behavior + file:line>; (3) Test pinning reality: <test file:line>; (4) Docs updated: <README / xmldoc lines>. Required regardless of deviation size — undocumented small deviations compound into trust loss.
  - **Implementer-side reminder — §13.13 is HONEST DOCUMENTATION, NOT a substitute for §13.4 / §13.5 user-permission-before-deferral**: §13.13 applies when REALITY (runtime / framework / library / external API) diverges from the Plan claim. It does NOT apply when the Implementer's OWN scope-limits (brief constraints, task scope, time pressure) diverge from the Plan — that is §13.4 / §13.5, and BOTH require ASKING FIRST. Documenting a self-imposed narrowing as a §13.13 reconciliation after the fact is a process-integrity violation (it cannot retroactively grant permission §13.4 / §13.5 require in advance). **Test for which predicate applies**: did REALITY force the deviation (the API doesn't behave as claimed; the framework forbids the approach) → §13.13. Did the IMPLEMENTER decide to narrow scope (the helper port is out-of-scope for this step) → STOP, ASK per §13.4 / §13.5; only after the user authorizes do you document the §13.13 reconciliation.
  - **Examples of legitimate §13.13 use**: (a) Plan claimed `pnpm --filter X build` excludes broken sibling deps; runtime built the full transitive graph — Implementer pivoted to per-package invocation + documented the divergence + added a regression test. (b) Plan claimed per-lib READMEs fit ≤80 lines; reality needed 95-115 for honest public-API enumeration — Implementer adjusted the budget + documented it + cross-referenced §11.21's ceiling.
  - **Examples of MISUSE (these required §13.4 / §13.5 ASK first)**: (a) Plan called for bidirectional parity tests; the brief restricted .NET-source edits, so Implementer narrowed to forward-only and documented it as §13.13 — WRONG, the constraint was self-imposed (consulted later, the user's answer was scope EXPANSION: "extend the .NET emitter to consume the spec"). (b) Implementer judged the `Clean()` helper out-of-scope and skipped it without asking — WRONG, the user later authorized the port. In both the documentation was honest but came in lieu of asking — exactly the substitution this predicate forbids.

- **13.14** (Process-bypass requires explicit written naming of specific rules / steps being skipped) Was any process step / rule predicate skipped during this scope? If yes, does the journal cite the EXPLICIT user message authorizing the bypass — including the SPECIFIC rule §-number or step name being skipped? Implicit / inferred / "go ahead" / "looks good" authorization from earlier in conversation is NOT sufficient — every bypass requires per-occurrence user-quoted authorization naming the specific rule or step.
  - **Scope**: every process activity in this catalog + every workflow step — audit rounds (§24.0h K=1), Fixer dispatches, commits (§13.1), test writing (§2.1), journal entries (§24.x), per-step gates, final-review gates. Verbal `go ahead` / `looks good` / implicit prior consent does NOT authorize bypass.
  - **Required**: the orchestrator writes a proposed-bypass message enumerating (a) the specific rule §-number or step name being skipped, (b) why the bypass is justified for THIS scope, (c) what evidence-trail discipline is forfeited. The user responds with explicit authorization quoting the specific rule / step. Approvals do NOT carry forward — every bypass requires fresh per-occurrence user-quoted authorization.
  - **Acceptable bypass phrasings** (these qualify, per CLAUDE.md MANDATORY block 1): `skip the journal for this`, `no audit needed for this typo fix`, `just commit it directly`, `don't write a test for this`, `K=1 approved for this round`, `defer the X work for now`.
  - **NOT acceptable** (these do NOT qualify): `go ahead`, `looks good`, `keep going`, `that's fine`, silence after a prior bypass, `we agreed earlier`, `same as last time`.
  - **Evidence**: per bypass in any journal artifact → journal cites the explicit user message authorizing it (verbatim quote + identification of the specific rule / step). A bypass lacking explicit user-quoted authorization is a §13.14 violation and process-integrity-breaches the work — the orchestrator MUST stop, surface the gap, and either retroactively obtain authorization (acknowledging the breach) or re-run with the bypassed step included.
  - **Why**: silent process drift is the failure mode this framework exists to prevent. "We agreed earlier" is how rules erode — the agent rationalizes that prior approval covers the current case, the user misses the scope creep, and the bypass becomes a default. Empirical: deliverable 0008 R-final-V self-invoked K=1 rationalized as "Fixer changes are narrow + tamper-evident" — the self-justification pattern §24.0h forbids; §13.14 generalizes it across the catalog.
  - **How**: every audit / Fixer / commit / test-write / journal-edit / gate defaults to FULL compliance. If a bypass seems justified, ASK in writing and wait for the user to name the specific rules / steps; until that arrives, proceed with full process. When in doubt, ask again rather than assume.

### Deferral posture (do-it-now is the default)

- **13.15** (Requested / in-scope functionality is BUILT, not reflexively deferred) Was any functionality the user explicitly requested — or any in-scope, host-independent, doable-now work — proposed for deferral / de-scoping / "track it as a limitation" / "minimum-viable + follow-ups," when it was actually buildable now? The default posture is **do-it-now**: deliver what was asked, in full. Deferral is legitimate ONLY when the work is GENUINELY blocked — and even then it is SURFACED + ASKED (§13.4 / §13.14), never unilaterally tracked.
  - **The test for "genuinely-blocked"** is a **missing build dependency** — something that must exist before the work can be built AND proven in isolation: an unbuilt collaborator with no faithful §1.32 test-double, an undesigned decision whose outcome changes the work's shape, missing infrastructure, or a host/process needed for LIVE wiring. NOT build dependencies (never justify deferral): "no consumer yet", "not wired into the live host yet", "not exercised cross-process yet", "a fixture / tracker row / ADR labels it deferred", "the real config / domain values don't exist yet". Proving in isolation (Testcontainers + in-memory TestServer + §1.32 doubles) needs no live host and no real consumer — so if the work is in-scope and no build dependency is missing, BUILD AND PROVE IT NOW; waiting is how no-dependency work gets silently forgotten. A genuine blocker still gets a committed tracker row (never a comment- / journal-only TODO). YAGNI applies only to work that is NOT known-needed.
  - **Scope**: applies hardest to functionality the user EXPLICITLY requested (including an up-front "remember to support X"). For that, "track it" / "recommend deferring" / "out of scope for this step" / "known limitation" / "specified-deferred" is NOT an acceptable default — it carries the same user-permission burden as §13.4, and the agent must show the work is GENUINELY blocked (per the build-dependency test), not merely larger / cleaner / orthogonal / pre-existing.
  - **Why**: reflexive deferral of doable, requested work erodes momentum, accumulates silent debt, forces the user to re-prompt, and dumbs down the delivered scope. "Cleaner / safer / smaller / orthogonal / pre-existing to defer" is NOT valid when the work is buildable now. This is a recurring, training-baked reflex — in one session the agent proposed deferring client wiring, the nested-model gRPC transport-mapper, temporal zoned-types, and the OpenAPI emitter, all host-independent + doable and several explicitly requested ("stop deferring shit"). A second form: marking work "inert until a consumer" or "deferred to the live-wiring step" when it was fully provable in isolation — no missing build dependency, only the absence of a live caller.
  - **How**: before proposing ANY deferral / de-scope / track-as-limitation, run the build-dependency test — "Is a build dependency genuinely missing, or am I de-scoping because it's larger / cleaner / not wired to a live consumer yet?" No dependency missing → BUILD AND PROVE IT NOW. Dependency missing → surface + ASK (§13.4), name the blocker, and lean toward "do it all properly now" over "ship the lean version + follow-ons." For explicitly-requested functionality the bar to defer is USER PERMISSION, not agent judgment.
  - **Evidence**: per deferral / de-scope proposed → a specific missing build dependency named (not "cleaner / orthogonal / larger / no consumer yet / not wired live"); OR, absent one, the work was built and proven in isolation. A "tracked limitation" / "specified-deferred" row for explicitly-requested functionality without user-quoted authorization is a §13.15 violation (and compounds §13.4).
  - Related: §13.4 (never defer planned work without permission — §13.15 is the broader DEFAULT-POSTURE rule: do-it-now; deferral is the justified-as-blocked + asked exception); §13.5 (mid-execution deviation); §13.14 (bypass requires explicit user naming). Codifies the MEMORY.md `no-default-deferral` feedback.

<sup>[↑ jump to top](#top)</sup>

---

