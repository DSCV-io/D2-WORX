// @d2/auth-app — Custom business logic handlers for the Auth service.
// Zero BetterAuth imports — this package is pure application logic.

export { AUTH_CACHE_KEYS } from "./cache-keys.js";

// --- CQRS Handler Interfaces (app-layer contracts) ---
export {
  Commands as AuthCommands,
  Queries as AuthQueries,
} from "./interfaces/cqrs/handlers/index.js";

import type { IHandlerContext } from "@d2/handler";
import type { SignInEvent } from "@d2/auth-domain";
import type { Commands, Queries, Complex } from "@d2/geo-client";

// --- Interfaces (Repository Handler Bundles) ---
export type {
  // Bundle types (used by factory functions + composition root)
  SignInEventRepoHandlers,
  EmulationConsentRepoHandlers,
  OrgContactRepoHandlers,
  // Individual handler types (used by app-layer handler constructors)
  ICreateSignInEventHandler,
  IFindSignInEventsByUserIdHandler,
  ICountSignInEventsByUserIdHandler,
  IGetLatestSignInEventDateHandler,
  IUpdateSignInEventWhoIsIdHandler,
  IUpdateUserImageHandler,
  UpdateUserImageInput,
  UpdateUserImageOutput,
  IUpdateOrgLogoHandler,
  UpdateOrgLogoInput,
  UpdateOrgLogoOutput,
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
  // Individual I/O types
  CreateSignInEventInput,
  CreateSignInEventOutput,
  FindSignInEventsByUserIdInput,
  FindSignInEventsByUserIdOutput,
  CountSignInEventsByUserIdInput,
  CountSignInEventsByUserIdOutput,
  GetLatestSignInEventDateInput,
  GetLatestSignInEventDateOutput,
  CreateEmulationConsentRecordInput,
  CreateEmulationConsentRecordOutput,
  FindEmulationConsentByIdInput,
  FindEmulationConsentByIdOutput,
  FindActiveConsentsByUserIdInput,
  FindActiveConsentsByUserIdOutput,
  FindActiveConsentByUserIdAndOrgInput,
  FindActiveConsentByUserIdAndOrgOutput,
  RevokeEmulationConsentRecordInput,
  RevokeEmulationConsentRecordOutput,
  CreateOrgContactRecordInput,
  CreateOrgContactRecordOutput,
  FindOrgContactByIdInput,
  FindOrgContactByIdOutput,
  FindOrgContactsByOrgIdInput,
  FindOrgContactsByOrgIdOutput,
  UpdateOrgContactRecordInput,
  UpdateOrgContactRecordOutput,
  UpdateSignInEventWhoIsIdInput,
  UpdateSignInEventWhoIsIdOutput,
  DeleteOrgContactRecordInput,
  DeleteOrgContactRecordOutput,
  // Read (R) — Email Availability
  CheckEmailAvailabilityInput as CheckEmailAvailabilityRepoInput,
  CheckEmailAvailabilityOutput as CheckEmailAvailabilityRepoOutput,
  ICheckEmailAvailabilityHandler,
  // Read (R) — Organization Existence
  CheckOrgExistsInput,
  CheckOrgExistsOutput,
  ICheckOrgExistsHandler,
  // Read (R) — Username Availability
  CheckUsernameAvailableInput,
  CheckUsernameAvailableOutput,
  ICheckUsernameAvailableHandler,
  // Update (U) — User Name
  UpdateUserNameInput,
  UpdateUserNameOutput,
  IUpdateUserNameHandler,
  // Update (U) — User Username
  UpdateUserUsernameInput,
  UpdateUserUsernameOutput,
  IUpdateUserUsernameHandler,
  // Read (R) — PingDb
  PingDbInput,
  PingDbOutput,
  IPingDbHandler,
  // Delete (D) — Job purge handlers
  PurgeExpiredSessionsInput,
  PurgeExpiredSessionsOutput,
  IPurgeExpiredSessionsHandler,
  PurgeSignInEventsInput,
  PurgeSignInEventsOutput,
  IPurgeSignInEventsHandler,
  PurgeExpiredInvitationsInput,
  PurgeExpiredInvitationsOutput,
  IPurgeExpiredInvitationsHandler,
  PurgeExpiredEmulationConsentsInput,
  PurgeExpiredEmulationConsentsOutput,
  IPurgeExpiredEmulationConsentsHandler,
} from "./interfaces/repository/handlers/index.js";

