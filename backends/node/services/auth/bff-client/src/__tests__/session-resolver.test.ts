import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { SessionResolver } from "../session-resolver.js";
import type { AuthBffConfig } from "../types.js";
import type { ILogger } from "@d2/logging";

function createSilentLogger(): ILogger {
  return {
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
    child: vi.fn(),
  } as unknown as ILogger;
}

const config: AuthBffConfig = { authServiceUrl: "http://localhost:5100" };

function makeRequest(cookie?: string, fingerprint?: string): Request {
  const headers = new Headers();
  if (cookie) headers.set("cookie", cookie);
  if (fingerprint) headers.set("x-client-fingerprint", fingerprint);
  return new Request("http://localhost:5173/dashboard", { headers });
}

function mockFetchResponse(data: unknown, status = 200) {
  return vi.fn().mockResolvedValue(
    new Response(JSON.stringify(data), {
      status,
      headers: { "Content-Type": "application/json" },
    }),
  );
}

const validSessionResponse = {
  session: {
    id: "sess-1",
    userId: "user-1",
    token: "tok-abc",
    expiresAt: "2026-03-10T00:00:00Z",
    activeOrganizationId: "org-1",
    activeOrganizationType: "customer",
    activeOrganizationRole: "owner",
    emulatedOrganizationId: null,
    emulatedOrganizationType: null,
  },
  user: {
    id: "user-1",
    email: "test@example.com",
    name: "Test User",
    username: "testuser",
    displayUsername: "TestUser",
    image: undefined,
  },
};

describe("SessionResolver", () => {
  let resolver: SessionResolver;
  let logger: ILogger;

  beforeEach(() => {
    logger = createSilentLogger();
    resolver = new SessionResolver(config, logger);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should return session and user on successful 200 response", async () => {
    vi.stubGlobal("fetch", mockFetchResponse(validSessionResponse));

    const result = await resolver.resolve(makeRequest("session_token=abc"));

    expect(result.session).toEqual({
      userId: "user-1",
      activeOrganizationId: "org-1",
      activeOrganizationType: "customer",
      activeOrganizationRole: "owner",
      emulatedOrganizationId: null,
      emulatedOrganizationType: null,
    });
    expect(result.user).toEqual({
      id: "user-1",
      email: "test@example.com",
      name: "Test User",
      username: "testuser",
      displayUsername: "TestUser",
      image: undefined,
    });
  });

  it("should return null session and user when no cookie header", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await resolver.resolve(makeRequest());

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("should return null session and user on 401 response", async () => {
    vi.stubGlobal("fetch", mockFetchResponse({ error: "Unauthorized" }, 401));

    const result = await resolver.resolve(makeRequest("session_token=expired"));

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
  });

  it("should return null session and user on network error (fail-closed)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("Connection refused")));

    const result = await resolver.resolve(makeRequest("session_token=abc"));

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
    expect(logger.warn).toHaveBeenCalledOnce();
  });

  it("should return null session and user on non-OK status", async () => {
    vi.stubGlobal("fetch", mockFetchResponse({ error: "Internal" }, 500));

    const result = await resolver.resolve(makeRequest("session_token=abc"));

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
    expect(logger.warn).toHaveBeenCalledOnce();
  });

  it("should forward cookie header correctly", async () => {
    const fetchMock = mockFetchResponse(validSessionResponse);
    vi.stubGlobal("fetch", fetchMock);

    await resolver.resolve(makeRequest("better-auth.session_token=xyz123"));

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://localhost:5100/api/auth/get-session");
    expect(init.headers.cookie).toBe("better-auth.session_token=xyz123");
  });

  it("should forward x-client-fingerprint header", async () => {
    const fetchMock = mockFetchResponse(validSessionResponse);
    vi.stubGlobal("fetch", fetchMock);

    await resolver.resolve(makeRequest("session_token=abc", "fp-hash-abc"));

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers["x-client-fingerprint"]).toBe("fp-hash-abc");
  });

  it("should send X-Api-Key header when apiKey is configured", async () => {
    const fetchMock = mockFetchResponse(validSessionResponse);
    vi.stubGlobal("fetch", fetchMock);

    const trustedResolver = new SessionResolver(
      { authServiceUrl: "http://localhost:5100", apiKey: "d2.sveltekit.auth.key" },
      logger,
    );

    await trustedResolver.resolve(makeRequest("session_token=abc"));

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers["x-api-key"]).toBe("d2.sveltekit.auth.key");
  });

  it("should not send X-Api-Key header when apiKey is not configured", async () => {
    const fetchMock = mockFetchResponse(validSessionResponse);
    vi.stubGlobal("fetch", fetchMock);

    await resolver.resolve(makeRequest("session_token=abc"));

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers["x-api-key"]).toBeUndefined();
  });

  it("should handle missing optional fields gracefully", async () => {
    const minimalResponse = {
      session: {
        id: "sess-2",
        userId: "user-2",
        token: "tok-def",
        expiresAt: "2026-03-10T00:00:00Z",
        // No org fields at all
      },
      user: {
        id: "user-2",
        email: "minimal@example.com",
        name: "Minimal User",
        // No username, displayUsername, image
      },
    };

    vi.stubGlobal("fetch", mockFetchResponse(minimalResponse));

    const result = await resolver.resolve(makeRequest("session_token=abc"));

    expect(result.session).toEqual({
      userId: "user-2",
      activeOrganizationId: null,
      activeOrganizationType: null,
      activeOrganizationRole: null,
      emulatedOrganizationId: null,
      emulatedOrganizationType: null,
    });
    expect(result.user).toEqual({
      id: "user-2",
      email: "minimal@example.com",
      name: "Minimal User",
      username: undefined,
      displayUsername: undefined,
      image: undefined,
    });
  });

  it("should return null when response body has no session/user", async () => {
    vi.stubGlobal("fetch", mockFetchResponse({}));

    const result = await resolver.resolve(makeRequest("session_token=abc"));

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
  });

  it("should log a warning when session cookie is present but auth service returns null", async () => {
    // BetterAuth returns 200 with null body when cookie value is unsigned/tampered.
    // This is a monitoring signal — the cookie existed but resolution silently failed.
    vi.stubGlobal("fetch", mockFetchResponse(null));

    const result = await resolver.resolve(
      makeRequest("better-auth.session_token=unsigned-or-tampered-value"),
    );

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
    expect(logger.warn).toHaveBeenCalledWith(
      expect.stringContaining("Session cookie present but auth service returned no session"),
    );
  });

  it("should not log a warning when non-session cookies are present but no session", async () => {
    // Other cookies (analytics, preferences) should not trigger the warning
    vi.stubGlobal("fetch", mockFetchResponse(null));

    const result = await resolver.resolve(makeRequest("other_cookie=value; tracking=123"));

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
    expect(logger.warn).not.toHaveBeenCalled();
  });
});
