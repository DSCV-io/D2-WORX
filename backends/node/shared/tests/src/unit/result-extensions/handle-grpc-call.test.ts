import { describe, it, expect } from "vitest";
import { HttpStatusCode, ErrorCodes } from "@d2/result";
import { D2ResultProtoFns, type D2ResultProto } from "@d2/protos";
import { handleGrpcCall } from "@d2/result-extensions";

// ---------------------------------------------------------------------------
// Test types
// ---------------------------------------------------------------------------

interface MockGrpcResponse {
  result: D2ResultProto;
  data: { id: number; name: string } | undefined;
}

function createSuccessResponse(data: { id: number; name: string } | undefined): MockGrpcResponse {
  return {
    result: D2ResultProtoFns.create({
      success: true,
      statusCode: HttpStatusCode.OK,
    }),
    data,
  };
}

function createFailureResponse(
  statusCode: number,
  errorCode: string,
  messages: string[],
): MockGrpcResponse {
  return {
    result: D2ResultProtoFns.create({
      success: false,
      statusCode,
      errorCode,
      messages,
    }),
    data: undefined,
  };
}

/**
 * Create a fake ServiceError (Error with a numeric `code` property).
 * Mirrors @grpc/grpc-js ServiceError shape.
 */
function createServiceError(code: number, message: string): Error {
  const err = new Error(message);
  (err as unknown as Record<string, unknown>)["code"] = code;
  (err as unknown as Record<string, unknown>)["details"] = message;
  (err as unknown as Record<string, unknown>)["metadata"] = {};
  return err;
}

// ---------------------------------------------------------------------------
// Successful gRPC calls
// ---------------------------------------------------------------------------

describe("handleGrpcCall — success", () => {
  it("converts a successful gRPC response to D2Result", async () => {
    const data = { id: 42, name: "test" };
    const response = createSuccessResponse(data);

    const result = await handleGrpcCall(
      () => Promise.resolve(response),
      (r) => r.result,
      (r) => r.data,
    );

    expect(result).toBeSuccess();
    expect(result).toHaveStatusCode(HttpStatusCode.OK);
    expect(result).toHaveData(data);
  });

  it("handles undefined data from response", async () => {
    const response = createSuccessResponse(undefined);

    const result = await handleGrpcCall(
      () => Promise.resolve(response),
      (r) => r.result,
      (r) => r.data,
    );

    expect(result).toBeSuccess();
    expect(result.data).toBeUndefined();
  });

  it("converts a failure gRPC response (non-transport error)", async () => {
    const response = createFailureResponse(HttpStatusCode.NotFound, ErrorCodes.NOT_FOUND, [
      "Resource not found.",
    ]);

    const result = await handleGrpcCall(
      () => Promise.resolve(response),
      (r) => r.result,
      (r) => r.data,
    );

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.NotFound);
    expect(result).toHaveErrorCode(ErrorCodes.NOT_FOUND);
    expect(result).toHaveMessages(["Resource not found."]);
  });
});

// ---------------------------------------------------------------------------
// gRPC transport errors
// ---------------------------------------------------------------------------

describe("handleGrpcCall — transport errors", () => {
  it("returns SERVICE_UNAVAILABLE for ServiceError", async () => {
    const serviceError = createServiceError(14, "UNAVAILABLE: Connection refused");

    const result = await handleGrpcCall<MockGrpcResponse, { id: number; name: string }>(
      () => Promise.reject(serviceError),
      (r) => r.result,
      (r) => r.data,
    );

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.ServiceUnavailable);
    expect(result).toHaveErrorCode(ErrorCodes.SERVICE_UNAVAILABLE);
    expect(result.messages).toEqual(
      expect.arrayContaining([expect.stringContaining("gRPC call failed with status 14")]),
    );
  });

  it("returns unhandledException for generic Error", async () => {
    const genericError = new Error("Something unexpected");

    const result = await handleGrpcCall<MockGrpcResponse, { id: number; name: string }>(
      () => Promise.reject(genericError),
      (r) => r.result,
      (r) => r.data,
    );

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.InternalServerError);
    expect(result).toHaveErrorCode(ErrorCodes.UNHANDLED_EXCEPTION);
  });

  it("returns unhandledException for Error with non-numeric code (e.g. Node.js ECONNREFUSED)", async () => {
    const nodeError = new Error("connect ECONNREFUSED");
    (nodeError as unknown as Record<string, unknown>)["code"] = "ECONNREFUSED";

    const result = await handleGrpcCall<MockGrpcResponse, { id: number; name: string }>(
      () => Promise.reject(nodeError),
      (r) => r.result,
      (r) => r.data,
    );

    // Has "code" but it's a string, not a number — should NOT match isServiceError
    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.InternalServerError);
    expect(result).toHaveErrorCode(ErrorCodes.UNHANDLED_EXCEPTION);
  });

  it("returns unhandledException for non-Error throw", async () => {
    const result = await handleGrpcCall<MockGrpcResponse, { id: number; name: string }>(
      () => Promise.reject("string error"),
      (r) => r.result,
      (r) => r.data,
    );

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.InternalServerError);
    expect(result).toHaveErrorCode(ErrorCodes.UNHANDLED_EXCEPTION);
  });
});

// ---------------------------------------------------------------------------
// Selector verification
// ---------------------------------------------------------------------------

describe("handleGrpcCall — selectors", () => {
  it("uses resultSelector to extract proto from response", async () => {
    const proto = D2ResultProtoFns.create({
      success: true,
      statusCode: HttpStatusCode.Created,
      traceId: "trace-sel",
    });
    const response = { nested: { result: proto }, payload: "data" };

    const result = await handleGrpcCall(
      () => Promise.resolve(response),
      (r) => r.nested.result,
      () => undefined,
    );

    expect(result).toBeSuccess();
    expect(result).toHaveStatusCode(HttpStatusCode.Created);
    expect(result.traceId).toBe("trace-sel");
  });

  it("uses dataSelector to extract data from response", async () => {
    const proto = D2ResultProtoFns.create({
      success: true,
      statusCode: HttpStatusCode.OK,
    });
    const response = { result: proto, items: [1, 2, 3] };

    const result = await handleGrpcCall(
      () => Promise.resolve(response),
      (r) => r.result,
      (r) => r.items,
    );

    expect(result).toBeSuccess();
    expect(result).toHaveData([1, 2, 3]);
  });
});
