// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { D2GrpcTrailers } from "@dcsv-io/d2-grpc-client";
import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogMap {
  readonly [constName: string]: string;
}

describe("gRPC trailers parity (.NET D2GrpcTrailers ↔ TS D2GrpcTrailers)", () => {
  const fixture = loadFixture<CatalogMap>("grpc-trailers", "trailers");
  const fixtureMap = fixture.data;
  const fixtureKeys = Object.keys(fixtureMap).sort();
  const tsCatalog = D2GrpcTrailers as Readonly<Record<string, string>>;
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

  // Regression pin for the lowercase-to-camelCase casing fix.
  it("TRACE_ID maps to 'traceId' (camelCase, NOT lowercase 'traceid')", () => {
    expect(D2GrpcTrailers.TRACE_ID).toBe("traceId");
    expect(D2GrpcTrailers.TRACE_ID).not.toBe("traceid");
  });
});
