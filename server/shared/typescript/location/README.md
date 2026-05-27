<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/location

> Parent: [`server/shared/typescript/`](../README.md)

> Hash-deduplicatable geographic value objects for SvelteKit BFF + Node services attaching location data to entities — covers `Coordinates`, `StreetAddress`, `AdminLocation`, the `composeLocationHash` free function, and the `IPostalCodeValidator` boundary contract with a global-range `defaultPostalCodeValidator`. Produces deterministic identity hashes for civic locations with normalized variants that dedup typo-distance inputs across languages and scripts. Mirrors `D2.Shared.Location` byte-for-byte over the cross-language parity fixture.

## Purpose

Three immutable, content-addressable value objects + one free hash composer + one boundary validator. Every factory returns `D2Result<T>` (smart-constructor pattern); same content produces the same `hashId` (`"v1." + sha256 hex`), so duplicate insertions across services or repeated submissions naturally collapse to the same row. Pure-domain layer policy: depends only on `@d2/geo-abstractions` (typed code enums), `@d2/result` (`D2Result<T>` factories), and `@d2/utilities` (`falsey` / `truthy` / `cleanStr` boundary helpers).

## Public API surface

- [`Coordinates`](src/value-objects/coordinates.ts) — branded interface with three universal representations (lat/lon decimal degrees, geohash-10, OLC plus-code-12) + optional accuracy metadata. Three factory functions:
  - `createCoordinates(latitude, longitude, accuracyMeters?)`
  - `coordinatesFromGeohash(geohash, accuracyMeters?)`
  - `coordinatesFromPlusCode(plusCode, accuracyMeters?)`

  All three converge on the canonical geohash-10 cell-center so cross-factory inputs for the same physical ~1m cell produce byte-identical `hashId` values. `accuracyMeters` is metadata — NOT included in the hash.

- [`StreetAddress`](src/value-objects/street-address.ts) — branded interface with 5 free-text lines (`line1` required, `line2..line5` optional, no gap rule). Two-stage normalization: stored form preserves case + strips decorative punctuation; hash form upper-cases + NFD-strips combining marks + applies a Unicode-category filter (Letter / Decimal-digit / ASCII space only).
  - `createStreetAddress(line1, line2?, line3?, line4?, line5?)` returns `D2Result<StreetAddress>`.
  - `normalizeForHash(line)` — internal helper exported for parity-test pin coverage of the hash-form normalization.
- [`AdminLocation`](src/value-objects/admin-location.ts) — branded interface with administrative hierarchy: country, subdivision, city, postal code (any subset). Cross-field coherence is enforced when both country + subdivision are supplied; subdivision-only callers have country auto-populated from the ISO 3166-2 prefix.
  - `createAdminLocation(countryCode?, subdivisionCode?, city?, postalCode?, postalCodeValidator?)` returns `D2Result<AdminLocation>`.
- [`composeLocationHash`](src/compose-location-hash.ts) — `composeLocationHash(c?, s?, a?): string | undefined`. Joins the three component `hashId`s into a single `"v1."`-prefixed hash. All-undefined / all-null input returns `undefined` (location absent; not an error).
- [`IPostalCodeValidator`](src/postal-code-validator.ts) — boundary contract `validate(postalCode?, countryCode?): D2Result<string>`.
- [`defaultPostalCodeValidator()`](src/postal-code-validator.ts) — returns the singleton default implementation (global-range shape check; 3-10 alphanumeric characters; internal spaces and hyphens allowed; alphanumeric at both ends). Country-blind by design; consumers wanting strict per-country validation implement their own `IPostalCodeValidator`.

The internal encoders (`encodeGeohash` / `decodeGeohash` / `truncateOrPadGeohash` / `isValidGeohash` / `encodePlusCode` / `decodePlusCode` / `isValidPlusCode`) are also exported for cross-language parity test fixtures + advanced consumers needing direct arithmetic without going through the full `Coordinates` pipeline.

## Dependencies

- `@d2/geo-abstractions` — typed `CountryCode` const + `SubdivisionCode` branded string (consumes `asSubdivisionCode` + the ISO 3166-2 prefix convention for parent-country derivation).
- `@d2/result` — `D2Result<T>` semantic factories (`ok`, `validationFailed`).
- `@d2/utilities` — `falsey` / `truthy` / `cleanStr` boundary helpers.

**No infrastructure deps, no runtime UI deps.** Hash computation uses Node's built-in `node:crypto` (`createHash`); the package is Node-only at the moment (mirrors the .NET counterpart's runtime profile).

## Security / PII

