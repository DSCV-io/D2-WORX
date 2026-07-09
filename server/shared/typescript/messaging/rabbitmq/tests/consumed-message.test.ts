// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { readHeaderString } from "../src/subscribing/consumed-message.js";

describe("readHeaderString", () => {
  it("returns a string header verbatim", () => {
    expect(readHeaderString({ traceparent: "00-abc" }, "traceparent")).toBe(
      "00-abc",
    );
  });

  it("decodes a byte-typed header (Uint8Array / Buffer) as UTF-8", () => {
    const bytes = new Uint8Array(Buffer.from("x-d2-context-value", "utf8"));
    expect(readHeaderString({ "x-d2-context": bytes }, "x-d2-context")).toBe(
      "x-d2-context-value",
    );
  });

  it("returns undefined for an absent or non-string/non-bytes header", () => {
    expect(readHeaderString({}, "traceparent")).toBeUndefined();
    expect(readHeaderString({ n: 42 }, "n")).toBeUndefined();
  });
});
