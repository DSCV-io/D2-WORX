<!--
Copyright (c) DCSV. All rights reserved.
-->

# Contacts

> Parent: [`server/shared/dotnet/`](../README.md)

> **Audience**: Backend .NET service engineers composing contact PII value objects + EF mapping into their own aggregates.

A folded owned-component contacts toolkit. The library provides composable value-object building blocks plus reusable Entity Framework Core mapping that fold into each host service's own entities and `DbContext` — it owns no database and ships no migrations.

- [`core/`](core/README.md) — **`D2.Shared.Contacts`**: the six composable, self-redacting PII value objects (`Personal`, `NameAffixes`, `Demographics`, `Professional`, `EmailAddress`, `PhoneNumber`), each constructed through a `Create(...) → D2Result<T>` smart constructor.
- [`entity-framework-core/`](entity-framework-core/README.md) — **`D2.Shared.Contacts.EntityFrameworkCore`**: per-VO complex-type and value-converter mapping helpers (`MapPersonal`, `MapProfessional`, `MapEmailAddress`, etc.) called from the host's `IEntityTypeConfiguration<T>`; wires max-length, converters, and anonymize defaults. Ships separately from `core/`.
