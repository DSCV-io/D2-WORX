import { describe, it, expect } from "vitest";
import { buildGrpcServer, type CommsGrpcServerOptions } from "@d2/comms-api";

/**
 * §5 Security conformance tests for the comms-api public surface.
 *
 * Comms exposes:
 *   - gRPC — S2S only. Auth via X-Api-Key header. Uniquely supports an
 *     `allowUnauthenticated: true` opt-in for local dev (with a warn log).
 *     Fail-closed by default; explicit escape hatch is the contract.
 *   - RabbitMQ consumer — internal only, not a public surface.
 *
 * Adding a new public route, a new RPC, or a new escape hatch requires
 * extending this file rather than tracking the verification as TODOs in
 * PROFILE_PROGRESS.
 */

function makeMinimalGrpcOptions(
  overrides: Partial<CommsGrpcServerOptions> = {},
): CommsGrpcServerOptions {
  return {
    provider: {} as any,
    grpcPort: 0,
    commsApiKeys: [],
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

describe("§5 Security — comms-api gRPC conformance", () => {
  describe('"Auth middleware must fail-closed on missing config"', () => {
    it("refuses to build the gRPC server when commsApiKeys is empty AND allowUnauthenticated is not set", async () => {
      // Default posture: missing config = refuse to start. Matches the
      // sibling files-api / auth-api gRPC servers.
      await expect(buildGrpcServer(makeMinimalGrpcOptions({ commsApiKeys: [] }))).rejects.toThrow(
        /COMMS_API_KEYS not configured/,
      );
    });

    it("refuses to build the gRPC server when commsApiKeys is undefined AND allowUnauthenticated is not set", async () => {
      await expect(
        buildGrpcServer(makeMinimalGrpcOptions({ commsApiKeys: undefined as any })),
      ).rejects.toThrow(/COMMS_API_KEYS not configured/);
    });

    it("starts WITHOUT api-key middleware when commsApiKeys empty AND allowUnauthenticated explicitly true", async () => {
      // Local-dev escape hatch — explicit opt-in only. The contract is that
      // the operator KNOWS they're running open, hence the loud warn log
      // (verified by the inner gRPC setup, not this test). The test pins
      // the SHAPE of the contract: the option must be present AND truthy
      // for the bypass to engage. Future refactors that flip the default
      // or accept the bypass without the explicit flag will fail here.
      const server = await buildGrpcServer(
        makeMinimalGrpcOptions({ commsApiKeys: [], allowUnauthenticated: true }),
      );
      expect(server).toBeDefined();
      // The Node gRPC server has no obvious "is auth wired?" introspection
      // API, so the contract is "doesn't throw + caller had to opt in
      // explicitly." Stronger assertion (e.g., interceptor list shape) would
      // require digging into grpc-js internals — not worth the coupling.
      try {
        server.forceShutdown();
      } catch {
        // ignore — server was never bound
      }
    });
  });
});
