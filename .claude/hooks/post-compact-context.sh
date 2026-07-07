#!/usr/bin/env sh
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
# post-compact-context.sh — SessionStart(matcher: compact) hook.
#
# WHY: after a context compaction the agent loses the working-state it had
# in-context. stdout from a SessionStart hook is injected back as context, so
# this prints a terse (<=15 line) re-orientation: the branch, the last 3
# commits, a DYNAMIC pointer at the active deliverable's decision record, and
# the durable memory index. Kept generic (discovers the newest wip journal at
# run time) so it does NOT go stale when the current deliverable (0026) ships.

set -u

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
cd "$PROJECT_DIR" 2>/dev/null || exit 0

echo "== Post-compact re-orientation =="
echo "Branch: $(git branch --show-current 2>/dev/null)"
echo "Recent commits:"
git log --oneline -3 2>/dev/null | sed 's/^/  /'

# Dynamic active-deliverable discovery: newest journal.md under docs/wip/
# (gitignored, local-only). Its top-level dir is the active deliverable; print
# its README Status line if present. Falls back to the wip root when absent.
JOURNAL=$(ls -t docs/wip/*/journal.md docs/wip/*/*/journal.md 2>/dev/null | head -1)
if [ -n "$JOURNAL" ]; then
  DELIV=$(printf '%s' "$JOURNAL" | sed -E 's#(docs/wip/[^/]+)/.*#\1#')
  echo "Active deliverable state: read $JOURNAL (append-only decision record, authoritative)"
  STATUS=$(grep -m1 -i 'Status' "$DELIV/README.md" 2>/dev/null)
  [ -n "$STATUS" ] && echo "  Root README Status: $(printf '%s' "$STATUS" | sed -E 's/[*_`]//g' | cut -c1-140)"
else
  echo "Active deliverable state: browse docs/wip/ (per-deliverable README Status line + journal)"
fi

echo "Durable memory index: C:\\Users\\User\\.claude\\projects\\C--DCSV-Projects-D2-WORX\\memory\\MEMORY.md"
