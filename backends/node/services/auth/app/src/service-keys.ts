import { createServiceKey } from "@d2/di";
import type { Translator } from "@d2/i18n";

// Import interface types for keys
import type {
  ICreateSignInEventHandler,
  IFindSignInEventsByUserIdHandler,
  ICountSignInEventsByUserIdHandler,
  IGetLatestSignInEventDateHandler,
  ICreateEmulationConsentRecordHandler,
  IFindEmulationConsentByIdHandler,
  IFindActiveConsentsByUserIdHandler,
  IFindActiveConsentByUserIdAndOrgHandler,
  IRevokeEmulationConsentRecordHandler,
  ICreateOrgContactRecordHandler,
  IFindOrgContactByIdHandler,
  IFindOrgContactsByOrgIdHandler,
  IUpdateOrgContactRecordHandler,
  IDeleteOrgContactRecordHandler,
  IUpdateSignInEventWhoIsIdHandler,
  IUpdateSessionWhoIsIdHandler,
  IUpdateUserImageHandler,
  IUpdateUserLocaleHandler,
  IUpdateUserTimezoneHandler,
  IUpdateOrgLogoHandler,
  IPurgeExpiredSessionsHandler,
  IPurgeSignInEventsHandler,
  IPurgeExpiredInvitationsHandler,
  IPurgeExpiredEmulationConsentsHandler,
  IPingDbHandler,
  IUpdateUserNameHandler,
  ICheckUsernameAvailableHandler,
  IUpdateUserUsernameHandler,
  IUpdateUserEmailHandler,
  IUpdateUserPhoneHandler,
  ICheckPhoneAvailabilityHandler,
  IGetUserByIdHandler,
  IFindActiveSessionsByUserIdHandler,
  IFindUserIdByIdentifierHandler,
} from "./interfaces/repository/handlers/index.js";
import type { ISignInThrottleStore } from "./interfaces/repository/sign-in-throttle-store.js";
import type { IOtpRateLimitStore } from "./interfaces/repository/otp-rate-limit-store.js";
import type { IVerificationStore } from "./interfaces/repository/verification-store.js";
import type { IVerifyUserPassword } from "./interfaces/repository/password-verifier.js";
import type { ICheckEmailAvailabilityHandler } from "./interfaces/repository/handlers/r/check-email-availability.js";
import type { ICheckOrgExistsHandler } from "./interfaces/repository/handlers/r/check-org-exists.js";
import type { Commands, Queries } from "./interfaces/cqrs/handlers/index.js";
import type { IPushUserUpdated } from "./interfaces/realtime/handlers/index.js";
// =============================================================================
// Infrastructure-layer keys (interfaces defined in auth-app, implemented in auth-infra)
// =============================================================================

// --- Sign-In Event Repository Handlers ---

export const ICreateSignInEventKey = createServiceKey<ICreateSignInEventHandler>(
  "Auth.Repo.CreateSignInEvent",
);
export const IFindSignInEventsByUserIdKey = createServiceKey<IFindSignInEventsByUserIdHandler>(
  "Auth.Repo.FindSignInEventsByUserId",
);
export const ICountSignInEventsByUserIdKey = createServiceKey<ICountSignInEventsByUserIdHandler>(
  "Auth.Repo.CountSignInEventsByUserId",
);
export const IGetLatestSignInEventDateKey = createServiceKey<IGetLatestSignInEventDateHandler>(
  "Auth.Repo.GetLatestSignInEventDate",
);
export const IUpdateSignInEventWhoIsIdKey = createServiceKey<IUpdateSignInEventWhoIsIdHandler>(
  "Auth.Repo.UpdateSignInEventWhoIsId",
);
export const IUpdateSessionWhoIsIdKey = createServiceKey<IUpdateSessionWhoIsIdHandler>(
  "Auth.Repo.UpdateSessionWhoIsId",
);
export const IUpdateUserImageKey = createServiceKey<IUpdateUserImageHandler>(
  "Auth.Repo.UpdateUserImage",
);
export const IUpdateOrgLogoKey = createServiceKey<IUpdateOrgLogoHandler>("Auth.Repo.UpdateOrgLogo");

// --- Emulation Consent Repository Handlers ---

export const ICreateEmulationConsentRecordKey =
  createServiceKey<ICreateEmulationConsentRecordHandler>("Auth.Repo.CreateEmulationConsentRecord");
export const IFindEmulationConsentByIdKey = createServiceKey<IFindEmulationConsentByIdHandler>(
  "Auth.Repo.FindEmulationConsentById",
);
export const IFindActiveConsentsByUserIdKey = createServiceKey<IFindActiveConsentsByUserIdHandler>(
  "Auth.Repo.FindActiveConsentsByUserId",
);
export const IFindActiveConsentByUserIdAndOrgKey =
  createServiceKey<IFindActiveConsentByUserIdAndOrgHandler>(
    "Auth.Repo.FindActiveConsentByUserIdAndOrg",
  );
export const IRevokeEmulationConsentRecordKey =
  createServiceKey<IRevokeEmulationConsentRecordHandler>("Auth.Repo.RevokeEmulationConsentRecord");

