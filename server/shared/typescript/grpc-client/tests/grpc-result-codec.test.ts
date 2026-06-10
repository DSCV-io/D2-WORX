// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { D2ResultProto } from "@d2/protos";
import {
  D2Result,
  HttpStatusCode,
  notFound,
  conflict,
  validationFailed,
  unauthorized,
  serviceUnavailable,
  unhandledException,
  tooManyRequests,
  payloadTooLarge,
  canceled,
  someFound,
  ok,
  fail,
  inputError,
  tk,
} from "@d2/result";
import { ErrorCategoryWire, type ErrorCategory } from "@d2/error-category";
import { TK } from "@d2/i18n-keys";
import { d2ResultToProto } from "../src/d2-result-to-proto.js";
import { d2ResultFromProto } from "../src/d2-result-from-proto.js";
import {
  handleGrpcCall,
  isTransientGrpcError,
  unaryCall,
} from "../src/handle-grpc-call.js";
import type { ServiceError } from "@grpc/grpc-js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeServiceError(
  code: number,
  message: string,
  details: string,
): ServiceError {
  const err = new Error(message) as ServiceError;
  err.code = code;
  err.details = details;
  err.metadata = { getMap: () => ({}) } as unknown as ServiceError["metadata"];
  return err;
}

function assertRoundTrip<T>(source: D2Result<T>, data?: T): void {
  const proto = d2ResultToProto(source as D2Result<unknown>);
  const rebuilt = d2ResultFromProto<T>(proto, data);

  expect(rebuilt.success).toBe(source.success);
  expect(rebuilt.statusCode).toBe(source.statusCode);
  expect(rebuilt.errorCode).toBe(source.errorCode);
  expect(rebuilt.category).toBe(source.category);
  expect(rebuilt.traceId).toBe(source.traceId);

  // messages: key + params
  expect(rebuilt.messages.length).toBe(source.messages.length);
  for (let i = 0; i < source.messages.length; i++) {
    expect(rebuilt.messages[i].key).toBe(source.messages[i].key);
    // params: undefined for empty, or match entries
    const srcParams = source.messages[i].params;
    const rebParams = rebuilt.messages[i].params;
    if (!srcParams || Object.keys(srcParams).length === 0) {
      expect(rebParams).toBeUndefined();
    } else {
      expect(rebParams).toBeDefined();
      for (const [k, v] of Object.entries(srcParams))
        expect((rebParams as Record<string, unknown>)[k]).toBe(String(v));
    }
  }

  // inputErrors
  expect(rebuilt.inputErrors.length).toBe(source.inputErrors.length);
  for (let i = 0; i < source.inputErrors.length; i++) {
    expect(rebuilt.inputErrors[i].field).toBe(source.inputErrors[i].field);
    expect(rebuilt.inputErrors[i].errors.length).toBe(
      source.inputErrors[i].errors.length,
    );
    for (let j = 0; j < source.inputErrors[i].errors.length; j++) {
      expect(rebuilt.inputErrors[i].errors[j].key).toBe(
        source.inputErrors[i].errors[j].key,
      );
      // params: undefined for empty, or match entries (mirrors top-level messages[] assertion)
      const srcErrParams = source.inputErrors[i].errors[j].params;
      const rebErrParams = rebuilt.inputErrors[i].errors[j].params;
      if (!srcErrParams || Object.keys(srcErrParams).length === 0) {
        expect(rebErrParams).toBeUndefined();
      } else {
        expect(rebErrParams).toBeDefined();
        for (const [k, v] of Object.entries(srcErrParams))
          expect((rebErrParams as Record<string, unknown>)[k]).toBe(String(v));
      }
    }
  }

  expect(rebuilt.data).toStrictEqual(data);
}

// ---------------------------------------------------------------------------
// d2ResultToProto + d2ResultFromProto round-trips
// ---------------------------------------------------------------------------

