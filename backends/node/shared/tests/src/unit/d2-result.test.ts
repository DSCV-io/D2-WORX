import { describe, it, expect } from "vitest";
import { D2Result, ErrorCodes, HttpStatusCode, type InputError } from "@d2/result";

// ---------------------------------------------------------------------------
// Success factories
// ---------------------------------------------------------------------------

describe("D2Result.ok", () => {
  it("creates a success result with defaults", () => {
    const result = D2Result.ok();

    expect(result).toBeSuccess();
    expect(result).toHaveStatusCode(HttpStatusCode.OK);
    expect(result.data).toBeUndefined();
    expect(result.messages).toHaveLength(0);
    expect(result.inputErrors).toHaveLength(0);
    expect(result.errorCode).toBeUndefined();
    expect(result.traceId).toBeUndefined();
  });

  it("creates a success result with data", () => {
    const data = { id: 1, name: "test" };
    const result = D2Result.ok({ data });

    expect(result).toBeSuccess();
    expect(result).toHaveData(data);
  });

  it("creates a success result with messages", () => {
    const result = D2Result.ok({ messages: ["Item created."] });

    expect(result).toBeSuccess();
    expect(result).toHaveMessages(["Item created."]);
  });

  it("creates a success result with traceId", () => {
    const result = D2Result.ok({ traceId: "trace-123" });

    expect(result).toBeSuccess();
    expect(result.traceId).toBe("trace-123");
  });
});

describe("D2Result.created", () => {
  it("creates a 201 success result", () => {
    const data = { id: 42 };
    const result = D2Result.created({ data });

    expect(result).toBeSuccess();
    expect(result).toHaveStatusCode(HttpStatusCode.Created);
    expect(result).toHaveData(data);
  });
});

// ---------------------------------------------------------------------------
// Failure factories
// ---------------------------------------------------------------------------

describe("D2Result.fail", () => {
  it("creates a failure result with defaults", () => {
    const result = D2Result.fail();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.BadRequest);
    expect(result.data).toBeUndefined();
  });

  it("creates a failure result with messages", () => {
    const result = D2Result.fail({
      messages: ["Something went wrong."],
    });

    expect(result).toBeFailure();
    expect(result).toHaveMessages(["Something went wrong."]);
  });

  it("creates a failure result with custom status code", () => {
    const result = D2Result.fail({
      statusCode: HttpStatusCode.ServiceUnavailable,
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.ServiceUnavailable);
  });

  it("creates a failure result with error code", () => {
    const result = D2Result.fail({
      errorCode: ErrorCodes.RATE_LIMITED,
    });

    expect(result).toBeFailure();
    expect(result).toHaveErrorCode(ErrorCodes.RATE_LIMITED);
  });

  it("creates a failure result with input errors", () => {
    const inputErrors: InputError[] = [
      ["email", "Email is required.", "Email must be valid."],
      ["password", "Password is required."],
    ];

    const result = D2Result.fail({ inputErrors });

    expect(result).toBeFailure();
    expect(result).toHaveInputErrors(inputErrors);
  });
});

describe("D2Result.notFound", () => {
  it("creates a 404 result with default message", () => {
    const result = D2Result.notFound();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.NotFound);
    expect(result).toHaveErrorCode(ErrorCodes.NOT_FOUND);
    expect(result).toHaveMessages(["common_errors_NOT_FOUND"]);
  });

  it("creates a 404 result with custom message", () => {
    const result = D2Result.notFound({
      messages: ["User not found."],
    });

    expect(result).toHaveMessages(["User not found."]);
  });
});

describe("D2Result.unauthorized", () => {
  it("creates a 401 result with default message", () => {
    const result = D2Result.unauthorized();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.Unauthorized);
    expect(result).toHaveErrorCode(ErrorCodes.UNAUTHORIZED);
    expect(result).toHaveMessages(["common_errors_UNAUTHORIZED"]);
  });
});

describe("D2Result.forbidden", () => {
  it("creates a 403 result with default message", () => {
    const result = D2Result.forbidden();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.Forbidden);
    expect(result).toHaveErrorCode(ErrorCodes.FORBIDDEN);
    expect(result).toHaveMessages(["common_errors_FORBIDDEN"]);
  });
});

