import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// Mock @d2/logging before importing the module under test
vi.mock("@d2/logging", () => ({
  createLogger: vi.fn(() => ({
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
    child: vi.fn(),
  })),
}));

// Mock @d2/auth-bff-client
vi.mock("@d2/auth-bff-client", () => ({
  SessionResolver: vi.fn(),
  JwtManager: vi.fn(),
  AuthProxy: vi.fn(),
}));

// Mock @d2/result (used by middleware.mock.server for stub handlers)
vi.mock("@d2/result", () => ({
  D2Result: { ok: vi.fn(() => ({ success: true })) },
}));

describe("getAuthContext", () => {
  const ENV_BACKUP: Record<string, string | undefined> = {};

  function setEnv(key: string, value: string) {
    ENV_BACKUP[key] = process.env[key];
    process.env[key] = value;
  }

  function clearEnv(key: string) {
    ENV_BACKUP[key] = process.env[key];
    delete process.env[key];
  }

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

  describe("production mode (no mock flags)", () => {
    it("throws when SVELTEKIT_AUTH__URL is missing", async () => {
      clearMockFlags();
      const { getAuthContext } = await import("./auth.server.js");
      expect(() => getAuthContext()).toThrow("FATAL");
      expect(() => getAuthContext()).toThrow("SVELTEKIT_AUTH__URL");
    });
  });

  describe("mock mode (D2_MOCK_INFRA / CI)", () => {
    it("returns mock context when D2_MOCK_INFRA=true and env vars are missing", async () => {
      setEnv("D2_MOCK_INFRA", "true");

      const { getAuthContext } = await import("./auth.server.js");
      const ctx = getAuthContext();

      expect(ctx).not.toBeNull();
      expect(ctx!.logger).toBeDefined();
      expect(ctx!.sessionResolver).toBeDefined();
      expect(ctx!.jwtManager).toBeDefined();
      expect(ctx!.authProxy).toBeDefined();
    });

    it("returns mock context when CI=true and env vars are missing", async () => {
      setEnv("CI", "true");

      const { getAuthContext } = await import("./auth.server.js");
      const ctx = getAuthContext();

      expect(ctx).not.toBeNull();
      expect(ctx!.logger).toBeDefined();
    });

    it("caches mock context on subsequent calls", async () => {
      setEnv("D2_MOCK_INFRA", "true");

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      const { getAuthContext } = await import("./auth.server.js");

      getAuthContext();
      getAuthContext();
      getAuthContext();

      // Warning logged only once (first call caches).
      expect(warnSpy).toHaveBeenCalledTimes(1);
      warnSpy.mockRestore();
    });

    it("uses mock context even when env vars ARE present with mock flag", async () => {
      setEnv("D2_MOCK_INFRA", "true");
      setEnv("SVELTEKIT_AUTH__URL", "http://localhost:5100");

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      const { getAuthContext } = await import("./auth.server.js");
      const ctx = getAuthContext();

      expect(ctx).not.toBeNull();
      // Mock flag overrides — warn is called (mock path), not real init.
      expect(warnSpy).toHaveBeenCalledWith(expect.stringContaining("Auth context mocked"));
      warnSpy.mockRestore();
    });
  });

  it("returns AuthContext when SVELTEKIT_AUTH__URL is set", async () => {
    clearMockFlags();
    setEnv("SVELTEKIT_AUTH__URL", "http://localhost:5100");

    const { getAuthContext } = await import("./auth.server.js");
    const ctx = getAuthContext();

    expect(ctx).not.toBeNull();
    expect(ctx!.config.authServiceUrl).toBe("http://localhost:5100");
    expect(ctx!.sessionResolver).toBeDefined();
    expect(ctx!.jwtManager).toBeDefined();
    expect(ctx!.authProxy).toBeDefined();
    expect(ctx!.logger).toBeDefined();
  });

  it("passes apiKey from SVELTEKIT_AUTH__API_KEY to config", async () => {
    clearMockFlags();
    setEnv("SVELTEKIT_AUTH__URL", "http://localhost:5100");
    setEnv("SVELTEKIT_AUTH__API_KEY", "test-api-key");

    const { getAuthContext } = await import("./auth.server.js");
    const ctx = getAuthContext();

    expect(ctx!.config.apiKey).toBe("test-api-key");
  });

  it("returns same instance on subsequent calls (singleton)", async () => {
    clearMockFlags();
    setEnv("SVELTEKIT_AUTH__URL", "http://localhost:5100");

    const { getAuthContext } = await import("./auth.server.js");
    const first = getAuthContext();
    const second = getAuthContext();

    expect(first).toBe(second);
  });
});
