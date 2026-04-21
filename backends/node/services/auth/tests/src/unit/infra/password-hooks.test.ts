import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { createHash } from "node:crypto";
import { checkBreachedPassword, createPasswordFunctions } from "@d2/auth-infra";
import { PASSWORD_POLICY } from "@d2/auth-domain";
import { MemoryCacheStore } from "@d2/cache-memory";

// Helper: compute HIBP-style response line for a password
function hibpSuffixAndCount(password: string, count: number): string {
  const sha1 = createHash("sha1").update(password).digest("hex").toUpperCase();
  const suffix = sha1.slice(5);
  return `${suffix}:${count}`;
}

function hibpPrefix(password: string): string {
  const sha1 = createHash("sha1").update(password).digest("hex").toUpperCase();
  return sha1.slice(0, 5);
}

describe("checkBreachedPassword", () => {
  let cache: MemoryCacheStore;
  const mockLogger = { warn: vi.fn() };

  beforeEach(() => {
    cache = new MemoryCacheStore();
    mockLogger.warn.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("should detect a breached password", async () => {
    const password = "breachedPass123!";
    const responseBody = [
      "0000000000000000000000000000000000A:5",
      hibpSuffixAndCount(password, 42),
      "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF:1",
    ].join("\n");

    vi.mocked(fetch).mockResolvedValue(new Response(responseBody, { status: 200 }));

    const result = await checkBreachedPassword(password, cache, mockLogger);
    expect(result.breached).toBe(true);
    expect(result.count).toBe(42);
  });

  it("should return not breached for clean password", async () => {
    const password = "cleanPassword!456";
    const responseBody = [
      "0000000000000000000000000000000000A:5",
      "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF:1",
    ].join("\n");

    vi.mocked(fetch).mockResolvedValue(new Response(responseBody, { status: 200 }));

    const result = await checkBreachedPassword(password, cache, mockLogger);
    expect(result.breached).toBe(false);
    expect(result.count).toBeUndefined();
  });

  it("should use cache hit and skip fetch", async () => {
    const password = "cachedTest!789";
    const prefix = hibpPrefix(password);
    const responseBody = [hibpSuffixAndCount(password, 10)].join("\n");

    // Pre-populate cache
    cache.set(prefix, responseBody, PASSWORD_POLICY.HIBP_CACHE_TTL_MS);

    const result = await checkBreachedPassword(password, cache, mockLogger);
    expect(result.breached).toBe(true);
    expect(result.count).toBe(10);
    expect(fetch).not.toHaveBeenCalled();
  });

  it("should populate cache on fetch success", async () => {
    const password = "fetchAndCache!0";
    const prefix = hibpPrefix(password);
    const responseBody = "0000000000000000000000000000000000A:5\n";

    vi.mocked(fetch).mockResolvedValue(new Response(responseBody, { status: 200 }));

    await checkBreachedPassword(password, cache, mockLogger);

    // Cache should now have the response
    const cached = cache.get<string>(prefix);
    expect(cached).toBe(responseBody);
  });

  it("should fail-open on HIBP API error (non-200)", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response("Rate limited", { status: 429 }));

    const result = await checkBreachedPassword("somePassword!!", cache, mockLogger);
    expect(result.breached).toBe(false);
    expect(mockLogger.warn).toHaveBeenCalledOnce();
    expect(mockLogger.warn.mock.calls[0][0]).toContain("429");
  });

  it("should fail-open on network error", async () => {
    vi.mocked(fetch).mockRejectedValue(new Error("ECONNREFUSED"));

    const result = await checkBreachedPassword("somePassword!!", cache, mockLogger);
    expect(result.breached).toBe(false);
    expect(mockLogger.warn).toHaveBeenCalledOnce();
    expect(mockLogger.warn.mock.calls[0][0]).toContain("unreachable");
  });

  it("should call HIBP API with correct URL and User-Agent", async () => {
    const password = "testUrlCheck!1";
    const prefix = hibpPrefix(password);

    vi.mocked(fetch).mockResolvedValue(new Response("", { status: 200 }));

    await checkBreachedPassword(password, cache, mockLogger);

    expect(fetch).toHaveBeenCalledWith(`${PASSWORD_POLICY.HIBP_API_BASE}${prefix}`, {
      headers: { "User-Agent": "D2-WORX-Auth" },
    });
  });
});

describe("createPasswordFunctions", () => {
  let cache: MemoryCacheStore;
  const mockLogger = { warn: vi.fn() };

  beforeEach(() => {
    cache = new MemoryCacheStore();
    mockLogger.warn.mockReset();
    vi.stubGlobal("fetch", vi.fn());
    // Default: HIBP says not breached
    vi.mocked(fetch).mockResolvedValue(
      new Response("0000000000000000000000000000000000A:1", { status: 200 }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe("hash", () => {
    it("should return a bcrypt hash for a valid password", async () => {
      const { hash } = createPasswordFunctions(cache, mockLogger);
      const hashed = await hash("correcthorsebattery");
      expect(hashed).toContain(":"); // salt:derivedKey format
    });

    it("should throw for numeric-only password", async () => {
      const { hash } = createPasswordFunctions(cache, mockLogger);
      await expect(hash("123456789012")).rejects.toThrow("auth_errors_PASSWORD_NUMERIC_ONLY");
    });

    it("should throw for date-like password", async () => {
      const { hash } = createPasswordFunctions(cache, mockLogger);
      await expect(hash("2025-10-01")).rejects.toThrow("auth_errors_PASSWORD_DATE_LIKE");
    });

    it("should throw for common password", async () => {
      const { hash } = createPasswordFunctions(cache, mockLogger);
      await expect(hash("q1w2e3r4t5y6")).rejects.toThrow("auth_errors_PASSWORD_TOO_COMMON");
    });

    it("should throw for breached password", async () => {
      const password = "breachedButUnique!";
      const responseBody = hibpSuffixAndCount(password, 500) + "\n";
      vi.mocked(fetch).mockResolvedValue(new Response(responseBody, { status: 200 }));

      const { hash } = createPasswordFunctions(cache, mockLogger);
      await expect(hash(password)).rejects.toThrow("auth_errors_PASSWORD_BREACHED");
    });

    it("should succeed when HIBP is down (fail-open)", async () => {
      vi.mocked(fetch).mockRejectedValue(new Error("ECONNREFUSED"));

      const { hash } = createPasswordFunctions(cache, mockLogger);
      const hashed = await hash("validPasswordHere!");
      expect(hashed).toContain(":"); // salt:derivedKey format
      expect(mockLogger.warn).toHaveBeenCalled();
    });

    it("should not call HIBP for passwords that fail domain validation", async () => {
      const { hash } = createPasswordFunctions(cache, mockLogger);
      await expect(hash("123456789012")).rejects.toThrow();
      // Domain validation fails first — fetch should never be called
      expect(fetch).not.toHaveBeenCalled();
    });
  });

  describe("verify", () => {
    it("should return true for correct password", async () => {
      const { hash, verify } = createPasswordFunctions(cache, mockLogger);
      const hashed = await hash("mySecurePassword!!");
      const isValid = await verify({ hash: hashed, password: "mySecurePassword!!" });
      expect(isValid).toBe(true);
    });

    it("should return false for wrong password", async () => {
      const { hash, verify } = createPasswordFunctions(cache, mockLogger);
      const hashed = await hash("mySecurePassword!!");
      const isValid = await verify({ hash: hashed, password: "wrongPassword!!" });
      expect(isValid).toBe(false);
    });
  });
});
