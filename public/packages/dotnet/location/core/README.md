<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Location

> Parent: [`public/packages/dotnet/`](../../README.md)

> **Audience**: Backend .NET service engineers attaching location or postal-address data to domain entities.

> Hash-deduplicatable geographic value objects for handlers and service code attaching location data to entities — covers `Coordinates`, `StreetAddress`, `AdminLocation`, the `ComposeLocationHash` free function, and the `IPostalCodeValidator` boundary contract with a global-range `DefaultPostalCodeValidator`. Produces deterministic identity hashes for civic locations, with normalized variants that dedup typo-distance inputs across languages and scripts.

## Purpose

Three immutable, content-addressable value objects + one free hash composer + one boundary validator. Every factory returns `D2Result<T>` (smart-constructor pattern); same content produces the same `HashId` (`"v1." + SHA-256 hex`), so duplicate insertions across services or repeated submissions naturally collapse to the same row. Pure-domain layer policy: depends on `D2.Shared.Geo.Abstractions` (typed code enums), `D2.Shared.Result` (D2Result factories), `D2.Shared.Utilities` (Falsey/Truthy/CleanStr boundary helpers and the canonical `NormalizeForHash` extension), and `D2.Shared.Validation.Abstractions` (FieldConstraints length caps) — no infrastructure deps, no NodaTime, no observability surface.

## Public API surface

- [`ValueObjects/Coordinates.cs`](ValueObjects/Coordinates.cs) — `sealed record` with three universal representations (lat/lon decimal degrees, geohash-10, OLC plus-code-13) + optional accuracy metadata. Three factories:
  - `Coordinates.Create(latitude, longitude, accuracyMeters?)` — from decimal degrees.
  - `Coordinates.FromGeohash(geohash, accuracyMeters?)` — from a 1-12 char geohash (truncated / re-encoded to canonical 10).
  - `Coordinates.FromPlusCode(plusCode, accuracyMeters?)` — from a valid OLC plus-code.

  All three converge on the canonical geohash-10 cell-center so inputs in different forms representing the same physical ~1m cell produce byte-identical `HashId` values. Accuracy is metadata — NOT included in the hash.
- [`ValueObjects/StreetAddress.cs`](ValueObjects/StreetAddress.cs) — `sealed record` with 5 free-text lines (`Line1` required, `Line2..Line5` optional, no gap rule). Two-stage normalization: stored form preserves case + strips decorative punctuation; hash form forwards to `D2.Shared.Utilities` `NormalizeForHash` extension (upper-case + NFD-strip combining marks + Unicode-category filter: Letter / Decimal-digit / ASCII space). Each line is capped at `FieldConstraints.STREET_LINE_MAX` (255 chars, measured on the post-clean stored value).
  - `StreetAddress.Create(line1, line2?, line3?, line4?, line5?)` returns `D2Result<StreetAddress>`.
  - Failure keys: `TK.Geo.Validation.ADDRESS_LINE1_REQUIRED` (line1 empty after clean), `TK.Geo.Validation.STREET_LINE_TOO_LONG` (any line exceeds `STREET_LINE_MAX` after clean).
- [`ValueObjects/AdminLocation.cs`](ValueObjects/AdminLocation.cs) — `sealed record` with administrative hierarchy: country, subdivision, city, postal code (any subset). Cross-field coherence is enforced when both country + subdivision are supplied; subdivision-only callers have country auto-populated from `SubdivisionCode.ParentCountry`. City is capped at `FieldConstraints.CITY_MAX` (255 chars) and postal code at `FieldConstraints.POSTAL_CODE_MAX` (16 chars), both measured on the post-clean value. The length cap is an unconditional structural floor that fires before the optional `IPostalCodeValidator`.
  - `AdminLocation.Create(countryIso31661Alpha2Code?, subdivisionIso31662Code?, city?, postalCode?, postalCodeValidator?)` returns `D2Result<AdminLocation>`.
  - Failure keys: `TK.Geo.Validation.ADMIN_EMPTY_RECORD` (all fields null/empty), `TK.Geo.Validation.ADMIN_COUNTRY_SUBDIVISION_MISMATCH` (country/subdivision mismatch), `TK.Geo.Validation.CITY_TOO_LONG` (city exceeds `CITY_MAX`), `TK.Geo.Validation.POSTAL_CODE_TOO_LONG` (postal exceeds `POSTAL_CODE_MAX`), `TK.Geo.Validation.POSTAL_CODE_INVALID` (validator failure).
