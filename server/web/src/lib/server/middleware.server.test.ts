import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// Helper: creates a constructable mock class.
function mockClass(props: Record<string, unknown> = {}) {
  return class {
    constructor() {
      Object.assign(this, { handleAsync: vi.fn(), ...props });
    }
  };
}

// Mock all @d2/* modules before importing the module under test.
vi.mock("ioredis", () => ({
  default: class Redis {},
}));

vi.mock("@d2/logging", () => ({
  createLogger: vi.fn().mockReturnValue({
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
  }),
}));

vi.mock("@d2/handler", () => ({
  HandlerContext: class HandlerContext {
    constructor(
      public request: unknown,
      public logger: unknown,
    ) {}
  },
}));

vi.mock("@d2/service-defaults/config", () => ({
  parseRedisUrl: vi.fn().mockReturnValue("redis://:pass@localhost:6379"),
  parseEnvArray: vi.fn().mockReturnValue([]),
}));

vi.mock("@d2/cache-redis", () => ({
  Get: mockClass(),
  Set: mockClass(),
  SetNx: mockClass(),
  Remove: mockClass(),
  Exists: mockClass(),
  GetTtl: mockClass(),
  Increment: mockClass(),
}));

vi.mock("@d2/cache-memory", () => ({
  MemoryCacheStore: class MemoryCacheStore {},
}));

vi.mock("@d2/utilities", () => ({
  Singleflight: class Singleflight {},
}));

vi.mock("@d2/geo-client", () => ({
  createGeoServiceClient: vi.fn().mockReturnValue({}),
  createGeoCircuitBreaker: vi.fn().mockReturnValue({}),
  FindWhoIs: mockClass(),
  Get: mockClass(),
  GetFromMem: mockClass(),
  GetFromDist: mockClass(),
  GetFromDisk: mockClass(),
  ReqUpdate: mockClass(),
  SetInMem: mockClass(),
  SetOnDisk: mockClass(),
  GeoRefDataSerializer: class GeoRefDataSerializer {},
  DEFAULT_GEO_CLIENT_OPTIONS: { allowedContextKeys: [], apiKey: "" },
}));

vi.mock("@d2/ratelimit", () => ({
  CheckRateLimit: mockClass(),
}));

vi.mock("@d2/idempotency", () => ({
  CheckIdempotency: mockClass(),
  checkIdempotency: vi.fn(),
}));

vi.mock("@d2/protos", () => ({}));

vi.mock("@d2/request-enrichment", () => ({
  enrichRequest: vi.fn(),
}));

