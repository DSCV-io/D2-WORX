#!/usr/bin/env sh
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
#
# git-guard.sh — PreToolUse(Bash) hook.
#
# WHY: rules.md §13.1 (never commit without explicit per-occurrence user
# permission) + §13.3 (never run destructive git — force push / hard reset /
# branch delete / stash / rebase / clean / restore / checkout-revert / rm /
# worktree remove). Sub-agents have no way to feel a permission gate, so this
# hook is the structural backstop: destructive/commit git is blocked UNLESS the
# orchestrator has planted the one-shot marker file .claude/.commit-authorized
# (created only after the user authorizes THIS commit; the cycle-commit skill is
# the sole sanctioned path that plants + removes it). Read-only git
# (status/log/diff/show/worktree list/stash list/checkout --help) is NEVER
# touched.
#
# SCOPE / THREAT MODEL — this is a DEFENSE-IN-DEPTH string-match tripwire layered
# OVER the permission system, NOT a hermetic sandbox. It matches the normalized
# command TEXT, so a shell-equivalent rewrite that reaches the same syscall
# without the literal "git <verb>" tokens can evade it — e.g. `git${IFS}commit`,
# quoted/escaped splits (`g""it commit`), an alias or wrapper function, `env git
# commit`, or invoking the git binary by path. It is a high-value backstop for the
# common/accidental path (and the honest sub-agent), not an airtight guarantee;
# it must never be read as one. The authoritative gate remains the permission
# system + human review; hardening git tokenization here is best-effort.
#
# Contract: PreToolUse reads the tool-call JSON on stdin; exit 0 = allow,
# exit 2 = block (stderr is surfaced to the agent). No jq — parse with node
# (universally present in this repo).

set -u

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
MARKER="${PROJECT_DIR}/.claude/.commit-authorized"

# Extract the Bash command string from the tool-input JSON on stdin.
CMD=$(node -e 'let d="";process.stdin.on("data",c=>d+=c).on("end",()=>{try{const j=JSON.parse(d);process.stdout.write((j.tool_input&&j.tool_input.command)||"");}catch(e){process.stdout.write("");}});')

# Nothing to inspect -> allow.
[ -n "$CMD" ] || exit 0

# Normalize whitespace so "git   commit" and newlines match uniformly.
NORM=$(printf '%s' "$CMD" | tr '\n\t' '  ' | tr -s ' ')

matched=""

# stash is special: block "git stash" (and push/pop/drop/clear/save/apply)
# but ALLOW the read-only "git stash list" / "git stash show".
if [ -z "$matched" ] && printf '%s' "$NORM" | grep -Eq 'git +stash'; then
  if ! printf '%s' "$NORM" | grep -Eq 'git +stash +(list|show)'; then
    matched="git stash"
  fi
fi

# checkout is special: block EVERY working-tree/branch checkout — the path-revert
# form "git checkout -- <path>", a branch switch "git checkout <branch>", and the
# "-B"/"-b" create-or-reset forms — because each can silently discard uncommitted
# working-tree state. ALLOW only the read-only help forms "git checkout --help"/"-h"
# (and "git help checkout" never matches this, its verb is "help", not "checkout").
if [ -z "$matched" ] && printf '%s' "$NORM" | grep -Eq 'git +checkout'; then
  if ! printf '%s' "$NORM" | grep -Eq 'git +checkout +(--help|-h)( |$)'; then
    matched="git checkout"
  fi
fi

# Remaining destructive / commit patterns, matched anywhere in the command
# (covers chained forms like "git add -A && git commit"). Each destructive verb
# is blocked in FULL (not just its most-dangerous flag): a bare "git reset" /
# "git clean" / "git restore" / "git rm" / "git worktree remove" can wipe or
# revert the uncommitted working tree this gate exists to protect.
if [ -z "$matched" ]; then
  # Each line: "ERE<TAB>label".
  while IFS='	' read -r pat label; do
    [ -n "$pat" ] || continue
    if printf '%s' "$NORM" | grep -Eq "$pat"; then
      matched="$label"
      break
    fi
  done <<'PATTERNS'
git +commit	git commit
git +push	git push
git +reset	git reset
git +clean	git clean
git +restore	git restore
git +rm( |$)	git rm
git +branch +(-D|-d|--delete)	git branch -D/-d
git +rebase	git rebase
git +worktree +remove	git worktree remove
PATTERNS
fi

# Non-destructive command -> allow untouched.
[ -n "$matched" ] || exit 0

# Destructive/commit: allow ONLY if the one-shot authorization marker exists.
if [ -f "$MARKER" ]; then
  exit 0
fi

printf '%s\n' \
  "BLOCKED by git-guard: detected a destructive/commit git operation ($matched)." \
  "This git op is fenced per rules.md §13.1 (commit) / §13.3 (destructive git)." \
  "It is allowed only after the orchestrator, on EXPLICIT per-occurrence user" \
  "authorization, plants the marker .claude/.commit-authorized (the cycle-commit" \
  "skill is the sanctioned path — it plants the marker, runs the op, then always" \
  "removes it). No marker present -> refusing to run '$matched'." >&2
exit 2