The value objects in this lib hold geographic and postal-address data that meets the GDPR definition of personally identifiable information:

- `Coordinates` at geohash-10 / ~1m precision is **precise geolocation data — PII per GDPR** (lat/long beyond country level).
- `StreetAddress` (all 5 lines) is **postal-address PII — directly GDPR-sensitive**.
- `AdminLocation.city` + `AdminLocation.postalCode` are **PII when combined with name or other identifying fields** (city+postal beyond country level).

**Consumer-side contract**: handlers, repositories, and any log-sink / serializer-side code holding these types MUST scrub them at the boundary. TS has no decorator-attribute equivalent to .NET's `[RedactData]`; the convention is custom log-sink masking + serializer-time scrubbing at the call site.

## Telemetry

N/A — pure-domain value-object lib, no telemetry surface by design. Consumer-side handlers carry the telemetry surface; per-VO instrumentation would inflate handler hot paths with no value-add.

## Configuration / Options

N/A — no env vars, no config schema. `defaultPostalCodeValidator()` is parameterless and country-blind by design; consumers wanting strict per-country validation implement their own `IPostalCodeValidator`.

## Usage examples

```ts
import {
  createCoordinates,
  createStreetAddress,
  createAdminLocation,
  composeLocationHash,
  defaultPostalCodeValidator,
} from "@d2/location";
import { CountryCodeConst, asSubdivisionCode } from "@d2/geo-abstractions";
import type { CountryCode } from "@d2/geo-abstractions";

// 1. Construct any subset of the three value objects (each returns D2Result<T>).
const coords = createCoordinates(40.7128, -74.006);
if (!coords.success) return coords; // propagate ValidationFailed

const street = createStreetAddress("350 Fifth Ave", "Floor 86");
if (!street.success) return street;

const admin = createAdminLocation(
  CountryCodeConst.US as CountryCode,
  asSubdivisionCode("US-NY"),
  "New York",
  "10118",
  defaultPostalCodeValidator(),
);
if (!admin.success) return admin;

// 2. Each VO carries its own content-addressable hashId.
//    coords.data.hashId  === "v1.<64 hex>"
//    street.data.hashId  === "v1.<64 hex>"
//    admin.data.hashId   === "v1.<64 hex>"

// 3. Compose into a single location identity (free function, returns string | undefined).
const locationHash = composeLocationHash(coords.data, street.data, admin.data);
// `undefined` only when all three inputs are undefined (location absent — not an error).
```

## Implementation notes

The cross-language parity fixture at [`contracts/location/parity-fixtures.json`](../../../contracts/location/parity-fixtures.json) is generated from this TS implementation via [`emit-fixture.mjs`](emit-fixture.mjs). When fixture cases need to be added (new encoding edge case, new normalization variant), regenerate with:

```bash
node server/shared/typescript/location/emit-fixture.mjs > contracts/location/parity-fixtures.json
```

The .NET side asserts byte-identical output against the same fixture file — divergence fails the build.

## Important / usage notes

Locations are **immutable**. "Updates" are modeled as create-new + repoint-references + delete-old. Same hash content = same `hashId` = same row in any consumer's local table. Built-in deduplication via the hash ID.

Cross-language parity is enforced by the [`contracts/location/parity-fixtures.json`](../../../contracts/location/parity-fixtures.json) fixture — every `hashId` / `expectedOutcome` row is asserted on BOTH the TypeScript side (`@d2/contract-tests/tests/location.parity.test.ts`) and the .NET side (`CrossLanguageLocationParityTests`). A byte-equal divergence on any case fails the build.

`composeLocationHash` returns `string | undefined` (not `D2Result<string>`) — the operation cannot fail (inputs are already-validated VOs or null/undefined); `undefined` means location is absent, not an error. Documented §17 carve-out.

**`/u` flag mandate** — every Unicode-property regex (`\p{L}` / `\p{Nd}` / etc.) in `normalizeForHash` and `cleanStored` MUST carry the `/u` flag. Without it, `\p{...}` escapes are unsupported and surrogate-pair emoji silently bypass the filter, producing wrong hash output. Auditor greps for any `/\p{/` without `u` flag should return zero hits.

## References

- [`server/shared/dotnet/location/README.md`](../../dotnet/location/README.md) — .NET counterpart.
- [`contracts/location/parity-fixtures.json`](../../../contracts/location/parity-fixtures.json) — cross-language fixture file.
- [`emit-fixture.mjs`](emit-fixture.mjs) — one-shot Node script to regenerate the fixture from the TS implementation (run via `node server/shared/typescript/location/emit-fixture.mjs > contracts/location/parity-fixtures.json`).
