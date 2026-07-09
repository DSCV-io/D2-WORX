#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
# wt-merge.sh <name> — merge a sibling worktree's uncommitted changes back into
# the main tree, file-granular. Backs the `worktree-ops` skill.
#
# POLICY: for every file present in BOTH trees, byte-identity is REQUIRED
# (git hash-object compare). Any true divergence STOPS the merge and prints the
# conflict list — this script NEVER auto-picks a side. Worktree-only files are
# copied per-file and re-hash-verified.
set -euo pipefail

NAME="${1:?usage: wt-merge.sh <name>}"
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
WT="$(dirname "$ROOT")/D2-WORX-$NAME"
[ -d "$WT" ] || { echo "wt-merge: worktree not found: $WT" >&2; exit 1; }

# File-granular -uall inventories of both sides.
mapfile -t SRC  < <(git         status --porcelain -uall | sed 's/^...//' | sort -u)
mapfile -t WTF  < <(git -C "$WT" status --porcelain -uall | sed 's/^...//' | sort -u)

printf '%s\n' "${SRC[@]}" > /tmp/wtm-src.$$
printf '%s\n' "${WTF[@]}" > /tmp/wtm-wt.$$
OVERLAP=$(comm -12 /tmp/wtm-src.$$ /tmp/wtm-wt.$$)
WTONLY=$(comm -13 /tmp/wtm-src.$$ /tmp/wtm-wt.$$)
rm -f /tmp/wtm-src.$$ /tmp/wtm-wt.$$

echo "wt-merge: overlap=$(printf '%s' "$OVERLAP" | grep -c . || true) worktree-only=$(printf '%s' "$WTONLY" | grep -c . || true)"

# Overlap: hash-compare; collect true divergences.
conflicts=""
while IFS= read -r f; do
  [ -n "$f" ] || continue
  h1=$(git hash-object "$ROOT/$f" 2>/dev/null || echo x)
  h2=$(git hash-object "$WT/$f"   2>/dev/null || echo y)
  [ "$h1" = "$h2" ] || conflicts="$conflicts\n  $f"
done <<< "$OVERLAP"

if [ -n "$conflicts" ]; then
  echo "wt-merge: STOP — divergent overlap files (resolve manually, no auto-pick):" >&2
  printf '%b\n' "$conflicts" >&2
  exit 1
fi

# Worktree-only files: copy per-file, then re-hash-verify.
copied=0
while IFS= read -r f; do
  [ -n "$f" ] || continue
  [ -f "$WT/$f" ] || continue
  mkdir -p "$ROOT/$(dirname "$f")"
  cp "$WT/$f" "$ROOT/$f"
  hs=$(git hash-object "$WT/$f"); hd=$(git hash-object "$ROOT/$f")
  [ "$hs" = "$hd" ] || { echo "wt-merge: copy-verify FAILED for $f" >&2; exit 1; }
  copied=$((copied + 1))
done <<< "$WTONLY"

echo "wt-merge: DONE — overlap byte-identical, copied $copied worktree-only file(s), all verified."
echo "wt-merge: NOTE — removing the worktree needs explicit user authorization (destructive)."
