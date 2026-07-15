#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
# gates.sh — run the zero-warning build/inspect gates + the two .NET test
# projects, capture full logs, print terse summaries. Backs the `gate-suite`
# skill (rules.md §5.21/§5.22/§5.23 zero-warning; §1 test discipline).
#
# WHY per-project SOLO test runs: `dotnet test` (MTP) IGNORES a VSTest --filter,
# so a whole-project run is the only reliable unit; the two test projects are
# run separately so a failure's owning project is unambiguous. On failure we
# grep the MTP TestResults log (null bytes stripped) for the failed test NAMES
# so identity is never lost in the summary.
set -uo pipefail

ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel 2>/dev/null || echo "${CLAUDE_PROJECT_DIR:-}")"
[ -n "$ROOT" ] && cd "$ROOT" || { echo "gates: cannot locate repo root" >&2; exit 1; }

RUN_TS=0
[ "${1:-}" = "--ts" ] && RUN_TS=1

TS="$(date +%Y%m%d-%H%M%S)"
LOGDIR="${TMPDIR:-/tmp}/d2-gates-$TS"
mkdir -p "$LOGDIR"
echo "gates: logs -> $LOGDIR"
rc=0

echo "gates: [build] dotnet build D2.slnx"
dotnet build D2.slnx > "$LOGDIR/build.log" 2>&1 || rc=1
WARN=$(grep -cE ': warning ' "$LOGDIR/build.log" || true)
ERR=$(grep -cE ': error ' "$LOGDIR/build.log" || true)
echo "gates:   build warnings=$WARN errors=$ERR (log: $LOGDIR/build.log)"
{ [ "$WARN" != 0 ] || [ "$ERR" != 0 ]; } && rc=1

# Requires JetBrains.ReSharper.GlobalTools ≥ 2026.1.x for net10 BCL resolution
# (2025.3.x emits mass false "Cannot resolve Int128/Exception" noise).
echo "gates: [inspect] jb inspectcode D2.slnx --settings=D2.sln.DotSettings --severity=WARNING --no-build"
jb inspectcode D2.slnx --settings=D2.sln.DotSettings \
   --severity=WARNING --format=Text --no-build \
   --output="$LOGDIR/inspect.log" > "$LOGDIR/inspect.stdout" 2>&1 || rc=1

# Single source of truth: private/tools/scripts/count-inspectcode-findings.sh
# (local zero-warning gate; docs/COMMANDS.md cites the same script;
# identity pin: private/tools/scripts/tests/count-inspectcode-findings.test.mjs).
# Not a PR CI job — inspectcode stays local-only by design.
ICOUNT=$(bash "$ROOT/private/tools/scripts/count-inspectcode-findings.sh" "$LOGDIR/inspect.log")
echo "gates:   inspectcode finding-lines=$ICOUNT (log: $LOGDIR/inspect.log)"
if [ "$ICOUNT" != 0 ]; then
  # Dump findings on non-zero so the failure is diagnosable (shared script --fail
  # would exit 1; we keep gates.sh rc aggregation and dump explicitly).
  cat "$LOGDIR/inspect.log"
  rc=1
fi

run_tests() {
  local proj="$1" name="$2" log="$LOGDIR/test-$2.log"
  echo "gates: [test] $name (SOLO whole-project — MTP ignores --filter)"
  dotnet test "$proj" > "$log" 2>&1 || rc=1
  local summ; summ=$(grep -iE 'passed!|failed!|test run' "$log" | tail -1 || true)
  echo "gates:   $name: ${summ:-<no summary line>} (log: $log)"
  if grep -qiE 'failed!' "$log"; then
    echo "gates:   FAILED test names in $name:"
    # The MTP TestResults log (…/bin/**/TestResults/<Project>_*.log) is the only
    # place the failed test IDENTITY survives; `find` (no globstar dependency)
    # locates it, `tr -d '\0'` strips null bytes BEFORE grep so grep does not
    # bail as "binary file matches", and `grep -a` forces text. Each failed line
    # is `failed <FullyQualifiedTestName> (<ms>)`.
    find "$(dirname "$proj")" -path '*/TestResults/*.log' -type f 2>/dev/null | while IFS= read -r tl; do
      tr -d '\0' < "$tl" | grep -aoE '^ *failed +[[:graph:]]+'
    done | sort -u | sed -E 's/^ *failed +/     /' || true
    rc=1
  fi
}

run_tests private/services/edge/tests/DcsvIo.D2.Private.Edge.Tests.csproj DcsvIo.D2.Private.Edge.Tests
run_tests public/packages/dotnet/tests/DcsvIo.D2.Tests.csproj DcsvIo.D2.Tests

if [ "$RUN_TS" = 1 ]; then
  echo "gates: [test-ts] TS package suites (pnpm -r test)"
  pnpm -r test > "$LOGDIR/test-ts.log" 2>&1 || rc=1
  echo "gates:   TS suites: $(grep -iE 'passed|failed' "$LOGDIR/test-ts.log" | tail -1 || echo '<see log>') (log: $LOGDIR/test-ts.log)"
fi

echo "gates: SUMMARY build(w=$WARN,e=$ERR) inspect(f=$ICOUNT) -> $([ $rc = 0 ] && echo GREEN || echo RED)"
exit $rc
