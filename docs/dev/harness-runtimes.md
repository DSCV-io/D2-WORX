<!--
Copyright (c) DCSV. All rights reserved.
-->

# Harness runtimes — Claude Code · Grok Build · (future Codex)

**Shared law, split runtime.** Process, predicates, journals, and skill *behavior* are one system. Model pins, agent definition paths, and **spawn names** differ per AI harness. This doc is the dual-runtime map so neither Claude Code nor Grok Build rewrites the other.

> **Pattern:** IF you use **Claude Code** → use **X**. IF you use **Grok Build** → use **Y**. IF you use **Codex** (future) → use **Z**.  
> Do **not** edit another runtime's pin surface to "make yours work."

### Hard anti-crossfire rule

| Host | May spawn | Must not spawn |
| --- | --- | --- |
| **Claude Code** | `claude-d2-*` only | `grok-d2-*`, bare `d2-*` |
| **Grok Build** | `grok-d2-*` only | `claude-d2-*`, bare `d2-*` |

Cross-prefix dispatch is a process bug even if the host tool accepts the name — it can pin the wrong model, load the wrong mission body, or fail spawn. Prefer `explore` / `general-purpose` over guessing a name.

---

## Spawn-name rule (critical)

Agent `name:` fields (what `Agent` / `spawn_subagent` take as `subagent_type`) are **runtime-prefixed** so the two trees never share an identity:

| Runtime | Spawn name pattern | Files |
| --- | --- | --- |
| **Claude Code** | `claude-d2-<role>` | `.claude/agents/claude-d2-<role>.md` |
| **Grok Build** | `grok-d2-<role>` | `.grok/agents/grok-d2-<role>.md` |
| **Codex** (future) | `codex-d2-<role>` (proposed) | its pin tree |

Examples: `claude-d2-implementer` vs `grok-d2-implementer`. Role vocabulary in prose can still say "the Implementer"; **dispatch always uses the runtime-prefixed name**.

**Why:** Grok discovers both `.claude/agents` and `.grok/agents` under Claude-compat. Same bare `name: d2-planner` in both trees is a dual-registration hazard (list vs spawn mismatch). Distinct names remove that class of clash.

---

## What is shared vs runtime-owned

| Layer | Shared (all runtimes) | Runtime-owned |
| --- | --- | --- |
| Process | `docs/dev/process.md` | Dispatch tool name (`Agent` vs `spawn_subagent`); spawn name prefix |
| Predicates | `docs/dev/rules.md` + `docs/dev/rules/*` | Evidence of model ID strings in §24.0i attestations |
| Condensed law | `CLAUDE.md` (also loaded by Grok as project instructions) | This file for multi-runtime map |
| Skills (behavior + scripts) | `.claude/skills/*` (Grok loads via Claude compat) | Skill routing text must cite **both** spawn names |
| Agent **mission** | Same role set (Planner, Implementer, …) | Separate files + prefixed `name:` per runtime |
| Agent **model / effort pins** | Dual table below | Claude files vs Grok files |
| Hooks / deny | `.claude/settings.json` (Grok loads via Claude compat) | Grok-native hooks under `.grok/hooks/` only if needed |
| MCP | Optional | Claude vs Grok config surfaces |
| **Codebase-memory MCP** | Usage law: [codebase-memory.md](codebase-memory.md) (graph = discovery accelerator, **not** SoT; Grep still for §24.13.1 Evidence paste) | Server install / MCP JSON path per host |
| Memory | Optional | Per-product memory stores |

**Damage rule:** never change Claude agent model pins to Grok IDs (or vice versa). never collapse spawn names back to unprefixed `d2-*` while both trees exist.

---

## Model tier map (dual)

| Capability tier | Intent | Claude Code pin | Grok Build pin |
| --- | --- | --- | --- |
| **Planning / judgment premium** | Orchestrator, Planner, Plan-Auditor, Plan-amender | `claude-fable-5` (Planner `effort: max`; Plan-Auditor `xhigh`; others `high`) | `grok-4.5` · `effort: high` (4.5 ceiling is **high** — no xhigh/max on this model) |
| **Deep workhorse** | Aggregator, Auditor-deep (C2/C3/E2), Implementer, Fixer | `claude-opus-4-8` · `effort: high` | `grok-4.5` · `effort: high` |
| **Volume / tight-contract** | Mechanical Auditor, Fixer-mechanical, Investigator | `claude-sonnet-4-6` · high / medium | **`grok-4.5` · `medium`** (ex-Composer seats; high is overkill for volume) |

**Why Grok collapses all three tiers onto `grok-4.5`:** (1) strongest available seat for planning/deep; (2) **cost ban on `grok-composer-2.5-fast`** (user ruling 2026-07-09) — higher $/token than `grok-4.5` on this billing surface and burned ~5% of a weekly budget on a single K=12 deliverable audit wave; (3) live `grok models` lists only `grok-4.5` + `grok-composer-2.5-fast` (no non-fast Composer ID to pin). Role *fences* (mechanical vs deep auditor, Fixer-mechanical STOP) stay; product model does not differ.

