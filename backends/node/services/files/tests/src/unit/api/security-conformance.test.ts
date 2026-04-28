import { describe, it, expect, vi } from "vitest";
import { Hono } from "hono";
import { ServiceCollection } from "@d2/di";
import { D2Result } from "@d2/result";
import { buildGrpcServer, type GrpcServerOptions } from "@d2/files-api";
import {
  IListFilesKey,
  IUploadFileKey,
  type ContextKeyConfig,
  type ContextKeyConfigMap,
} from "@d2/files-app";
import { isInfrastructurePath } from "@d2/request-enrichment";
import { createListRoutes } from "../../../../api/src/routes/list-routes.js";
import { createUploadRoutes } from "../../../../api/src/routes/upload-routes.js";
import { SCOPE_KEY, REQUEST_CONTEXT_KEY } from "../../../../api/src/context-keys.js";

/**
 * §5 Security conformance tests for the files-api public surface.
 *
 * Files exposes:
 *   - HTTP (Hono) — public-facing upload/download/list. Auth via JWT.
 *   - gRPC — S2S only. Auth via X-Api-Key header.
 *
 * IDOR posture: the route layer is intentionally a thin pass-through.
 * `ResolveFileAccess` is the single chokepoint enforcing
 * `request.userId === relatedEntityId` (etc.) per the contextKey's
 * `readResolution`. Equality / mismatch coverage lives in
 * `resolve-file-access.test.ts` — adding a route-level guard would
 * duplicate that policy AND violate the dependency-inversion intent of the
 * generic `?contextKey=` shape (Files would have to know about every
 * consumer). The conformance assertions here therefore cover transport
 * concerns only: fail-closed gRPC config, infrastructure path bypass, and
 * pagination clamping.
 *
 * Adding a new public route or middleware to files-api requires extending
 * this file rather than tracking the verification as TODOs in PROFILE_PROGRESS.
 */

function makeMinimalGrpcOptions(overrides: Partial<GrpcServerOptions> = {}): GrpcServerOptions {
  return {
    provider: {} as any,
    grpcPort: 0,
    filesApiKeys: [],
    logger: {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {},
      fatal: () => {},
    } as any,
    ...overrides,
  };
}

/**
 * Mounts createListRoutes against a stub scope so the pagination clamp can be
 * exercised without spinning up the full gateway. The IDOR equality logic
 * runs inside ResolveFileAccess (covered separately) — the mock just
 * captures the input that the route forwards.
 */
function buildListRoutesHarness(): {
  app: Hono;
  listFilesMock: ReturnType<typeof vi.fn>;
} {
  const listFilesMock = vi.fn().mockResolvedValue(D2Result.ok({ data: { items: [], total: 0 } }));

  const services = new ServiceCollection();
  services.addInstance(IListFilesKey, { handleAsync: listFilesMock });
  const provider = services.build();

  const app = new Hono();
  app.use("*", async (c, next) => {
    (c as any).set(SCOPE_KEY, provider.createScope());
    await next();
  });
  app.route("/", createListRoutes());
  return { app, listFilesMock };
}

/**
 * Mounts createUploadRoutes against a stub scope + request context so the
 * threadId UUID validation can be exercised without spinning up the full
 * gateway. The mock just captures the input UploadFile would receive.
 */
function buildUploadRoutesHarness(): {
  app: Hono;
  uploadFileMock: ReturnType<typeof vi.fn>;
} {
  const uploadFileMock = vi
    .fn()
    .mockResolvedValue(D2Result.ok({ data: { fileId: "f1", presignedUrl: "https://stub" } }));

  const services = new ServiceCollection();
  services.addInstance(IUploadFileKey, { handleAsync: uploadFileMock });
  const provider = services.build();

  const threadAttachmentConfig: ContextKeyConfig = {
    contextKey: "thread_attachment",
    uploadResolution: "callback",
    readResolution: "authenticated",
    listResolution: "callback",
    callbackAddress: "comms:3200",
    allowedCategories: ["image"],
    maxSizeBytes: 1024 * 1024,
    variants: [{ name: "original" }],
  };
  const configs: ContextKeyConfigMap = new Map([
    [threadAttachmentConfig.contextKey, threadAttachmentConfig],
  ]);

  const app = new Hono();
  app.use("*", async (c, next) => {
    (c as any).set(SCOPE_KEY, provider.createScope());
    (c as any).set(REQUEST_CONTEXT_KEY, {
      isAuthenticated: true,
      isTrustedService: false,
      isOrgEmulating: false,
      isUserImpersonating: false,
      isAgentStaff: false,
      isAgentAdmin: false,
      isTargetingStaff: false,
      isTargetingAdmin: false,
      userId: "user-uploader",
      traceId: "trace-test",
    });
    await next();
  });
  app.route("/", createUploadRoutes(configs));
  return { app, uploadFileMock };
}