// --- Org Contact Repository Handlers ---

export const ICreateOrgContactRecordKey = createServiceKey<ICreateOrgContactRecordHandler>(
  "Auth.Repo.CreateOrgContactRecord",
);
export const IFindOrgContactByIdKey = createServiceKey<IFindOrgContactByIdHandler>(
  "Auth.Repo.FindOrgContactById",
);
export const IFindOrgContactsByOrgIdKey = createServiceKey<IFindOrgContactsByOrgIdHandler>(
  "Auth.Repo.FindOrgContactsByOrgId",
);
export const IUpdateOrgContactRecordKey = createServiceKey<IUpdateOrgContactRecordHandler>(
  "Auth.Repo.UpdateOrgContactRecord",
);
export const IDeleteOrgContactRecordKey = createServiceKey<IDeleteOrgContactRecordHandler>(
  "Auth.Repo.DeleteOrgContactRecord",
);

// --- Job Repository Handlers ---

export const IPurgeExpiredSessionsKey = createServiceKey<IPurgeExpiredSessionsHandler>(
  "Auth.Repo.PurgeExpiredSessions",
);
export const IPurgeSignInEventsKey = createServiceKey<IPurgeSignInEventsHandler>(
  "Auth.Repo.PurgeSignInEvents",
);
export const IPurgeExpiredInvitationsKey = createServiceKey<IPurgeExpiredInvitationsHandler>(
  "Auth.Repo.PurgeExpiredInvitations",
);
export const IPurgeExpiredEmulationConsentsKey =
  createServiceKey<IPurgeExpiredEmulationConsentsHandler>(
    "Auth.Repo.PurgeExpiredEmulationConsents",
  );

// --- Email Availability Repository Handler ---

export const ICheckEmailAvailabilityRepoKey = createServiceKey<ICheckEmailAvailabilityHandler>(
  "Auth.Repo.CheckEmailAvailability",
);

// --- Organization Existence Repository Handler ---

export const ICheckOrgExistsKey = createServiceKey<ICheckOrgExistsHandler>(
  "Auth.Repo.CheckOrgExists",
);

// --- User Account Repository Handlers ---

export const IUpdateUserNameKey = createServiceKey<IUpdateUserNameHandler>(
  "Auth.Repo.UpdateUserName",
);
export const ICheckUsernameAvailableKey = createServiceKey<ICheckUsernameAvailableHandler>(
  "Auth.Repo.CheckUsernameAvailable",
);
export const IUpdateUserUsernameKey = createServiceKey<IUpdateUserUsernameHandler>(
  "Auth.Repo.UpdateUserUsername",
);
export const IUpdateUserLocaleRepoKey = createServiceKey<IUpdateUserLocaleHandler>(
  "Auth.Repo.UpdateUserLocale",
);
export const IUpdateUserTimezoneRepoKey = createServiceKey<IUpdateUserTimezoneHandler>(
  "Auth.Repo.UpdateUserTimezone",
);

// --- Health Check Repository Handler ---

export const IPingDbKey = createServiceKey<IPingDbHandler>("Auth.Repo.PingDb");

// --- Sign-In Throttle Store ---

export const ISignInThrottleStoreKey = createServiceKey<ISignInThrottleStore>(
  "Auth.SignInThrottleStore",
);

// --- OTP Rate Limit Store ---

export const IOtpRateLimitStoreKey = createServiceKey<IOtpRateLimitStore>("Auth.OtpRateLimitStore");

// --- Verification Store ---

export const IVerificationStoreKey = createServiceKey<IVerificationStore>("Auth.VerificationStore");

// --- Password Verifier ---

export const IVerifyUserPasswordKey =
  createServiceKey<IVerifyUserPassword>("Auth.VerifyUserPassword");

// --- Realtime Handlers ---

export const IPushUserUpdatedKey = createServiceKey<IPushUserUpdated>(
  "Auth.Realtime.PushUserUpdated",
);

// =============================================================================
// Application-layer keys (defined and implemented in auth-app)
// =============================================================================

// --- Command Handlers ---

export const IRecordSignInEventKey = createServiceKey<Commands.IRecordSignInEventHandler>(
  "Auth.App.RecordSignInEvent",
);
export const IRecordSignInOutcomeKey = createServiceKey<Commands.IRecordSignInOutcomeHandler>(
  "Auth.App.RecordSignInOutcome",
);
export const ICreateEmulationConsentKey = createServiceKey<Commands.ICreateEmulationConsentHandler>(
  "Auth.App.CreateEmulationConsent",
);
export const IRevokeEmulationConsentKey = createServiceKey<Commands.IRevokeEmulationConsentHandler>(
  "Auth.App.RevokeEmulationConsent",
);
export const ICreateOrgContactKey = createServiceKey<Commands.ICreateOrgContactHandler>(
  "Auth.App.CreateOrgContact",
);
export const IUpdateOrgContactKey = createServiceKey<Commands.IUpdateOrgContactHandler>(
  "Auth.App.UpdateOrgContact",
);
export const IDeleteOrgContactKey = createServiceKey<Commands.IDeleteOrgContactHandler>(
  "Auth.App.DeleteOrgContact",
);
export const ICreateUserContactKey = createServiceKey<Commands.ICreateUserContactHandler>(
  "Auth.App.CreateUserContact",
);
export const IUpdateUserRealNameKey = createServiceKey<Commands.IUpdateUserRealNameHandler>(
  "Auth.App.UpdateUserRealName",
);
export const IUpdateUsernameKey =
  createServiceKey<Commands.IUpdateUsernameHandler>("Auth.App.UpdateUsername");
