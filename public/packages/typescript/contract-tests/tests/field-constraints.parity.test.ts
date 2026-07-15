// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  BiologicalSex,
  FieldConstraints,
  NamePrefix,
  NameSuffix,
} from "@d2/validation-abstractions";
import { describe, expect, it } from "vitest";

import { canonicalize, loadFixture } from "../src/index.js";

/**
 * The field-constraints fixture carries two groups:
 *  - `constraints` — `{ NAME: <integer> }` over every `FieldConstraints` const.
 *  - `enums` — per-enum `{ memberName: memberName }` (string-wire enums; every
 *    value is the same string as its key). `JsonStringEnumConverter`-serialized
 *    on the .NET side; string-valued const-objects on the TS side.
 *
 * The .NET `FieldConstraintsFixtureEmitter` reflects both groups off the
 * `D2.Shared.Validation.Abstractions` assembly; this test asserts the TS
 * catalog matches per-VALUE so a drift names the specific drifted member.
 */
type ConstraintsFixture = Readonly<Record<string, number>>;
type EnumFixtureEntry = Readonly<Record<string, string>>;
interface FieldConstraintsFixture {
  readonly constraints: ConstraintsFixture;
  readonly enums: Readonly<Record<string, EnumFixtureEntry>>;
}

/** Map fixture enum-key (the .NET enum type name) → the corresponding TS export. */
const tsEnumCatalog: Readonly<
  Record<string, Readonly<Record<string, string>>>
> = {
  NamePrefix: NamePrefix as Readonly<Record<string, string>>,
  NameSuffix: NameSuffix as Readonly<Record<string, string>>,
  BiologicalSex: BiologicalSex as Readonly<Record<string, string>>,
};

describe("field-constraints parity (.NET catalog ↔ TS catalog)", () => {
  const fixture = loadFixture<FieldConstraintsFixture>(
    "validation",
    "field-constraints",
  );
  const fixtureData = fixture.data;

  describe("FieldConstraints", () => {
    const fixtureConstraints = fixtureData.constraints;
    const fixtureNames = Object.keys(fixtureConstraints).sort();
    const tsNames = Object.keys(FieldConstraints)
      .filter((k) => Number.isNaN(Number(k)))
      .sort();

    it("has identical constant-name membership", () => {
      expect(tsNames).toEqual(fixtureNames);
    });

    // Per-VALUE pin: every fixture entry asserted individually so a drift
    // names the specific drifted constant.
    for (const name of fixtureNames) {
      it(`constant ${name} has identical value`, () => {
        const fixtureValue = fixtureConstraints[name];
        const tsValue = (FieldConstraints as Readonly<Record<string, number>>)[
          name
        ];
        expect(tsValue).toBe(fixtureValue);
      });
    }

    it("canonical maps are byte-equal", () => {
      const tsAsMap: Record<string, number> = {};
      for (const k of tsNames) {
        const v = (FieldConstraints as Readonly<Record<string, number>>)[k];
        if (v !== undefined) tsAsMap[k] = v;
      }
      expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureConstraints));
    });
  });

  describe("taxonomy enums", () => {
    const fixtureEnums = fixtureData.enums;
    const fixtureEnumNames = Object.keys(fixtureEnums).sort();

    it("fixture covers every TS-exported taxonomy enum", () => {
      const tsEnumNames = Object.keys(tsEnumCatalog).sort();
      expect(fixtureEnumNames).toEqual(tsEnumNames);
    });

    for (const enumName of fixtureEnumNames) {
      describe(`enum ${enumName}`, () => {
        const fixtureEntry = fixtureEnums[enumName]!;
        const fixtureKeys = Object.keys(fixtureEntry).sort();
        const tsExport = tsEnumCatalog[enumName]!;
        const tsKeys = Object.keys(tsExport)
          .filter((k) => Number.isNaN(Number(k)))
          .sort();

        it("has identical memberName membership", () => {
          expect(tsKeys).toEqual(fixtureKeys);
        });

        // Per-VALUE pin: every fixture entry asserted individually.
        for (const memberName of fixtureKeys) {
          it(`member ${memberName} has identical wire value`, () => {
            const fixtureValue = fixtureEntry[memberName];
            const tsValue = tsExport[memberName];
            expect(tsValue).toBe(fixtureValue);
          });
        }

        it("canonical maps are byte-equal", () => {
          const tsAsMap: Record<string, string> = {};
          for (const k of tsKeys) {
            const v = tsExport[k];
            if (v !== undefined) tsAsMap[k] = v;
          }
          expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureEntry));
        });
      });
    }
  });
});
