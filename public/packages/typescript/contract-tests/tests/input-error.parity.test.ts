// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  InputErrorWireShape,
  inputError,
  type InputError,
} from "@dcsv-io/d2-result";

import { canonicalize, loadFixture } from "../src/index.js";

interface ConstMap {
  readonly [constName: string]: string;
}

describe("input-error parity (.NET wire shape ↔ TS wire shape)", () => {
  describe("InputErrorWireShape ↔ input-error/shape.json", () => {
    const fixture = loadFixture<ConstMap>("input-error", "shape");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsKeys = Object.keys(InputErrorWireShape).sort();
    const tsCatalog = InputErrorWireShape as unknown as ConstMap;

    it("has identical constName membership", () => {
      expect(tsKeys).toEqual(fixtureKeys);
    });

    // Per-VALUE pin: every fixture entry asserted individually so a
    // failure message names the specific drifted property.
    for (const constName of fixtureKeys) {
      it(`constant ${constName} has identical wire value`, () => {
        const fixtureValue = fixtureMap[constName];
        const tsValue = tsCatalog[constName];
        expect(tsValue).toBe(fixtureValue);
      });
    }

    it("canonical maps are byte-equal", () => {
      const tsAsMap: Record<string, string> = {};
      for (const k of tsKeys) tsAsMap[k] = tsCatalog[k]!;
      expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureMap));
    });
  });

  // Round-trip fixtures: the .NET-side InputErrorFixtureEmitter serializes
  // real InputError records (with [JsonPropertyName] attributes referencing
  // the codegen-emitted InputErrorWireShape constants) and writes the
  // canonical JSON shape to the fixtures dir. ANY drift on FIELD / ERRORS /
  // nested TKMessage shape fails here.
  describe("round-trip parity (.NET-serialized fixtures ↔ TS interface)", () => {
    it("single-error: .NET wire shape parses to {field, errors: [{key}]}", () => {
      const fixture = loadFixture<InputError>(
        "input-error",
        "round-trip-single-error",
      );
      expect(fixture.data).toEqual({
        field: "email",
        errors: [{ key: "common_validation_EMAIL_INVALID" }],
      });
      // Symmetric: inputError() helper produces the same byte shape.
      expect(
        inputError("email", [{ key: "common_validation_EMAIL_INVALID" }]),
      ).toEqual(fixture.data);
    });

    it("multiple-errors: .NET wire shape preserves errors array order", () => {
      const fixture = loadFixture<InputError>(
        "input-error",
        "round-trip-multiple-errors",
      );
      expect(fixture.data).toEqual({
        field: "password",
        errors: [
          { key: "common_validation_PASSWORD_REQUIRED" },
          {
            key: "auth_validation_PASSWORD_TOO_SHORT",
            params: { minLength: "12" },
          },
        ],
      });
    });

    it("dot-notation-field: .NET wire shape preserves nested field paths", () => {
      const fixture = loadFixture<InputError>(
        "input-error",
        "round-trip-dot-notation-field",
      );
      expect(fixture.data).toEqual({
        field: "address.city",
        errors: [{ key: "common_validation_FIELD_REQUIRED" }],
      });
    });

    it("wire-shape property names come from the InputErrorWireShape catalog", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "input-error",
        "round-trip-single-error",
      );
      const propertyNames = Object.keys(fixture.data).sort();
      // The fixture's data object MUST use the catalog property names —
      // if .NET emits "Field" instead of "field" (PascalCase regression),
      // this assertion fails with a clear message naming the drift.
      expect(propertyNames).toContain(InputErrorWireShape.FIELD);
      expect(propertyNames).toContain(InputErrorWireShape.ERRORS);
    });

    // Cross-shape integration: an InputError carrying a TKMessage with
    // params exercises BOTH wire-shape catalogs nested together — this
    // is the "real" wire path a gateway would emit on a form failure.
    it("nested-TKMessage-shape: input-error errors[] uses TkMessage wire shape", () => {
      const fixture = loadFixture<InputError>(
        "input-error",
        "round-trip-multiple-errors",
      );
      const secondError = fixture.data.errors[1]!;
      // The nested TKMessage MUST also use the tk-message wire shape
      // (key / params) — proves the wire-shape composition holds across
      // the catalog boundary.
      expect(Object.keys(secondError).sort()).toEqual(["key", "params"]);
    });
  });
});
