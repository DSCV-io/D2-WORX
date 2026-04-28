import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { generateKeyPair, exportJWK, SignJWT } from "jose";
import type { KeyLike } from "jose";
import { verifyToken, checkFingerprint, populateRequestContext, jwtAuth } from "@d2/jwt-auth";
import type { VerifyTokenOptions } from "@d2/jwt-auth";
import { createLocalJWKSet } from "jose";
import { subtle } from "node:crypto";
import { OrgType } from "@d2/handler";
import { createServer } from "node:http";
import type { Server } from "node:http";

// ---------------------------------------------------------------------------
// Test key pair (RS256 — generated once per suite)
// ---------------------------------------------------------------------------

let privateKey: KeyLike;
let publicJwkForTests: ReturnType<typeof Object.create>;
let verifyOptions: VerifyTokenOptions;

const TEST_ISSUER = "d2-worx-test";
const TEST_AUDIENCE = "d2-services-test";

beforeAll(async () => {
  const { publicKey, privateKey: pk } = await generateKeyPair("RS256");
  privateKey = pk;

  const publicJwk = await exportJWK(publicKey);
  publicJwk.kid = "test-key-1";
  publicJwk.alg = "RS256";
  publicJwkForTests = publicJwk;

  const jwks = createLocalJWKSet({ keys: [publicJwk] });
  verifyOptions = { jwks, issuer: TEST_ISSUER, audience: TEST_AUDIENCE };
});

/**
 * Helper: signs a JWT with the test private key.
 */
async function signToken(
  claims: Record<string, unknown>,
  options?: { expiresIn?: string; algorithm?: string },
): Promise<string> {
  let builder = new SignJWT(claims)
    .setProtectedHeader({ alg: options?.algorithm ?? "RS256", kid: "test-key-1" })
    .setIssuer(TEST_ISSUER)
    .setAudience(TEST_AUDIENCE)
    .setIssuedAt();

  if (options?.expiresIn !== "none") {
    builder = builder.setExpirationTime(options?.expiresIn ?? "15m");
  }

  return builder.sign(privateKey);
}

// ---------------------------------------------------------------------------
// verifyToken
// ---------------------------------------------------------------------------

