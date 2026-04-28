import { describe, it, expect, vi, beforeEach } from "vitest";
import type { IHandlerContext, IRequestContext } from "@d2/handler";

// --- Hoisted mocks ---

const { mockSend, mockGetSignedUrl } = vi.hoisted(() => ({
  mockSend: vi.fn(),
  mockGetSignedUrl: vi.fn(),
}));

vi.mock("@aws-sdk/client-s3", () => ({
  S3Client: vi.fn(),
  PutObjectCommand: vi.fn(),
  GetObjectCommand: vi.fn(),
  HeadObjectCommand: vi.fn(),
  DeleteObjectCommand: vi.fn(),
  DeleteObjectsCommand: vi.fn(),
  ListBucketsCommand: vi.fn(),
}));

vi.mock("@aws-sdk/s3-request-presigner", () => ({
  getSignedUrl: mockGetSignedUrl,
}));

// --- Imports (after mocks) ---

import {
  PutStorageObject,
  GetStorageObject,
  HeadStorageObject,
  DeleteStorageObject,
  DeleteStorageObjects,
  PresignPutUrl,
  PingStorage,
  DEFAULT_FILES_STORAGE_OPTIONS,
} from "@d2/files-infra";

// --- Helpers ---

const TEST_BUCKET = "test-bucket";

function createTestContext(): IHandlerContext {
  const request: IRequestContext = {
    isAuthenticated: true,
    isOrgEmulating: null,
    isUserImpersonating: null,
    isTrustedService: null,
    userId: "user-123",
    targetOrgId: "org-456",
  };
  return {
    request,
    logger: {
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      debug: vi.fn(),
      trace: vi.fn(),
      fatal: vi.fn(),
      child: vi.fn().mockReturnThis(),
    },
  } as unknown as IHandlerContext;
}

function createMockS3() {
  return { send: mockSend } as never;
}

// --- Tests ---