describe("d2ResultToProto / d2ResultFromProto — round-trip every shape", () => {
  it("ok (success, no data)", () => assertRoundTrip(ok()));

  it("ok with data", () => {
    const source = ok({ id: "abc", name: "Alice" });
    assertRoundTrip(source, source.data);
  });

  it("notFound", () => assertRoundTrip(notFound()));

  it("conflict", () => assertRoundTrip(conflict()));

  it("unauthorized", () => assertRoundTrip(unauthorized()));

  it("serviceUnavailable", () => assertRoundTrip(serviceUnavailable()));

  it("unhandledException", () => assertRoundTrip(unhandledException()));

  it("tooManyRequests", () => assertRoundTrip(tooManyRequests()));

  it("payloadTooLarge", () => assertRoundTrip(payloadTooLarge()));

  it("canceled", () => assertRoundTrip(canceled()));

  it("someFound with data (partial success — success=false AND data present)", () => {
    const items = [{ id: "1" }, { id: "2" }];
    const source = someFound({ data: items });
    assertRoundTrip(source, items);
  });

  it("validationFailed with multi-field multi-message inputErrors WITH params", () => {
    const source = validationFailed({
      inputErrors: [
        inputError("email", [
          tk("common_errors_VALIDATION_FAILED"),
          tk("common_errors_TOO_LONG", { max: "254" }),
        ]),
        inputError("phone", [
          tk("common_errors_BAD_REQUEST"),
          tk("common_errors_VALIDATION_FAILED", {
            field: "phone",
            rule: "e164",
          }),
        ]),
      ],
    });
    assertRoundTrip(source);
  });

  it("typed failure D2Result<T> — no data on a failure", () => {
    const source = notFound<{ id: string }>({ traceId: "trace-xyz" });
    const proto = d2ResultToProto(source);
    const rebuilt = d2ResultFromProto<{ id: string }>(proto);
    expect(rebuilt.data).toBeUndefined();
    expect(rebuilt.success).toBe(false);
    expect(rebuilt.traceId).toBe("trace-xyz");
  });

  it("fail with non-catalog errorCode (category undefined)", () => {
    // Use D2Result ctor directly — fail() doesn't thread category, so this
    // test deliberately leaves category undefined to verify absent-category behavior.
    const source = new D2Result({
      success: false,
      errorCode: "CUSTOM_CODE_XYZ",
      messages: [{ key: "custom_key" }],
    });
    assertRoundTrip(source);
    const proto = d2ResultToProto(source);
    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.category).toBeUndefined();
    expect(rebuilt.errorCode).toBe("CUSTOM_CODE_XYZ");
  });

  it("category field round-trips for every ErrorCategory", () => {
    const categories: ErrorCategory[] = [
      ErrorCategoryWire.Conflict,
      ErrorCategoryWire.InfrastructureUnavailable,
      ErrorCategoryWire.InternalError,
      ErrorCategoryWire.NotFound,
      ErrorCategoryWire.PartialSuccess,
      ErrorCategoryWire.PayloadTooLarge,
      ErrorCategoryWire.PolicyDenied,
      ErrorCategoryWire.RateLimited,
      ErrorCategoryWire.ValidationFailure,
    ];
    for (const cat of categories) {
      // Construct directly — `fail()` doesn't thread the optional category field;
      // use D2Result ctor to test the codec in isolation from factory plumbing.
      const source = new D2Result({
        success: false,
        messages: [{ key: "k" }],
        category: cat,
      });
      const proto = d2ResultToProto(source);
      const rebuilt = d2ResultFromProto(proto);
      expect(rebuilt.category).toBe(cat);
    }
  });

  it("traceId thread-through", () => {
    const source = notFound({ traceId: "trace-abc-123" });
    const proto = d2ResultToProto(source);
    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.traceId).toBe("trace-abc-123");
  });

  it("TKMessage with params — type bridge: proto {[k]:string} → Record<string,unknown>", () => {
    const source = fail({
      messages: [
        tk("auth_errors_LOCALE_INVALID_FORMAT", {
          locale: "xx-ZZ",
          rule: "BCP47",
        }),
      ],
    });
    const proto = d2ResultToProto(source);
    expect(proto.messages[0].params["locale"]).toBe("xx-ZZ");
    expect(proto.messages[0].params["rule"]).toBe("BCP47");

    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.messages[0].key).toBe("auth_errors_LOCALE_INVALID_FORMAT");
    expect(
      (rebuilt.messages[0].params as Record<string, unknown>)["locale"],
    ).toBe("xx-ZZ");
    expect(
      (rebuilt.messages[0].params as Record<string, unknown>)["rule"],
    ).toBe("BCP47");
  });

  it("empty params map → TKMessage.params is undefined after round-trip", () => {
    const source = fail({ messages: [{ key: "some_key" }] });
    const proto = d2ResultToProto(source);
    expect(Object.keys(proto.messages[0].params).length).toBe(0);

    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.messages[0].params).toBeUndefined();
  });

  it("statusCode exact integer pin — NOT a lossy gRPC bucket (404 ≠ 409 ≠ 400)", () => {
    const pairs: [D2Result, number][] = [
      [notFound(), HttpStatusCode.NotFound],
      [conflict(), HttpStatusCode.Conflict],
      [validationFailed(), HttpStatusCode.BadRequest],
      [unauthorized(), HttpStatusCode.Unauthorized],
      [serviceUnavailable(), HttpStatusCode.ServiceUnavailable],
      [tooManyRequests(), HttpStatusCode.TooManyRequests],
    ];
    for (const [src, expected] of pairs) {
      const rebuilt = d2ResultFromProto(d2ResultToProto(src));
      expect(rebuilt.statusCode).toBe(expected);
    }
  });
});

