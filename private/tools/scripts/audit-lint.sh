#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
#
# audit-lint.sh — self-run inspection script for mechanical patterns we
# kept re-finding on multi-pass review sweeps (phase verbiage, audit-named
# files, British spellings, line length, test method prefixes).
#
# NOT a commit/PR gate. Run it yourself during the audit loop to surface
# obvious mechanical issues; treat the output as advisory. Codegen output,
# vendor files, and similar may legitimately produce "violations" — your
# judgment decides what's a real finding.
#
# Each gate has a clear failure message + an inline allowlist comment
# (`audit-lint:allow-<gate>`) for the rare in-source legitimate exception.
#
# Exit code: 0 = no findings, non-zero = findings (advisory only).

set -uo pipefail

ROOT_DIR=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
cd "$ROOT_DIR"

# Mode:
#   'all'    — scan the entire tree (default; for `make audit-lint` and CI).
#   'staged' — only scan files in the staged-for-commit diff (for the
#              husky pre-commit hook). This makes the gates incrementally
#              adoptable: pre-existing baseline violations don't block
#              commits unless they're in a file you're currently modifying.
#              Touch a file → fix its violations.
MODE="all"
[ "${1:-}" = "--staged" ] && MODE="staged"

if [ "$MODE" = "staged" ]; then
  STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACMR 2>/dev/null \
    | grep -E '\.(cs|ts|tsx|svelte|md|proto)$' || true)
  if [ -z "$STAGED_FILES" ]; then
    echo "audit-lint: no staged source files — skipping."
    exit 0
  fi
fi

VIOLATIONS=0
SECTION_FAILED=0

err()   { echo "  ✖  $1" >&2; VIOLATIONS=$((VIOLATIONS + 1)); SECTION_FAILED=1; }
note()  { echo "     $1" >&2; }
section() {
  echo ""
  echo "─── $1 ───"
  SECTION_FAILED=0
}

# In 'staged' mode, scan only the staged files. In 'all' mode, scan a tree.
# `grep_target` writes file paths (one per line) to stdout; the gate then
# pipes that into a content-grep.
grep_target() {
  if [ "$MODE" = "staged" ]; then
    echo "$STAGED_FILES" | while IFS= read -r f; do
      [ -z "$f" ] && continue
      [ ! -f "$f" ] && continue
      echo "$f"
    done
  else
    # Generates the same listing the per-gate `grep -r` would have walked,
    # so the inner pipe stays uniform across modes.
    find server/ docs/ contracts/ -type f \
      \( -name '*.cs' -o -name '*.ts' -o -name '*.tsx' -o -name '*.svelte' \
         -o -name '*.md' -o -name '*.proto' \) \
      ! -path '*/node_modules/*' ! -path '*/bin/*' ! -path '*/obj/*' \
      ! -path '*/Generated/*' ! -path '*/TestResults/*' ! -path '*/old/*' \
      ! -path '*/.git/*' 2>/dev/null
  fi
}

# Common excludes. Generated code, vendor trees, the script itself (which
# necessarily contains the pattern strings), tracking docs, MEMORY files.
COMMON_EXCLUDE_DIRS=(
  --exclude-dir=.git
  --exclude-dir=node_modules
  --exclude-dir=bin
  --exclude-dir=obj
  --exclude-dir=Generated
  --exclude-dir=TestResults
  --exclude-dir=old
)

# Paths permitted to contain phase/stage verbiage by design.
PHASE_VERBIAGE_OK_PATHS='^(private/docs/v2/|tools/scripts/audit-lint\.sh$|.*MEMORY\.md$|CHANGELOG\.md$)'

# =====================================================================
# Gate 1 — forbidden test filenames
# =====================================================================
section "Test filenames must describe the FEATURE, not the audit round"

bad_files=$(git ls-files \
  '*Phase[0-9]*Tests.cs' '*PhaseTests.cs' \
  '*Audit*Tests.cs' '*Sweep*Tests.cs' '*Round[0-9]*Tests.cs' \
  2>/dev/null || true)

if [ -n "$bad_files" ]; then
  while IFS= read -r f; do
    # Filter out tracked-but-deleted files (git ls-files returns the index).
    [ -z "$f" ] && continue
    [ ! -f "$f" ] && continue
    err "$f"
  done <<< "$bad_files"
  if [ "$SECTION_FAILED" -eq 1 ]; then
    note "tests live next to the feature they cover, not bundled by review-round"
  fi
fi

# =====================================================================
# Gate 2 — forbidden test method name prefixes
# =====================================================================
section "Test method names must describe BEHAVIOR, not the audit row"

# Match: AuditN_ / Audit{Letter}_ / PhaseN_ / single-letter-row-label like
# F1_/M2_/H4_/L3_/O1_/R5_/S2_/QN_, plus combo labels like F3F4L5_.
prefix_pattern='public[[:space:]]+(async[[:space:]]+)?(void|Task|ValueTask)[[:space:]]+(Audit[0-9]+_|Audit[A-Z]_|Phase[0-9]+_|[HMFLORSQ][0-9]+_|F[0-9]+F[0-9]+L[0-9]+_)'

