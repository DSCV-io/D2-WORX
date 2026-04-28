import { Hono } from "hono";
import { eq } from "drizzle-orm";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { TK, resolveLocale } from "@d2/i18n";
import type { Translator } from "@d2/i18n";
import { ILoggerKey } from "@d2/logging";
import { cleanDisplayStr } from "@d2/utilities";
import { SESSION_FIELDS, GEO_CONTEXT_KEYS, ROLES, type Role } from "@d2/auth-domain";
import { INotifyKey } from "@d2/comms-client";
import { ICreateContactsKey, IGetContactsByExtKeysKey } from "@d2/geo-client";
import type { Auth } from "@d2/auth-infra";
import { user as userTable, organization as orgTable } from "@d2/auth-infra";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import type { SessionVariables } from "../middleware/session.js";
import { type ScopeVariables } from "../middleware/scope.js";
import { SCOPE_KEY, SESSION_KEY, USER_KEY } from "../context-keys.js";
import { requireOrg, requireRole } from "@d2/auth-policy";

/** Roles that may be assigned via invitation. */
const INVITABLE_ROLES = new Set<Role>(ROLES);

/**
 * Maps each inviter role to the set of roles they are allowed to assign.
 * - Owners can invite: officer, agent, auditor (NOT other owners)
 * - Officers can invite: agent, auditor (NOT owners or other officers)
 */
const INVITATION_HIERARCHY: Readonly<Partial<Record<Role, readonly Role[]>>> = {
  owner: ["officer", "agent", "auditor"],
  officer: ["agent", "auditor"],
};

/** Max lengths for string input fields — derived from Geo DB schema (ContactConfig.cs). */
const MAX_FIRST_NAME = 255;
const MAX_LAST_NAME = 255;
const MAX_PHONE = 20;

/**
 * Custom invitation route that replaces BetterAuth's `sendInvitationEmail` callback.
 *
 * Flow:
 *   1. Validate input (email, role, optional contact details)
 *   2. Look up user by email in auth DB
 *   3. Call BetterAuth `createInvitation` → get invitationId
 *   4. If user NOT found: create Geo contact for the invitee
 *   5. Publish invitation email event with inviteeUserId or inviteeContactId
 *   6. Return { invitationId }
 */
export interface InvitationRoutesOptions {
  auth: Auth;
  db: NodePgDatabase;
  baseUrl: string;
  translator: Translator;
  /** Public-facing base URL for email links. Falls back to baseUrl if not set. */
  emailBaseUrl?: string;
}

