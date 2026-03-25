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

export interface AuthInfraConfig {
  readonly db: NodePgDatabase;
  /** gRPC address of the SignalR Gateway (e.g., "d2-signalr:5401"). Optional — push disabled if not set. */
  readonly signalrGatewayAddress?: string;
  /** API key for authenticating gRPC calls to the SignalR Gateway. */
  readonly signalrApiKey?: string;
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
