<!--
Copyright (c) DCSV. All rights reserved.
-->

# location/

> Parent: [`server/shared/dotnet/`](../README.md)

Immutable, content-addressable location value objects. The cluster provides the pure-domain VOs and their `IPostalCodeValidator` boundary contract (`core/`) plus the reusable EF Core mapping helpers (`entity-framework-core/`).

- [`core/`](core/README.md) — **`D2.Shared.Location`**: three immutable value objects (`Coordinates`, `StreetAddress`, `AdminLocation`) plus `ComposeLocationHash` and `DefaultPostalCodeValidator`. Pure-domain — no EF Core, no NodaTime.
- [`entity-framework-core/`](entity-framework-core/README.md) — **`D2.Shared.Location.EntityFrameworkCore`**: per-VO complex-type mapping helpers (`MapStreetAddress`, `MapAdminLocation`, `MapCoordinates`) with max-length, value converters, and anonymize defaults. Ships no `DbContext`, no migrations.
