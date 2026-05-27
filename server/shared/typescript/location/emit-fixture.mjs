// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// One-shot script to generate the cross-language parity fixture.
// Run via: `node server/shared/typescript/location/emit-fixture.mjs > contracts/location/parity-fixtures.json`
// The script computes expected hashes using the TS implementation; the
// .NET parity test asserts byte-identical output for every row.

import {
  composeLocationHash,
  createAdminLocation,
  createCoordinates,
  createStreetAddress,
  coordinatesFromPlusCode,
  normalizeForHash,
} from "./dist/index.js";

const CountryCodeUS = "US";
const SubUSNY = "US-NY";

const cases = [];

// === Single-VO cases ===

// 1. coords-only NYC via Create(lat, lon)
{
  const r = createCoordinates(40.7128, -74.006);
  cases.push({
    name: "coords-only-nyc-create",
    kind: "coordinates",
    factory: "create",
    inputs: { latitude: 40.7128, longitude: -74.006 },
    expectedHashId: r.data.hashId,
    expectedGeohash: r.data.geohash,
    expectedPlusCode: r.data.plusCode,
    expectedLatitude: r.data.latitude,
    expectedLongitude: r.data.longitude,
  });
}

// 2. street-only "123 Main St"
{
  const r = createStreetAddress("123 Main St");
  cases.push({
    name: "street-only-main",
    kind: "street-address",
    inputs: { line1: "123 Main St" },
    expectedHashId: r.data.hashId,
  });
}

// 3. admin-only US country
{
  const r = createAdminLocation(CountryCodeUS);
  cases.push({
    name: "admin-only-us-country",
    kind: "admin-location",
    inputs: { countryCode: "US" },
    expectedHashId: r.data.hashId,
  });
}

// === 2-of-3 compose combos ===
const c = createCoordinates(40.7128, -74.006).data;
const s = createStreetAddress("123 Main St").data;
const a = createAdminLocation(CountryCodeUS, undefined, "Brooklyn").data;

cases.push({
  name: "compose-coords-and-street",
  kind: "compose",
  inputs: {
    coordinates: { factory: "create", args: [40.7128, -74.006] },
    streetAddress: { line1: "123 Main St" },
    adminLocation: null,
  },
  expectedComposeHash: composeLocationHash(c, s, undefined),
});

cases.push({
  name: "compose-coords-and-admin",
  kind: "compose",
  inputs: {
    coordinates: { factory: "create", args: [40.7128, -74.006] },
    streetAddress: null,
    adminLocation: { countryCode: "US", city: "Brooklyn" },
  },
  expectedComposeHash: composeLocationHash(c, undefined, a),
});

cases.push({
  name: "compose-street-and-admin",
  kind: "compose",
  inputs: {
    coordinates: null,
    streetAddress: { line1: "123 Main St" },
    adminLocation: { countryCode: "US", city: "Brooklyn" },
  },
  expectedComposeHash: composeLocationHash(undefined, s, a),
});

// === All-3 present ===
cases.push({
  name: "compose-all-three",
  kind: "compose",
  inputs: {
    coordinates: { factory: "create", args: [40.7128, -74.006] },
    streetAddress: { line1: "123 Main St" },
    adminLocation: { countryCode: "US", city: "Brooklyn" },
  },
  expectedComposeHash: composeLocationHash(c, s, a),
});

// === All-3 null → undefined/null compose ===
cases.push({
  name: "compose-all-null",
  kind: "compose",
  inputs: { coordinates: null, streetAddress: null, adminLocation: null },
  expectedComposeHash:
    composeLocationHash(undefined, undefined, undefined) ?? null,
});

// === Accuracy varies — same hash ===
const c2 = createCoordinates(40.7128, -74.006, 50).data;
cases.push({
  name: "coords-accuracy-50m",
  kind: "coordinates",
  factory: "create",
  inputs: { latitude: 40.7128, longitude: -74.006, accuracyMeters: 50 },
  expectedHashId: c2.hashId,
});
cases.push({
  name: "coords-accuracy-999m",
  kind: "coordinates",
  factory: "create",
  inputs: { latitude: 40.7128, longitude: -74.006, accuracyMeters: 999 },
  expectedHashId: createCoordinates(40.7128, -74.006, 999).data.hashId,
});

// === StreetAddress whitespace / punctuation / casing variants → same hash ===
const variants = [
  "123 Main St.",
  "123 main st",
  "123  Main  St",
  "123 MAIN ST",
];
for (const v of variants) {
  cases.push({
    name: `street-variant-${v.replace(/[^a-zA-Z0-9]/g, "_")}`,
    kind: "street-address",
    inputs: { line1: v },
    expectedHashId: createStreetAddress(v).data.hashId,
  });
}

// === StreetAddress diacritics — Latin ===
for (const pair of [
  ["Café", "Cafe"],
  ["Zürich", "Zurich"],
]) {
  for (const v of pair) {
    cases.push({
      name: `street-diacritic-${v}`,
      kind: "street-address",
      inputs: { line1: v },
      expectedHashId: createStreetAddress(v).data.hashId,
    });
  }
}

