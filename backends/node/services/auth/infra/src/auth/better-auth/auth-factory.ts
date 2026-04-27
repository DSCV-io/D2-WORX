import { AsyncLocalStorage } from "node:async_hooks";
import { betterAuth, APIError } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { bearer } from "better-auth/plugins/bearer";
import { jwt } from "better-auth/plugins/jwt";
import { admin } from "better-auth/plugins/admin";
import { organization } from "better-auth/plugins/organization";
import { username } from "better-auth/plugins/username";
import type { SecondaryStorage } from "better-auth";
import { eq, and } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { JWT_CLAIM_TYPES, SESSION_FIELDS, USER_STATUS } from "@d2/auth-domain";
import { BASE_LOCALE, TK } from "@d2/i18n";

/**
 * Per-request sign-up preferences (locale + timezone from cookies).
 *
 * BetterAuth's databaseHooks don't have access to the HTTP request, so
 * we stash cookie values here via Hono middleware and read them in the
 * user.create.before hook. Exported for use by the middleware.
 */
export interface SignUpPreferences {
  locale?: string;
  timezone?: string;
}

export const signUpPrefsStorage = new AsyncLocalStorage<SignUpPreferences>();
import type { AuthServiceConfig } from "./auth-config.js";
import { AUTH_CONFIG_DEFAULTS } from "./auth-config.js";
import { generateId } from "./hooks/id-hooks.js";
import { beforeCreateOrganization } from "./hooks/org-hooks.js";
import { ensureUsername } from "./hooks/username-hooks.js";
import {
  ac,
  ownerPermissions,
  officerPermissions,
  agentPermissions,
  auditorPermissions,
} from "./access-control.js";
import * as betterAuthSchema from "../../repository/schema/better-auth-tables.js";

/**
 * Callback interface for app-layer hooks.
 *
 * The auth-infra package does not import from auth-app. Instead,
 * the API layer (composition root) provides these callbacks when
 * creating the BetterAuth instance, enabling auth-infra to trigger
 * app-layer logic without a circular dependency.
 */
/** Minimal logger for BetterAuth callback hooks (avoids scope/DI dependency). */
export interface AuthHooksLogger {
  warn(msg: string, ...args: unknown[]): void;
  debug(msg: string, ...args: unknown[]): void;
}

