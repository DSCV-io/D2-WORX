#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
# count-inspectcode-findings.sh — single source of truth for JetBrains
# inspectcode Text-format finding-line counts (zero-warning gate).
#
# JetBrains Text format prefixes finding lines with leading whitespace; header
# lines do not. Count of lines matching `^\s+` is the finding-line metric used
# by the local gate and CI. inspectcode exits 0 even when findings exist, so
# callers MUST use this count (not process exit) as the zero-warning assertion.
#
# Consumers (must stay twins via this script — do not re-inline the parse):
#   - .claude/skills/gate-suite/scripts/gates.sh
#   - .github/workflows/test.yml (inspectcode job)
#   - docs/COMMANDS.md (documents this script as the shared parse)
#
# Identity pin: tools/scripts/tests/count-inspectcode-findings.test.mjs
# Usage:
#   count-inspectcode-findings.sh <inspectcode.log>
#     → prints integer count of indented finding-lines; exit 0.
#   count-inspectcode-findings.sh --fail <inspectcode.log>
#     → same count on stdout; if count != 0, dumps the log to stdout and exit 1.
#
set -euo pipefail

FAIL=0
LOG=""

if [ "${1:-}" = "--fail" ]; then
  FAIL=1
  LOG="${2:-}"
else
  LOG="${1:-}"
fi

if [ -z "$LOG" ]; then
  echo "usage: count-inspectcode-findings.sh [--fail] <inspectcode.log>" >&2
  exit 2
fi

if [ ! -f "$LOG" ]; then
  echo "count-inspectcode-findings: log missing: $LOG" >&2
  exit 1
fi

# grep -c prints a count even on no-match but exits 1 when the count is 0.
# With `set -e` + pipefail the pipeline would abort the script — swallow the
# no-match status and normalize empty to 0. head -1 guards against any doubling.
count=$(grep -cE '^\s+' "$LOG" 2>/dev/null | head -1 || true)
[ -n "$count" ] || count=0

echo "$count"

if [ "$FAIL" = 1 ] && [ "$count" != "0" ]; then
  cat "$LOG"
  exit 1
fi

exit 0
