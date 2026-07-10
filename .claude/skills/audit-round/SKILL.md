---
name: audit-round
description: K=7 audit-round dispatch pack - concern bundles A-G (universal max), dirty-only re-dispatch, reading lists, agent routing, dispatch-brief and Aggregator skeletons. Use when dispatching an audit or final-review round. Keywords - audit, K=7, cluster, bundle, atom provenance, auditor, aggregator, sweep, dispatch, round, dirty-only.
---

<!--
Copyright (c) DCSV. All rights reserved.
-->

# audit-round — K=7 dispatch pack (universal max)

MIRROR of `docs/dev/process.md §3` (Auditor cluster partition + Aggregator role) and `§4` (dispatch-brief template). **`process.md` is canonical — if this and it disagree, process.md wins; update both in lockstep (§11.32).**

> **Canonical one-liner:** Default full audit partition is **K=7 concern bundles (A–G)**. **K=12 atomic dispatch is retired.** Targeted Y and dirty-only re-dispatch apply on per-step rounds; **FINAL-REVIEW of a deliverable is full K=7 at deliverable scope.**

## Defaults

| Mode | K | Seats | When |
| --- | --- | --- | --- |
| **Full partition** (Plan-Audit R1 in-scope, justified full code-audit, **FINAL-REVIEW**) | **K=7** | Bundles **A–G** | Complete catalog walk |
| **Targeted first code-audit** | **Y ⊆ K=7** | Journal-justified subset of A–G | Per-step, step-relevant only |
| **Re-round after findings** | dirty subset | Only seats with ≥1 finding + sister-blast | Plan-Audit AND code-audit AND within FINAL-REVIEW after findings |

One audit round = K parallel seat Auditors (READ-ONLY) + 1 Aggregator + (if findings) 1 Fixer. Every round is a BRAND-NEW batch — fresh context is the point. **K=1** needs explicit per-round user permission (§24.0h); never self-invoked. **Dirty-only is not K=1.** **K=12 atomic dispatch is retired.**

**Provenance:** atoms A1…E3 stay stable §-ownership IDs only — **not** a dispatch mode and **not** "FINAL K=12". Bundles are the only dispatch seats. Prefer one partial per **bundle**; finding IDs use seat/bundle code + §-number.

**First code-audit** may dispatch **Y ⊆ K=7** (step-relevant bundles) with journal justification. Plan-Audit R1 for in-scope steps defaults full K=7 unless carve-out. **FINAL-REVIEW opens at full K=7** (whole deliverable scope — not dirty-only of the last step).

## K=7 concern bundles (universal full partition)

| Bundle | Atoms | rules.md § | Theme | Tier | Category files (under `docs/dev/rules/`) |
| --- | --- | --- | --- | --- | --- |
| **A** | A1+A2 | §1,2,4,15,18,22 | Correctness | mechanical | 01, 02, 04, 15, 18, 22 |
| **B** | B1+B2+B3 | §5,6,7,12,16,17 | Conventions + lib/D2Result | mechanical | 05, 06, 07, 12, 16, 17 |
| **C** | C1+C2 | §3,8,9 | PII/ops + architecture | **deep** | 03, 08, 09 |
| **D** | C3 | §10,13 | Security + permissions **PURE** | **deep** | 10, 13 |
| **E** | D1 | §11,14 | Docs + verbiage **PURE** | mechanical | 11, 14 |
| **F** | E1+E3 | §19–21,23,25,26 | Ops quality + temporal/codegen | mechanical (deep if ruling-critical codegen) | 19, 20, 21, 23, 25, 26 |
| **G** | E2 | §24 | Audit-meta **PURE** | **deep** | 24 |

Every seat ALSO reads the index-level Deliverable completeness checklist. Mapping is §-number → atom → bundle; Aggregator resolves straddles.

**Atom → bundle (provenance only):** A1+A2→A · B1+B2+B3→B · C1+C2→C · C3→D · D1→E · E1+E3→F · E2→G.

## Historical atom IDs (not a dispatch mode)

Atoms A1…E3 are §-ownership / provenance IDs for reading old journals. **Do not dispatch 12 atomic seats.**

| Atom | Name | rules.md § | Category files |
| --- | --- | --- | --- |
| A1 | Tests / coverage + regression | §1, §2 | 01-test-discipline, 02-bug-fix-regression-testing |
| A2 | Races, disposal, degradation, idempotency | §4, §15, §18, §22 | 04, 15, 18, 22 |
| B1 | C# conventions | §5 | 05-csharp-code-conventions |
| B2 | TS conventions + naming + i18n | §6, §7, §12 | 06, 07, 12 |
| B3 | Shared-lib hygiene + D2Result | §16, §17 | 16, 17 |
| C1 | PII/logging + operations | §3, §8 | 03, 08 |
| C2 | Architectural layer | §9 | 09-architectural-layer-hygiene |
| C3 | Security + permissions | §10, §13 | 10, 13 |
| D1 | KEEP doc parity + verbiage hygiene | §11, §14 | 11, 14 |
| E1 | UX + DX + observability + config | §19, §20, §21, §23 | 19, 20, 21, 23 |
| E2 | Audit-meta | §24 | 24-audit-evidence-discipline-meta-how-to-audit |
| E3 | Temporal + codegen | §25, §26 | 25, 26 |

## Agent-type routing

Spawn names are **runtime-prefixed** (full table → [docs/dev/harness-runtimes.md](../../../docs/dev/harness-runtimes.md)):