export function createInvitationRoutes(options: InvitationRoutesOptions) {
  const { auth, db, baseUrl, translator, emailBaseUrl } = options;
  const app = new Hono<{ Variables: SessionVariables & ScopeVariables }>();

  app.post("/api/invitations", requireOrg(), requireRole("officer"), async (c) => {
    const body = await c.req.json();

    // 1. Validate input
    const email = (body.email as string | undefined)?.trim();
    const role = body.role as string | undefined;
    const firstName = cleanDisplayStr((body.firstName as string) ?? undefined);
    const lastName = cleanDisplayStr((body.lastName as string) ?? undefined);
    const phone = ((body.phone as string) ?? "").trim() || undefined;

    if (!email || !role) {
      const inputErrors: [string, ...string[]][] = [];
      if (!email) inputErrors.push(["email", TK.auth.errors.EMAIL_REQUIRED]);
      if (!role) inputErrors.push(["role", TK.auth.errors.ROLE_REQUIRED]);
      return c.json(D2Result.validationFailed({ inputErrors }), 400 as ContentfulStatusCode);
    }
    if (!INVITABLE_ROLES.has(role as Role)) {
      return c.json(
        D2Result.validationFailed({
          inputErrors: [["role", TK.auth.errors.INVALID_ROLE]],
        }),
        400 as ContentfulStatusCode,
      );
    }

    // Reject inputs exceeding max length instead of silently truncating
    {
      const lengthErrors: [string, ...string[]][] = [];
      if (firstName && firstName.length > MAX_FIRST_NAME) {
        lengthErrors.push(["firstName", TK.common.errors.VALIDATION_FAILED]);
      }
      if (lastName && lastName.length > MAX_LAST_NAME) {
        lengthErrors.push(["lastName", TK.common.errors.VALIDATION_FAILED]);
      }
      if (phone && phone.length > MAX_PHONE) {
        lengthErrors.push(["phone", TK.common.errors.VALIDATION_FAILED]);
      }
      if (lengthErrors.length > 0) {
        return c.json(
          D2Result.validationFailed({ inputErrors: lengthErrors }),
          400 as ContentfulStatusCode,
        );
      }
    }

    const session = c.get(SESSION_KEY)!;
    const organizationId = session[SESSION_FIELDS.ACTIVE_ORG_ID] as string;
    const inviter = c.get(USER_KEY)!;

    // 1b. Enforce role hierarchy — inviter can only assign subordinate roles
    // Safe cast: requireRole("officer") middleware already validated this is a valid Role.
    const inviterRole = session[SESSION_FIELDS.ACTIVE_ORG_ROLE] as Role;
    const allowedRoles = INVITATION_HIERARCHY[inviterRole];
    if (!allowedRoles || !allowedRoles.includes(role as Role)) {
      return c.json(
        D2Result.forbidden({
          messages: [TK.auth.errors.INVITATION_ROLE_HIERARCHY],
        }),
        403 as ContentfulStatusCode,
      );
    }

    // 2. Look up user by email
    const existingUsers = await db
      .select({ id: userTable.id })
      .from(userTable)
      .where(eq(userTable.email, email.toLowerCase()))
      .limit(1);
    const existingUser = existingUsers[0];

    // 3. Call BetterAuth createInvitation
    let invitationId: string;
    try {
      const invitationResult = (await auth.api.createInvitation({
        headers: c.req.raw.headers,
        body: {
          email,
          organizationId,
          role: role as "owner" | "officer" | "agent" | "auditor",
        },
      })) as { id: string };
      invitationId = invitationResult.id;
    } catch (err) {
      // Log error type for debugging but never log the message (may contain sensitive data)
      try {
        const scope = c.get(SCOPE_KEY);
        const logger = scope.resolve(ILoggerKey);
        logger.warn("Invitation creation failed", {
          errorType: err instanceof Error ? err.constructor.name : "Unknown",
        });
      } catch {
        // Logging is best-effort — never let it break the error response
      }
      return c.json(
        D2Result.validationFailed({
          messages: [TK.auth.errors.INVITATION_CREATION_FAILED],
        }),
        400 as ContentfulStatusCode,
      );
    }

    // 4. If user NOT found, create a Geo contact for the invitee
    let inviteeContactId: string | undefined;
    if (!existingUser) {
      const scope = c.get(SCOPE_KEY);
      const createContacts = scope.resolve(ICreateContactsKey);

      const contactResult = await createContacts.handleAsync({
        contacts: [
          {
            createdAt: new Date(),
            contextKey: GEO_CONTEXT_KEYS.ORG_INVITATION,
            relatedEntityId: invitationId,
            contactMethods: {
              emails: [{ value: email, labels: [] }],
              phoneNumbers: phone ? [{ value: phone, labels: [] }] : [],
            },
            personalDetails:
              firstName || lastName
                ? {
                    firstName,
                    lastName,
                    professionalCredentials: [],
                  }
                : undefined,
            professionalDetails: undefined,
            location: undefined,
          },
        ],
      });

      if (contactResult.success && contactResult.data?.data[0]) {
        inviteeContactId = contactResult.data.data[0].id;
      }
    }

    // 5. Look up org name for the email
    const orgs = await db
      .select({ name: orgTable.name })
      .from(orgTable)
      .where(eq(orgTable.id, organizationId))
      .limit(1);
    const orgName = orgs[0]?.name ?? "the organization";

    // 6. Send invitation notification via comms-client
    // Use emailBaseUrl for public-facing links (falls back to baseUrl)
    const linkBase = emailBaseUrl ?? baseUrl;
    const invitationUrl = `${linkBase}/api/auth/organization/accept-invitation?invitationId=${invitationId}`;
    const inviterName = inviter.name ?? "Someone";

    // Resolve the recipient contactId — either from the Geo contact we just created,
    // or from the existing user's Geo contact (via ext-keys lookup)
    let recipientContactId = inviteeContactId;
    if (!recipientContactId && existingUser) {
      // Existing user — look up their Geo contact via ext-keys
      const scope2 = c.get(SCOPE_KEY);
      const getContactsByExtKeys = scope2.resolve(IGetContactsByExtKeysKey);
      const lookupResult = await getContactsByExtKeys.handleAsync({
        keys: [{ contextKey: GEO_CONTEXT_KEYS.USER, relatedEntityId: existingUser.id }],
      });
      const lookupKey = `${GEO_CONTEXT_KEYS.USER}:${existingUser.id}`;
      recipientContactId = lookupResult.data?.data.get(lookupKey)?.[0]?.id;
    }

    if (recipientContactId) {
      const scope3 = c.get(SCOPE_KEY);
      const notifier = scope3.resolve(INotifyKey);
      const t = translator.t;
      const locale = resolveLocale(undefined);
      const interpolation = {
        orgName,
        inviterName,
        inviterEmail: inviter.email,
        role: role as string,
        url: invitationUrl,
      };

      await notifier.handleAsync({
        recipientContactId,
        title: t(locale, "auth_email_invitation_subject", interpolation),
        content: [
          t(locale, "auth_email_invitation_greeting"),
          "",
          t(locale, "auth_email_invitation_body", interpolation),
          "",
          `[${t(locale, "auth_email_invitation_action")}](${invitationUrl})`,
          "",
          t(locale, "auth_email_link_fallback"),
          invitationUrl,
        ].join("\n"),
        plaintext: t(locale, "auth_email_invitation_plaintext", interpolation),
        channels: ["email"],
        correlationId: invitationId,
        senderService: "auth",
      });
    }

    return c.json(D2Result.ok({ data: { invitationId } }), 201 as ContentfulStatusCode);
  });

  return app;
}