export type { ISignInThrottleStore } from "./interfaces/repository/sign-in-throttle-store.js";

export type {
  PushUserUpdatedInput,
  PushUserUpdatedOutput,
  IPushUserUpdated,
} from "./interfaces/realtime/handlers/index.js";

// --- Command Handlers ---
export { RecordSignInEvent } from "./implementations/cqrs/handlers/c/record-sign-in-event.js";
export type {
  RecordSignInEventInput,
  RecordSignInEventOutput,
} from "./interfaces/cqrs/handlers/c/record-sign-in-event.js";

export { CreateEmulationConsent } from "./implementations/cqrs/handlers/c/create-emulation-consent.js";
export type {
  CreateEmulationConsentInput,
  CreateEmulationConsentOutput,
} from "./interfaces/cqrs/handlers/c/create-emulation-consent.js";

export { RevokeEmulationConsent } from "./implementations/cqrs/handlers/c/revoke-emulation-consent.js";
export type {
  RevokeEmulationConsentInput,
  RevokeEmulationConsentOutput,
} from "./interfaces/cqrs/handlers/c/revoke-emulation-consent.js";

export { CreateOrgContact } from "./implementations/cqrs/handlers/c/create-org-contact.js";
export type {
  ContactInput,
  CreateOrgContactInput,
  CreateOrgContactOutput,
} from "./interfaces/cqrs/handlers/c/create-org-contact.js";

export { UpdateOrgContactHandler } from "./implementations/cqrs/handlers/c/update-org-contact.js";
export type {
  UpdateOrgContactHandlerInput,
  UpdateOrgContactOutput,
} from "./interfaces/cqrs/handlers/c/update-org-contact.js";

export { DeleteOrgContact } from "./implementations/cqrs/handlers/c/delete-org-contact.js";
export type {
  DeleteOrgContactInput,
  DeleteOrgContactOutput,
} from "./interfaces/cqrs/handlers/c/delete-org-contact.js";

export { CreateUserContact } from "./implementations/cqrs/handlers/c/create-user-contact.js";
export type {
  CreateUserContactInput,
  CreateUserContactOutput,
} from "./interfaces/cqrs/handlers/c/create-user-contact.js";

export { UpdateUserRealName } from "./implementations/cqrs/handlers/c/update-user-real-name.js";
export type {
  UpdateUserRealNameInput,
  UpdateUserRealNameOutput,
} from "./interfaces/cqrs/handlers/c/update-user-real-name.js";

export { UpdateUsername } from "./implementations/cqrs/handlers/c/update-username.js";
export type {
  UpdateUsernameInput,
  UpdateUsernameOutput,
} from "./interfaces/cqrs/handlers/c/update-username.js";

export { RecordSignInOutcome } from "./implementations/cqrs/handlers/c/record-sign-in-outcome.js";
export type {
  RecordSignInOutcomeInput,
  RecordSignInOutcomeOutput,
} from "./interfaces/cqrs/handlers/c/record-sign-in-outcome.js";

// --- Query Handlers ---
export { GetSignInEvents } from "./implementations/cqrs/handlers/q/get-sign-in-events.js";
export type {
  GetSignInEventsInput,
  GetSignInEventsOutput,
} from "./interfaces/cqrs/handlers/q/get-sign-in-events.js";

export { GetActiveConsents } from "./implementations/cqrs/handlers/q/get-active-consents.js";
export type {
  GetActiveConsentsInput,
  GetActiveConsentsOutput,
} from "./interfaces/cqrs/handlers/q/get-active-consents.js";

export { GetOrgContacts } from "./implementations/cqrs/handlers/q/get-org-contacts.js";
export type {
  GetOrgContactsInput,
  GetOrgContactsOutput,
  HydratedOrgContact,
} from "./interfaces/cqrs/handlers/q/get-org-contacts.js";

export { CheckSignInThrottle } from "./implementations/cqrs/handlers/q/check-sign-in-throttle.js";
export type {
  CheckSignInThrottleInput,
  CheckSignInThrottleOutput,
} from "./interfaces/cqrs/handlers/q/check-sign-in-throttle.js";

export { CheckEmailAvailability } from "./implementations/cqrs/handlers/q/check-email-availability.js";
export type {
  CheckEmailAvailabilityInput,
  CheckEmailAvailabilityOutput,
} from "./interfaces/cqrs/handlers/q/check-email-availability.js";

// --- Factory Functions ---

