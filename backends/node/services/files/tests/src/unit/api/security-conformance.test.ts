import { describe, it, expect, vi } from "vitest";
import { Hono } from "hono";
import { ServiceCollection } from "@d2/di";
import { D2Result } from "@d2/result";
import { buildGrpcServer, type GrpcServerOptions } from "@d2/files-api";
import { IListFilesKey } from "@d2/files-app";
import { isInfrastructurePath } from "@d2/request-enrichment";
import { createListRoutes } from "../../../../api/src/routes/list-routes.js";
import { SCOPE_KEY } from "../../../../api/src/context-keys.js";

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
});
