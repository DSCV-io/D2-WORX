<!--
Copyright (c) DCSV. All rights reserved.
Committed snapshot of the gitignored deliverable-0029 workspace root
(docs/wip/0029-audit-token-discipline/README.md), captured at SHIP 2026-07-10.
Per-step journals remain local-only under docs/wip/0029-audit-token-discipline/ (gitignored).
-->

# 0029 — Audit token discipline (compact evidence + fat steps + wave policy)

**Status:** SHIPPED 2026-07-10 (FR_COLLAPSED — step CLEAN Y-audit is deliverable gate)  
**Branch:** `nova` (working tree at SHIP; commit on user approval)  
**Type:** Meta / process — no product code

## Goal

Cut audit and process token cost **without** weakening anti-laziness evidence: compact partials and journals, shared-context as SoT, Y/dirty/FR mode wave policy, fat-step planning default, orchestrator hygiene. Grok model IDs and effort pins **unchanged**.

## What shipped

| Surface | Change |
| --- | --- |
| `docs/dev/process.md` | Compact evidence SoT + 3-layer partial + N/A reason codes; shared-context mandatory sections; slim briefs; Aggregator full-catalog + short return; **Audit wave policy** matrix; fat-step PLAN law; FR_FULL / FR_LITE / FR_COLLAPSED; FR-mode SHIP; Plan-Audit three-way (Skip \| Y+Aggregator \| full K=7); dirty-only one-law with §24 |
| `docs/dev/rules/24-…md` | §24.0e / 0f / 6 dirty-only + full-catalog; §24.0h FR modes; §24.1/2/3/5/8/10/13.2 compact + full-catalog under Y; §24.16 three-way Plan-Audit |
| `docs/dev/rules.md` | Glossary (FR modes, Evidence ledger); completeness FR_COLLAPSED + pure-meta gate N/A |
| `.claude/skills/audit-round/SKILL.md` | Mirror compact / wave / slim brief / Aggregator |
| `AGENTS.md` | Condensed lockstep (compact evidence, wave/FR modes, fat-step PLAN-only boundary, dirty-only re-round wording) |
| Agent briefs ×3 runtimes | Auditor / auditor-deep / aggregator / plan-auditor mission bodies only — **no model/effort pin changes** |
| `docs/dev/harness-runtimes.md` | One-liner: 0029 does not retier models |

## Process integrity (this deliverable)

- **1-step** fat step (D9 exemplar): Plan → Plan-Audit Y=E+G → Implement → code-audit Y=E+G → dirty-only re-rounds → SHIP under **FR_COLLAPSED** (no separate FR journal).
- Plan-Audit: R1 AMEND → R2 M findings → R3 CLEAN.
- Code-audit: R1 (2M) → R2 (2M residual) → R3 (1M fix-log fold) → **R4 CLEAN** full-catalog zero FINDING.
- Models: all `grok-4.5` at pre-existing efforts; composer-2.5-fast still cost-banned.

## Locked decisions (summary)

| # | Choice |
| --- | --- |
| D1 | 1-stepper |
| D2 | FR_COLLAPSED for this deliverable |
| D3–D4 | Plan-Audit + code-audit Y=E+G |
| D5 | No model/effort retier |
| D6–D8 | Compact evidence; shared-context SoT; wave matrix + FR modes |
| D9 | Fat steps by default (mechanical split only) |
| D10–D11 | Pilot on next product deliverable; no preflight script |

## Kinds of misses (distilled)

| Class | Example | Candidate rule / note |
| --- | --- | --- |
| Residual dual-law prose | Absolute “no PASS inheritance” vs dirty-only Aggregator re-cite | Whole-doc residual rewrite inventory (Plan T12) — keep |
| Sidecar fix log not folded | `rN-fixer-log.md` before journal Fix log | Aggregator must fold Fixer logs same round (§24.0g) |
| Incomplete Implementer pre-flight | Paraphrased checklist | §24.13.1 full paste on meta steps too |

## Final report

Shipped process/rules/skills/agent-brief lockstep for cheaper audits with preserved evidence discipline. Next product deliverable should measure token/seat cost under Y-default + compact partials; adjust FR_LITE gates only with evidence.