describe("§5 Security — files-api gRPC conformance", () => {
  describe('"Auth middleware must fail-closed on missing config"', () => {
    it("refuses to build the gRPC server when filesApiKeys is empty", async () => {
      await expect(buildGrpcServer(makeMinimalGrpcOptions({ filesApiKeys: [] }))).rejects.toThrow(
        /FILES_API_KEYS not configured/,
      );
    });

    it("refuses to build the gRPC server when filesApiKeys is undefined", async () => {
      await expect(
        buildGrpcServer(makeMinimalGrpcOptions({ filesApiKeys: undefined as any })),
      ).rejects.toThrow(/FILES_API_KEYS not configured/);
    });
  });
});

describe("§5 Security — files-api HTTP conformance", () => {
  describe('"Infrastructure paths must be exempt from ALL business middleware"', () => {
    // Files-api mounts request-enrichment + rate-limit at "*". Both consult
    // isInfrastructurePath at their entry to short-circuit the bypass — so
    // probe paths neither trigger WhoIs lookups nor consume rate-limit
    // budget. Matches .NET InfrastructurePaths.IsInfrastructure parity.
    it.each(["/health", "/ready", "/api/health", "/alive", "/metrics"])(
      "%s is recognized as infrastructure (skips business middleware)",
      (path) => {
        expect(isInfrastructurePath(path)).toBe(true);
      },
    );
  });

  describe('"Pagination limits — default 50, max 100"', () => {
    it("clamps limit to 100 when caller requests higher", async () => {
      const { app, listFilesMock } = buildListRoutesHarness();

      await app.request("/files?contextKey=user_avatar&relatedEntityId=user-1&limit=999");

      expect(listFilesMock).toHaveBeenCalledWith(expect.objectContaining({ limit: 100 }));
    });

    it("defaults limit to 50 when no value is supplied", async () => {
      const { app, listFilesMock } = buildListRoutesHarness();

      await app.request("/files?contextKey=user_avatar&relatedEntityId=user-1");

      expect(listFilesMock).toHaveBeenCalledWith(expect.objectContaining({ limit: 50 }));
    });
  });

  describe('"threadId path param must validate as a UUID before reaching UploadFile" (B2)', () => {
    // Regression: threadId came directly from the URL path with no validation.
    // Without a route-level UUID check, malformed values flow into both the
    // S3 key path (where they become a directory segment) and into the gRPC
    // payload (`relatedEntityId`) sent to Comms's CanAccess. The Zod check
    // at the route boundary blocks malformed values before either trust
    // boundary is crossed.
    const VALID_UUID = "01934a4f-1c2b-7c3d-8e1a-1234567890ab";

    it("rejects non-UUID threadId with 400 and never invokes UploadFile", async () => {
      const { app, uploadFileMock } = buildUploadRoutesHarness();

      const res = await app.request("/threads/not-a-uuid/attachments", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contentType: "image/png",
          displayName: "x.png",
          sizeBytes: 1024,
        }),
      });

      expect(res.status).toBe(400);
      expect(uploadFileMock).not.toHaveBeenCalled();
    });

    it("rejects path-traversal-shaped threadId with 400", async () => {
      const { app, uploadFileMock } = buildUploadRoutesHarness();

      // Hono's router blocks `/` in path segments outright (404), so we
      // exercise the next-most-likely junk shape: a wildcard.
      const res = await app.request("/threads/*/attachments", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contentType: "image/png",
          displayName: "x.png",
          sizeBytes: 1024,
        }),
      });

      expect(res.status).toBe(400);
      expect(uploadFileMock).not.toHaveBeenCalled();
    });

    it("accepts a valid UUID and forwards it as relatedEntityId", async () => {
      const { app, uploadFileMock } = buildUploadRoutesHarness();

      const res = await app.request(`/threads/${VALID_UUID}/attachments`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contentType: "image/png",
          displayName: "x.png",
          sizeBytes: 1024,
        }),
      });

      expect(res.status).toBe(200);
      expect(uploadFileMock).toHaveBeenCalledWith(
        expect.objectContaining({
          contextKey: "thread_attachment",
          relatedEntityId: VALID_UUID,
        }),
      );
    });
  });
});