// ---------------------------------------------------------------------------
// Adversarial / degradation
// ---------------------------------------------------------------------------

describe("d2ResultFromProto — adversarial", () => {
  it("unknown category wire string → category undefined, no throw", () => {
    const proto = D2ResultProto.create({
      success: false,
      statusCode: HttpStatusCode.BadRequest,
      category: "xyz_unknown_value",
    });
    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.category).toBeUndefined();
  });

  it("absent category → category undefined", () => {
    const proto = D2ResultProto.create({
      success: true,
      statusCode: HttpStatusCode.OK,
    });
    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.category).toBeUndefined();
  });

  it("empty errorCode string → errorCode undefined (truthyOrUndefined)", () => {
    const proto = D2ResultProto.create({
      success: false,
      statusCode: HttpStatusCode.BadRequest,
      errorCode: "",
    });
    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.errorCode).toBeUndefined();
  });

  it("empty traceId string → traceId undefined (truthyOrUndefined)", () => {
    const proto = D2ResultProto.create({
      success: true,
      statusCode: HttpStatusCode.OK,
      traceId: "",
    });
    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.traceId).toBeUndefined();
  });

  it("large inputErrors payload — envelope carries it intact (no ~8KB trailer bound)", () => {
    const manyFields = Array.from({ length: 50 }, (_, i) =>
      inputError(`field_${i}`, [
        tk("common_errors_VALIDATION_FAILED"),
        tk("common_errors_TOO_LONG", { max: "100", actual: String(i * 3) }),
      ]),
    );
    const source = validationFailed({ inputErrors: manyFields });
    const proto = d2ResultToProto(source);
    const rebuilt = d2ResultFromProto(proto);
    expect(rebuilt.inputErrors.length).toBe(50);
    for (let i = 0; i < 50; i++)
      expect(rebuilt.inputErrors[i].field).toBe(`field_${i}`);
  });
});

// ---------------------------------------------------------------------------
// isTransientGrpcError
// ---------------------------------------------------------------------------

