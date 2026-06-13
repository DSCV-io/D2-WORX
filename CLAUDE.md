<!--
Copyright (c) DCSV. All rights reserved.
-->

# CLAUDE.md — D²-WORX Development Guide

**D²-WORX** — Microservices SaaS framework. C# 14 / .NET 10 backend, SvelteKit BFF (TypeScript 5.9 / Svelte 5). Pre-Alpha. PolyForm Strict license (reference implementation, non-commercial).

> # ⚠️⚠️⚠️ READ FIRST ⚠️⚠️⚠️
>
> **Before planning, implementing, or auditing ANYTHING — read [docs/dev/rules.md](docs/dev/rules.md) + [docs/dev/process.md](docs/dev/process.md) first.** If your work conflicts with them, you're making a mistake **UNLESS the user explicitly acknowledges in writing the SPECIFIC rules / steps being bypassed** (per [rules.md §13.14](docs/dev/rules.md#13-permission--action-discipline) — Process-bypass requires explicit written naming).
>
> rules.md is the canonical predicate catalog. process.md is the canonical workflow + sub-agent architecture + audit-loop protocol. CLAUDE.md condenses both for fast-access mental-model purposes per [rules.md §11.32](docs/dev/rules.md#11-documentation-parity--best-practices) — update CLAUDE.md + rules.md + process.md in lockstep when either canonical doc changes.

> This doc covers process, patterns, and code rules. Architectural decisions live distributed across the docs in `docs/` (PATTERNS.md, TESTS.md, PARITY.md, SRC_GEN.md, etc.) and the per-lib / per-service `README.md` files.

> **📍 PROJECT STATE — READ FIRST**: The active project tracking doc is **[docs/v2/V2.md](docs/v2/V2.md)** (current scope + per-phase tracking docs). This pointer is the single source for "what's the project doing right now" — when this doc archives, the pointer here gets updated to its successor. Archived: [docs/archive/PHASE_0_WIPE.md](docs/archive/PHASE_0_WIPE.md), [docs/archive/PHASE_1_GEO_LIBS.md](docs/archive/PHASE_1_GEO_LIBS.md). A frozen v1 snapshot lives at `/old/v1/D2-WORX/` (read-only, reference for historical patterns not yet captured in current docs).

<a name="mandatory-block-0-orchestrator-only-main-thread"></a>

> # ⚠️⚠️⚠️ MANDATORY — ORCHESTRATOR-ONLY MAIN THREAD ⚠️⚠️⚠️
>
> **The main thread ORCHESTRATES — it does NOT plan, implement, audit, or fix. EVERY round of plan / impl / audit / fix is a FRESH sub-agent spawned via `Agent`. A second audit round = brand-new K=12 Auditor batch + brand-new Aggregator. A follow-up fix = brand-new Fixer. The fresh-context property is the entire point.**
>
> **Why fresh context matters**: sub-agents that re-audit their own prior work develop leniency (they're motivated to declare convergence) and carry stale assumptions forward; a brand-new Auditor walks the catalog from zero with no investment in prior conclusions. Empirically validated in the 0002-auth-inbound trial — fresh-Auditor rounds caught two production bugs that the same-context loop missed.
>
> **Roles (model per [rules.md §24.0i](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit))**: Planner / Plan-amender / Aggregator / Orchestrator → **Opus**. Implementer / Auditor / Plan-Auditor / Fixer / Final-reviewer / Investigator → **Sonnet**. Sweeping Implementer / Fixer Opus carve-out (atomic >40-file, >3-concern, cross-runtime, cascading pipeline) — cite criterion in dispatch brief. Auditor-shape Opus escalation requires explicit per-occurrence user approval.
>
> **K=12 parallel-cluster dispatch is the default per audit round.** K=1 (single Auditor doubles as Aggregator) requires **explicit per-round user permission** per [rules.md §24.0h](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit) — never self-invoked.
>
> **Plan currency before dispatch**: any EXECUTE decision that amends the Plan MUST be written into the Plan artifacts (journal + Plan file Living State + Cross-cutting decisions table) in the SAME orchestrator turn — BEFORE the next sub-agent is dispatched. Sub-agents have no conversation context. Mechanism → [process.md §4 Plan currency](docs/dev/process.md#plan-currency-before-dispatch); enforcement → [rules.md §24.17](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit).
>
> **If you find yourself about to `Edit` a source file from the main thread, STOP. Spawn an Implementer with the change spec. About to walk `rules.md`, STOP — spawn an Auditor. About to read a journal to "check progress," STOP — spawn a sub-agent to summarize and report. The discipline is structural, not optional.** A one-line typo fix still spawns Planner → Implementer → Auditor → (Fixer if findings). The ONLY bypass is an explicit user request naming the specific rule / step being skipped (per [rules.md §13.14](docs/dev/rules.md#13-permission--action-discipline)).
>
> Full role table + tool-access lists + empirical justification + cluster partition + Aggregator spec → [process.md §3](docs/dev/process.md#3-sub-agent-architecture) + [§4 Audit-loop mechanics](docs/dev/process.md#4-audit-loop-mechanics).
>
> _Canonical form (with empirical justification + full role table): [process.md §3 Sub-agent architecture](docs/dev/process.md#3-sub-agent-architecture). Update both in lockstep when either changes (per §11.32 KEEP-doc duplication discipline)._

---

<a name="mandatory-block-1-every-code-change"></a>

> # ⚠️⚠️⚠️ MANDATORY — APPLIES TO EVERY CODE CHANGE ⚠️⚠️⚠️
>
> **EVERY code change — including "just change this one line", "rename this var", "fix this typo", "add this property", "tweak this config" — follows the Development Workflow (§1, detailed in [docs/dev/process.md](docs/dev/process.md)) AND adheres to every applicable predicate in [docs/dev/rules.md](docs/dev/rules.md).**
>
> **There is no "small change" carve-out.** A one-line edit IS a deliverable: it goes through PLAN (read rules.md, identify what categories apply, write a plan entry), EXECUTE (write the code, write the test, walk the audit loop until clean), and REVIEW (present to user for sign-off — DO NOT auto-commit). Skipping any of these because "it's just a small thing" is the failure mode this whole framework exists to prevent — small changes that bypass the process are how regressions ship, how PII leaks slip in, how docs drift, how conventions slip, how production breaks.
>
> **The ONLY way to bypass any part of this process is an explicit user request like "skip the journal for this", "no audit needed for this typo fix", "just commit it directly", or "don't write a test for this." Without that explicit bypass instruction, the process applies in full.**
>
> If a request seems too small to deserve the full process, ask the user: "this is a one-line change — should I do the full process or do you want to bypass [specific step]?" Default = full process.
>
> _Canonical form (full permission-discipline predicate catalog): [rules.md §13 Permission / Action Discipline](docs/dev/rules.md#13-permission--action-discipline) + [process.md](docs/dev/process.md). Update both in lockstep when either changes (per §11.32 KEEP-doc duplication discipline)._

---

<a name="mandatory-block-2-audit-evidence--proof-discipline"></a>

> # ⚠️⚠️⚠️ MANDATORY — AUDIT EVIDENCE & PROOF DISCIPLINE ⚠️⚠️⚠️
>
> **EVERY audit round MUST embed a complete markdown evidence table in the journal (`docs/wip/<deliverable>/<NN>-<step>/journal.md`).** Prose-only journals = audit INCOMPLETE = step NOT done. The whole framework exists so the user can OPEN A JOURNAL FILE and SEE the evidence directly.
>
> ## ⚠️ THREE ARTIFACTS, NEVER COLLAPSED
>
> 1. **Big table** (`## Latest sweep results`) — REPLACED each sweep, one row per rules.md subsection, status `✅ PASS` / `⚪ N/A` / `❌ FINDING-{H/M/L}` / `🟡 *` with file:line + reason/description+fix.
> 2. **Findings log** (`## Sweep findings log (append-only)`) — APPEND-ONLY `### Round N findings` subsections; never deleted / reclassified.
> 3. **Fix log** (`## Fix log (append-only)`) — APPEND-ONLY 5-field entries (rules.md §, finding round, what changed, file:line, timestamp/commit). **NEVER touches the big table.**
>
> **Closure is proven by ABSENCE of a finding from the next sweep's big table — not by a fix-log entry claiming "fixed."**
>
> **Why this shape**: the artifacts are tamper-evident BY DESIGN. The Fixer can't "fix" a finding by editing the big table (their claim has to survive a fresh Auditor walking the catalog from zero); the append-only logs prevent silent reclassification of past findings; closure-by-absence prevents motivated "fixed" claims that the next sweep would surface as STILL_PRESENT.
>
> ## ⚠️ ROUND SEQUENCE (per [rules.md §24.0a-f](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit))
>
> 1. **Sweep** → REPLACE big table; APPEND `### Round N findings` to findings log.
> 2. **Fix work** → per finding, apply fix + APPEND fix-log entry (5 fields). Big table untouched. Cross-cutting fixes run sister-sweep over full scope (§24.13). User-flagged or STILL_PRESENT findings require tamper-evident BEFORE/AFTER + `git diff --stat` (§24.14).
> 3. **EVERY finding gets fixed.** No silent carryover. Deferral requires explicit user permission (§13.4 / §13.14) + deferral entry in fix log.
> 4. **Next sweep** with BRAND-NEW Auditor — walk full catalog from scratch, REPLACE big table, append `### Round N+1`. PASS in Round N+1 = closed. STILL FINDING = re-fix.
> 5. **Loop terminates** when ONE sweep produces zero-FINDING big table. No "convergence claimed" without a clean real sweep.
>
> **Detailed protocol → [process.md §4](docs/dev/process.md#4-audit-loop-mechanics). Predicates → [rules.md §24](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit) (§24.0 artifacts, §24.0a-h lifecycle, §24.10 status, §24.12 self-audit, §24.13 sister-sweep, §24.14 tamper-evident). Visual flow → [Deliverable workflow chart](docs/dev/rules.md#deliverable-workflow-chart--order-of-operations-with-loops).**
>
> **If you find yourself about to claim "audit complete" without the 3-artifact model satisfied, STOP. Walk the model. Embed the artifacts. Then claim convergence.**
>
> _Canonical form (full §24 predicate catalog with Evidence + Why + How blocks per predicate): [rules.md §24 Audit Evidence Discipline](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit). Update both in lockstep when either changes (per §11.32 KEEP-doc duplication discipline)._

---

<a name="mandatory-block-3-deliverable-completeness-checklist"></a>

> # ⚠️⚠️⚠️ DELIVERABLE COMPLETENESS CHECKLIST — THE GATE BEFORE USER REVIEW ⚠️⚠️⚠️
>
> **Before declaring ANY deliverable "ready for REVIEW," walk the [Deliverable completeness checklist in rules.md](docs/dev/rules.md#deliverable-completeness-checklist-the-gate-before-user-review). Every box must be a YES with a citation. If any box is NO, the deliverable is NOT ready — go finish the gap and re-walk the checklist.**
>
> **Final attestation**: before presenting for user REVIEW, write the attestation block from the checklist into the deliverable's root README (verbatim wording in rules.md). The attestation is YOUR signed claim that every box is honest YES — invalidating it is a process-integrity breach.
>
> **What the checklist covers**: per-step audit-loop convergence, cross-cutting verification (parity tests, doc parity, generated-file regeneration, observability completeness), and final-review sweep convergence. Walked once per deliverable immediately before declaring REVIEW-ready. A signed YES that turns out to be NO is a process-integrity breach under §24.0 tamper-evidence — the attestation isn't ceremonial, it's the gate.
>
> **You MUST walk this checklist immediately before declaring any deliverable ready for user review. No exceptions, no "I'll skip it just this once," no "the steps were clean so the whole deliverable must be clean." Walk every box, write the attestation, then present.**
>
> _Canonical form (full checklist with per-step / final-review / cross-cutting / process-integrity gates + verbatim attestation wording): [rules.md Deliverable completeness checklist](docs/dev/rules.md#deliverable-completeness-checklist-the-gate-before-user-review). Update both in lockstep when either changes (per §11.32 KEEP-doc duplication discipline)._

---

## §1. Development Workflow

The agent reaches alignment with the user during PLAN, then **the main-thread orchestrator drives EXECUTE by spawning fresh sub-agents** for every round of planning, implementation, auditing, and fixing (per-step audit loop with 10-iteration ceiling, append-only journal capturing every round) and FINAL-REVIEW (same loop scoped to whole deliverable), then hands off to REVIEW. The user's value lives in PLAN (design decisions) and REVIEW (architectural feedback) — not in pushing the agent through audit rounds.

**Detailed protocol → [docs/dev/process.md](docs/dev/process.md).**
**Orchestrator + sub-agent architecture → [docs/dev/process.md §3 Sub-agent architecture](docs/dev/process.md#3-sub-agent-architecture).**
**Verbose audit rule catalog walked each round → [docs/dev/rules.md](docs/dev/rules.md).**
**Past deliverables' final reports + lessons → [docs/dev/deliverables/](docs/dev/deliverables/README.md).**

### Phase summary

- **PLAN** — Discuss with user. Lock high-level goal + step breakdown (one csproj or equivalent per step) + cross-cutting decisions + risk analysis. Output: `docs/wip/<deliverable>/README.md` (gitignored workspace) populated with step list, decisions, prerequisites.
- **EXECUTE** — Per step, in prerequisite order, the **main-thread orchestrator** spawns fresh sub-agents for each phase (per the orchestrator-only main-thread block above):
  1. **Spawn Planner sub-agent** — given step description + applicable rules.md categories + relevant docs to read; appends Plan section to `docs/wip/<deliverable>/<NN>-<step>/journal.md` including pre-emptive gate checks (test coverage plan, convention check, PII check, layer check) to push catches BEFORE writing code, not after.
  2. **Spawn Implementer sub-agent** — given the journal Plan + applicable rules.md categories; writes code + tests; returns files-touched + build/inspectcode status.
  3. **Audit loop**: fresh K=12 Auditor batch + Aggregator each round → fresh Fixer per finding → next round = brand-new sub-agents. Terminate on zero-FINDING sweep. 10-iter ceiling. Each Auditor produces the 3-artifact journal — **big table** (one row per rules.md subsection, status `✅ PASS` / `⚪ N/A` / `❌ FINDING-{H/M/L}` with file:line citation + reason/description+fix), **findings log** (append-only per-round subsection), **fix log** (5-field per fix). PASS rows require file:line; N/A rows require step-scope-specific reason; FINDING rows require severity + file:line + description + fix. Mechanics → [process.md §4](docs/dev/process.md#4-audit-loop-mechanics) / [rules.md §24](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit). Anti-laziness preamble enforced inside Auditor dispatch briefs.
  4. Per-step distillation → root README's kinds-of-misses log + candidate predicate additions for `rules.md` (orchestrator may delegate distillation to a sub-agent if the journal is large).
- **FINAL-REVIEW** — Same orchestrator-driven audit loop, scope = whole deliverable. Fresh Final-reviewer sub-agent(s) per round. Catches integration / cross-step / consistency bugs.
- **SHIP** — Aggregate proposed rule additions FROM the per-step + final-review journals (they MUST still be readable at this point — they're the evidence behind every proposed rule). Present root README to user. Apply approved rules to `rules.md`. **Copy the root README as a snapshot** from `docs/wip/NNNN-<name>/README.md` to `docs/dev/deliverables/NNNN-<name>.md` (committed — single file; 4-digit index prefix so deliverables sort naturally in ship order). The per-step journals stay where they are in the gitignored `docs/wip/NNNN-<name>/` workspace — local-only artifacts that the workflow NEVER auto-deletes. User removes them manually whenever they want.
- **REVIEW** — User reviews shipped deliverable. Feedback is captured-and-confirmed first, NOT fixed-on-sight. Bugs that the audit should have caught become new predicates (self-improvement loop).

### Permission gates (must block — no inferred permission)

Per [process.md §2 Permission gates](docs/dev/process.md#2-permission-gates-when-to-pause-for-the-user):

- Commit creation — explicit user permission per occurrence
- Bulk file operations — declare scope before executing; user has chance to redirect
- Destructive git operations (force push, hard reset, branch delete) — explicit authorization
- Deferring planned work — ASK, not unilaterally skip
- Architectural decision changes mid-execution — ASK before deviating from locked PLAN

### Self-improvement (the key insight)

Every deliverable's distillation surfaces classes of miss. Approved misses become permanent predicates in `rules.md`. Future deliverables start with a stricter ruleset → audit loops converge in fewer rounds → deliverables ship faster → user spends less time pushing the agent through audit cycles.

The journal IS the evidence of process integrity. Honest journals are self-rewarding: every honest miss becomes a future gate-check.

---

## §2. Commands

> ⚠️ **DO NOT START SERVICES MANUALLY** — Never run `dotnet run`, `pnpm dev`, `pnpm preview`, or any long-running server directly. Services are managed by Docker Compose.
> E2E tests that self-manage their infrastructure (Testcontainers, child processes with cleanup) ARE allowed — they start and stop their own services.

**Most-cited commands** (full catalog — Docker Compose lifecycle, single-project builds, test filters, lint, versioning — in [docs/COMMANDS.md](docs/COMMANDS.md)):

- **Build full .NET solution**: `dotnet build server/D2.slnx` — must be zero warnings (StyleCop / CS / null-ref).
- **JetBrains inspections**: `jb inspectcode server/D2.slnx --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log` — must be zero warnings. Catches issues `dotnet build` does NOT (e.g. `[MustDisposeResource]`, captured-closure issues).
- **Run .NET tests**: `dotnet test server/D2.slnx` (full) or `dotnet test server/D2.slnx --filter Category=Unit` (unit only).
- **SvelteKit type check**: `cd server/web && pnpm exec svelte-check`.

---

<a name="3-reference-documents"></a><a name="35-doc-update-map"></a>

## §3. Reference Documents + Doc Update Map

One table, two axes: **When to read** before touching the area, and **When to update** after touching the area. If your change spans multiple rows, update each. If no row fits, the change probably needs a new doc — ASK before creating one.

| Document                                                  | When to read                                                      | When to update (what triggers an edit)                                                                                                                                                                                                                |
| --------------------------------------------------------- | ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [docs/dev/process.md](docs/dev/process.md)                | Before starting ANY deliverable                                   | Workflow / sub-agent architecture / audit-loop mechanics / Plan-Audit changes (paired with [rules.md §24](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit) if predicate changes)                                                    |
| [docs/dev/rules.md](docs/dev/rules.md)                    | Read end-to-end at PLAN; walked each audit round                  | New predicate or category change (and codegen-discipline row also touches [§26](docs/dev/rules.md#26-codegen-discipline-spec--proto--schema-derived-types))                                                                                            |
| [docs/dev/deliverables/](docs/dev/deliverables/README.md) | When researching a past deliverable's outcome                     | At SHIP — the deliverable's root README snapshot lands here                                                                                                                                                                                           |
| [docs/COMMANDS.md](docs/COMMANDS.md)                      | When you need the build / test / lint / versioning commands       | When a command, flag, or service-lifecycle invocation changes                                                                                                                                                                                         |
| [CONTRIBUTING.md](CONTRIBUTING.md)                        | PR preparation                                                    | Branch / commit / PR convention changes                                                                                                                                                                                                              |
| [docs/PATTERNS.md](docs/PATTERNS.md)                      | Any handler / DI / repo / cache / middleware / service-structure work | A handler / service-structure / DI registration / `D2Result` factory usage / RedactionSpec / mapper / repo pattern changes                                                                                                                          |
| [docs/TESTS.md](docs/TESTS.md)                            | Adding or modifying tests                                         | Test category, custom matcher, adversarial-coverage rule, fixture pattern changes                                                                                                                                                                     |
| [docs/PARITY.md](docs/PARITY.md)                          | Adding cross-language components                                  | Anything cross-language (.NET ↔ SvelteKit ↔ future)                                                                                                                                                                                                   |
| [docs/SRC_GEN.md](docs/SRC_GEN.md)                        | Any source-gen / spec-driven codegen work                         | Adding a new generator, modifying spec format, new emitter                                                                                                                                                                                            |
| [docs/TIMESTAMPS.md](docs/TIMESTAMPS.md)                  | Any timestamp / temporal handling work                            | Timestamp categories, NodaTime type selection, DST rules, PostgreSQL column mapping, wire `DateTimeOffset?` conversion changes                                                                                                                       |
| [ADRs](docs/adrs/README.md)                               | When researching an architectural decision or proposing a new one | An architectural decision overrides a prior v2 plan (paired with V2.md tracking entry)                                                                                                                                                                |
| [ADR-0020 (service-project structure)](docs/adrs/0020-service-project-structure.md) | Any service-project layout work — where domain/app/infra/api/tests live, handler folders, mappers, concern folders, the dependency law | Service-structure convention changes — layer set, the two-section app split, per-op folders, the Commands/Queries rule, the five-surface mapper rule, concern vocabulary, global-usings policy (paired with PATTERNS.md + rules.md §5/§7/§9 + this §4) |
| [server/shared/dotnet/messaging/rabbitmq/README.md](server/shared/dotnet/messaging/rabbitmq/README.md) | Any async messaging work                                          | Async messaging — wire format, exchange / queue topology, AMQP headers, encryption frame, DLQ behavior changes                                                                                                                                       |
| [docs/v2/PHASE_3_EDGE.md](docs/v2/PHASE_3_EDGE.md)        | Any Edge service operational-guarantee work                       | Edge service operational guarantees — HTTP idempotency, request enrichment, session 3-tier, scheduled-jobs receiver, multi-instance scaling changes                                                                                                  |
| [docs/v2/PHASE_3_RATE_LIMITING.md](docs/v2/PHASE_3_RATE_LIMITING.md) | Any rate-limit middleware work                                    | Rate-limit middleware design / bucket math / kill-switch / FP-too-common detection / cookie shortcut changes                                                                                                                                          |
| [docs/v2/PHASE_0_AUTH.md](docs/v2/PHASE_0_AUTH.md)        | Any KeyCustodian / key rotation / secret handling work            | KeyCustodian, key rotation, secret handling, compromise runbook changes                                                                                                                                                                              |
| **Active project tracking doc** (see header — currently [docs/v2/V2.md](docs/v2/V2.md)) | Before starting any task                                          | Phase progression / wipe state / open phase questions / new tracked issue; architectural decisions that override prior v2 plan (also add an ADR entry)                                                                                               |
| Per-lib / per-service `README.md`                         | When working in that lib / service                                | Add/modify a public API on a lib or service                                                                                                                                                                                                          |

Per-service / per-library `README.md` files appear in `server/services/{service}/` and `server/shared/dotnet/{lib}/`.

---

## §4. Patterns & Architecture

**Rule: Follow existing patterns. Do not invent new ones when established patterns apply. If no pattern fits, ASK before inventing. Behavioral Guidelines (§7) apply to ALL work in this section — especially: ask when uncertain, research first, follow existing conventions.**

**Patterns are documented in detail in [docs/PATTERNS.md](docs/PATTERNS.md). This section summarizes the operational rules every D² engineer needs daily.**

### Service project structure

Every service under `server/services/` takes one fixed layered shape — canonical: [docs/adrs/0020-service-project-structure.md](docs/adrs/0020-service-project-structure.md); daily-driver: [docs/PATTERNS.md "Service project structure"](docs/PATTERNS.md#service-project-structure).

**Five projects + the dependency law.** A standalone service is `domain/` + `app/` + `infra/` + `api/` + `tests/` (+ consumer-facing `clients/` + a `netstandard2.0` source-gen shell when it owns error codes). Layers depend in one direction:

```
Domain  ←  App  ←  Infra  ←  Api      (Tests reference what they test; Clients reference contracts + shared libs only)
```

Domain references shared primitives only (NO EF / Options / DI / logging / vendor SDK). App declares ports + shapes, is transport-agnostic. Infra is the only vendor-SDK-touching layer. **Api is the only project allowed to reference Infra.** A **module-within-host** (KeyCustodian, the auth module — both inside Edge) takes `domain`/`app`/`infra` but OMITS `api/` and its own `tests/` (the host's api is its composition root + transport mapper; its tests live in the host's test project under a `<Module>/` subtree).

**Domain** = `Entities/` + `ValueObjects/` + `Enums/` + `Rules/` (pure no-port no-IO logic — generators, verifiers, projections; a tunable is a method param, not `IOptions<>`). Pure logic lives in domain `Rules/`, NOT in app handlers.

**App = two sections.** `app/Application/` (per-op handlers + `Observability/` + `AddD2<Service>App()`) and `app/Infrastructure/` (ports + shapes by concern + `Configuration/`). The `infra/` project mirrors `app/Infrastructure/`'s concern folders with the adapters.

**Per-operation handler folders.** One folder per op under its category: `Application/Handlers/{Commands,Queries}/<Op>/{I<Op>Handler.cs, <Op>Handler.cs, <Op>Input.cs, <Op>Output.cs}`. File name = type name; input always `<Op>Input`, output always `<Op>Output`. NO `Models/` bucket — a DTO co-locates with its op or promotes to a domain VO. NO `Interfaces/`/`Implementations/` mirror — co-locate the interface with its impl.

**Commands vs Queries — the binary side-effect rule.** Category is determined SOLELY by whether the op mutates persistent/shared state (DB write / distributed-cache write / external write / message publish). Side effect → `Commands/`; none → `Queries/`. **The verb is irrelevant** — a `Find…`/`Get…` op that writes is a `Command`. No "Complex" tier. Local/in-memory caching does NOT make a Query a Command.

**Concern folders + mandatory vendor subfolders.** Capability-noun concern folders (`Persistence`, `Messaging`, `Email`, `Vault`, …); every `infra/` concern carries a tech/vendor/protocol subfolder EVEN for a sole impl (`infra/Persistence/Postgres/`, `infra/Email/Resend/`). The generic `Providers/` wrapper is dead. Namespaces keep the `.App`/`.Infra` layer segment verbatim.

**Mappers — the uppermost-node rule (one home per surface).** Transport (proto/REST ↔ `<Op>Input`/`<Op>Output`) → `api/Mappers/`; persistence (record ↔ aggregate) → `app/Infrastructure/Persistence/`; provider-SDK ↔ domain → `infra/<Concern>/<Vendor>/`; messaging-wire ↔ domain → `infra/Messaging/<Broker>/`; primitives → domain VO `Create` factories. All pure C# 14 extension members.

### Multi-provider — keyed resolver

App stays vendor-blind (ONE capability port per concern); infra registers keyed adapters (.NET keyed DI, one per vendor subfolder); a runtime-selected vendor uses `I<Capability>Resolver.Get(key) → D2Result<T>` (typed unknown-key failure, not a thrown exception). For messaging the resolver layers on top of `[MqPub]`.

### Verb Semantics

- **Find** = "Resolve this for me" — may fetch from external source, may cache/persist. Example: `FindWhoIs`
- **Get** = "Give me this by ID" — direct lookup, read-only. Example: `GetWhoIsByIds`

### Handler Pattern

`.NET`: `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. Per-handler PII redaction via the `[RedactData]` attribute on data types (KEEP) + `DefaultOptions` overrides for proto-generated DTOs that can't carry the attribute.

### D2Result Pattern

Result objects replace exceptions for control flow. **Always use semantic factories** — never raw `Fail()` with manual status codes when a factory exists. Available: `Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Canceled`, `SomeFound`. Raw `Fail` only when no factory matches (e.g., re-mapping arbitrary upstream status codes).

Partial success: `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found).

### Interface organization

One handler interface per file, co-located with its implementation in the per-operation folder (`Application/Handlers/{Commands,Queries}/<Op>/I<Op>Handler.cs`). Consumers `using` the folder namespace directly — no `partial` interface aggregation, no grouping aliases. The per-op folder IS the discoverability mechanism.

### DI Registration

`.NET`: `services.AddTransient<IXxx, Xxx>()` via `Microsoft.Extensions.DependencyInjection`. Each layer exports `AddXxx(services)` extension method.

### EF-as-DDD + rich sum-type domains

CQRS handlers access relational data by composing queries against `I<Service>DbContext` + domain aggregates + LINQ directly — the per-op Repository handler is retired ([ADR-0017](docs/adrs/0017-ef-as-ddd-persistence.md)). `BaseHandler`/`BaseRepoHandler` retain all cross-cutting.

New stateful domain aggregates use **abstract base + sealed per-state types** so illegal transitions are uncompilable. The `Status` enum is a persistence discriminator derived from the type, not the authority on transitions. EF persistence uses a **flat `<Entity>Record`** (never TPH) + pure `ToDomain()`/`ProjectOnto()` mappers + `xmin` concurrency token + same-transaction audit writes. All three components live in `app/Infrastructure/Persistence/`; the concrete `DbContext`, EF config, and `Migrations/` live in `infra/Persistence/Postgres/`.

Canonical decision records: [ADR-0017](docs/adrs/0017-ef-as-ddd-persistence.md) + [ADR-0016](docs/adrs/0016-keycustodian-lifecycle-store.md). Operational form: [docs/PATTERNS.md §Repository](docs/PATTERNS.md#repository).

### Other Established Patterns

Options pattern, Caching marker interfaces (`ILocalCache` / `IDistributedCache` / `ITieredCache`), content-addressable entities (SHA-256 hash IDs), C# 14 extension-member mappers, batch operations, health-checks-must-use-production-code-path — see [docs/PATTERNS.md](docs/PATTERNS.md).

### Key Architecture Decisions

Auth (self-rolled .NET module within Edge, RFC 8693 + 6749 §4.4, JWKS at OIDC-canonical path), JWT (RS256, 15min expiry, `d2_`-prefixed snake_case custom claims), KeyCustodian (lifecycle of all long-lived secrets, state machine + overlap rotation), SvelteKit BFF (pure SSR, browser → Edge direct for auth mutations, `@d2/headers` route guards), sync gRPC / async RabbitMQ split (sensitive payloads encrypted via `D2.Shared.Encryption`), notifications via D2.Courier only, sessions 3-tier (cookie cache 5min → Redis → PostgreSQL dual-write), DB topology (one PG server, per-domain DBs, PG advisory-lock migration safety), object storage (SeaweedFS for user files, MinIO for LGTM blocks), production deployment (eventually Swarm + Portainer; pre-launch Compose on VPS) — see [docs/PATTERNS.md](docs/PATTERNS.md) + [docs/v2/V2.md](docs/v2/V2.md).

**Why these specifics** (constraints, not preferences): **RS256** (not HS256 — no shared secrets across service boundaries; not EdDSA — JWKS interop); **15min JWT expiry** (refresh-token rotation forces re-anchoring); **snake_case custom claims with `d2_` prefix** (the `:` punctuation in OAuth scope strings collides with camelCase JSON-path conventions); **3-tier sessions** (cookie cache eliminates Redis hop for short bursts; Redis is hot path; PG is durable backstop).

_Canonical form (full service-structure standard with rationale + carve-outs): [ADR-0020](docs/adrs/0020-service-project-structure.md) + [PATTERNS.md §service-project-structure](docs/PATTERNS.md#service-project-structure). Update all three in lockstep when any changes (per §11.32 KEEP-doc duplication discipline)._

---

## §5. Critical Reminders (top-of-mind for every change)

**The complete, verbose, authoritative rule catalog lives in [docs/dev/rules.md](docs/dev/rules.md) — security, race conditions, naming, object disposal, D2Result, OOTB shared libs, logging, PII redaction, graceful degradation, UX, DX, observability, idempotency, configuration, conventions, audit-evidence discipline, and more (~200 evidence-required predicates across 24 categories). READ IT END-TO-END DURING THE PLAN PHASE OF EVERY DELIVERABLE.**

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

### Sub-agent dispatch discipline

- **Sub-agent model policy (per [rules.md §24.0i](docs/dev/rules.md#24-audit-evidence-discipline-meta--how-to-audit))**: Auditor / Plan-Auditor / Final-reviewer / Implementer / Fixer / Investigator → Sonnet; Planner / Plan-amender / Aggregator → Opus; Orchestrator (main thread) → Opus. Sweeping Implementer / Fixer Opus carve-out per the four criteria in §24.0i (atomic >40-file / >3-concern / cross-runtime / cascading pipeline) — cite the criterion in the dispatch brief + sub-agent self-attestation. Auditor-shape role escalation to Opus requires explicit per-occurrence user approval per [§13.14](docs/dev/rules.md#13-permission--action-discipline). Canonical table + WHY per role + self-documentation requirement: [process.md §3 Sub-agent model policy per role](docs/dev/process.md#sub-agent-model-policy-per-role). Trust-but-verify discipline (orchestrator verification of Sonnet outputs): [process.md §4 Orchestrator verification of Sonnet sub-agent outputs](docs/dev/process.md#orchestrator-verification-of-sonnet-sub-agent-outputs).

### Ask when uncertain (the #1 rule)

- **ALWAYS ask when uncertain** — non-negotiable. Do not guess. Do not assume. If requirements / approach / tradeoffs are unclear, ASK. Every time. The failure mode isn't "didn't ask when I knew I was uncertain" — it's "didn't notice I should be uncertain." Stay alert.

### Test discipline (drives multi-pass audits when skipped)

- **Test every public path on first pass** — every `public` method (including DI extensions, gRPC plumbing, factory wrappers, "thin glue") gets ≥1 test BEFORE the feature is done. [rules.md §1.1]
- **Every bug fix lands with a regression test in the same change** — fails-without-fix, passes-with-fix. Behavior-descriptive name. **No fix without a test, no exceptions.** [rules.md §2]
- **Tests are adversarial** — happy path + garbage / null / empty / whitespace / oversized / malformed / wrong-type / cross-field deps / error propagation / idempotency / concurrency. [rules.md §1.2]
- **Composition/DI resolution tests must `GetRequiredService<>()` EVERY registered seam** — descriptor-presence ≠ resolvability. A test that resolves 3 of 8 handlers leaves 5 unverified. [rules.md §1.3]

### Use OOTB shared libs (don't hand-roll)

- **Falsey() / Truthy() instead of `string.IsNullOrEmpty` / `coll == null || coll.Count == 0` / `guid == Guid.Empty`.** [rules.md §5.1]
- **ThrowIfFalsey() for required-argument guards on string / collection / Guid** — one call covers null + empty/whitespace + `Guid.Empty` with BCL-split exceptions; carve-outs: plain reference-type null-guards (use BCL `ThrowIfNull`), generated files, genuine-cycle projects (e.g. `I18n.Abstractions ← Utilities`), guards needing a bespoke message. The no-Utilities carve-out is for GENUINE CYCLES ONLY — do NOT skip a `D2.Shared.Utilities` reference for "purity" when no cycle exists. [rules.md §5.1a]
- **D2.Shared.Utilities extensions instead of hand-rolled `TryParse` + null check** (`str.TryParseTruthyNull(out Guid? r)` / `str.TryParseTruthyNull<TEnum>(out var r)`). [rules.md §5.2]
- **D2Result semantic factories** (`Ok` / `NotFound` / `ValidationFailed` / `Conflict` / `ServiceUnavailable` / etc.) — never raw `Fail()` with manual statusCode when a factory exists. [rules.md §5.3]
- **Catalog of shared libs** to reach for first: [rules.md §16](docs/dev/rules.md#16-ootb-shared-lib-tooling--use-whats-there).

### PII / logging safety (the highest-risk class)

- **`[LoggerMessage]` MUST NOT accept `Exception`** — `ex.Message` leaks broker URI passwords, user input. Use `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` separately. [rules.md §3.1]
- **`[RedactData]` on PII types** — emails, phones, IPs, addresses, names, message content, filenames, presigned URLs, AMQP URIs. [rules.md §3.3]
- **At-rest PII anonymization via `D2.Shared.DataGovernance`** — GDPR right-to-erasure uses `[Anonymizable]` / fluent `.Anonymize*` + `IAnonymizationEngine`; faux/tombstone values are non-i18n literals; STRICTLY SEPARATE from `[RedactData]` (log-masking); engine logs omit the subject id. [rules.md §3.15]
- **Sensitive context in encrypted RMQ payload, NOT plaintext headers.** [rules.md §3.4]
- **Constant-time comparisons for API keys / tokens / secrets** (`CryptographicOperations.FixedTimeEquals`). [rules.md §3.9]

### Code quality (zero tolerance)

- **Zero warnings, BOTH tools** (they catch different issues — not interchangeable; running only one = real coverage gap). `dotnet build server/D2.slnx` (StyleCop / CS / null-ref / Roslyn analyzers) AND `jb inspectcode server/D2.slnx --severity=WARNING` (JetBrains-only: `[MustDisposeResource]`, captured-closure issues, object-init suggestions). Never suppress; fix ALL warnings/errors anywhere in the project — never dismiss as "pre-existing" (branch hygiene: if you touched the area, you own its cleanliness). [rules.md §5.21, §5.22, §5.23]

### Documentation parity

- **Doc edits in the SAME change as code edits** (not a separate commit). Telemetry tag enumerations, counter lists, config tables drift the moment you defer. [rules.md §11.1]
- **File headers on every source file you create or modify.** [rules.md §7.7]

### Convention slippage (memory of these = first-pass clean)

- **Field prefixes**: `_` (mutable), `r_` (readonly), `s_` (static), `sr_` (static readonly), `_UPPER` (private const), `UPPER` (public const). Primary-constructor params on handlers carry NO `r_` prefix. [rules.md §7.1]
- **`namespace` BEFORE `using` directives** in C#. [rules.md §5.10]
- **Global-usings policy** (frequency-driven per project): globalize ANY namespace repeated across ≥3 files in that project — including EF Core, DI, Options, `System.Security.Cryptography`, vendor SDKs. The dependency law is enforced by `<ProjectReference>` edges, not using-directive visibility; a global using is per-project and cannot leak across the layer boundary. Per-file usings stay for low-frequency (1–2 file) namespaces. Use `global using IClock = D2.Shared.Time.IClock;` in every project that uses both NodaTime and `D2.Shared.Time`. Never duplicate SDK ImplicitUsings or Tier-1 entries. [rules.md §5.26]
- Other convention predicates (string.Empty, no this., brace rules, C# 14 extension members, sealed default, American English, line length, no phase verbiage, tests next to feature) → [rules.md §5/§7](docs/dev/rules.md#5-c-code-conventions) (also covered by MEMORY.md feedback entries).

### Architectural layer hygiene

- **JWT validations at TRANSPORT layer (auth middleware), NOT per-handler `HandlerOptions`.** `RequiredScopes` IS per-handler; `ValidateAudience` is NOT. [rules.md §9.2]
- **Handlers validate input via `Domain.Create(input) → D2Result<Domain>` at the TOP of `ExecuteAsync`** — never let Redis / DB be the first to reject invalid data. [rules.md §9.4]
- **EF-as-DDD — CQRS handlers use `I<Service>DbContext` + aggregates + LINQ directly; the per-op Repository handler layer is retired.** `BaseHandler`/`BaseRepoHandler` retain all cross-cutting. No `I<Op>Repository` wrapper between handler and EF. [rules.md §9.37]
- **Stateful domain aggregates use abstract base + sealed per-state types — illegal transitions are uncompilable.** The `Status` enum is a derived persistence discriminator only; domain logic branches on type, not enum value. For entities not yet migrated to sum-type shape, an explicit valid-transitions table is mandatory. [rules.md §9.31]
- **Flat `<Entity>Record` + pure mapper for EF persistence of sum-type aggregates** — no TPH. Persist via a flat non-polymorphic record, `ToDomain()` mapper (switch on `Status`), `ProjectOnto()` UPDATE helper, `xmin` concurrency token, same-transaction audit. [rules.md §9.38]
- **NEVER hand-write DB migrations** — use `dotnet ef migrations add <Name>`. [rules.md §9.10]
- **EF migration `.cs` files MUST be excluded from StyleCop via `.editorconfig` `[**/Migrations/*.cs] generated_code = true`** — never suppress SA\* per-rule or hand-edit the generated output. [rules.md §26.9]
- **Never return `Ok()` after a branching operation unconditionally** — if a nested handler / provider can fail, check its result. [rules.md §9.20]

### Caching

- **Inject one of `ILocalCache` / `IDistributedCache` / `ITieredCache`** from `D2.Shared.Caching.Abstractions`. Use `*AndBroadcast*` write variants when other instances cache the same key. Every op returns `D2Result<T>`. [rules.md §16.3]

### Codegen discipline (generated files are reproducible — keep them that way)

- **NEVER hand-edit generated files.** Fix the GENERATOR, the INPUT, or EXTEND the pipeline — never the output. "Generated" includes `*.g.<ext>` files, anything under `Generated/`, anything produced by a documented pipeline (Roslyn source-gen output, `tools/ts-codegen` output, proto-derived files, Drizzle migration artifacts, Paraglide-compiled locales, **Tier-2 spec files like `contracts/geo/*.spec.json`** built by the 0008 geo data pipeline), and anything carrying a `GENERATED` / `do not edit` banner. Hand-edits get silently overwritten on the next pipeline run — the "fix" never existed from the pipeline's perspective. [rules.md §26.5]
- **Spec-mirror DTO types FORBIDDEN in destination assemblies** — autogen from the schema instead, OR move the DTO into source-gen internals under §26.2's no-leak + parity-test conditions. [rules.md §26.1]
- **Hand-write a DTO that mirrors a `.proto` / `.spec.json` / `.openapi.yaml` / `.graphql` shape in a published package = process-integrity failure.** [rules.md §26.1]
- **Error codes are SPEC-DECLARED** — every code lives in a `*-error-codes.spec.json` (generic `contracts/error-codes/` or per-domain `contracts/<domain>-error-codes/`) carrying its `httpStatus` + `category` + a valid `userMessageKey`; the constants, typed `D2Result` failure factories, and merged cross-service registry are GENERATED from that spec. No free-text code literals, no hand-mapped `Fail(statusCode, message)` where a spec entry could declare it, no hand-written `<Domain>Failures` duplicating generated output. [rules.md §26.6]
- **Emitters reference the TK CONSTANT, never a string-literal of the key / symbol-path** — an emitted `tk("TK.X.Y.Z")` path literal silently bypasses the catalog (it's not a real key, so it renders the raw path AND diverges from the runtime that uses the constant); fix the emitter, ship the cross-runtime render test. [rules.md §26.7]

> Need a specific category (security / concurrency / disposal / D2Result / OOTB libs / logging / PII / graceful degradation / UX / DX / observability / idempotency / configuration / codegen) → [rules.md table of contents](docs/dev/rules.md#table-of-contents) routes by §-number.

_Canonical form (full §1-§24 predicate catalog with Evidence + Why + How blocks per predicate): [rules.md](docs/dev/rules.md). This §5 is the impossible-to-miss short list; rules.md is the authoritative source. Update both in lockstep when either changes (per §11.32 KEEP-doc duplication discipline)._

---

## §6. Code Conventions

### C# Naming

| Element                          | Convention      | Example             |
| -------------------------------- | --------------- | ------------------- |
| Classes/Records/Interfaces       | `PascalCase`    | `GetReferenceData`  |
| Methods/Properties               | `PascalCase`    | `HandleAsync`       |
| Private instance fields          | `_camelCase`    | `_memoryCache`      |
| Private readonly instance fields | `r_camelCase`   | `r_getFromMem`      |
| Private static fields            | `s_camelCase`   | `s_instance`        |
| Private static readonly fields   | `sr_camelCase`  | `sr_activitySource` |
| Static readonly (non-private)    | `SR_PascalCase` | `SR_ActivitySource` |
| Private constants                | `_UPPER_CASE`   | `_BATCH_SIZE`       |
| Public/Internal constants        | `UPPER_CASE`    | `MAX_ATTEMPTS`      |
| Local constants (tests)          | `snake_case`    | `expected_count`    |
| Local variables                  | `camelCase`     | `result`            |

**Primary-constructor handlers**: Constructor parameters do NOT take the `r_` prefix — they're parameters, not fields, even though they're accessed like fields inside the class body. The carve-out applies ONLY to handler primary-constructor parameters; regular fields keep their prefixes.

> Common gotchas: folder casing inside-project = PascalCase, outside-project = lowercase; observability tags = camelCase (`traceId`, `correlationId`, `userId`, `orgId`, `service`); TS naming = camelCase methods / PascalCase types / kebab-case files; translation keys carry domain prefix (`auth_*`, `webclient_*`, `common_*`); per-operation handler naming (services) = `I<Op>Handler` / `<Op>Handler` / `<Op>Input` / `<Op>Output`, file name = type name, co-located in the op folder (see §4 + [rules.md §9.24](docs/dev/rules.md#9-architectural-layer-hygiene)). Full reference → [rules.md §7](docs/dev/rules.md#7-naming-file-headers-folder-casing).

_Canonical form (C# Naming table + TS naming + folder casing + file headers + observability fields + git conventions): [rules.md §7.1 Naming](docs/dev/rules.md#7-naming-file-headers-folder-casing). The table above is duplicated here for at-a-glance reference. Update both in lockstep when either changes (per §11.32 KEEP-doc duplication discipline)._

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

### Code Intelligence + Windows LSP workaround

Code Intelligence (TypeScript via `mcp__cclsp__*` tools — `get_hover`, `find_definition`, `find_references`, `find_workspace_symbols`, `get_diagnostics`; C# via `csharp-ls` for `workspaceSymbol` + diagnostics with Grep / Glob / Read fallback because `hover` / `documentSymbol` time out on the large solution) + Windows cmd-wrap fix for `marketplace.json` (must be reapplied after every `claude plugin marketplace update` — the update overwrites the file) → MEMORY.md "Code Intelligence (LSP)" + "Manual LSP Fix" sections.

### Project Structure

Key roots in the tree:

- `contracts/` — proto source of truth + i18n message files + fixtures
- `server/` — all trusted code (.NET services + SvelteKit BFF + .NET shared libs)
- `infra/` — deployment + observability (compose, docker, observability)
- `tools/` — dev tooling (scripts, generators)
- `docs/` — project documentation (PATTERNS, TESTS, PARITY, SRC_GEN, dev/process, dev/rules, adrs/, v2/, etc.)
- `secrets/` — gitignored + Claude-deny-ruled key material (root key, encryption keys, dev TLS certs). Populated by `tools/scripts/gen-dev-keys.sh`.
- `.claude/` — project-level Claude Code settings (`settings.json` with deny rules)

---

## §8. Local Secrets & Claude Deny Rules

Environment configuration is split:

| File                   | Contents                                                                                      | Committed?                                                    | Claude can read?    | Claude can edit?    |
| ---------------------- | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------- | ------------------- | ------------------- |
| `.env.local`           | Non-secret config — service URLs, ports, log levels, feature flags, CORS origins              | No (gitignored)                                               | **Yes**             | **Yes**             |
| `.env.local.example`   | Template with safe defaults                                                                   | **Yes**                                                       | Yes                 | Yes                 |
| `.env.secrets`         | Real third-party creds — Twilio, Resend, IPinfo, OAuth client secrets, prod-like DB passwords | No (gitignored)                                               | **No (deny-ruled)** | **No (deny-ruled)** |
| `.env.secrets.example` | Template with placeholder values like `TWILIO_AUTH_TOKEN=replace_with_real_value`             | **Yes**                                                       | Yes                 | Yes                 |
| `secrets/`             | Key material — root key, dev encryption keys, dev TLS certs                                   | No (gitignored, populated by `tools/scripts/gen-dev-keys.sh`) | **No (deny-ruled)** | **No (deny-ruled)** |

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
