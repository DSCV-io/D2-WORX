---
name: audit-round
description: The K=12 audit-round dispatch pack - cluster partition, per-cluster reading lists, agent routing, dispatch-brief and Aggregator skeletons. Use when dispatching an audit or final-review round. Keywords - audit, K=12, cluster, auditor, aggregator, sweep, dispatch, round.
---

<!--
Copyright (c) DCSV. All rights reserved.
-->

# audit-round — K=12 dispatch pack

MIRROR of `docs/dev/process.md §3` (Auditor cluster partition + Aggregator role) and `§4` (dispatch-brief template). **`process.md` is canonical — if this and it disagree, process.md wins; update both in lockstep (§11.32).**

One audit round = 12 parallel cluster Auditors (READ-ONLY) + 1 Aggregator + (if findings) 1 Fixer. Every round is a BRAND-NEW batch — fresh context is the point. K=1 needs explicit per-round user permission (§24.0h); never self-invoked.

## Cluster partition (fixed — same §-range → same cluster code across deliverables)
| Cluster | Name | rules.md § | Category files to read (under `docs/dev/rules/`) |
| --- | --- | --- | --- |
| A1 | Tests / coverage + regression | §1, §2 | 01-test-discipline, 02-bug-fix-regression-testing |
| A2 | Races, disposal, degradation, idempotency | §4, §15, §18, §22 | 04-concurrency-race-conditions, 15-object-disposal-resource-lifetime, 18-graceful-degradation-failure-modes, 22-idempotency-exactly-once-semantics |
| B1 | C# conventions | §5 | 05-csharp-code-conventions |
| B2 | TS conventions + naming + i18n | §6, §7, §12 | 06-typescript-sveltekit-code-conventions, 07-naming-file-headers-folder-casing, 12-i18n-discipline |
| B3 | Shared-lib hygiene + D2Result | §16, §17 | 16-ootb-shared-lib-tooling-use-whats-there, 17-d2result-usage-extensions |
| C1 | PII/logging + operations | §3, §8 | 03-pii-logging-safety, 08-build-tooling-hygiene |
| C2 | Architectural layer | §9 | 09-architectural-layer-hygiene |
| C3 | Security + permissions | §10, §13 | 10-security-endpoints-auth-secrets-input, 13-permission-action-discipline |
| D1 | KEEP doc parity + verbiage hygiene | §11, §14 | 11-documentation-parity-best-practices, 14-phase-audit-conversation-verbiage-hygiene |
| E1 | UX + DX + observability + config | §19, §20, §21, §23 | 19-user-experience-ux, 20-developer-experience-dx, 21-observability-completeness, 23-configuration-hygiene |
| E2 | Audit-meta | §24 | 24-audit-evidence-discipline-meta-how-to-audit |
| E3 | Temporal + codegen | §25, §26 | 25-temporal-types-date-time-clock, 26-codegen-discipline-spec-proto-schema-derived-types |

Every cluster ALSO reads the index-level Deliverable completeness checklist. When a predicate seems to straddle clusters, the mapping is §-number → cluster (NOT topic → cluster); the Aggregator resolves straddles.

## Agent-type routing

Spawn names are **runtime-prefixed** (full table → [docs/dev/harness-runtimes.md](../../../docs/dev/harness-runtimes.md)):

| Role | Claude Code spawn | Grok Build spawn | Model tier |
| --- | --- | --- | --- |
| Mechanical auditor | `claude-d2-auditor` | `grok-d2-auditor` | Sonnet / Composer |
| Deep auditor (C2/C3/E2 + ruling-critical) | `claude-d2-auditor-deep` | `grok-d2-auditor-deep` | Opus / Grok 4.5 |
| Aggregator | `claude-d2-aggregator` | `grok-d2-aggregator` | Opus / Grok 4.5 |
| Fixer | `claude-d2-fixer` | `grok-d2-fixer` | Opus / Grok 4.5 |
| Fixer-mechanical | `claude-d2-fixer-mechanical` | `grok-d2-fixer-mechanical` | Sonnet / Composer |
| Planner / Plan-Auditor / Plan-amender / Investigator / Implementer | `claude-d2-<role>` | `grok-d2-<role>` | see harness-runtimes |

- Mechanical clusters (A1, A2, B1, B2, B3, C1, D1, E1, E3) → mechanical auditor row.
- Judgment-heavy **C2 (arch layer), C3 (security), E2 (audit-meta)** + any cluster the orchestrator flags ruling-fidelity-critical → deep auditor row. This split is a role CHOICE, not an escalation.
- FINAL-REVIEW reuses the auditor definitions at deliverable scope (no separate final-reviewer).
- **Never** spawn the other runtime's prefix (Claude must not spawn `grok-d2-*`; Grok must not spawn `claude-d2-*`).

## Flag-routing conventions (review-flag classes → cluster)
Route each user/review flag to the cluster owning its §-number: PII/log-leak → C1; layer-violation / EF-DDD / handler-shape → C2; auth/secret/permission → C3; doc-drift / phase-verbiage / conversation-ID → D1; codegen / spec-mirror / baseline → E3; audit-evidence integrity → E2; test-gap / missing-regression → A1. Cross-cutting flags belong to the Aggregator, not a single cluster.

## Auditor dispatch-brief skeleton
- **Role + scope**: cluster code + its §-range; file scope = the step's touched paths (or `git diff --name-only` recipe) / whole deliverable at final-review.
- **Reading list**: this cluster's category files + the completeness checklist + the round shared-context file. Reads ONLY what the brief names (no conversation memory).
- **Working-tree note**: read the on-disk WORKING TREE, not `git show HEAD:` — latest Implementer/Fixer output is uncommitted (§24.19).
- **Evidence-paste mandate (§24.13.1)**: paste the LITERAL grep command + output into the partial; PASS rows need file:line, N/A rows a scope-specific reason, FINDING rows severity + file:line + description + fix; Status prepends ✅/❌/⚪/🟡.
- **Anti-laziness preamble (verbatim)**: WALK EVERY NUMBERED SUBSECTION, no skipping; regex is a TOOL not source of truth (§24.13.2); sister-sweep at full predicate applicability (§24.13.3).
- **Partial path**: `audit-rN/rN-partial-<CLUSTER>-<cluster-name>.md`.
- **Constraints**: READ-ONLY (no Edit/NotebookEdit; no nested sub-agent spawn); no commits; never touch another Auditor's partial. Open the return with the model-attestation block. ≤N-line return.

## Aggregator dispatch skeleton
- Read all 12 partials (`rN-partial-{A1..E3}-*.md`).
- Merge the 12 big-table chunks into ONE sorted-by-§ table, REPLACING `## Latest sweep results` (anti-laziness preamble above it).
- Append one `### Round N findings (timestamp)` to the append-only findings log; fold Fixer logs into the append-only Fix log.
- Cross-cluster verification + cross-cluster sister-sweep (no single Auditor can see these).
- Cannot flip a per-cluster verdict unilaterally (add cross-cluster findings yes; overrule no — escalate ties). Cannot mark CLEAN — closure is proven by ABSENCE from the NEXT round's big table.

## MANDATORY — fix work-packages enumerate EVERY finding ID
A fix dispatch MUST list every finding ID from the consolidated round (H+M+L), each with its own remediation line. A prior fix work-package that omitted a finding caused a carryover — never summarize "and the rest"; enumerate all of them.
