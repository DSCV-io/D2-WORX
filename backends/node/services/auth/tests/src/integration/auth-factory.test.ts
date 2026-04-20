import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import {
  createAuth,
  createPasswordFunctions,
  type AuthHooks,
  type AuthServiceConfig,
  type PrefixCache,
} from "@d2/auth-infra";
import { JWT_CLAIM_TYPES } from "@d2/auth-domain";
import {
  startPostgres,
  stopPostgres,
  getPool,
  getDb,
  cleanAllTables,
} from "./postgres-test-helpers.js";

/**
 * Tests the REAL `createAuth()` factory with all 6 plugins (bearer, username,
 * jwt, organization, admin) and hooks (UUIDv7 IDs, org type validation,
 * password validation, session hooks, JWT claims, email verification)
 * against real PostgreSQL.
 *
 * This is the highest-value integration test — it catches wiring issues,
 * plugin interactions, and hook behaviors that unit tests can't reach.
 */
describe("createAuth() full integration", () => {
  const testConfig: AuthServiceConfig = {
    databaseUrl: "", // populated by testcontainer
    redisUrl: "",
    baseUrl: "http://localhost:3333",
    corsOrigins: ["http://localhost:5173"],
    jwtIssuer: "test-issuer",
    jwtAudience: "test-audience",
    jwtExpirationSeconds: 900,
    passwordMinLength: 12,
    passwordMaxLength: 128,
  };

  let auth: ReturnType<typeof createAuth>;
  let onSignInCalls: Array<{
    userId: string;
    sessionId: string;
    ipAddress: string;
    userAgent: string;
  }>;
  let hooks: AuthHooks;

  /**
   * Mock HIBP cache — always returns empty response (no breaches found).
   * This avoids network calls while still exercising domain validation
   * (numeric-only, date-like, common blocklist) via createPasswordFunctions.
   */
  const mockHibpCache: PrefixCache = {
    get: () => "",
    set: () => {},
  };

  beforeAll(async () => {
    await startPostgres();

    onSignInCalls = [];

    const passwordFns = createPasswordFunctions(mockHibpCache);

    hooks = {
      onSignIn: async (data) => {
        onSignInCalls.push(data);
      },
      passwordFunctions: passwordFns,
    };

    auth = createAuth(testConfig, getDb(), undefined, hooks);
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
    onSignInCalls = [];
  });

  /**
   * Raw sign-up: creates user but does NOT auto-sign-in (email unverified).
   * Returns user only — no token or session.
   */
  async function rawSignUp(email: string, name: string, password = "SecurePass123!@#") {
    const res = await auth.api.signUpEmail({
      body: { email, password, name },
    });
    return { user: res.user };
  }

  /**
   * Sign up + manually verify email + sign in.
   * Use this for tests that need an authenticated session.
   */
  async function signUpAndVerify(email: string, name: string, password = "SecurePass123!@#") {
    const { user } = await rawSignUp(email, name, password);

    // Manually verify email (no notification service yet)
    await getPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [user.id]);

    // Sign in to get session + token
    const signInRes = await auth.api.signInEmail({
      body: { email, password },
    });
    const token =
      signInRes.token ??
      ((signInRes as Record<string, unknown>).session as { token: string })?.token;
    return { user, token: token as string, session: signInRes.session };
  }

  // -----------------------------------------------------------------------
  // Email verification
  // -----------------------------------------------------------------------
  describe("email verification", () => {
    it("should NOT auto-sign-in on sign-up (email unverified)", async () => {
      const res = await auth.api.signUpEmail({
        body: { email: "no-auto@example.com", password: "SecurePass123!@#", name: "No Auto" },
      });

      // BetterAuth returns { token: null, user } when requireEmailVerification is true
      expect(res.token).toBeNull();
      expect(res.user).toBeDefined();
      expect(res.user.email).toBe("no-auto@example.com");

      // No session created in DB
      const sessionRows = await getPool().query("SELECT * FROM session WHERE user_id = $1", [
        res.user.id,
      ]);
      expect(sessionRows.rows).toHaveLength(0);
    });

    it("should block sign-in when email is not verified", async () => {
      await rawSignUp("unverified@example.com", "Unverified");

      await expect(
        auth.api.signInEmail({
          body: { email: "unverified@example.com", password: "SecurePass123!@#" },
        }),
      ).rejects.toThrow();
    });

    it("should allow sign-in after email is verified", async () => {
      const { user } = await rawSignUp("verified@example.com", "Verified");

      // Manually verify email
      await getPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [user.id]);

      // Sign in should succeed now
      const signInRes = await auth.api.signInEmail({
        body: { email: "verified@example.com", password: "SecurePass123!@#" },
      });

      expect(signInRes.token).toBeDefined();
      expect(signInRes.user).toBeDefined();
      expect(signInRes.user.email).toBe("verified@example.com");
    });
  });

  // -----------------------------------------------------------------------
  // Full sign-up → verify → sign-in flow (end-to-end)
  // -----------------------------------------------------------------------
  describe("full sign-up flow", () => {
    it("should create user, account, and valid JWT after verification + sign-in", async () => {
      // Step 1: Sign up (no session — email unverified)
      const signUpRes = await auth.api.signUpEmail({
        body: { email: "e2e@example.com", password: "SecurePass123!@#", name: "E2E User" },
      });
      const userId = signUpRes.user.id;

      // Verify no session created at sign-up
      expect(signUpRes.token).toBeNull();

      // ---- 1. User row — every column populated correctly ----
      const userRow = await getPool().query('SELECT * FROM "user" WHERE id = $1', [userId]);
      expect(userRow.rows).toHaveLength(1);
      const u = userRow.rows[0];

      expect(u.id).toBe(userId);
      expect(u.id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
      expect(u.email).toBe("e2e@example.com");
      expect(u.name).toBe("E2E User");
      expect(u.email_verified).toBe(false);
      expect(u.created_at).toBeInstanceOf(Date);
      expect(u.updated_at).toBeInstanceOf(Date);
      expect(u.image).toBeNull();
      expect(u.role).toBe("agent");
      expect(u.banned).toBe(false);
      expect(u.ban_reason).toBeNull();
      expect(u.ban_expires).toBeNull();

      // Username — auto-generated, lowercase, PascalCase display variant
      expect(u.username).toBeDefined();
      expect(u.username).toBe(u.username.toLowerCase());
      expect(u.display_username).toBeDefined();
      expect(u.username).toBe(u.display_username.toLowerCase());
      expect(u.display_username).toMatch(/^[A-Z][a-z]+[A-Z][a-z]+\d{1,3}$/);

      // ---- 2. Account row — credential provider linked ----
      const accountRow = await getPool().query("SELECT * FROM account WHERE user_id = $1", [
        userId,
      ]);
      expect(accountRow.rows).toHaveLength(1);
      const acct = accountRow.rows[0];

      expect(acct.id).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      );
      expect(acct.provider_id).toBe("credential");
      expect(acct.user_id).toBe(userId);
      expect(acct.password).toBeDefined();
      expect(acct.password.length).toBeGreaterThan(0);
      expect(acct.created_at).toBeInstanceOf(Date);

      // Step 2: Verify email
      await getPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [userId]);

      // Step 3: Sign in (now succeeds)
      const signInRes = await auth.api.signInEmail({
        body: { email: "e2e@example.com", password: "SecurePass123!@#" },
      });
      const token = signInRes.token as string;
      expect(token).toBeDefined();

      // ---- 3. Session row — created by explicit sign-in ----
      const sessionRow = await getPool().query(
        "SELECT * FROM session WHERE user_id = $1 AND token = $2",
        [userId, token],
      );
      expect(sessionRow.rows).toHaveLength(1);
      const sess = sessionRow.rows[0];

      expect(sess.id).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      );
      expect(sess.user_id).toBe(userId);
      expect(sess.token).toBe(token);
      expect(sess.expires_at).toBeInstanceOf(Date);
      expect(sess.created_at).toBeInstanceOf(Date);

      // ---- 4. JWT — all claims present and correct ----
      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      expect(tokenRes.token).toBeDefined();

      const parts = tokenRes.token.split(".");
      const payload = JSON.parse(Buffer.from(parts[1], "base64url").toString());

      // Standard JWT fields
      expect(payload.iss).toBe("test-issuer");
      expect(payload.aud).toBe("test-audience");
      expect(payload.iat).toEqual(expect.any(Number));
      expect(payload.exp).toEqual(expect.any(Number));
      expect(payload.exp - payload.iat).toBe(900); // 15-minute expiry

      // Identity claims — match DB values
      expect(payload[JWT_CLAIM_TYPES.SUB]).toBe(userId);
      expect(payload[JWT_CLAIM_TYPES.EMAIL]).toBe("e2e@example.com");
      expect(payload[JWT_CLAIM_TYPES.USERNAME]).toBe(u.username);

      // Org claims — null (no org set)
      expect(payload[JWT_CLAIM_TYPES.ORG_ID]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.ORG_TYPE]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.ROLE]).toBeNull();

      // Emulation — inactive
      expect(payload[JWT_CLAIM_TYPES.EMULATED_ORG_ID]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.IS_EMULATING]).toBe(false);

      // Impersonation — inactive
      expect(payload[JWT_CLAIM_TYPES.IMPERSONATED_BY]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.IS_IMPERSONATING]).toBe(false);
      expect(payload[JWT_CLAIM_TYPES.IMPERSONATING_EMAIL]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.IMPERSONATING_USERNAME]).toBeNull();

      // Fingerprint — null (no hook provided in test)
      expect(payload[JWT_CLAIM_TYPES.FINGERPRINT]).toBeNull();

      // ---- 5. onSignIn hook — fired by explicit sign-in ----
      await new Promise((r) => setTimeout(r, 100));
      const signInCall = onSignInCalls.find((c) => c.userId === userId);
      expect(signInCall).toBeDefined();
      expect(signInCall!.userId).toBe(userId);
    });
  });

  // -----------------------------------------------------------------------
  // UUIDv7 + forceAllowId
  // -----------------------------------------------------------------------
  describe("UUIDv7 ID generation", () => {
    it("should generate UUIDv7 IDs for new users", async () => {
      const { user } = await rawSignUp("uuid@example.com", "UUID User");

      expect(user.id).toBeDefined();
      // UUIDv7 format: 8-4-4-4-12 = 36 chars, version nibble = 7
      expect(user.id).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      );
    });

    it("should generate UUIDv7 IDs for organizations", async () => {
      const { token } = await signUpAndVerify("org-id@example.com", "Org ID User");

      const org = await auth.api.createOrganization({
        body: { name: "Test Org", slug: "test-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      expect(org.id).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      );
    });
  });

  // -----------------------------------------------------------------------
  // Session hooks (onSignIn callback)
  // -----------------------------------------------------------------------
  describe("session hooks", () => {
    it("should NOT fire onSignIn on sign-up (no auto-sign-in)", async () => {
      await rawSignUp("hook-no-fire@example.com", "No Fire");

      // Wait a tick
      await new Promise((r) => setTimeout(r, 100));

      // No sign-in hook should fire (sign-up doesn't create a session)
      const call = onSignInCalls.find((c) => c.userId !== undefined);
      expect(call).toBeUndefined();
    });

    it("should fire onSignIn after explicit sign-in (post-verification)", async () => {
      const { user } = await signUpAndVerify("signin-hook@example.com", "SignIn Hook");

      // Wait a tick for fire-and-forget callback
      await new Promise((r) => setTimeout(r, 100));

      const call = onSignInCalls.find((c) => c.userId === user.id);
      expect(call).toBeDefined();
      expect(call!.userId).toBe(user.id);
    });
  });

  // -----------------------------------------------------------------------
  // User row completeness after sign-up
  // -----------------------------------------------------------------------
  describe("user row completeness", () => {
    it("should populate all user columns correctly after sign-up", async () => {
      const { user } = await rawSignUp("complete@example.com", "Complete User");

      const result = await getPool().query('SELECT * FROM "user" WHERE id = $1', [user.id]);
      expect(result.rows).toHaveLength(1);
      const row = result.rows[0];

      // Identity
      expect(row.id).toBe(user.id);
      expect(row.email).toBe("complete@example.com");
      expect(row.name).toBe("Complete User");

      // Username — auto-generated, lowercase, matches displayUsername case-insensitively
      expect(row.username).toBeDefined();
      expect(row.username).toBe(row.username.toLowerCase());
      expect(row.display_username).toBeDefined();
      expect(row.username).toBe(row.display_username.toLowerCase());

      // displayUsername format: PascalCase adjective + PascalCase noun + 1-3 digit suffix
      expect(row.display_username).toMatch(/^[A-Z][a-z]+[A-Z][a-z]+\d{1,3}$/);

      // Email verification — false at sign-up
      expect(row.email_verified).toBe(false);

      // Timestamps
      expect(row.created_at).toBeInstanceOf(Date);
      expect(row.updated_at).toBeInstanceOf(Date);

      // Admin plugin fields — defaultRole is "agent" (from admin() plugin config)
      expect(row.role).toBe("agent");
      expect(row.banned).toBe(false);
      expect(row.ban_reason).toBeNull();
      expect(row.ban_expires).toBeNull();

      // Optional fields
      expect(row.image).toBeNull();
    });

    it("should generate unique usernames for different users", async () => {
      const { user: user1 } = await rawSignUp("unique-user1@example.com", "Unique One");
      const { user: user2 } = await rawSignUp("unique-user2@example.com", "Unique Two");

      const result = await getPool().query(
        'SELECT username, display_username FROM "user" WHERE id IN ($1, $2)',
        [user1.id, user2.id],
      );
      expect(result.rows).toHaveLength(2);
      expect(result.rows[0].username).not.toBe(result.rows[1].username);
      expect(result.rows[0].display_username).not.toBe(result.rows[1].display_username);
    });
  });

  // -----------------------------------------------------------------------
  // JWT claims
  // -----------------------------------------------------------------------
  describe("JWT claims", () => {
    it("should issue a JWT with all identity claims populated", async () => {
      const { user, token } = await signUpAndVerify("jwt@example.com", "JWT User");

      // Fetch the auto-generated username from DB for assertion
      const dbRow = await getPool().query('SELECT username FROM "user" WHERE id = $1', [user.id]);
      const dbUsername = dbRow.rows[0].username as string;

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      expect(tokenRes.token).toBeDefined();

      // Decode JWT payload (base64url)
      const parts = tokenRes.token.split(".");
      const payload = JSON.parse(Buffer.from(parts[1], "base64url").toString());

      // Standard JWT fields
      expect(payload.iss).toBe("test-issuer");
      expect(payload.aud).toBe("test-audience");
      expect(payload.iat).toEqual(expect.any(Number));
      expect(payload.exp).toEqual(expect.any(Number));

      // Identity claims — all must be present and correct
      expect(payload[JWT_CLAIM_TYPES.SUB]).toBe(user.id);
      expect(payload[JWT_CLAIM_TYPES.EMAIL]).toBe("jwt@example.com");
      expect(payload[JWT_CLAIM_TYPES.USERNAME]).toBe(dbUsername);

      // Org claims — null when no active org
      expect(payload[JWT_CLAIM_TYPES.ORG_ID]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.ORG_TYPE]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.ROLE]).toBeNull();

      // Emulation claims — inactive
      expect(payload[JWT_CLAIM_TYPES.EMULATED_ORG_ID]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.IS_EMULATING]).toBe(false);

      // Impersonation claims — inactive
      expect(payload[JWT_CLAIM_TYPES.IMPERSONATED_BY]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.IS_IMPERSONATING]).toBe(false);
      expect(payload[JWT_CLAIM_TYPES.IMPERSONATING_EMAIL]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.IMPERSONATING_USERNAME]).toBeNull();

      // Fingerprint — null (no getFingerprintForCurrentRequest hook in test)
      expect(payload[JWT_CLAIM_TYPES.FINGERPRINT]).toBeNull();
    });

    it("should include org context claims when session has active org", async () => {
      const { token } = await signUpAndVerify("jwt-org@example.com", "JWT Org User");

      const org = await auth.api.createOrganization({
        body: { name: "JWT Org", slug: "jwt-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      await auth.api.setActiveOrganization({
        body: { organizationId: org.id },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const parts = tokenRes.token.split(".");
      const payload = JSON.parse(Buffer.from(parts[1], "base64url").toString());

      expect(payload[JWT_CLAIM_TYPES.ORG_ID]).toBe(org.id);
      // Role in JWT comes from custom session field (populated by app-layer code,
      // not BetterAuth's setActiveOrganization). Verify it's present as a key.
      expect(JWT_CLAIM_TYPES.ROLE in payload).toBe(true);
    });
  });

  // -----------------------------------------------------------------------
  // Organization + orgType
  // -----------------------------------------------------------------------
  describe("organization + orgType", () => {
    it("should create an org with default orgType 'customer'", async () => {
      const { token } = await signUpAndVerify("org-default@example.com", "Org Default");

      const org = await auth.api.createOrganization({
        body: { name: "Default Org", slug: "default-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const result = await getPool().query("SELECT org_type FROM organization WHERE id = $1", [
        org.id,
      ]);
      expect(result.rows[0].org_type).toBe("customer");
    });

    it("should assign creator as owner member", async () => {
      const { user, token } = await signUpAndVerify("org-owner@example.com", "Org Owner");

      const org = await auth.api.createOrganization({
        body: { name: "Owner Org", slug: "owner-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const result = await getPool().query(
        "SELECT role FROM member WHERE organization_id = $1 AND user_id = $2",
        [org.id, user.id],
      );
      expect(result.rows).toHaveLength(1);
      expect(result.rows[0].role).toBe("owner");
    });
  });

  // -----------------------------------------------------------------------
  // Password validation (domain rules via hash hook)
  // -----------------------------------------------------------------------
  describe("password validation", () => {
    it("should reject numeric-only passwords", async () => {
      await expect(
        auth.api.signUpEmail({
          body: {
            email: "numonly@example.com",
            password: "123456789012",
            name: "Numeric User",
          },
        }),
      ).rejects.toThrow();
    });

    it("should reject common blocklist passwords", async () => {
      await expect(
        auth.api.signUpEmail({
          body: {
            email: "blocklist@example.com",
            password: "password1234",
            name: "Blocklist User",
          },
        }),
      ).rejects.toThrow();
    });

    it("should accept a valid strong password", async () => {
      const { user } = await rawSignUp(
        "valid-pass@example.com",
        "Valid Pass User",
        "MyStr0ng!P@ssw0rd",
      );

      expect(user).toBeDefined();
      expect(user.email).toBe("valid-pass@example.com");
    });
  });

  // -----------------------------------------------------------------------
  // createUserContact hook (Geo contact on sign-up)
  // -----------------------------------------------------------------------
  describe("createUserContact hook", () => {
    it("should call createUserContact hook with userId and email on sign-up", async () => {
      const createUserContactCalls: Array<{
        userId: string;
        email: string;
        name: string;
        locale: string;
      }> = [];

      const authWithHook = createAuth(testConfig, getDb(), undefined, {
        ...hooks,
        createUserContact: async (data) => {
          createUserContactCalls.push(data);
        },
      });

      await authWithHook.api.signUpEmail({
        body: { email: "geo-test@example.com", password: "SecurePass123!@#", name: "Geo Test" },
      });

      expect(createUserContactCalls).toHaveLength(1);
      expect(createUserContactCalls[0].email).toBe("geo-test@example.com");
      expect(createUserContactCalls[0].name).toBe("Geo Test");
      expect(createUserContactCalls[0].locale).toBe("en-US");
      expect(createUserContactCalls[0].userId).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      );
    });

    it("should fail sign-up when createUserContact hook throws", async () => {
      const authWithFailingHook = createAuth(testConfig, getDb(), undefined, {
        ...hooks,
        createUserContact: async () => {
          throw new Error("Geo unavailable");
        },
      });

      await expect(
        authWithFailingHook.api.signUpEmail({
          body: { email: "fail@example.com", password: "SecurePass123!@#", name: "Fail Test" },
        }),
      ).rejects.toThrow();

      // Verify no user was created (fail-fast)
      const result = await getPool().query('SELECT * FROM "user" WHERE email = $1', [
        "fail@example.com",
      ]);
      expect(result.rows).toHaveLength(0);
    });

    it("should pass the same userId to the hook and the created user record", async () => {
      let hookUserId = "";

      const authWithHook = createAuth(testConfig, getDb(), undefined, {
        ...hooks,
        createUserContact: async (data) => {
          hookUserId = data.userId;
        },
      });

      const res = await authWithHook.api.signUpEmail({
        body: {
          email: "id-match@example.com",
          password: "SecurePass123!@#",
          name: "ID Match",
        },
      });

      expect(hookUserId).toBe(res.user.id);
    });
  });

  // -----------------------------------------------------------------------
  // Custom session fields
  // -----------------------------------------------------------------------
  describe("custom session fields", () => {
    it("should persist active organization in session after setActiveOrganization", async () => {
      const { token } = await signUpAndVerify("session-fields@example.com", "Session Fields");

      const org = await auth.api.createOrganization({
        body: { name: "Session Org", slug: "session-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      await auth.api.setActiveOrganization({
        body: { organizationId: org.id },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const result = await getPool().query(
        `SELECT active_organization_id FROM session WHERE token = $1`,
        [token],
      );
      expect(result.rows.length).toBeGreaterThanOrEqual(1);
      expect(result.rows[0].active_organization_id).toBe(org.id);
    });
  });
});
