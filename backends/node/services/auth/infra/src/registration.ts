import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import type { ServiceCollection } from "@d2/di";
import { IHandlerContextKey } from "@d2/handler";
import {
  IPingDbKey,
  ICreateSignInEventKey,
  IFindSignInEventsByUserIdKey,
  ICountSignInEventsByUserIdKey,
  IGetLatestSignInEventDateKey,
  IUpdateSignInEventWhoIsIdKey,
  IUpdateSessionWhoIsIdKey,
  IUpdateUserImageKey,
  IUpdateOrgLogoKey,
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
  IPurgeExpiredSessionsKey,
  IPurgeSignInEventsKey,
  IPurgeExpiredInvitationsKey,
  IPurgeExpiredEmulationConsentsKey,
  ICheckEmailAvailabilityRepoKey,
  ICheckOrgExistsKey,
  IUpdateUserNameKey,
  ICheckUsernameAvailableKey,
  IUpdateUserUsernameKey,
  IUpdateUserLocaleRepoKey,
  IUpdateUserTimezoneRepoKey,
  IUpdateUserEmailKey,
  IUpdateUserPhoneKey,
  ICheckPhoneAvailabilityKey,
  IGetUserByIdKey,
  IGetActiveSessionsByUserIdKey,
  IGetUserIdByIdentifierKey,
  IUpdateUserStatusKey,
  IGetDeletedUsersToPurgeKey,
  IAnonymizeUserKey,
  ICheckSoleOwnerOrgsKey,
  IDeleteAllUserSessionsKey,
  IPushUserUpdatedKey,
} from "@d2/auth-app";
import { PushUserUpdated } from "./realtime/handlers/push-user-updated.js";
import { CreateSignInEvent } from "./repository/handlers/c/create-sign-in-event.js";
import { FindSignInEventsByUserId } from "./repository/handlers/r/find-sign-in-events-by-user-id.js";
import { CountSignInEventsByUserId } from "./repository/handlers/r/count-sign-in-events-by-user-id.js";
import { GetLatestSignInEventDate } from "./repository/handlers/r/get-latest-sign-in-event-date.js";
import { CreateEmulationConsentRecord } from "./repository/handlers/c/create-emulation-consent-record.js";
import { FindEmulationConsentById } from "./repository/handlers/r/find-emulation-consent-by-id.js";
import { FindActiveConsentsByUserId } from "./repository/handlers/r/find-active-consents-by-user-id.js";
import { FindActiveConsentByUserIdAndOrg } from "./repository/handlers/r/find-active-consent-by-user-id-and-org.js";
import { RevokeEmulationConsentRecord } from "./repository/handlers/u/revoke-emulation-consent-record.js";
import { UpdateSignInEventWhoIsId } from "./repository/handlers/u/update-sign-in-event-who-is-id.js";
import { UpdateSessionWhoIsId } from "./repository/handlers/u/update-session-who-is-id.js";
import { UpdateUserImage } from "./repository/handlers/u/update-user-image.js";
import { UpdateOrgLogo } from "./repository/handlers/u/update-org-logo.js";
import { CreateOrgContactRecord } from "./repository/handlers/c/create-org-contact-record.js";
import { FindOrgContactById } from "./repository/handlers/r/find-org-contact-by-id.js";
import { FindOrgContactsByOrgId } from "./repository/handlers/r/find-org-contacts-by-org-id.js";
import { UpdateOrgContactRecord } from "./repository/handlers/u/update-org-contact-record.js";
import { DeleteOrgContactRecord } from "./repository/handlers/d/delete-org-contact-record.js";
import { PurgeExpiredSessions } from "./repository/handlers/d/purge-expired-sessions.js";
import { PurgeSignInEvents } from "./repository/handlers/d/purge-sign-in-events.js";
import { PurgeExpiredInvitations } from "./repository/handlers/d/purge-expired-invitations.js";
import { PurgeExpiredEmulationConsents } from "./repository/handlers/d/purge-expired-emulation-consents.js";
import { PingDb } from "./repository/handlers/r/ping-db.js";
import { CheckEmailAvailability } from "./repository/handlers/r/check-email-availability.js";
import { CheckOrgExists } from "./repository/handlers/r/check-org-exists.js";
import { UpdateUserName } from "./repository/handlers/u/update-user-name.js";
import { CheckUsernameAvailable } from "./repository/handlers/r/check-username-available.js";
import { UpdateUserUsername } from "./repository/handlers/u/update-user-username.js";
import { UpdateUserLocale } from "./repository/handlers/u/update-user-locale.js";
import { UpdateUserEmail } from "./repository/handlers/u/update-user-email.js";
import { UpdateUserPhone } from "./repository/handlers/u/update-user-phone.js";
import { CheckPhoneAvailability } from "./repository/handlers/r/check-phone-availability.js";
import { GetUserById } from "./repository/handlers/r/get-user-by-id.js";
import { GetActiveSessionsByUserId } from "./repository/handlers/r/get-active-sessions-by-user-id.js";
import { GetUserIdByIdentifier } from "./repository/handlers/r/get-user-id-by-identifier.js";
import { UpdateUserStatus } from "./repository/handlers/u/update-user-status.js";
import { GetDeletedUsersToPurge } from "./repository/handlers/r/get-deleted-users-to-purge.js";
import { AnonymizeUser } from "./repository/handlers/u/anonymize-user.js";
import { CheckSoleOwnerOrgs } from "./repository/handlers/r/check-sole-owner-orgs.js";
import { DeleteAllUserSessions } from "./repository/handlers/d/delete-all-user-sessions.js";
import { UpdateUserTimezone } from "./repository/handlers/u/update-user-timezone.js";

