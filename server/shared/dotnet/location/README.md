<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Location

> Parent: [`server/shared/dotnet/`](../README.md)

> **Audience**: Backend .NET service engineers attaching location or postal-address data to domain entities.

> Hash-deduplicatable geographic value objects for handlers and service code attaching location data to entities — covers `Coordinates`, `StreetAddress`, `AdminLocation`, the `ComposeLocationHash` free function, and the `IPostalCodeValidator` boundary contract with a global-range `DefaultPostalCodeValidator`. Produces deterministic identity hashes for civic locations, with normalized variants that dedup typo-distance inputs across languages and scripts.

## Purpose

Three immutable, content-addressable value objects + one free hash composer + one boundary validator. Every factory returns `D2Result<T>` (smart-constructor pattern); same content produces the same `HashId` (`"v1." + SHA-256 hex`), so duplicate insertions across services or repeated submissions naturally collapse to the same row. Pure-domain layer policy: depends only on `D2.Shared.Geo.Abstractions` (typed code enums), `D2.Shared.Result` (D2Result factories), and `D2.Shared.Utilities` (Falsey/Truthy/CleanStr boundary helpers) — no infrastructure deps, no NodaTime, no observability surface.

## Public API surface

- [`ValueObjects/Coordinates.cs`](ValueObjects/Coordinates.cs) — `sealed record` with three universal representations (lat/lon decimal degrees, geohash-10, OLC plus-code-12) + optional accuracy metadata. Three factories:
  - `Coordinates.Create(latitude, longitude, accuracyMeters?)` — from decimal degrees.
  - `Coordinates.FromGeohash(geohash, accuracyMeters?)` — from a 1-12 char geohash (truncated / re-encoded to canonical 10).
  - `Coordinates.FromPlusCode(plusCode, accuracyMeters?)` — from a valid OLC plus-code.

  All three converge on the canonical geohash-10 cell-center so inputs in different forms representing the same physical ~1m cell produce byte-identical `HashId` values. Accuracy is metadata — NOT included in the hash.
- [`ValueObjects/StreetAddress.cs`](ValueObjects/StreetAddress.cs) — `sealed record` with 5 free-text lines (`Line1` required, `Line2..Line5` optional, no gap rule). Two-stage normalization: stored form preserves case + strips decorative punctuation; hash form upper-cases + NFD-strips combining marks + applies a Unicode-category filter (keeps Letter / Decimal-digit / ASCII space).
  - `StreetAddress.Create(line1, line2?, line3?, line4?, line5?)` returns `D2Result<StreetAddress>`.
- [`ValueObjects/AdminLocation.cs`](ValueObjects/AdminLocation.cs) — `sealed record` with administrative hierarchy: country, subdivision, city, postal code (any subset). Cross-field coherence is enforced when both country + subdivision are supplied; subdivision-only callers have country auto-populated from `SubdivisionCode.ParentCountry`.
  - `AdminLocation.Create(countryIso31661Alpha2Code?, subdivisionIso31662Code?, city?, postalCode?, postalCodeValidator?)` returns `D2Result<AdminLocation>`.
- [`ComposeLocationHash.cs`](ComposeLocationHash.cs) — `static class` with `Compose(Coordinates?, StreetAddress?, AdminLocation?): string?` — joins the three component `HashId`s into a single `"v1."`-prefixed hash. All-null input returns `null` (location absent; not an error).
- [`IPostalCodeValidator.cs`](IPostalCodeValidator.cs) — boundary contract `D2Result<string> Validate(string?, CountryCode?)`. Lives in `D2.Shared.Location` (not Abstractions) so the DI seam stays out of pure-vocabulary projects.
- [`DefaultPostalCodeValidator.cs`](DefaultPostalCodeValidator.cs) — `sealed class` implementing the global-range shape check (3-10 alphanumeric characters; internal spaces and hyphens allowed; alphanumeric at both ends). Country-blind by design; consumers override for strict per-country validation.
- [`DependencyInjection.cs`](DependencyInjection.cs) — `extension(IServiceCollection)` block-form `AddD2Location()` registers `IPostalCodeValidator → DefaultPostalCodeValidator` (singleton, idempotent via `TryAddSingleton`).

## Dependencies

- `D2.Shared.Geo.Abstractions` — typed `CountryCode` enum + `SubdivisionCode` wrapper struct (including `SubdivisionCode.ParentCountry` for `AdminLocation` coherence).
- `D2.Shared.Result` — `D2Result<T>` semantic factories (`Ok`, `ValidationFailed`).
- `D2.Shared.Utilities` — `Falsey()` / `Truthy()` / `CleanStr()` extension methods.
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

`ComposeLocationHash.Compose` returns `string?` (NOT `D2Result<string>`) — the operation cannot fail (inputs are already-validated VOs or null); `null` means location is absent, not an error. Documented §17 carve-out.

## References

- [`docs/PATTERNS.md`](../../../docs/PATTERNS.md) — content-addressable entities + hash composition.
- [`../geo-abstractions/README.md`](../geo-abstractions/README.md) — the typed `CountryCode` + `SubdivisionCode` surface this lib consumes.
- [`contracts/location/parity-fixtures.json`](../../../contracts/location/parity-fixtures.json) — hash-determinism fixture file.
