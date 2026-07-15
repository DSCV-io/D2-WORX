// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  CountryCode,
  CurrencyAcceptanceLevel,
  CurrencyCode,
  DateFormatPattern,
  DayOfWeek,
  GeopoliticalEntityCode,
  GeopoliticalEntityType,
  LanguageCode,
  MeasurementSystem,
  WritingDirection,
} from "@dcsv-io/d2-geo-abstractions";

import { canonicalize, loadFixture } from "../src/index.js";

/**
 * Each enum in the fixture is one of two shapes:
 *  - `{ memberName: memberName }` — string-wire enums (every value is the
 *    same string as its key). `JsonStringEnumConverter`-serialized on the
 *    .NET side; string-valued const-objects on the TS side.
 *  - `{ memberName: <integer> }` — integer-wire enums (the const-object's
 *    value is the integer backing the .NET enum).
 *
 * Fixture keys are kept on the bare singular names (`Country`,
 * `Currency`, `Language`, `GeopoliticalEntity`) per the .NET
 * `GeoEnumsFixtureEmitter` (which preserves the bare wire-form identity
 * for cross-language stability). The TS const-objects carry the `*Code`
 * suffix to denote closed-set enum types.
 */
type EnumFixtureEntry = Readonly<Record<string, string | number>>;
interface EnumsFixture {
  readonly [enumName: string]: EnumFixtureEntry;
}

/** Map fixture-key (the .NET enum type name) → the corresponding TS export. */
const tsEnumCatalog: Readonly<
  Record<string, Readonly<Record<string, string | number>>>
> = {
  Country: CountryCode as Readonly<Record<string, string | number>>,
  Currency: CurrencyCode as Readonly<Record<string, string | number>>,
  Language: LanguageCode as Readonly<Record<string, string | number>>,
  GeopoliticalEntity: GeopoliticalEntityCode as Readonly<
    Record<string, string | number>
  >,
  GeopoliticalEntityType: GeopoliticalEntityType as Readonly<
    Record<string, string | number>
  >,
  WritingDirection: WritingDirection as Readonly<
    Record<string, string | number>
  >,
  DateFormatPattern: DateFormatPattern as Readonly<
    Record<string, string | number>
  >,
  CurrencyAcceptanceLevel: CurrencyAcceptanceLevel as Readonly<
    Record<string, string | number>
  >,
  MeasurementSystem: MeasurementSystem as Readonly<
    Record<string, string | number>
  >,
  // .NET's GeoDayOfWeek (named with the Geo prefix to avoid colliding with the
  // BCL System.DayOfWeek) maps to TS's `DayOfWeek` (no Geo prefix on the TS
  // side since there's no namespace collision in TS-land).
  GeoDayOfWeek: DayOfWeek as Readonly<Record<string, string | number>>,
};

describe("geo enums parity (.NET catalog ↔ TS catalog)", () => {
  const fixture = loadFixture<EnumsFixture>("geo", "enums");
  const fixtureData = fixture.data;
  const fixtureEnumNames = Object.keys(fixtureData).sort();

  it("fixture covers every TS-exported geo enum (no missing fixture entries)", () => {
    const tsEnumNames = Object.keys(tsEnumCatalog).sort();
    expect(fixtureEnumNames).toEqual(tsEnumNames);
  });

  for (const enumName of fixtureEnumNames) {
    describe(`enum ${enumName}`, () => {
      const fixtureEntry = fixtureData[enumName]!;
      const fixtureKeys = Object.keys(fixtureEntry).sort();
      const tsExport = tsEnumCatalog[enumName]!;

      // The TS const-object form embeds both the keys and the values. Strip
      // out any reverse-lookup numeric entries some const-objects can carry —
      // we only compare the member-name keys.
      const tsKeys = Object.keys(tsExport)
        .filter((k) => Number.isNaN(Number(k))) // skip numeric reverse-lookup keys
        .sort();

      it("has identical memberName membership", () => {
        expect(tsKeys).toEqual(fixtureKeys);
      });

      // Per-VALUE pin: every fixture entry asserted individually so a
      // drift names the specific drifted member.
      for (const memberName of fixtureKeys) {
        it(`member ${memberName} has identical wire value`, () => {
          const fixtureValue = fixtureEntry[memberName];
          const tsValue = tsExport[memberName];
          expect(tsValue).toBe(fixtureValue);
        });
      }

      it("canonical maps are byte-equal", () => {
        const tsAsMap: Record<string, string | number> = {};
        for (const k of tsKeys) {
          const v = tsExport[k];
          if (v !== undefined) tsAsMap[k] = v;
        }

        expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureEntry));
      });
    });
  }
});
