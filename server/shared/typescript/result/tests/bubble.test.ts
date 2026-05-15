// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { bubble, bubbleFail } from "../src/bubble.js";
import { fail, notFound, ok } from "../src/factories.js";

describe("bubbleFail", () => {
  it("transfers fail-shape into a fresh typed result", () => {
    const upstream = notFound({ traceId: "t" });
    const downstream = bubbleFail<{ id: string }>(upstream);
    expect(downstream.failed).toBe(true);
    expect(downstream.statusCode).toBe(upstream.statusCode);
    expect(downstream.errorCode).toBe(upstream.errorCode);
    expect(downstream.traceId).toBe("t");
    expect(downstream.data).toBeUndefined();
  });

  it("throws when source is success", () => {
    expect(() => bubbleFail(ok())).toThrow(RangeError);
  });

  it("preserves messages + inputErrors", () => {
    const upstream = fail({
      messages: [{ key: "TK.X" }],
      inputErrors: [{ field: "f", errors: [{ key: "TK.Y" }] }],
    });
    const r = bubbleFail<number>(upstream);
    expect(r.messages).toEqual([{ key: "TK.X" }]);
    expect(r.inputErrors).toHaveLength(1);
  });
});

describe("bubble", () => {
  it("passes through success and overrides data", () => {
    const upstream = ok<number>(42);
    const r = bubble<number, string>(upstream, "hello");
    expect(r.success).toBe(true);
    expect(r.data).toBe("hello");
  });

  it("passes through failure shape", () => {
    const upstream = notFound();
    const r = bubble<unknown, string>(upstream);
    expect(r.failed).toBe(true);
    expect(r.errorCode).toBe(upstream.errorCode);
    expect(r.data).toBeUndefined();
  });
});
