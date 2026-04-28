import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { PostgreSqlContainer, type StartedPostgreSqlContainer } from "@testcontainers/postgresql";
import pg from "pg";
import { runMigrations } from "@d2/auth-infra";

describe("Drizzle migrations (integration)", () => {
  let container: StartedPostgreSqlContainer;
  let pool: pg.Pool;

  beforeAll(async () => {
    container = await new PostgreSqlContainer("postgres:18").start();
    pool = new pg.Pool({ connectionString: container.getConnectionUri() });
  }, 120_000);

  afterAll(async () => {
    await pool?.end();
    await container?.stop();
  });

  it("should apply migrations to an empty database", async () => {
    await expect(runMigrations(pool)).resolves.not.toThrow();
  });

  it("should be idempotent (running twice does not error)", async () => {
    await expect(runMigrations(pool)).resolves.not.toThrow();
  });

  it("should create sign_in_event table with correct columns", async () => {
    const result = await pool.query(`
      SELECT column_name, data_type, is_nullable
      FROM information_schema.columns
      WHERE table_name = 'sign_in_event'
      ORDER BY ordinal_position
    `);

    const columns = result.rows.map((r) => r.column_name);
    expect(columns).toContain("id");
    expect(columns).toContain("user_id");
    expect(columns).toContain("successful");
    expect(columns).toContain("ip_address");
    expect(columns).toContain("user_agent");
    expect(columns).toContain("who_is_id");
    expect(columns).toContain("created_at");
  });

  it("should create emulation_consent table with correct columns", async () => {
    const result = await pool.query(`
      SELECT column_name, data_type, is_nullable
      FROM information_schema.columns
      WHERE table_name = 'emulation_consent'
      ORDER BY ordinal_position
    `);

    const columns = result.rows.map((r) => r.column_name);
    expect(columns).toContain("id");
    expect(columns).toContain("user_id");
    expect(columns).toContain("granted_to_org_id");
    expect(columns).toContain("expires_at");
    expect(columns).toContain("revoked_at");
    expect(columns).toContain("created_at");
  });

  it("should create org_contact table with correct columns", async () => {
    const result = await pool.query(`
      SELECT column_name, data_type, is_nullable
      FROM information_schema.columns
      WHERE table_name = 'org_contact'
      ORDER BY ordinal_position
    `);

    const columns = result.rows.map((r) => r.column_name);
    expect(columns).toContain("id");
    expect(columns).toContain("organization_id");
    expect(columns).toContain("label");
    expect(columns).toContain("is_primary");
    expect(columns).toContain("created_at");
    expect(columns).toContain("updated_at");
    // contact_id should NOT exist (dropped in migration)
    expect(columns).not.toContain("contact_id");
  });

  // ---- BetterAuth-managed tables ----

  it("should create user table with correct columns", async () => {
    const result = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'user' ORDER BY ordinal_position
    `);
    const columns = result.rows.map((r) => r.column_name);
    expect(columns).toContain("id");
    expect(columns).toContain("name");
    expect(columns).toContain("email");
    expect(columns).toContain("email_verified");
    expect(columns).toContain("image");
    expect(columns).toContain("created_at");
    expect(columns).toContain("updated_at");
    expect(columns).toContain("username");
    expect(columns).toContain("display_username");
    expect(columns).toContain("role");
    expect(columns).toContain("banned");
    expect(columns).toContain("ban_reason");
    expect(columns).toContain("ban_expires");
  });

  it("should enforce username uniqueness constraint", async () => {
    // Insert a user
    await pool.query(`
      INSERT INTO "user" (id, name, email, username, display_username, email_verified, created_at, updated_at)
      VALUES ('u1', 'User One', 'one@example.com', 'testuser1', 'TestUser1', false, NOW(), NOW())
    `);

    // Attempt duplicate username
    await expect(
      pool.query(`
        INSERT INTO "user" (id, name, email, username, display_username, email_verified, created_at, updated_at)
        VALUES ('u2', 'User Two', 'two@example.com', 'testuser1', 'TestUser2', false, NOW(), NOW())
      `),
    ).rejects.toThrow(/unique/i);
  });

  it("should enforce display_username uniqueness constraint", async () => {
    // Insert a user (may already exist from previous test, use different username)
    await pool.query(`
      INSERT INTO "user" (id, name, email, username, display_username, email_verified, created_at, updated_at)
      VALUES ('u3', 'User Three', 'three@example.com', 'uniqueuser3', 'DisplayUser3', false, NOW(), NOW())
    `);

    // Attempt duplicate display_username
    await expect(
      pool.query(`
        INSERT INTO "user" (id, name, email, username, display_username, email_verified, created_at, updated_at)
        VALUES ('u4', 'User Four', 'four@example.com', 'uniqueuser4', 'DisplayUser3', false, NOW(), NOW())
      `),
    ).rejects.toThrow(/unique/i);
  });

  it("should create session table with custom fields", async () => {
    const result = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'session' ORDER BY ordinal_position
    `);
    const columns = result.rows.map((r) => r.column_name);
    expect(columns).toContain("id");
    expect(columns).toContain("token");
    expect(columns).toContain("user_id");
    expect(columns).toContain("expires_at");
    expect(columns).toContain("ip_address");
    expect(columns).toContain("user_agent");
    expect(columns).toContain("active_organization_id");
    expect(columns).toContain("impersonated_by");
    // Custom session fields
    expect(columns).toContain("active_organization_type");
    expect(columns).toContain("active_organization_role");
    expect(columns).toContain("emulated_organization_id");
    expect(columns).toContain("emulated_organization_type");
  });

  it("should create account table with correct columns", async () => {
    const result = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'account' ORDER BY ordinal_position
    `);
    const columns = result.rows.map((r) => r.column_name);
    expect(columns).toContain("id");
    expect(columns).toContain("account_id");
    expect(columns).toContain("provider_id");
    expect(columns).toContain("user_id");
    expect(columns).toContain("password");
  });

  it("should create organization table with custom orgType field", async () => {
    const result = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'organization' ORDER BY ordinal_position
    `);
    const columns = result.rows.map((r) => r.column_name);
    expect(columns).toContain("id");
    expect(columns).toContain("name");
    expect(columns).toContain("slug");
    expect(columns).toContain("org_type");
  });

  it("should create member and invitation tables", async () => {
    const memberResult = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'member' ORDER BY ordinal_position
    `);
    const memberColumns = memberResult.rows.map((r) => r.column_name);
    expect(memberColumns).toContain("id");
    expect(memberColumns).toContain("organization_id");
    expect(memberColumns).toContain("user_id");
    expect(memberColumns).toContain("role");

    const invResult = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'invitation' ORDER BY ordinal_position
    `);
    const invColumns = invResult.rows.map((r) => r.column_name);
    expect(invColumns).toContain("id");
    expect(invColumns).toContain("organization_id");
    expect(invColumns).toContain("email");
    expect(invColumns).toContain("inviter_id");
    expect(invColumns).toContain("status");
  });

  it("should create jwks and verification tables", async () => {
    const jwksResult = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'jwks' ORDER BY ordinal_position
    `);
    const jwksColumns = jwksResult.rows.map((r) => r.column_name);
    expect(jwksColumns).toContain("id");
    expect(jwksColumns).toContain("public_key");
    expect(jwksColumns).toContain("private_key");

    const verResult = await pool.query(`
      SELECT column_name FROM information_schema.columns
      WHERE table_name = 'verification' ORDER BY ordinal_position
    `);
    const verColumns = verResult.rows.map((r) => r.column_name);
    expect(verColumns).toContain("id");
    expect(verColumns).toContain("identifier");
    expect(verColumns).toContain("value");
  });

  // ---- Indexes ----

  it("should create expected custom table indexes", async () => {
    const result = await pool.query(`
      SELECT indexname FROM pg_indexes
      WHERE schemaname = 'public'
        AND indexname LIKE 'idx_%'
      ORDER BY indexname
    `);

    const indexNames = result.rows.map((r) => r.indexname);
    // Migration 0010_sign_in_event_composite_idx.sql DROPped the
    // single-column `idx_sign_in_event_user_id` and replaced it with a
    // composite (user_id, created_at DESC) for paginated queries.
    expect(indexNames).toContain("idx_sign_in_event_user_id_created_at");
    expect(indexNames).toContain("idx_emulation_consent_user_id");
    expect(indexNames).toContain("idx_emulation_consent_active_unique");
    expect(indexNames).toContain("idx_org_contact_organization_id");
  });

  it("should create expected BetterAuth table indexes", async () => {
    const result = await pool.query(`
      SELECT indexname FROM pg_indexes
      WHERE schemaname = 'public'
      ORDER BY indexname
    `);

    const indexNames = result.rows.map((r) => r.indexname);
    expect(indexNames).toContain("account_user_id_idx");
    expect(indexNames).toContain("session_user_id_idx");
    expect(indexNames).toContain("organization_slug_idx");
    expect(indexNames).toContain("member_organization_id_idx");
    expect(indexNames).toContain("member_user_id_idx");
    expect(indexNames).toContain("invitation_organization_id_idx");
    expect(indexNames).toContain("invitation_email_idx");
    expect(indexNames).toContain("verification_identifier_idx");
  });

  it("should enforce partial unique index on emulation_consent", async () => {
    // Insert two active consents with same user_id + granted_to_org_id
    await pool.query(`
      INSERT INTO emulation_consent (id, user_id, granted_to_org_id, expires_at, created_at)
      VALUES ('id-1', 'user-1', 'org-1', NOW() + INTERVAL '1 day', NOW())
    `);

    await expect(
      pool.query(`
        INSERT INTO emulation_consent (id, user_id, granted_to_org_id, expires_at, created_at)
        VALUES ('id-2', 'user-1', 'org-1', NOW() + INTERVAL '1 day', NOW())
      `),
    ).rejects.toThrow(/unique/i);
  });
});
