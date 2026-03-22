import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import {
  createAuth,
  createPasswordFunctions,
  type AuthHooks,
  type AuthServiceConfig,
  type PrefixCache,
} from "@d2/auth-infra";
import { JWT_CLAIM_TYPES, SESSION_FIELDS } from "@d2/auth-domain";
import {
  startPostgres,
  stopPostgres,
  getPool,
  getDb,
  cleanAllTables,
} from "./postgres-test-helpers.js";

/**
 * BetterAuth behavioral integration tests — validates plugin interactions,
 * session management, JWT issuance, schema conventions, and ID generation
 * against a real PostgreSQL instance with all production plugins enabled.
 */
describe("BetterAuth behavioral integration", () => {
  const testConfig: AuthServiceConfig = {
    databaseUrl: "",
    redisUrl: "",
    baseUrl: "http://localhost:3333",
    corsOrigins: ["http://localhost:5173"],
    jwtIssuer: "test-issuer",
    jwtAudience: "test-audience",
    jwtExpirationSeconds: 900,
    passwordMinLength: 12,
    passwordMaxLength: 128,
  };

  const mockHibpCache: PrefixCache = {
    get: () => "",
    set: () => {},
  };

  let auth: ReturnType<typeof createAuth>;
  let hooks: AuthHooks;

  beforeAll(async () => {
    await startPostgres();

    const passwordFns = createPasswordFunctions(mockHibpCache);
    hooks = { passwordFunctions: passwordFns };
    auth = createAuth(testConfig, getDb(), undefined, hooks);
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  // ---- Helpers ----

  async function rawSignUp(email: string, name: string, password = "SecurePass123!@#") {
    const res = await auth.api.signUpEmail({
      body: { email, password, name },
    });
    return { user: res.user };
  }

  async function signUpAndVerify(email: string, name: string, password = "SecurePass123!@#") {
    const { user } = await rawSignUp(email, name, password);
    await getPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [user.id]);
    const signInRes = await auth.api.signInEmail({
      body: { email, password },
    });
    const token =
      signInRes.token ??
      ((signInRes as Record<string, unknown>).session as { token: string })?.token;
    return { user, token: token as string, session: signInRes.session };
  }

  async function createOrgAndActivate(token: string, name: string, slug: string) {
    const org = await auth.api.createOrganization({
      body: { name, slug },
      headers: new Headers({ Authorization: `Bearer ${token}` }),
    });
    await auth.api.setActiveOrganization({
      body: { organizationId: org.id },
      headers: new Headers({ Authorization: `Bearer ${token}` }),
    });
    return org;
  }

  // =========================================================================
  // Q1 — RS256 JWT structure and JWKS
  // =========================================================================
  describe("RS256 JWT structure", () => {
    it("should produce a JWT with alg: RS256 in the header", async () => {
      const { token } = await signUpAndVerify("q1-alg@example.com", "Q1 Alg");

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      // Decode JWT header (first segment)
      const header = JSON.parse(Buffer.from(tokenRes.token.split(".")[0], "base64url").toString());

      expect(header.alg).toBe("RS256");
      // typ is optional per RFC 7519; BetterAuth/jose may omit it
      if (header.typ) {
        expect(header.typ).toBe("JWT");
      }
    });

    it("should have a three-segment JWT (header.payload.signature)", async () => {
      const { token } = await signUpAndVerify("q1-segments@example.com", "Q1 Segments");

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const parts = tokenRes.token.split(".");
      expect(parts).toHaveLength(3);
      // Each segment should be non-empty base64url
      for (const part of parts) {
        expect(part.length).toBeGreaterThan(0);
      }
    });

    it("should expose a JWKS endpoint with RS256 keys", async () => {
      // The JWKS endpoint is available via auth.api — we need to call it via handler
      // BetterAuth's JWKS endpoint returns { keys: [...] }
      // First, generate at least one JWT so a key exists
      const { token } = await signUpAndVerify("q1-jwks@example.com", "Q1 JWKS");
      await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      // Query JWKS table directly to verify RS256 key was generated
      const jwksRows = await getPool().query("SELECT * FROM jwks");
      expect(jwksRows.rows.length).toBeGreaterThanOrEqual(1);

      // The jwks table stores the key pair — verify it's RSA (for RS256)
      // Note: JWK `alg` field is optional per RFC 7517. The algorithm is
      // specified in the JWT header, not necessarily in the stored JWK.
      const keyRow = jwksRows.rows[0];
      const publicKey = JSON.parse(keyRow.public_key);
      expect(publicKey.kty).toBe("RSA");
      // RSA key should have modulus (n) and exponent (e)
      expect(publicKey.n).toBeDefined();
      expect(publicKey.e).toBeDefined();
    });

    it("should produce a JWT with valid standard claims", async () => {
      const { token } = await signUpAndVerify("q1-claims@example.com", "Q1 Claims");

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const payload = JSON.parse(Buffer.from(tokenRes.token.split(".")[1], "base64url").toString());

      // Standard JWT claims that .NET AddJwtBearer() validates
      expect(payload.iss).toBe("test-issuer");
      expect(payload.aud).toBe("test-audience");
      expect(payload.iat).toEqual(expect.any(Number));
      expect(payload.exp).toEqual(expect.any(Number));
      expect(payload.exp - payload.iat).toBe(900);

      // sub claim — .NET maps this to ClaimTypes.NameIdentifier
      expect(payload[JWT_CLAIM_TYPES.SUB]).toBeDefined();
      expect(typeof payload[JWT_CLAIM_TYPES.SUB]).toBe("string");
    });
  });

  // =========================================================================
  // Q2 — Session lifecycle (create → update → revoke)
  // =========================================================================
  describe("session lifecycle", () => {
    it("should create a session row in the database on sign-in", async () => {
      const { user, token } = await signUpAndVerify("q2-create@example.com", "Q2 Create");

      const sessions = await getPool().query("SELECT * FROM session WHERE user_id = $1", [user.id]);
      expect(sessions.rows.length).toBeGreaterThanOrEqual(1);

      const sess = sessions.rows.find((r: Record<string, unknown>) => r.token === token);
      expect(sess).toBeDefined();
      expect(sess.user_id).toBe(user.id);
      expect(sess.expires_at).toBeInstanceOf(Date);
    });

    it("should update session custom fields via setActiveOrganization", async () => {
      const { user, token } = await signUpAndVerify("q2-update@example.com", "Q2 Update");

      const org = await auth.api.createOrganization({
        body: { name: "Q2 Org", slug: "q2-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      await auth.api.setActiveOrganization({
        body: { organizationId: org.id },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      // Verify the session row was updated
      const sessions = await getPool().query(
        "SELECT active_organization_id FROM session WHERE user_id = $1 AND token = $2",
        [user.id, token],
      );
      expect(sessions.rows).toHaveLength(1);
      expect(sessions.rows[0].active_organization_id).toBe(org.id);
    });

    it("should revoke a session (remove from database)", async () => {
      const { user, token } = await signUpAndVerify("q2-revoke@example.com", "Q2 Revoke");

      // Verify session exists
      const before = await getPool().query(
        "SELECT * FROM session WHERE user_id = $1 AND token = $2",
        [user.id, token],
      );
      expect(before.rows).toHaveLength(1);

      // Revoke the session
      await auth.api.revokeSession({
        body: { token },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      // Verify session was removed
      const after = await getPool().query(
        "SELECT * FROM session WHERE user_id = $1 AND token = $2",
        [user.id, token],
      );
      expect(after.rows).toHaveLength(0);
    });

    it("should support multiple concurrent sessions per user", async () => {
      const email = "q2-multi@example.com";
      const password = "SecurePass123!@#";
      await rawSignUp(email, "Q2 Multi");
      await getPool().query('UPDATE "user" SET email_verified = true WHERE email = $1', [email]);

      // Create two sessions
      const session1 = await auth.api.signInEmail({ body: { email, password } });
      const session2 = await auth.api.signInEmail({ body: { email, password } });

      const token1 = session1.token as string;
      const token2 = session2.token as string;

      expect(token1).not.toBe(token2);

      const sessions = await getPool().query("SELECT token FROM session WHERE user_id = $1", [
        session1.user.id,
      ]);
      expect(sessions.rows.length).toBeGreaterThanOrEqual(2);
    });

    it("should list active sessions for a user", async () => {
      const { token } = await signUpAndVerify("q2-list@example.com", "Q2 List");

      const sessions = await auth.api.listSessions({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      expect(sessions).toBeDefined();
      expect(Array.isArray(sessions)).toBe(true);
      expect(sessions.length).toBeGreaterThanOrEqual(1);
    });

    it("should revoke other sessions (keep current, remove rest)", async () => {
      const email = "q2-revoke-others@example.com";
      const password = "SecurePass123!@#";
      await rawSignUp(email, "Q2 Revoke Others");
      await getPool().query('UPDATE "user" SET email_verified = true WHERE email = $1', [email]);

      // Create two sessions
      await auth.api.signInEmail({ body: { email, password } });
      const session2 = await auth.api.signInEmail({ body: { email, password } });
      const activeToken = session2.token as string;

      // Revoke others from session2 (keeps session2 alive)
      await auth.api.revokeOtherSessions({
        headers: new Headers({ Authorization: `Bearer ${activeToken}` }),
      });

      // Only current session should remain
      const remaining = await auth.api.listSessions({
        headers: new Headers({ Authorization: `Bearer ${activeToken}` }),
      });
      expect(remaining).toHaveLength(1);
    });
  });

  // =========================================================================
  // Q3 — Session additionalFields behavior
  //
  // BetterAuth's setActiveOrganization only sets activeOrganizationId OOTB.
  // Our databaseHooks.session.update.before hook enriches the patch with
  // activeOrganizationType + activeOrganizationRole BEFORE BetterAuth
  // writes to DB/Redis/cookie, eliminating any staleness window.
  // =========================================================================
  describe("session additionalFields persistence", () => {
    it("should set activeOrganizationId after setActiveOrganization", async () => {
      const { user, token } = await signUpAndVerify("q3-orgid@example.com", "Q3 OrgId");

      const org = await createOrgAndActivate(token, "Q3 Org", "q3-org");

      const sessions = await getPool().query(
        `SELECT active_organization_id, active_organization_type, active_organization_role
         FROM session WHERE user_id = $1 AND token = $2`,
        [user.id, token],
      );
      expect(sessions.rows).toHaveLength(1);
      const sess = sessions.rows[0];

      // BetterAuth DOES set activeOrganizationId (OOTB org plugin)
      expect(sess.active_organization_id).toBe(org.id);
    });

    it("should auto-populate orgType and role after setActiveOrganization", async () => {
      const { user, token } = await signUpAndVerify("q3-fields@example.com", "Q3 Fields");
      await createOrgAndActivate(token, "Q3 Fields Org", "q3-fields-org");

      const sessions = await getPool().query(
        `SELECT active_organization_type, active_organization_role
         FROM session WHERE user_id = $1 AND token = $2`,
        [user.id, token],
      );
      expect(sessions.rows).toHaveLength(1);
      const sess = sessions.rows[0];

      // session.update.before hook enriches the session patch with org type + member role
      expect(sess.active_organization_type).toBe("customer");
      expect(sess.active_organization_role).toBe("owner");
    });

    it("should enrich session on org creation (auto-activate)", async () => {
      const { user, token } = await signUpAndVerify("q3-autocreate@example.com", "Q3 AutoCreate");

      // createOrganization auto-activates (BetterAuth sets activeOrganizationId
      // via internalAdapter.updateSession, which triggers our update.before hook)
      const org = await auth.api.createOrganization({
        body: { name: "Q3 AutoCreate Org", slug: "q3-autocreate-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const sessions = await getPool().query(
        `SELECT active_organization_id, active_organization_type, active_organization_role
         FROM session WHERE user_id = $1 AND token = $2`,
        [user.id, token],
      );
      expect(sessions.rows).toHaveLength(1);
      const sess = sessions.rows[0];

      expect(sess.active_organization_id).toBe(org.id);
      expect(sess.active_organization_type).toBe("customer");
      expect(sess.active_organization_role).toBe("owner");
    });

    it("should return custom session fields via getSession when populated", async () => {
      const { token } = await signUpAndVerify("q3-getsession@example.com", "Q3 GetSession");
      await createOrgAndActivate(token, "Q3 Session Org", "q3-session-org");

      // Hook auto-populates — no manual DB update needed
      const sessionRes = await auth.api.getSession({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      expect(sessionRes).toBeDefined();
      const session = sessionRes!.session as Record<string, unknown>;
      expect(session[SESSION_FIELDS.ACTIVE_ORG_TYPE]).toBe("customer");
      expect(session[SESSION_FIELDS.ACTIVE_ORG_ROLE]).toBe("owner");
    });

    it("should clear orgType and role when clearing active org", async () => {
      const { user, token } = await signUpAndVerify("q3-clear@example.com", "Q3 Clear");
      await createOrgAndActivate(token, "Q3 Clear Org", "q3-clear-org");

      // Verify enrichment happened
      const before = await getPool().query(
        `SELECT active_organization_type, active_organization_role
         FROM session WHERE user_id = $1 AND token = $2`,
        [user.id, token],
      );
      expect(before.rows[0].active_organization_type).toBe("customer");
      expect(before.rows[0].active_organization_role).toBe("owner");

      // Clear active org
      await auth.api.setActiveOrganization({
        body: { organizationId: null },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const after = await getPool().query(
        `SELECT active_organization_id, active_organization_type, active_organization_role
         FROM session WHERE user_id = $1 AND token = $2`,
        [user.id, token],
      );
      expect(after.rows[0].active_organization_id).toBeNull();
      expect(after.rows[0].active_organization_type).toBeNull();
      expect(after.rows[0].active_organization_role).toBeNull();
    });

    it("should return emulation fields as null when not emulating", async () => {
      const { token } = await signUpAndVerify("q3-no-emulation@example.com", "Q3 NoEmulation");
      await createOrgAndActivate(token, "Q3 NoEmul Org", "q3-noemul-org");

      const sessionRes = await auth.api.getSession({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const session = sessionRes!.session as Record<string, unknown>;
      expect(session[SESSION_FIELDS.EMULATED_ORG_ID]).toBeNull();
      expect(session[SESSION_FIELDS.EMULATED_ORG_TYPE]).toBeNull();
    });
  });

  // =========================================================================
  // Q4 — definePayload includes org context from session
  //
  // definePayload receives ({ user, session }) — confirmed working.
  // With session.update.before hook enriching the session, orgType/role
  // in JWTs are automatically populated after setActiveOrganization.
  // =========================================================================
  describe("JWT definePayload with org context", () => {
    it("should include orgId in JWT from BetterAuth session (OOTB)", async () => {
      const { user, token } = await signUpAndVerify("q4-orgid@example.com", "Q4 OrgId");
      const org = await createOrgAndActivate(token, "Q4 OrgId Org", "q4-orgid-org");

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const payload = JSON.parse(Buffer.from(tokenRes.token.split(".")[1], "base64url").toString());

      // Identity claims — always present
      expect(payload[JWT_CLAIM_TYPES.SUB]).toBe(user.id);
      expect(payload[JWT_CLAIM_TYPES.EMAIL]).toBe("q4-orgid@example.com");

      // BetterAuth populates activeOrganizationId — definePayload reads it
      expect(payload[JWT_CLAIM_TYPES.ORG_ID]).toBe(org.id);

      // session.update.before hook enriches session — definePayload reads enriched values
      expect(payload[JWT_CLAIM_TYPES.ORG_TYPE]).toBe("customer");
      expect(payload[JWT_CLAIM_TYPES.ROLE]).toBe("owner");
    });

    it("should include orgType and role in JWT when session fields are populated", async () => {
      const { token } = await signUpAndVerify("q4-full@example.com", "Q4 Full");
      const org = await createOrgAndActivate(token, "Q4 Full Org", "q4-full-org");

      // Hook auto-populates — no manual DB update needed
      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const payload = JSON.parse(Buffer.from(tokenRes.token.split(".")[1], "base64url").toString());

      // All org claims present — hook enriches session before cookie/DB write
      expect(payload[JWT_CLAIM_TYPES.ORG_ID]).toBe(org.id);
      expect(payload[JWT_CLAIM_TYPES.ORG_TYPE]).toBe("customer");
      expect(payload[JWT_CLAIM_TYPES.ROLE]).toBe("owner");

      // Emulation — inactive
      expect(payload[JWT_CLAIM_TYPES.EMULATED_ORG_ID]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.IS_EMULATING]).toBe(false);
    });

    it("should show null org claims in JWT when no active org", async () => {
      const { token } = await signUpAndVerify("q4-noorg@example.com", "Q4 NoOrg");

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const payload = JSON.parse(Buffer.from(tokenRes.token.split(".")[1], "base64url").toString());

      expect(payload[JWT_CLAIM_TYPES.ORG_ID]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.ORG_TYPE]).toBeNull();
      expect(payload[JWT_CLAIM_TYPES.ROLE]).toBeNull();
    });

    it("should update orgId in JWT when active org changes", async () => {
      const { token } = await signUpAndVerify("q4-switch@example.com", "Q4 Switch");

      const org1 = await auth.api.createOrganization({
        body: { name: "Q4 Org A", slug: "q4-org-a" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const org2 = await auth.api.createOrganization({
        body: { name: "Q4 Org B", slug: "q4-org-b" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      // Activate org1
      await auth.api.setActiveOrganization({
        body: { organizationId: org1.id },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const jwt1 = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const payload1 = JSON.parse(Buffer.from(jwt1.token.split(".")[1], "base64url").toString());
      expect(payload1[JWT_CLAIM_TYPES.ORG_ID]).toBe(org1.id);

      // Switch to org2
      await auth.api.setActiveOrganization({
        body: { organizationId: org2.id },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const jwt2 = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const payload2 = JSON.parse(Buffer.from(jwt2.token.split(".")[1], "base64url").toString());
      expect(payload2[JWT_CLAIM_TYPES.ORG_ID]).toBe(org2.id);

      // JWTs should be different (different org context)
      expect(jwt1.token).not.toBe(jwt2.token);
    });

    it("should receive both user and session in definePayload (verified by claim presence)", async () => {
      const { user, token } = await signUpAndVerify("q4-payload@example.com", "Q4 Payload");

      const tokenRes = await auth.api.getToken({
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });
      const payload = JSON.parse(Buffer.from(tokenRes.token.split(".")[1], "base64url").toString());

      // User-derived claims (proves user param works)
      expect(payload[JWT_CLAIM_TYPES.SUB]).toBe(user.id);
      expect(payload[JWT_CLAIM_TYPES.EMAIL]).toBe("q4-payload@example.com");

      // Session-derived claims (proves session param works — even if null)
      expect(JWT_CLAIM_TYPES.ORG_ID in payload).toBe(true);
      expect(JWT_CLAIM_TYPES.EMULATED_ORG_ID in payload).toBe(true);
      expect(JWT_CLAIM_TYPES.IS_EMULATING in payload).toBe(true);
    });
  });

  // =========================================================================
  // Q6 — snake_case column naming with all plugins
  // =========================================================================
  describe("snake_case column naming", () => {
    it("should use snake_case columns in user table", async () => {
      const { user } = await rawSignUp("q6-user@example.com", "Q6 User");

      // Query with snake_case column names — should succeed
      const result = await getPool().query(
        `SELECT id, email, name, email_verified, created_at, updated_at, image,
                role, banned, ban_reason, ban_expires, username, display_username
         FROM "user" WHERE id = $1`,
        [user.id],
      );
      expect(result.rows).toHaveLength(1);
      expect(result.rows[0].email_verified).toBe(false);
      expect(result.rows[0].created_at).toBeInstanceOf(Date);
    });

    it("should use snake_case columns in account table", async () => {
      const { user } = await rawSignUp("q6-account@example.com", "Q6 Account");

      const result = await getPool().query(
        `SELECT id, user_id, provider_id, account_id, access_token,
                refresh_token, id_token, access_token_expires_at,
                refresh_token_expires_at, scope, password, created_at, updated_at
         FROM account WHERE user_id = $1`,
        [user.id],
      );
      expect(result.rows).toHaveLength(1);
      expect(result.rows[0].provider_id).toBe("credential");
      expect(result.rows[0].user_id).toBe(user.id);
    });

    it("should use snake_case columns in session table (including custom fields)", async () => {
      const { user, token } = await signUpAndVerify("q6-session@example.com", "Q6 Session");

      const result = await getPool().query(
        `SELECT id, user_id, token, ip_address, user_agent, expires_at, created_at, updated_at,
                active_organization_id, active_organization_type, active_organization_role,
                emulated_organization_id, emulated_organization_type
         FROM session WHERE user_id = $1 AND token = $2`,
        [user.id, token],
      );
      expect(result.rows).toHaveLength(1);
      expect(result.rows[0].user_id).toBe(user.id);
      expect(result.rows[0].token).toBe(token);
    });

    it("should use snake_case columns in organization table (including orgType)", async () => {
      const { token } = await signUpAndVerify("q6-org@example.com", "Q6 Org");

      const org = await auth.api.createOrganization({
        body: { name: "Q6 Org", slug: "q6-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const result = await getPool().query(
        `SELECT id, name, slug, org_type, logo, metadata, created_at
         FROM organization WHERE id = $1`,
        [org.id],
      );
      expect(result.rows).toHaveLength(1);
      expect(result.rows[0].org_type).toBe("customer");
      expect(result.rows[0].slug).toBe("q6-org");
    });

    it("should use snake_case columns in member table", async () => {
      const { user, token } = await signUpAndVerify("q6-member@example.com", "Q6 Member");

      const org = await auth.api.createOrganization({
        body: { name: "Q6 Member Org", slug: "q6-member-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const result = await getPool().query(
        `SELECT id, user_id, organization_id, role, created_at
         FROM member WHERE organization_id = $1 AND user_id = $2`,
        [org.id, user.id],
      );
      expect(result.rows).toHaveLength(1);
      expect(result.rows[0].role).toBe("owner");
      expect(result.rows[0].organization_id).toBe(org.id);
    });

    it("should use snake_case columns in invitation table", async () => {
      const { token } = await signUpAndVerify("q6-invite-sender@example.com", "Q6 Inviter");

      const org = await auth.api.createOrganization({
        body: { name: "Q6 Invite Org", slug: "q6-invite-org" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      await auth.api.setActiveOrganization({
        body: { organizationId: org.id },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      await auth.api.createInvitation({
        body: {
          email: "q6-invitee@example.com",
          role: "agent",
          organizationId: org.id,
        },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      const result = await getPool().query(
        `SELECT id, email, organization_id, role, inviter_id, status, expires_at, created_at
         FROM invitation WHERE organization_id = $1`,
        [org.id],
      );
      expect(result.rows).toHaveLength(1);
      expect(result.rows[0].email).toBe("q6-invitee@example.com");
      expect(result.rows[0].organization_id).toBe(org.id);
      expect(result.rows[0].role).toBe("agent");
      expect(result.rows[0].status).toBe("pending");
    });
  });

  // =========================================================================
  // Q7 — Pre-generated user ID preserved (no forceAllowId flag)
  // =========================================================================
  describe("pre-generated user ID", () => {
    it("should preserve the ID set in the before hook (without forceAllowId)", async () => {
      let hookUserId = "";

      const authWithHook = createAuth(testConfig, getDb(), undefined, {
        ...hooks,
        createUserContact: async (data) => {
          hookUserId = data.userId;
        },
      });

      const res = await authWithHook.api.signUpEmail({
        body: {
          email: "q7-preserve@example.com",
          password: "SecurePass123!@#",
          name: "Q7 Preserve",
        },
      });

      // The hook received a userId, and the created user has the same one
      expect(hookUserId).toBe(res.user.id);

      // Verify in DB — same ID
      const result = await getPool().query('SELECT id FROM "user" WHERE email = $1', [
        "q7-preserve@example.com",
      ]);
      expect(result.rows[0].id).toBe(hookUserId);
    });

    it("should use UUIDv7 format for pre-generated IDs", async () => {
      let hookUserId = "";

      const authWithHook = createAuth(testConfig, getDb(), undefined, {
        ...hooks,
        createUserContact: async (data) => {
          hookUserId = data.userId;
        },
      });

      await authWithHook.api.signUpEmail({
        body: {
          email: "q7-format@example.com",
          password: "SecurePass123!@#",
          name: "Q7 Format",
        },
      });

      // UUIDv7 format: version nibble = 7
      expect(hookUserId).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      );
    });
  });

  // =========================================================================
  // Q8 — Password change hook (account.update.after)
  //
  // When a user changes their password, the account.update.after hook fires
  // with the updated account row (which includes the password hash field).
  // Our hook detects this and calls publishPasswordChanged.
  // =========================================================================
  describe("password change notification hook", () => {
    it("should invoke publishPasswordChanged callback when password is changed", async () => {
      const passwordChangedCalls: { userId: string; email: string; name: string }[] = [];

      const passwordFns = createPasswordFunctions(mockHibpCache);
      const hookAuth = createAuth(testConfig, getDb(), undefined, {
        passwordFunctions: passwordFns,
        publishPasswordChanged: async (input) => {
          passwordChangedCalls.push(input);
        },
      });

      const email = "q8-pw-change@example.com";
      const oldPassword = "SecurePass123!@#";
      const newPassword = "NewSecurePass456!@#";

      // Sign up and verify
      const signUpRes = await hookAuth.api.signUpEmail({
        body: { email, password: oldPassword, name: "Q8 PwChange" },
      });
      await getPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [
        signUpRes.user.id,
      ]);

      const signInRes = await hookAuth.api.signInEmail({
        body: { email, password: oldPassword },
      });
      const token =
        signInRes.token ??
        ((signInRes as Record<string, unknown>).session as { token: string })?.token;

      // Change password
      await hookAuth.api.changePassword({
        body: { currentPassword: oldPassword, newPassword },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      // The hook fires asynchronously — give it a moment to complete
      await new Promise((resolve) => setTimeout(resolve, 500));

      expect(passwordChangedCalls).toHaveLength(1);
      expect(passwordChangedCalls[0].userId).toBe(signUpRes.user.id);
      expect(passwordChangedCalls[0].email).toBe(email);
      expect(passwordChangedCalls[0].name).toBe("Q8 PwChange");
    });

    it("should NOT invoke publishPasswordChanged on regular user updates", async () => {
      const passwordChangedCalls: { userId: string }[] = [];

      const passwordFns = createPasswordFunctions(mockHibpCache);
      const hookAuth = createAuth(testConfig, getDb(), undefined, {
        passwordFunctions: passwordFns,
        publishPasswordChanged: async (input) => {
          passwordChangedCalls.push(input);
        },
      });

      const email = "q8-no-trigger@example.com";
      const password = "SecurePass123!@#";

      await hookAuth.api.signUpEmail({
        body: { email, password, name: "Q8 NoTrigger" },
      });
      await getPool().query('UPDATE "user" SET email_verified = true WHERE email = $1', [email]);

      const signInRes = await hookAuth.api.signInEmail({
        body: { email, password },
      });
      const token =
        signInRes.token ??
        ((signInRes as Record<string, unknown>).session as { token: string })?.token;

      // Update user name (not password) — should NOT trigger the hook
      await hookAuth.api.updateUser({
        body: { name: "Updated Name" },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      await new Promise((resolve) => setTimeout(resolve, 500));

      expect(passwordChangedCalls).toHaveLength(0);
    });

    it("should allow sign-in with new password after change", async () => {
      const passwordFns = createPasswordFunctions(mockHibpCache);
      const hookAuth = createAuth(testConfig, getDb(), undefined, {
        passwordFunctions: passwordFns,
      });

      const email = "q8-new-pw@example.com";
      const oldPassword = "SecurePass123!@#";
      const newPassword = "NewSecurePass456!@#";

      await hookAuth.api.signUpEmail({
        body: { email, password: oldPassword, name: "Q8 NewPw" },
      });
      await getPool().query('UPDATE "user" SET email_verified = true WHERE email = $1', [email]);

      const signInRes = await hookAuth.api.signInEmail({
        body: { email, password: oldPassword },
      });
      const token =
        signInRes.token ??
        ((signInRes as Record<string, unknown>).session as { token: string })?.token;

      await hookAuth.api.changePassword({
        body: { currentPassword: oldPassword, newPassword },
        headers: new Headers({ Authorization: `Bearer ${token}` }),
      });

      // Old password should fail
      await expect(
        hookAuth.api.signInEmail({ body: { email, password: oldPassword } }),
      ).rejects.toThrow();

      // New password should work
      const newSignIn = await hookAuth.api.signInEmail({
        body: { email, password: newPassword },
      });
      expect(newSignIn.user.id).toBeDefined();
    });
  });
});