export interface AuthHooks {
  /** Structured logger for BetterAuth callback error logging. Falls back to console if not provided. */
  logger?: AuthHooksLogger;
  /** Called after a successful sign-in to record audit events. */
  onSignIn?: (data: {
    userId: string;
    sessionId: string;
    ipAddress: string;
    userAgent: string;
    deviceFingerprint?: string;
    clientFingerprint?: string;
    serverFingerprint?: string;
  }) => Promise<void>;
  /**
   * Returns the client fingerprint for the current request.
   * Used by `definePayload` to embed an `fp` claim in JWTs, enabling
   * gateway-side validation that the JWT is being used by the same client.
   * Typically backed by `AsyncLocalStorage` in the composition root.
   */
  getFingerprintForCurrentRequest?: () => string | undefined;
  /**
   * Returns the device fingerprint for the current request (from enrichment middleware).
   * Combined hash: sha256(clientFp + serverFp + clientIp). Typically backed by
   * `AsyncLocalStorage` in the composition root.
   */
  getDeviceFingerprintForCurrentRequest?: () => string | undefined;
  /**
   * Returns the client (hardware/browser) fingerprint for the current request.
   * Stable across networks — derived from canvas/WebGL/timezone/etc on the
   * browser and forwarded as the `d2-cfp` cookie or `X-Client-Fingerprint`
   * header.
   */
  getClientFingerprintForCurrentRequest?: () => string | undefined;
  /**
   * Returns the server (network) fingerprint for the current request.
   * Derived from request headers (UA + accept headers + IP class) — changes
   * when the user roams networks.
   */
  getServerFingerprintForCurrentRequest?: () => string | undefined;
  /**
   * Custom password hash/verify functions with domain validation + HIBP checks.
   * Created by `createPasswordFunctions()` in the composition root.
   */
  passwordFunctions?: {
    hash: (password: string) => Promise<string>;
    verify: (data: { hash: string; password: string }) => Promise<boolean>;
  };
  /**
   * Publishes a verification email event to RabbitMQ for the comms service.
   * Called by BetterAuth's `emailVerification.sendVerificationEmail` callback.
   */
  publishVerificationEmail?: (input: {
    userId: string;
    email: string;
    name: string;
    verificationUrl: string;
    token: string;
  }) => Promise<void>;
  /**
   * Publishes a password reset email event to RabbitMQ for the comms service.
   * Called by BetterAuth's `emailAndPassword.sendResetPassword` callback.
   */
  publishPasswordReset?: (input: {
    userId: string;
    email: string;
    name: string;
    resetUrl: string;
    token: string;
  }) => Promise<void>;
  /**
   * Publishes a password-changed security notification email.
   * Called in databaseHooks.account.update.after when a password hash changes.
   * Fire-and-forget — password change is already committed.
   */
  publishPasswordChanged?: (input: {
    userId: string;
    email: string;
    name: string;
  }) => Promise<void>;
  /**
   * Creates a Geo contact for a newly registered user.
   * Called in databaseHooks.user.create.before (Contact BEFORE User pattern).
   * If this throws, sign-up fails entirely (fail-fast — no stale users).
   */
  createUserContact?: (data: {
    userId: string;
    email: string;
    name: string;
    locale: string;
    timezone: string;
  }) => Promise<void>;
  /**
   * Cancels a pending user deletion when an account in the grace window
   * signs back in successfully. Invoked from `session.create.before` —
   * fire-and-forget so a downstream failure (Comms email send, etc.)
   * does not block the session creation.
   *
   * The implementation is in auth-app (CancelUserDeletion handler); the
   * composition root wires it via a fresh DI scope per call.
   */
  cancelUserDeletion?: (data: { userId: string }) => Promise<void>;
  /**
   * Busts the Redis session-cache + pushes a `user:updated` SignalR event
   * after a password change. The frontend's root-layout listener picks up
   * the event, calls `bustSessionCache()` + `invalidateAll()`, and every
   * open tab refreshes its data — including the security tab's active
   * sessions list (which shrinks if `revokeOtherSessions=true`).
   *
   * Fire-and-forget — password change is already committed before this fires.
   * Without this, components that mutate password-related state would have
   * to call `invalidateAll()` themselves (which CLAUDE.md §5 SvelteKit
   * forbids — single source of truth for cache-bust is the SignalR event).
   */
  invalidateAndPushUserUpdated?: (input: { userId: string }) => Promise<void>;
}

/**
 * Creates a fully configured BetterAuth instance.
 *
 * This is the single place where BetterAuth is configured with all
 * plugins, hooks, and session settings. The returned auth instance
 * is used by the API layer to handle requests.
 *
 * @param config - Auth service configuration
 * @param db - Drizzle database instance (owned by the composition root)
 * @param secondaryStorage - Optional Redis-backed secondary storage
 * @param hooks - Optional app-layer callbacks for cross-layer events
 */
