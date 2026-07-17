// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";
import { CommonHeaders } from "@dcsv-io/d2-headers-common";
import { GrpcHeaders } from "@dcsv-io/d2-headers-grpc";
import { HttpHeaders } from "@dcsv-io/d2-headers-http";
import { canonicalize, loadFixture } from "../src/index.js";

interface HeaderMap {
  readonly [constName: string]: string;
}

describe("headers parity (.NET catalog ↔ TS catalog)", () => {
  const cases: ReadonlyArray<{
    scenario: string;
    tsCatalog: Readonly<Record<string, string>>;
    catalogName: string;
  }> = [
    {
      scenario: "common",
      tsCatalog: CommonHeaders,
      catalogName: "CommonHeaders",
    },
    { scenario: "http", tsCatalog: HttpHeaders, catalogName: "HttpHeaders" },
    { scenario: "amqp", tsCatalog: AmqpHeaders, catalogName: "AmqpHeaders" },
    { scenario: "grpc", tsCatalog: GrpcHeaders, catalogName: "GrpcHeaders" },
  ];

  for (const { scenario, tsCatalog, catalogName } of cases) {
    describe(`${catalogName} ↔ headers/${scenario}.json`, () => {
      const fixture = loadFixture<HeaderMap>("headers", scenario);
      const fixtureMap = fixture.data;
      const fixtureKeys = Object.keys(fixtureMap).sort();
      const tsKeys = Object.keys(tsCatalog).sort();

      it("has identical constName membership", () => {
        expect(tsKeys).toEqual(fixtureKeys);
      });

      // Per-VALUE pin: every fixture entry asserted individually so a
      // failure message names the specific drifted constant.
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
  }
});
