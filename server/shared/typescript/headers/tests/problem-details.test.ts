// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { AuthFailures, AuthErrorCodes } from "@d2/auth-abstractions";
import {
  fail,
  notFound,
  unauthorized,
  forbidden,
  conflict,
  serviceUnavailable,
  validationFailed,
} from "@d2/result";
import {
  PROBLEM_DETAILS_CONTENT_TYPE,
  PROBLEM_TYPE_URI_PREFIX,
  ProblemDetailsExtensionKeys,
  ProblemDetailsTitles,
  toProblemDetails,
} from "../src/index.js";

describe("toProblemDetails — happy path", () => {
  it("renders an unauthorized failure as RFC 7807 body", () => {
    const failure = AuthFailures.bearerMissing("trace-1");
    const body = toProblemDetails(failure, { instance: "/dashboard" });
    expect(body.type).toBe(`${PROBLEM_TYPE_URI_PREFIX}auth-bearer-missing`);
    expect(body.status).toBe(401);
    expect(body.instance).toBe("/dashboard");
    expect(body.title).toBe("Unauthorized");
    expect(body[ProblemDetailsExtensionKeys.ERROR_CODE]).toBe(
      AuthErrorCodes.AUTH_BEARER_MISSING,
    );
    expect(body[ProblemDetailsExtensionKeys.TRACE_ID]).toBe("trace-1");
    expect(Array.isArray(body[ProblemDetailsExtensionKeys.MESSAGES])).toBe(
      true,
    );
  });

  it("uses opts.title override when provided", () => {
    const failure = AuthFailures.scopeInsufficient("trace-2");
    const body = toProblemDetails(failure, {
      instance: "/admin",
      title: "Custom title",
    });
    expect(body.title).toBe("Custom title");
  });

  it("emits opts.detail when provided", () => {
    const failure = unauthorized();
    const body = toProblemDetails(failure, {
      instance: "/x",
      detail: "Helpful detail",
    });
    expect(body.detail).toBe("Helpful detail");
  });

  it("omits detail when not provided", () => {
    const failure = unauthorized();
    const body = toProblemDetails(failure, { instance: "/x" });
    expect("detail" in body).toBe(false);
  });

  it("omits traceId extension when failure has no traceId", () => {
    const failure = unauthorized();
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(ProblemDetailsExtensionKeys.TRACE_ID in body).toBe(false);
  });

  it("omits messages extension when failure has empty messages", () => {
    const failure = fail({ statusCode: 500, errorCode: "X" });
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(ProblemDetailsExtensionKeys.MESSAGES in body).toBe(false);
  });

  it("falls back to UNKNOWN error code when failure has none", () => {
    const failure = fail({ statusCode: 500 });
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body[ProblemDetailsExtensionKeys.ERROR_CODE]).toBe("UNKNOWN");
    expect(body.type).toBe(`${PROBLEM_TYPE_URI_PREFIX}unknown`);
  });

  it("kebabizes underscored error codes in the type URI", () => {
    const failure = fail({ statusCode: 401, errorCode: "AUTH_BEARER_MISSING" });
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body.type).toBe(`${PROBLEM_TYPE_URI_PREFIX}auth-bearer-missing`);
  });
});

describe("toProblemDetails — per-failure-shape titles", () => {
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
    // Spec carries REQUEST_FAILED = "Request Failed" as the fallback entry
    // (null httpStatus row) — any status not in the per-status table maps
    // here. Cross-language parity locked: .NET emits the same fallback.
    [499, "Request Failed"],
  ])("status %i defaults to title %s", (status, title) => {
    // Cast: the test deliberately includes 499 (not in the HttpStatusCode union)
    // to exercise the "Request Failed" fallback path. fail() accepts the narrow
    // HttpStatusCode union; widen via `as never` to allow the out-of-union numeric.
    const failure = fail({ statusCode: status as never });
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body.title).toBe(title);
  });
});

describe("toProblemDetails — every D2Result factory survives the round-trip", () => {
  it.each([
    ["notFound", notFound()],
    ["unauthorized", unauthorized()],
    ["forbidden", forbidden()],
    ["validationFailed", validationFailed()],
    ["conflict", conflict()],
    ["serviceUnavailable", serviceUnavailable()],
  ])("%s", (_name, failure) => {
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body.status).toBe(failure.statusCode);
    expect(body[ProblemDetailsExtensionKeys.ERROR_CODE]).toBe(
      failure.errorCode,
    );
    expect(Array.isArray(body[ProblemDetailsExtensionKeys.MESSAGES])).toBe(
      true,
    );
  });
});

describe("toProblemDetails — every AuthFailures.* survives the round-trip", () => {
  it.each([
    AuthFailures.bearerMalformed,
    AuthFailures.bearerMissing,
    AuthFailures.jwksUnavailable,
    AuthFailures.jwtActChainMalformed,
    AuthFailures.jwtAudienceMismatch,
    AuthFailures.jwtClaimMissing,
    AuthFailures.jwtExpired,
    AuthFailures.jwtIssuerMismatch,
    AuthFailures.jwtKidNotFound,
    AuthFailures.jwtNotYetValid,
    AuthFailures.jwtSignatureInvalid,
    AuthFailures.scopeInsufficient,
    AuthFailures.sessionLivenessUnavailable,
    AuthFailures.sessionRevoked,
  ])("AuthFailures.%s", (factory) => {
    const failure = factory("trace-x");
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body[ProblemDetailsExtensionKeys.ERROR_CODE]).toBe(
      failure.errorCode,
    );
    expect(body[ProblemDetailsExtensionKeys.TRACE_ID]).toBe("trace-x");
    expect(body.status).toBe(failure.statusCode);
  });
});

describe("PROBLEM_TYPE_URI_PREFIX wire pin", () => {
  it("matches the .NET D2ProblemDetailsExtensions PROBLEM_TYPE_URI_PREFIX value", () => {
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

describe("toProblemDetails — input errors surfacing", () => {
  it("emits d2_input_errors extension when failure carries non-empty inputErrors", () => {
    const failure = validationFailed({
      inputErrors: [{ field: "email", errors: [{ key: "TK.X" }] }],
    });
    const body = toProblemDetails(failure, { instance: "/x" });
    const ext = body[ProblemDetailsExtensionKeys.INPUT_ERRORS];
    expect(Array.isArray(ext)).toBe(true);
    expect((ext as ReadonlyArray<{ field: string }>)[0]?.field).toBe("email");
  });

  it("omits d2_input_errors extension when failure has empty inputErrors", () => {
    const failure = unauthorized();
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(ProblemDetailsExtensionKeys.INPUT_ERRORS in body).toBe(false);
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
