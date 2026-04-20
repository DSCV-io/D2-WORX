import type { ServiceCollection } from "@d2/di";
import { IHandlerContextKey } from "@d2/handler";
import {
  ICreateContactsKey,
  IDeleteContactsByExtKeysKey,
  IGetContactsByExtKeysKey,
  IUpdateContactsByExtKeysKey,
  IFindWhoIsKey,
} from "@d2/geo-client";
import {
  // Infra keys (interfaces defined here, implemented in auth-infra)
  ICreateSignInEventKey,
  IFindSignInEventsByUserIdKey,
  ICountSignInEventsByUserIdKey,
  IGetLatestSignInEventDateKey,
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
  // App keys
  IRecordSignInEventKey,
  IRecordSignInOutcomeKey,
  ICreateEmulationConsentKey,
  IRevokeEmulationConsentKey,
  ICreateOrgContactKey,
  IUpdateOrgContactKey,
  IDeleteOrgContactKey,
  ICreateUserContactKey,
  IGetSignInEventsKey,
  IGetMySessionsKey,
  IFindActiveSessionsByUserIdKey,
  IGetActiveConsentsKey,
  IGetOrgContactsKey,
  ICheckSignInThrottleKey,
  ICheckEmailAvailabilityKey,
  ICheckEmailAvailabilityRepoKey,
  ICheckOrgExistsKey,
  IPingDbKey,
  ICheckHealthKey,
} from "./service-keys.js";
import { RecordSignInEvent } from "./implementations/cqrs/handlers/c/record-sign-in-event.js";
import { RecordSignInOutcome } from "./implementations/cqrs/handlers/c/record-sign-in-outcome.js";
import { CreateEmulationConsent } from "./implementations/cqrs/handlers/c/create-emulation-consent.js";
import { RevokeEmulationConsent } from "./implementations/cqrs/handlers/c/revoke-emulation-consent.js";
import { CreateOrgContact } from "./implementations/cqrs/handlers/c/create-org-contact.js";
import { UpdateOrgContactHandler } from "./implementations/cqrs/handlers/c/update-org-contact.js";
import { DeleteOrgContact } from "./implementations/cqrs/handlers/c/delete-org-contact.js";
import { CreateUserContact } from "./implementations/cqrs/handlers/c/create-user-contact.js";
import { UpdateUserRealName } from "./implementations/cqrs/handlers/c/update-user-real-name.js";
import { UpdateUsername } from "./implementations/cqrs/handlers/c/update-username.js";
import { UpdateUserLocale } from "./implementations/cqrs/handlers/c/update-user-locale.js";
import { UpdateUserTimezone } from "./implementations/cqrs/handlers/c/update-user-timezone.js";
import { GetSignInEvents } from "./implementations/cqrs/handlers/q/get-sign-in-events.js";
import { GetMySessions } from "./implementations/cqrs/handlers/q/get-my-sessions.js";
import { GetActiveConsents } from "./implementations/cqrs/handlers/q/get-active-consents.js";
import { GetOrgContacts } from "./implementations/cqrs/handlers/q/get-org-contacts.js";
import { CheckSignInThrottle } from "./implementations/cqrs/handlers/q/check-sign-in-throttle.js";
import { CheckHealth } from "./implementations/cqrs/handlers/q/check-health.js";
import { CheckEmailAvailability } from "./implementations/cqrs/handlers/q/check-email-availability.js";
import { RequestEmailChange } from "./implementations/cqrs/handlers/c/request-email-change.js";
import { VerifyEmailChange } from "./implementations/cqrs/handlers/c/verify-email-change.js";
import { RequestPhoneChange } from "./implementations/cqrs/handlers/c/request-phone-change.js";
import { VerifyPhoneChange } from "./implementations/cqrs/handlers/c/verify-phone-change.js";
import { RemovePhone } from "./implementations/cqrs/handlers/c/remove-phone.js";
import { INotifyKey } from "@d2/comms-client";
import { DistributedCache } from "@d2/interfaces";
import { IMessageBusPingKey } from "@d2/messaging";
import { RunSessionPurge } from "./implementations/cqrs/handlers/c/run-session-purge.js";
import { RunSignInEventPurge } from "./implementations/cqrs/handlers/c/run-sign-in-event-purge.js";
import { RunInvitationCleanup } from "./implementations/cqrs/handlers/c/run-invitation-cleanup.js";
import { RunEmulationConsentCleanup } from "./implementations/cqrs/handlers/c/run-emulation-consent-cleanup.js";
import { HandleFileProcessed } from "./implementations/cqrs/handlers/c/handle-file-processed.js";
import { InvalidateUserSessionCache } from "./implementations/cqrs/handlers/c/invalidate-user-session-cache.js";
import type { AuthJobOptions } from "./auth-job-options.js";
import { DEFAULT_AUTH_JOB_OPTIONS } from "./auth-job-options.js";
import {
  IPurgeExpiredSessionsKey,
  IPurgeSignInEventsKey,
  IPurgeExpiredInvitationsKey,
  IPurgeExpiredEmulationConsentsKey,
  IRunSessionPurgeKey,
  IRunSignInEventPurgeKey,
  IRunInvitationCleanupKey,
  IRunEmulationConsentCleanupKey,
  IHandleFileProcessedKey,
  IUpdateUserImageKey,
  IUpdateOrgLogoKey,
  IUpdateUserRealNameKey,
  IUpdateUsernameKey,
  IUpdateUserLocaleKey,
  IUpdateUserLocaleRepoKey,
  IUpdateUserTimezoneKey,
  IUpdateUserTimezoneRepoKey,
  IPushUserUpdatedKey,
  IUpdateUserNameKey,
  ICheckUsernameAvailableKey,
  IUpdateUserUsernameKey,
  IInvalidateUserSessionCacheKey,
  IRequestEmailChangeKey,
  IVerifyEmailChangeKey,
  IRequestPhoneChangeKey,
  IVerifyPhoneChangeKey,
  IRemovePhoneKey,
  IOtpRateLimitStoreKey,
  IVerificationStoreKey,
  IVerifyUserPasswordKey,
  ICheckPhoneAvailabilityKey,
  IGetUserByIdKey,
  IUpdateUserEmailKey,
  IUpdateUserPhoneKey,
  ITranslatorKey,
} from "./service-keys.js";

