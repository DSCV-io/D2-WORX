#!/usr/bin/env bash
# wt-setup.sh <name> — create a sibling git worktree branched at HEAD and carry
# the CURRENT uncommitted working state into it, so a parallel implementer works
# from the same uncommitted base. Backs the `worktree-ops` skill.
#
# WHY per-file untracked copy (never per-directory): a prior manual run failed
# copying directory entries; `git status --porcelain -uall` lists FILES, and we
# copy each with `cp --parents` so the path structure is recreated exactly.
set -euo pipefail

NAME="${1:?usage: wt-setup.sh <name>}"
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
WT="$(dirname "$ROOT")/D2-WORX-$NAME"

echo "wt-setup: creating worktree $WT (branch $NAME @ HEAD)"
git worktree add "$WT" -b "$NAME" HEAD

# Inventory the current uncommitted state (tracked-modified + untracked).
mapfile -t UNTRACKED < <(git status --porcelain -uall | grep '^??' | sed 's/^?? //')
TRACKED_N=$(git status --porcelain -uall | grep -vc '^??' || true)

echo "wt-setup: applying $TRACKED_N tracked change(s) via binary patch"
if [ "$TRACKED_N" -gt 0 ]; then
  git diff HEAD --binary | git -C "$WT" apply --index
fi

echo "wt-setup: copying ${#UNTRACKED[@]} untracked file(s) per-file (cp --parents)"
for f in "${UNTRACKED[@]}"; do
  [ -f "$f" ] || continue           # skip dir entries defensively
  cp --parents "$f" "$WT/"
done

# Parity: worktree uncommitted entry count must equal source.
SRC_N=$(git status --porcelain -uall | wc -l | tr -d ' ')
WT_N=$(git -C "$WT" status --porcelain -uall | wc -l | tr -d ' ')
echo "wt-setup: entry-count parity source=$SRC_N worktree=$WT_N"
[ "$SRC_N" = "$WT_N" ] || { echo "wt-setup: PARITY MISMATCH — inspect $WT manually" >&2; exit 1; }

echo "wt-setup: pnpm install in worktree"
( cd "$WT" && pnpm install )

echo "wt-setup: DONE — $WT ready with the carried uncommitted base."
