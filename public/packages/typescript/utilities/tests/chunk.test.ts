// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { chunk } from "../src/chunk.js";

describe("chunk", () => {
  it("splits evenly", () => {
    expect(chunk([1, 2, 3, 4], 2)).toEqual([
      [1, 2],
      [3, 4],
    ]);
  });

  it("handles uneven last chunk", () => {
    expect(chunk([1, 2, 3, 4, 5], 2)).toEqual([[1, 2], [3, 4], [5]]);
  });

  it("returns empty array for empty input", () => {
    expect(chunk([], 5)).toEqual([]);
  });

  it("returns one-per-chunk for size 1", () => {
    expect(chunk([1, 2, 3], 1)).toEqual([[1], [2], [3]]);
  });

  it("returns single chunk when size > length", () => {
    expect(chunk([1, 2, 3], 100)).toEqual([[1, 2, 3]]);
  });

  it.each([0, -1, 1.5, Number.NaN])("throws on invalid size %s", (size) => {
    expect(() => chunk([1, 2, 3], size)).toThrow(RangeError);
  });
});