export function createAuth(
  config: AuthServiceConfig,
  db: NodePgDatabase,
  secondaryStorage?: SecondaryStorage,
  hooks?: AuthHooks,
) {
  const log: AuthHooksLogger = hooks?.logger ?? { warn: console.warn, debug: console.debug };
  const sessionExpiresIn = config.sessionExpiresIn ?? AUTH_CONFIG_DEFAULTS.sessionExpiresIn;
  const sessionUpdateAge = config.sessionUpdateAge ?? AUTH_CONFIG_DEFAULTS.sessionUpdateAge;
  const cookieCacheMaxAge = config.cookieCacheMaxAge ?? AUTH_CONFIG_DEFAULTS.cookieCacheMaxAge;
  const jwtExpirationSeconds =
    config.jwtExpirationSeconds ?? AUTH_CONFIG_DEFAULTS.jwtExpirationSeconds;

  /** Rewrites a BetterAuth-generated URL to use emailBaseUrl (if configured). */
  function rewriteEmailUrl(url: string): URL {
    const parsed = new URL(url);
    if (config.emailBaseUrl) {
      const publicBase = new URL(config.emailBaseUrl);
      parsed.protocol = publicBase.protocol;
      parsed.hostname = publicBase.hostname;
      parsed.port = publicBase.port;
    }
    return parsed;
  }
  const jwksRotationDays = config.jwksRotationDays ?? AUTH_CONFIG_DEFAULTS.jwksRotationDays;

  const auth = betterAuth({
    baseURL: config.baseUrl,
    basePath: "/api/auth",

    database: drizzleAdapter(db, {
      provider: "pg",
      schema: betterAuthSchema,
    }),

    secondaryStorage,

    // We use the verification table for our own account-change OTP records
    // (RequestEmailChange, RequestPhoneChange). Records persist in Postgres
    // so that updateValue (attempts increment) and id-based lookups work.
    // Without this, BetterAuth omits "verification" from its internal schema
    // when secondaryStorage is set, breaking any DB-backed verification ops.
    verification: { disableCleanup: false, storeInDatabase: true },

    emailAndPassword: {
      enabled: true,
      autoSignIn: false,
      requireEmailVerification: true,
      minPasswordLength: config.passwordMinLength ?? AUTH_CONFIG_DEFAULTS.passwordMinLength,
      maxPasswordLength: config.passwordMaxLength ?? AUTH_CONFIG_DEFAULTS.passwordMaxLength,
      password: hooks?.passwordFunctions,
      sendResetPassword: hooks?.publishPasswordReset
        ? async ({ user, url, token }) => {
            const rewritten = rewriteEmailUrl(url);
            rewritten.pathname = "/reset-password";
            rewritten.search = "";
            rewritten.searchParams.set("token", token);
            await hooks.publishPasswordReset!({
              userId: user.id,
              email: user.email,
              name: user.name ?? "User",
              resetUrl: rewritten.toString(),
              token,
            });
          }
        : undefined,
    },

    emailVerification: {
      sendOnSignUp: true,
      sendOnSignIn: true,
      autoSignInAfterVerification: true,
      sendVerificationEmail: hooks?.publishVerificationEmail
        ? async ({ user, url, token }) => {
            try {
              const rewritten = rewriteEmailUrl(url);
              rewritten.searchParams.set("callbackURL", "/auth/email-verified");
              await hooks.publishVerificationEmail!({
                userId: user.id,
                email: user.email,
                name: user.name ?? "User",
                verificationUrl: rewritten.toString(),
                token,
              });
            } catch (err: unknown) {
              // Fail-open: RabbitMQ down shouldn't crash sign-in/sign-up.
              // BetterAuth awaits this callback — if it throws, the entire flow
              // fails with 500. The user can re-trigger via sign-in (sendOnSignIn: true).
              log.warn("sendVerificationEmail: failed (fail-open)", {
                error: err instanceof Error ? err.message : String(err),
              });
            }
          }
        : undefined,
    },

    session: {
      expiresIn: sessionExpiresIn,
      updateAge: sessionUpdateAge,
      storeSessionInDatabase: true,
      cookieCache: {
        enabled: true,
        maxAge: cookieCacheMaxAge,
        strategy: "compact",
      },
      cookieOptions: {
        sameSite: "lax",
      },
      additionalFields: {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: {
          type: "string",
          required: false,
          input: false,
        },
        [SESSION_FIELDS.ACTIVE_ORG_ROLE]: {
          type: "string",
          required: false,
          input: false,
        },
        [SESSION_FIELDS.EMULATED_ORG_ID]: {
          type: "string",
          required: false,
          input: false,
        },
        [SESSION_FIELDS.EMULATED_ORG_TYPE]: {
          type: "string",
          required: false,
          input: false,
        },
        [SESSION_FIELDS.WHO_IS_ID]: {
          type: "string",
          required: false,
          input: false,
        },
        [SESSION_FIELDS.DEVICE_FINGERPRINT]: {
          type: "string",
          required: false,
          input: false,
        },
        [SESSION_FIELDS.CLIENT_FINGERPRINT]: {
          type: "string",
          required: false,
          input: false,
        },
        [SESSION_FIELDS.SERVER_FINGERPRINT]: {
          type: "string",
          required: false,
          input: false,
        },
      },
    },

    advanced: {
      database: {
        generateId,
      },
    },

    databaseHooks: {
      user: {
        create: {
          before: async (user) => {
            // Ensure username fields are populated before persistence
            let data = ensureUsername(user as Record<string, unknown>);

            // Ensure pre-generated IDs are preserved (forceAllowId pattern).
            // When no ID is pre-set, generate one so the Geo contact gets a stable userId.
            const userId = (user.id as string) ?? generateId();
            data = { ...data, id: userId };

            // Read sign-up preferences from AsyncLocalStorage (set by Hono middleware
            // from PARAGLIDE_LOCALE + D2_TIMEZONE cookies). BetterAuth's databaseHooks
            // don't have request access, so this is the bridge.
            const prefs = signUpPrefsStorage.getStore();
            const locale = prefs?.locale ?? (user.locale as string) ?? BASE_LOCALE;
            const timezone = prefs?.timezone ?? (user.timezone as string) ?? "America/New_York";

            // Inject into user data so BetterAuth persists them to the DB row.
            data = { ...data, locale, timezone };

            // Create Geo contact BEFORE user (fail-fast if Geo unavailable)
            if (hooks?.createUserContact) {
              await hooks.createUserContact({
                userId,
                email: user.email as string,
                name: user.name as string,
                locale,
                timezone,
              });
            }

            return { data };
          },
        },
      },
      session: {
        update: {
          before: async (data, context) => {
            const patch = data as Record<string, unknown>;
            if (!("activeOrganizationId" in patch)) return;

            const orgId = patch.activeOrganizationId as string | null;

            // Clearing active org → clear custom fields too
            if (!orgId) {
              return {
                data: {
                  [SESSION_FIELDS.ACTIVE_ORG_TYPE]: null,
                  [SESSION_FIELDS.ACTIVE_ORG_ROLE]: null,
                },
              };
            }

            // Extract userId from BetterAuth's endpoint context (set by orgSessionMiddleware)
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            const userId = (context as any)?.context?.session?.user?.id as string | undefined;
            if (!userId) return;

            try {
              const [orgRow] = await db
                .select({ orgType: betterAuthSchema.organization.orgType })
                .from(betterAuthSchema.organization)
                .where(eq(betterAuthSchema.organization.id, orgId))
                .limit(1);

              const [memberRow] = await db
                .select({ role: betterAuthSchema.member.role })
                .from(betterAuthSchema.member)
                .where(
                  and(
                    eq(betterAuthSchema.member.organizationId, orgId),
                    eq(betterAuthSchema.member.userId, userId),
                  ),
                )
                .limit(1);

              return {
                data: {
                  [SESSION_FIELDS.ACTIVE_ORG_TYPE]: orgRow?.orgType ?? null,
                  [SESSION_FIELDS.ACTIVE_ORG_ROLE]: memberRow?.role ?? null,
                },
              };
            } catch (err: unknown) {
              // DB error — don't block session update. Fields stay null.
              log.warn(
                "session.update.before: failed to resolve org type/role (fields stay null)",
                { error: err instanceof Error ? err.message : String(err) },
              );
              return;
            }
          },
        },
        create: {
          before: async (session) => {
            // Snapshot the request's three fingerprints (combined / client / server)
            // from AsyncLocalStorage onto the session row. The `data` return
            // pattern is how BetterAuth merges custom additionalFields into
            // the row before INSERT (mirrors the update.before hook above).
            const deviceFp = hooks?.getDeviceFingerprintForCurrentRequest?.();
            const clientFp = hooks?.getClientFingerprintForCurrentRequest?.();
            const serverFp = hooks?.getServerFingerprintForCurrentRequest?.();
            const fpFields: Record<string, string | null> = {
              [SESSION_FIELDS.DEVICE_FINGERPRINT]: deviceFp ?? null,
              [SESSION_FIELDS.CLIENT_FINGERPRINT]: clientFp ?? null,
              [SESSION_FIELDS.SERVER_FINGERPRINT]: serverFp ?? null,
            };

            // Block sign-in for fully anonymized users; cancel pending deletion
            // for users still in the grace window. This runs alongside the admin
            // plugin's own ban check (BetterAuth merges hooks per lifecycle slot).
            const userId = (session as Record<string, unknown>)["userId"] as string | undefined;
            if (!userId) return { data: { ...session, ...fpFields } };

            try {
              const [row] = await db
                .select({ status: betterAuthSchema.user.status })
                .from(betterAuthSchema.user)
                .where(eq(betterAuthSchema.user.id, userId))
                .limit(1);
              const status = row?.status as string | undefined;

              if (status === USER_STATUS.DELETED) {
                // Pass the raw TK key as the message; the SvelteKit BFF
                // (`translateMessage` helper) resolves it against the user's
                // locale before rendering. BetterAuth doesn't run inside our
                // D2Result/translator layer, so we cannot translate here —
                // the FE owns the actual i18n round-trip.
                throw new APIError("FORBIDDEN", { message: TK.auth.errors.ACCOUNT_DELETED });
              }

              if (status === USER_STATUS.PENDING_DELETION && hooks?.cancelUserDeletion) {
                // Fire-and-forget the side effect; let the session create proceed.
                // CancelUserDeletion flips status back to active + sends the
                // cancellation email — none of which should block sign-in.
                hooks.cancelUserDeletion({ userId }).catch((err: unknown) => {
                  log.warn("session.create.before: cancelUserDeletion failed (non-blocking)", {
                    error: err instanceof Error ? err.message : String(err),
                    userId,
                  });
                });
              }
            } catch (err: unknown) {
              if (err instanceof APIError) throw err;
              // DB lookup failure — fail-open. Sign-in proceeds; we'd rather a
              // pending-deletion user occasionally not get their cancel-email
              // than block all sign-ins on a transient DB blip.
              log.warn("session.create.before: status lookup failed (fail-open)", {
                error: err instanceof Error ? err.message : String(err),
                userId,
              });
            }

            return { data: { ...session, ...fpFields } };
          },
          after: async (session) => {
            // Record sign-in event via app-layer callback
            if (hooks?.onSignIn) {
              const ipAddress = (session["ipAddress"] as string) ?? "unknown";
              const userAgent = (session["userAgent"] as string) ?? "unknown";
              const userId = session["userId"] as string;
              const sessionId = session["id"] as string;

              if (userId && sessionId) {
                // Fire-and-forget — don't block session creation
                hooks
                  .onSignIn({
                    userId,
                    sessionId,
                    ipAddress,
                    userAgent,
                    deviceFingerprint: hooks.getDeviceFingerprintForCurrentRequest?.(),
                    clientFingerprint: hooks.getClientFingerprintForCurrentRequest?.(),
                    serverFingerprint: hooks.getServerFingerprintForCurrentRequest?.(),
                  })
                  .catch((err: unknown) => {
                    // Swallow errors — sign-in audit is non-critical
                    log.warn("session.create.after: onSignIn callback failed (non-critical)", {
                      error: err instanceof Error ? err.message : String(err),
                    });
                  });
              }
            }
          },
        },
      },
      account: {
        update: {
          after: async (account) => {
            // BetterAuth's `changePassword` endpoint commits a new hash via
            // `internalAdapter.updateAccount({ password })`, which lands here.
            // Other account.update flows (OAuth token refresh, etc.) also pass
            // `password` because the `after` hook receives the full row, not
            // just changed fields — accept the over-fire (rare for credential
            // accounts) over a "did password actually change" comparison that
            // would require carrying state across `before`/`after`.
            if (!hooks?.publishPasswordChanged || !account.password) return;
            const userId = account.userId as string;
            if (!userId) return;

            log.debug("account.update.after: password-changed hook fired", { userId });

            try {
              // Look up the user to get their name and email for the notification.
              const [userRow] = await db
                .select({
                  email: betterAuthSchema.user.email,
                  name: betterAuthSchema.user.name,
                })
                .from(betterAuthSchema.user)
                .where(eq(betterAuthSchema.user.id, userId))
                .limit(1);

              if (!userRow) {
                log.warn("account.update.after: user row not found for password notification", {
                  userId,
                });
                return;
              }

              hooks
                .publishPasswordChanged({
                  userId,
                  email: userRow.email,
                  name: userRow.name,
                })
                .catch((err: unknown) => {
                  log.warn("account.update.after: publishPasswordChanged failed (non-critical)", {
                    error: err instanceof Error ? err.message : String(err),
                    userId,
                  });
                });

              // Bust session cache + push user:updated so every open tab
              // refreshes its data (security tab's session list shrinks if
              // other sessions were revoked). Without this, the frontend
              // would have to call invalidateAll() itself — which violates
              // §5 SvelteKit single-source-of-truth-for-cache-bust rule.
              hooks.invalidateAndPushUserUpdated?.({ userId }).catch((err: unknown) => {
                log.warn(
                  "account.update.after: invalidateAndPushUserUpdated failed (non-critical)",
                  {
                    error: err instanceof Error ? err.message : String(err),
                    userId,
                  },
                );
              });
            } catch (err: unknown) {
              log.warn("account.update.after: failed to look up user for password notification", {
                error: err instanceof Error ? err.message : String(err),
                userId,
              });
            }
          },
        },
      },
    },

    trustedOrigins: config.corsOrigins,

    user: {
      additionalFields: {
        locale: {
          type: "string",
          required: false,
          defaultValue: BASE_LOCALE,
          input: false,
        },
        timezone: {
          type: "string",
          required: false,
          defaultValue: "America/New_York",
          input: false,
        },
        phone: {
          type: "string",
          required: false,
          input: false,
        },
        phoneVerified: {
          type: "boolean",
          required: false,
          defaultValue: false,
          input: false,
        },
      },
    },

    plugins: [
      bearer(),
      username(),
      jwt({
        jwks: {
          keyPairConfig: {
            alg: "RS256",
            modulusLength: 2048,
          },
          rotationInterval: jwksRotationDays * 24 * 60 * 60,
          gracePeriod: 30 * 24 * 60 * 60, // 30 days
        },
        jwt: {
          issuer: config.jwtIssuer,
          audience: config.jwtAudience,
          expirationTime: `${jwtExpirationSeconds}s`,
          definePayload: async ({ user, session }) => {
            const s = session as Record<string, unknown>;
            const u = user as Record<string, unknown>;

            // Resolve impersonator details if impersonation is active
            let impersonatingEmail: string | null = null;
            let impersonatingUsername: string | null = null;
            const impersonatedBy = (s["impersonatedBy"] as string) ?? null;

            if (impersonatedBy) {
              try {
                const impersonator = await db
                  .select({
                    email: betterAuthSchema.user.email,
                    username: betterAuthSchema.user.username,
                  })
                  .from(betterAuthSchema.user)
                  .where(eq(betterAuthSchema.user.id, impersonatedBy))
                  .limit(1);
                const imp = impersonator[0];
                if (imp) {
                  impersonatingEmail = imp.email;
                  impersonatingUsername = imp.username;
                }
              } catch (err: unknown) {
                // Non-critical — impersonator details are for audit only
                log.debug("definePayload: impersonator lookup failed (non-critical)", {
                  error: err instanceof Error ? err.message : String(err),
                });
              }
            }

            return {
              [JWT_CLAIM_TYPES.SUB]: user.id,
              [JWT_CLAIM_TYPES.EMAIL]: user.email,
              [JWT_CLAIM_TYPES.USERNAME]: (u["username"] as string) ?? null,
              [JWT_CLAIM_TYPES.ORG_ID]: s[SESSION_FIELDS.ACTIVE_ORG_ID] ?? null,
              [JWT_CLAIM_TYPES.ORG_TYPE]: s[SESSION_FIELDS.ACTIVE_ORG_TYPE] ?? null,
              [JWT_CLAIM_TYPES.ROLE]: s[SESSION_FIELDS.ACTIVE_ORG_ROLE] ?? null,
              [JWT_CLAIM_TYPES.EMULATED_ORG_ID]: s[SESSION_FIELDS.EMULATED_ORG_ID] ?? null,
              [JWT_CLAIM_TYPES.IS_EMULATING]: !!s[SESSION_FIELDS.EMULATED_ORG_ID],
              [JWT_CLAIM_TYPES.IMPERSONATED_BY]: impersonatedBy,
              [JWT_CLAIM_TYPES.IS_IMPERSONATING]: !!impersonatedBy,
              [JWT_CLAIM_TYPES.IMPERSONATING_EMAIL]: impersonatingEmail,
              [JWT_CLAIM_TYPES.IMPERSONATING_USERNAME]: impersonatingUsername,
              [JWT_CLAIM_TYPES.FINGERPRINT]: hooks?.getFingerprintForCurrentRequest?.() ?? null,
            };
          },
        },
      }),
      organization({
        ac,
        roles: {
          owner: ownerPermissions,
          officer: officerPermissions,
          agent: agentPermissions,
          auditor: auditorPermissions,
        },
        creatorRole: "owner",
        allowUserToCreateOrganization: true,
        invitationExpiresIn: 48 * 60 * 60, // 48 hours
        schema: {
          organization: {
            additionalFields: {
              orgType: {
                type: "string",
                required: false,
                defaultValue: "customer",
                input: false,
              },
            },
          },
        },
        organizationHooks: {
          beforeCreateOrganization: async (data) => {
            beforeCreateOrganization(data.organization as Record<string, unknown>);
          },
        },
      }),
      admin({
        defaultRole: "agent",
        impersonationSessionDuration: 60 * 60, // 1 hour
      }),
    ],
  });

  return auth;
}

/** The return type of createAuth — use this to type the auth instance. */
export type Auth = ReturnType<typeof createAuth>;
