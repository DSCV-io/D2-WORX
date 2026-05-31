<!--
Copyright (c) DCSV. All rights reserved.
-->

# Architectural Decision Records (ADRs)

Index of D²-WORX's architectural decision records — engineers preparing to make
a load-bearing architectural decision, reviewers verifying decision provenance,
and contributors onboarding to past architectural choices find here the
canonical catalog.

ADRs are recorded **per architectural decision, NOT per deliverable**. A single
deliverable may produce zero, one, or several ADRs; an ADR may span
multiple deliverables. The `Deliverable:` cross-link field captures the
deliverable(s) in which the decision was actually made.

---

## Format spec

Each ADR file uses the standard Nygard format extended with one D²-WORX-specific
field:

```markdown
# ADR-NNNN: <Decision Title>

- **Status**: Accepted | Superseded by ADR-NNNN | Deprecated
- **Date**: YYYY-MM-DD
- **Deliverable**: <NNNN-name>

## Context

<the forces at play; the situation that drove the decision>

## Decision

<the architectural choice made — present tense>

## Consequences

<positive + negative + neutral consequences; what becomes easier / harder>

## Alternatives considered

<options weighed before landing on the Decision; why they were not chosen>
```

### File naming

ADRs live in this directory as `NNNN-kebab-case-title.md` (e.g.
`0001-self-rolled-dotnet-auth.md`). The number is monotonically increasing
across the catalog — never re-used, never re-ordered.

### Status values

- **Accepted** — currently in force; new work follows it.
- **Superseded by ADR-NNNN** — replaced by a later decision; the ADR file
  stays as historical record.
- **Deprecated** — the decision is no longer relevant (the system it described
  has been removed); kept for historical context.

---

## Index

| #   | Title | Status | Date | Deliverable |
| --- | ----- | ------ | ---- | ----------- |
| [0001](0001-contacts-folded-owned-component.md) | Contacts are a folded owned-component library, not a standalone contacts service/DB | Accepted | 2026-05-30 | TBD — contacts (Phase 2) |
| [0002](0002-spec-driven-codegen.md) | Spec-driven codegen as the cross-language source of truth | Accepted | 2026-05-30 | Phase 0 — shared libraries |
| [0003](0003-d2result-errors-as-values.md) | `D2Result` — errors-as-values, not exceptions for control flow | Accepted | 2026-05-30 | Phase 0 — shared libraries |
| [0004](0004-i18n-tkmessage.md) | i18n — `TKMessage` (translation-key-as-type) + source-generated `TK` constants | Accepted | 2026-05-30 | Phase 0 — shared libraries |
| [0005](0005-handler-pipeline.md) | Universal handler pipeline — `BaseHandler` + provider-pluggable repo handlers | Accepted | 2026-05-30 | Phase 0 — shared libraries |
| [0006](0006-abstractions-implementation-split.md) | Domain-safe abstractions slices + provider-pluggable implementations | Accepted | 2026-05-30 | Phase 0 — shared libraries |

> Backfill of ADRs from earlier shipped deliverables is queued as a separate
> task. This index grows as ADRs land.

---

## References

- [Michael Nygard's original ADR essay](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
  — the canonical Nygard format spec this catalog adapts.
- [`docs/dev/deliverables/`](../dev/deliverables/README.md) — per-deliverable shipped
  snapshots. Past architectural decisions are also captured there in
  deliverable form; ADRs distill the single decision out of the broader
  deliverable narrative.
