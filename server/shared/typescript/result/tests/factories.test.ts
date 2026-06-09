// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { ErrorCodes } from "../src/error-codes.g.js";
import {
  canceled,
  conflict,
  forbidden,
  notFound,
  payloadTooLarge,
  serviceUnavailable,
  tooManyRequests,
  unauthorized,
  unhandledException,
  validationFailed,
} from "../src/factories.g.js";
import { created, fail, ok, someFound } from "../src/factories.js";
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
    // The default TK message must ride the wire as the snake en-US.json key
    // (which resolves in the Translator/Paraglide catalog), NOT the raw
    // PascalCase symbol path (which would fall through un-renderable). The
    // factory references the `TK.common.errors.NOT_FOUND` constant whose value
    // is this snake key, matching the .NET wire key for the same factory.
    expect(r.messages[0]?.key).toBe("common_errors_NOT_FOUND");
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
    expect(r.messages[0]?.key).toBe("common_errors_UNAUTHORIZED");
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
    expect(r.messages[0]?.key).toBe("common_errors_FORBIDDEN");
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
    expect(r.messages[0]?.key).toBe("common_errors_VALIDATION_FAILED");
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
    expect(r.messages[0]?.key).toBe("common_errors_CONFLICT");
  });
});

describe("serviceUnavailable()", () => {
  it("HTTP 503 + SERVICE_UNAVAILABLE", () => {
    const r = serviceUnavailable();
    expect(r.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(r.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
    expect(r.messages[0]?.key).toBe("common_errors_SERVICE_UNAVAILABLE");
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
    // Quirk: default TK key is UNKNOWN, NOT UNHANDLED_EXCEPTION (no such TK key exists).
    // The error CODE and the user-message KEY deliberately differ.
    expect(r.messages[0]?.key).toBe("common_errors_UNKNOWN");
  });
});

describe("payloadTooLarge()", () => {
  it("HTTP 413 + PAYLOAD_TOO_LARGE", () => {
    const r = payloadTooLarge();
    expect(r.statusCode).toBe(HttpStatusCode.RequestEntityTooLarge);
    expect(r.errorCode).toBe(ErrorCodes.PAYLOAD_TOO_LARGE);
    expect(r.messages[0]?.key).toBe("common_errors_PAYLOAD_TOO_LARGE");
  });
});

describe("tooManyRequests()", () => {
  it("HTTP 429 + RATE_LIMITED + override-able", () => {
    const r = tooManyRequests();
    expect(r.statusCode).toBe(HttpStatusCode.TooManyRequests);
    expect(r.errorCode).toBe(ErrorCodes.RATE_LIMITED);
    // Quirk: factory name is tooManyRequests, error code is RATE_LIMITED, but
    // the default TK key is TOO_MANY_REQUESTS (three-way name divergence by design).
    expect(r.messages[0]?.key).toBe("common_errors_TOO_MANY_REQUESTS");
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
    expect(r.messages[0]?.key).toBe("common_errors_CANCELED");
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

describe("generated factories stamp the spec category", () => {
  // Per-factory pin: each generated base factory sets the snake-wire category
  // declared in contracts/error-codes/error-codes.spec.json. Producer-set at
  // generation time (no runtime registry lookup).
  it.each([
    [notFound(), "not_found"],
    [forbidden(), "policy_denied"],
    [unauthorized(), "policy_denied"],
    [validationFailed(), "validation_failure"],
    [conflict(), "conflict"],
    [unhandledException(), "internal_error"],
    [serviceUnavailable(), "infrastructure_unavailable"],
    [tooManyRequests(), "rate_limited"],
    [payloadTooLarge(), "payload_too_large"],
    [canceled(), "validation_failure"],
  ] as const)("factory carries category %#", (result, expectedCategory) => {
    expect(result.category).toBe(expectedCategory);
  });

  it("with_error_code factory honors a caller category override", () => {
    // The override exists so a delegating domain factory can stamp its own
    // code's category onto the base factory it delegates to.
    expect(forbidden({ category: "validation_failure" }).category).toBe(
      "validation_failure",
    );
    expect(unauthorized({ category: "not_found" }).category).toBe("not_found");
  });

  it("hand-rolled ok/created/fail carry no category", () => {
    expect(ok().category).toBeUndefined();
    expect(created().category).toBeUndefined();
    expect(fail().category).toBeUndefined();
  });

  it("someFound carries partial_success category", () => {
    expect(someFound().category).toBe("partial_success");
    expect(someFound<{ id: string }>({ data: { id: "x" } }).category).toBe(
      "partial_success",
    );
  });
});
