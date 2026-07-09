#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
# precheck.sh — the pre-commit gate for the `cycle-commit` skill. Read-only; it
# NEVER stages or commits. Run it, read the output, THEN (only after explicit
# per-occurrence user commit permission, rules.md §13.1) run do-commit.sh.
set -uo pipefail

ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$ROOT"
rc=0

echo "precheck: branch = $(git branch --show-current)"
echo "precheck: uncommitted entries = $(git status --porcelain -uall | wc -l | tr -d ' ')"

# csharp-ls lock gotcha — a currency/seed run can flap while it holds DLLs.
if tasklist 2>/dev/null | grep -qi 'csharp-ls'; then
  PID=$(tasklist 2>/dev/null | grep -i 'csharp-ls' | awk '{print $2}' | head -1)
  echo "precheck: NOTE csharp-ls.exe running (PID $PID) — kill before reseeding if the currency check flaps."
fi

# Baseline currency must be stable across two runs (rules.md §26.20).
echo "precheck: baseline currency check x2"
pnpm --filter release-runner check-baselines || rc=1
pnpm --filter release-runner check-baselines || rc=1

echo "precheck: prettier NOTE — the pre-commit hook chunks staged .ts/.js/.json/.svelte/.css/.yaml"
echo "  through prettier --check; .md is .prettierignore'd (never prettier --write). Fix CODE only."

echo "precheck: RESULT $([ $rc = 0 ] && echo PASS || echo FAIL)"
exit $rc