- [`ComposeLocationHash.cs`](ComposeLocationHash.cs) — `static class` with `Compose(Coordinates?, StreetAddress?, AdminLocation?): string?` — joins the three component `HashId`s into a single `"v1."`-prefixed hash. All-null input returns `null` (location absent; not an error).
- [`IPostalCodeValidator.cs`](IPostalCodeValidator.cs) — boundary contract `D2Result<string> Validate(string?, CountryCode?)`. Lives in `D2.Shared.Location` (not Abstractions) so the DI seam stays out of pure-vocabulary projects.
- [`DefaultPostalCodeValidator.cs`](DefaultPostalCodeValidator.cs) — `sealed class` implementing the global-range shape check (3-10 alphanumeric characters; internal spaces and hyphens allowed; alphanumeric at both ends). Country-blind by design; consumers override for strict per-country validation.
- [`DependencyInjection.cs`](DependencyInjection.cs) — `extension(IServiceCollection)` block-form `AddD2Location()` registers `IPostalCodeValidator → DefaultPostalCodeValidator` (singleton, idempotent via `TryAddSingleton`).

## Dependencies

- `D2.Shared.Geo.Abstractions` — typed `CountryCode` enum + `SubdivisionCode` wrapper struct (including `SubdivisionCode.ParentCountry` for `AdminLocation` coherence).
- `D2.Shared.Result` — `D2Result<T>` semantic factories (`Ok`, `ValidationFailed`).
- `D2.Shared.Utilities` — `Falsey()` / `Truthy()` / `CleanStr()` / `NormalizeForHash()` extension methods. The `NormalizeForHash` extension is the canonical implementation for hash canonicalization; `StreetAddress.NormalizeForHash` is a thin internal forwarder to it.
- `D2.Shared.Validation.Abstractions` — `FieldConstraints` length cap constants (`STREET_LINE_MAX`, `CITY_MAX`, `POSTAL_CODE_MAX`) consumed by the VO `Create` gates.
- `Microsoft.Extensions.DependencyInjection.Abstractions` (NuGet) — `IServiceCollection` receiver for the `AddD2Location` registration extension.

**NO `D2.Shared.Geo.Default`, NO NodaTime, NO logging, NO observability — pure-domain by design.**

## Security / PII

The value objects in this lib hold geographic and postal-address data that meets the GDPR definition of personally identifiable information:

- `Coordinates` at geohash-10 / ~1m precision is **precise geolocation data — PII per GDPR** (geographic data: lat/long beyond country level).
- `StreetAddress` (all 5 lines) is **postal-address PII — directly GDPR-sensitive**.
- `AdminLocation.City` + `AdminLocation.PostalCode` are **PII when combined with name or other identifying fields** (city+postal beyond country level).

**Consumer-side contract**: handlers, repositories, audit-log emitters, and any DTO or entity holding fields of these types MUST apply `[RedactData]` on the property/field. Example:

```csharp
public sealed record Sighting
{
    [RedactData] public Coordinates? Coords { get; init; }
    [RedactData] public StreetAddress? Address { get; init; }
    [RedactData] public AdminLocation? Admin { get; init; }
}
```

This lib does NOT itself carry `[RedactData]` on the VO definitions — it has no `[LoggerMessage]` declarations and no JSON-serialization surface; the annotation is meaningful only at the CONSUMER layer where the PII reaches a sink.