describe("D2Result.validationFailed", () => {
  it("creates a 400 validation failure with default message", () => {
    const result = D2Result.validationFailed();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.BadRequest);
    expect(result).toHaveErrorCode(ErrorCodes.VALIDATION_FAILED);
    expect(result).toHaveMessages(["common_errors_VALIDATION_FAILED"]);
  });

  it("creates a validation failure with input errors", () => {
    const inputErrors: InputError[] = [["name", "Name is required."]];

    const result = D2Result.validationFailed({ inputErrors });

    expect(result).toBeFailure();
    expect(result).toHaveInputErrors(inputErrors);
  });

  it("accepts an errorCode override for client-side discrimination", () => {
    const result = D2Result.validationFailed({ errorCode: "PHONE_NO_CHANGE" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.BadRequest);
    expect(result).toHaveErrorCode("PHONE_NO_CHANGE");
    expect(result).toHaveMessages(["common_errors_VALIDATION_FAILED"]);
  });
});

describe("D2Result.tooManyRequests", () => {
  it("creates a 429 result with default message + RATE_LIMITED code", () => {
    const result = D2Result.tooManyRequests();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.TooManyRequests);
    expect(result).toHaveErrorCode(ErrorCodes.RATE_LIMITED);
    expect(result).toHaveMessages(["common_errors_TOO_MANY_REQUESTS"]);
  });

  it("respects an explicit messages override", () => {
    const result = D2Result.tooManyRequests({
      messages: ["common_errors_OTP_RATE_LIMITED"],
    });

    expect(result).toHaveMessages(["common_errors_OTP_RATE_LIMITED"]);
    expect(result).toHaveErrorCode(ErrorCodes.RATE_LIMITED);
  });
});

describe("D2Result.conflict", () => {
  it("creates a 409 conflict result with default message", () => {
    const result = D2Result.conflict();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.Conflict);
    expect(result).toHaveErrorCode(ErrorCodes.CONFLICT);
    expect(result).toHaveMessages(["common_errors_CONFLICT"]);
  });
});

describe("D2Result.unhandledException", () => {
  it("creates a 500 result with default message", () => {
    const result = D2Result.unhandledException();

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.InternalServerError);
    expect(result).toHaveErrorCode(ErrorCodes.UNHANDLED_EXCEPTION);
    expect(result).toHaveMessages(["common_errors_unknown"]);
  });
});

// ---------------------------------------------------------------------------
// Partial success
// ---------------------------------------------------------------------------

describe("D2Result.someFound", () => {
  it("creates a 206 partial content result", () => {
    const data = [1, 2, 3];
    const result = D2Result.someFound({ data });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.PartialContent);
    expect(result).toHaveErrorCode(ErrorCodes.SOME_FOUND);
    expect(result).toHaveData(data);
  });

  it("someFound is failure but carries data", () => {
    const data = { found: ["a", "b"], missing: ["c"] };
    const result = D2Result.someFound({ data });

    expect(result.success).toBe(false);
    expect(result.data).toEqual(data);
  });
});

// ---------------------------------------------------------------------------
// Helpers: checkSuccess / checkFailure
// ---------------------------------------------------------------------------

describe("D2Result.checkSuccess", () => {
  it("returns data when result is success", () => {
    const data = { value: 42 };
    const result = D2Result.ok({ data });

    expect(result.checkSuccess()).toEqual(data);
  });

  it("returns undefined when result is failure", () => {
    const result = D2Result.fail<{ value: number }>();

    expect(result.checkSuccess()).toBeUndefined();
  });
});

