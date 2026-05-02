<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.GeoReference

> **Status**: placeholder — not yet implemented.

## Purpose

Embedded geographic reference data — countries, IANA timezones, currencies, locales, regions. **NOT a service** (replaces v1 D2.Geo's reference-data RPCs). Loaded once at process startup; consumed in-process with no network round-trip.

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

- Embedded library replaces what would otherwise be reference-data RPCs from a Geo service

## Why embedded (not service)

- Reference data is small (few MB)
- Updates rarely (yearly at most for political boundaries)
- Every service consumer needs it on every request → in-process avoids N round-trips
- Updates ship via library version bump (per `versionize`); consumers re-deploy to pick up
