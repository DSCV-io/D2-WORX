// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  defaultTitleForStatus,
  PROBLEM_DETAILS_CONTENT_TYPE,
  PROBLEM_TYPE_URI_PREFIX,
  ProblemDetailsExtensionKeys,
  ProblemDetailsTitles,
} from "../src/generated/problem-details.g.js";

describe("PROBLEM_TYPE_URI_PREFIX wire pin", () => {
  it("matches the .NET D2ProblemDetailsKeys PROBLEM_TYPE_URI_PREFIX value", () => {
    expect(PROBLEM_TYPE_URI_PREFIX).toBe("https://problems.d2.dcsv.io/");
  });
});

describe("ProblemDetailsExtensionKeys wire pin", () => {
  it("ERROR_CODE matches d2_error_code", () => {
    expect(ProblemDetailsExtensionKeys.ERROR_CODE).toBe("d2_error_code");
  });

  it("MESSAGES matches d2_messages", () => {
    expect(ProblemDetailsExtensionKeys.MESSAGES).toBe("d2_messages");
  });

  it("INPUT_ERRORS matches d2_input_errors", () => {
    expect(ProblemDetailsExtensionKeys.INPUT_ERRORS).toBe("d2_input_errors");
  });

  it("TRACE_ID matches traceId", () => {
    expect(ProblemDetailsExtensionKeys.TRACE_ID).toBe("traceId");
  });

  it("CORRELATION_ID matches correlationId", () => {
    expect(ProblemDetailsExtensionKeys.CORRELATION_ID).toBe("correlationId");
  });
});

describe("PROBLEM_DETAILS_CONTENT_TYPE wire pin", () => {
  it("matches application/problem+json (RFC 7807 §6.1)", () => {
    expect(PROBLEM_DETAILS_CONTENT_TYPE).toBe("application/problem+json");
  });
});

describe("ProblemDetailsTitles wire pin", () => {
  // Per-VALUE pins for the codegen-emitted ProblemDetailsTitles catalog.
  // Cross-language parity with the .NET side is structurally guaranteed
  // (same spec source); these pins protect against accidental .g.ts
  // tampering AND against accidental spec edits.
  it.each([
    ["BAD_REQUEST", "Bad Request"],
    ["UNAUTHORIZED", "Unauthorized"],
    ["FORBIDDEN", "Forbidden"],
    ["NOT_FOUND", "Not Found"],
    ["CONFLICT", "Conflict"],
    ["PAYLOAD_TOO_LARGE", "Payload Too Large"],
    ["TOO_MANY_REQUESTS", "Too Many Requests"],
    ["INTERNAL_SERVER_ERROR", "Internal Server Error"],
    ["SERVICE_UNAVAILABLE", "Service Unavailable"],
    ["REQUEST_FAILED", "Request Failed"],
  ])("%s matches %s", (constName, expected) => {
    const map = ProblemDetailsTitles as unknown as Record<string, string>;
    expect(map[constName]).toBe(expected);
  });
});

describe("defaultTitleForStatus", () => {
  it.each([
    [400, "Bad Request"],
    [401, "Unauthorized"],
    [403, "Forbidden"],
    [404, "Not Found"],
    [409, "Conflict"],
    [413, "Payload Too Large"],
    [429, "Too Many Requests"],
    [500, "Internal Server Error"],
    [503, "Service Unavailable"],
  ])("status %i returns %s", (status, expected) => {
    expect(defaultTitleForStatus(status)).toBe(expected);
  });

  it("returns fallback title for unrecognized status", () => {
    expect(defaultTitleForStatus(499)).toBe("Request Failed");
  });
});
