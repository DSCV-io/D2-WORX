/**
 * JWT claim type names used across the D2 platform.
 * Must match .NET JwtClaimTypes in D2.Shared.Handler.Auth.
 */
export const JWT_CLAIM_TYPES = {
  SUB: "sub",
  EMAIL: "email",
  ORG_ID: "orgId",
  ORG_NAME: "orgName",
  ORG_TYPE: "orgType",
  ROLE: "role",
  EMULATED_ORG_ID: "emulatedOrgId",
  EMULATED_ORG_NAME: "emulatedOrgName",
  EMULATED_ORG_TYPE: "emulatedOrgType",
  IS_EMULATING: "isEmulating",
  IMPERSONATED_BY: "impersonatedBy",
  IS_IMPERSONATING: "isImpersonating",
  USERNAME: "username",
  IMPERSONATING_EMAIL: "impersonatingEmail",
  IMPERSONATING_USERNAME: "impersonatingUsername",
  FINGERPRINT: "fp",
} as const;

/**
 * Session field names — BetterAuth additionalFields on the session object.
 * Used when reading/writing custom session data.
 */
export const SESSION_FIELDS = {
  ACTIVE_ORG_TYPE: "activeOrganizationType",
  ACTIVE_ORG_ROLE: "activeOrganizationRole",
  ACTIVE_ORG_ID: "activeOrganizationId",
  EMULATED_ORG_ID: "emulatedOrganizationId",
  EMULATED_ORG_TYPE: "emulatedOrganizationType",
  WHO_IS_ID: "whoIsId",
} as const;

/**
 * Authorization policy names — shared between Node.js and .NET.
 * Must match .NET AuthPolicies in D2.Shared.Handler.Auth.
 */
export const AUTH_POLICIES = {
  AUTHENTICATED: "Authenticated",
  HAS_ACTIVE_ORG: "HasActiveOrg",
  STAFF_ONLY: "StaffOnly",
  ADMIN_ONLY: "AdminOnly",
} as const;

/**
 * Custom HTTP request header names used across the D2 platform.
 * Must match .NET RequestHeaders in D2.Shared.Handler.Auth.
 */
export const REQUEST_HEADERS = {
  IDEMPOTENCY_KEY: "Idempotency-Key",
  CLIENT_FINGERPRINT: "X-Client-Fingerprint",
} as const;

/**
 * Password policy constants.
 *
 * Min/max length is enforced by BetterAuth natively (before the hash hook).
 * Domain rules (numeric-only, date-like, common blocklist) and HIBP checks
 * are enforced inside the custom `hash` function.
 */
export const PASSWORD_POLICY = {
  MIN_LENGTH: 12,
  MAX_LENGTH: 128,
  HIBP_CACHE_TTL_MS: 24 * 60 * 60 * 1000,
  HIBP_API_BASE: "https://api.pwnedpasswords.com/range/",
  HIBP_CACHE_MAX_ENTRIES: 10_000,
} as const;

/**
 * Sign-in brute-force protection constants.
 *
 * Progressive delay per (identifier, identity) pair — never hard-locks.
 * Known-good identities (IP + fingerprint that previously signed in successfully)
 * bypass throttling entirely.
 */
/**
 * Geo contact context keys used by the auth service.
 *
 * These prefixed keys avoid clashes with other services that also store
 * contacts via the Geo ext-key API. Every contextKey sent to Geo from auth
 * MUST use one of these constants.
 */
export const GEO_CONTEXT_KEYS = {
  /** User's personal contact (1:1 with auth user, created during sign-up). */
  USER: "auth_user",
  /** Organization contact (via org_contact junction table). */
  ORG_CONTACT: "auth_org_contact",
  /** Invitation contact (created for non-existing invitees). */
  ORG_INVITATION: "auth_org_invitation",
} as const;

/**
 * Context keys used by the auth service for file uploads via the Files service.
 * These must match the context key configs registered on the Files service side.
 */
export const AUTH_FILE_CONTEXT_KEYS = {
  /** User avatar image. */
  USER_AVATAR: "user_avatar",
  /** Organization logo image. */
  ORG_LOGO: "org_logo",
  /** Organization document. */
  ORG_DOCUMENT: "org_document",
  /** Conversational thread attachment (owned by Comms, but callback routed through Auth). */
  THREAD_ATTACHMENT: "thread_attachment",
} as const;

export const SIGN_IN_THROTTLE = {
  /** Number of failed attempts before delays begin. */
  FREE_ATTEMPTS: 3,
  /** Maximum delay in milliseconds (15 minutes). */
  MAX_DELAY_MS: 15 * 60 * 1000,
  /** TTL for the attempt counter in seconds (15 minutes). */
  ATTEMPT_WINDOW_SECONDS: 15 * 60,
  /** TTL for known-good identity flag in seconds (90 days). */
  KNOWN_GOOD_TTL_SECONDS: 90 * 24 * 60 * 60,
  /** Local memory cache TTL for known-good lookups in milliseconds (5 minutes). */
  KNOWN_GOOD_CACHE_TTL_MS: 5 * 60 * 1000,
} as const;