/**
 * Registers auth application-layer services (CQRS handlers, notification publishers)
 * with the DI container. Mirrors .NET's `services.AddAuthApp()` pattern.
 *
 * All CQRS handlers are transient — new instance per resolve.
 */
export function addAuthApp(
  services: ServiceCollection,
  jobOptions: AuthJobOptions = DEFAULT_AUTH_JOB_OPTIONS,
): void {
  // --- Command Handlers ---

  services.addTransient(
    IRecordSignInEventKey,
    (sp) =>
      new RecordSignInEvent(sp.resolve(ICreateSignInEventKey), sp.resolve(IHandlerContextKey)),
  );

  services.addTransient(
    IRecordSignInOutcomeKey,
    (sp) =>
      new RecordSignInOutcome(sp.resolve(ISignInThrottleStoreKey), sp.resolve(IHandlerContextKey)),
  );

  services.addTransient(
    ICreateEmulationConsentKey,
    (sp) =>
      new CreateEmulationConsent(
        sp.resolve(ICreateEmulationConsentRecordKey),
        sp.resolve(IFindActiveConsentByUserIdAndOrgKey),
        sp.resolve(IHandlerContextKey),
        sp.resolve(ICheckOrgExistsKey),
      ),
  );

  services.addTransient(
    IRevokeEmulationConsentKey,
    (sp) =>
      new RevokeEmulationConsent(
        sp.resolve(IFindEmulationConsentByIdKey),
        sp.resolve(IRevokeEmulationConsentRecordKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    ICreateOrgContactKey,
    (sp) =>
      new CreateOrgContact(
        sp.resolve(ICreateOrgContactRecordKey),
        sp.resolve(IDeleteOrgContactRecordKey),
        sp.resolve(IHandlerContextKey),
        sp.resolve(ICreateContactsKey),
      ),
  );

  services.addTransient(
    IUpdateOrgContactKey,
    (sp) =>
      new UpdateOrgContactHandler(
        sp.resolve(IFindOrgContactByIdKey),
        sp.resolve(IUpdateOrgContactRecordKey),
        sp.resolve(IHandlerContextKey),
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IUpdateContactsByExtKeysKey),
      ),
  );

  services.addTransient(
    IDeleteOrgContactKey,
    (sp) =>
      new DeleteOrgContact(
        sp.resolve(IFindOrgContactByIdKey),
        sp.resolve(IDeleteOrgContactRecordKey),
        sp.resolve(IHandlerContextKey),
        sp.resolve(IDeleteContactsByExtKeysKey),
      ),
  );

  services.addTransient(
    ICreateUserContactKey,
    (sp) => new CreateUserContact(sp.resolve(ICreateContactsKey), sp.resolve(IHandlerContextKey)),
  );

  services.addTransient(
    IUpdateUserRealNameKey,
    (sp) =>
      new UpdateUserRealName(
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IUpdateContactsByExtKeysKey),
        sp.resolve(IUpdateUserNameKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );

  services.addTransient(
    IUpdateUsernameKey,
    (sp) =>
      new UpdateUsername(
        sp.resolve(ICheckUsernameAvailableKey),
        sp.resolve(IUpdateUserUsernameKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );

  services.addTransient(
    IUpdateUserLocaleKey,
    (sp) =>
      new UpdateUserLocale(
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IUpdateContactsByExtKeysKey),
        sp.resolve(IUpdateUserLocaleRepoKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );

  services.addTransient(
    IUpdateUserTimezoneKey,
    (sp) =>
      new UpdateUserTimezone(
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IUpdateContactsByExtKeysKey),
        sp.resolve(IUpdateUserTimezoneRepoKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );

  services.addTransient(
    IRequestEmailChangeKey,
    (sp) =>
      new RequestEmailChange(
        sp.resolve(IVerifyUserPasswordKey),
        sp.resolve(IOtpRateLimitStoreKey),
        sp.resolve(IVerificationStoreKey),
        sp.resolve(ICheckEmailAvailabilityRepoKey),
        sp.resolve(IUpdateUserEmailKey),
        sp.resolve(IGetUserByIdKey),
        sp.resolve(INotifyKey),
        sp.resolve(ITranslatorKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IVerifyEmailChangeKey,
    (sp) =>
      new VerifyEmailChange(
        sp.resolve(IVerificationStoreKey),
        sp.resolve(IOtpRateLimitStoreKey),
        sp.resolve(IUpdateUserEmailKey),
        sp.resolve(IGetUserByIdKey),
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IUpdateContactsByExtKeysKey),
        sp.resolve(INotifyKey),
        sp.resolve(ITranslatorKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );

  services.addTransient(
    IRequestPhoneChangeKey,
    (sp) =>
      new RequestPhoneChange(
        sp.resolve(IVerifyUserPasswordKey),
        sp.resolve(IOtpRateLimitStoreKey),
        sp.resolve(IVerificationStoreKey),
        sp.resolve(ICheckPhoneAvailabilityKey),
        sp.resolve(IGetUserByIdKey),
        sp.resolve(INotifyKey),
        sp.resolve(ITranslatorKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IVerifyPhoneChangeKey,
    (sp) =>
      new VerifyPhoneChange(
        sp.resolve(IVerificationStoreKey),
        sp.resolve(IOtpRateLimitStoreKey),
        sp.resolve(IUpdateUserPhoneKey),
        sp.resolve(IGetUserByIdKey),
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IUpdateContactsByExtKeysKey),
        sp.resolve(INotifyKey),
        sp.resolve(ITranslatorKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );

  services.addTransient(
    IRemovePhoneKey,
    (sp) =>
      new RemovePhone(
        sp.resolve(IVerifyUserPasswordKey),
        sp.resolve(IGetUserByIdKey),
        sp.resolve(IUpdateUserPhoneKey),
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IUpdateContactsByExtKeysKey),
        sp.resolve(INotifyKey),
        sp.resolve(ITranslatorKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );

  // --- Query Handlers ---

  services.addTransient(
    IGetSignInEventsKey,
    (sp) =>
      new GetSignInEvents(
        sp.resolve(IFindSignInEventsByUserIdKey),
        sp.resolve(ICountSignInEventsByUserIdKey),
        sp.resolve(IGetLatestSignInEventDateKey),
        sp.resolve(IFindWhoIsKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IGetMySessionsKey,
    (sp) =>
      new GetMySessions(
        sp.resolve(IFindActiveSessionsByUserIdKey),
        sp.resolve(IFindWhoIsKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IGetActiveConsentsKey,
    (sp) =>
      new GetActiveConsents(
        sp.resolve(IFindActiveConsentsByUserIdKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IGetOrgContactsKey,
    (sp) =>
      new GetOrgContacts(
        sp.resolve(IFindOrgContactsByOrgIdKey),
        sp.resolve(IHandlerContextKey),
        sp.resolve(IGetContactsByExtKeysKey),
      ),
  );

  services.addTransient(
    ICheckSignInThrottleKey,
    (sp) =>
      new CheckSignInThrottle(sp.resolve(ISignInThrottleStoreKey), sp.resolve(IHandlerContextKey)),
  );

  services.addTransient(
    ICheckEmailAvailabilityKey,
    (sp) =>
      new CheckEmailAvailability(
        sp.resolve(ICheckEmailAvailabilityRepoKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    ICheckHealthKey,
    (sp) =>
      new CheckHealth(
        sp.resolve(IPingDbKey),
        sp.resolve(DistributedCache.IDistributedCachePingKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IMessageBusPingKey),
      ),
  );

  // --- Job Handlers ---

  services.addTransient(
    IRunSessionPurgeKey,
    (sp) =>
      new RunSessionPurge(
        sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
        sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
        sp.resolve(IPurgeExpiredSessionsKey),
        jobOptions,
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IRunSignInEventPurgeKey,
    (sp) =>
      new RunSignInEventPurge(
        sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
        sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
        sp.resolve(IPurgeSignInEventsKey),
        jobOptions,
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IRunInvitationCleanupKey,
    (sp) =>
      new RunInvitationCleanup(
        sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
        sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
        sp.resolve(IPurgeExpiredInvitationsKey),
        jobOptions,
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IRunEmulationConsentCleanupKey,
    (sp) =>
      new RunEmulationConsentCleanup(
        sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
        sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
        sp.resolve(IPurgeExpiredEmulationConsentsKey),
        jobOptions,
        sp.resolve(IHandlerContextKey),
      ),
  );

  // Session cache invalidation (bust BetterAuth's Redis-cached sessions after user mutations)
  services.addTransient(
    IInvalidateUserSessionCacheKey,
    (sp) =>
      new InvalidateUserSessionCache(
        sp.resolve(DistributedCache.IDistributedCacheGetKey),
        sp.resolve(DistributedCache.IDistributedCacheRemoveKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  // File callback handler
  services.addTransient(
    IHandleFileProcessedKey,
    (sp) =>
      new HandleFileProcessed(
        sp.resolve(IUpdateUserImageKey),
        sp.resolve(IUpdateOrgLogoKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(IPushUserUpdatedKey),
        sp.tryResolve(IInvalidateUserSessionCacheKey),
      ),
  );
}
