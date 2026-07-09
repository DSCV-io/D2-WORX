#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
# do-commit.sh "<commit message>" — the sanctioned commit path for cycle-commit.
# Invoke ONLY after EXPLICIT per-occurrence user commit permission (rules.md
# §13.1). It plants the one-shot .claude/.commit-authorized marker that the
# git-guard PreToolUse hook requires, runs the commit, and ALWAYS removes the
# marker (trap on EXIT — even on failure) so authorization never leaks.
#
# Conventions: conventional-commit subject, <=100 chars (the commit-msg hook
# rejects >101; aim <=72). NO Co-Authored-By trailer (standing user rule).
set -uo pipefail

MSG="${1:?usage: do-commit.sh \"type(scope): subject\"}"
ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$ROOT"
MARKER="$ROOT/.claude/.commit-authorized"

cleanup() { rm -f "$MARKER"; }
trap cleanup EXIT

SUBJ="${MSG%%$'\n'*}"
if [ "${#SUBJ}" -gt 100 ]; then
  echo "do-commit: subject is ${#SUBJ} chars (>100) — the commit-msg hook will reject it." >&2
  exit 1
fi

echo "do-commit: planting authorization marker"
: > "$MARKER"

echo "do-commit: git add -A"
git add -A

echo "do-commit: committing"
git commit -m "$MSG"

echo "do-commit: verifying"
git log --oneline -1
git status --porcelain -uall | grep . && { echo "do-commit: WARNING tree not clean post-commit" >&2; exit 1; } || true
echo "do-commit: DONE (marker removed by trap)."
