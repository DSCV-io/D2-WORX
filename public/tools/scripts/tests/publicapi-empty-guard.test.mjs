// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
//   node --test tools/scripts/tests/publicapi-empty-guard.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";

import {
  assertExtractionNotWrongfullyEmpty,
  assertShippedContentNotWrongfullyEmpty,
  countPublicApiLines,
  formatPublicApiFile,
  NULLABLE_HEADER,
} from "../lib/publicapi-empty-guard.mjs";

// --- extraction guard (seeder success path) ---

test("extraction: THROWS when HEAD non-empty extracts to 0 without allow", () => {
  assert.throws(
    () =>
      assertExtractionNotWrongfullyEmpty({
        packageId: "DcsvIo.D2.Time",
        priorSurfaceCount: 42,
        extractedSurfaceCount: 0,
        allowEmpty: false,
      }),
    /refusing to write empty PublicAPI for DcsvIo\.D2\.Time: had 42 lines at HEAD/,
  );
});

test("extraction: allow-empty permits intentional N→0 surface", () => {
  assert.doesNotThrow(() =>
    assertExtractionNotWrongfullyEmpty({
      packageId: "DcsvIo.D2.Time",
      priorSurfaceCount: 42,
      extractedSurfaceCount: 0,
      allowEmpty: true,
    }),
  );
});

test("extraction: new package (HEAD 0) may extract empty without allow", () => {
  assert.doesNotThrow(() =>
    assertExtractionNotWrongfullyEmpty({
      packageId: "DcsvIo.D2.BrandNew",
      priorSurfaceCount: 0,
      extractedSurfaceCount: 0,
      allowEmpty: false,
    }),
  );
});

test("extraction: normal non-empty extract OK", () => {
  assert.doesNotThrow(() =>
    assertExtractionNotWrongfullyEmpty({
      packageId: "DcsvIo.D2.Time",
      priorSurfaceCount: 42,
      extractedSurfaceCount: 40,
      allowEmpty: false,
    }),
  );
});

// --- on-disk / commit gate ---

test("disk: THROWS empty vs non-empty HEAD without allow (failure wipe)", () => {
  assert.throws(
    () =>
      assertShippedContentNotWrongfullyEmpty({
        packageId: "DcsvIo.D2.AspNetCore",
        shippedContent: `${NULLABLE_HEADER}\n`,
        headSurfaceCount: 147,
        allowEmpty: false,
      }),
    /PublicAPI\.Shipped\.txt for DcsvIo\.D2\.AspNetCore is EMPTY/,
  );
});

test("disk: missing file treated as empty", () => {
  assert.throws(
    () =>
      assertShippedContentNotWrongfullyEmpty({
        packageId: "DcsvIo.D2.AspNetCore",
        shippedContent: "",
        headSurfaceCount: 10,
        allowEmpty: false,
      }),
    /EMPTY/,
  );
});

test("disk: brand-new / HEAD-empty package may be header-only", () => {
  assert.doesNotThrow(() =>
    assertShippedContentNotWrongfullyEmpty({
      packageId: "DcsvIo.D2.BrandNew",
      shippedContent: `${NULLABLE_HEADER}\n`,
      headSurfaceCount: 0,
      allowEmpty: false,
    }),
  );
});

test("disk: intentional first empty commit needs allowEmpty", () => {
  assert.doesNotThrow(() =>
    assertShippedContentNotWrongfullyEmpty({
      packageId: "DcsvIo.D2.AspNetCore",
      shippedContent: formatPublicApiFile([]),
      headSurfaceCount: 147,
      allowEmpty: true,
    }),
  );
});

test("disk: partial reduction (still has lines) never trips empty guard", () => {
  assert.doesNotThrow(() =>
    assertShippedContentNotWrongfullyEmpty({
      packageId: "DcsvIo.D2.Time",
      shippedContent: formatPublicApiFile(["P:OnlyOneLeft"]),
      headSurfaceCount: 99,
      allowEmpty: false,
    }),
  );
});

test("countPublicApiLines ignores header and blanks only", () => {
  assert.equal(countPublicApiLines(`${NULLABLE_HEADER}\n`), 0);
  assert.equal(countPublicApiLines(formatPublicApiFile(["P:Foo", "P:Bar"])), 2);
});

test("remediation strings cite public/tools/scripts/seed-publicapi-baselines.mjs", () => {
  try {
    assertExtractionNotWrongfullyEmpty({
      packageId: "DcsvIo.D2.Time",
      priorSurfaceCount: 1,
      extractedSurfaceCount: 0,
      allowEmpty: false,
    });
    assert.fail("expected throw");
  } catch (e) {
    const msg = String(/** @type {Error} */ (e).message);
    assert.match(msg, /public\/tools\/scripts\/seed-publicapi-baselines\.mjs/);
    assert.doesNotMatch(msg, /(?<!public\/)tools\/scripts\/seed-publicapi/);
  }
});