// === StreetAddress non-Latin scripts ===
const nonLatin = [
  { name: "cyrillic-moscow", line1: "Москва" },
  { name: "cjk-tokyo", line1: "东京" },
  { name: "greek-athens", line1: "Αθήνα" },
  { name: "arabic-riyadh", line1: "الرياض" },
  { name: "devanagari-delhi", line1: "नई दिल्ली" },
];
for (const e of nonLatin) {
  const r = createStreetAddress(e.line1);
  cases.push({
    name: `street-${e.name}`,
    kind: "street-address",
    inputs: { line1: e.line1 },
    expectedHashId: r.data.hashId,
    expectedNormalizedForHash: normalizeForHash(e.line1),
  });
}

// === StreetAddress emoji + mixed script ===
const emojiR = createStreetAddress("💩 Address");
cases.push({
  name: "street-emoji-stripped",
  kind: "street-address",
  inputs: { line1: "💩 Address" },
  expectedHashId: emojiR.data.hashId,
  expectedNormalizedForHash: normalizeForHash("💩 Address"),
});

const mixedR = createStreetAddress("123 Москва-Centre");
cases.push({
  name: "street-mixed-cyrillic-hyphen",
  kind: "street-address",
  inputs: { line1: "123 Москва-Centre" },
  expectedHashId: mixedR.data.hashId,
  expectedNormalizedForHash: normalizeForHash("123 Москва-Centre"),
});

// === Country-only ===
cases.push({
  name: "admin-country-only-us",
  kind: "admin-location",
  inputs: { countryCode: "US" },
  expectedHashId: createAdminLocation(CountryCodeUS).data.hashId,
});

// === Subdivision-only auto-pops country ===
{
  const r = createAdminLocation(undefined, SubUSNY);
  cases.push({
    name: "admin-subdivision-only-autopop",
    kind: "admin-location",
    inputs: { subdivisionCode: "US-NY" },
    expectedHashId: r.data.hashId,
    expectedCountryCode: "US",
  });
}

// === Country + mismatched subdivision → ValidationFailed ===
cases.push({
  name: "admin-mismatch-fails",
  kind: "admin-location",
  inputs: { countryCode: "CA", subdivisionCode: "US-NY" },
  expectedOutcome: "ValidationFailed",
});

// === Coordinates from 3 input forms representing the same canonical cell ===
{
  const fromLatLon = createCoordinates(40.7128, -74.006).data;
  cases.push({
    name: "coords-tri-form-latlon",
    kind: "coordinates",
    factory: "create",
    inputs: { latitude: 40.7128, longitude: -74.006 },
    expectedHashId: fromLatLon.hashId,
  });
  cases.push({
    name: "coords-tri-form-geohash",
    kind: "coordinates",
    factory: "fromGeohash",
    inputs: { geohash: fromLatLon.geohash },
    expectedHashId: fromLatLon.hashId,
  });
  // Plus-code may not produce byte-identical cell at all decimal degrees due to encoder
  // rounding asymmetry; pin the actual output rather than asserting equality.
  const fromPC = coordinatesFromPlusCode(fromLatLon.plusCode).data;
  cases.push({
    name: "coords-tri-form-pluscode",
    kind: "coordinates",
    factory: "fromPlusCode",
    inputs: { plusCode: fromLatLon.plusCode },
    expectedHashId: fromPC.hashId,
  });
}

// === Coordinate edge cases — pole, dateline ===
const polLat = createCoordinates(89.9999, 0.0).data;
cases.push({
  name: "coords-near-north-pole",
  kind: "coordinates",
  factory: "create",
  inputs: { latitude: 89.9999, longitude: 0.0 },
  expectedHashId: polLat.hashId,
});

const dateline = createCoordinates(0.0, 179.9999).data;
cases.push({
  name: "coords-near-dateline-east",
  kind: "coordinates",
  factory: "create",
  inputs: { latitude: 0.0, longitude: 179.9999 },
  expectedHashId: dateline.hashId,
});

// === Postal validator — Ok ===
cases.push({
  name: "postal-valid-us-zip5",
  kind: "postal-code",
  inputs: { postalCode: "90210" },
  expectedOutcome: "Ok",
  expectedNormalized: "90210",
});

// === Postal validator — Fail ===
cases.push({
  name: "postal-invalid-too-short",
  kind: "postal-code",
  inputs: { postalCode: "AB" },
  expectedOutcome: "ValidationFailed",
});

const out = {
  $comment:
    "Cross-language parity fixture for D2.Shared.Location / @d2/location. Generated by emit-fixture.mjs from the TS implementation; the .NET parity test asserts byte-identical hash output for every row. Regenerate when adding new cases.",
  version: "1.0",
  cases,
};

console.log(JSON.stringify(out, null, 2));
