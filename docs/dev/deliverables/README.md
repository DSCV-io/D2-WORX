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

## Index

| File | Deliverable | Rules added |
|---|---|---|
| [0001-shared-libs-review.md](0001-shared-libs-review.md) | Shared-libs review | — |
| [0002-auth-inbound.md](0002-auth-inbound.md) | Auth inbound | — |
| [0003-harmless-endpoint-rename.md](0003-harmless-endpoint-rename.md) | HarmlessEndpoint rename | — |
| [0004-service-defaults.md](0004-service-defaults.md) | Service defaults | — |
| [0005-codegen-cleanup-and-dotnet-improvements.md](0005-codegen-cleanup-and-dotnet-improvements.md) | Codegen cleanup + .NET improvements | — |
| [0006-ts-bridge.md](0006-ts-bridge.md) | TS bridge | — |
| [0007-wire-parity.md](0007-wire-parity.md) | Wire parity | — |
| [0008-geo-data-pipeline.md](0008-geo-data-pipeline.md) | Geo data pipeline | — |
| [0009-geo-libs.md](0009-geo-libs.md) | Geo libs | — |
| [0011-doc-cleanup.md](0011-doc-cleanup.md) | Doc cleanup | — |
| [0012-tk-constants-in-tests.md](0012-tk-constants-in-tests.md) | TK constants in tests | — |
| [0013-auth-hardening.md](0013-auth-hardening.md) | Auth endpoint-declaration hardening | §5.1a (ThrowIfFalsey), §5.25a (no redundant `!`), §24.19 (uncommitted working-tree) |
| [0014-data-governance.md](0014-data-governance.md) | `D2.Shared.DataGovernance` anonymization foundation | §3.15 (anonymization), §5.1a (cycle-only carve-out), §24.20 (tool-invisible lenses), §24.21 (full-solution gate-verify), §24.22 (source xmldoc scan) |