describe("D2Result.checkFailure", () => {
  it("returns data when result is failure", () => {
    const data = [1, 2];
    const result = D2Result.someFound({ data });

    expect(result.checkFailure()).toEqual(data);
  });

  it("returns undefined when result is success", () => {
    const result = D2Result.ok({ data: "hello" });

    expect(result.checkFailure()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// failed getter
// ---------------------------------------------------------------------------

describe("D2Result.failed", () => {
  it("returns false for success results", () => {
    const result = D2Result.ok();
    expect(result.failed).toBe(false);
  });

  it("returns true for failure results", () => {
    const result = D2Result.fail();
    expect(result.failed).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Immutability
// ---------------------------------------------------------------------------

describe("D2Result immutability", () => {
  it("messages array is frozen", () => {
    const result = D2Result.ok({ messages: ["hello"] });

    expect(() => {
      (result.messages as string[]).push("world");
    }).toThrow();
  });

  it("inputErrors array is frozen", () => {
    const result = D2Result.validationFailed({
      inputErrors: [["field", "error"]],
    });

    expect(() => {
      (result.inputErrors as InputError[]).push(["other", "error"]);
    }).toThrow();
  });
});

// ---------------------------------------------------------------------------
// Bubbling
// ---------------------------------------------------------------------------

describe("D2Result.bubbleFail", () => {
  it("propagates failure with different data type", () => {
    const source = D2Result.notFound<string>({
      messages: ["User not found."],
      traceId: "trace-abc",
    });

    const bubbled = D2Result.bubbleFail<number>(source);

    expect(bubbled).toBeFailure();
    expect(bubbled).toHaveStatusCode(HttpStatusCode.NotFound);
    expect(bubbled).toHaveErrorCode(ErrorCodes.NOT_FOUND);
    expect(bubbled).toHaveMessages(["User not found."]);
    expect(bubbled.traceId).toBe("trace-abc");
    expect(bubbled.data).toBeUndefined();
  });

  it("copies input errors during bubbling", () => {
    const inputErrors: InputError[] = [["email", "Invalid email."]];
    const source = D2Result.validationFailed<string>({ inputErrors });

    const bubbled = D2Result.bubbleFail<{ id: number }>(source);

    expect(bubbled).toHaveInputErrors(inputErrors);
  });
});

describe("D2Result.bubble", () => {
  it("converts result with new data", () => {
    const source = D2Result.ok<string>({ data: "hello" });
    const bubbled = D2Result.bubble(source, 42);

    expect(bubbled).toBeSuccess();
    expect(bubbled).toHaveStatusCode(HttpStatusCode.OK);
    expect(bubbled).toHaveData(42);
  });

  it("converts failure result preserving all details", () => {
    const source = D2Result.fail<string>({
      messages: ["Oops."],
      statusCode: HttpStatusCode.Conflict,
      errorCode: ErrorCodes.CONFLICT,
      traceId: "trace-xyz",
    });

    const bubbled = D2Result.bubble<number[]>(source, [1, 2, 3]);

    expect(bubbled).toBeFailure();
    expect(bubbled).toHaveStatusCode(HttpStatusCode.Conflict);
    expect(bubbled).toHaveErrorCode(ErrorCodes.CONFLICT);
    expect(bubbled).toHaveMessages(["Oops."]);
    expect(bubbled.traceId).toBe("trace-xyz");
    expect(bubbled).toHaveData([1, 2, 3]);
  });
});

// ---------------------------------------------------------------------------
// Error codes enum completeness
// ---------------------------------------------------------------------------

describe("ErrorCodes", () => {
  it.each([
    "NOT_FOUND",
    "FORBIDDEN",
    "UNAUTHORIZED",
    "VALIDATION_FAILED",
    "CONFLICT",
    "UNHANDLED_EXCEPTION",
    "COULD_NOT_BE_SERIALIZED",
    "COULD_NOT_BE_DESERIALIZED",
    "SERVICE_UNAVAILABLE",
    "SOME_FOUND",
    "RATE_LIMITED",
  ] as const)("contains %s", (code) => {
    expect(ErrorCodes[code]).toBe(code);
  });
});

// ---------------------------------------------------------------------------
// HTTP status codes completeness
// ---------------------------------------------------------------------------

describe("HttpStatusCode", () => {
  it.each([
    ["OK", 200],
    ["Created", 201],
    ["PartialContent", 206],
    ["BadRequest", 400],
    ["Unauthorized", 401],
    ["Forbidden", 403],
    ["NotFound", 404],
    ["Conflict", 409],
    ["TooManyRequests", 429],
    ["InternalServerError", 500],
    ["ServiceUnavailable", 503],
  ] as const)("contains %s = %i", (name, value) => {
    expect(HttpStatusCode[name]).toBe(value);
  });
});

// ---------------------------------------------------------------------------
// Default status code inference
// ---------------------------------------------------------------------------

describe("D2Result constructor defaults", () => {
  it("defaults success to 200 OK", () => {
    const result = new D2Result({ success: true });
    expect(result).toHaveStatusCode(HttpStatusCode.OK);
  });

  it("defaults failure to 400 BadRequest", () => {
    const result = new D2Result({ success: false });
    expect(result).toHaveStatusCode(HttpStatusCode.BadRequest);
  });

  it("uses explicit statusCode over default", () => {
    const result = new D2Result({
      success: false,
      statusCode: HttpStatusCode.ServiceUnavailable,
    });
    expect(result).toHaveStatusCode(HttpStatusCode.ServiceUnavailable);
  });
});
