// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  D2ResultEnvelopeFieldNames,
  ALL_D2RESULT_ENVELOPE_FIELD_NAMES,
} from "@d2/result";

import { canonicalize, loadFixture } from "../src/index.js";

interface ConstMap {
  readonly [constName: string]: string;
}

describe("d2result-envelope parity (.NET ↔ TS, Shape B envelope field names)", () => {
  // Catalog parity: every UPPER_SNAKE_CASE constName in the .NET-side
  // catalog must exist in the TS-side catalog with byte-equal wire value.
  describe("D2ResultEnvelopeFieldNames ↔ field-names.json", () => {
    const fixture = loadFixture<ConstMap>("d2result-envelope", "field-names");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsCatalog = D2ResultEnvelopeFieldNames as unknown as ConstMap;
    const tsKeys = Object.keys(tsCatalog).sort();

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

    it("ALL_D2RESULT_ENVELOPE_FIELD_NAMES contains exactly the wire values", () => {
      const fixtureValues = Object.values(fixtureMap).sort();
      const tsValues = [...ALL_D2RESULT_ENVELOPE_FIELD_NAMES].sort();
      expect(tsValues).toEqual(fixtureValues);
    });
  });

  // Round-trip fixtures: .NET-side D2ResultEnvelopeFixtureEmitter
  // serializes real D2Result instances via System.Text.Json + the
  // production [JsonPropertyName] attributes; we parse the captured
  // JSON and pin the envelope shape. Drift on a property name → drift
  // on the fixture → fail here with a clear catalog-pinned message.
  describe("round-trip parity (.NET-serialized fixtures ↔ TS envelope shape)", () => {
    /**
     * Pin the envelope property names present on a round-trip fixture
     * against the catalog — the property set MUST be a subset of the
     * 7 envelope field names (extra wire fields = leakage; missing
     * required fields = drift). Optional fields (data, errorCode,
     * traceId) may be omitted; required-by-shape fields (success,
     * messages, inputErrors, statusCode) MUST be present.
     */
    function assertEnvelopeShape(
      body: Record<string, unknown>,
      requiredKeys: readonly string[],
    ): void {
      const allowed = new Set(ALL_D2RESULT_ENVELOPE_FIELD_NAMES);
      for (const key of Object.keys(body))
        expect(allowed.has(key), `wire field '${key}' is not in catalog`).toBe(
          true,
        );
      for (const k of requiredKeys)
        expect(Object.keys(body), `missing required field '${k}'`).toContain(k);
    }

    const ALWAYS_PRESENT: readonly string[] = [
      D2ResultEnvelopeFieldNames.SUCCESS,
      D2ResultEnvelopeFieldNames.MESSAGES,
      D2ResultEnvelopeFieldNames.INPUT_ERRORS,
      D2ResultEnvelopeFieldNames.STATUS_CODE,
    ];

    it("round-trip-ok: success=true, status=200, no error code / data / trace", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "d2result-envelope",
        "round-trip-ok",
      );
      assertEnvelopeShape(fixture.data, ALWAYS_PRESENT);
      expect(fixture.data[D2ResultEnvelopeFieldNames.SUCCESS]).toBe(true);
      expect(fixture.data[D2ResultEnvelopeFieldNames.STATUS_CODE]).toBe(200);
      expect(fixture.data[D2ResultEnvelopeFieldNames.MESSAGES]).toEqual([]);
      expect(fixture.data[D2ResultEnvelopeFieldNames.INPUT_ERRORS]).toEqual([]);
      // errorCode / traceId are present-as-null per System.Text.Json default
      // behavior for nullable read-only auto-properties (no null-omit
      // configured globally). data is absent because non-generic D2Result
      // doesn't carry a Data property at all.
      expect(fixture.data[D2ResultEnvelopeFieldNames.ERROR_CODE]).toBeNull();
      expect(fixture.data[D2ResultEnvelopeFieldNames.TRACE_ID]).toBeNull();
    });

    it("round-trip-ok-with-data: success=true with typed payload", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "d2result-envelope",
        "round-trip-ok-with-data",
      );
      assertEnvelopeShape(fixture.data, [
        ...ALWAYS_PRESENT,
        D2ResultEnvelopeFieldNames.DATA,
      ]);
      expect(fixture.data[D2ResultEnvelopeFieldNames.SUCCESS]).toBe(true);
      expect(fixture.data[D2ResultEnvelopeFieldNames.DATA]).toEqual({
        Id: "x",
        Name: "fixture",
      });
    });

    it("round-trip-not-found: success=false, status=404, errorCode=NOT_FOUND", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "d2result-envelope",
        "round-trip-not-found",
      );
      assertEnvelopeShape(fixture.data, ALWAYS_PRESENT);
      expect(fixture.data[D2ResultEnvelopeFieldNames.SUCCESS]).toBe(false);
      expect(fixture.data[D2ResultEnvelopeFieldNames.STATUS_CODE]).toBe(404);
      expect(fixture.data[D2ResultEnvelopeFieldNames.ERROR_CODE]).toBe(
        "NOT_FOUND",
      );
      // messages carries one TKMessage object (key: "common_errors_NOT_FOUND")
      // per contracts/tk-message spec — not a string. Pinning the object
      // shape here keeps the wire contract enforceable by CI.
      expect(fixture.data[D2ResultEnvelopeFieldNames.MESSAGES]).toEqual([
        { key: "common_errors_NOT_FOUND" },
      ]);
      // The NotFound factory stamps ErrorCategory.NotFound; it rides the wire
      // as the snake string via ErrorCategoryJsonConverter.
      expect(fixture.data[D2ResultEnvelopeFieldNames.CATEGORY]).toBe(
        "not_found",
      );
    });

    it("round-trip-with-category: NotFound carries category=not_found", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "d2result-envelope",
        "round-trip-with-category",
      );
      assertEnvelopeShape(fixture.data, [
        ...ALWAYS_PRESENT,
        D2ResultEnvelopeFieldNames.CATEGORY,
      ]);
      expect(fixture.data[D2ResultEnvelopeFieldNames.CATEGORY]).toBe(
        "not_found",
      );
    });

    it("round-trip-ok: category is omitted (success carries no category)", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "d2result-envelope",
        "round-trip-ok",
      );
      // [JsonIgnore(WhenWritingNull)] on Category → the key is ABSENT (not
      // null / not "") when there is no category.
      expect(
        Object.keys(fixture.data),
        "success result must omit the category key",
      ).not.toContain(D2ResultEnvelopeFieldNames.CATEGORY);
    });

    it("round-trip-validation-failed: VALIDATION_FAILED + inputErrors carries InputError[]", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "d2result-envelope",
        "round-trip-validation-failed",
      );
      assertEnvelopeShape(fixture.data, ALWAYS_PRESENT);
      expect(fixture.data[D2ResultEnvelopeFieldNames.SUCCESS]).toBe(false);
      expect(fixture.data[D2ResultEnvelopeFieldNames.STATUS_CODE]).toBe(400);
      expect(fixture.data[D2ResultEnvelopeFieldNames.ERROR_CODE]).toBe(
        "VALIDATION_FAILED",
      );
      // inputErrors is the per-field InputError[] shape per
      // contracts/input-error — pinning {field, errors: TKMessage[]} here
      // closes the cross-language wire loop for the FORM-validation surface.
      expect(fixture.data[D2ResultEnvelopeFieldNames.INPUT_ERRORS]).toEqual([
        {
          field: "email",
          errors: [{ key: "common_validation_EMAIL_INVALID" }],
        },
      ]);
      // ValidationFailed stamps ErrorCategory.ValidationFailure.
      expect(fixture.data[D2ResultEnvelopeFieldNames.CATEGORY]).toBe(
        "validation_failure",
      );
    });

    it("round-trip-with-trace-id: traceId carries the W3C lower-hex 32-char id", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "d2result-envelope",
        "round-trip-with-trace-id",
      );
      assertEnvelopeShape(fixture.data, ALWAYS_PRESENT);
      expect(fixture.data[D2ResultEnvelopeFieldNames.TRACE_ID]).toBe(
        "0123456789abcdef0123456789abcdef",
      );
      expect(fixture.data[D2ResultEnvelopeFieldNames.STATUS_CODE]).toBe(500);
      expect(fixture.data[D2ResultEnvelopeFieldNames.ERROR_CODE]).toBe(
        "UNHANDLED_EXCEPTION",
      );
    });

    it("wire-shape property names come from D2ResultEnvelopeFieldNames catalog", () => {
      // Catalog-pin guard — load every round-trip fixture, scan its
      // top-level property names, assert each one is a member of the
      // codegen-emitted catalog. ANY new field name on the wire
      // (e.g. PascalCase regression, hand-rolled leak) fails here with
      // a clear name-on-name diff.
      const scenarios = [
        "round-trip-ok",
        "round-trip-ok-with-data",
        "round-trip-not-found",
        "round-trip-validation-failed",
        "round-trip-with-trace-id",
        "round-trip-with-category",
      ] as const;
      const allowed = new Set(ALL_D2RESULT_ENVELOPE_FIELD_NAMES);
      for (const scenario of scenarios) {
        const fixture = loadFixture<Record<string, unknown>>(
          "d2result-envelope",
          scenario,
        );
        for (const key of Object.keys(fixture.data)) {
          expect(
            allowed.has(key),
            `${scenario}: wire field '${key}' is not in D2ResultEnvelopeFieldNames`,
          ).toBe(true);
        }
      }
    });
  });
});
