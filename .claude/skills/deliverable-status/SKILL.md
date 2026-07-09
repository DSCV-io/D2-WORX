---
name: deliverable-status
description: Quick orientation - current branch, recent commits, uncommitted count, and pointers to the active tracking doc, wip journals, and the memory index. Use to reorient at session start or mid-task. Keywords - status, orient, where am I, branch, deliverable, wip, progress.
---

<!--
Copyright (c) DCSV. All rights reserved.
-->

# deliverable-status

Live snapshot (injected at skill-load):

- Branch: !`git branch --show-current`
- Recent commits: !`git log --oneline -6`
- Uncommitted entries: !`git status --porcelain -uall | wc -l`

## Pointers
- **Active tracking doc**: per CLAUDE.md header — currently `docs/v2/V2.md` (the single source for "what's the project doing now"; the pointer updates when it archives).
- **Deliverable state**: `docs/wip/<deliverable>/README.md` Status line + the deliverable's `journal.md` append-only decision record (authoritative; gitignored + local-only). Newest journal under `docs/wip/` is the active one.
- **Durable memory index**: `C:\Users\User\.claude\projects\C--DCSV-Projects-D2-WORX\memory\MEMORY.md`.
- **Code discovery**: if `codebase-memory-mcp` is connected, project `D2-WORX` — usage law in `docs/dev/codebase-memory.md` (graph ≠ SoT; prefer over Grep for discovery).
