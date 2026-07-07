#!/usr/bin/env bash
# reseed.sh — regenerate every consumable-package release baseline, then prove
# currency is stable. Backs the `reseed-baselines` skill (rules.md §26.20).
#
# WHY the exact order: seed-publicapi (.NET PublicAPI.*.txt) -> build the TS
# dists (api-extractor reads dist/index.d.ts) -> seed-apiextractor (.api.md) ->
# currency check TWICE. The double check exists because the post-seed `tsc -b`
# of a TS package can shift its api.md-derived fingerprint on the FIRST currency
# run; a second run must then report all-current. If run #2 is not all-current,
# the baselines are genuinely stale — re-run this script.
set -euo pipefail

ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel 2>/dev/null || echo "${CLAUDE_PROJECT_DIR:-}")"
[ -n "$ROOT" ] && cd "$ROOT" || { echo "reseed: cannot locate repo root" >&2; exit 1; }
echo "reseed: repo root = $ROOT"

# GOTCHA (memory: csharp-ls locks source-gen DLLs -> MSB3021/3027 during seed).
# Detect but NEVER auto-kill (killing the LSP is a user decision).
if tasklist 2>/dev/null | grep -qi 'csharp-ls'; then
  PID=$(tasklist 2>/dev/null | grep -i 'csharp-ls' | awk '{print $2}' | head -1)
  echo "reseed: ABORT — csharp-ls.exe is running (PID $PID) and locks source-gen DLLs;" >&2
  echo "  the seed will fail with MSB3021/3027. Kill it yourself, then re-run:" >&2
  echo "    taskkill //F //PID $PID" >&2
  exit 1
fi

echo "reseed: [1/4] seeding .NET PublicAPI + .release-fingerprint baselines"
node tools/scripts/seed-publicapi-baselines.mjs

echo "reseed: [2/4] building TS consumable dists (api-extractor consumes dist/index.d.ts)"
pnpm -r --filter "./server/shared/typescript/**" \
        --filter "./server/services/edge/key-custodian/client-ts" build

echo "reseed: [3/4] seeding TS api-extractor (.api.md) + etc/.release-fingerprint baselines"
node tools/scripts/seed-apiextractor-baselines.mjs

echo "reseed: [4/4] currency check x2 (stability)"
pnpm --filter release-runner check-baselines
echo "reseed: currency run #1 all-current; re-running for stability"
pnpm --filter release-runner check-baselines

echo "reseed: DONE — all baselines reseeded and currency stable across two runs."
