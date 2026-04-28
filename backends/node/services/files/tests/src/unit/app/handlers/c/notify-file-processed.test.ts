import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { NotifyFileProcessed } from "@d2/files-app";
import type { NotifyFileProcessedInput } from "@d2/files-app";
import { createMockCallOnFileProcessed, createMockContext } from "../../helpers/mock-handlers.js";

function createHandler(
  overrides: {
    callOnFileProcessed?: ReturnType<typeof createMockCallOnFileProcessed>;
  } = {},
) {
  const callOnFileProcessed = overrides.callOnFileProcessed ?? createMockCallOnFileProcessed();
  const context = createMockContext();

  return {
    handler: new NotifyFileProcessed(callOnFileProcessed, context),
    callOnFileProcessed,
  };
}

const validInput: NotifyFileProcessedInput = {
  address: "auth:5101",
  fileId: "file-001",
  contextKey: "user_avatar",
  relatedEntityId: "user-123",
  status: "ready",
};

describe("NotifyFileProcessed", () => {
  // --- Happy path ---

  it("should call gRPC callback with correct input and return success", async () => {
    const { handler, callOnFileProcessed } = createHandler();

    const result = await handler.handleAsync(validInput);

    expect(result.success).toBe(true);
    expect(result.data?.success).toBe(true);
    expect(callOnFileProcessed.handleAsync).toHaveBeenCalledWith({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
      variants: undefined,
    });
  });

  it("should pass variants when provided", async () => {
    const { handler, callOnFileProcessed } = createHandler();
    const variants = ["original", "thumb", "medium"];

    const result = await handler.handleAsync({ ...validInput, variants });

    expect(result.success).toBe(true);
    expect(callOnFileProcessed.handleAsync).toHaveBeenCalledWith(
      expect.objectContaining({ variants }),
    );
  });

  it("should omit variants when not provided", async () => {
    const { handler, callOnFileProcessed } = createHandler();

    await handler.handleAsync(validInput);

    const call = vi.mocked(callOnFileProcessed.handleAsync).mock.calls[0]![0];
    expect(call.variants).toBeUndefined();
  });

  it("should succeed with status rejected", async () => {
    const { handler } = createHandler();

    const result = await handler.handleAsync({ ...validInput, status: "rejected" });

    expect(result.success).toBe(true);
    expect(result.data?.success).toBe(true);
  });

  // --- Error propagation ---

  it("should propagate gRPC callback failure via bubbleFail", async () => {
    const callOnFileProcessed = createMockCallOnFileProcessed();
    vi.mocked(callOnFileProcessed.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler({ callOnFileProcessed });

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

  it("should reject empty fileId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ ...validInput, fileId: "" });
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

  it("should reject invalid status value", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      status: "invalid" as "ready",
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

  it("should not call gRPC callback on validation failure", async () => {
    const { handler, callOnFileProcessed } = createHandler();
    await handler.handleAsync({ ...validInput, fileId: "" });
    expect(callOnFileProcessed.handleAsync).not.toHaveBeenCalled();
  });
});
