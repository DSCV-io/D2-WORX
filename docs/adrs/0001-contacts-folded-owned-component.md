<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0001: Contacts are a folded owned-component library, not a standalone contacts service or per-service contacts DB

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: TBD — contacts (Phase 2; design locked at PLAN 2026-05-30, not yet built)

## Context

"Contacts" (a person's or organization's name, email, phone, postal address) has been modeled three different ways across the project's history. This ADR records the move to the third and captures the lineage so implementers, auditors, and researchers do not act on stale assumptions.

**v1 (frozen snapshot at `/old/v1/D2-WORX/`).** `Contact` was a standalone aggregate living **inside the Geo service**: its own `contacts` table, a UUIDv7 primary key, and an external-key tuple `(ContextKey, RelatedEntityId)` (a `UNIQUE` index, hardened in a later migration) that let any other service address a contact it did not store. Contact PII was centralized in Geo. Two cross-service consumption patterns drove that centralization:

- **Auth ran a blocking SAGA against Geo on every profile mutation** (email / phone / name / locale / timezone change) — Geo was the system of record for contact PII; Auth held none of it locally (it mirrored only the display name into BetterAuth's own `user.name`). Compensating rollback on failure; hard-fail if Geo was down.
- **Comms resolved the contact at delivery time** (`GetContactsByIds`) to fetch the email/phone, and keyed channel-preferences by the Geo `contact_id`. A `ContactEviction` RabbitMQ broadcast invalidated every service's contact cache after each delete-and-recreate "update".

Auth also kept a separate `org_contact` junction (label + `isPrimary` + a pointer to a Geo contact) — it stored zero contact PII itself. No invoice/billing/shipping service ever existed; `billing_contact` was test/doc scaffolding only.

**Prior v2 plan (~2026-05-23, V2.md §5.6).** The Geo service was dissolved and `D2.Shared.Contacts` became a library — but still a **standalone** one: each consuming service stood up its **own contacts database** (`auth_contacts_db`, etc.), the library owned its `DbContext` + migrations + repository handlers, the `RelatedService*` external-key triple was **required** on every row, "updates" were modeled as delete-and-recreate-and-repoint, and Courier still resolved contacts at delivery time.

**Today (2026-05-30).** Re-examining usage surfaced that (a) a contact is almost always a *subset of a host entity*, and (b) the cross-service machinery in both prior models existed only to support a contact-PII centralization the project no longer wants — so the prior plan's apparatus (its own per-service DB, the `RelatedService*` ext-key, repository handlers, Courier-side resolution) falls away once contacts fold into their host entities. Research inputs: EF Core 10 owned-vs-complex-type capabilities + reusable-mapping patterns; DDD value-object-vs-entity-vs-owned modeling (snapshot-vs-reference for invoice addresses); GDPR data-minimization + right-to-erasure for per-host PII; and the v1 source itself.

## Decision

`D2.Shared.Contacts` is a **.NET-only library of value-object building blocks + reusable EF Core mapping**. It is **not a service, not a database, and exposes no cross-service contact lookup**. Contacts are **folded into each consuming service's own aggregates, tables, and `DbContext`**.

- **The library ships** (and owns zero migrations / zero `DbContext` / zero DB):
  - value-object building blocks with `Create(...) → D2Result<T>` smart constructors — `EmailAddress` / `PhoneNumber` (wrapping `D2.Shared.Validation`'s `IEmailValidator` / `IPhoneValidator`), `Personal`, `Professional`, and an address composed from `D2.Shared.Location` value objects (+ `IPostalCodeValidator`);
  - reusable `EntityTypeBuilder<TOwner>` extension methods — `EmbedContact` (inline snapshot), `OwnContactInTable` (1:1), `OwnsContactBook` (collection);
  - a generic, reflection-driven **redaction/correlation sweeper** over the host's EF model.
- **Three usage shapes → three EF mechanisms**: invoice billing/shipping = immutable **snapshot** (`ComplexProperty` / `OwnsOne`); user/org 1:1 = **owned component** (`OwnsOne`, optionally `+ ToTable`); org address book = **owned collection** (`OwnsMany` + `ToTable`). (Inline complex-type collections are not in EF Core 10 — deferred to EF 11 — so the collection shape uses `OwnsMany`.)
- **Identity:** UUIDv7 where a contact has its own row (address book), embedded otherwise — **not** content-addressed (contacts are PII; Location's hash-dedup trait deliberately does not extend to them). The required `RelatedService*` triple is **dropped**; the host's own FK/aggregate is the ownership.
- **Correlation + erasure keys:** every contact carries optional `Guid? UserId` / `Guid? OrgId`; guests/externals fall back to the channel address. Erasure is a subject-id fan-out (the library sweeper makes it uniform); legal-hold rows are field-redacted / crypto-shredded in place, not deleted.
- **Delivery preferences + consent are a separate concern**, owned centrally by **Courier** (suppression keyed by channel address; routing prefs keyed by subject id) — not by contacts. The caller passes resolved delivery info + a message category; Courier gates `WHETHER` / `WHICH` channel / `HOW`-`WHEN`. The detailed prefs/category-lane schema is **deferred** (mostly carried from v1).

## Consequences

**Positive.**

- No contacts service, no contacts DB, no cross-service lookup / SAGA / cache-eviction apparatus — large reduction in moving parts vs. both v1 and the prior plan.
- PII is purpose-limited per host (GDPR-aligned data-minimization); no central PII honeypot or cross-service PII store.
- **Inherently searchable even when embedded** — a `Contact` is a fixed, well-known shape carrying optional `UserId` / `OrgId`, so the reflection-driven sweeper discovers every contact-bearing entity directly from the host's EF model. "All of John's contact data", subject-keyed correlation, and redaction are therefore fully achievable across services *without* a central store: searchability is a property of the type's shape, not of where it is stored.
- Direct reuse of the just-built `D2.Shared.Validation` + `D2.Shared.Location` libraries; one-line EF folding for consumers.

**Negative / risks (with mitigations).**

- **Migration cascade** — a contact-shape change can require a migration in every adopting service (migrations live in the host context). Mitigated by **semver + additive-only-within-a-major**, per-service adoption cadence, and keeping the library pure (no business behavior); `D2.Shared.Validation` + `D2.Shared.Location` pinned transitively so validation can't skew independently of shape.
- **Validation version skew** across services on different library versions — bounded by additive-only discipline + transitive pinning.
- **Shared-kernel coupling** — contained by shipping only pure data + mapping + validation.

## Alternatives considered

- **Standalone per-service contacts DB (the prior v2 plan).** The one alternative genuinely weighed. Rejected: it carries a whole DB / `DbContext` / migration / repository / ext-key apparatus to support a cross-service resolution the folded model removes; the `RelatedService*` triple existed mainly for v1's Geo-centralization and Courier resolution, both now gone.

_Not alternatives (recorded to pre-empt the question):_ the v1 **central Geo-service contacts** model is prior art (see Context) — already abandoned before this decision, not re-litigated here. A **content-addressable contact** (Location's hash-dedup model) was never a candidate: the "treat contacts like Location" question only ever meant *folded into a host entity* (Location's structural trait), never *content-addressed* — contacts are PII, where dedup does not belong.

## References

- V2.md §5.6 — superseded design (struck through, preserved) + §5.6 (Revised 2026-05-30).
- v1 snapshot: `/old/v1/D2-WORX/backends/dotnet/services/Geo/` (`Geo.Domain/Entities/Contact.cs`, `Geo.Infra/Repository/Entities/ContactConfig.cs`, the `*Contact*` migrations, `ContactEvictionPublisher.cs`); `/old/v1/D2-WORX/backends/node/services/{auth,comms}/` (the `org_contact` junction; Comms delivery + `channel_preference`).
- EF Core 10 owned/complex types + reusable mapping; DDD snapshot-vs-reference; GDPR data-minimization + right-to-erasure for per-host PII — research captured in the 2026-05-30 PLAN discussion.
- [Michael Nygard's ADR essay](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) — the format this record follows.