| Role | Claude Code spawn | Grok Build spawn | Codex spawn | Model tier (when host applies pin) |
| --- | --- | --- | --- | --- |
| Mechanical auditor | `claude-d2-auditor` | `grok-d2-auditor` | `codex-d2-auditor` | Sonnet / Grok 4.5 / Terra |
| Deep auditor (bundles C/D/G + ruling-critical) | `claude-d2-auditor-deep` | `grok-d2-auditor-deep` | `codex-d2-auditor-deep` | Opus / Grok 4.5 / Sol |
| Aggregator | `claude-d2-aggregator` | `grok-d2-aggregator` | `codex-d2-aggregator` | Opus / Grok 4.5 / Sol |
| Fixer | `claude-d2-fixer` | `grok-d2-fixer` | `codex-d2-fixer` | Opus / Grok 4.5 / Sol |
| Fixer-mechanical | `claude-d2-fixer-mechanical` | `grok-d2-fixer-mechanical` | `codex-d2-fixer-mechanical` | Sonnet / Grok 4.5 / Terra |
| Planner / Plan-Auditor / Plan-amender / Investigator / Implementer | `claude-d2-<role>` | `grok-d2-<role>` | `codex-d2-<role>` | see harness-runtimes |

- Mechanical seats: **A / B / E / F** (unless F ruling-critical codegen).
- Judgment-heavy deep seats: **C, D, G** + any seat flagged ruling-fidelity-critical. Role CHOICE, not escalation.
- FINAL-REVIEW reuses auditor definitions at deliverable scope (no separate final-reviewer); seats remain bundles A–G.
- **Never cross prefixes** — Claude must not spawn `grok-d2-*` / `codex-d2-*`; Grok must not spawn `claude-d2-*` / `codex-d2-*`; Codex must not spawn `claude-d2-*` / `grok-d2-*`; never bare `d2-*`.
- **Formal §24.0i-pinned waves** (multi-seat K≤7 + model/effort honesty): only on hosts that **apply** role pins (Claude Code / Grok Build today). Codex pin trees are inventory; spawn may be label-only and concurrency may be ~4 — see [harness-runtimes.md](../../../docs/dev/harness-runtimes.md) known limits before treating Codex as a formal peer.

## Flag-routing conventions (review-flag classes → seat)
Route each user/review flag by §-number → atom → bundle: PII/log-leak → C1→C; layer-violation / EF-DDD / handler-shape → C2→C; auth/secret/permission → C3→D; doc-drift / phase-verbiage / conversation-ID → D1→E; codegen / spec-mirror / baseline → E3→F; audit-evidence integrity → E2→G; test-gap / missing-regression → A1→A. Cross-cutting flags belong to the Aggregator, not a single seat.

## Auditor dispatch-brief skeleton
- **Role + scope**: seat code + its §-range; file scope = the step's touched paths (or `git diff --name-only` recipe) / whole deliverable at final-review.
- **Reading list**: this seat's category files + the completeness checklist + the round shared-context file. Reads ONLY what the brief names (no conversation memory).
- **Working-tree note**: read the on-disk WORKING TREE, not `git show HEAD:` — latest Implementer/Fixer output is uncommitted (§24.19).
- **Code discovery (when MCP available)**: prefer `codebase-memory-mcp` (use dispatch-provided `MCP_PROJECT` (orchestrator resolves by canonical Git root per `docs/dev/codebase-memory.md`); if missing, fail closed/report and use disk) — `search_graph` / `search_code` (files|compact) — over Grep/Glob to **locate** symbols and files in scope. Graph is **not** SoT ([docs/dev/codebase-memory.md](../../../docs/dev/codebase-memory.md)). Cap `trace_path` depth; do not dump high-fan-in callers into the partial.
- **Evidence-paste mandate (§24.13.1)**: still paste the LITERAL grep/shell command + output into the partial when the predicate Evidence line / checklist requires it — graph QNs are not a substitute. PASS rows need file:line, N/A rows a scope-specific reason, FINDING rows severity + file:line + description + fix; Status prepends ✅/❌/⚪/🟡.
- **Anti-laziness preamble (verbatim)**: WALK EVERY NUMBERED SUBSECTION, no skipping; regex is a TOOL not source of truth (§24.13.2); sister-sweep at full predicate applicability (§24.13.3).
- **Partial path**: `audit-rN/rN-partial-<BUNDLE>-<name>.md` (e.g. `A-correctness`) for mid-step and FINAL-REVIEW.
- **Constraints**: READ-ONLY (no Edit/NotebookEdit; no nested sub-agent spawn); no commits; never touch another Auditor's partial. Open the return with the model-attestation block. ≤N-line return.

## Aggregator dispatch skeleton
- Read all **K** partials for the round (≤7 full partition, or dirty-seat count).
- Merge the K big-table chunks into ONE sorted-by-§ table, REPLACING `## Latest sweep results` (anti-laziness preamble above it). Dirty-only: fold dirty partials over prior clean seats.
- Append one `### Round N findings (timestamp)` to the append-only findings log; fold Fixer logs into the append-only Fix log; **name dirty seats** for the next re-dispatch.
- Cross-cluster verification + cross-cluster sister-sweep (no single Auditor can see these).
- Cannot flip a per-seat verdict unilaterally (add cross-cluster findings yes; overrule no — escalate ties). Cannot mark CLEAN — closure is proven by ABSENCE from the NEXT round's big table.

## MANDATORY — fix work-packages enumerate EVERY finding ID
A fix dispatch MUST list every finding ID from the consolidated round (H+M+L), each with its own remediation line. A prior fix work-package that omitted a finding caused a carryover — never summarize "and the rest"; enumerate all of them.
