<!--
Copyright (c) DCSV. All rights reserved.
-->

<!-- §11.33 carve-out: agent-directive doc loaded by Copilot auto-context; not part of the human-navigable docs tree -->

# Copilot review + completion instructions

**You are GitHub Copilot acting on the D²-WORX codebase. Before suggesting code, reviewing a PR, or producing any other output, READ THE CANONICAL DOCS FIRST:**

- **[AGENTS.md](../AGENTS.md)** — shared condensed project law (Critical Reminders, Doc Update Map, behavioral guidelines). Read top-to-bottom. (`CLAUDE.md` is a Claude Code adapter that imports AGENTS.md — not a second law body.)
- **[docs/dev/rules.md](../docs/dev/rules.md)** — the full canonical predicate catalog (evidence-required rules across categories: security, race conditions, naming, object disposal, D2Result usage, OOTB shared libs, logging, PII redaction, observability, idempotency, configuration, framing, audit-evidence discipline, and more).
- **[docs/dev/process.md](../docs/dev/process.md)** — phase lifecycle (PLAN → EXECUTE → FINAL-REVIEW → SHIP → REVIEW), permission gates, sub-agent architecture, audit-loop mechanics.

These three docs are AUTHORITATIVE. If your suggestion conflicts with any predicate in rules.md or any protocol in process.md, you are making a mistake — UNLESS the PR author has explicitly acknowledged in writing the SPECIFIC predicates / steps being bypassed (per rules.md §13.14).

## How to apply the canonical docs to a PR review

1. Identify which areas the PR touches (handlers, DI, caching, messaging, auth, BFF, codegen, docs, tests, etc.).
2. Walk the **relevant** rules.md categories for those areas — every category has a numbered predicate list with Evidence / Why / How blocks.
3. Verify the PR satisfies the predicates that apply. Cite by `rules.md §N.M` when surfacing issues, so the author can resolve directly against the canonical source.
4. For process / permission / journal / audit-loop concerns, cite `process.md §N`.
5. For at-a-glance reminders (the Critical Reminders block, the C# Naming table, the Doc Update Map), **AGENTS.md** is the appropriate cite — but its content is a CONDENSED view of rules.md / process.md per §11.32, so deep-dives should follow the cross-pointers back to the canonical source.

## What this doc INTENTIONALLY does NOT do

- It does NOT restate individual predicates from rules.md. Restated rules drift; the canonical catalog stays canonical.
- It does NOT enumerate behavioral guidelines from AGENTS.md §7.
- It does NOT carry process protocols from process.md.
- It does NOT cite specific patterns / library usage / convention examples — those live in [docs/PATTERNS.md](../docs/PATTERNS.md) and the per-lib / per-service READMEs reachable from [README.md](../README.md) + [AGENTS.md §3](../AGENTS.md#3-reference-documents).

## §11.32 lockstep annotation

This doc INTENTIONALLY exists alongside AGENTS.md / rules.md / process.md as part of the meta-doc set (per rules.md §14.1 allowlist + §11 KEEP-doc framing). The pointer-only model means:

- Copilot reads this file first (via GitHub's auto-context for PR review + completions).
- This file directs Copilot to the canonical sources.
- Substantive updates land in rules.md / process.md / AGENTS.md — this file rarely changes.

When this file IS edited (e.g., to add a new canonical pointer or refine the review workflow above), the same change verifies the linked canonical docs still match — per §11.1 doc-edit-in-same-change discipline.
