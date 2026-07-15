// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
// Regression test for the api-extractor-seeder fail-loud guard. Runnable with
// the built-in node test runner (zero config, portable):
//   node --test tools/scripts/tests/apiextractor-empty-guard.test.mjs
//
// The guard is the npm twin of the .NET seeder's empty-guard: a degenerate
// .api.md with NO `export ` line is the "api-extractor saw an empty/missing
// dist/index.d.ts" signature that would otherwise let a fingerprint be composed
// over degenerate content and pass the currency gate. The guard MUST throw in
// that case and MUST NOT throw for a report with exports or an explicit opt-in.

import assert from "node:assert/strict";
import { test } from "node:test";

import { assertApiReportNotDegenerate } from "../lib/apiextractor-empty-guard.mjs";

test("THROWS when a report has NO exports and is not allow-listed", () => {
  assert.throws(
    () =>
      assertApiReportNotDegenerate({
        pkgName: "@d2/result",
        hasPublicMembers: false,
        allowEmpty: false,
      }),
    /refusing to write a fingerprint over a degenerate \.api\.md for @d2\/result/,
  );
});

test("does NOT throw when --allow-empty opts the package in", () => {
  assert.doesNotThrow(() =>
    assertApiReportNotDegenerate({
      pkgName: "@d2/result",
      hasPublicMembers: false,
      allowEmpty: true,
    }),
  );
});

test("does NOT throw for a report WITH exports", () => {
  assert.doesNotThrow(() =>
    assertApiReportNotDegenerate({
      pkgName: "@d2/result",
      hasPublicMembers: true,
      allowEmpty: false,
    }),
  );
});

test("the throw message names the escape hatch (both flag + env var)", () => {
  assert.throws(
    () =>
      assertApiReportNotDegenerate({
        pkgName: "@d2/telemetry",
        hasPublicMembers: false,
        allowEmpty: false,
      }),
    /--allow-empty @d2\/telemetry.*SEED_ALLOW_EMPTY=@d2\/telemetry/s,
  );
});
