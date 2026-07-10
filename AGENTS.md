<!--
Copyright (c) DCSV. All rights reserved.
-->

# AGENTS.md — D²-WORX Development Guide

**Who / problem:** Shared condensed project law for AI harnesses (Claude Code / Grok Build / Codex) and developers implementing D²-WORX — workflow gates, Doc Update Map, Critical Reminders, conventions — so agents do not invent process or drift from `rules.md` / `process.md`.

**Product:** **D²-WORX** — Microservices SaaS framework. C# 14 / .NET 10 backend, SvelteKit BFF (TypeScript 5.9 / Svelte 5). Pre-Alpha. PolyForm Strict license (reference implementation, non-commercial).

**TOC** — [MANDATORY 0](#mandatory-block-0-orchestrator-only-main-thread) · [MANDATORY 1](#mandatory-block-1-every-code-change) · [MANDATORY 2](#mandatory-block-2-audit-evidence--proof-discipline) · [MANDATORY 3](#mandatory-block-3-deliverable-completeness-checklist) · [§1 Workflow](#1-development-workflow) · [§2 Commands](#2-commands) · [§3 Doc map](#3-reference-documents) · [§4 Patterns](#4-patterns--architecture) · [§5 Critical Reminders](#5-critical-reminders-top-of-mind-for-every-change) · [§6 Conventions](#6-code-conventions) · [§7 Behavior](#7-behavioral-guidelines-dispositional--how-to-approach-work) · [§8 Secrets / deny](#8-local-secrets--multi-runtime-deny-map)

> # ⚠️⚠️⚠️ READ FIRST ⚠️⚠️⚠️
>
> **Before planning, implementing, or auditing ANYTHING — read [docs/dev/rules.md](docs/dev/rules.md) + [docs/dev/process.md](docs/dev/process.md) first.** Conflicting with them is a mistake UNLESS the user acknowledges in writing the SPECIFIC rules / steps being bypassed (per [rules.md §13.14](docs/dev/rules/13-permission-action-discipline.md#13-permission--action-discipline)).
>
> rules.md = canonical predicate catalog; process.md = canonical workflow + sub-agent architecture + audit-loop protocol. **AGENTS.md** condenses both for fast access — update **AGENTS.md** + rules.md + process.md in lockstep when either changes (per [rules.md §11.32](docs/dev/rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices)). Runtime adapters (`CLAUDE.md` for Claude Code — thin `@AGENTS.md` import only) are **not** a second law body.
>
> **One primary harness at a time** unless the user explicitly authorizes multi-harness experiments. Spawn only the active host's prefix (`claude-d2-*` / `grok-d2-*` / `codex-d2-*`). Multi-runtime pin trees in-repo are inventory, not concurrent-use permission. Map → [docs/dev/harness-runtimes.md](docs/dev/harness-runtimes.md).

> **📍 PROJECT STATE**: Active tracking doc = **[docs/v2/V2.md](docs/v2/V2.md)** (current scope + per-phase docs) — the single source for "what's the project doing now"; when it archives, this pointer updates to its successor. Frozen v1 snapshot at `/old/v1/D2-WORX/` (read-only reference).

<a name="mandatory-block-0-orchestrator-only-main-thread"></a>

> # ⚠️⚠️⚠️ MANDATORY — ORCHESTRATOR-ONLY MAIN THREAD ⚠️⚠️⚠️
>
> **The main thread ORCHESTRATES — it does NOT plan, implement, audit, or fix. EVERY round of plan / impl / audit / fix is a FRESH sub-agent** — IF Claude Code → `Agent` + `claude-d2-<role>`; IF Grok Build → `spawn_subagent` + `grok-d2-<role>`; IF Codex → `spawn_agent` + `codex-d2-<role>` (never bare `d2-*`; never another runtime's prefix — [harness-runtimes.md](docs/dev/harness-runtimes.md)). A second audit round = brand-new multi-seat Auditor batch + Aggregator (K=7 concern bundles A–G max; dirty-only after findings); a follow-up fix = brand-new Fixer. The fresh-context property is the entire point — WHY → [process.md §3](docs/dev/process.md#3-sub-agent-architecture).
>
> **Model + spawn names per [rules.md §24.0i](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)** — capability tiers are shared; **product model IDs and spawn names are runtime-owned** ([docs/dev/harness-runtimes.md](docs/dev/harness-runtimes.md)). **IF Claude Code** → spawn `claude-d2-<role>` from `.claude/agents/claude-d2-*.md`: planning roles → **Fable 5** (`max` only for the Planner; **Plan-Auditor → Opus 4.8 `xhigh`**); deep workhorse → **Opus 4.8**; volume → **Sonnet 4.6**. **IF Grok Build** → spawn `grok-d2-<role>` from `.grok/agents/grok-d2-*.md`: **all** Grok D2 roles → **`grok-4.5`** (planning/deep · `high`; volume Auditor/Investigator/Fixer-mechanical · `medium`; **`grok-composer-2.5-fast` cost-banned**). **IF Codex** → spawn `codex-d2-<role>` from `.codex/agents/codex-d2-*.toml`: planning roles → **`gpt-5.6-sol`** (Planner `max`, Plan-Auditor `xhigh`, Plan-amender `high`); deep workhorse → **`gpt-5.6-sol · high`**; volume → **`gpt-5.6-terra`** (Auditor `high`; Investigator/Fixer-mechanical `medium`). Never bare unprefixed `d2-*` or another runtime's prefix. Auditors are two-tier (mechanical vs C2/C3/E2 + ruling-critical deep); Final-reviewer = the Auditor definitions at deliverable scope. Sweeping Implementer / Fixer planning-tier carve-out (atomic >40-file / >3-concern / cross-runtime / cascading pipeline — cite the criterion; on Grok the model ID remains 4.5, on Codex Sol already occupies the deep and planning seats); any other pinned-tier override needs per-occurrence user approval → §24.0i.
>
> **Multi-seat K=7 dispatch is the default per audit round:** Default full audit partition is **K=7 concern bundles (A–G)**. **K=12 atomic dispatch is retired.** Targeted Y ⊆ K=7 and dirty-only re-dispatch apply on per-step rounds; **FINAL-REVIEW of a deliverable is full K=7 at deliverable scope** (not dirty-only of the last step). After findings re-dispatch **dirty seats only** (+ sister-blast) — dirty-only is not K=1. K=1 requires explicit per-round user permission per [rules.md §24.0h](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) — never self-invoked.
>
> **Plan currency before dispatch**: any EXECUTE decision that amends the Plan MUST be written into the Plan artifacts (journal + Plan Living State + Cross-cutting decisions table) in the SAME orchestrator turn, BEFORE the next dispatch (sub-agents have no conversation context). Mechanism → [process.md §4](docs/dev/process.md#plan-currency-before-dispatch); enforcement → [rules.md §24.17](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).
>
> **About to `Edit` a source file from the main thread? STOP — spawn an Implementer. About to walk rules.md? STOP — spawn an Auditor. About to read a journal to check progress? STOP — spawn a sub-agent to summarize.** Even a one-line typo fix runs Planner → Implementer → Auditor → (Fixer if findings). The ONLY bypass is an explicit user request naming the rule / step skipped (per [rules.md §13.14](docs/dev/rules/13-permission-action-discipline.md#13-permission--action-discipline)).
>
> Full role table + tool-access lists + empirical justification + cluster partition + Aggregator spec → [process.md §3](docs/dev/process.md#3-sub-agent-architecture) + [§4](docs/dev/process.md#4-audit-loop-mechanics).
>
> _Canonical: [process.md §3](docs/dev/process.md#3-sub-agent-architecture). Update both in lockstep per §11.32._

---

<a name="mandatory-block-1-every-code-change"></a>

> # ⚠️⚠️⚠️ MANDATORY — APPLIES TO EVERY CODE CHANGE ⚠️⚠️⚠️
>
> **EVERY code change — including "change one line", "rename this var", "fix this typo", "add this property", "tweak this config" — follows the Development Workflow (§1) AND every applicable predicate in [rules.md](docs/dev/rules.md).**
>
> **There is no "small change" carve-out.** A one-line edit IS a deliverable: PLAN (read rules.md, identify applicable categories, write a plan entry) → EXECUTE (code + test + audit loop until clean) → REVIEW (present for sign-off — DO NOT auto-commit). Small changes that bypass the process are how regressions ship, PII leaks slip in, docs drift, conventions slip.
>
> **The ONLY bypass is an explicit user request** like "skip the journal", "no audit for this typo", "just commit it", or "don't write a test." Without it the process applies in full; if a request seems too small, ASK: "one-line change — full process, or bypass [specific step]?" Default = full process.
>
> _Canonical: [rules.md §13](docs/dev/rules/13-permission-action-discipline.md#13-permission--action-discipline) + [process.md](docs/dev/process.md). Update both in lockstep per §11.32._

---

<a name="mandatory-block-2-audit-evidence--proof-discipline"></a>

> # ⚠️⚠️⚠️ MANDATORY — AUDIT EVIDENCE & PROOF DISCIPLINE ⚠️⚠️⚠️
>
> **EVERY audit round MUST embed a complete markdown evidence table in the journal (`docs/wip/<deliverable>/<NN>-<step>/journal.md`).** Prose-only journals = audit INCOMPLETE = step NOT done.
>
> **THREE ARTIFACTS, NEVER COLLAPSED** (tamper-evident by design — WHY → [rules.md §24.0](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)):
> 1. **Big table** (`## Latest sweep results`) — REPLACED each sweep, one row per rules.md subsection, status `✅ PASS` / `⚪ N/A` / `❌ FINDING-{H/M/L}` / `🟡 *` + file:line + reason/description+fix.
> 2. **Findings log** (`## Sweep findings log (append-only)`) — APPEND-ONLY `### Round N findings`; never deleted / reclassified.
> 3. **Fix log** (`## Fix log (append-only)`) — APPEND-ONLY 5-field entries (rules.md §, finding round, what changed, file:line, timestamp/commit). NEVER touches the big table.
>
> **Closure is proven by ABSENCE of a finding from the next sweep's big table — not by a fix-log "fixed" claim.**
>
> **ROUND SEQUENCE** (per [rules.md §24.0a-f](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)):
> 1. **Sweep** → REPLACE big table; APPEND `### Round N findings`.
> 2. **Fix work** → per finding, apply fix + APPEND a fix-log entry. Big table untouched. Cross-cutting fixes run a sister-sweep over full scope (§24.13); user-flagged / STILL_PRESENT findings require tamper-evident BEFORE/AFTER + `git diff --stat` (§24.14).
> 3. **EVERY finding gets fixed** — no silent carryover; deferral requires explicit user permission (§13.4 / §13.14) + a fix-log deferral entry.
> 4. **Next sweep** with a BRAND-NEW Auditor walking the catalog from scratch. PASS in Round N+1 = closed; STILL FINDING = re-fix.
> 5. **Loop terminates** on ONE zero-FINDING sweep. No "convergence" without a clean real sweep.
>
> Detailed protocol → [process.md §4](docs/dev/process.md#4-audit-loop-mechanics). Predicates → [rules.md §24](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) (§24.0 artifacts, §24.0a-h lifecycle, §24.10 status, §24.12 self-audit, §24.13 sister-sweep, §24.14 tamper-evident). Visual flow → [Deliverable workflow chart](docs/dev/rules.md#deliverable-workflow-chart--order-of-operations-with-loops).
>
> _Canonical: [rules.md §24](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit). Update both in lockstep per §11.32._

---

<a name="mandatory-block-3-deliverable-completeness-checklist"></a>

> # ⚠️⚠️⚠️ DELIVERABLE COMPLETENESS CHECKLIST — THE GATE BEFORE USER REVIEW ⚠️⚠️⚠️
>
> **Before declaring ANY deliverable "ready for REVIEW," walk the [Deliverable completeness checklist in rules.md](docs/dev/rules.md#deliverable-completeness-checklist-the-gate-before-user-review). Every box = YES with a citation. Any NO → not ready; finish the gap and re-walk.**
>
> **Final attestation**: before presenting for REVIEW, write the attestation block from the checklist (verbatim wording in rules.md) into the deliverable's root README. It is YOUR signed claim that every box is an honest YES — a signed YES that turns out NO is a process-integrity breach under §24.0 tamper-evidence.
>
> **Covers**: per-step audit-loop convergence, cross-cutting verification (parity tests, doc parity, generated-file regeneration, observability completeness), and final-review sweep convergence. Walked once per deliverable immediately before declaring REVIEW-ready — no exceptions.
>
> _Canonical: [rules.md Deliverable completeness checklist](docs/dev/rules.md#deliverable-completeness-checklist-the-gate-before-user-review). Update both in lockstep per §11.32._

---

## §1. Development Workflow

Reach alignment with the user in PLAN; the **main-thread orchestrator drives EXECUTE by spawning fresh sub-agents** for every plan / impl / audit / fix round (per-step audit loop, 10-iteration ceiling, append-only journal) and FINAL-REVIEW (same loop, whole-deliverable scope); then hand off to REVIEW. User value lives in PLAN (design) and REVIEW (architecture) — not in pushing the agent through audit rounds.

**Detailed protocol → [process.md](docs/dev/process.md); sub-agent architecture → [process.md §3](docs/dev/process.md#3-sub-agent-architecture); audit catalog → [rules.md](docs/dev/rules.md); past deliverables → [docs/dev/deliverables/](docs/dev/deliverables/README.md).**

### Phase summary

- **PLAN** — Discuss with user. Lock the high-level goal + step breakdown (one csproj or equivalent per step) + cross-cutting decisions + risk analysis. Output: `docs/wip/<deliverable>/README.md` (gitignored) with step list, decisions, prerequisites.
- **EXECUTE** — Per step, in prerequisite order, the orchestrator spawns fresh sub-agents per phase: (1) **Planner** — appends a Plan section to `docs/wip/<deliverable>/<NN>-<step>/journal.md` with pre-emptive gate checks (test-coverage plan, convention / PII / layer check). (2) **Implementer** — writes code + tests; returns files-touched + build/inspectcode status. (3) **Audit loop** — fresh multi-seat Auditor batch + Aggregator per round (**K=7** bundles A–G max; Y ⊆ K=7 first code-audit when justified; dirty-only after findings) → fresh Fixer per finding → next round = brand-new sub-agents; terminate on a zero-FINDING sweep (10-iter ceiling); each Auditor produces the 3-artifact journal ([process.md §4](docs/dev/process.md#4-audit-loop-mechanics) / [rules.md §24](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit)). (4) Per-step distillation → root README's kinds-of-misses log + candidate rules.md predicates.
- **FINAL-REVIEW** — Same audit loop, scope = whole deliverable; **full K=7** (bundles A–G) Final-reviewer batch + Aggregator. Catches integration / cross-step / consistency bugs.
- **SHIP** — Aggregate proposed rule additions from the journals. Present the root README to the user; apply approved rules to `rules.md`; snapshot the root README to `docs/dev/deliverables/NNNN-<name>.md` (committed). Per-step journals stay in gitignored `docs/wip/` — never auto-deleted.
- **REVIEW** — User reviews. Feedback is captured-and-confirmed first, NOT fixed-on-sight. Bugs the audit should have caught become new predicates (self-improvement loop).

### Permission gates (must block — no inferred permission)

Per [process.md §2](docs/dev/process.md#2-permission-gates-when-to-pause-for-the-user):

- Commit creation — explicit user permission per occurrence.
- Bulk file operations — declare scope before executing.
- Destructive git operations (force push, hard reset, branch delete) — explicit authorization.
- Deferring planned work — ASK, not unilaterally skip.
- Architectural decision changes mid-execution — ASK before deviating from the locked PLAN.

### Self-improvement (the key insight)

Every deliverable's distillation surfaces classes of miss; approved misses become permanent `rules.md` predicates → future audit loops converge in fewer rounds. The journal IS the evidence of process integrity — every honest miss becomes a future gate-check.

_Canonical: [process.md §1](docs/dev/process.md#1-phase-lifecycle) (lifecycle) + [process.md §2](docs/dev/process.md#2-permission-gates-when-to-pause-for-the-user) (gates). Update both in lockstep per [§11.32](docs/dev/rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices)._

---

## §2. Commands

> ⚠️ **DO NOT START SERVICES MANUALLY** — never run `dotnet run`, `pnpm dev`, `pnpm preview`, or any long-running server; Docker Compose manages services. E2E tests that self-manage their infrastructure (Testcontainers, child processes with cleanup) ARE allowed.

**Most-cited** (full catalog — Compose lifecycle, single-project builds, test filters, lint, versioning — in [docs/COMMANDS.md](docs/COMMANDS.md)):

- **Build .NET solution**: `dotnet build server/D2.slnx` — zero warnings (StyleCop / CS / null-ref).
- **JetBrains inspections**: `jb inspectcode server/D2.slnx --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log` — zero warnings; catches what `dotnet build` does not (`[MustDisposeResource]`, captured-closure).
- **Run .NET tests**: `dotnet test server/D2.slnx` (full) or `... -- --filter-trait "Category=Unit"` (unit only).
- **SvelteKit type check**: `cd server/web && pnpm exec svelte-check`.

---

<a name="3-reference-documents"></a><a name="35-doc-update-map"></a>

## §3. Reference Documents + Doc Update Map

One table, two axes: **read** before touching the area, **update** after. A change spanning multiple rows updates each. No row fits → the change probably needs a new doc; ASK first.

| Document | When to read | When to update |
|---|---|---|
| [docs/dev/process.md](docs/dev/process.md) | Before ANY deliverable | Workflow / sub-agent architecture / audit-loop / Plan-Audit changes (pair with [rules §24](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) if a predicate changes) |
| [docs/dev/harness-runtimes.md](docs/dev/harness-runtimes.md) | Before any sub-agent dispatch or pin/model change; three-runtime agent pins + spawn names (`claude-d2-*` / `grok-d2-*` / `codex-d2-*`) | Adding/retiering a role pin, changing product model IDs / effort, or renaming spawn paths (pair with [process.md §3](docs/dev/process.md#3-sub-agent-architecture) + [rules §24.0i](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) + the active runtime's pin files) |
| [docs/dev/codebase-memory.md](docs/dev/codebase-memory.md) | When `codebase-memory-mcp` is connected; before audit/impl discovery work | Graph-vs-Grep usage law, project name, token discipline (pair with process.md tool-access + agent universal constraints + audit-round skill) |
| [docs/dev/rules.md](docs/dev/rules.md) index + per-category files under [docs/dev/rules/](docs/dev/rules/) | End-to-end at PLAN; walked each audit round (each seat reads only its category files — K=7 concern bundles A–G) | New predicate / category change — edit the category file under `docs/dev/rules/` (codegen row also touches [§26](docs/dev/rules/26-codegen-discipline-spec-proto-schema-derived-types.md#26-codegen-discipline-spec--proto--schema-derived-types)) |
| [docs/dev/deliverables/](docs/dev/deliverables/README.md) | Researching a past deliverable | At SHIP — the root README snapshot lands here |
| [docs/COMMANDS.md](docs/COMMANDS.md) | Build / test / lint / versioning commands | A command, flag, or service-lifecycle invocation changes |
| [CONTRIBUTING.md](CONTRIBUTING.md) + [docs/COMMANDS.md](docs/COMMANDS.md) (versioning) | PR prep; releasing / versioning a consumable package | Branch / commit / PR conventions; per-package versioning, `tools/release-runner`, breaking-change gate, `release-libs.yml` (pair with [§26.19](docs/dev/rules/26-codegen-discipline-spec-proto-schema-derived-types.md#26-codegen-discipline-spec--proto--schema-derived-types)) |
| [docs/PATTERNS.md](docs/PATTERNS.md) | Any handler / DI / repo / cache / middleware / service-structure work | A handler / service-structure / DI / `D2Result` factory / RedactionSpec / mapper / repo pattern change |
| [docs/TESTS.md](docs/TESTS.md) | Adding / modifying tests | Test category, custom matcher, adversarial-coverage rule, fixture pattern change |
| [docs/PARITY.md](docs/PARITY.md) | Adding cross-language components | Anything cross-language (.NET ↔ SvelteKit ↔ future) |
| [docs/SRC_GEN.md](docs/SRC_GEN.md) | Any source-gen / spec-driven codegen | New generator, spec-format change, new emitter |
| [docs/TIMESTAMPS.md](docs/TIMESTAMPS.md) | Any timestamp / temporal work | Timestamp categories, NodaTime type selection, DST rules, PG column mapping, wire `DateTimeOffset?` conversion |
| [ADRs](docs/adrs/README.md) | Researching / proposing an architectural decision | An ADR overrides a prior v2 plan (pair with a V2.md entry) |
| [ADR-0020](docs/adrs/0020-service-project-structure.md) | Any service-project layout work | Service-structure convention changes — layer set, two-section app split, per-op folders, Commands/Queries rule, five-surface mapper rule, concern vocabulary, global-usings (pair with PATTERNS.md + rules §5/§7/§9 + §4 here) |
| [messaging/rabbitmq/README.md](server/shared/dotnet/messaging/rabbitmq/README.md) | Any async messaging work | Async messaging — wire format, exchange / queue topology, AMQP headers, encryption frame, DLQ behavior |
| [docs/v2/PHASE_3_EDGE.md](docs/v2/PHASE_3_EDGE.md) | Any Edge operational-guarantee work | Edge guarantees — HTTP idempotency, request enrichment, session 3-tier, scheduled-jobs receiver, multi-instance scaling |
| [docs/v2/PHASE_3_RATE_LIMITING.md](docs/v2/PHASE_3_RATE_LIMITING.md) | Any rate-limit middleware work | Rate-limit design / bucket math / kill-switch / FP-too-common detection / cookie shortcut |
| [docs/v2/PHASE_3_AUTH.md](docs/v2/PHASE_3_AUTH.md) | Any KeyCustodian / key rotation / secret handling | KeyCustodian, key rotation, secret handling, compromise runbook |
| **Active tracking doc** (header — currently [docs/v2/V2.md](docs/v2/V2.md)) | Before starting any task | Phase progression / wipe state / open questions / new tracked issue; decisions overriding a prior v2 plan (also add an ADR) |
| Per-lib / per-service `README.md` | When working in that lib / service | Add / modify a public API on a lib or service |
| [docs/README.md](docs/README.md) | How the doc set is organized (tiers + lifecycle) | New tier-level category or holding-pen lifecycle rule (pair with [§11.43](docs/dev/rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices)) |

Per-service / per-library `README.md` files appear in `server/services/{service}/` and `server/shared/dotnet/{lib}/`.

---

## §4. Patterns & Architecture

**Follow existing patterns; do not invent when one applies. No pattern fits → ASK. Behavioral Guidelines (§7) apply throughout.** Detail: [docs/PATTERNS.md](docs/PATTERNS.md).

### Service project structure

Canonical: [ADR-0020](docs/adrs/0020-service-project-structure.md); daily-driver: [PATTERNS.md](docs/PATTERNS.md#service-project-structure).

**Five projects + the dependency law.** Standalone service = `domain/` + `app/` + `infra/` + `api/` + `tests/` (+ a consumer-facing `client/` package — SINGULAR, matching its `.Client` assembly — + a `netstandard2.0` source-gen shell when it owns error codes). Service-client RUNTIME code lives in the service's `client/`, never `server/shared/` (shared = service-agnostic abstractions only).

```
Domain  ←  App  ←  Infra  ←  Api      (Tests reference what they test; Clients reference contracts + shared libs only)
```

Domain references shared primitives only (NO EF / Options / DI / logging / vendor SDK); App declares ports + shapes (transport-agnostic); Infra is the only vendor-SDK layer; **Api is the only project allowed to reference Infra.** A **module-within-host** (KeyCustodian + the auth module, both in Edge) takes `domain`/`app`/`infra` but OMITS `api/` + its own `tests/` (host api = composition root + transport mapper; tests live under a `<Module>/` subtree in the host test project).

**Domain** = `Entities/` + `ValueObjects/` + `Enums/` + `Rules/` (pure no-port no-IO logic — a tunable is a method param, not `IOptions<>`; pure logic lives here, NOT in app handlers). **App = two sections**: `app/Application/` (per-op handlers + `Observability/` + `AddD2<Service>App()`) and `app/Infrastructure/` (ports + shapes by concern + `Configuration/`); `infra/` mirrors the concern folders with adapters.

**Op-noun concern folders + `Facade/`.** App-layer support types live in op-noun concern folders SIBLING to `Handlers/` (namespace = folder), NOT nested in `Handlers/<Op>/`; the `Application/` root keeps ONLY the composition-root extension. The `client/` package mirrors this (`Facade/` = `I<Module>Api.g.cs`; each concern folder = its ops' `.g.cs` DTOs + hand-written runtime). Concern→folder is spec-driven via `@d2Concern("<Segment>")` (fail-loud `D2TSP013` if a client-exposed op omits it). Canonical: [ADR-0020](docs/adrs/0020-service-project-structure.md) + [SRC_GEN.md](docs/SRC_GEN.md).

**Per-operation handler folders.** `Application/Handlers/{Commands,Queries}/<Op>/{I<Op>Handler.cs, <Op>Handler.cs, <Op>Input.cs, <Op>Output.cs}`; file name = type name. NO `Models/` bucket; NO `Interfaces/`/`Implementations/` mirror.

**Commands vs Queries — binary side-effect rule.** Op mutates persistent/shared state (DB / distributed-cache / external write / message publish) → `Commands/`; none → `Queries/`. **The verb is irrelevant**; no "Complex" tier; local/in-memory caching doesn't make a Query a Command.

**Concern folders + mandatory vendor subfolders.** Capability-noun concern folders; every `infra/` concern carries a tech/vendor/protocol subfolder even for a sole impl (`infra/Persistence/Postgres/`); namespaces keep the `.App`/`.Infra` segment verbatim.

**Mappers — one home per surface.** Transport (proto/REST ↔ `<Op>Input`/`<Op>Output`) → `api/Mappers/`; persistence (record ↔ aggregate) → `app/Infrastructure/Persistence/`; provider-SDK ↔ domain → `infra/<Concern>/<Vendor>/`; messaging-wire ↔ domain → `infra/Messaging/<Broker>/`; primitives → domain VO `Create` factories. All pure C# 14 extension members.

### Multi-provider — keyed resolver

App stays vendor-blind (ONE capability port per concern); infra registers keyed adapters (.NET keyed DI, one per vendor subfolder); a runtime-selected vendor uses `I<Capability>Resolver.Get(key) → D2Result<T>` (typed unknown-key failure). For messaging the resolver layers on `[MqPub]`.

### Verb Semantics

- **Find** = "Resolve this for me" — may fetch externally, may cache/persist. E.g. `FindWhoIs`.
- **Get** = "Give me this by ID" — direct lookup, read-only. E.g. `GetWhoIsByIds`.

### Handler Pattern

`.NET`: every concrete handler is `BaseHandler<TSelf, I, O>` / `BaseRepoHandler<TSelf, I, O>` with three **file-scoped** `using` aliases — `H` = its own interface (`I<Op>Handler`), `I` = input, `O` = output — used in the type args, the `, H` slot, `ExecuteAsync(I input, CancellationToken ct)`, and the `ValueTask<D2Result<O?>>` return. **`TSelf` (own class name) is spelled out, NEVER aliased** (names the type for OTel span naming). Aliases are per-file, never global.

```csharp
namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;

using H = D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey.IGenerateKeyHandler;
using I = GenerateKeyInput;
using O = KeySummary;

public sealed class GenerateKeyHandler(HandlerContext<GenerateKeyHandler> ctx, …)
    : BaseRepoHandler<GenerateKeyHandler, I, O>(ctx, classifier), H
{
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(I input, CancellationToken ct) { … }
}
```

Handler context is injected via the primary-constructor `HandlerContext<TSelf>` parameter. Per-handler PII redaction via `[RedactData]` on data types + `DefaultOptions` overrides for proto DTOs that can't carry the attribute ([rules.md §5.29](docs/dev/rules/05-csharp-code-conventions.md#5-c-code-conventions)).

### D2Result Pattern

Result objects replace exceptions for control flow. **Always use semantic factories** — never raw `Fail()` with manual status codes when a factory exists: `Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Canceled`, `SomeFound`. Raw `Fail` only when no factory matches. Partial success: `NOT_FOUND` (none) → `SOME_FOUND` (partial, data returned) → `OK` (all).

### Interface organization

One handler interface per file, co-located with its impl in the per-op folder. Consumers `using` the folder namespace directly — no `partial` aggregation, no grouping aliases. The per-op folder IS the discoverability mechanism.

### DI Registration

`.NET`: `services.AddTransient<IXxx, Xxx>()` via `Microsoft.Extensions.DependencyInjection`; each layer exports an `AddXxx(services)` extension method.

### EF-as-DDD + rich sum-type domains

CQRS handlers compose queries against `I<Service>DbContext` + domain aggregates + LINQ directly — the per-op Repository handler is retired ([ADR-0017](docs/adrs/0017-ef-as-ddd-persistence.md)); `BaseHandler`/`BaseRepoHandler` retain all cross-cutting. New stateful aggregates use **abstract base + sealed per-state types** (illegal transitions uncompilable); `Status` is a persistence discriminator derived from the type, not the transition authority. EF persistence = **flat `<Entity>Record`** (never TPH) + pure `ToDomain()`/`ProjectOnto()` mappers + `xmin` token + same-transaction audit (all in `app/Infrastructure/Persistence/`; concrete `DbContext` / EF config / `Migrations/` in `infra/Persistence/Postgres/`). Canonical: [ADR-0017](docs/adrs/0017-ef-as-ddd-persistence.md) + [ADR-0016](docs/adrs/0016-keycustodian-lifecycle-store.md); operational: [PATTERNS.md §Repository](docs/PATTERNS.md#repository).

### Other Established Patterns

Options pattern, caching marker interfaces (`ILocalCache` / `IDistributedCache` / `ITieredCache`), content-addressable entities (SHA-256 hash IDs), C# 14 extension-member mappers, batch operations, health-checks-use-production-code-path — see [docs/PATTERNS.md](docs/PATTERNS.md).

### Key Architecture Decisions

Auth (self-rolled .NET module within Edge, JWKS at OIDC-canonical path; service-to-service = mint one internal transaction-token at the Edge boundary + forward unchanged + re-validate each hop, mTLS authenticating the workload — RFC 8693 token-exchange is the boundary-mint + exception tool, NOT a per-hop default — [ADR-0022](docs/adrs/0022-service-auth-mint-once-forward.md) + [ADR-0023](docs/adrs/0023-mtls-workload-identity.md)); JWT (RS256, 15min expiry, `d2_`-prefixed snake_case claims); KeyCustodian (lifecycle of all long-lived secrets incl. the mTLS CA, state machine + overlap rotation); SvelteKit BFF (pure SSR, browser → Edge direct for auth mutations, `@d2/headers` route guards); sync gRPC / async RabbitMQ split (sensitive payloads encrypted via `D2.Shared.Encryption`); notifications via D2.Courier only; sessions 3-tier (cookie cache 5min → Redis → PostgreSQL dual-write); DB topology (one PG server, per-domain DBs, PG advisory-lock migration safety); object storage (SeaweedFS user files, MinIO LGTM blocks); deployment (eventually Swarm + Portainer; pre-launch Compose on VPS) — see [PATTERNS.md](docs/PATTERNS.md) + [V2.md](docs/v2/V2.md). Why the specifics (RS256, 15min, `d2_` snake_case, 3-tier sessions) → [PATTERNS.md](docs/PATTERNS.md).

_Canonical: [ADR-0020](docs/adrs/0020-service-project-structure.md) + [PATTERNS.md §service-project-structure](docs/PATTERNS.md#service-project-structure). Update all three in lockstep per §11.32._

---

## §5. Critical Reminders (top-of-mind for every change)

**The complete authoritative catalog is [docs/dev/rules.md](docs/dev/rules.md) (~440 evidence-required predicates across 26 categories) — READ IT END-TO-END DURING PLAN.** This section is the impossible-to-miss short list: one gate + §-citation per bullet; the cited § holds the full predicate (Evidence + Why + How).

### Production-readiness mindset

- **Every change must survive bad input, infrastructure failure, concurrency, hostile users, and future engineers.** "Works on my machine" is not the bar.

### Permission gates (NEVER bypass)

- **NEVER commit without explicit user permission** for THIS commit (not an earlier "go ahead"); take every commit through the sanctioned `cycle-commit` marker path (plants the one-shot `.claude/.commit-authorized` marker + EXIT-trap-removes it). Structural backstops: Claude/Grok → `git-guard`; Codex → `d2-policy-guard.mjs` — both require the shared marker. Never a raw `git commit`. [rules.md §13.1 / §13.1a](docs/dev/rules/13-permission-action-discipline.md#13-permission--action-discipline)
- **NEVER bulk-edit / sed / mass-rename without first declaring scope** (count, glob, what changes). [rules.md §13.2]
- **NEVER destructive git ops** (force push, hard reset, branch delete, `git stash` in sub-agents) without explicit authorization. [rules.md §13.3]
- **NEVER defer / skip planned work** without asking first. [rules.md §13.4]
- **NEVER reflexively defer / de-scope in-scope doable-now work — do-it-now is the default.** Deferral is legitimate ONLY for genuinely-blocked work (a missing build dependency) + surfacing + user permission + a committed tracker row; "no consumer yet / not wired in yet / pre-existing" is not a blocker. [rules.md §13.15]
- **NEVER start services manually** (`dotnet run`, `pnpm dev`) — Docker Compose manages services. [rules.md §8.1]
- **NEVER `Grep` `secrets/` or `.env.secrets` by name.** [rules.md §3.11]

### Sub-agent dispatch discipline

- **Sub-agent model policy** ([rules.md §24.0i](docs/dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit); three-runtime pins + spawn names → [harness-runtimes.md](docs/dev/harness-runtimes.md)): capability tiers shared (planning / deep workhorse / volume). **Claude** spawn `claude-d2-*` from `.claude/agents/` (Fable / Opus / Sonnet). **Grok** spawn `grok-d2-*` from `.grok/agents/` (`grok-4.5` only; volume · medium; deep/plan · high; composer cost-banned). **Codex** spawn `codex-d2-*` from `.codex/agents/` (Sol · max/xhigh/high for planning; Sol · high for deep; Terra · high/medium for volume). Auditors two-tier (mechanical vs deep for bundles C/D/G + ruling-critical); Final-reviewer = Auditor defs at deliverable scope (full K=7). Sweeping Implementer / Fixer carve-out per the four §24.0i criteria — cite + self-attest; any other pinned override needs per-occurrence user approval ([§13.14](docs/dev/rules/13-permission-action-discipline.md#13-permission--action-discipline)). Table + trust-but-verify → [process.md §3](docs/dev/process.md#sub-agent-model-policy-per-role) + [§4](docs/dev/process.md#orchestrator-verification-of-sub-agent-outputs).

### Ask when uncertain (the #1 rule)

- **ALWAYS ask when uncertain** — non-negotiable; don't guess or assume. The failure mode isn't "didn't ask when I knew I was uncertain" — it's "didn't notice I should be uncertain."

### Test discipline (drives multi-pass audits when skipped)

- **Test every public path on first pass** — every `public` method (DI extensions, gRPC plumbing, factory wrappers, thin glue) gets ≥1 test before done. [rules.md §1.1]
- **Every bug fix lands with a regression test in the same change** — fails-without-fix, passes-with-fix. No fix without a test. [rules.md §2]
- **Tests are adversarial** — happy path + garbage / null / empty / whitespace / oversized / malformed / wrong-type / cross-field / error propagation / idempotency / concurrency. [rules.md §1.2]
- **Composition/DI tests `GetRequiredService<>()` EVERY registered seam** — descriptor-presence ≠ resolvability. [rules.md §1.3]
- **Test doubles for unbuilt collaborators MUST assert the real seam contract** — a hollow canned-value double = FINDING-HIGH. [rules.md §1.32]

### Use OOTB shared libs (don't hand-roll)

- **Falsey() / Truthy()** not `string.IsNullOrEmpty` / `coll == null || coll.Count == 0` / `guid == Guid.Empty`. [rules.md §5.1]
- **ThrowIfFalsey()** for required-argument guards on string / collection / Guid. Carve-outs: reference-type null-guards (BCL `ThrowIfNull`), generated files, genuine-cycle projects, bespoke-message guards. [rules.md §5.1a]
- **D2.Shared.Utilities extensions** not hand-rolled `TryParse` + null check (`str.TryParseTruthyNull(out Guid? r)` / `<TEnum>`). [rules.md §5.2]
- **D2Result semantic factories** — never raw `Fail()` with a manual statusCode when a factory exists. [rules.md §5.3]
- **Catalog of shared libs to reach for first**: [rules.md §16](docs/dev/rules/16-ootb-shared-lib-tooling-use-whats-there.md#16-ootb-shared-lib-tooling--use-whats-there).

### PII / logging safety (the highest-risk class)

- **`[LoggerMessage]` MUST NOT accept `Exception`** — `ex.Message` leaks passwords / user input; use `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)`. [rules.md §3.1]
- **`[RedactData]` on PII types** — emails, phones, IPs, addresses, names, message content, filenames, presigned URLs, AMQP URIs. [rules.md §3.3]
- **Redaction marker declares an ACCURATE `RedactReason` — no silent PII default** — `SecretInformation` vs `PersonalInformation`; the emitter FAILS LOUD on a bare marker (PII and secret regimes are strictly separate). [rules.md §3.17]
- **At-rest PII anonymization via `D2.Shared.DataGovernance`** — `[Anonymizable]` / `IAnonymizationEngine`; STRICTLY SEPARATE from `[RedactData]` log-masking. [rules.md §3.15]
- **Sensitive context in encrypted RMQ payload, NOT plaintext headers.** [rules.md §3.4]
- **Constant-time comparisons for API keys / tokens / secrets** (`CryptographicOperations.FixedTimeEquals`). [rules.md §3.9]

### Code quality (zero tolerance)

- **Zero warnings, BOTH tools** — `dotnet build server/D2.slnx` AND `jb inspectcode server/D2.slnx --severity=WARNING` (they catch different issues). Never suppress; fix ALL, never dismiss as "pre-existing." [rules.md §5.21, §5.22, §5.23]
- **Prettier-clean CODE commits** (`.husky/pre-commit`) — touched `.ts`/`.js`/`.json`/`.svelte`/`.css`/`.yaml` pass `prettier --check` (`pnpm format`). **Markdown EXCLUDED** (`.md` in `.prettierignore`, NEVER `prettier --write`). [rules.md §5.28]

### Documentation parity

- **Doc edits in the SAME change as code edits** (not a separate commit). [rules.md §11.1]
- **File headers on every source file you create or modify.** [rules.md §7.7]
- **Agent-facing docs (AGENTS.md / rules / process.md / reference docs) are agent-first — human navigability NEVER at the cost of context bloat; densest faithful form wins (no Mermaid / box-drawing art).** [rules.md §11.45]

### Convention slippage (memory of these = first-pass clean)

- **Field prefixes**: `_` (mutable), `r_` (readonly), `s_` (static), `sr_` (static readonly), `_UPPER` (private const), `UPPER` (public const). Handler primary-ctor params carry NO `r_`. [rules.md §7.1]
- **`namespace` BEFORE `using` directives** in C#. [rules.md §5.10]
- **Global-usings policy** — globalize any namespace across ≥3 files in a project (EF Core, DI, Options, `System.Security.Cryptography`, vendor SDKs); per-file usings for 1–2-file namespaces; `global using IClock = D2.Shared.Time.IClock;` wherever both NodaTime + `D2.Shared.Time` are used. [rules.md §5.26]
- **Blank-line padding in method bodies** — single blank before AND after every control-flow / `using` block AND every multi-line statement; consecutive simple single-liners grouped; single blanks only (SA1507); none at block open/close (SA1505/08) or before a `catch`/`finally`/`else`/`while` continuation (SA1510); not for generated / migration files. [rules.md §5.8a]
- **Handler I/O/H aliases** — file-scoped `using H = I<Op>Handler;` / `I = <Op>Input;` / `O = <Op>Output;` in the `BaseHandler<TSelf, I, O>` args, `, H` slot, `ExecuteAsync(I input, …)`, `ValueTask<D2Result<O?>>`; TSelf never aliased; alias directives exempt from the 100-char ceiling. [rules.md §5.29, §7.14]
- **Other predicates** (string.Empty, no `this.`, braces, C# 14 extension members, sealed default, American English, line length, no phase verbiage, tests next to feature) → [rules.md §5](docs/dev/rules/05-csharp-code-conventions.md#5-c-code-conventions)/§7.

### Architectural layer hygiene

- **JWT signature/expiry/audience validation at the TRANSPORT layer (auth middleware), NOT per-handler `HandlerOptions`.** `RequiredScopes` IS per-handler; `ValidateAudience` is NOT. [rules.md §9.2]
- **A captured live credential enters request-scoped state ONLY after ALL inbound auth gates pass** (incl. session-liveness / revocation), symmetrically across every transport. [rules.md §9.39]
- **Authority-grade facts (`RequestOrigin`, `ImmediateCaller`) recomputed FRESH from local unforgeable transport evidence at each establishment boundary** — a forwarded wire value is NEVER trusted for authority; cross-process `ImmediateCaller` = mTLS peer cert (SPIFFE SAN via `GetD2PeerWorkloadIdentity()`) ONLY. [rules.md §9.41]
- **Fail-closed — the establishment enum's type-zero (`RequestOrigin.Unestablished`) is an explicit FIRST-checked DENY in every authority rule.** [rules.md §9.42]
- **Telemetry-only hop-trace fields (CallPath) stay a DIFFERENT TYPE than authority-grade fields and are NEVER a parameter to any authority rule** — structural exclusion. [rules.md §9.43]
- **Authority over a cluster-root-grade secret routes through a DEDICATED capability seam in exactly ONE composition root + a structural deny on the general surface** — not a boolean guard; ship the DI-isolation test. [rules.md §9.44]
- **Handlers validate input via `Domain.Create(input) → D2Result<Domain>` at the TOP of `ExecuteAsync`** — never let Redis / DB reject invalid data first. [rules.md §9.4]
- **EF-as-DDD — CQRS handlers use `I<Service>DbContext` + aggregates + LINQ directly; the per-op Repository layer is retired.** [rules.md §9.37]
- **Stateful aggregates use abstract base + sealed per-state types — illegal transitions uncompilable.** `Status` is a derived discriminator only; not-yet-migrated entities need an explicit valid-transitions table. [rules.md §9.31]
- **Flat `<Entity>Record` + pure mapper for EF persistence of sum-type aggregates** — no TPH; `ToDomain()` (switch on `Status`), `ProjectOnto()`, `xmin` token, same-transaction audit. [rules.md §9.38]
- **NEVER hand-write DB migrations** — `dotnet ef migrations add <Name>`. [rules.md §9.10]
- **EF migration `.cs` excluded from StyleCop via `.editorconfig` `[**/Migrations/*.cs] generated_code = true`** — never suppress SA\* or hand-edit. [rules.md §26.9]
- **Never return `Ok()` unconditionally after a branching operation** — check the nested result. [rules.md §9.20]

### Caching

- **Inject one of `ILocalCache` / `IDistributedCache` / `ITieredCache`** from `D2.Shared.Caching.Abstractions`; use `*AndBroadcast*` writes when other instances cache the same key; every op returns `D2Result<T>`. [rules.md §16.3]

### Codegen discipline (generated files are reproducible — keep them that way)

- **NEVER hand-edit generated files** — fix the GENERATOR, the INPUT, or EXTEND the pipeline. "Generated" = `*.g.<ext>`, anything under `Generated/`, any documented-pipeline output (Roslyn source-gen, `tools/ts-codegen`, proto-derived, Drizzle migrations, Paraglide locales, Tier-2 specs like `contracts/geo/*.spec.json`), anything banner-marked. [rules.md §26.5]
- **Spec-mirror DTO types FORBIDDEN in destination assemblies** — autogen from the schema, OR move into source-gen internals under §26.2's conditions. [rules.md §26.1]
- **Hand-writing a DTO that mirrors a `.proto` / `.spec.json` / `.openapi.yaml` / `.graphql` shape in a published package = process-integrity failure.** [rules.md §26.1]
- **Error codes are SPEC-DECLARED** — every code in a `*-error-codes.spec.json` with `httpStatus` + `category` + a valid `userMessageKey`; constants, typed `D2Result` factories, and the merged registry are GENERATED. No free-text literals, no hand-mapped `Fail(statusCode, message)`, no hand-written `<Domain>Failures`. [rules.md §26.6]
- **Emitters reference the TK CONSTANT, never a string-literal key / symbol-path** — a `tk("TK.X.Y.Z")` path literal bypasses the catalog; ship the cross-runtime render test. [rules.md §26.7]
- **Generators validated independently — never "pending a consumer"** — integration tests drive the artifact against real shared libs + faithful §1.32 doubles; a committed ledger names each emitter's validation + each double's replace-trigger. [rules.md §26.15, §26.16]
- **Conversation/decision IDs banned across ALL generator surfaces** — the §14 / §14.3 sweep covers emitter source + emitted output + runtime messages + docs. [rules.md §26.17]
- **Hand-authored files MUST NOT carry a generated banner or `.g.*` extension** — normal header + plain extension + a ledger note. [rules.md §26.18]
- **Consumable shared packages versioned per-package** — semver + CHANGELOG from the build-free artifact diff (`tools/release-runner`); footers escalate but never lower the bump; wire/contract breaks auto-gated by `tools/contract-gate`; registry publishing never automatic. [rules.md §26.19]
- **After touching any consumable source, re-seed `.release-fingerprint` before committing** — `pnpm --filter release-runner check-baselines`; for `.NET` also promote `PublicAPI.Unshipped.txt`. Stale baseline = FINDING-HIGH. [rules.md §26.20]
- **Compare against emitted constants, never raw spec-emitted literals** — a `switch`/`==`/`is` over an error code references `KeyCustodianErrorCodes.KEYCUSTODIAN_*` (or `ErrorCodes.*`), never a hand-typed string; wire-value assertions exempt. [rules.md §26.21]
- **Closed-set telemetry tag keys / values / reason codes = named constants** — one source of truth at every emit / switch / `==` site. [rules.md §21.11]

> Route to a category (security / concurrency / disposal / D2Result / OOTB libs / logging / PII / graceful degradation / UX / DX / observability / idempotency / configuration / codegen) → [rules.md table of contents](docs/dev/rules.md#table-of-contents).

_Canonical: [rules.md](docs/dev/rules.md) (full §1–§26 predicate catalog with Evidence + Why + How per predicate). This §5 is the impossible-to-miss short list; rules.md is authoritative. Update both in lockstep per §11.32._

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

**Primary-constructor handlers**: constructor parameters do NOT take the `r_` prefix — they're parameters, not fields, even though accessed like fields inside the class body. Applies ONLY to handler primary-constructor parameters; regular fields keep their prefixes.

> Gotchas: folder casing inside-project = PascalCase, outside-project = lowercase; observability tags = camelCase (`traceId`, `correlationId`, `userId`, `orgId`, `service`); TS = camelCase methods / PascalCase types / kebab-case files; translation keys carry a domain prefix (`auth_*`, `webclient_*`, `common_*`); per-op handler naming = `I<Op>Handler` / `<Op>Handler` / `<Op>Input` / `<Op>Output`, file name = type name, co-located in the op folder ([rules.md §9.24](docs/dev/rules/09-architectural-layer-hygiene.md#9-architectural-layer-hygiene)); **test-only / fixture-only symbols carry a `Fixture` / `Fake` / `Stub` / `Test` / `Sample` marker in the LEAF name** (not just the namespace) — across fixtures, doubles, seams, AND generated fixture DTOs / proto-messages / enums / models / handlers / services / clients; drive the marker from the codegen SOURCE and regenerate, never hand-edit `.g.*`; SHARED real types a fixture merely consumes keep their real names ([rules.md §7.23](docs/dev/rules/07-naming-file-headers-folder-casing.md#7-naming-file-headers-folder-casing)). Full reference → [rules.md §7](docs/dev/rules/07-naming-file-headers-folder-casing.md#7-naming-file-headers-folder-casing).

_Canonical: [rules.md §7.1 Naming](docs/dev/rules/07-naming-file-headers-folder-casing.md#7-naming-file-headers-folder-casing) (+ TS naming, folder casing, file headers, observability fields, git conventions). The table above is at-a-glance duplication. Update both in lockstep per §11.32._

---

## §7. Behavioral Guidelines (dispositional — how to approach work)

> **⚠️ MANDATORY — equally binding as §4 (Patterns) and the [rules.md](docs/dev/rules.md) predicates. These shape HOW you work; the predicates govern WHAT the work looks like.**

1. **ALWAYS ask when uncertain** — non-negotiable. Don't guess, don't assume. The failure mode isn't "didn't ask when I knew I was uncertain" — it's "didn't notice I should be uncertain."
2. **Read freely** — explore any files needed for context. Reading is cheap.
3. **Ask before changing** — no file modifications without explicit user approval (per the PLAN gate).
4. **Research first** — check tests / interfaces / existing implementations before proposing; find a similar existing implementation before inventing.
5. **Follow existing conventions** — the patterns docs are the source of truth; don't invent when an established pattern applies. No pattern fits → ASK.
6. **Check the project tracking doc** (header) before starting — current phase, status, resolved decisions.
7. **Provide options** — present multiple approaches for user decision rather than silently picking one.
8. **Maximize parallelization** — spawn as many sub-agents as makes sense; run independent work (reads, doc updates, fixes, test runs, audits) in parallel, use background agents for non-blocking work.
9. **Effort asymmetry — fix small issues, don't defer them** — spot a minor issue (broken doc link, stale ref, formatting nit, small test gap, missed cleanup, drifted comment) → fix it in the same turn and mention it. Report-without-fixing ONLY when: (a) the user asked to audit / report only, (b) the fix is non-trivial / destructive, (c) it would balloon scope, or (d) it changes behavior the user must approve.

> **Predicates** (zero warnings, write tests, regression-pin every fix, never commit / defer without permission, etc.) live in [rules.md §13](docs/dev/rules/13-permission-action-discipline.md#13-permission--action-discipline) + elsewhere; walked each audit round.

### Code Intelligence + Windows LSP workaround

TypeScript via `mcp__cclsp__*` (`get_hover`, `find_definition`, `find_references`, `find_workspace_symbols`, `get_diagnostics`); C# via `csharp-ls` for `workspaceSymbol` + diagnostics (Grep / Glob / Read fallback — `hover` / `documentSymbol` time out on the large solution). The Windows cmd-wrap fix for `marketplace.json` must be reapplied after every `claude plugin marketplace update` → MEMORY.md "Code Intelligence (LSP)" + "Manual LSP Fix".

### Project Structure

Key roots: `contracts/` (proto source of truth + i18n messages + fixtures) · `server/` (all trusted code — .NET services + SvelteKit BFF + .NET shared libs) · `infra/` (deployment + observability) · `tools/` (dev tooling) · `docs/` (project documentation) · `secrets/` (gitignored + Claude-deny-ruled key material; populated by `tools/scripts/gen-dev-keys.sh`) · `.claude/` (project-level Claude Code settings with deny rules).

---

## §8. Local Secrets & multi-runtime deny map

Environment configuration is split:

| File | Contents | Committed? | Agent read? | Agent edit? |
|---|---|---|---|---|
| `.env.local` | Non-secret config — service URLs, ports, log levels, feature flags, CORS origins | No (gitignored) | **Yes** | **Yes** |
| `.env.local.example` | Template with safe defaults | **Yes** | Yes | Yes |
| `.env.secrets` | Real third-party creds — Twilio, Resend, IPinfo, OAuth client secrets, prod-like DB passwords | No (gitignored) | **No (deny-ruled)** | **No (deny-ruled)** |
| `.env.secrets.example` | Template with placeholders (`TWILIO_AUTH_TOKEN=replace_with_real_value`) | **Yes** | Yes | Yes |
| `secrets/` | Key material — root key, dev encryption keys, dev TLS certs | No (gitignored; populated by `tools/scripts/gen-dev-keys.sh`) | **No (deny-ruled)** | **No (deny-ruled)** |

Compose loads both env files (`.env.local` first, `.env.secrets` second so secrets override placeholders on any collision):

```yaml
services:
  edge:
    env_file:
      - .env.local
      - .env.secrets
```

**Adding a new secret**: (1) add `NEW_THING_API_KEY=replace_with_real_value` to `.env.secrets.example`; (2) wire it into the right service in `infra/compose/compose.yml`; (3) tell the operator to copy it into `.env.secrets`, set the real value, and restart the service. Agents cannot edit `.env.secrets` (deny rule). Same pattern for encryption keys: update `tools/scripts/gen-dev-keys.sh`; operator runs it; output lands in `secrets/`.

**Deny / structural map (multi-runtime):**

| Host | Secret-path deny | Commit / destructive-git backstop | Shared marker |
| --- | --- | --- | --- |
| Claude Code / Grok | `.claude/settings.json` Read/Write/Edit deny | `.claude/hooks/git-guard.sh` | `.claude/.commit-authorized` via `cycle-commit` |
| Codex | `.codex/hooks/d2-policy-guard.mjs` on PreToolUse matcher (Bash/patch/Edit/Write/Read-class); residual MCP-read gap → [harness-runtimes.md](docs/dev/harness-runtimes.md) | same `d2-policy-guard.mjs` | same marker |

The exact-match `**/.env.secrets` deliberately does NOT match `.env.secrets.example` — the template stays fully editable.

**Behavioral rule**: never `Grep` the `secrets/` directory or `.env.secrets` file by name. If a secret enters context (runtime output, grep match), STOP and tell the operator immediately so they can rotate the exposed value.
