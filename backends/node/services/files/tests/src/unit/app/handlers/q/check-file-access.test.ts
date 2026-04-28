import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { CheckFileAccess } from "@d2/files-app";
import type { CheckFileAccessInput } from "@d2/files-app";
import { createMockCallCanAccess, createMockContext } from "../../helpers/mock-handlers.js";

function createHandler(
  overrides: {
    callCanAccess?: ReturnType<typeof createMockCallCanAccess>;
  } = {},
) {
  const callCanAccess = overrides.callCanAccess ?? createMockCallCanAccess(true);
  const context = createMockContext();

  return {
    handler: new CheckFileAccess(callCanAccess, context),
    callCanAccess,
  };
}

const validInput: CheckFileAccessInput = {
  address: "comms:3200",
  contextKey: "thread_attachment",
  relatedEntityId: "thread-789",
  requestingUserId: "user-123",
  requestingOrgId: "org-456",
  action: "read",
};

describe("CheckFileAccess", () => {
  // --- Happy path ---

  it("should return allowed: true when gRPC responds with allowed: true", async () => {
    const { handler, callCanAccess } = createHandler();

    const result = await handler.handleAsync(validInput);

    expect(result.success).toBe(true);
    expect(result.data?.allowed).toBe(true);
    expect(callCanAccess.handleAsync).toHaveBeenCalledWith({
      address: "comms:3200",
      contextKey: "thread_attachment",
      relatedEntityId: "thread-789",
      requestingUserId: "user-123",
      requestingOrgId: "org-456",
      action: "read",
    });
  });

  it("should return allowed: false when gRPC responds with allowed: false", async () => {
    const callCanAccess = createMockCallCanAccess(false);
    const { handler } = createHandler({ callCanAccess });

    const result = await handler.handleAsync(validInput);

    expect(result.success).toBe(true);
    expect(result.data?.allowed).toBe(false);
  });

  it("should work with upload action", async () => {
    const { handler } = createHandler();

    const result = await handler.handleAsync({ ...validInput, action: "upload" });

    expect(result.success).toBe(true);
    expect(result.data?.allowed).toBe(true);
  });

  // --- Fail-closed behavior ---

  it("should return allowed: false when response data is missing", async () => {
    const callCanAccess = createMockCallCanAccess(true);
    vi.mocked(callCanAccess.handleAsync).mockResolvedValue(D2Result.ok({ data: undefined }));
    const { handler } = createHandler({ callCanAccess });

    const result = await handler.handleAsync(validInput);

    expect(result.success).toBe(true);
    expect(result.data?.allowed).toBe(false);
  });

  // --- Error propagation ---

  it("should propagate gRPC callback failure via bubbleFail", async () => {
    const callCanAccess = createMockCallCanAccess();
    vi.mocked(callCanAccess.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler({ callCanAccess });

    const result = await handler.handleAsync(validInput);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });

  // --- Validation errors ---

  it("should reject empty address", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ ...validInput, address: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should reject empty contextKey", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ ...validInput, contextKey: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should reject empty relatedEntityId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ ...validInput, relatedEntityId: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should reject invalid action value", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      action: "delete" as "read",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should reject address exceeding max length", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ ...validInput, address: "x".repeat(256) });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should reject empty requestingUserId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ ...validInput, requestingUserId: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should reject empty requestingOrgId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ ...validInput, requestingOrgId: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should not call gRPC callback on validation failure", async () => {
    const { handler, callCanAccess } = createHandler();
    await handler.handleAsync({ ...validInput, contextKey: "" });
    expect(callCanAccess.handleAsync).not.toHaveBeenCalled();
  });
});
