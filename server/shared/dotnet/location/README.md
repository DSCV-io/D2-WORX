<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Location

> Parent: [`server/shared/dotnet/`](../README.md)

> **Status**: NOT IMPLEMENTED — tracked at [docs/v2/PHASE_1.md](../../../../docs/v2/PHASE_1.md).

## Purpose

Location value objects — `AdminLocation`, `Coordinates`, `StreetAddress`. **Content-addressable** (SHA-256 hash IDs — identical content produces the same ID, enabling built-in deduplication + cacheability). Immutable. Used wherever a service needs to record / reference a geographic location (sign-in events, contacts, file metadata).

## Public API surface

- `AdminLocation` — country / state / city / postal code (administrative hierarchy)
  - Factory: `AdminLocation.Create(country, state, city, postal)` — computes SHA-256 hash ID
- `Coordinates` — latitude / longitude pair
  - Factory: `Coordinates.Create(lat, lon)` — validates ranges
- `StreetAddress` — street + admin location + coordinates (composite for full physical addresses)
  - Factory: `StreetAddress.Create(street, adminLocation, coordinates)`
- All value objects are `record` types with `required init` properties + content-addressable hash IDs (64-char hex)

## Dependencies

- `D2.Shared.GeoReference` (validates country / state codes against reference data)
- `D2.Shared.Utilities` (`ToNullIfEmpty()` at boundaries, hash computation)
- `D2.Shared.Result` (factory validation)

## References

- [`docs/PATTERNS.md`](../../../../docs/PATTERNS.md) — content-addressable entities pattern (Location + WhoIs both use SHA-256 hash IDs)
- [`../geo-reference/README.md`](../geo-reference/README.md) — embedded reference data this lib validates against (country / state / region codes)

## Important

Locations are **immutable**. "Updates" are modeled as create-new + repoint-references + delete-old. Same hash content = same ID = same row in any consumer's local table. Built-in deduplication via the hash ID.
