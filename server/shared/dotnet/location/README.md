<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Location

> **Status**: placeholder — not yet implemented.

## Purpose

Location value objects — `AdminLocation`, `Coordinates`, `StreetAddress`. **Content-addressable** (SHA-256 hash IDs — identical content produces the same ID, enabling built-in deduplication + cacheability). Immutable. Used wherever a service needs to record / reference a geographic location (sign-in events, contacts, file metadata, future payments).

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

- Storage — Location used by Edge WhoIs + future services

## Important

Locations are **immutable**. "Updates" are modeled as create-new + repoint-references + delete-old. Same hash content = same ID = same row in any consumer's local table. Built-in deduplication via the hash ID.
