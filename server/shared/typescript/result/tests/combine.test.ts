// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { combine, combineMany } from "../src/combine.js";
import { ErrorCodes } from "../src/error-codes.js";
import { fail, notFound, ok, validationFailed } from "../src/factories.js";
import { HttpStatusCode } from "../src/http-status-codes.js";

describe("combine() — fixed-arity tuple form", () => {
  it("all-success → tuple of payloads", () => {
    const r = combine(ok<number>(1), ok<string>("two"));
    expect(r.success).toBe(true);
    expect(r.data).toEqual([1, "two"]);
  });

  it("one fail → aggregated failure result", () => {
    const r = combine(ok<number>(1), notFound());
    expect(r.failed).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.NotFound);
    expect(r.errorCode).toBe(ErrorCodes.NOT_FOUND);
  });

  it("multiple failures aggregate messages + inputErrors", () => {
    const r = combine(
      validationFailed({
        inputErrors: [{ field: "a", errors: [{ key: "TK.X" }] }],
      }),
      validationFailed({
        inputErrors: [{ field: "b", errors: [{ key: "TK.Y" }] }],
      }),
    );
    expect(r.inputErrors.map((e) => e.field)).toEqual(["a", "b"]);
  });

  it("supports up to 5 inputs", () => {
    const r = combine(ok(1), ok(2), ok(3), ok(4), ok(5));
    expect(r.success).toBe(true);
    expect(r.data).toEqual([1, 2, 3, 4, 5]);
  });
});

describe("combineMany() — iterable form", () => {
  it("empty input → ok with empty array", () => {
    const r = combineMany<number>([]);
    expect(r.success).toBe(true);
    expect(r.data).toEqual([]);
  });

  it("aggregates failures inheriting first-fail status", () => {
    const r = combineMany<number>([
      ok(1),
      notFound<number>({ traceId: "t1" }),
      fail<number>({
        statusCode: HttpStatusCode.Conflict,
        errorCode: "X",
      }),
    ]);
    expect(r.failed).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.NotFound);
    expect(r.errorCode).toBe(ErrorCodes.NOT_FOUND);
  });

  it("all-success → ok with array", () => {
    const r = combineMany<number>([ok(1), ok(2), ok(3)]);
    expect(r.success).toBe(true);
    expect(r.data).toEqual([1, 2, 3]);
  });

  it("falls back to VALIDATION_FAILED + 400 on error-shape with no codes", () => {
    const failNoCode = fail<number>({
      statusCode: undefined as unknown as HttpStatusCode,
    });
    const r = combineMany<number>([failNoCode]);
    expect(r.failed).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
  });
});