import type {
  SignInEventRepoHandlers,
  EmulationConsentRepoHandlers,
  OrgContactRepoHandlers,
  ICheckOrgExistsHandler,
} from "./interfaces/repository/handlers/index.js";
import type { ISignInThrottleStore } from "./interfaces/repository/sign-in-throttle-store.js";
import type { InMemoryCache } from "@d2/interfaces";
import { RecordSignInEvent } from "./implementations/cqrs/handlers/c/record-sign-in-event.js";
import { GetSignInEvents } from "./implementations/cqrs/handlers/q/get-sign-in-events.js";
import { RecordSignInOutcome } from "./implementations/cqrs/handlers/c/record-sign-in-outcome.js";
import { CheckSignInThrottle } from "./implementations/cqrs/handlers/q/check-sign-in-throttle.js";
import { CreateEmulationConsent } from "./implementations/cqrs/handlers/c/create-emulation-consent.js";
import { RevokeEmulationConsent } from "./implementations/cqrs/handlers/c/revoke-emulation-consent.js";
import { GetActiveConsents } from "./implementations/cqrs/handlers/q/get-active-consents.js";
import { CreateOrgContact } from "./implementations/cqrs/handlers/c/create-org-contact.js";
import { UpdateOrgContactHandler } from "./implementations/cqrs/handlers/c/update-org-contact.js";
import { DeleteOrgContact } from "./implementations/cqrs/handlers/c/delete-org-contact.js";
import { GetOrgContacts } from "./implementations/cqrs/handlers/q/get-org-contacts.js";
import { CreateUserContact } from "./implementations/cqrs/handlers/c/create-user-contact.js";

/** Creates sign-in event handlers (mirrors .NET AddXxx() pattern). */
export function createSignInEventHandlers(
  repo: SignInEventRepoHandlers,
  context: IHandlerContext,
  memoryCache?: {
    get: InMemoryCache.IGetHandler<{
      events: SignInEvent[];
      total: number;
      latestDate?: string;
    }>;
    set: InMemoryCache.ISetHandler<{
      events: SignInEvent[];
      total: number;
      latestDate?: string;
    }>;
  },
) {
  return {
    record: new RecordSignInEvent(repo.create, context),
    getByUser: new GetSignInEvents(
      repo.findByUserId,
      repo.countByUserId,
      repo.getLatestEventDate,
      context,
      memoryCache,
    ),
  };
}

/** Creates emulation consent handlers. */
export function createEmulationConsentHandlers(
  repo: EmulationConsentRepoHandlers,
  context: IHandlerContext,
  checkOrgExists: ICheckOrgExistsHandler,
) {
  return {
    create: new CreateEmulationConsent(
      repo.create,
      repo.findActiveByUserIdAndOrg,
      context,
      checkOrgExists,
    ),
    revoke: new RevokeEmulationConsent(repo.findById, repo.revoke, context),
    getActive: new GetActiveConsents(repo.findActiveByUserId, context),
  };
}

/** Geo contact handler dependencies for org contact handlers. */
export interface OrgContactGeoDeps {
  createContacts: Commands.ICreateContactsHandler;
  deleteContactsByExtKeys: Commands.IDeleteContactsByExtKeysHandler;
  updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  getContactsByExtKeys: Queries.IGetContactsByExtKeysHandler;
}

/** Creates org contact handlers. */
export function createOrgContactHandlers(
  repo: OrgContactRepoHandlers,
  context: IHandlerContext,
  geo: OrgContactGeoDeps,
) {
  return {
    create: new CreateOrgContact(repo.create, repo.delete, context, geo.createContacts),
    update: new UpdateOrgContactHandler(
      repo.findById,
      repo.update,
      context,
      geo.updateContactsByExtKeys,
    ),
    delete: new DeleteOrgContact(repo.findById, repo.delete, context, geo.deleteContactsByExtKeys),
    getByOrg: new GetOrgContacts(repo.findByOrgId, context, geo.getContactsByExtKeys),
  };
}

/** Creates sign-in throttle handlers for brute-force protection. */
export function createSignInThrottleHandlers(
  store: ISignInThrottleStore,
  context: IHandlerContext,
  memoryCache?: {
    get: InMemoryCache.IGetHandler<boolean>;
    set: InMemoryCache.ISetHandler<boolean>;
  },
) {
  return {
    check: new CheckSignInThrottle(store, context, memoryCache),
    record: new RecordSignInOutcome(store, context, memoryCache),
  };
}

