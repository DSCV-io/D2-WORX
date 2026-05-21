<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.GeoReference

> Parent: [`server/shared/dotnet/`](../README.md)

> **Status**: NOT IMPLEMENTED — tracked at [docs/v2/PHASE_1.md](../../../../docs/v2/PHASE_1.md).

## Purpose

Embedded geographic reference data — countries, IANA timezones, currencies, locales, regions. **NOT a service** — loaded once at process startup; consumed in-process with no network round-trip.

## Public API surface

- `IGeoReference` — single interface providing all reference-data lookups
  - `GetCountries() → IReadOnlyList<Country>`
  - `GetTimezones() → IReadOnlyList<Timezone>`
  - `GetCurrencies() → IReadOnlyList<Currency>`
  - `GetLocales() → IReadOnlyList<Locale>`
  - `GetRegionsForCountry(string countryCode) → IReadOnlyList<Region>`
  - Lookups by code / ID
- `GeoRefDataUpdatedEvent` — emitted when the embedded data is refreshed (consumed by services that cache derived state)
- DI registration: `services.AddD2GeoReference()` (loads data on first resolve)

## Dependencies

- `D2.Shared.Result` (lookup failures)
- `D2.Shared.Utilities`
- (Embedded data files shipped as resources)

## References

- [`../location/README.md`](../location/README.md) — `D2.Shared.Location` consumes this lib to validate country / state / region codes against the embedded reference data
- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) — embedded-vs-service decision rationale (see "Why embedded" below)

## Why embedded (not service)

- Reference data is small (few MB)
- Updates rarely (yearly at most for political boundaries)
- Every service consumer needs it on every request → in-process avoids N round-trips
- Updates ship via library version bump (per `versionize`); consumers re-deploy to pick up
