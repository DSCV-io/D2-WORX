<!--
Copyright (c) DCSV. All rights reserved.
-->

# CLAUDE.md — Claude Code adapter (not law)

> **Shared project law is [AGENTS.md](AGENTS.md).** This file is a Claude Code entrypoint only — it does **not** create a fifth meta-doc / law surface (meta-doc set = `AGENTS.md` + `docs/dev/rules.md` + `docs/dev/process.md` + `.github/copilot-instructions.md`). Edit law in **AGENTS.md** (lockstep with rules.md / process.md per [rules.md §11.32](docs/dev/rules/11-documentation-parity-best-practices.md#11-documentation-parity--best-practices)). Do not re-expand a full duplicate body here.

@AGENTS.md

---

## Claude-only surface

| Concern | Location |
| --- | --- |
| Spawn names | `claude-d2-*` only — `.claude/agents/claude-d2-*.md` (never `grok-d2-*`, `codex-d2-*`, or bare `d2-*`) |
| Skills (canonical bodies) | `.claude/skills/*` |
| Deny rules / hooks | `.claude/settings.json` (Grok may load via Claude-compat) |
| Multi-runtime pins + map | [docs/dev/harness-runtimes.md](docs/dev/harness-runtimes.md) |
| Process + audit loop | [docs/dev/process.md](docs/dev/process.md) |
| Predicates | [docs/dev/rules.md](docs/dev/rules.md) |

**One primary harness at a time** unless the user explicitly runs a multi-harness experiment. When Claude Code is the active host, dispatch only `claude-d2-*` roles.

**Windows LSP note (Claude plugins):** after `claude plugin marketplace update`, re-apply the Windows cmd-wrap fix for `marketplace.json` if C#/TS LSP breaks — see project memory / past notes; not law.
