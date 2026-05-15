// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { ErrorCodes } from "../src/error-codes.js";
import {
  canceled,
  conflict,
  created,
  fail,
  forbidden,
  notFound,
  ok,
  payloadTooLarge,
  serviceUnavailable,
  someFound,
  tooManyRequests,
  unauthorized,
  unhandledException,
  validationFailed,
} from "../src/factories.js";
import { HttpStatusCode } from "../src/http-status-codes.js";

describe("ok()", () => {
  it("no args → success, no payload", () => {
    const r = ok();
    expect(r.success).toBe(true);
    expect(r.data).toBeUndefined();
    expect(r.statusCode).toBe(HttpStatusCode.OK);
  });

  it("data overload", () => {
    const r = ok<{ id: string }>({ id: "x" });
    expect(r.data).toEqual({ id: "x" });
  });

  it("data + traceId", () => {
    const r = ok<number>(42, "trace-2");
    expect(r.data).toBe(42);
    expect(r.traceId).toBe("trace-2");
  });

  it("string-typed payload preserved", () => {
    const r = ok<string>("two");
    expect(r.data).toBe("two");
    expect(r.traceId).toBeUndefined();
  });
});

describe("created()", () => {
  it("HTTP 201 + success=true", () => {
    const r = created();
    expect(r.success).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.Created);
  });
  it("traceId pass-through", () => {
    expect(created({ traceId: "t" }).traceId).toBe("t");
  });
});

describe("fail()", () => {
  it("default HTTP 400 + no errorCode", () => {
    const r = fail();
    expect(r.failed).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.errorCode).toBeUndefined();
  });
  it("status + errorCode override", () => {
    const r = fail({
      statusCode: HttpStatusCode.Conflict,
      errorCode: "MY_CODE",
    });
    expect(r.statusCode).toBe(HttpStatusCode.Conflict);
    expect(r.errorCode).toBe("MY_CODE");
  });
});

describe("notFound()", () => {
  it("HTTP 404 + NOT_FOUND code + default TK", () => {
    const r = notFound();
    expect(r.statusCode).toBe(HttpStatusCode.NotFound);
    expect(r.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(r.messages[0]?.key).toBe("TK.Common.Errors.NOT_FOUND");
  });
  it("override messages preserved", () => {
    const r = notFound({ messages: [{ key: "TK.X" }] });
    expect(r.messages).toEqual([{ key: "TK.X" }]);
  });
});

describe("unauthorized()", () => {
  it("HTTP 401 + UNAUTHORIZED code", () => {
    const r = unauthorized();
    expect(r.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(r.errorCode).toBe(ErrorCodes.UNAUTHORIZED);
  });
  it("errorCode override (e.g. AUTH_JWT_EXPIRED)", () => {
    expect(unauthorized({ errorCode: "AUTH_JWT_EXPIRED" }).errorCode).toBe(
      "AUTH_JWT_EXPIRED",
    );
  });
});

describe("forbidden()", () => {
  it("HTTP 403 + FORBIDDEN code", () => {
    const r = forbidden();
    expect(r.statusCode).toBe(HttpStatusCode.Forbidden);
    expect(r.errorCode).toBe(ErrorCodes.FORBIDDEN);
  });
  it("errorCode override", () => {
    expect(forbidden({ errorCode: "X" }).errorCode).toBe("X");
  });
});

describe("validationFailed()", () => {
  it("HTTP 400 + VALIDATION_FAILED + inputErrors pass-through", () => {
    const r = validationFailed({
      inputErrors: [{ field: "email", errors: [{ key: "TK.X" }] }],
    });
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
    expect(r.inputErrors).toHaveLength(1);
  });
  it("errorCode override (e.g. FILES_INVALID_CONTENT_TYPE)", () => {
    expect(
      validationFailed({ errorCode: "FILES_INVALID_CONTENT_TYPE" }).errorCode,
    ).toBe("FILES_INVALID_CONTENT_TYPE");
  });
});

describe("conflict()", () => {
  it("HTTP 409 + CONFLICT", () => {
    const r = conflict();
    expect(r.statusCode).toBe(HttpStatusCode.Conflict);
    expect(r.errorCode).toBe(ErrorCodes.CONFLICT);
  });
});

describe("serviceUnavailable()", () => {
  it("HTTP 503 + SERVICE_UNAVAILABLE", () => {
    const r = serviceUnavailable();
    expect(r.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(r.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
  });
  it("errorCode override (e.g. AUTH_JWKS_UNAVAILABLE)", () => {
    expect(
      serviceUnavailable({ errorCode: "AUTH_JWKS_UNAVAILABLE" }).errorCode,
    ).toBe("AUTH_JWKS_UNAVAILABLE");
  });
});

describe("unhandledException()", () => {
  it("HTTP 500 + UNHANDLED_EXCEPTION", () => {
    const r = unhandledException();
    expect(r.statusCode).toBe(HttpStatusCode.InternalServerError);
    expect(r.errorCode).toBe(ErrorCodes.UNHANDLED_EXCEPTION);
  });
});

describe("payloadTooLarge()", () => {
  it("HTTP 413 + PAYLOAD_TOO_LARGE", () => {
    const r = payloadTooLarge();
    expect(r.statusCode).toBe(HttpStatusCode.RequestEntityTooLarge);
    expect(r.errorCode).toBe(ErrorCodes.PAYLOAD_TOO_LARGE);
  });
});

describe("tooManyRequests()", () => {
  it("HTTP 429 + RATE_LIMITED + override-able", () => {
    const r = tooManyRequests();
    expect(r.statusCode).toBe(HttpStatusCode.TooManyRequests);
    expect(r.errorCode).toBe(ErrorCodes.RATE_LIMITED);
    expect(tooManyRequests({ errorCode: "OTP_RATE_LIMITED" }).errorCode).toBe(
      "OTP_RATE_LIMITED",
    );
  });
});

describe("canceled()", () => {
  it("HTTP 400 + CANCELED", () => {
    const r = canceled();
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.errorCode).toBe(ErrorCodes.CANCELED);
  });
});

describe("someFound()", () => {
  it("HTTP 206 + SOME_FOUND, success=false (partial-success ladder)", () => {
    const r = someFound<{ ids: string[] }>({ data: { ids: ["a"] } });
    expect(r.statusCode).toBe(HttpStatusCode.PartialContent);
    expect(r.errorCode).toBe(ErrorCodes.SOME_FOUND);
    expect(r.success).toBe(false);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data).toEqual({ ids: ["a"] });
  });
});
