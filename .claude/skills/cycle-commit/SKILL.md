---
name: cycle-commit
description: The sanctioned commit ceremony - pre-checks, plant the git-guard authorization marker, commit, always remove the marker. Invoke ONLY after explicit per-occurrence user commit permission. Keywords - commit, cycle, marker, authorize, §13.1, git-guard.
allowed-tools: Bash, Read
---

# cycle-commit

The ONLY sanctioned path through the `git-guard` PreToolUse hook. The hook blocks every commit/destructive-git op unless `.claude/.commit-authorized` exists; this skill plants that marker and always removes it.

> INVOKE ONLY AFTER EXPLICIT PER-OCCURRENCE USER COMMIT PERMISSION (rules.md §13.1). A prior "go ahead" does not authorize THIS commit. If you are unsure whether the user authorized this specific commit — STOP and ask.

## Step 1 — pre-checks (read-only, never stages)
```
bash .claude/skills/cycle-commit/scripts/precheck.sh
```
Verifies branch + uncommitted count, warns if `csharp-ls.exe` is running (currency flap risk), runs the baseline currency check ×2 for stability (§26.20), and prints the prettier expectation (the pre-commit hook chunks staged `.ts/.js/.json/.svelte/.css/.yaml` through `prettier --check`; `.md` is `.prettierignore`'d — never `prettier --write`). Must print `RESULT PASS`.

## Step 2 — commit (only after user permission)
```
bash .claude/skills/cycle-commit/scripts/do-commit.sh "type(scope): subject"
```
Plants `.claude/.commit-authorized`, `git add -A`, commits, verifies with `git log --oneline -1` + a clean `git status`, and removes the marker via an EXIT trap (marker cleared even on failure — authorization never leaks).

## Commit-message conventions
- Conventional-commit subject (`type(scope): subject`); `type` enforced by `.husky/commit-msg`.
- Subject ≤100 chars (the hook rejects >101; aim ≤72). do-commit.sh pre-checks this.
- NO `Co-Authored-By` trailer (standing user rule).

## Why the marker, not a script bypass
The marker is the canonical authorization token: it also lets a direct `git commit` tool call through the guard, and its lifetime is exactly one commit. Planting it is the act of recording that the user authorized this specific commit.