## Telemetry

N/A — pure-domain value-object lib, no telemetry surface by design. Consumer-side handlers carry the telemetry surface; per-VO instrumentation would inflate handler hot paths with no value-add.

## Configuration / Options

N/A — no env vars, no appsettings, no Options record. `DefaultPostalCodeValidator` carries hardcoded constants (the global-range regex + a 50ms `matchTimeout` — see source); per-country validation is a consumer DI override via `services.Replace(...)`. No configurable defaults beyond the validator DI-override seam.

## Usage examples

```csharp
using D2.Shared.Geo.Abstractions;
using D2.Shared.Location;
using D2.Shared.Location.ValueObjects;

// 1. Construct any subset of the three value objects (each returns D2Result<T>).
var coordsResult = Coordinates.Create(40.7128, -74.0060);
if (!coordsResult.Success)
    return coordsResult.AsFailure<MyAggregate>(); // propagate ValidationFailed

var streetResult = StreetAddress.Create(line1: "350 Fifth Ave", line2: "Floor 86");
if (!streetResult.Success)
    return streetResult.AsFailure<MyAggregate>();

var adminResult = AdminLocation.Create(
    countryIso31661Alpha2Code: CountryCode.US,
    subdivisionIso31662Code: SubdivisionCode.US_NY,
    city: "New York",
    postalCode: "10118",
    postalCodeValidator: _validator); // injected IPostalCodeValidator
if (!adminResult.Success)
    return adminResult.AsFailure<MyAggregate>();

// 2. Each VO carries its own content-addressable HashId.
//    coordsResult.Data.HashId  == "v1.<64 hex>"
//    streetResult.Data.HashId  == "v1.<64 hex>"
//    adminResult.Data.HashId   == "v1.<64 hex>"

// 3. Compose into a single location identity (free function, returns string?).
string? locationHash = ComposeLocationHash.Compose(
    coordsResult.Data,
    streetResult.Data,
    adminResult.Data);
// Null only when all three inputs are null (location absent — not an error).
```

DI wire-up (composition root):

```csharp
services.AddD2Location();          // registers IPostalCodeValidator → DefaultPostalCodeValidator
services.Replace(ServiceDescriptor // optional: override with country-specific validator
    .Singleton<IPostalCodeValidator, MyStrictPostalCodeValidator>());
```

## Important / usage notes

Locations are **immutable**. "Updates" are modeled as create-new + repoint-references + delete-old. Same hash content = same `HashId` = same row in any consumer's local table. Built-in deduplication via the hash ID.

Hash-algorithm stability is enforced by the [`contracts/location/parity-fixtures.json`](../../../contracts/location/parity-fixtures.json) fixture — every `HashId` / `expectedOutcome` row is asserted by `LocationHashDeterminismTests` in `D2.Shared.Tests`. A byte divergence means the hash algorithm changed and would silently produce duplicate records for previously-identical content-addressable entities.

`ComposeLocationHash.Compose` returns `string?` (NOT `D2Result<string>`) — the operation cannot fail (inputs are already-validated VOs or null); `null` means location is absent, not an error. Returning a plain value rather than a `D2Result` is appropriate here because the method has no failure mode — it is a pure composition of already-validated inputs.

## References

- [`../entity-framework-core/README.md`](../entity-framework-core/README.md) — the sibling `D2.Shared.Location.EntityFrameworkCore` lib that maps these VOs onto a host's EF Core model (`MapStreetAddress` / `MapAdminLocation` / `MapCoordinates` + per-field anonymize defaults).
- [`docs/PATTERNS.md`](../../../docs/PATTERNS.md) — content-addressable entities + hash composition.
- [`../geo/abstractions/README.md`](../../geo/abstractions/README.md) — the typed `CountryCode` + `SubdivisionCode` surface this lib consumes.
- [`contracts/location/parity-fixtures.json`](../../../contracts/location/parity-fixtures.json) — hash-determinism fixture file.
