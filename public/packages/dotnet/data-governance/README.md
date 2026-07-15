<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# data-governance/

> Parent: [`public/packages/dotnet/`](../README.md)

GDPR anonymization framework. The cluster provides the vocabulary and marker interfaces every service shares (`abstractions/`) plus the EF Core engine, fluent decoration API, and startup model guard (`entity-framework-core/`).

- [`abstractions/`](abstractions/README.md) — **`DcsvIo.D2.DataGovernance.Abstractions`**: GDPR-anonymization markers (`IUserOwned`, `IOrgOwned`, `IExemptFromAnonymization`, `IAnonymizationTrackable`), the `[Anonymizable]` attribute, `AnonymizeKind` / `AnonymizationRule` / `AnonymizationOutcome`, and the `IAnonymizationEngine` seam. Zero EF Core, zero DI.
- [`entity-framework-core/`](entity-framework-core/README.md) — **`DcsvIo.D2.DataGovernance.EntityFrameworkCore`**: fluent `.Anonymize*` extension methods, `AnonymizableAttributeConvention`, `AnonymizationEngine` (Tier-A/B subject erasure), `AnonymizationModelValidator` (deny-by-default boot guard), and `AddD2DataGovernance` DI entry point.