describe("verifyToken", () => {
  it("accepts a valid RS256 token", async () => {
    const token = await signToken({ sub: "user-1", email: "a@b.com" });
    const result = await verifyToken(token, verifyOptions);
    expect(result.success).toBe(true);
    expect(result.data?.payload.sub).toBe("user-1");
  });

  it("rejects an expired token", async () => {
    const token = await signToken({ sub: "user-1" }, { expiresIn: "-1s" });
    const result = await verifyToken(token, verifyOptions);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
    expect(result.messages).toEqual(expect.arrayContaining([expect.stringContaining("expired")]));
  });

  it("rejects a token with wrong issuer", async () => {
    const token = await new SignJWT({ sub: "user-1" })
      .setProtectedHeader({ alg: "RS256", kid: "test-key-1" })
      .setIssuer("wrong-issuer")
      .setAudience(TEST_AUDIENCE)
      .setExpirationTime("15m")
      .sign(privateKey);

    const result = await verifyToken(token, verifyOptions);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("rejects a token with wrong audience", async () => {
    const token = await new SignJWT({ sub: "user-1" })
      .setProtectedHeader({ alg: "RS256", kid: "test-key-1" })
      .setIssuer(TEST_ISSUER)
      .setAudience("wrong-audience")
      .setExpirationTime("15m")
      .sign(privateKey);

    const result = await verifyToken(token, verifyOptions);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("rejects a completely invalid token string", async () => {
    const result = await verifyToken("not.a.jwt", verifyOptions);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("rejects an empty string", async () => {
    const result = await verifyToken("", verifyOptions);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("rejects a token signed with a different key", async () => {
    const { privateKey: otherKey } = await generateKeyPair("RS256");
    const token = await new SignJWT({ sub: "user-1" })
      .setProtectedHeader({ alg: "RS256", kid: "unknown-key" })
      .setIssuer(TEST_ISSUER)
      .setAudience(TEST_AUDIENCE)
      .setExpirationTime("15m")
      .sign(otherKey);

    const result = await verifyToken(token, verifyOptions);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("returns the payload claims on success", async () => {
    const token = await signToken({
      sub: "user-42",
      email: "test@d2.io",
      orgId: "org-1",
      orgType: "customer",
      role: "member",
    });
    const result = await verifyToken(token, verifyOptions);
    expect(result.success).toBe(true);
    expect(result.data?.payload["email"]).toBe("test@d2.io");
    expect(result.data?.payload["orgId"]).toBe("org-1");
    expect(result.data?.payload["orgType"]).toBe("customer");
    expect(result.data?.payload["role"]).toBe("member");
  });
});

// ---------------------------------------------------------------------------
// checkFingerprint
// ---------------------------------------------------------------------------

describe("checkFingerprint", () => {
  async function computeFp(ua: string, accept: string): Promise<string> {
    const data = new TextEncoder().encode(`${ua}|${accept}`);
    const hash = await subtle.digest("SHA-256", data);
    return Array.from(new Uint8Array(hash))
      .map((b) => b.toString(16).padStart(2, "0"))
      .join("");
  }

  it("passes when fingerprint matches", async () => {
    const ua = "Mozilla/5.0 TestBrowser";
    const accept = "application/json";
    const fp = await computeFp(ua, accept);

    const result = await checkFingerprint({ fp }, ua, accept);
    expect(result.success).toBe(true);
  });

  it("fails when fingerprint does not match", async () => {
    const result = await checkFingerprint(
      { fp: "0000000000000000000000000000000000000000000000000000000000000000" },
      "Mozilla/5.0 StolenBrowser",
      "text/html",
    );
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
    expect(result.messages).toEqual(expect.arrayContaining([expect.stringContaining("mismatch")]));
  });

  it("rejects when no fp claim is present (fail-closed by default)", async () => {
    // The old behavior was a soft-pass for "backward compat." Any future
    // issuer or dev token without `fp` could bypass binding entirely. New
    // default is fail-closed; opt-in via `allowMissingClaim: true` for
    // explicit dev paths.
    const result = await checkFingerprint({}, "Any UA", "Any Accept");
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("rejects when fp claim is not a string (fail-closed by default)", async () => {
    const result = await checkFingerprint({ fp: 12345 }, "UA", "Accept");
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("passes missing fp claim only when allowMissingClaim is explicitly opted in", async () => {
    const result = await checkFingerprint({}, "Any UA", "Any Accept", {
      allowMissingClaim: true,
    });
    expect(result.success).toBe(true);
  });

  it("produces consistent hashes for same inputs", async () => {
    const fp1 = await computeFp("TestUA", "application/json");
    const fp2 = await computeFp("TestUA", "application/json");
    expect(fp1).toBe(fp2);
  });

  it("produces different hashes for different UAs", async () => {
    const fp1 = await computeFp("UA-1", "application/json");
    const fp2 = await computeFp("UA-2", "application/json");
    expect(fp1).not.toBe(fp2);
  });
});

// ---------------------------------------------------------------------------
// populateRequestContext
// ---------------------------------------------------------------------------

describe("populateRequestContext", () => {
  it("maps basic user claims to IRequestContext", () => {
    const ctx = populateRequestContext({
      sub: "user-123",
      email: "user@example.com",
      username: "testuser",
    });

    expect(ctx.isAuthenticated).toBe(true);
    expect(ctx.isTrustedService).toBe(false);
    expect(ctx.userId).toBe("user-123");
    expect(ctx.email).toBe("user@example.com");
    expect(ctx.username).toBe("testuser");
  });

  it("maps org claims to both agent and target", () => {
    const ctx = populateRequestContext({
      sub: "user-1",
      orgId: "org-1",
      orgName: "Test Org",
      orgType: "customer",
      role: "admin",
    });

    expect(ctx.agentOrgId).toBe("org-1");
    expect(ctx.agentOrgName).toBe("Test Org");
    expect(ctx.agentOrgType).toBe(OrgType.Customer);
    expect(ctx.agentOrgRole).toBe("admin");
    // Target = agent when not emulating
    expect(ctx.targetOrgId).toBe("org-1");
    expect(ctx.targetOrgName).toBe("Test Org");
    expect(ctx.targetOrgType).toBe(OrgType.Customer);
    expect(ctx.targetOrgRole).toBe("admin");
  });

  it("maps emulation claims to target org", () => {
    const ctx = populateRequestContext({
      sub: "support-user",
      orgId: "support-org",
      orgName: "Support Org",
      orgType: "support",
      role: "member",
      isEmulating: true,
      emulatedOrgId: "customer-org",
      emulatedOrgName: "Customer Org",
      emulatedOrgType: "customer",
    });

    expect(ctx.isOrgEmulating).toBe(true);
    // Agent = support user's actual org
    expect(ctx.agentOrgId).toBe("support-org");
    expect(ctx.agentOrgType).toBe(OrgType.Support);
    // Target = emulated org
    expect(ctx.targetOrgId).toBe("customer-org");
    expect(ctx.targetOrgName).toBe("Customer Org");
    expect(ctx.targetOrgType).toBe(OrgType.Customer);
    expect(ctx.targetOrgRole).toBe("auditor");
  });

  it("maps impersonation claims", () => {
    const ctx = populateRequestContext({
      sub: "impersonated-user",
      isImpersonating: true,
      impersonatedBy: "admin-user",
      impersonatingEmail: "admin@example.com",
      impersonatingUsername: "adminuser",
    });

    expect(ctx.isUserImpersonating).toBe(true);
    expect(ctx.impersonatedBy).toBe("admin-user");
    expect(ctx.impersonatingEmail).toBe("admin@example.com");
    expect(ctx.impersonatingUsername).toBe("adminuser");
  });

  it("computes isAgentStaff/isAgentAdmin for support org", () => {
    const ctx = populateRequestContext({
      sub: "user-1",
      orgType: "support",
    });
    expect(ctx.isAgentStaff).toBe(true);
    expect(ctx.isAgentAdmin).toBe(false);
  });

  it("computes isAgentStaff/isAgentAdmin for admin org", () => {
    const ctx = populateRequestContext({
      sub: "user-1",
      orgType: "admin",
    });
    expect(ctx.isAgentStaff).toBe(true);
    expect(ctx.isAgentAdmin).toBe(true);
  });

  it("computes isAgentStaff=false for customer org", () => {
    const ctx = populateRequestContext({
      sub: "user-1",
      orgType: "customer",
    });
    expect(ctx.isAgentStaff).toBe(false);
    expect(ctx.isAgentAdmin).toBe(false);
  });

  it("handles isEmulating as string 'true'", () => {
    const ctx = populateRequestContext({
      sub: "user-1",
      isEmulating: "true",
      emulatedOrgId: "emu-org",
    });
    expect(ctx.isOrgEmulating).toBe(true);
    expect(ctx.targetOrgId).toBe("emu-org");
  });

  it("handles missing optional claims gracefully", () => {
    const ctx = populateRequestContext({ sub: "user-1" });
    expect(ctx.userId).toBe("user-1");
    expect(ctx.email).toBeUndefined();
    expect(ctx.username).toBeUndefined();
    expect(ctx.agentOrgId).toBeUndefined();
    expect(ctx.agentOrgType).toBeUndefined();
    expect(ctx.isOrgEmulating).toBe(false);
    expect(ctx.isUserImpersonating).toBe(false);
  });

  it("rejects unknown org types", () => {
    const ctx = populateRequestContext({
      sub: "user-1",
      orgType: "InvalidType",
    });
    expect(ctx.agentOrgType).toBeUndefined();
    expect(ctx.targetOrgType).toBeUndefined();
  });

  it("handles all valid OrgType values", () => {
    for (const orgType of Object.values(OrgType)) {
      const ctx = populateRequestContext({ sub: "user-1", orgType });
      expect(ctx.agentOrgType).toBe(orgType);
    }
  });
});

// ---------------------------------------------------------------------------
// jwtAuth (Hono middleware factory)
// ---------------------------------------------------------------------------

describe("jwtAuth middleware", () => {
  let jwksServer: Server;
  let jwksUrl: string;

  /**
   * Spins up a tiny HTTP server that serves the test public key as a JWKS
   * endpoint, allowing createRemoteJWKSet (used internally by jwtAuth) to
   * resolve the key without mocking.
   */
  beforeAll(async () => {
    // Reuse the public JWK already exported in the file-level beforeAll.
    const jwk = { ...publicJwkForTests, use: "sig" };
    const jwksJson = JSON.stringify({ keys: [jwk] });

    jwksServer = createServer((req, res) => {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(jwksJson);
    });

    await new Promise<void>((resolve) => {
      jwksServer.listen(0, "127.0.0.1", () => resolve());
    });

    const addr = jwksServer.address();
    if (!addr || typeof addr === "string") {
      throw new Error("Failed to get server address");
    }
    jwksUrl = `http://127.0.0.1:${addr.port}/.well-known/jwks.json`;
  });

  afterAll(async () => {
    await new Promise<void>((resolve, reject) => {
      jwksServer.close((err) => (err ? reject(err) : resolve()));
    });
  });

  /**
   * Helper: creates a mock Hono Context object with the minimum surface area
   * needed by the jwtAuth middleware. Avoids requiring `hono` as a direct
   * dependency of @d2/shared-tests.
   */
  function createMockContext(headers: Record<string, string> = {}): {
    ctx: {
      req: { header: (name: string) => string | undefined };
      json: (body: unknown, status: number) => Response;
      get: (key: string) => unknown;
      set: (key: string, value: unknown) => void;
    };
    getSetValues: () => Record<string, unknown>;
  } {
    const lowerHeaders: Record<string, string> = {};
    for (const [k, v] of Object.entries(headers)) {
      lowerHeaders[k.toLowerCase()] = v;
    }

    const setValues: Record<string, unknown> = {};

    const ctx = {
      req: {
        header: (name: string) => lowerHeaders[name.toLowerCase()],
      },
      json: (body: unknown, status: number) =>
        new Response(JSON.stringify(body), {
          status,
          headers: { "Content-Type": "application/json" },
        }),
      get: (key: string) => setValues[key],
      set: (key: string, value: unknown) => {
        setValues[key] = value;
      },
    };

    return { ctx, getSetValues: () => setValues };
  }

  /**
   * Helper: invokes the middleware and returns either the short-circuit
   * Response (on auth failure) or null (if next() was called, meaning auth
   * passed). Also returns the values set on the context via c.set().
   */
  async function callMiddleware(
    headers: Record<string, string> = {},
    options: { fingerprintCheck?: boolean } = {},
  ): Promise<{
    response: Response | null;
    setValues: Record<string, unknown>;
  }> {
    const middleware = jwtAuth({
      jwksUrl,
      issuer: TEST_ISSUER,
      audience: TEST_AUDIENCE,
      // Default off for most tests — the integration tests sign tokens
      // without `fp` and aren't about fingerprint binding. Tests that DO
      // want to exercise fp behavior pass `{ fingerprintCheck: true }`.
      // The strict-default behavior of `checkFingerprint` itself is
      // asserted in the unit tests above.
      fingerprintCheck: options.fingerprintCheck ?? false,
    });

    const { ctx, getSetValues } = createMockContext(headers);
    let nextCalled = false;
    const next = async () => {
      nextCalled = true;
    };

    const result = await middleware(ctx as never, next);

    if (result instanceof Response) {
      return { response: result, setValues: getSetValues() };
    }

    if (nextCalled) {
      return { response: null, setValues: getSetValues() };
    }

    // Shouldn't happen — middleware either returns a Response or calls next
    throw new Error("Middleware neither returned a Response nor called next()");
  }

  it("returns 401 when Authorization header is missing", async () => {
    const { response } = await callMiddleware({});

    expect(response).not.toBeNull();
    expect(response!.status).toBe(401);
    const body = await response!.json();
    expect(body.success).toBe(false);
    expect(body.statusCode).toBe(401);
    expect(body.messages).toEqual(
      expect.arrayContaining([expect.stringContaining("Missing Authorization header")]),
    );
  });

  it("returns 401 when Authorization header has no Bearer prefix", async () => {
    const token = await signToken({ sub: "user-1" });
    const { response } = await callMiddleware({
      Authorization: `Basic ${token}`,
    });

    expect(response).not.toBeNull();
    expect(response!.status).toBe(401);
    const body = await response!.json();
    expect(body.success).toBe(false);
    expect(body.statusCode).toBe(401);
    expect(body.messages).toEqual(
      expect.arrayContaining([expect.stringContaining("Invalid Authorization header format")]),
    );
  });

  it("returns 401 for an expired token", async () => {
    const token = await signToken({ sub: "user-1" }, { expiresIn: "-1s" });
    const { response } = await callMiddleware({
      Authorization: `Bearer ${token}`,
    });

    expect(response).not.toBeNull();
    expect(response!.status).toBe(401);
    const body = await response!.json();
    expect(body.success).toBe(false);
    expect(body.statusCode).toBe(401);
    expect(body.messages).toEqual(
      expect.arrayContaining([expect.stringContaining("Unauthorized")]),
    );
  });

  it("passes and populates requestContext for a valid token", async () => {
    const token = await signToken({
      sub: "user-42",
      email: "test@d2.io",
      orgId: "org-1",
      orgType: "customer",
      role: "member",
    });

    const { response, setValues } = await callMiddleware({
      Authorization: `Bearer ${token}`,
    });

    // next() was called — no error response
    expect(response).toBeNull();

    // requestContext was populated from JWT claims
    const reqCtx = setValues["requestContext"] as Record<string, unknown>;
    expect(reqCtx).toBeDefined();
    expect(reqCtx.isAuthenticated).toBe(true);
    expect(reqCtx.userId).toBe("user-42");
    expect(reqCtx.agentOrgId).toBe("org-1");
    expect(reqCtx.targetOrgId).toBe("org-1");
    expect(reqCtx.agentOrgType).toBe(OrgType.Customer);
    expect(reqCtx.agentOrgRole).toBe("member");
  });

  it("passes when token has fp claim matching the request fingerprint", async () => {
    const ua = "Mozilla/5.0 TestBrowser";
    const accept = "application/json";
    // Compute the expected fingerprint SHA-256(UA|Accept)
    const data = new TextEncoder().encode(`${ua}|${accept}`);
    const hash = await subtle.digest("SHA-256", data);
    const fp = Array.from(new Uint8Array(hash))
      .map((b) => b.toString(16).padStart(2, "0"))
      .join("");

    const token = await signToken({ sub: "user-1", fp });

    const { response, setValues } = await callMiddleware(
      {
        Authorization: `Bearer ${token}`,
        "User-Agent": ua,
        Accept: accept,
      },
      { fingerprintCheck: true },
    );

    expect(response).toBeNull();
    const reqCtx = setValues["requestContext"] as Record<string, unknown>;
    expect(reqCtx).toBeDefined();
    expect(reqCtx.userId).toBe("user-1");
  });

  it("returns 401 when token fp claim does not match request fingerprint", async () => {
    // Token fingerprint was computed with one UA/Accept pair
    const originalUa = "Mozilla/5.0 OriginalBrowser";
    const originalAccept = "application/json";
    const data = new TextEncoder().encode(`${originalUa}|${originalAccept}`);
    const hash = await subtle.digest("SHA-256", data);
    const fp = Array.from(new Uint8Array(hash))
      .map((b) => b.toString(16).padStart(2, "0"))
      .join("");

    const token = await signToken({ sub: "user-1", fp });

    // Request comes from a DIFFERENT client (different UA)
    const { response } = await callMiddleware(
      {
        Authorization: `Bearer ${token}`,
        "User-Agent": "Mozilla/5.0 StolenBrowser",
        Accept: "text/html",
      },
      { fingerprintCheck: true },
    );

    expect(response).not.toBeNull();
    expect(response!.status).toBe(401);
    const body = await response!.json();
    expect(body.success).toBe(false);
    expect(body.statusCode).toBe(401);
    expect(body.messages).toEqual(
      expect.arrayContaining([expect.stringContaining("Unauthorized")]),
    );
  });
});