/** Return type of createSignInThrottleHandlers. */
export type SignInThrottleHandlers = ReturnType<typeof createSignInThrottleHandlers>;

/** Creates user contact handler for sign-up Geo contact creation. */
export function createUserContactHandler(
  createContacts: Commands.ICreateContactsHandler,
  context: IHandlerContext,
) {
  return new CreateUserContact(createContacts, context);
}

// --- Health Check Handler ---
export { CheckHealth } from "./implementations/cqrs/handlers/q/check-health.js";
export type {
  CheckHealthInput,
  CheckHealthOutput,
  ComponentHealth,
} from "./interfaces/cqrs/handlers/q/check-health.js";

// --- Job Handlers ---
export { RunSessionPurge } from "./implementations/cqrs/handlers/c/run-session-purge.js";
export type {
  RunSessionPurgeInput,
  RunSessionPurgeOutput,
} from "./interfaces/cqrs/handlers/c/run-session-purge.js";

export { RunSignInEventPurge } from "./implementations/cqrs/handlers/c/run-sign-in-event-purge.js";
export type {
  RunSignInEventPurgeInput,
  RunSignInEventPurgeOutput,
} from "./interfaces/cqrs/handlers/c/run-sign-in-event-purge.js";

export { RunInvitationCleanup } from "./implementations/cqrs/handlers/c/run-invitation-cleanup.js";
export type {
  RunInvitationCleanupInput,
  RunInvitationCleanupOutput,
} from "./interfaces/cqrs/handlers/c/run-invitation-cleanup.js";

export { RunEmulationConsentCleanup } from "./implementations/cqrs/handlers/c/run-emulation-consent-cleanup.js";
export type {
  RunEmulationConsentCleanupInput,
  RunEmulationConsentCleanupOutput,
} from "./interfaces/cqrs/handlers/c/run-emulation-consent-cleanup.js";

export { HandleFileProcessed } from "./implementations/cqrs/handlers/c/handle-file-processed.js";
export type {
  HandleFileProcessedInput,
  HandleFileProcessedOutput,
} from "./interfaces/cqrs/handlers/c/handle-file-processed.js";

export { InvalidateUserSessionCache } from "./implementations/cqrs/handlers/c/invalidate-user-session-cache.js";
export type {
  InvalidateUserSessionCacheInput,
  InvalidateUserSessionCacheOutput,
} from "./interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";

// --- Job Options ---
export type { AuthJobOptions } from "./auth-job-options.js";
export { DEFAULT_AUTH_JOB_OPTIONS } from "./auth-job-options.js";

// --- DI Registration ---
export { addAuthApp } from "./registration.js";
export {
  // Infra-layer keys (interfaces defined here, implemented in auth-infra)
  ICreateSignInEventKey,
  IFindSignInEventsByUserIdKey,
  ICountSignInEventsByUserIdKey,
  IGetLatestSignInEventDateKey,
  IUpdateSignInEventWhoIsIdKey,
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
  ISignInThrottleStoreKey,
  // App-layer keys
  IRecordSignInEventKey,
  IRecordSignInOutcomeKey,
  ICreateEmulationConsentKey,
  IRevokeEmulationConsentKey,
  ICreateOrgContactKey,
  IUpdateOrgContactKey,
  IDeleteOrgContactKey,
  ICreateUserContactKey,
  IGetSignInEventsKey,
  IGetActiveConsentsKey,
  IGetOrgContactsKey,
  ICheckSignInThrottleKey,
  ICheckEmailAvailabilityKey,
  ICheckEmailAvailabilityRepoKey,
  ICheckOrgExistsKey,
  IPingDbKey,
  IPushUserUpdatedKey,
  ICheckHealthKey,
  // Job keys
  IPurgeExpiredSessionsKey,
  IPurgeSignInEventsKey,
  IPurgeExpiredInvitationsKey,
  IPurgeExpiredEmulationConsentsKey,
  IRunSessionPurgeKey,
  IRunSignInEventPurgeKey,
  IRunInvitationCleanupKey,
  IRunEmulationConsentCleanupKey,
  IHandleFileProcessedKey,
  IInvalidateUserSessionCacheKey,
  IUpdateUserImageKey,
  IUpdateOrgLogoKey,
  IUpdateUserRealNameKey,
  IUpdateUsernameKey,
  IUpdateUserNameKey,
  ICheckUsernameAvailableKey,
  IUpdateUserUsernameKey,
} from "./service-keys.js";
