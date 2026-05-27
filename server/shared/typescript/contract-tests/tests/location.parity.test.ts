// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import {
  composeLocationHash,
  coordinatesFromGeohash,
  coordinatesFromPlusCode,
  createAdminLocation,
  createCoordinates,
  createStreetAddress,
  defaultPostalCodeValidator,
  normalizeForHash,
  type AdminLocation,
  type Coordinates,
  type StreetAddress,
} from "@d2/location";
import type { CountryCode } from "@d2/geo-abstractions";
import { asSubdivisionCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

/**
 * Cross-runtime parity test for D2.Shared.Location / @d2/location.
 *
 * Loads `contracts/location/parity-fixtures.json` and asserts each
 * fixture row produces the byte-identical hash output from the TS
 * implementation. The .NET side (`CrossLanguageLocationParityTests.cs`)
 * does the same — when both pass, cross-language hash parity is proven.
 *
 * DELIBERATE-DRIFT VALIDATION (§6.1):
 *   DD-1 — Change `encodeGeohash`'s default precision from 10 to 11 in
 *     server/shared/typescript/location/src/encoding/geohash-encoder.ts,
 *     rerun this test → expect EVERY coords-bearing case to fail.
 *     Revert to restore green.
 *   DD-2 — Remove the `.normalize("NFD")` step from `normalizeForHash`
 *     in src/value-objects/street-address.ts, rerun → expect every
 *     Latin-diacritic row (Café/Zürich) to diverge from the .NET-pinned
 *     fixture value. Revert to restore green.
 *   DD-3 — In compose-location-hash.ts, strip the inner `"v1."` prefix
 *     from each component's `hashId` before concatenation, rerun →
 *     expect every multi-component compose case to diverge. Revert.
 *
 * These are documentation comments — the manual procedure the
 * Implementer performed to prove the comparator catches divergence.
 */

interface FixtureCase {
  readonly name: string;
  readonly kind: string;
  readonly factory?: string;
  readonly inputs: Record<string, unknown>;
  readonly expectedHashId?: string;
  readonly expectedComposeHash?: string | null;
  readonly expectedOutcome?: string;
  readonly expectedNormalizedForHash?: string;
  readonly expectedCountryCode?: string;
  readonly expectedNormalized?: string;
}

interface Fixture {
  readonly version: string;
  readonly cases: readonly FixtureCase[];
}

function loadFixture(): Fixture {
  const here = dirname(fileURLToPath(import.meta.url));
  // From server/shared/typescript/contract-tests/tests/ up to repo root = 5 levels.
  const repoRoot = join(here, "..", "..", "..", "..", "..");
  const path = join(repoRoot, "contracts", "location", "parity-fixtures.json");
  return JSON.parse(readFileSync(path, "utf8")) as Fixture;
}

const fixture = loadFixture();

function getString(v: unknown): string | undefined {
  return typeof v === "string" ? v : undefined;
}

function getNumber(v: unknown): number | undefined {
  return typeof v === "number" ? v : undefined;
}

function buildCoordsFromInputs(inputs: Record<string, unknown>): Coordinates {
  const factory =
    typeof inputs.factory === "string" ? inputs.factory : "create";
  if (factory === "create") {
    const args = inputs.args as unknown[];
    const lat = args[0] as number;
    const lon = args[1] as number;
    return createCoordinates(lat, lon).data!;
  }
  throw new Error(`Unknown coords factory in nested compose input: ${factory}`);
}

describe("Cross-language Location parity (TS ↔ .NET via contracts/location/parity-fixtures.json)", () => {
  for (const c of fixture.cases) {
    it(`${c.name} (${c.kind})`, () => {
      switch (c.kind) {
        case "coordinates": {
          const factory = c.factory ?? "create";
          let r;
          if (factory === "create") {
            r = createCoordinates(
              c.inputs.latitude as number,
              c.inputs.longitude as number,
              getNumber(c.inputs.accuracyMeters),
            );
          } else if (factory === "fromGeohash") {
            r = coordinatesFromGeohash(c.inputs.geohash as string);
          } else if (factory === "fromPlusCode") {
            r = coordinatesFromPlusCode(c.inputs.plusCode as string);
          } else {
            throw new Error(`Unknown coordinates factory: ${factory}`);
          }
          if (c.expectedOutcome === "ValidationFailed") {
            expect(r.success).toBe(false);
            return;
          }
          expect(r.success).toBe(true);
          expect(r.data!.hashId).toBe(c.expectedHashId);
          break;
        }
        case "street-address": {
          const r = createStreetAddress(
            getString(c.inputs.line1),
            getString(c.inputs.line2),
            getString(c.inputs.line3),
            getString(c.inputs.line4),
            getString(c.inputs.line5),
          );
          if (c.expectedOutcome === "ValidationFailed") {
            expect(r.success).toBe(false);
            return;
          }
          expect(r.success).toBe(true);
          expect(r.data!.hashId).toBe(c.expectedHashId);
          if (c.expectedNormalizedForHash !== undefined) {
            expect(normalizeForHash(getString(c.inputs.line1))).toBe(
              c.expectedNormalizedForHash,
            );
          }
          break;
        }
        case "admin-location": {
          const country =
            typeof c.inputs.countryCode === "string"
              ? (c.inputs.countryCode as CountryCode)
              : undefined;
          const sub =
            typeof c.inputs.subdivisionCode === "string"
              ? asSubdivisionCode(c.inputs.subdivisionCode)
              : undefined;
          const r = createAdminLocation(
            country,
            sub,
            getString(c.inputs.city),
            getString(c.inputs.postalCode),
          );
          if (c.expectedOutcome === "ValidationFailed") {
            expect(r.success).toBe(false);
            return;
          }
          expect(r.success).toBe(true);
          expect(r.data!.hashId).toBe(c.expectedHashId);
          if (c.expectedCountryCode !== undefined) {
            expect(r.data!.countryIso31661Alpha2Code).toBe(c.expectedCountryCode);
          }
          break;
        }
        case "compose": {
          let coord: Coordinates | undefined;
          if (
            c.inputs.coordinates !== null &&
            typeof c.inputs.coordinates === "object"
          ) {
            coord = buildCoordsFromInputs(
              c.inputs.coordinates as Record<string, unknown>,
            );
          }
          let street: StreetAddress | undefined;
          if (
            c.inputs.streetAddress !== null &&
            typeof c.inputs.streetAddress === "object"
          ) {
            const s = c.inputs.streetAddress as Record<string, unknown>;
            street = createStreetAddress(getString(s.line1)).data!;
          }
          let admin: AdminLocation | undefined;
          if (
            c.inputs.adminLocation !== null &&
            typeof c.inputs.adminLocation === "object"
          ) {
            const a = c.inputs.adminLocation as Record<string, unknown>;
            const country =
              typeof a.countryCode === "string"
                ? (a.countryCode as CountryCode)
                : undefined;
            const sub =
              typeof a.subdivisionCode === "string"
                ? asSubdivisionCode(a.subdivisionCode)
                : undefined;
            admin = createAdminLocation(
              country,
              sub,
              getString(a.city),
              getString(a.postalCode),
            ).data!;
          }
          const composed = composeLocationHash(coord, street, admin);
          if (
            c.expectedComposeHash === null ||
            c.expectedComposeHash === undefined
          ) {
            expect(composed).toBeUndefined();
          } else {
            expect(composed).toBe(c.expectedComposeHash);
          }
          break;
        }
        case "postal-code": {
          const v = defaultPostalCodeValidator();
          const r = v.validate(getString(c.inputs.postalCode));
          if (c.expectedOutcome === "ValidationFailed") {
            expect(r.success).toBe(false);
            return;
          }
          expect(r.success).toBe(true);
          if (c.expectedNormalized !== undefined) {
            expect(r.data).toBe(c.expectedNormalized);
          }
          break;
        }
        default:
          throw new Error(`Unknown kind: ${c.kind}`);
      }
    });
  }
});