**Do not dispatch `grok-composer-2.5-fast`** for any D2-WORX role until the user re-approves a volume seat after pricing changes.

**Sweeping carve-out (Implementer / Fixer):** criteria unchanged. On Claude: Opus → Fable. On Grok: still cite + self-attest even when model stays `grok-4.5`.

**Verify live IDs:** `grok models` → default `grok-4.5`; treat `grok-composer-2.5-fast` as **available-but-banned** for this project.

---

## How pinning works (Claude vs Grok)

### Claude Code

1. **Pin file:** `.claude/agents/claude-d2-<role>.md`
2. **Spawn:** `Agent` / subagent with `subagent_type: claude-d2-<role>` (e.g. `claude-d2-implementer`); model selector must match pin (§24.0i).
3. **Override:** §13.14 or Sweeping carve-out only.
4. **Attestation:** `Model: <claude-model-id>`.

### Grok Build

1. **Pin file:** `.grok/agents/grok-d2-<role>.md`
2. **Spawn:** `spawn_subagent` with `subagent_type: grok-d2-<role>` (e.g. `grok-d2-implementer`). **No separate model arg** — frontmatter is the pin. Never `model: inherit` on these roles.
3. **Override:** §13.14 only (no silent `[subagents.models]` overrides for D2 roles).
4. **Attestation:** `Model: grok-4.5` (all D2 roles; never attest composer-2.5-fast for this project).
5. **After pin/name changes:** restart the Grok session, then `grok inspect` + smoke spawn.

### Future Codex (Z)

`codex-d2-<role>` names + own pin tree; third column on tables; do not fold into Claude/Grok files.

---

## Per-role pin table (authoritative quick reference)

| Role | Claude spawn / file | Claude model · effort | Grok spawn / file | Grok model · effort |
| --- | --- | --- | --- | --- |
| Planner | `claude-d2-planner` / `claude-d2-planner.md` | `claude-fable-5` · max | `grok-d2-planner` / `grok-d2-planner.md` | `grok-4.5` · high |
| Plan-Auditor | `claude-d2-plan-auditor` | `claude-fable-5` · xhigh | `grok-d2-plan-auditor` | `grok-4.5` · high |
| Plan-amender | `claude-d2-plan-amender` | `claude-fable-5` · high | `grok-d2-plan-amender` | `grok-4.5` · high |
| Aggregator | `claude-d2-aggregator` | `claude-opus-4-8` · high | `grok-d2-aggregator` | `grok-4.5` · high |
| Auditor (mechanical) | `claude-d2-auditor` | `claude-sonnet-4-6` · high | `grok-d2-auditor` | `grok-4.5` · medium |
| Auditor-deep | `claude-d2-auditor-deep` | `claude-opus-4-8` · high | `grok-d2-auditor-deep` | `grok-4.5` · high |
| Implementer | `claude-d2-implementer` | `claude-opus-4-8` · high | `grok-d2-implementer` | `grok-4.5` · high |
| Fixer | `claude-d2-fixer` | `claude-opus-4-8` · high | `grok-d2-fixer` | `grok-4.5` · high |
| Fixer-mechanical | `claude-d2-fixer-mechanical` | `claude-sonnet-4-6` · medium | `grok-d2-fixer-mechanical` | `grok-4.5` · medium |
| Investigator | `claude-d2-investigator` | `claude-sonnet-4-6` · high | `grok-d2-investigator` | `grok-4.5` · medium |
| Orchestrator (main) | (session) | Fable 5 | (session) | `grok-4.5` · high |

---

## Operator checklist (after rename / pin change)

1. Restart Grok CLI (spawn registry + system prompt).
2. `grok inspect` — agents show `grok-d2-*` from `.grok/agents/`; no bare `d2-*` left.
3. Smoke: `grok-d2-investigator` / `grok-d2-auditor` / `grok-d2-fixer-mechanical` → expect **`grok-4.5` · medium** (not composer-2.5-fast; not high); `grok-d2-planner` / deep seats → expect `grok-4.5` · high.
4. Claude Code (when used): spawn `claude-d2-*` only.
5. Never commit without **per-occurrence** user permission for THIS commit. Take every commit through the sanctioned `cycle-commit` path (plants the one-shot `.claude/.commit-authorized` marker + EXIT-trap-removes it — the `git-guard` hook blocks any direct `git commit`); never a raw `git commit` without the marker. Do not add `Co-Authored-By` trailers.
6. If **codebase-memory-mcp** is connected: ensure project `D2-WORX` is indexed (`index_status` / `index_repository`) before heavy discovery work. Usage law → [codebase-memory.md](codebase-memory.md).

---

## Cross-links

- Process: [process.md](process.md)
- §24.0i: [rules/24-audit-evidence-discipline-meta-how-to-audit.md](rules/24-audit-evidence-discipline-meta-how-to-audit.md)
- Condensed law: [../../CLAUDE.md](../../CLAUDE.md)
- Claude pins: [../../.claude/agents/](../../.claude/agents/)
- Grok pins: [../../.grok/agents/](../../.grok/agents/)
