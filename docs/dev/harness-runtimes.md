<!--
Copyright (c) DCSV. All rights reserved.
-->

# Harness runtimes — Claude Code · Grok Build · Codex

**Shared law, split runtime.** Process, predicates, journals, and skill *behavior* are one system. Model pins, agent definition paths, and **spawn names** differ per AI harness. This doc maps Claude Code · Grok Build · Codex so they do not rewrite one another.

**0029-audit-token-discipline** does not change product model IDs or efforts (Grok remains `grok-4.5` at current efforts; composer still cost-banned; no retier).

> **Pattern:** IF you use **Claude Code** → use **X**. IF you use **Grok Build** → use **Y**. IF you use **Codex** → use **Z**.
> Do **not** edit another runtime's pin surface to "make yours work."

## One primary harness (default)

**Production / formal §24.0i-pinned D2 work uses exactly one active host that applies role pins** — today **Claude Code or Grok Build** — unless the user explicitly authorizes a multi-harness experiment. **Codex** pin trees, skills adapters, and hooks are **in-repo inventory + known-limits** (see [Codex known host limits](#codex) below): spawn may ignore TOML model/effort pins; concurrent slots may be ~4. Until native per-role selection is proven, **do not run formal §24.0i-pinned audit/impl waves on Codex** — use Claude or Grok for those waves. Pin trees for all three may exist; that is **not** permission to treat every host as a formal pin-enforcing peer. When the active host is set, spawn only that host's `*-d2-*` prefix.

## Hard anti-crossfire rule

| Host | May spawn | Must not spawn |
| --- | --- | --- |
| **Claude Code** | `claude-d2-*` only | `grok-d2-*`, `codex-d2-*`, bare `d2-*` |
| **Grok Build** | `grok-d2-*` only | `claude-d2-*`, `codex-d2-*`, bare `d2-*` |
| **Codex** | `codex-d2-*` only | `claude-d2-*`, `grok-d2-*`, bare `d2-*` |

Cross-prefix dispatch is a process bug even if the host tool accepts the name — it can pin the wrong model, load the wrong mission body, or fail spawn. Prefer `explore` / `general-purpose` over guessing a name.

---

## Spawn-name rule (critical)

Agent `name:` fields (what `Agent` / `spawn_subagent` / `spawn_agent` take as the role id) are **runtime-prefixed** so pin trees never share an identity:

| Runtime | Spawn name pattern | Files |
| --- | --- | --- |
| **Claude Code** | `claude-d2-<role>` | `.claude/agents/claude-d2-<role>.md` |
| **Grok Build** | `grok-d2-<role>` | `.grok/agents/grok-d2-<role>.md` |
| **Codex** | `codex-d2-<role>` | `.codex/agents/codex-d2-<role>.toml` |

Examples: `claude-d2-implementer` vs `grok-d2-implementer` vs `codex-d2-implementer`. Role vocabulary in prose can still say "the Implementer"; **dispatch always uses the runtime-prefixed name**.

**Why:** Grok discovers both `.claude/agents` and `.grok/agents` under Claude-compat. Same bare `name: d2-planner` in both trees is a dual-registration hazard (list vs spawn mismatch). Distinct names remove that class of clash.

---

## What is shared vs runtime-owned

| Layer | Shared (all runtimes) | Runtime-owned |
| --- | --- | --- |
| Process | `docs/dev/process.md` | Dispatch tool name (`Agent` vs `spawn_subagent` vs `spawn_agent`); spawn name prefix |
| Predicates | `docs/dev/rules.md` + `docs/dev/rules/*` | Evidence of model ID strings in §24.0i attestations |
| Condensed law | `AGENTS.md` | **Claude Code:** root `CLAUDE.md` is a **thin adapter** that `@AGENTS.md`-imports the shared law (not a second full body; not a fifth meta-doc). **Grok / Codex:** load `AGENTS.md` natively. Grok also loads `CLAUDE.md` if present — keep it thin so dual full copies cannot double-inject. |
| Skills (behavior + scripts) | `.claude/skills/*` canonical bodies | Grok loads Claude-compatible skills; Codex discovers `.agents/skills/*` adapters; routing text cites active-runtime prefixes |
| Agent **mission** | Same role set (Planner, Implementer, …) | Separate files + prefixed `name:` per runtime |
| Agent **model / effort pins** | Tier table below | Claude / Grok / Codex pin files |
| Hooks / deny | Shared policy in `rules.md` | Claude/Grok: `.claude/settings.json`; Codex: `.codex/hooks/*` + project config |
| MCP | Optional | Claude/Grok user config vs Codex `.codex/config.toml` |
| **Codebase-memory MCP** | Usage law: [codebase-memory.md](codebase-memory.md) (graph = discovery accelerator, **not** SoT; Grep still for §24.13.1 Evidence paste) | Server install / MCP JSON path per host |
| Memory | Optional | Per-product memory stores |

**Damage rule:** never change Claude agent model pins to Grok IDs (or vice versa). never collapse spawn names back to unprefixed `d2-*` while both trees exist.

---

## Model tier map

| Capability tier | Intent | Claude Code pin | Grok Build pin | Codex pin |
| --- | --- | --- | --- | --- |
| **Planning / judgment premium** | Orchestrator, Planner, Plan-Auditor, Plan-amender | `claude-fable-5` (Planner `max`; others `high`); **Plan-Auditor → `claude-opus-4-8` · `xhigh`** (cost ruling 2026-07-09 — Fable uneconomical at multi-seat Plan-Audit volume; K=7 max seats) | `grok-4.5` · `high` | `gpt-5.6-sol` (Planner `max`; Plan-Auditor `xhigh`; others `high`) |
| **Deep workhorse** | Aggregator, Auditor-deep (bundles C/D/G), Implementer, Fixer | `claude-opus-4-8` · `high` | `grok-4.5` · `high` | `gpt-5.6-sol` · `high` |
| **Volume / tight-contract** | Mechanical Auditor, Fixer-mechanical, Investigator | `claude-sonnet-4-6` · high / medium | `grok-4.5` · `medium` | `gpt-5.6-terra` · Auditor `high`; Investigator/Fixer-mechanical `medium` |

**Why Grok collapses all three tiers onto `grok-4.5`:** (1) strongest available seat for planning/deep; (2) **cost ban on `grok-composer-2.5-fast`** (user ruling 2026-07-09) — higher $/token than `grok-4.5` on this billing surface and burned ~5% of a weekly budget on a single large multi-seat deliverable audit wave (historical always-12; today's max is K=7); (3) live `grok models` lists only `grok-4.5` + `grok-composer-2.5-fast` (no non-fast Composer ID to pin). Role *fences* (mechanical vs deep auditor, Fixer-mechanical STOP) stay; product model does not differ.

**Do not dispatch `grok-composer-2.5-fast`** for any D2-WORX role until the user re-approves a volume seat after pricing changes.

**Sweeping carve-out (Implementer / Fixer):** criteria unchanged. On Claude: Opus → Fable. On Grok: still cite + self-attest even when model stays `grok-4.5`.

**Verify live IDs:** `grok models` → default `grok-4.5`; treat `grok-composer-2.5-fast` as **available-but-banned** for this project.

---

## How pinning works

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

### Codex

1. **Pin file:** `.codex/agents/codex-d2-<role>.toml` (documents **intended** model/effort per role; authoritative **only when the host applies the pin**).
2. **Spawn:** prefer host support for real `agent_type: codex-d2-<role>` selection when available; do not use a Claude/Grok prefix.
3. **Project config:** `.codex/config.toml` pins orchestrator defaults, aspirational agent thread caps, the `AGENTS.md` read ceiling, codebase-memory MCP, PreToolUse hooks, and MCP tool approval:
   - `max_threads = 8` — **aspiration** for K+1 with K≤7 (full K=7 Auditors + orchestrator); host may cap concurrent slots far lower (observed ~4) — see known limits.
   - `default_tools_approval_mode = "writes"` — MCP tools that **write** still require approval; read-only MCP tools are not auto-approved for free-form mutation. This is an operator footgun surface: it does **not** replace repository policy hooks or user permission gates.
   - `project_doc_max_bytes` — ceiling for project instruction injection (keep large `AGENTS.md` loadable).
4. **Override:** §13.14 or the Sweeping carve-out only.
5. **Attestation:** `Model: gpt-5.6-sol` or `Model: gpt-5.6-terra`, matching the role TOML **when the host actually applies that pin** (if spawn is label-only, attest the **actual** child model, not the unused TOML).
6. **After pin/config changes:** restart Codex before relying on discovery; a running thread does not retroactively reload project config.
7. **Hooks / deny map:** PreToolUse → `.codex/hooks/d2-policy-guard.mjs` (same `.claude/.commit-authorized` marker as Claude/Grok `git-guard`; blocks commit/destructive git without marker; blocks deny-ruled secret paths on matched tools). SessionStart compact → `post-compact-context.mjs`. Matcher covers Bash / apply_patch / Edit / Write / Read-class names used by the host; **residual:** pure MCP filesystem reads outside the matcher are behavioral-only (Claude settings deny still covers Claude/Grok Read).

**Known host limits (eval 2026-07-09 — re-verify after product updates):** some Codex builds treat `spawn_agent(task_name=…)` as a **label only** (child inherits the parent model/effort; TOML pins are not applied). Concurrent agent slots may also be far below a configured `max_threads` aspiration (observed ~4, not 8). Until native per-role model selection is proven, **do not use Codex for formal §24.0i-pinned D2 audit/impl waves** — keep formal work on a host that honors pins (Claude Code or Grok Build), or build an explicit `codex exec` dispatcher that sets model/effort per role (separate harness deliverable). Codex inventory remains valid for config/hooks/smoke eval.

---

## Per-role pin table (intended pins — authoritative when the host applies them)

| Role | Claude model · effort | Grok model · effort | Codex model · effort |
| --- | --- | --- | --- |
| Planner | `claude-fable-5` · max | `grok-4.5` · high | `gpt-5.6-sol` · max |
| Plan-Auditor | `claude-opus-4-8` · xhigh | `grok-4.5` · high | `gpt-5.6-sol` · xhigh |
| Plan-amender | `claude-fable-5` · high | `grok-4.5` · high | `gpt-5.6-sol` · high |
| Aggregator | `claude-opus-4-8` · high | `grok-4.5` · high | `gpt-5.6-sol` · high |
| Auditor (mechanical) | `claude-sonnet-4-6` · high | `grok-4.5` · medium | `gpt-5.6-terra` · high |
| Auditor-deep | `claude-opus-4-8` · high | `grok-4.5` · high | `gpt-5.6-sol` · high |
| Implementer | `claude-opus-4-8` · high | `grok-4.5` · high | `gpt-5.6-sol` · high |
| Fixer | `claude-opus-4-8` · high | `grok-4.5` · high | `gpt-5.6-sol` · high |
| Fixer-mechanical | `claude-sonnet-4-6` · medium | `grok-4.5` · medium | `gpt-5.6-terra` · medium |
| Investigator | `claude-sonnet-4-6` · high | `grok-4.5` · medium | `gpt-5.6-terra` · medium |
| Orchestrator (main) | Fable 5 | `grok-4.5` · high | `gpt-5.6-sol` · xhigh |

Spawn/file names are the runtime prefix plus the role: Claude `.md`, Grok `.md`, Codex `.toml`.

---

## Operator checklist (after rename / pin change)

1. Restart the runtime whose config or pins changed (spawn registry + system prompt).
2. `grok inspect` — agents show `grok-d2-*` from `.grok/agents/`; no bare `d2-*` left.
3. Smoke: `grok-d2-investigator` / `grok-d2-auditor` / `grok-d2-fixer-mechanical` → expect **`grok-4.5` · medium** (not composer-2.5-fast; not high); `grok-d2-planner` / deep seats → expect `grok-4.5` · high.
4. Claude Code: spawn `claude-d2-*` only. Codex: spawn `codex-d2-*` only and verify `codex mcp list` shows `codebase-memory-mcp` enabled.
5. Never commit without **per-occurrence** user permission for THIS commit. Take every commit through the sanctioned `cycle-commit` path (plants the one-shot `.claude/.commit-authorized` marker + EXIT-trap-removes it). Structural backstops: Claude/Grok → `git-guard` (+ `.claude/settings.json` deny); Codex → `d2-policy-guard.mjs` (same marker). Never a raw `git commit` without the marker. Do not add `Co-Authored-By` trailers.
6. If **codebase-memory-mcp** is connected: the orchestrator resolves `MCP_PROJECT` by canonical Git root once per session/dispatch, injects it into every sub-agent brief, and ensures it is indexed; roles consume only the dispatch-provided value and, if missing, fail closed/report and use disk (`index_status` / `index_repository`) before heavy discovery work. Usage law → [codebase-memory.md](codebase-memory.md).

---

## Cross-links

- Condensed law: [../../AGENTS.md](../../AGENTS.md) · Claude adapter: [../../CLAUDE.md](../../CLAUDE.md)
- Process: [process.md](process.md)
- §24.0i: [rules/24-audit-evidence-discipline-meta-how-to-audit.md](rules/24-audit-evidence-discipline-meta-how-to-audit.md)
- Codebase memory: [codebase-memory.md](codebase-memory.md)
- Claude pins: [../../.claude/agents/](../../.claude/agents/)
- Grok pins: [../../.grok/agents/](../../.grok/agents/)
- Codex pins: [../../.codex/agents/](../../.codex/agents/)