export interface AuthInfraConfig {
  readonly db: NodePgDatabase;
  /** gRPC address of the SignalR Gateway (e.g., "d2-signalr:5401"). Optional — push disabled if not set. */
  readonly signalrGatewayAddress?: string;
  /** API key for authenticating gRPC calls to the SignalR Gateway. */
  readonly signalrApiKey?: string;
  /**
   * Defense-in-depth cap on the number of pending-deletion users
   * `GetDeletedUsersToPurge` returns per nightly tick. Defaults to
   * `AuthJobOptions.userPurgeBatchSize` (50000) when not provided.
   */
  readonly userPurgeBatchSize?: number;
}

/**
 * Registers auth infrastructure services (repository handlers, realtime handlers)
 * with the DI container. Mirrors .NET's `services.AddAuthInfra(configuration)` pattern.
 *
 * All handlers are transient — new instance per resolve, receiving scoped IHandlerContext.
 */
export function addAuthInfra(
  services: ServiceCollection,
  db: NodePgDatabase,
  config?: Omit<AuthInfraConfig, "db">,
): void {
  // Health check handler
  services.addTransient(IPingDbKey, (sp) => new PingDb(db, sp.resolve(IHandlerContextKey)));

  // Email availability check (public, pre-auth)
  services.addTransient(
    ICheckEmailAvailabilityRepoKey,
    (sp) => new CheckEmailAvailability(db, sp.resolve(IHandlerContextKey)),
  );

  // Organization existence check
  services.addTransient(
    ICheckOrgExistsKey,
    (sp) => new CheckOrgExists(db, sp.resolve(IHandlerContextKey)),
  );

  // Sign-in event repo handlers
  services.addTransient(
    ICreateSignInEventKey,
    (sp) => new CreateSignInEvent(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindSignInEventsByUserIdKey,
    (sp) => new FindSignInEventsByUserId(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    ICountSignInEventsByUserIdKey,
    (sp) => new CountSignInEventsByUserId(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IGetLatestSignInEventDateKey,
    (sp) => new GetLatestSignInEventDate(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateSignInEventWhoIsIdKey,
    (sp) => new UpdateSignInEventWhoIsId(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateSessionWhoIsIdKey,
    (sp) => new UpdateSessionWhoIsId(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateUserImageKey,
    (sp) => new UpdateUserImage(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateOrgLogoKey,
    (sp) => new UpdateOrgLogo(db, sp.resolve(IHandlerContextKey)),
  );

  // User account repo handlers
  services.addTransient(
    IUpdateUserNameKey,
    (sp) => new UpdateUserName(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    ICheckUsernameAvailableKey,
    (sp) => new CheckUsernameAvailable(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateUserUsernameKey,
    (sp) => new UpdateUserUsername(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateUserLocaleRepoKey,
    (sp) => new UpdateUserLocale(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateUserTimezoneRepoKey,
    (sp) => new UpdateUserTimezone(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateUserEmailKey,
    (sp) => new UpdateUserEmail(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateUserPhoneKey,
    (sp) => new UpdateUserPhone(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    ICheckPhoneAvailabilityKey,
    (sp) => new CheckPhoneAvailability(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IGetUserByIdKey,
    (sp) => new GetUserById(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IGetActiveSessionsByUserIdKey,
    (sp) => new GetActiveSessionsByUserId(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IGetUserIdByIdentifierKey,
    (sp) => new GetUserIdByIdentifier(db, sp.resolve(IHandlerContextKey)),
  );

  // User deletion repo handlers
  services.addTransient(
    IUpdateUserStatusKey,
    (sp) => new UpdateUserStatus(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IGetDeletedUsersToPurgeKey,
    (sp) =>
      new GetDeletedUsersToPurge(db, sp.resolve(IHandlerContextKey), config?.userPurgeBatchSize),
  );
  services.addTransient(
    IAnonymizeUserKey,
    (sp) => new AnonymizeUser(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    ICheckSoleOwnerOrgsKey,
    (sp) => new CheckSoleOwnerOrgs(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IDeleteAllUserSessionsKey,
    (sp) => new DeleteAllUserSessions(db, sp.resolve(IHandlerContextKey)),
  );

  // Emulation consent repo handlers
  services.addTransient(
    ICreateEmulationConsentRecordKey,
    (sp) => new CreateEmulationConsentRecord(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindEmulationConsentByIdKey,
    (sp) => new FindEmulationConsentById(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindActiveConsentsByUserIdKey,
    (sp) => new FindActiveConsentsByUserId(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindActiveConsentByUserIdAndOrgKey,
    (sp) => new FindActiveConsentByUserIdAndOrg(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IRevokeEmulationConsentRecordKey,
    (sp) => new RevokeEmulationConsentRecord(db, sp.resolve(IHandlerContextKey)),
  );

  // Org contact repo handlers
  services.addTransient(
    ICreateOrgContactRecordKey,
    (sp) => new CreateOrgContactRecord(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindOrgContactByIdKey,
    (sp) => new FindOrgContactById(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindOrgContactsByOrgIdKey,
    (sp) => new FindOrgContactsByOrgId(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateOrgContactRecordKey,
    (sp) => new UpdateOrgContactRecord(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IDeleteOrgContactRecordKey,
    (sp) => new DeleteOrgContactRecord(db, sp.resolve(IHandlerContextKey)),
  );

  // Job purge repo handlers
  services.addTransient(
    IPurgeExpiredSessionsKey,
    (sp) => new PurgeExpiredSessions(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IPurgeSignInEventsKey,
    (sp) => new PurgeSignInEvents(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IPurgeExpiredInvitationsKey,
    (sp) => new PurgeExpiredInvitations(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IPurgeExpiredEmulationConsentsKey,
    (sp) => new PurgeExpiredEmulationConsents(db, sp.resolve(IHandlerContextKey)),
  );

  // --- Realtime Handlers ---

  if (config?.signalrGatewayAddress && config.signalrApiKey) {
    services.addTransient(
      IPushUserUpdatedKey,
      (sp) =>
        new PushUserUpdated(
          config.signalrGatewayAddress!,
          config.signalrApiKey!,
          sp.resolve(IHandlerContextKey),
        ),
    );
  }
}
