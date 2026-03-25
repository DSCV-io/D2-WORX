import { createServiceKey } from "@d2/di";

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
  IUpdateUserImageHandler,
  IUpdateOrgLogoHandler,
  IPurgeExpiredSessionsHandler,
  IPurgeSignInEventsHandler,
  IPurgeExpiredInvitationsHandler,
  IPurgeExpiredEmulationConsentsHandler,
  IPingDbHandler,
  IUpdateUserNameHandler,
  ICheckUsernameAvailableHandler,
  IUpdateUserUsernameHandler,
} from "./interfaces/repository/handlers/index.js";
import type { ISignInThrottleStore } from "./interfaces/repository/sign-in-throttle-store.js";
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

// --- Health Check Repository Handler ---

export const IPingDbKey = createServiceKey<IPingDbHandler>("Auth.Repo.PingDb");

// --- Sign-In Throttle Store ---

export const ISignInThrottleStoreKey = createServiceKey<ISignInThrottleStore>(
  "Auth.SignInThrottleStore",
);

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

// --- Query Handlers ---

export const IGetSignInEventsKey = createServiceKey<Queries.IGetSignInEventsHandler>(
  "Auth.App.GetSignInEvents",
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
