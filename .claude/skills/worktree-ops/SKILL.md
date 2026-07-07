---
name: worktree-ops
description: Set up a sibling git worktree carrying the current uncommitted state, or merge one back file-granularly. Use for parallel implementers sharing an uncommitted base. Keywords - worktree, parallel, isolation, uncommitted, carry, merge back, hash-compare.
allowed-tools: Bash, Read
---

# worktree-ops

Two modes for running parallel work off the SAME uncommitted base without committing first.

## Setup — carry the uncommitted base into a new worktree
```
bash .claude/skills/worktree-ops/scripts/wt-setup.sh <name>
```
Creates `../D2-WORX-<name>` on branch `<name>` at HEAD, then reproduces the current uncommitted state there: tracked changes via `git diff HEAD --binary | git -C <wt> apply --index`, untracked files copied PER-FILE with `cp --parents` (never per-directory — a directory entry breaks the copy), entry-count parity asserted, then `pnpm install` in the worktree.

## Merge — bring a worktree's changes back
```
bash .claude/skills/worktree-ops/scripts/wt-merge.sh <name>
```
File-granular `-uall` inventory of both sides, `comm` to split overlap vs worktree-only. Every OVERLAP file must be byte-identical (`git hash-object` compare); worktree-only files are copied per-file and re-hash-verified.

## Overlap-conflict policy
Any true divergence in an overlap file STOPS the merge and prints the conflict list. The script NEVER auto-picks a side — resolve each conflict by hand, then re-run.

## Reminders
- Removing a worktree (`git worktree remove --force`) is destructive — needs EXPLICIT user authorization (rules.md §13.3); the git-guard hook blocks it without the commit-authorized marker.
- Both scripts operate on the WORKING TREE only; nothing is committed.
