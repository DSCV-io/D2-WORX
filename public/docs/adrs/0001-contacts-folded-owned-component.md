<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0001: Contacts are a folded owned-component library, not a standalone contacts service or per-service contacts DB

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: `DcsvIo.D2.Contacts` library (composable PII value-object toolkit + reusable EF mapping)

## Context

"Contacts" (a person's or organization's name, email, phone, postal address) has been modeled three different ways across the project's history. This ADR records the move to the third and captures the lineage so implementers, auditors, and researchers do not act on stale assumptions.

**v1 (frozen snapshot at `/old/v1/D2-WORX/`).** `Contact` was a standalone aggregate living **inside the Geo service**: its own `contacts` table, a UUIDv7 primary key, and an external-key tuple `(ContextKey, RelatedEntityId)` (a `UNIQUE` index, hardened in a later migration) that let any other service address a contact it did not store. Contact PII was centralized in Geo. Two cross-service consumption patterns drove that centralization:

- **Auth ran a blocking SAGA against Geo on every profile mutation** (email / phone / name / locale / timezone change) — Geo was the system of record for contact PII; Auth held none of it locally (it mirrored only the display name into BetterAuth's own `user.name`). Compensating rollback on failure; hard-fail if Geo was down.
- **Comms resolved the contact at delivery time** (`GetContactsByIds`) to fetch the email/phone, and keyed channel-preferences by the Geo `contact_id`. A `ContactEviction` RabbitMQ broadcast invalidated every service's contact cache after each delete-and-recreate "update".

Auth also kept a separate `org_contact` junction (label + `isPrimary` + a pointer to a Geo contact) — it stored zero contact PII itself. No invoice/billing/shipping service ever existed; `billing_contact` was test/doc scaffolding only.

**Prior v2 plan (~2026-05-23, V2.md §5.6).** The Geo service was dissolved and `DcsvIo.D2.Contacts` became a library — but still a **standalone** one: each consuming service stood up its **own contacts database** (`auth_contacts_db`, etc.), the library owned its `DbContext` + migrations + repository handlers, the `RelatedService*` external-key triple was **required** on every row, "updates" were modeled as delete-and-recreate-and-repoint, and Courier still resolved contacts at delivery time.

**Today (2026-06-04).** Re-examining usage surfaced that (a) a contact is almost always a *subset of a host entity*, and (b) the cross-service machinery in both prior models existed only to support a contact-PII centralization the project no longer wants — so the prior plan's apparatus (its own per-service DB, the `RelatedService*` ext-key, repository handlers, Courier-side resolution) falls away once contacts fold into their host entities. During implementation, the initial design's EF wrapper-selector API was itself refined: empirical spikes confirmed that a pure VO-as-property model mapped via infra `IEntityTypeConfiguration<T>` — using EF complex types for multi-field VOs and value converters for single-value VOs — outperforms wrapper selectors on domain purity, SQL cleanliness, and engine compatibility. The implementation shipped this refined mechanism (see revision note in Decision). Research inputs: EF Core 10 complex-type + value-converter capabilities + reusable-mapping patterns; DDD value-object-vs-entity modeling (snapshot-vs-reference for invoice addresses); GDPR data-minimization + right-to-erasure for per-host PII; and the v1 source itself.

## Decision

> **Revision (2026-06-04):** The core decision — folded owned-component, no service, no DB — is unchanged. The _implementation mechanism_ was refined during build: the initial wrapper-selector EF API (`EmbedContact`/`OwnContactInTable`/`OwnsContactBook`) was replaced by a pure **VO-as-property + infra `IEntityTypeConfiguration<T>`** model using EF complex types and value converters. Details below.

`DcsvIo.D2.Contacts` is a **.NET-only composable PII value-object toolkit + reusable EF Core mapping**. It is **not a service, not a database, and exposes no cross-service contact lookup**. Contacts are **folded into each consuming service's own aggregates, tables, and `DbContext`**.

**Six composable PII value objects** (`Personal`, `NameAffixes`, `Demographics`, `Professional`, `EmailAddress`, `PhoneNumber`) in `DcsvIo.D2.Contacts`, each `Create(...) → D2Result<T>`, pure-domain, `[RedactData]` on PII, zero EF references. A separate **reusable EF mapping** library (`DcsvIo.D2.Contacts.EntityFrameworkCore`) ships per-VO helpers; no contacts DB, no contacts `DbContext`, no migrations.

**VO-as-property model.** Host aggregates hold plain VO-typed CLR properties — zero EF references, no EF attributes on domain types. All EF mapping (columns, converters, lengths, indexes, anonymization) lives in the host's infra `IEntityTypeConfiguration<T>`. Two CLR-shape concessions (not EF deps): VOs use `required init` properties (EF 10 materializes them via `GetUninitializedObject` + property setters — no parameterless ctor needed) and every mapped member is EF-settable via `init`.

**EF mechanism split.** Multi-field VOs (`Personal`, `NameAffixes`, `Demographics`, `Professional`) → EF **complex types** (`ComplexProperty`) — first-class queryable member columns, clean JOIN-free SQL, value semantics. Single-value VOs (`EmailAddress`, `PhoneNumber`) → EF **value converters** — one column, native unique index, root-scoped anonymize templates. `OwnsOne` / `OwnsMany` are not used: the anonymization engine's bulk `ExecuteUpdate` erasure cannot reach owned child-table rows or `.ToJson()` blobs — a principled boundary. Unbounded erasable collections with contact fields become first-class root entities (their own FK and metadata, with contact VOs mapped once).

**Personal correlation `HashId`.** `"v1." + SHA-256` over the normalized First|Middle|Last, **excluding** PreferredName — so a display-name change leaves the digest stable. This is **correlation, not dedup** — contacts are PII; Location's hash-dedup trait deliberately does not extend to them. `Professional` has no `HashId`.

**NameAffixes / Demographics split.** Honorific affixes (`Prefix`/`Suffix`, closed taxonomy + `Other` escape hatch) and demographics (`DateOfBirth` / `BiologicalSex`) are separate optional VOs from the core `Personal` name, so a host composes only what it needs.

**Field-constraints catalog.** A spec-driven codegen catalog (`public/contracts/validation/field-constraints.spec.json`) emits shared field-length `FieldConstraints` constants + three taxonomy enums (`NamePrefix` / `NameSuffix` / `BiologicalSex`) to .NET (`DcsvIo.D2.Validation.Abstractions`) and TypeScript (`@dcsv-io/d2-validation-abstractions`). The VO `Create` gates, Location, and frontend Zod schemas consume them — one source of truth for field-length caps.

**Consumes `DcsvIo.D2.DataGovernance`** (the standalone anonymization engine — ADR-0015). The library ships no sweeper; host entities are marked `IUserOwned` / `IOrgOwned` / `IAnonymizationTrackable` and VO columns are decorated via the fluent `.Anonymize*` API from `DcsvIo.D2.DataGovernance.EntityFrameworkCore`.

**The restructure outcome.** EF mapping is split per-area into independent siblings: `DcsvIo.D2.Contacts.EntityFrameworkCore` (contact-VO helpers) and `DcsvIo.D2.Location.EntityFrameworkCore` (`MapStreetAddress` / `MapAdminLocation` / `MapCoordinates` + `LocationVoDecorator`) with no dependency between them. A shared generic `DcsvIo.D2.EntityFrameworkCore` owns the VO-agnostic `CreateD2Index<TEntity>` typed migration helper (the EF-10 complex-member-index workaround — see Consequences). `DcsvIo.D2.Location` lives at `location/core/` mirroring `contacts/core/`. A native `ComplexTypePropertyBuilder<T>` `.Anonymize*` overload added to `DcsvIo.D2.DataGovernance.EntityFrameworkCore` enables both EF libs to use the fluent path uniformly.

**Correlation + erasure keys** (unchanged). Every contact-bearing host entity carries optional `Guid? UserId` / `Guid? OrgId`; guests/externals fall back to the channel address. Erasure is a subject-id fan-out via `DcsvIo.D2.DataGovernance`; legal-hold rows are field-overwritten with tombstone values in place.

**Delivery preferences + consent** remain a separate concern owned centrally by Courier (suppression keyed by channel address; routing prefs keyed by subject id).

## Consequences

**Positive.**

- No contacts service, no contacts DB, no cross-service lookup / SAGA / cache-eviction apparatus — large reduction in moving parts vs. both v1 and the prior plan.
- PII is purpose-limited per host (GDPR-aligned data-minimization); no central PII honeypot or cross-service PII store.
- **Subject-keyed erasure and correlation** are achieved by marking host entities with the `DcsvIo.D2.DataGovernance` markers and decorating VO columns via the fluent `.Anonymize*` API; the engine sweeps the host model. No bespoke sweeper ships.
- Direct reuse of `DcsvIo.D2.Validation` + `DcsvIo.D2.Location` libraries; one-line EF folding for consumers.
- Complex-type VOs are first-class queryable columns (`WHERE personal_first_name = 'John'`, `ORDER BY professional_company_name`) — no JOIN overhead, no shadow-key columns.

**Negative / risks (with mitigations).**

- **Migration cascade** — a contact-shape change can require a migration in every adopting service (migrations live in the host context). Mitigated by **semver + additive-only-within-a-major**, per-service adoption cadence, and keeping the library pure (no business behavior); `DcsvIo.D2.Validation` + `DcsvIo.D2.Location` pin transitively so validation cannot skew independently of shape.
- **Validation version skew** across services on different library versions — bounded by additive-only discipline + transitive pinning.
- **EF Core 10 complex-member-index limitation** — model-aware indexes on `ComplexProperty` member columns are not expressible in EF 10 (fluent `HasIndex(u => u.Vo.Member)` throws; metadata-path indexes are silently discarded at finalization). The ONLY EF-10 path is a raw `migrationBuilder.CreateIndex(...)` line in the host migration. The toolkit ships `CreateD2Index<TEntity>(u => u.Vo.Member)` (`DcsvIo.D2.EntityFrameworkCore`) to keep that line typed and clean. EF 11 (issue [#31246](https://github.com/dotnet/efcore/issues/31246), merged 2026-05-19, shipping ~Nov 2026) makes `HasIndex(u => u.Vo.Member)` native; migrating existing `CreateD2Index` calls to fluent `HasIndex` once the host adopts EF 11 is a tracked follow-up. Value-converter indexes and complex-member _queries_ have no such limitation.
- **Shared-kernel coupling** — contained by shipping only pure data + mapping + validation, no business behavior.

## Alternatives considered

- **Standalone per-service contacts DB (the prior v2 plan).** The one alternative genuinely weighed. Rejected: it carries a whole DB / `DbContext` / migration / repository / ext-key apparatus to support a cross-service resolution the folded model removes; the `RelatedService*` triple existed mainly for v1's Geo-centralization and Courier resolution, both now gone.

_Not alternatives (recorded to pre-empt the question):_ the v1 **central Geo-service contacts** model is prior art (see Context) — already abandoned before this decision, not re-litigated here. A **content-addressable contact** (Location's hash-dedup model) was never a candidate: the "treat contacts like Location" question only ever meant *folded into a host entity* (Location's structural trait), never *content-addressed* — contacts are PII, where dedup does not belong.

## References

- [`V2.md §5.6`](../v2/V2.md) — superseded planning design (struck through, preserved) + §5.6 (Revised 2026-05-30, built-note 2026-06-04).
- Per-lib READMEs for the shipped surface: [`contacts/core/`](../../public/packages/dotnet/contacts/core/README.md) · [`contacts/entity-framework-core/`](../../public/packages/dotnet/contacts/entity-framework-core/README.md) · [`location/entity-framework-core/`](../../public/packages/dotnet/location/entity-framework-core/README.md) · [`entity-framework-core/`](../../public/packages/dotnet/entity-framework-core/README.md).
- `docs/PATTERNS.md` — monorepo-private contact-VO / EF VO-mapping / `CreateD2Index` / field-constraints patterns. **Not required for a public clone** of this ADR tree.
- ADR-0015 ([`0015-anonymization-data-governance.md`](0015-anonymization-data-governance.md)) — the standalone anonymization engine this library consumes.
- v1 snapshot: `/old/v1/D2-WORX/backends/dotnet/services/Geo/` (`Geo.Domain/Entities/Contact.cs`, `Geo.Infra/Repository/Entities/ContactConfig.cs`, the `*Contact*` migrations, `ContactEvictionPublisher.cs`); `/old/v1/D2-WORX/backends/node/services/{auth,comms}/` (the `org_contact` junction; Comms delivery + `channel_preference`).
- EF Core 10 complex types + value converters + reusable mapping; DDD snapshot-vs-reference; GDPR data-minimization + right-to-erasure — research captured in the 2026-05-30 and 2026-06-03 PLAN discussions.
- [Michael Nygard's ADR essay](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) — the format this record follows.
