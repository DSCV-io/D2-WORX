// @d2/auth-infra — BetterAuth configuration, repositories, and storage adapters.
// This is the ONLY package that imports better-auth.

// --- Auth Factory ---
export { createAuth, signUpPrefsStorage } from "./auth/better-auth/auth-factory.js";
export type { Auth, AuthHooks, SignUpPreferences } from "./auth/better-auth/auth-factory.js";

// --- Config ---
export { AUTH_CONFIG_DEFAULTS } from "./auth/better-auth/auth-config.js";
export type { AuthServiceConfig } from "./auth/better-auth/auth-config.js";

// --- Access Control ---
export {
  ac,
  ownerPermissions,
  officerPermissions,
  agentPermissions,
  auditorPermissions,
} from "./auth/better-auth/access-control.js";

// --- Secondary Storage ---
export { createSecondaryStorage } from "./auth/better-auth/secondary-storage.js";

// --- Hooks ---
export { generateId } from "./auth/better-auth/hooks/id-hooks.js";
export { beforeCreateOrganization } from "./auth/better-auth/hooks/org-hooks.js";
export { ensureUsername } from "./auth/better-auth/hooks/username-hooks.js";
export {
  createPasswordFunctions,
  checkBreachedPassword,
} from "./auth/better-auth/hooks/password-hooks.js";
export type {
  PasswordFunctions,
  BreachCheckResult,
  PrefixCache,
} from "./auth/better-auth/hooks/password-hooks.js";

// --- Mappers ---
export { toDomainUser } from "./mappers/user-mapper.js";
export { toDomainOrganization } from "./mappers/org-mapper.js";
export { toDomainSession } from "./mappers/session-mapper.js";
export { toDomainMember } from "./mappers/member-mapper.js";
export { toDomainInvitation } from "./mappers/invitation-mapper.js";

// --- Repository Handler Factories ---
export {
  createSignInEventRepoHandlers,
  createEmulationConsentRepoHandlers,
  createOrgContactRepoHandlers,
} from "./repository/handlers/factories.js";

// --- Sign-In Throttle ---
export { SignInThrottleStore } from "./auth/sign-in-throttle-store.js";

// --- OTP Rate Limit ---
export { OtpRateLimitStore } from "./auth/otp-rate-limit-store.js";

// --- Verification Store (BetterAuth-backed) ---
export { BetterAuthVerificationStore } from "./auth/verification-store.js";

// --- Password Verifier (BetterAuth-backed) ---
export { BetterAuthPasswordVerifier } from "./auth/password-verifier.js";

// --- Drizzle Schema ---
export { signInEvent, emulationConsent, orgContact } from "./repository/schema/index.js";
export type {
  SignInEventRow,
  NewSignInEvent,
  EmulationConsentRow,
  NewEmulationConsent,
  OrgContactRow,
  NewOrgContact,
} from "./repository/schema/index.js";

// BetterAuth table schema (used by Drizzle adapter and tests)
export {
  user,
  session,
  account,
  verification,
  jwks,
  organization,
  member,
  invitation,
} from "./repository/schema/index.js";

// --- Migrations ---
export { runMigrations } from "./repository/migrate.js";

// --- Email Check (pre-auth repo — constructed manually in composition root) ---
export { CheckEmailAvailability as CheckEmailAvailabilityRepo } from "./repository/handlers/r/check-email-availability.js";

// --- Organization Existence Check ---
export { CheckOrgExists } from "./repository/handlers/r/check-org-exists.js";

// --- Purge Handlers (used by integration tests) ---
export { PurgeExpiredSessions } from "./repository/handlers/d/purge-expired-sessions.js";
export { PurgeSignInEvents } from "./repository/handlers/d/purge-sign-in-events.js";
export { PurgeExpiredInvitations } from "./repository/handlers/d/purge-expired-invitations.js";
export { PurgeExpiredEmulationConsents } from "./repository/handlers/d/purge-expired-emulation-consents.js";

// --- Repository Handlers (file callback) ---
export { UpdateUserImage } from "./repository/handlers/u/update-user-image.js";
export { UpdateOrgLogo } from "./repository/handlers/u/update-org-logo.js";
export { UpdateUserLocale } from "./repository/handlers/u/update-user-locale.js";
export { UpdateUserTimezone } from "./repository/handlers/u/update-user-timezone.js";

// --- Messaging Consumers ---
export { createWhoIsResolutionConsumer } from "./messaging/consumers/whois-resolution-consumer.js";
export type { WhoIsResolutionConsumerDeps } from "./messaging/consumers/whois-resolution-consumer.js";

// --- Realtime Handlers ---
export { PushUserUpdated } from "./realtime/handlers/push-user-updated.js";

// --- DI Registration ---
export { addAuthInfra } from "./registration.js";
export type { AuthInfraConfig } from "./registration.js";
export {
  ICreateSignInEventKey,
  IFindSignInEventsByUserIdKey,
  ICountSignInEventsByUserIdKey,
  IGetLatestSignInEventDateKey,
  IUpdateSignInEventWhoIsIdKey,
  IUpdateSessionWhoIsIdKey,
  IFindActiveSessionsByUserIdKey,
  IFindUserIdByIdentifierKey,
  ICreateEmulationConsentRecordKey,
  IFindEmulationConsentByIdKey,
  IFindActiveConsentsByUserIdKey,
  IFindActiveConsentByUserIdAndOrgKey,
  IRevokeEmulationConsentRecordKey,
  ICreateOrgContactRecordKey,
  IFindOrgContactByIdKey,
  IFindOrgContactsByOrgIdKey,
  IUpdateOrgContactRecordKey,
  IDeleteOrgContactRecordKey,
  ICheckOrgExistsKey,
  ISignInThrottleStoreKey,
} from "./service-keys.js";
