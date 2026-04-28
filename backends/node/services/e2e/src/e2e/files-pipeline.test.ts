import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import {
  startContainers,
  stopContainers,
  getAuthPgUrl,
  getFilesPgUrl,
  getGeoPgUrl,
  getRedisUrl,
  getRabbitUrl,
  getFilesPool,
  getAuthPool,
} from "../helpers/containers.js";
import { startGeoService, stopGeoService } from "../helpers/geo-dotnet-service.js";
import {
  startAuthService,
  stopAuthService,
  type AuthServiceHandle,
} from "../helpers/auth-service.js";
import { startAuthHttpServer, type AuthHttpServer } from "../helpers/auth-http-server.js";
import {
  startFilesService,
  stopFilesService,
  type FilesServiceHandle,
} from "../helpers/files-service.js";
import {
  startMinIO,
  stopMinIO,
  getMinioEndpoint,
  getBucketName,
} from "../helpers/minio-container.js";
import {
  startClamAV,
  stopClamAV,
  getClamdHost,
  getClamdPort,
} from "../helpers/clamav-container.js";
import {
  startStubSignalR,
  stopStubSignalR,
  getStubSignalRAddress,
  getCapturedPushEvents,
  clearCapturedPushEvents,
} from "../helpers/stub-signalr-gateway.js";
import { waitFor, waitForRow } from "../helpers/wait.js";
import { FILES_MESSAGING } from "@d2/files-domain";

const GEO_API_KEY = "e2e-test-key";
const AUTH_GRPC_PORT = 5199;
const AUTH_API_KEY = "e2e-files-callback-key";

