// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { ErrorCodes } from "../src/error-codes.js";
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