describe("PutStorageObject", () => {
  let handler: PutStorageObject;

  beforeEach(() => {
    handler = new PutStorageObject(createMockS3(), TEST_BUCKET, createTestContext());
  });

  it("should upload buffer to S3 and return ok", async () => {
    mockSend.mockResolvedValueOnce({});

    const result = await handler.handleAsync({
      key: "files/user-123/avatar.jpg",
      buffer: Buffer.from("image-data"),
      contentType: "image/jpeg",
    });

    expect(result).toBeSuccess();
    expect(mockSend).toHaveBeenCalledTimes(1);
  });

  it("should return serviceUnavailable on S3 error", async () => {
    mockSend.mockRejectedValueOnce(new Error("network error"));

    const result = await handler.handleAsync({
      key: "files/fail.jpg",
      buffer: Buffer.from("x"),
      contentType: "image/jpeg",
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should handle empty buffer", async () => {
    mockSend.mockResolvedValueOnce({});

    const result = await handler.handleAsync({
      key: "files/empty",
      buffer: Buffer.alloc(0),
      contentType: "application/octet-stream",
    });

    expect(result).toBeSuccess();
  });

  it("should call S3 send exactly once per upload", async () => {
    mockSend.mockResolvedValueOnce({});

    await handler.handleAsync({
      key: "files/one-call.jpg",
      buffer: Buffer.from("data"),
      contentType: "image/jpeg",
    });

    expect(mockSend).toHaveBeenCalledTimes(1);
  });
});

describe("GetStorageObject", () => {
  let handler: GetStorageObject;

  beforeEach(() => {
    handler = new GetStorageObject(createMockS3(), TEST_BUCKET, createTestContext());
  });

  it("should return buffer on success", async () => {
    const bodyBytes = new Uint8Array([1, 2, 3]);
    mockSend.mockResolvedValueOnce({
      Body: { transformToByteArray: () => Promise.resolve(bodyBytes) },
    });

    const result = await handler.handleAsync({ key: "files/doc.pdf" });

    expect(result).toBeSuccess();
    expect(result.data?.buffer).toEqual(Buffer.from(bodyBytes));
  });

  it("should return notFound when Body is undefined", async () => {
    mockSend.mockResolvedValueOnce({ Body: undefined });

    const result = await handler.handleAsync({ key: "files/missing.jpg" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(404);
  });

  it("should return notFound for NoSuchKey error", async () => {
    const err = new Error("NoSuchKey");
    err.name = "NoSuchKey";
    mockSend.mockRejectedValueOnce(err);

    const result = await handler.handleAsync({ key: "files/nope.txt" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(404);
  });

  it("should return notFound for NotFound error", async () => {
    const err = new Error("NotFound");
    err.name = "NotFound";
    mockSend.mockRejectedValueOnce(err);

    const result = await handler.handleAsync({ key: "files/gone.txt" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(404);
  });

  it("should return notFound for 404 error name", async () => {
    const err = new Error("404");
    err.name = "404";
    mockSend.mockRejectedValueOnce(err);

    const result = await handler.handleAsync({ key: "files/404.txt" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(404);
  });

  it("should return serviceUnavailable for non-NotFound S3 errors", async () => {
    mockSend.mockRejectedValueOnce(new Error("InternalError"));

    const result = await handler.handleAsync({ key: "files/broken.txt" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should return serviceUnavailable for non-Error thrown values", async () => {
    mockSend.mockRejectedValueOnce("some string error");

    const result = await handler.handleAsync({ key: "files/weird.txt" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });
});

describe("HeadStorageObject", () => {
  let handler: HeadStorageObject;

  beforeEach(() => {
    handler = new HeadStorageObject(createMockS3(), TEST_BUCKET, createTestContext());
  });

  it("should return exists=true with metadata when object exists", async () => {
    mockSend.mockResolvedValueOnce({
      ContentType: "image/jpeg",
      ContentLength: 4096,
    });

    const result = await handler.handleAsync({ key: "files/avatar.jpg" });

    expect(result).toBeSuccess();
    expect(result.data?.exists).toBe(true);
    expect(result.data?.contentType).toBe("image/jpeg");
    expect(result.data?.sizeBytes).toBe(4096);
  });

  it("should return exists=false for NotFound error", async () => {
    const err = new Error("NotFound");
    err.name = "NotFound";
    mockSend.mockRejectedValueOnce(err);

    const result = await handler.handleAsync({ key: "files/missing.jpg" });

    expect(result).toBeSuccess();
    expect(result.data?.exists).toBe(false);
  });

  it("should return exists=false for NoSuchKey error", async () => {
    const err = new Error("NoSuchKey");
    err.name = "NoSuchKey";
    mockSend.mockRejectedValueOnce(err);

    const result = await handler.handleAsync({ key: "files/gone.jpg" });

    expect(result).toBeSuccess();
    expect(result.data?.exists).toBe(false);
  });

  it("should return exists=false for 404 error name", async () => {
    const err = new Error("404");
    err.name = "404";
    mockSend.mockRejectedValueOnce(err);

    const result = await handler.handleAsync({ key: "files/nope.jpg" });

    expect(result).toBeSuccess();
    expect(result.data?.exists).toBe(false);
  });

  it("should return serviceUnavailable for non-NotFound errors", async () => {
    mockSend.mockRejectedValueOnce(new Error("AccessDenied"));

    const result = await handler.handleAsync({ key: "files/forbidden.jpg" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should handle missing ContentType and ContentLength gracefully", async () => {
    mockSend.mockResolvedValueOnce({});

    const result = await handler.handleAsync({ key: "files/no-meta.bin" });

    expect(result).toBeSuccess();
    expect(result.data?.exists).toBe(true);
    expect(result.data?.contentType).toBeUndefined();
    expect(result.data?.sizeBytes).toBeUndefined();
  });
});

describe("DeleteStorageObject", () => {
  let handler: DeleteStorageObject;

  beforeEach(() => {
    handler = new DeleteStorageObject(createMockS3(), TEST_BUCKET, createTestContext());
  });

  it("should delete object and return ok", async () => {
    mockSend.mockResolvedValueOnce({});

    const result = await handler.handleAsync({ key: "files/old-avatar.jpg" });

    expect(result).toBeSuccess();
    expect(mockSend).toHaveBeenCalledTimes(1);
  });

  it("should return serviceUnavailable on S3 error", async () => {
    mockSend.mockRejectedValueOnce(new Error("S3 delete failed"));

    const result = await handler.handleAsync({ key: "files/fail-delete.jpg" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });
});

describe("DeleteStorageObjects", () => {
  let handler: DeleteStorageObjects;

  beforeEach(() => {
    handler = new DeleteStorageObjects(createMockS3(), TEST_BUCKET, createTestContext());
  });

  it("should delete multiple objects and return ok", async () => {
    mockSend.mockResolvedValueOnce({});

    const result = await handler.handleAsync({
      keys: ["files/a.jpg", "files/b.jpg", "files/c.jpg"],
    });

    expect(result).toBeSuccess();
    expect(mockSend).toHaveBeenCalledTimes(1);
  });

  it("should return ok immediately for empty keys array without calling S3", async () => {
    const result = await handler.handleAsync({ keys: [] });

    expect(result).toBeSuccess();
    expect(mockSend).not.toHaveBeenCalled();
  });

  it("should return serviceUnavailable on S3 error", async () => {
    mockSend.mockRejectedValueOnce(new Error("Batch delete failed"));

    const result = await handler.handleAsync({ keys: ["files/x.jpg"] });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should handle single key in array", async () => {
    mockSend.mockResolvedValueOnce({});

    const result = await handler.handleAsync({ keys: ["files/single.jpg"] });

    expect(result).toBeSuccess();
    expect(mockSend).toHaveBeenCalledTimes(1);
  });
});

describe("PresignPutUrl", () => {
  let handler: PresignPutUrl;

  beforeEach(() => {
    handler = new PresignPutUrl(
      createMockS3(),
      TEST_BUCKET,
      DEFAULT_FILES_STORAGE_OPTIONS,
      createTestContext(),
    );
  });

  it("should return presigned URL on success", async () => {
    mockGetSignedUrl.mockResolvedValueOnce("https://minio.local/presigned?sig=abc");

    const result = await handler.handleAsync({
      key: "files/upload.jpg",
      contentType: "image/jpeg",
      maxSizeBytes: 5 * 1024 * 1024,
    });

    expect(result).toBeSuccess();
    expect(result.data?.url).toBe("https://minio.local/presigned?sig=abc");
  });

  it("should pass correct expiry to getSignedUrl", async () => {
    mockGetSignedUrl.mockResolvedValueOnce("https://example.com/signed");

    await handler.handleAsync({
      key: "files/test.pdf",
      contentType: "application/pdf",
      maxSizeBytes: 10_000_000,
    });

    expect(mockGetSignedUrl).toHaveBeenCalledWith(
      expect.anything(), // S3Client
      expect.anything(), // PutObjectCommand instance
      { expiresIn: 900 }, // DEFAULT_EXPIRY_SECONDS = 15 min
    );
  });

  it("should return serviceUnavailable when getSignedUrl throws", async () => {
    mockGetSignedUrl.mockRejectedValueOnce(new Error("presign failed"));

    const result = await handler.handleAsync({
      key: "files/fail.jpg",
      contentType: "image/jpeg",
      maxSizeBytes: 1024,
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });
});

describe("PingStorage", () => {
  let handler: PingStorage;

  beforeEach(() => {
    handler = new PingStorage(createMockS3(), createTestContext());
  });

  it("should return healthy=true with latency when S3 is reachable", async () => {
    mockSend.mockResolvedValueOnce({ Buckets: [] });

    const result = await handler.handleAsync({});

    expect(result).toBeSuccess();
    expect(result.data?.healthy).toBe(true);
    expect(result.data?.latencyMs).toBeGreaterThanOrEqual(0);
    expect(result.data?.error).toBeUndefined();
  });

  it("should return healthy=false with error message on S3 failure", async () => {
    mockSend.mockRejectedValueOnce(new Error("connection refused"));

    const result = await handler.handleAsync({});

    expect(result).toBeSuccess(); // PingStorage always returns ok
    expect(result.data?.healthy).toBe(false);
    expect(result.data?.latencyMs).toBeGreaterThanOrEqual(0);
    expect(result.data?.error).toBe("connection refused");
  });

  it("should convert non-Error thrown values to string", async () => {
    mockSend.mockRejectedValueOnce(42);

    const result = await handler.handleAsync({});

    expect(result).toBeSuccess();
    expect(result.data?.healthy).toBe(false);
    expect(result.data?.error).toBe("42");
  });

  it("should never return a failure result (health check always ok)", async () => {
    mockSend.mockRejectedValueOnce(new Error("timeout"));

    const result = await handler.handleAsync({});

    // PingStorage wraps errors in ok() with healthy=false — never serviceUnavailable
    expect(result).toBeSuccess();
    expect(result.data?.healthy).toBe(false);
  });
});
