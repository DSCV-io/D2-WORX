<!--
Copyright (c) DCSV. All rights reserved.
-->

# Codebase memory (MCP) — agent discovery playbook

**Status:** optional accelerator in active use on Grok (and any host with the server). **Not source of truth.**  
**Canonical product:** [DeusData/codebase-memory-mcp](https://github.com/DeusData/codebase-memory-mcp) (upstream README = tool surface + install; **this file = D2-WORX usage law**).

Lockstep: [process.md §3](process.md#3-sub-agent-architecture) (tool access) · [harness-runtimes.md](harness-runtimes.md) (MCP row) · agent universal constraints · [audit-round skill](../../.claude/skills/audit-round/SKILL.md). Predicate evidence remains [rules §24.13.1–.2](rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit).

---

## Standing law

1. **Graph ≠ truth.** Authority = **working tree on disk** + KEEP docs + journals + `rules.md`. Graph spans, rankings, and call edges can be stale, over-wide, or test-biased.
2. **Prefer graph over Grep/Glob for discovery** when the MCP is connected and project **`D2-WORX`** is `ready` (`index_status` / `list_projects`). Goal: fewer tool hops + smaller tool-result payloads (upstream claims ~10×–120× fewer tokens vs file-by-file exploration — treat as motivation, not a meter).
3. **Grep is still mandatory when §24 / Evidence demands a literal command paste** (§24.13.1 checklist, sister-sweeps, Implementer pre-flight). Pattern: **graph narrows → Grep/shell proves → Read confirms spirit** (§24.13.2). Never replace a required Evidence grep with “search_graph said so.”
4. **Never Grep/read `secrets/` or `.env.secrets`** (unchanged; graph must not be used to pull secret paths into context either).
5. **No commit of team graph artifacts unless the user asks.** Local index lives under the MCP cache (`~/.cache/codebase-memory-mcp/` or host override). Do not add `.codebase-memory/graph.db.zst` by default.

---

## Project / index

| Item | Value |
| --- | --- |
| `project` arg | **`D2-WORX`** (always pass it) |
| First-time / empty | `index_repository({ repo_path: <abs repo root>, mode: "full", name: "D2-WORX" })` |
| Health | `index_status({ project: "D2-WORX" })` → `status: ready` |
| After huge WT / branch hop | Re-index or rely on watcher if `auto_watch` is on; if results look wrong vs disk, re-index before trusting |
| Modes | `full` = all files + similarity/semantic; `moderate` = filtered + semantic; `fast` = no semantic. Prefer **`full`** for this monorepo unless re-index cost forces otherwise |

MCP tool names are host-qualified (e.g. `codebase-memory-mcp__search_graph`). Discover schemas via the host’s MCP catalog; do not invent params.

---

## Tool choice (default path)

| Need | Prefer | Avoid / limit |
| --- | --- | --- |
| Find **definition** / symbol / type | `search_graph` (`query` or `name_pattern`, + `file_pattern` / `label`) | Blind repo-wide Grep |
| Find **files** with a string | `search_code` **`mode: "files"`** | Grep full dumps |
| Ranked call-site / function bags | `search_code` **`mode: "compact"`** + low `limit` | `mode: "full"` unless one hit |
| One function body | `search_graph` → `get_code_snippet(qualified_name)` then **disk Read** if span looks wrong | Whole-file Read first |
| Who calls X / impact | `trace_path` **depth 1–2**, `include_tests: false` unless tests are in scope | Unbounded inbound on high fan-in (e.g. `Falsey`) |
| Uncommitted blast radius | `detect_changes` | Manual guess of touch set |
| Architecture orientation | `get_architecture` with **narrow `aspects`** / `path` | `aspects: ["all"]` mid-audit |
| Complex multi-hop | `query_graph` (Cypher subset; always `LIMIT`) | Open-ended MATCH |
| Exact string / unindexed / generated noise / §24 Evidence paste | **Grep** (or shell) as today | Claiming graph closed the Evidence row alone |

**Token burn (no built-in meter):** burn ≈ size of tool results in context. Proxies: `search_code`’s `total_grep_matches` vs returned rows / `dedup_ratio`; keep `limit` low; prefer `files` ≪ `compact` ≪ `full`; never dump high-fan-in `trace_path` into a partial.

---

## Audit / Plan-Audit / Fixer discipline

- **Discovery:** graph-first within the step’s path scope (`file_pattern` / `path_filter` = step touch set or package root).
- **Evidence rows:** still paste **literal** Grep/shell + stdout when the predicate’s Evidence line or §24.13.1 checklist requires it. Graph QNs and line hints may appear *in addition*, not instead.
- **PASS spirit:** open the file on disk (§24.13.2). Graph rank ≠ manual read.
- **Sister-sweep / Fixer self-grep:** still full deliverable-diff Grep per §24.13.3–.4. Graph may help enumerate symbols; scope authority remains `git diff --name-only` + predicate applicability.
- **Orchestrator dispatch briefs:** may say “use codebase-memory for discovery; Evidence greps still literal.” Do not drop Grep from Auditor/Implementer tool access.

---

## Known D2-WORX caveats (from smoke)

- Broad BM25 can **prefer tests** over production types — add `file_pattern`, `label` (`Class`/`Method`/`Function`), or path under `server/shared/...` production trees.
- C# 14 extension members: graph finds definitions Grep patterns like `public static bool X` can miss.
- `get_code_snippet` end lines can over-read adjacent members — verify short methods on disk.
- Package labels in architecture overview are coarse (`shared` / `services`); use path filters for precision.

---

## When MCP is missing or index not ready

Fall back to Grep/Glob/Read as before. Do **not** block a deliverable on indexing. If the server is configured but empty, one `index_repository` at session start is enough for the host; sub-agents inherit the same store.

---

## Doc update map

| Change | Touch |
| --- | --- |
| Usage law / tool defaults | this file |
| Sub-agent tool-access wording | [process.md](process.md) + agent universal constraints (both runtimes) |
| MCP / harness map | [harness-runtimes.md](harness-runtimes.md) |
| Audit dispatch pack | `.claude/skills/audit-round/SKILL.md` |
| Condensed pointer | [CLAUDE.md](../../CLAUDE.md) §3 table |
