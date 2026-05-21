<!--
Copyright (c) DCSV. All rights reserved.
-->

# Shipped Deliverables — Snapshot READMEs

This folder holds the post-ship snapshot of each deliverable's root README, copied out of the local `docs/wip/<deliverable>/` workspace at SHIP time. See [../process.md](../process.md) for the loop protocol.

## What lives here

One file per shipped deliverable, named `NNNN-<name>.md` (4-digit index prefix so the directory listing sorts naturally in ship order). Each file is the post-ship snapshot of the deliverable's root tracking doc:

- High-level goal + final status (`SHIPPED YYYY-MM-DD`)
- Step list with iteration counts (e.g. "✅ 02-service-identity-stack (3 audit rounds to clean)")
- Cross-cutting decisions made during PLAN
- Final kinds-of-misses log
- Final report — what shipped, what was learned, references to the rule additions this deliverable produced

## What does NOT live here

- **Per-step journals** — those stay where they are in `docs/wip/NNNN-<name>/` (gitignored, local-only — same 4-digit index as the committed snapshot, so finding the local workspace for a past committed snapshot is trivial when the journals still exist locally). The workflow does NOT auto-delete them; the user removes them manually whenever they want. Until then, they remain available as audit-trail evidence accessible from the local file system, just never crossing the commit boundary.
- **In-flight work** — that's also in `docs/wip/NNNN-<name>/`, gitignored.
- **Code / pattern docs** — code lives in `server/`; patterns live in `docs/PATTERNS.md` and the per-lib READMEs.

## Why we keep the snapshot README

Three reasons:

1. **Audit-trail of shipped scope.** Future-you can scan past deliverables and see what each one shipped, the iteration cost (audit rounds per step), and the kinds-of-misses log distilled from the round-by-round work.
2. **Origin trace for `rules.md` predicates.** When a predicate's value is ever questioned ("why is this rule even here?"), the deliverable that surfaced the original miss is documented and citeable. Deeper detail (the actual round where the miss happened) lives in the local journals during the deliverable's life and gets manually archived or deleted by the user later.
3. **Reviewability for PR / external readers.** The snapshot README is short enough to read end-to-end and conveys both what shipped and how rigorously it was vetted. The committed surface stays compact; the wider evidence stays local.
