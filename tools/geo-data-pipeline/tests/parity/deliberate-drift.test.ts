// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

/**
 * Deliberate-drift validation: proves the cross-catalog FK integrity check actually
 * detects violations rather than passing vacuously.
 *
 * Strategy: load the shipped timezones + countries specs, deliberately corrupt the
 * timezone catalog by introducing a phantom country FK that does NOT exist in the
 * countries catalog, then assert the FK-integrity check FAILS with the expected
 * violation.
 *
 * Pure in-memory; does not write any files.
 */

function locateGeoDir(): string {
  let dir = dirname(fileURLToPath(import.meta.url));
  for (let i = 0; i < 12; i++) {
    const candidate = join(dir, "contracts", "geo");
    if (existsSync(candidate)) return candidate;
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error(
    "could not locate contracts/geo from " +
      dirname(fileURLToPath(import.meta.url)),
  );
}

const GEO_DIR = locateGeoDir();

interface SpecFile<T> {
  entries: T[];
}

interface Country {
  iso31661Alpha2Code: string;
}

interface Timezone {
  ianaIdentifier: string;
  countryISO31661Alpha2Code: string | null;
}

async function readSpec<T>(name: string): Promise<SpecFile<T>> {
  const path = resolve(GEO_DIR, name);
  return JSON.parse(await readFile(path, "utf8")) as SpecFile<T>;
}

/** Mirrors the FK-integrity logic in tier-2-output.test.ts. Returns the list of orphan FKs. */
function findOrphanTimezoneCountryRefs(
  countries: readonly Country[],
  timezones: readonly Timezone[],
): string[] {
  const validCountryCodes = new Set(countries.map((c) => c.iso31661Alpha2Code));
  const orphans: string[] = [];
  for (const tz of timezones) {
    if (
      tz.countryISO31661Alpha2Code &&
      !validCountryCodes.has(tz.countryISO31661Alpha2Code)
    ) {
      orphans.push(`${tz.ianaIdentifier} -> ${tz.countryISO31661Alpha2Code}`);
    }
  }
  return orphans;
}

describe("Deliberate-drift validation (parity-test infrastructure proof)", () => {
  it("FK-integrity check passes for the SHIPPED catalogs (sanity baseline)", async () => {
    const countriesSpec = await readSpec<Country>("countries.spec.json");
    const timezonesSpec = await readSpec<Timezone>("timezones.spec.json");

    const orphans = findOrphanTimezoneCountryRefs(
      countriesSpec.entries,
      timezonesSpec.entries,
    );
    // We expect zero orphans in the shipped data (the parity tests guard this).
    expect(orphans).toEqual([]);
  });

  it("FK-integrity FAILS when a corrupted timezone references a non-existent country", async () => {
    const countriesSpec = await readSpec<Country>("countries.spec.json");
    const timezonesSpec = await readSpec<Timezone>("timezones.spec.json");

    // Deliberately corrupt: inject a timezone with a phantom country code.
    // "ZZ" is the canonical ISO 3166-1 reserved "unknown / invalid" code and is
    // guaranteed NOT to appear in the real countries catalog.
    const corruptedTimezones: Timezone[] = [
      ...timezonesSpec.entries,
      {
        ianaIdentifier: "Test/DeliberateDrift",
        countryISO31661Alpha2Code: "ZZ",
      },
    ];

    const orphans = findOrphanTimezoneCountryRefs(
      countriesSpec.entries,
      corruptedTimezones,
    );

    // The drift MUST be detected.
    expect(orphans).toHaveLength(1);
    expect(orphans[0]).toBe("Test/DeliberateDrift -> ZZ");
  });

  it("FK-integrity check detects multiple corruptions independently", async () => {
    const countriesSpec = await readSpec<Country>("countries.spec.json");

    // Build a tiny synthetic timezone catalog with two phantom FKs.
    const corruptedTimezones: Timezone[] = [
      { ianaIdentifier: "Test/Phantom1", countryISO31661Alpha2Code: "ZZ" },
      { ianaIdentifier: "Test/Phantom2", countryISO31661Alpha2Code: "QQ" },
      // and one legitimate one (US is always in the catalog)
      { ianaIdentifier: "Test/Legit", countryISO31661Alpha2Code: "US" },
    ];

    const orphans = findOrphanTimezoneCountryRefs(
      countriesSpec.entries,
      corruptedTimezones,
    );

    expect(orphans).toHaveLength(2);
    expect(orphans).toContain("Test/Phantom1 -> ZZ");
    expect(orphans).toContain("Test/Phantom2 -> QQ");
    expect(orphans).not.toContain("Test/Legit -> US");
  });
});
