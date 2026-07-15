// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { readAttemptCount } from "../src/subscribing/attempt-count.js";

describe("readAttemptCount — mirrors SubscriberChannel.ReadAttemptCount", () => {
  it("returns 0 for absent headers / missing x-death / non-array x-death", () => {
    expect(readAttemptCount(undefined)).toBe(0);
    expect(readAttemptCount({})).toBe(0);
    expect(readAttemptCount({ "x-death": "not-an-array" })).toBe(0);
  });

  it("sums count across expired + rejected reasons only", () => {
    const headers = {
      "x-death": [
        { reason: "expired", count: 2 },
        { reason: "rejected", count: 1 },
        { reason: "maxlen", count: 9 }, // ignored — broker flow control
        { reason: "delivery_limit", count: 7 }, // ignored
      ],
    };
    expect(readAttemptCount(headers)).toBe(3);
  });

  it("handles a bigint count (AMQP long)", () => {
    expect(
      readAttemptCount({ "x-death": [{ reason: "expired", count: 4n }] }),
    ).toBe(4);
  });

  it("skips malformed entries (non-object, missing reason/count, wrong types)", () => {
    const headers = {
      "x-death": [
        null,
        "string",
        { count: 5 }, // no reason
        { reason: 123, count: 5 }, // non-string reason
        { reason: "expired" }, // no count
        { reason: "expired", count: "3" }, // non-number count → 0
        { reason: "expired", count: 6 }, // the only counted entry
      ],
    };
    expect(readAttemptCount(headers)).toBe(6);
  });
});
