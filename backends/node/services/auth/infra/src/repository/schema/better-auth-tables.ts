import { sql } from "drizzle-orm";
import { pgTable, text, boolean, timestamp, jsonb, index, uniqueIndex } from "drizzle-orm/pg-core";

/**
 * Drizzle schema for BetterAuth-managed tables.
 *
 * Generated from BetterAuth's internal schema (getAuthTables) with plugins:
 * bearer, jwt, organization, access (RBAC), admin/impersonation.
 *
 * Column naming follows the BetterAuth CLI convention:
 *   - JS property: camelCase  (e.g., emailVerified)
 *   - DB column:   snake_case (e.g., email_verified)
 *
 * Custom fields from auth-factory.ts config are included:
 *   session: activeOrganizationType, activeOrganizationRole,
 *            emulatedOrganizationId, emulatedOrganizationType
 *   organization: orgType
 */

// ---- Core Tables ----

export const user = pgTable(
  "user",
  {
    id: text("id").primaryKey(),
    name: text("name").notNull(),
    email: text("email").notNull().unique(),
    emailVerified: boolean("email_verified").notNull().default(false),
    image: text("image"),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    updatedAt: timestamp("updated_at").notNull().defaultNow(),
    // Username plugin
    username: text("username").notNull().unique(),
    displayUsername: text("display_username").notNull().unique(),
    // i18n
    locale: text("locale").default("en-US"),
    timezone: text("timezone").default("America/New_York"),
    // Phone (digits-only E.164 like Geo). Verified flag tracks OTP confirmation.
    phone: text("phone"),
    phoneVerified: boolean("phone_verified").notNull().default(false),
    // Admin plugin
    role: text("role"),
    banned: boolean("banned").default(false),
    banReason: text("ban_reason"),
    banExpires: timestamp("ban_expires"),
    // Account lifecycle: 'active' | 'pending_deletion' | 'deleted'.
    // Banned is orthogonal (admin plugin's own boolean); status drives the
    // self-service deletion flow. `deleted_at` is the grace clock for
    // pending_deletion (independent of `updated_at` so normal mutations
    // don't reset it). On full anonymization it's repurposed to record the
    // anonymization timestamp.
    status: text("status").notNull().default("active"),
    deletedAt: timestamp("deleted_at"),
    deletionFeedback: jsonb("deletion_feedback"),
  },
  (t) => [
    // Partial unique index: enforces one user per phone, allows multiple null phones.
    uniqueIndex("user_phone_unique")
      .on(t.phone)
      .where(sql`${t.phone} IS NOT NULL`),
    // Partial index for the nightly purge job — only indexes pending users.
    index("user_pending_deletion_idx")
      .on(t.deletedAt)
      .where(sql`${t.status} = 'pending_deletion'`),
  ],
);

export const session = pgTable(
  "session",
  {
    id: text("id").primaryKey(),
    expiresAt: timestamp("expires_at").notNull(),
    token: text("token").notNull().unique(),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    updatedAt: timestamp("updated_at").notNull(),
    ipAddress: text("ip_address"),
    userAgent: text("user_agent"),
    userId: text("user_id")
      .notNull()
      .references(() => user.id, { onDelete: "cascade" }),
    // Organization plugin
    activeOrganizationId: text("active_organization_id"),
    // Admin/impersonation plugin
    impersonatedBy: text("impersonated_by"),
    // Custom session fields (additionalFields in auth-factory.ts)
    activeOrganizationType: text("active_organization_type"),
    activeOrganizationRole: text("active_organization_role"),
    emulatedOrganizationId: text("emulated_organization_id"),
    emulatedOrganizationType: text("emulated_organization_type"),
    // Resolved at sign-in via cross-service Geo FindWhoIs (async, fail-open).
    // Populated by the WhoIs resolution consumer ~milliseconds after the row is
    // inserted. Null until resolved (or if resolution fails). References the
    // content-addressable Geo WhoIs hash — frontend re-hydrates via Geo client.
    whoIsId: text("who_is_id"),
  },
  (table) => [index("session_user_id_idx").on(table.userId)],
);

export const account = pgTable(
  "account",
  {
    id: text("id").primaryKey(),
    accountId: text("account_id").notNull(),
    providerId: text("provider_id").notNull(),
    userId: text("user_id")
      .notNull()
      .references(() => user.id, { onDelete: "cascade" }),
    accessToken: text("access_token"),
    refreshToken: text("refresh_token"),
    idToken: text("id_token"),
    accessTokenExpiresAt: timestamp("access_token_expires_at"),
    refreshTokenExpiresAt: timestamp("refresh_token_expires_at"),
    scope: text("scope"),
    password: text("password"),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    updatedAt: timestamp("updated_at").notNull(),
  },
  (table) => [index("account_user_id_idx").on(table.userId)],
);

export const verification = pgTable(
  "verification",
  {
    id: text("id").primaryKey(),
    identifier: text("identifier").notNull(),
    value: text("value").notNull(),
    expiresAt: timestamp("expires_at").notNull(),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    updatedAt: timestamp("updated_at").notNull().defaultNow(),
  },
  (table) => [index("verification_identifier_idx").on(table.identifier)],
);

// ---- JWT Plugin ----

export const jwks = pgTable("jwks", {
  id: text("id").primaryKey(),
  publicKey: text("public_key").notNull(),
  privateKey: text("private_key").notNull(),
  createdAt: timestamp("created_at").notNull(),
  expiresAt: timestamp("expires_at"),
});

// ---- Organization Plugin ----

export const organization = pgTable(
  "organization",
  {
    id: text("id").primaryKey(),
    name: text("name").notNull(),
    slug: text("slug").notNull().unique(),
    logo: text("logo"),
    createdAt: timestamp("created_at").notNull(),
    updatedAt: timestamp("updated_at").notNull().defaultNow(),
    metadata: text("metadata"),
    // Custom organization field (additionalFields in auth-factory.ts)
    orgType: text("org_type").default("customer"),
  },
  (table) => [index("organization_slug_idx").on(table.slug)],
);

export const member = pgTable(
  "member",
  {
    id: text("id").primaryKey(),
    organizationId: text("organization_id")
      .notNull()
      .references(() => organization.id, { onDelete: "cascade" }),
    userId: text("user_id")
      .notNull()
      .references(() => user.id, { onDelete: "cascade" }),
    role: text("role").notNull().default("member"),
    createdAt: timestamp("created_at").notNull(),
  },
  (table) => [
    index("member_organization_id_idx").on(table.organizationId),
    index("member_user_id_idx").on(table.userId),
    // Composite (organization_id, role) — backs the correlated COUNT(*) in
    // CheckSoleOwnerOrgs so the per-org "are you the only owner?" check
    // stays an index-only scan even on large orgs.
    index("member_organization_role_idx").on(table.organizationId, table.role),
  ],
);

export const invitation = pgTable(
  "invitation",
  {
    id: text("id").primaryKey(),
    organizationId: text("organization_id")
      .notNull()
      .references(() => organization.id, { onDelete: "cascade" }),
    email: text("email").notNull(),
    role: text("role"),
    status: text("status").notNull().default("pending"),
    expiresAt: timestamp("expires_at").notNull(),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    inviterId: text("inviter_id")
      .notNull()
      .references(() => user.id, { onDelete: "cascade" }),
  },
  (table) => [
    index("invitation_organization_id_idx").on(table.organizationId),
    index("invitation_email_idx").on(table.email),
  ],
);