describe("E2E: Files pipeline", () => {
  let geoAddress: string;
  let authHandle: AuthServiceHandle;
  let authHttpServer: AuthHttpServer;
  let filesHandle: FilesServiceHandle;

  // Shared state for download tests (set by full pipeline test)
  let readyFileId: string;
  let uploaderJwt: string;

  // --- Setup ---

  beforeAll(async () => {
    // Phase 1: Infrastructure (parallel)
    await Promise.all([startContainers(), startMinIO(), startClamAV(), startStubSignalR()]);

    // Phase 2: Geo service (needs containers)
    geoAddress = await startGeoService({
      pgUrl: getGeoPgUrl(),
      redisUrl: getRedisUrl(),
      rabbitUrl: getRabbitUrl(),
      apiKey: GEO_API_KEY,
    });

    // Phase 3: Auth service with gRPC enabled (needs Geo)
    authHandle = await startAuthService({
      databaseUrl: getAuthPgUrl(),
      redisUrl: getRedisUrl(),
      rabbitMqUrl: getRabbitUrl(),
      geoAddress,
      geoApiKey: GEO_API_KEY,
      grpcPort: AUTH_GRPC_PORT,
      authApiKeys: [AUTH_API_KEY],
    });

    // Phase 4: Auth HTTP server (for JWKS endpoint)
    authHttpServer = await startAuthHttpServer(authHandle.app);

    // Phase 5: Files service (needs everything)
    filesHandle = await startFilesService({
      databaseUrl: getFilesPgUrl(),
      redisUrl: getRedisUrl(),
      rabbitMqUrl: getRabbitUrl(),
      s3Endpoint: getMinioEndpoint(),
      s3AccessKey: "minioadmin",
      s3SecretKey: "minioadmin",
      s3BucketName: getBucketName(),
      clamdHost: getClamdHost(),
      clamdPort: getClamdPort(),
      jwksUrl: `${authHttpServer.baseUrl}/api/auth/jwks`,
      signalrGatewayAddress: getStubSignalRAddress(),
      callbackApiKey: AUTH_API_KEY,
      authGrpcAddress: `localhost:${AUTH_GRPC_PORT}`,
    });
  }, 180_000);

  afterAll(async () => {
    await stopFilesService();
    authHttpServer?.close();
    await stopAuthService();
    await stopGeoService();
    await stopClamAV();
    await stopMinIO();
    await stopStubSignalR();
    await stopContainers();
  });

  beforeEach(() => {
    clearCapturedPushEvents();
  });

  // --- Helpers ---

  /**
   * Signs up a user, verifies email, signs in, and obtains a JWT.
   */
  async function signUpAndGetJwt(
    email: string,
    name: string,
  ): Promise<{ userId: string; jwt: string }> {
    const password = "SecurePass123!@#";

    const signUpRes = await authHandle.auth.api.signUpEmail({
      body: { email, password, name },
    });
    expect(signUpRes.user).toBeDefined();
    const userId = signUpRes.user.id;

    // Verify email (bypass notification)
    await getAuthPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [userId]);

    // Sign in to get session token
    const signInRes = await authHandle.auth.api.signInEmail({
      body: { email, password },
    });
    const sessionToken = signInRes.token as string;
    expect(sessionToken).toBeDefined();

    // Get JWT from session
    const tokenRes = await authHandle.auth.api.getToken({
      headers: new Headers({ Authorization: `Bearer ${sessionToken}` }),
    });
    expect(tokenRes.token).toBeDefined();

    return { userId, jwt: tokenRes.token };
  }

  /**
   * Calls POST /api/v1/avatar on the Files HTTP server.
   */
  async function uploadAvatar(
    jwt: string,
    body: { contentType: string; displayName: string; sizeBytes: number },
  ) {
    const res = await fetch(`${filesHandle.baseUrl}/api/v1/avatar`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${jwt}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    });
    return { status: res.status, data: (await res.json()) as Record<string, unknown> };
  }

  /**
   * PUTs raw bytes to a presigned MinIO URL.
   */
  async function putToPresignedUrl(
    url: string,
    buffer: Buffer,
    contentType: string,
  ): Promise<number> {
    const res = await fetch(url, {
      method: "PUT",
      headers: { "Content-Type": contentType },
      body: new Uint8Array(buffer),
    });
    return res.status;
  }

  /**
   * Publishes { fileId } to the intake queue (bypasses MinIO notifications).
   */
  async function triggerIntake(fileId: string): Promise<void> {
    await filesHandle.intakePublisher.send(
      { exchange: FILES_MESSAGING.EVENTS_EXCHANGE, routingKey: FILES_MESSAGING.UPLOAD_ROUTING_KEY },
      { fileId },
    );
  }

  // --- Tests ---

  it(
    "should complete full avatar upload pipeline: upload → scan → variants → callback → push",
    { timeout: 60_000 },
    async () => {
      const { userId, jwt } = await signUpAndGetJwt("files-avatar@example.com", "Files Avatar");

      // Save for download tests
      uploaderJwt = jwt;

      // 1. Upload request → get presigned URL
      const uploadRes = await uploadAvatar(jwt, {
        contentType: "image/gif",
        displayName: "my-avatar.gif",
        sizeBytes: 1024, // Declared >= actual (35 bytes) — OK
      });

      expect(uploadRes.status).toBeLessThan(300);
      expect(uploadRes.data.success).toBe(true);

      const resultData = uploadRes.data.data as Record<string, unknown>;
      const fileId = resultData.fileId as string;
      const presignedUrl = resultData.presignedUrl as string;
      expect(fileId).toBeDefined();
      expect(presignedUrl).toContain(getBucketName());

      // Save for download tests
      readyFileId = fileId;

      // 2. Upload actual file to MinIO via presigned URL (1x1 GIF)
      const gifBytes = createMinimalGif();
      const putStatus = await putToPresignedUrl(presignedUrl, gifBytes, "image/gif");
      expect(putStatus).toBe(200);

      // 3. Trigger intake (simulates MinIO notification)
      await triggerIntake(fileId);

      // 4. Wait for file to reach "ready" status in DB
      const readyRow = await waitForRow<{ status: string; variants: unknown }>(
        getFilesPool(),
        "SELECT status, variants FROM file WHERE id = $1 AND status = 'ready'",
        [fileId],
        { timeout: 30_000, label: "file ready status" },
      );

      expect(readyRow.status).toBe("ready");
      expect(readyRow.variants).toBeDefined();

      const variants = readyRow.variants as Array<{ size: string; contentType: string }>;
      expect(variants.length).toBeGreaterThanOrEqual(1);

      // Should have both "thumb" (resized → WebP) and "original" (pass-through → original type)
      const thumbVariant = variants.find((v) => v.size === "thumb");
      const originalVariant = variants.find((v) => v.size === "original");
      expect(thumbVariant).toBeDefined();
      expect(originalVariant).toBeDefined();
      expect(thumbVariant!.contentType).toBe("image/webp");
      expect(originalVariant!.contentType).toBe("image/gif"); // Original keeps source format

      // 5. Verify Auth callback — user.image should be set
      await waitFor(
        async () => {
          const userRow = await getAuthPool().query('SELECT image FROM "user" WHERE id = $1', [
            userId,
          ]);
          return userRow.rows.length > 0 && userRow.rows[0].image !== null;
        },
        { timeout: 15_000, label: "user.image set by callback" },
      );

      const userRow = await getAuthPool().query('SELECT image FROM "user" WHERE id = $1', [userId]);
      expect(userRow.rows[0].image).toBeDefined();

      // 6. Verify SignalR push event was captured
      await waitFor(async () => getCapturedPushEvents().length > 0, {
        timeout: 10_000,
        label: "SignalR push event",
      });

      const pushEvents = getCapturedPushEvents();
      const fileReadyEvent = pushEvents.find((e) => e.event === "file:ready");
      expect(fileReadyEvent).toBeDefined();
      expect(fileReadyEvent!.channel).toBe(`user:${userId}`);

      const payload = JSON.parse(fileReadyEvent!.payloadJson);
      expect(payload.fileId).toBe(fileId);
      expect(payload.contextKey).toBe("user_avatar");
      expect(payload.status).toBe("ready");
    },
  );

  it("should reject upload with invalid content type", async () => {
    const { jwt } = await signUpAndGetJwt("files-badtype@example.com", "Bad Type");

    const uploadRes = await uploadAvatar(jwt, {
      contentType: "application/exe",
      displayName: "virus.exe",
      sizeBytes: 1024,
    });

    // user_avatar only allows "image" category — application/exe is not image
    expect(uploadRes.status).toBeGreaterThanOrEqual(400);
    expect(uploadRes.data.success).toBe(false);
  });

  it("should reject upload exceeding max size", async () => {
    const { jwt } = await signUpAndGetJwt("files-oversize@example.com", "Over Size");

    const uploadRes = await uploadAvatar(jwt, {
      contentType: "image/gif",
      displayName: "huge.gif",
      sizeBytes: 999_999_999, // Exceeds 5 MB limit for user_avatar
    });

    expect(uploadRes.status).toBe(413);
    expect(uploadRes.data.success).toBe(false);
  });

  it("should reject file when actual upload exceeds declared size", async () => {
    const { jwt } = await signUpAndGetJwt("files-sizemismatch@example.com", "Size Mismatch");

    // Declare small size (100 bytes)
    const uploadRes = await uploadAvatar(jwt, {
      contentType: "image/gif",
      displayName: "tiny-but-not.gif",
      sizeBytes: 100,
    });

    expect(uploadRes.status).toBeLessThan(300);
    const resultData = uploadRes.data.data as Record<string, unknown>;
    const fileId = resultData.fileId as string;
    const presignedUrl = resultData.presignedUrl as string;

    // Upload much larger file (10 KB) — exceeds declared 100 bytes
    const bigBuffer = Buffer.alloc(10_240, 0xff);
    await putToPresignedUrl(presignedUrl, bigBuffer, "image/gif");

    // Trigger intake — handler will compare actual vs declared size
    await triggerIntake(fileId);

    // Wait for file to be rejected with size_mismatch
    const rejectedRow = await waitForRow<{ status: string; rejection_reason: string }>(
      getFilesPool(),
      "SELECT status, rejection_reason FROM file WHERE id = $1 AND status = 'rejected'",
      [fileId],
      { timeout: 15_000, label: "file rejected (size_mismatch)" },
    );

    expect(rejectedRow.status).toBe("rejected");
    expect(rejectedRow.rejection_reason).toBe("size_mismatch");
  });

  it("should detect EICAR test virus and reject file", { timeout: 60_000 }, async () => {
    const { jwt } = await signUpAndGetJwt("files-eicar@example.com", "EICAR Test");

    // EICAR test string is 68 bytes
    const eicar = Buffer.from(
      "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*",
      "ascii",
    );

    const uploadRes = await uploadAvatar(jwt, {
      contentType: "image/gif",
      displayName: "eicar.gif",
      sizeBytes: eicar.length,
    });

    expect(uploadRes.status).toBeLessThan(300);
    const resultData = uploadRes.data.data as Record<string, unknown>;
    const fileId = resultData.fileId as string;
    const presignedUrl = resultData.presignedUrl as string;

    // Upload EICAR test string (industry-standard AV test)
    await putToPresignedUrl(presignedUrl, eicar, "image/gif");

    // Trigger intake → processing → ClamAV scan → rejection
    await triggerIntake(fileId);

    // Wait for file to be rejected by ClamAV
    const rejectedRow = await waitForRow<{ status: string; rejection_reason: string }>(
      getFilesPool(),
      "SELECT status, rejection_reason FROM file WHERE id = $1 AND status = 'rejected'",
      [fileId],
      { timeout: 30_000, label: "file rejected (virus)" },
    );

    expect(rejectedRow.status).toBe("rejected");
    expect(rejectedRow.rejection_reason).toBeDefined();
  });

  it("should reject unauthenticated upload with 401", async () => {
    const res = await fetch(`${filesHandle.baseUrl}/api/v1/avatar`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        contentType: "image/gif",
        displayName: "no-auth.gif",
        sizeBytes: 1024,
      }),
    });

    expect(res.status).toBe(401);
  });

  it("should download a ready file's variants", async () => {
    // Uses the file from the full pipeline test (must run first)
    expect(readyFileId).toBeDefined();
    expect(uploaderJwt).toBeDefined();

    // Download original variant (uploader has jwt_owner access)
    const originalRes = await fetch(`${filesHandle.baseUrl}/api/v1/files/${readyFileId}/original`, {
      method: "GET",
      headers: { Authorization: `Bearer ${uploaderJwt}` },
    });

    expect(originalRes.status).toBe(200);
    expect(originalRes.headers.get("content-type")).toBe("image/gif"); // Original keeps source format
    expect(originalRes.headers.get("cache-control")).toContain("immutable");
    const originalBody = await originalRes.arrayBuffer();
    expect(originalBody.byteLength).toBeGreaterThan(0);

    // Download thumb variant (resized → WebP)
    const thumbRes = await fetch(`${filesHandle.baseUrl}/api/v1/files/${readyFileId}/thumb`, {
      method: "GET",
      headers: { Authorization: `Bearer ${uploaderJwt}` },
    });

    expect(thumbRes.status).toBe(200);
    expect(thumbRes.headers.get("content-type")).toBe("image/webp");
    const thumbBody = await thumbRes.arrayBuffer();
    expect(thumbBody.byteLength).toBeGreaterThan(0);
  });

  it("should return 404 when downloading a file that is not ready", async () => {
    const { jwt } = await signUpAndGetJwt("files-notready@example.com", "Not Ready");

    // Upload but don't trigger intake (file stays "pending")
    const uploadRes = await uploadAvatar(jwt, {
      contentType: "image/gif",
      displayName: "pending.gif",
      sizeBytes: 512,
    });
    expect(uploadRes.status).toBeLessThan(300);
    const fileId = (uploadRes.data.data as Record<string, unknown>).fileId as string;

    // Try to download — should fail because file is not "ready"
    const res = await fetch(`${filesHandle.baseUrl}/api/v1/files/${fileId}/original`, {
      method: "GET",
      headers: { Authorization: `Bearer ${jwt}` },
    });

    // File exists but is not ready → 404 (variant not found)
    expect(res.status).toBe(404);
  });
});

/**
 * Creates a minimal valid GIF89a image (1x1 white pixel, 35 bytes).
 * Sharp can process this and convert to WebP for variant generation.
 */
function createMinimalGif(): Buffer {
  return Buffer.from(
    "474946383961" + // GIF89a signature
      "0100" + // width: 1
      "0100" + // height: 1
      "80" + // GCT flag, 1-bit color resolution, GCT size 2
      "00" + // background color index
      "00" + // pixel aspect ratio
      "ffffff" + // GCT color 0: white
      "000000" + // GCT color 1: black
      "2c" + // image descriptor
      "00000000" + // left, top: 0, 0
      "0100" + // width: 1
      "0100" + // height: 1
      "00" + // no local color table
      "02" + // LZW minimum code size
      "02" + // sub-block: 2 bytes
      "4c01" + // LZW compressed data (single white pixel)
      "00" + // sub-block terminator
      "3b", // GIF trailer
    "hex",
  );
}
