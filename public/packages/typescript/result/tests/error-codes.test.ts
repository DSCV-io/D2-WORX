// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ALL_ERROR_CODES,
  ErrorCodes,
  getErrorHttpStatus,
} from "../src/error-codes.g.js";
import { HttpStatusCode } from "../src/http-status-codes.js";

describe("ErrorCodes — per-VALUE pinning (defends rename safety)", () => {
  it.each([
    ["NOT_FOUND", "NOT_FOUND"],
    ["FORBIDDEN", "FORBIDDEN"],
    ["UNAUTHORIZED", "UNAUTHORIZED"],
    ["VALIDATION_FAILED", "VALIDATION_FAILED"],
    ["CONFLICT", "CONFLICT"],
    ["UNHANDLED_EXCEPTION", "UNHANDLED_EXCEPTION"],
    ["COULD_NOT_BE_SERIALIZED", "COULD_NOT_BE_SERIALIZED"],
    ["COULD_NOT_BE_DESERIALIZED", "COULD_NOT_BE_DESERIALIZED"],
    ["SERVICE_UNAVAILABLE", "SERVICE_UNAVAILABLE"],
    ["SOME_FOUND", "SOME_FOUND"],
    ["PARTIAL_SUCCESS", "PARTIAL_SUCCESS"],
    ["RATE_LIMITED", "RATE_LIMITED"],
    ["IDEMPOTENCY_IN_FLIGHT", "IDEMPOTENCY_IN_FLIGHT"],
    ["PAYLOAD_TOO_LARGE", "PAYLOAD_TOO_LARGE"],
    ["CANCELED", "CANCELED"],
  ])("ErrorCodes.%s = %s", (key, value) => {
    expect(ErrorCodes[key as keyof typeof ErrorCodes]).toBe(value);
  });
});

describe("ALL_ERROR_CODES — exhaustiveness", () => {
  it("contains every ErrorCodes key", () => {
    expect([...ALL_ERROR_CODES].sort()).toEqual(Object.keys(ErrorCodes).sort());
  });

  it("preserves spec order (NOT_FOUND first, CANCELED last)", () => {
    expect(ALL_ERROR_CODES[0]).toBe("NOT_FOUND");
    expect(ALL_ERROR_CODES[ALL_ERROR_CODES.length - 1]).toBe("CANCELED");
  });
});

describe("getErrorHttpStatus — per-VALUE pinning (mirrors .NET ErrorCodes.GetHttpStatus)", () => {
  it.each([
    ["NOT_FOUND", 404],
    ["FORBIDDEN", 403],
    ["UNAUTHORIZED", 401],
    ["VALIDATION_FAILED", 400],
    ["CONFLICT", 409],
    ["UNHANDLED_EXCEPTION", 500],
    ["COULD_NOT_BE_SERIALIZED", 500],
    ["COULD_NOT_BE_DESERIALIZED", 500],
    ["SERVICE_UNAVAILABLE", 503],
    ["SOME_FOUND", 206],
    ["PARTIAL_SUCCESS", 207],
    ["RATE_LIMITED", 429],
    ["IDEMPOTENCY_IN_FLIGHT", 409],
    ["PAYLOAD_TOO_LARGE", 413],
    ["CANCELED", 400],
  ])("getErrorHttpStatus(%s) = %s", (code, expectedStatus) => {
    expect(getErrorHttpStatus(code)).toBe(expectedStatus);
  });

  it("returns 500 for an unknown code (defensive default)", () => {
    expect(getErrorHttpStatus("UNKNOWN_TO_THE_CATALOG")).toBe(500);
  });
});

describe("HttpStatusCode — per-VALUE pinning", () => {
  it.each([
    ["OK", 200],
    ["Created", 201],
    ["PartialContent", 206],
    ["BadRequest", 400],
    ["Unauthorized", 401],
    ["Forbidden", 403],
    ["NotFound", 404],
    ["Conflict", 409],
    ["RequestEntityTooLarge", 413],
    ["TooManyRequests", 429],
    ["InternalServerError", 500],
    ["ServiceUnavailable", 503],
  ])("HttpStatusCode.%s = %s", (key, value) => {
    expect(HttpStatusCode[key as keyof typeof HttpStatusCode]).toBe(value);
  });
});