bad_methods=$(grep -rEn "$prefix_pattern" \
  --include='*.cs' \
  "${COMMON_EXCLUDE_DIRS[@]}" \
  server/ 2>/dev/null \
  | grep -v 'audit-lint:allow-prefix' || true)

if [ -n "$bad_methods" ]; then
  echo "$bad_methods" >&2
  err "$(echo "$bad_methods" | wc -l) test method(s) carry an audit/phase prefix"
  note "rename to feature-descriptive names (e.g. F5_OnlyCountsExpired → OnlyCountsExpired)"
fi

# =====================================================================
# Gate 3 — American English only
# =====================================================================
section "American English (no British/Canadian spellings)"

# Word-bounded match. Allowlist via inline comment 'audit-lint:allow-spelling'
# on the offending line for the rare proper-noun / third-party identifier.
british_pattern='\b(behaviour|behaviours|colour|colours|coloured|analyse|analysed|analysing|analyser|honour|honoured|honouring|cancelled|cancelling|favourite|favourites|neighbour|neighbours|defence|defences|optimise|optimised|optimising|customise|customised|customising|recognise|recognised|recognising|prioritise|prioritised|specialise|specialised|categorise|categorised|utilise|utilised|realise|realised|minimise|minimised|maximise|maximised|emphasise|emphasised|criticise|criticised|summaris(e|ed|ing)|programme|modelled|modelling|signalled|signalling|labelled|labelling|travelled|travelling|organis(e|ed|ing|ation|ations))\b'

british_hits=$(grep_target | xargs -r grep -IEn -i "$british_pattern" 2>/dev/null \
  | grep -v 'audit-lint:allow-spelling' || true)

if [ -n "$british_hits" ]; then
  echo "$british_hits" >&2
  err "$(echo "$british_hits" | wc -l) British/Canadian spelling(s)"
  note "use American: behavior/color/analyze/honor/canceled/favorite/defense/recognize/..."
fi

# =====================================================================
# Gate 4 — no phase/stage/temp verbiage in code or KEEP docs
# =====================================================================
section "Phase/stage verbiage must stay inside private/docs/v2/ tracking docs"

# Patterns that index our internal review history. KEEP docs / source
# describe CURRENT state — readers don't care which phase added a thing.
verbiage_pattern='(\b(Phase [0-9]|phase [0-9]|Phase-[0-9]|Wave [0-9]|wave [0-9]|Sweep [0-9]|sweep [0-9]|Round [0-9]|audit (pass|sweep|decision|row|round)|audit-row|Step [0-9]+\.[0-9]+|gap closure|previously lacked|pre-fix |post-fix |fix verification|temporary(ly)? for)\b|//.*\bTODO\b|//.*\bFIXME\b|//.*\bHACK\b|/\*.*\bTODO\b|/\*.*\bFIXME\b|/\*.*\bHACK\b|#.*\bTODO\b|#.*\bFIXME\b|#.*\bHACK\b)'

verbiage_hits=$(grep_target | xargs -r grep -IEn "$verbiage_pattern" 2>/dev/null \
  | grep -vE "$PHASE_VERBIAGE_OK_PATHS" \
  | grep -v 'audit-lint:allow-verbiage' || true)

if [ -n "$verbiage_hits" ]; then
  echo "$verbiage_hits" >&2
  err "$(echo "$verbiage_hits" | wc -l) line(s) with phase/audit/temp verbiage"
  note "phase tracking lives in private/docs/v2/; keep docs describe current reality"
fi

# =====================================================================
# Gate 5 — line length (100 chars max)
# =====================================================================
section "Line length ≤ 100 chars (C# / TS source only)"

# Allowlist via inline `// audit-lint:allow-long` comment on the offending
# line. Markdown deliberately excluded — long table rows + long URL refs are
# normal in docs.
long_lines=$(grep_target | grep -E '\.(cs|ts|tsx)$' \
  | xargs -r grep -En '.{101}' 2>/dev/null \
  | grep -v 'audit-lint:allow-long' || true)

if [ -n "$long_lines" ]; then
  count=$(echo "$long_lines" | wc -l)
  # Show only the first 10 to keep the output legible.
  echo "$long_lines" | head -10 >&2
  if [ "$count" -gt 10 ]; then
    note "(showing 10 of $count over-long lines)"
  fi
  err "$count line(s) exceed 100 chars"
fi

# =====================================================================
# Summary
# =====================================================================
echo ""
if [ $VIOLATIONS -gt 0 ]; then
  echo "✖  audit-lint: $VIOLATIONS violation(s) — see above"
  echo ""
  echo "   Fix the underlying issue, OR add an inline allowlist comment"
  echo "   (audit-lint:allow-<gate>) on the offending line if the violation"
  echo "   is a genuine exception. Adding allowlist entries should be rare."
  exit 1
else
  echo "✔  audit-lint: clean"
fi