describe("isTransientGrpcError", () => {
  // DEADLINE_EXCEEDED, RESOURCE_EXHAUSTED, ABORTED, INTERNAL, UNAVAILABLE
  const transient_codes = [4, 8, 10, 13, 14];
  // CANCELLED, INVALID_ARGUMENT, NOT_FOUND, PERMISSION_DENIED, UNAUTHENTICATED
  const non_transient_codes = [1, 3, 5, 7, 16];

  for (const code of transient_codes) {
    it(`gRPC status ${code} → isTransientGrpcError true`, () => {
      const err = makeServiceError(code, "err", "details");
      expect(isTransientGrpcError(err)).toBe(true);
    });
  }

  for (const code of non_transient_codes) {
    it(`gRPC status ${code} → isTransientGrpcError false`, () => {
      const err = makeServiceError(code, "err", "details");
      expect(isTransientGrpcError(err)).toBe(false);
    });
  }

  it("non-ServiceError → false", () => {
    expect(isTransientGrpcError(new Error("plain error"))).toBe(false);
    expect(isTransientGrpcError("string error")).toBe(false);
    expect(isTransientGrpcError(null)).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// handleGrpcCall
// ---------------------------------------------------------------------------

describe("handleGrpcCall", () => {
  it("success → re-materializes D2Result from proto envelope", async () => {
    const expected = notFound<string>({ traceId: "t1" });
    const protoValue = d2ResultToProto(expected);

    const fakeResponse = { result: protoValue, data: undefined };
    const result = await handleGrpcCall(
      () => Promise.resolve(fakeResponse),
      (r) => r.result,
      (r) => r.data,
    );

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(result.category).toBe(ErrorCategoryWire.NotFound);
    expect(result.traceId).toBe("t1");
  });

  it("success with data → data selector stitched into result", async () => {
    const items = [{ id: "a" }, { id: "b" }];
    const protoValue = d2ResultToProto(someFound());
    const fakeResponse = { result: protoValue, items };

    const result = await handleGrpcCall<typeof fakeResponse, typeof items>(
      () => Promise.resolve(fakeResponse),
      (r) => r.result,
      (r) => r.items,
    );
    expect(result.data).toStrictEqual(items);
    expect(result.statusCode).toBe(HttpStatusCode.PartialContent);
  });

  // long test description — cannot wrap
  it("ServiceError (UNAVAILABLE) → serviceUnavailable, message is TK constant (NOT err.message)", async () => {
    const sentinel = "SECRET_BROKER_URI://user:password@host/vhost";
    const err = makeServiceError(14, sentinel, sentinel); // UNAVAILABLE

    const result = await handleGrpcCall(
      () => Promise.reject(err),
      (r: never) => r,
      (r: never) => r,
    );

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);

    // Regression guard: the sentinel must not appear in any message
    const allMessageKeys = result.messages.map((m) => m.key).join("|");
    expect(allMessageKeys).not.toContain(sentinel);
    expect(allMessageKeys).not.toContain("SECRET");
    // The message MUST be the TK constant
    expect(result.messages[0].key).toBe(
      TK.common.errors.SERVICE_UNAVAILABLE.key,
    );
  });

  it("ServiceError (DEADLINE_EXCEEDED) → serviceUnavailable with TK constant", async () => {
    const sentinel = "grpc connect timeout at secret-host:50051";
    const err = makeServiceError(4, sentinel, sentinel);

    const result = await handleGrpcCall(
      () => Promise.reject(err),
      (r: never) => r,
      (r: never) => r,
    );
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    const keys = result.messages.map((m) => m.key).join("|");
    expect(keys).not.toContain("timeout");
    expect(keys).not.toContain("secret");
  });

  // long test description — cannot wrap
  it("ServiceError (CANCELLED = 1) → canceled result with TK constant, sentinel absent", async () => {
    const sentinel = "SECRET_INTERNAL_HOST://user:password@broker:5672/vhost";
    const err = makeServiceError(1, sentinel, sentinel);
    const result = await handleGrpcCall(
      () => Promise.reject(err),
      (r: never) => r,
      (r: never) => r,
    );
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.category).toBe(ErrorCategoryWire.ValidationFailure);

    // Sentinel MUST NOT appear in any message — raw transport strings never reach the client
    const allMessageKeys = result.messages.map((m) => m.key).join("|");
    expect(allMessageKeys).not.toContain(sentinel);
    expect(allMessageKeys).not.toContain("SECRET");
    // The message MUST be the TK constant
    expect(result.messages[0].key).toBe(TK.common.errors.CANCELED.key);
  });

  it("ServiceError (UNAUTHENTICATED = 16) → unauthorized result", async () => {
    const err = makeServiceError(16, "unauthenticated", "unauthenticated");
    const result = await handleGrpcCall(
      () => Promise.reject(err),
      (r: never) => r,
      (r: never) => r,
    );
    expect(result.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(result.messages[0].key).toBe(TK.common.errors.UNAUTHORIZED.key);
  });

  it("non-ServiceError → unhandledException with TK constant (NOT err.message)", async () => {
    const sentinel = "internal-error: db-pass=top-secret-pw-123";
    const plainError = new Error(sentinel);

    const result = await handleGrpcCall(
      () => Promise.reject(plainError),
      (r: never) => r,
      (r: never) => r,
    );

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
    const keys = result.messages.map((m) => m.key).join("|");
    expect(keys).not.toContain("internal-error");
    expect(keys).not.toContain("top-secret");
    expect(result.messages[0].key).toBe(TK.common.errors.UNKNOWN.key);
  });

  it("traceId threads through from envelope", async () => {
    const source = notFound({ traceId: "trace-99" });
    const proto = d2ResultToProto(source);
    const result = await handleGrpcCall(
      () => Promise.resolve({ result: proto }),
      (r) => r.result,
      () => undefined,
    );
    expect(result.traceId).toBe("trace-99");
  });

  // Transport-fault results must carry the caller-supplied traceId.
  // Mirrors .NET HandleAsyncTests.RpcException_TraceId_AppearsInFailResult.
  // long test description — cannot wrap
  it("transport fault (UNAVAILABLE) with passed traceId → result.traceId equals the passed value", async () => {
    const trace = "abc123";
    const err = makeServiceError(14, "unavailable", "unavailable"); // UNAVAILABLE
    const result = await handleGrpcCall(
      () => Promise.reject(err),
      (r: never) => r,
      (r: never) => r,
      trace,
    );
    expect(result.success).toBe(false);
    expect(result.traceId).toBe(trace);
  });

  // long test description — cannot wrap
  it("transport fault (CANCELLED) with passed traceId → result.traceId equals the passed value", async () => {
    const trace = "cancel-trace-42";
    const err = makeServiceError(1, "canceled", "canceled"); // CANCELLED
    const result = await handleGrpcCall(
      () => Promise.reject(err),
      (r: never) => r,
      (r: never) => r,
      trace,
    );
    expect(result.success).toBe(false);
    expect(result.traceId).toBe(trace);
  });

  // long test description — cannot wrap
  it("transport fault (UNAUTHENTICATED) with passed traceId → result.traceId equals the passed value", async () => {
    const trace = "unauth-trace-7";
    const err = makeServiceError(16, "unauthenticated", "unauthenticated"); // UNAUTHENTICATED
    const result = await handleGrpcCall(
      () => Promise.reject(err),
      (r: never) => r,
      (r: never) => r,
      trace,
    );
    expect(result.success).toBe(false);
    expect(result.traceId).toBe(trace);
  });

  // long test description — cannot wrap
  it("non-ServiceError with passed traceId → result.traceId equals the passed value", async () => {
    const trace = "unhandled-trace-99";
    const plainError = new Error("unexpected failure");
    const result = await handleGrpcCall(
      () => Promise.reject(plainError),
      (r: never) => r,
      (r: never) => r,
      trace,
    );
    expect(result.success).toBe(false);
    expect(result.traceId).toBe(trace);
  });
});

// ---------------------------------------------------------------------------
// unaryCall
// ---------------------------------------------------------------------------

describe("unaryCall", () => {
  it("resolves when callback called without error", async () => {
    const response = { result: { success: true } };
    const method = (
      _req: unknown,
      cb: (err: null, res: typeof response) => void,
    ) => {
      cb(null, response);
      return {} as ReturnType<typeof unaryCall>;
    };

    const result = await unaryCall(
      method as unknown as Parameters<typeof unaryCall>[0],
      {},
    );
    expect(result).toStrictEqual(response);
  });

  it("rejects when callback called with error", async () => {
    const err = makeServiceError(14, "unavailable", "service down");
    const method = (
      _req: unknown,
      cb: (err: ServiceError, res: never) => void,
    ) => {
      cb(err, null as never);
      return {} as ReturnType<typeof unaryCall>;
    };

    await expect(
      unaryCall(method as unknown as Parameters<typeof unaryCall>[0], {}),
    ).rejects.toThrow();
  });

  it("deadlineMs option passes a deadline CallOptions", async () => {
    let receivedOptions: unknown;
    const method = (
      _req: unknown,
      _meta: unknown,
      opts: unknown,
      cb: (err: null, res: { done: true }) => void,
    ) => {
      receivedOptions = opts;
      cb(null, { done: true });
      return {} as ReturnType<typeof unaryCall>;
    };

    await unaryCall(
      method as unknown as Parameters<typeof unaryCall>[0],
      {},
      { deadlineMs: 5000 },
    );
    expect(
      (receivedOptions as Record<string, unknown>)["deadline"],
    ).toBeDefined();
  });
});