describe("middleware.server", () => {
  const ENV_BACKUP: Record<string, string | undefined> = {};

  function setEnv(key: string, value: string) {
    ENV_BACKUP[key] = process.env[key];
    process.env[key] = value;
  }

  function clearEnv(key: string) {
    ENV_BACKUP[key] = process.env[key];
    delete process.env[key];
  }

  function setRequiredEnv() {
    setEnv("REDIS_URL", "redis://:pass@localhost:6379");
    setEnv("GEO_GRPC_ADDRESS", "localhost:5138");
    setEnv("SVELTEKIT_GEO_CLIENT__APIKEY", "test-key");
  }

  /** Ensure mock flags are cleared so MOCK_INFRA is false at module load. */
  function clearMockFlags() {
    clearEnv("D2_MOCK_INFRA");
    clearEnv("CI");
  }

  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    for (const [key, value] of Object.entries(ENV_BACKUP)) {
      if (value === undefined) {
        delete process.env[key];
      } else {
        process.env[key] = value;
      }
    }
    Object.keys(ENV_BACKUP).forEach((k) => delete ENV_BACKUP[k]);
  });

  describe("production mode (no CI/skip flags)", () => {
    it("throws when Redis connection string is missing", async () => {
      clearMockFlags();
      setEnv("GEO_GRPC_ADDRESS", "localhost:5138");
      setEnv("SVELTEKIT_GEO_CLIENT__APIKEY", "test-key");

      const { getMiddlewareContext } = await import("./middleware.server");

      expect(() => getMiddlewareContext()).toThrow("FATAL");
      expect(() => getMiddlewareContext()).toThrow("REDIS_URL");
    });

    it("throws when GEO_GRPC_ADDRESS is missing", async () => {
      clearMockFlags();
      setEnv("REDIS_URL", "redis://:pass@localhost:6379");
      setEnv("SVELTEKIT_GEO_CLIENT__APIKEY", "test-key");

      const { getMiddlewareContext } = await import("./middleware.server");

      expect(() => getMiddlewareContext()).toThrow("FATAL");
      expect(() => getMiddlewareContext()).toThrow("GEO_GRPC_ADDRESS");
    });

    it("throws when SVELTEKIT_GEO_CLIENT__APIKEY is missing", async () => {
      clearMockFlags();
      setEnv("REDIS_URL", "redis://:pass@localhost:6379");
      setEnv("GEO_GRPC_ADDRESS", "localhost:5138");

      const { getMiddlewareContext } = await import("./middleware.server");

      expect(() => getMiddlewareContext()).toThrow("FATAL");
      expect(() => getMiddlewareContext()).toThrow("SVELTEKIT_GEO_CLIENT__APIKEY");
    });

    it("throws every time when env vars are missing (no cached null)", async () => {
      clearMockFlags();
      const { getMiddlewareContext } = await import("./middleware.server");

      expect(() => getMiddlewareContext()).toThrow("FATAL");
      expect(() => getMiddlewareContext()).toThrow("FATAL");
      expect(() => getMiddlewareContext()).toThrow("FATAL");
    });
  });

  describe("mock mode (D2_MOCK_INFRA / CI)", () => {
    it("returns mock context when D2_MOCK_INFRA=true and env vars are missing", async () => {
      setEnv("D2_MOCK_INFRA", "true");

      const { getMiddlewareContext } = await import("./middleware.server");
      const ctx = getMiddlewareContext();

      expect(ctx).not.toBeNull();
      expect(ctx!.logger).toBeDefined();
      expect(ctx!.findWhoIs).toBeDefined();
      expect(ctx!.rateLimitCheck).toBeDefined();
      expect(ctx!.getGeoRefData).toBeDefined();
    });

    it("returns mock context when CI=true and env vars are missing", async () => {
      setEnv("CI", "true");

      const { getMiddlewareContext } = await import("./middleware.server");
      const ctx = getMiddlewareContext();

      expect(ctx).not.toBeNull();
      expect(ctx!.logger).toBeDefined();
    });

    it("caches mock context on subsequent calls", async () => {
      setEnv("D2_MOCK_INFRA", "true");

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      const { getMiddlewareContext } = await import("./middleware.server");

      getMiddlewareContext();
      getMiddlewareContext();
      getMiddlewareContext();

      // Warning logged only once (first call caches).
      expect(warnSpy).toHaveBeenCalledTimes(1);
      warnSpy.mockRestore();
    });

    it("uses mock context even when env vars ARE present with mock flag", async () => {
      setEnv("D2_MOCK_INFRA", "true");
      setRequiredEnv();

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      const { getMiddlewareContext } = await import("./middleware.server");
      const ctx = getMiddlewareContext();

      expect(ctx).not.toBeNull();
      expect(ctx!.logger).toBeDefined();
      // Mock flag overrides — warn is called (mock path), not real init.
      expect(warnSpy).toHaveBeenCalledWith(expect.stringContaining("Infrastructure mocked"));
      warnSpy.mockRestore();
    });
  });

  it("returns context when all env vars are present", async () => {
    clearMockFlags();
    setRequiredEnv();

    const { getMiddlewareContext } = await import("./middleware.server");
    const ctx = getMiddlewareContext();

    expect(ctx).not.toBeNull();
    expect(ctx!.logger).toBeDefined();
    expect(ctx!.findWhoIs).toBeDefined();
    expect(ctx!.rateLimitCheck).toBeDefined();
    expect(ctx!.idempotencyCheck).toBeDefined();
    expect(ctx!.redisSet).toBeDefined();
    expect(ctx!.redisRemove).toBeDefined();
    expect(ctx!.getGeoRefData).toBeDefined();
  });

  it("returns cached singleton on subsequent calls", async () => {
    clearMockFlags();
    setRequiredEnv();

    const { getMiddlewareContext } = await import("./middleware.server");
    const ctx1 = getMiddlewareContext();
    const ctx2 = getMiddlewareContext();

    expect(ctx1).toBe(ctx2);
  });
});
