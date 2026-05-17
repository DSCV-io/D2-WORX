// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { TkMessageWireShape, tk, type TKMessage } from "@d2/result";

import { canonicalize, loadFixture } from "../src/index.js";

interface ConstMap {
  readonly [constName: string]: string;
}

describe("tk-message parity (.NET wire shape ↔ TS wire shape)", () => {
  describe("TkMessageWireShape ↔ tk-message/shape.json", () => {
    const fixture = loadFixture<ConstMap>("tk-message", "shape");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsKeys = Object.keys(TkMessageWireShape).sort();
    const tsCatalog = TkMessageWireShape as unknown as ConstMap;

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

  // Round-trip fixtures: the .NET-side TkMessageFixtureEmitter serializes
  // TKMessages via the production TKMessageJsonConverter and writes the
  // canonical JSON shape to the fixtures dir. Parity tests parse the
  // fixture, assert it matches what tk() / the TS TKMessage interface
  // produces — ANY drift on KEY / PARAMS / nested params shape fails here.
  describe("round-trip parity (.NET-serialized fixtures ↔ TS interface)", () => {
    it("no-params: .NET wire shape parses to {key: string}", () => {
      const fixture = loadFixture<TKMessage>("tk-message", "round-trip-no-params");
      expect(fixture.data).toEqual({ key: "common_errors_NOT_FOUND" });
      // Symmetric: tk() helper produces the same byte shape.
      expect(tk("common_errors_NOT_FOUND")).toEqual(fixture.data);
    });

    it("with-params: .NET wire shape parses to {key, params: Record<string,string>}", () => {
      const fixture = loadFixture<TKMessage>("tk-message", "round-trip-with-params");
      expect(fixture.data).toEqual({
        key: "common_errors_LIMIT_EXCEEDED",
        params: { maxLength: "256" },
      });
      // Symmetric tk() construction matches the fixture byte-for-byte.
      expect(
        tk("common_errors_LIMIT_EXCEEDED", { maxLength: "256" }),
      ).toEqual(fixture.data);
    });

    it("with-multiple-params: .NET wire shape preserves all bindings", () => {
      const fixture = loadFixture<TKMessage>(
        "tk-message",
        "round-trip-with-multiple-params",
      );
      expect(fixture.data).toEqual({
        key: "auth_errors_PASSWORD_WEAK",
        params: { minLength: "12", maxLength: "128" },
      });
    });

    it("wire-shape property names come from the TkMessageWireShape catalog", () => {
      const fixture = loadFixture<Record<string, unknown>>(
        "tk-message",
        "round-trip-with-params",
      );
      const propertyNames = Object.keys(fixture.data).sort();
      // The fixture's data object MUST use the catalog property names —
      // if .NET emits "Key" instead of "key" (PascalCase regression), this
      // assertion fails with a clear message naming the drift.
      expect(propertyNames).toContain(TkMessageWireShape.KEY);
      expect(propertyNames).toContain(TkMessageWireShape.PARAMS);
    });
  });
});
