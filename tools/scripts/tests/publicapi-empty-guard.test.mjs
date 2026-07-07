// Copyright (c) DCSV. All rights reserved.
//
// Regression test for the PublicAPI-seeder fail-loud guard. Runnable with the
// built-in node test runner (zero config, portable):
//   node --test tools/scripts/tests/
//
// The guard is the high-signal defect fix: a prior-non-empty package extracting
// to an empty surface is the "analyzer did not re-run" signature that silently
// wiped 30 baselines under a green gate. The guard MUST throw in that case and
// MUST NOT throw for a genuinely-new package or an explicit opt-in removal.

import assert from "node:assert/strict";
import { test } from "node:test";

import { assertExtractionNotWrongfullyEmpty } from "../lib/publicapi-empty-guard.mjs";

test("THROWS when a prior non-empty surface extracts to empty", () => {
  assert.throws(
    () =>
      assertExtractionNotWrongfullyEmpty({
        packageId: "D2.Shared.Time",
        priorSurfaceCount: 42,
        extractedSurfaceCount: 0,
        allowEmpty: false,
      }),
    /refusing to write empty PublicAPI for D2\.Shared\.Time: had 42 lines at HEAD/,
  );
});

test("does NOT throw when --allow-empty opts the package in", () => {
  assert.doesNotThrow(() =>
    assertExtractionNotWrongfullyEmpty({
      packageId: "D2.Shared.Time",
      priorSurfaceCount: 42,
      extractedSurfaceCount: 0,
      allowEmpty: true,
    }),
  );
});

test("does NOT throw for a genuinely-new package (no committed surface)", () => {
  assert.doesNotThrow(() =>
    assertExtractionNotWrongfullyEmpty({
      packageId: "D2.Shared.BrandNew",
      priorSurfaceCount: 0,
      extractedSurfaceCount: 0,
      allowEmpty: false,
    }),
  );
});

test("does NOT throw for a normal non-empty extraction", () => {
  assert.doesNotThrow(() =>
    assertExtractionNotWrongfullyEmpty({
      packageId: "D2.Shared.Time",
      priorSurfaceCount: 42,
      extractedSurfaceCount: 42,
      allowEmpty: false,
    }),
  );
});
