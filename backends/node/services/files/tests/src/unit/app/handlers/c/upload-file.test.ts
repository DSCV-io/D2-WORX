import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { UploadFile } from "@d2/files-app";
import {
  createMockRepo,
  createMockStorage,
  createMockContext,
  createMockResolveFileAccess,
} from "../../helpers/mock-handlers.js";
import { TEST_CONFIG_MAP, TEST_USER_AVATAR_CONFIG } from "../../helpers/test-config.js";

function createHandler(
  overrides: {
    repo?: ReturnType<typeof createMockRepo>;
    storage?: ReturnType<typeof createMockStorage>;
    resolveAccess?: ReturnType<typeof createMockResolveFileAccess>;
    contextOverrides?: Parameters<typeof createMockContext>[0];
  } = {},
) {
  const repo = overrides.repo ?? createMockRepo();
  const storage = overrides.storage ?? createMockStorage();
  const context = createMockContext(overrides.contextOverrides);
  const resolveAccess = overrides.resolveAccess ?? createMockResolveFileAccess();
  return {
    handler: new UploadFile(repo, storage, TEST_CONFIG_MAP, context, resolveAccess),
    repo,
    storage,
    context,
    resolveAccess,
  };
}

const validInput = {
  contextKey: "user_avatar",
  relatedEntityId: "user-123",
  contentType: "image/jpeg",
  displayName: "avatar.jpg",
  sizeBytes: 1024,
};

describe("UploadFile", () => {
  it("should return presigned URL on success", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync(validInput);
    expect(result.success).toBe(true);
    expect(result.data?.fileId).toBeTruthy();
    expect(result.data?.presignedUrl).toBe("https://minio.local/presigned");
  });

  it("should persist file record via repo", async () => {
    const repo = createMockRepo();
    const { handler } = createHandler({ repo });
    await handler.handleAsync(validInput);
    expect(repo.create.handleAsync).toHaveBeenCalledTimes(1);
  });

  it("should call presignPut handler with correct parameters", async () => {
    const storage = createMockStorage();
    const { handler } = createHandler({ storage });
    await handler.handleAsync(validInput);
    expect(storage.presignPut.handleAsync).toHaveBeenCalledWith({
      key: expect.stringContaining("user_avatar/user-123/"),
      contentType: "image/jpeg",
      maxSizeBytes: TEST_USER_AVATAR_CONFIG.maxSizeBytes,
    });
  });

  it("should return forbidden for unknown context key", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      contextKey: "unknown_key",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should return forbidden when access is denied", async () => {
    const { handler } = createHandler({
      resolveAccess: createMockResolveFileAccess(false),
    });
    const result = await handler.handleAsync(validInput);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should return 400 for disallowed content type", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      contentType: "application/pdf",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should reject SVG uploads in the image category (XSS via presigned GET — see B1)", async () => {
    // Regression: `image/svg+xml` was previously in ALLOWED_CONTENT_TYPES.image,
    // letting attackers upload SVG-with-script as avatar/org-logo. Presigned
    // MinIO GET URLs serve the bytes with `Content-Type: image/svg+xml` and
    // no `attachment` disposition, so the script executes in the storage
    // origin. Drop SVG outright — no current product use case justifies the
    // exposure.
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      contextKey: "user_avatar",
      contentType: "image/svg+xml",
      displayName: "evil.svg",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    expect(result.errorCode).toBe("FILES_CONTENT_TYPE_NOT_ALLOWED");
  });

  it("should return 413 when size exceeds config limit", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      sizeBytes: TEST_USER_AVATAR_CONFIG.maxSizeBytes + 1,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(413);
  });

  it("should return validation error for empty displayName", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      displayName: "",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should return validation error for zero sizeBytes", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      sizeBytes: 0,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should return validation error for empty contextKey", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      ...validInput,
      contextKey: "",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should delegate to resolveAccess with correct input", async () => {
    const resolveAccess = createMockResolveFileAccess();
    const { handler } = createHandler({ resolveAccess });
    await handler.handleAsync(validInput);
    expect(resolveAccess.handleAsync).toHaveBeenCalledWith({
      config: TEST_USER_AVATAR_CONFIG,
      action: "upload",
      relatedEntityId: "user-123",
    });
  });

  it("should propagate repo failure", async () => {
    const repo = createMockRepo();
    vi.mocked(repo.create.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler({ repo });
    const result = await handler.handleAsync(validInput);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });
});
