// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { D2Result } from "../src/d2-result.js";
import { HttpStatusCode } from "../src/http-status-codes.js";

describe("D2Result", () => {
  it("defaults statusCode to OK on success", () => {
    const r = new D2Result({ success: true });
    expect(r.success).toBe(true);
    expect(r.failed).toBe(false);
    expect(r.statusCode).toBe(HttpStatusCode.OK);
    expect(r.messages).toEqual([]);
    expect(r.inputErrors).toEqual([]);
    expect(r.errorCode).toBeUndefined();
    expect(r.traceId).toBeUndefined();
  });

  it("defaults statusCode to BadRequest on failure", () => {
    const r = new D2Result({ success: false });
    expect(r.failed).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("preserves explicit statusCode override", () => {
    const r = new D2Result({
      success: true,
      statusCode: HttpStatusCode.Created,
    });
    expect(r.statusCode).toBe(HttpStatusCode.Created);
  });

  it("isPartialSuccess true on HTTP 206", () => {
    const r = new D2Result({
      success: false,
      statusCode: HttpStatusCode.PartialContent,
    });
    expect(r.isPartialSuccess).toBe(true);
  });

  it("isPartialSuccess false on other statuses", () => {
    const r = new D2Result({ success: true });
    expect(r.isPartialSuccess).toBe(false);
  });

  it("withTraceId returns a new instance with traceId swapped", () => {
    const r = new D2Result({ success: true, traceId: "old" });
    const r2 = r.withTraceId("new");
    expect(r.traceId).toBe("old");
    expect(r2.traceId).toBe("new");
    expect(r2).not.toBe(r);
  });

  it("withTraceId can clear the traceId via undefined", () => {
    const r = new D2Result({ success: true, traceId: "old" });
    expect(r.withTraceId(undefined).traceId).toBeUndefined();
  });

  it("carries data through generic D2Result<T>", () => {
    const r = new D2Result<{ id: string }>({
      success: true,
      data: { id: "x" },
    });
    expect(r.data).toEqual({ id: "x" });
  });

  it("withTraceId preserves data + messages + inputErrors", () => {
    const r = new D2Result<number>({
      success: false,
      data: 42,
      messages: [{ key: "TK.X" }],
      inputErrors: [{ field: "f", errors: [{ key: "TK.Y" }] }],
      statusCode: HttpStatusCode.Conflict,
      errorCode: "MY_CODE",
      traceId: "old",
    });
    const r2 = r.withTraceId("new");
    expect(r2.data).toBe(42);
    expect(r2.messages).toEqual([{ key: "TK.X" }]);
    expect(r2.inputErrors).toEqual([{ field: "f", errors: [{ key: "TK.Y" }] }]);
    expect(r2.statusCode).toBe(HttpStatusCode.Conflict);
    expect(r2.errorCode).toBe("MY_CODE");
  });
});