export const IUpdateUserLocaleKey = createServiceKey<Commands.IUpdateUserLocaleHandler>(
  "Auth.App.UpdateUserLocale",
);
export const IUpdateUserTimezoneKey = createServiceKey<Commands.IUpdateUserTimezoneHandler>(
  "Auth.App.UpdateUserTimezone",
);

// --- Query Handlers ---

export const IGetSignInEventsKey = createServiceKey<Queries.IGetSignInEventsHandler>(
  "Auth.App.GetSignInEvents",
);
export const IGetMySessionsKey = createServiceKey<Queries.IGetMySessionsHandler>(
  "Auth.App.GetMySessions",
);
export const IGetActiveConsentsKey = createServiceKey<Queries.IGetActiveConsentsHandler>(
  "Auth.App.GetActiveConsents",
);
export const IGetOrgContactsKey =
  createServiceKey<Queries.IGetOrgContactsHandler>("Auth.App.GetOrgContacts");
export const ICheckSignInThrottleKey = createServiceKey<Queries.ICheckSignInThrottleHandler>(
  "Auth.App.CheckSignInThrottle",
);
export const ICheckHealthKey =
  createServiceKey<Queries.ICheckHealthHandler>("Auth.App.CheckHealth");
export const ICheckEmailAvailabilityKey = createServiceKey<Queries.ICheckEmailAvailabilityHandler>(
  "Auth.App.CheckEmailAvailability",
);

// --- Job Handlers (Command) ---

export const IRunSessionPurgeKey = createServiceKey<Commands.IRunSessionPurgeHandler>(
  "Auth.App.RunSessionPurge",
);
export const IRunSignInEventPurgeKey = createServiceKey<Commands.IRunSignInEventPurgeHandler>(
  "Auth.App.RunSignInEventPurge",
);
export const IRunInvitationCleanupKey = createServiceKey<Commands.IRunInvitationCleanupHandler>(
  "Auth.App.RunInvitationCleanup",
);
export const IRunEmulationConsentCleanupKey =
  createServiceKey<Commands.IRunEmulationConsentCleanupHandler>(
    "Auth.App.RunEmulationConsentCleanup",
  );

export const IHandleFileProcessedKey = createServiceKey<Commands.IHandleFileProcessedHandler>(
  "Auth.App.HandleFileProcessed",
);

export const IInvalidateUserSessionCacheKey =
  createServiceKey<Commands.IInvalidateUserSessionCacheHandler>(
    "Auth.App.InvalidateUserSessionCache",
  );

// --- Email/Phone change (OTP) handlers ---

export const IRequestEmailChangeKey = createServiceKey<Commands.IRequestEmailChangeHandler>(
  "Auth.App.RequestEmailChange",
);
export const IVerifyEmailChangeKey = createServiceKey<Commands.IVerifyEmailChangeHandler>(
  "Auth.App.VerifyEmailChange",
);
export const IRequestPhoneChangeKey = createServiceKey<Commands.IRequestPhoneChangeHandler>(
  "Auth.App.RequestPhoneChange",
);
export const IVerifyPhoneChangeKey = createServiceKey<Commands.IVerifyPhoneChangeHandler>(
  "Auth.App.VerifyPhoneChange",
);
export const IRemovePhoneKey =
  createServiceKey<Commands.IRemovePhoneHandler>("Auth.App.RemovePhone");

// --- Repository keys for new lookups/updates ---

export const ICheckPhoneAvailabilityKey = createServiceKey<ICheckPhoneAvailabilityHandler>(
  "Auth.Repo.CheckPhoneAvailability",
);

export const IGetUserByIdKey = createServiceKey<IGetUserByIdHandler>("Auth.Repo.GetUserById");

export const IFindActiveSessionsByUserIdKey =
  createServiceKey<IFindActiveSessionsByUserIdHandler>("Auth.Repo.FindActiveSessionsByUserId");

export const IFindUserIdByIdentifierKey =
  createServiceKey<IFindUserIdByIdentifierHandler>("Auth.Repo.FindUserIdByIdentifier");

export const IUpdateUserEmailKey = createServiceKey<IUpdateUserEmailHandler>(
  "Auth.Repo.UpdateUserEmail",
);

export const IUpdateUserPhoneKey = createServiceKey<IUpdateUserPhoneHandler>(
  "Auth.Repo.UpdateUserPhone",
);

// --- i18n Translator (singleton, registered in composition-root) ---
export const ITranslatorKey = createServiceKey<Translator>("Auth.Translator");
