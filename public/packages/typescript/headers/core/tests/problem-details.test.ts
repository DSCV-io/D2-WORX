// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
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
  PROBLEM_TYPE_URI_PREFIX,
  ProblemDetailsExtensionKeys,
  toProblemDetails,
} from "../src/index.js";

describe("toProblemDetails — happy path", () => {
  it("renders an unauthorized failure as RFC 7807 body", () => {
    const failure = AuthFailures.bearerMissing({ traceId: "trace-1" });
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
    const failure = AuthFailures.scopeInsufficient({ traceId: "trace-2" });
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
    const failure = factory({ traceId: "trace-x" });
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body[ProblemDetailsExtensionKeys.ERROR_CODE]).toBe(
      failure.errorCode,
    );
    expect(body[ProblemDetailsExtensionKeys.TRACE_ID]).toBe("trace-x");
    expect(body.status).toBe(failure.statusCode);
  });
});

describe("toProblemDetails — category extension (omit-when-absent)", () => {
  it("emits d2_category extension when failure carries a category", () => {
    // validationFailed() stamps category: "validation_failure"
    const failure = validationFailed();
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body[ProblemDetailsExtensionKeys.CATEGORY]).toBe(
      "validation_failure",
    );
  });

  it("emits d2_category for notFound (category: not_found)", () => {
    const failure = notFound();
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(body[ProblemDetailsExtensionKeys.CATEGORY]).toBe("not_found");
  });

  it("omits d2_category extension when failure has no category", () => {
    // fail() with no category → category is undefined
    const failure = fail({ statusCode: 500, errorCode: "X" });
    const body = toProblemDetails(failure, { instance: "/x" });
    expect(ProblemDetailsExtensionKeys.CATEGORY in body).toBe(false);
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
