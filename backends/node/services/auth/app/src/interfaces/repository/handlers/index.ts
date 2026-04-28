// --- Handler type imports (used by bundle interfaces below) ---
import type { ICreateSignInEventHandler } from "./c/create-sign-in-event.js";
import type { ICreateEmulationConsentRecordHandler } from "./c/create-emulation-consent-record.js";
import type { ICreateOrgContactRecordHandler } from "./c/create-org-contact-record.js";
import type { IFindSignInEventsByUserIdHandler } from "./r/find-sign-in-events-by-user-id.js";
import type { ICountSignInEventsByUserIdHandler } from "./r/count-sign-in-events-by-user-id.js";
import type { IGetLatestSignInEventDateHandler } from "./r/get-latest-sign-in-event-date.js";
import type { IFindEmulationConsentByIdHandler } from "./r/find-emulation-consent-by-id.js";
import type { IFindActiveConsentsByUserIdHandler } from "./r/find-active-consents-by-user-id.js";
import type { IFindActiveConsentByUserIdAndOrgHandler } from "./r/find-active-consent-by-user-id-and-org.js";
import type { IFindOrgContactByIdHandler } from "./r/find-org-contact-by-id.js";
import type { IFindOrgContactsByOrgIdHandler } from "./r/find-org-contacts-by-org-id.js";
import type { IRevokeEmulationConsentRecordHandler } from "./u/revoke-emulation-consent-record.js";
import type { IUpdateOrgContactRecordHandler } from "./u/update-org-contact-record.js";
import type { IUpdateSignInEventWhoIsIdHandler } from "./u/update-sign-in-event-who-is-id.js";
import type { IDeleteOrgContactRecordHandler } from "./d/delete-org-contact-record.js";

// --- Create (C) ---
export type {
  CreateSignInEventInput,
  CreateSignInEventOutput,
  ICreateSignInEventHandler,
} from "./c/create-sign-in-event.js";

export type {
  CreateEmulationConsentRecordInput,
  CreateEmulationConsentRecordOutput,
  ICreateEmulationConsentRecordHandler,
} from "./c/create-emulation-consent-record.js";

export type {
  CreateOrgContactRecordInput,
  CreateOrgContactRecordOutput,
  ICreateOrgContactRecordHandler,
} from "./c/create-org-contact-record.js";
export { CREATE_ORG_CONTACT_RECORD_REDACTION } from "./c/create-org-contact-record.js";

// --- Read (R) ---
export type {
  CheckEmailAvailabilityInput,
  CheckEmailAvailabilityOutput,
  ICheckEmailAvailabilityHandler,
} from "./r/check-email-availability.js";

export type {
  CheckOrgExistsInput,
  CheckOrgExistsOutput,
  ICheckOrgExistsHandler,
} from "./r/check-org-exists.js";

export type {
  CheckUsernameAvailableInput,
  CheckUsernameAvailableOutput,
  ICheckUsernameAvailableHandler,
} from "./r/check-username-available.js";
export { CHECK_USERNAME_AVAILABLE_REDACTION } from "./r/check-username-available.js";

export type {
  FindSignInEventsByUserIdInput,
  FindSignInEventsByUserIdOutput,
  IFindSignInEventsByUserIdHandler,
} from "./r/find-sign-in-events-by-user-id.js";

export type {
  CountSignInEventsByUserIdInput,
  CountSignInEventsByUserIdOutput,
  ICountSignInEventsByUserIdHandler,
} from "./r/count-sign-in-events-by-user-id.js";

export type {
  GetLatestSignInEventDateInput,
  GetLatestSignInEventDateOutput,
  IGetLatestSignInEventDateHandler,
} from "./r/get-latest-sign-in-event-date.js";

export type {
  FindEmulationConsentByIdInput,
  FindEmulationConsentByIdOutput,
  IFindEmulationConsentByIdHandler,
} from "./r/find-emulation-consent-by-id.js";

export type {
  FindActiveConsentsByUserIdInput,
  FindActiveConsentsByUserIdOutput,
  IFindActiveConsentsByUserIdHandler,
} from "./r/find-active-consents-by-user-id.js";

export type {
  FindActiveConsentByUserIdAndOrgInput,
  FindActiveConsentByUserIdAndOrgOutput,
  IFindActiveConsentByUserIdAndOrgHandler,
} from "./r/find-active-consent-by-user-id-and-org.js";

export type {
  FindOrgContactByIdInput,
  FindOrgContactByIdOutput,
  IFindOrgContactByIdHandler,
} from "./r/find-org-contact-by-id.js";
export { FIND_ORG_CONTACT_BY_ID_REDACTION } from "./r/find-org-contact-by-id.js";

export type {
  FindOrgContactsByOrgIdInput,
  FindOrgContactsByOrgIdOutput,
  IFindOrgContactsByOrgIdHandler,
} from "./r/find-org-contacts-by-org-id.js";
export { FIND_ORG_CONTACTS_BY_ORG_ID_REDACTION } from "./r/find-org-contacts-by-org-id.js";

// --- Update (U) ---
export type {
  RevokeEmulationConsentRecordInput,
  RevokeEmulationConsentRecordOutput,
  IRevokeEmulationConsentRecordHandler,
} from "./u/revoke-emulation-consent-record.js";

export type {
  UpdateOrgContactRecordInput,
  UpdateOrgContactRecordOutput,
  IUpdateOrgContactRecordHandler,
} from "./u/update-org-contact-record.js";
export { UPDATE_ORG_CONTACT_RECORD_REDACTION } from "./u/update-org-contact-record.js";

export type {
  UpdateUserNameInput,
  UpdateUserNameOutput,
  IUpdateUserNameHandler,
} from "./u/update-user-name.js";
export { UPDATE_USER_NAME_REDACTION } from "./u/update-user-name.js";

export type {
  UpdateUserUsernameInput,
  UpdateUserUsernameOutput,
  IUpdateUserUsernameHandler,
} from "./u/update-user-username.js";
export { UPDATE_USER_USERNAME_REDACTION } from "./u/update-user-username.js";

export type {
  UpdateSignInEventWhoIsIdInput,
  UpdateSignInEventWhoIsIdOutput,
  IUpdateSignInEventWhoIsIdHandler,
} from "./u/update-sign-in-event-who-is-id.js";

export type {
  UpdateSessionWhoIsIdInput,
  UpdateSessionWhoIsIdOutput,
  IUpdateSessionWhoIsIdHandler,
} from "./u/update-session-who-is-id.js";

export type {
  UpdateUserImageInput,
  UpdateUserImageOutput,
  IUpdateUserImageHandler,
} from "./u/update-user-image.js";
export { UPDATE_USER_IMAGE_REDACTION } from "./u/update-user-image.js";

export type {
  UpdateUserLocaleInput,
  UpdateUserLocaleOutput,
  IUpdateUserLocaleHandler,
} from "./u/update-user-locale.js";

export type {
  UpdateUserTimezoneInput,
  UpdateUserTimezoneOutput,
  IUpdateUserTimezoneHandler,
} from "./u/update-user-timezone.js";

export type {
  UpdateUserEmailInput,
  UpdateUserEmailOutput,
  IUpdateUserEmailHandler,
} from "./u/update-user-email.js";
export { UPDATE_USER_EMAIL_REDACTION } from "./u/update-user-email.js";

export type {
  UpdateUserPhoneInput,
  UpdateUserPhoneOutput,
  IUpdateUserPhoneHandler,
} from "./u/update-user-phone.js";
export { UPDATE_USER_PHONE_REDACTION } from "./u/update-user-phone.js";

export type {
  CheckPhoneAvailabilityInput,
  CheckPhoneAvailabilityOutput,
  ICheckPhoneAvailabilityHandler,
} from "./r/check-phone-availability.js";

export type {
  GetUserByIdInput,
  GetUserByIdOutput,
  IGetUserByIdHandler,
} from "./r/get-user-by-id.js";

export type {
  GetActiveSessionsByUserIdInput,
  GetActiveSessionsByUserIdOutput,
  IGetActiveSessionsByUserIdHandler,
} from "./r/get-active-sessions-by-user-id.js";

export type {
  GetUserIdByIdentifierInput,
  GetUserIdByIdentifierOutput,
  IGetUserIdByIdentifierHandler,
} from "./r/get-user-id-by-identifier.js";

// --- User Deletion Repo Handlers ---
export type {
  UpdateUserStatusInput,
  UpdateUserStatusOutput,
  IUpdateUserStatusHandler,
} from "./u/update-user-status.js";
export { UPDATE_USER_STATUS_REDACTION } from "./u/update-user-status.js";

export type {
  GetDeletedUsersToPurgeInput,
  GetDeletedUsersToPurgeOutput,
  IGetDeletedUsersToPurgeHandler,
} from "./r/get-deleted-users-to-purge.js";
export { GET_DELETED_USERS_TO_PURGE_REDACTION } from "./r/get-deleted-users-to-purge.js";

export type {
  AnonymizeUserInput,
  AnonymizeUserOutput,
  IAnonymizeUserHandler,
} from "./u/anonymize-user.js";
export { ANONYMIZE_USER_REDACTION } from "./u/anonymize-user.js";

export type {
  CheckSoleOwnerOrgsInput,
  CheckSoleOwnerOrgsOutput,
  ICheckSoleOwnerOrgsHandler,
} from "./r/check-sole-owner-orgs.js";
export { CHECK_SOLE_OWNER_ORGS_REDACTION } from "./r/check-sole-owner-orgs.js";

export type {
  DeleteAllUserSessionsInput,
  DeleteAllUserSessionsOutput,
  IDeleteAllUserSessionsHandler,
} from "./d/delete-all-user-sessions.js";
export { DELETE_ALL_USER_SESSIONS_REDACTION } from "./d/delete-all-user-sessions.js";

export type {
  UpdateOrgLogoInput,
  UpdateOrgLogoOutput,
  IUpdateOrgLogoHandler,
} from "./u/update-org-logo.js";
export { UPDATE_ORG_LOGO_REDACTION } from "./u/update-org-logo.js";

// --- Delete (D) ---
export type {
  DeleteOrgContactRecordInput,
  DeleteOrgContactRecordOutput,
  IDeleteOrgContactRecordHandler,
} from "./d/delete-org-contact-record.js";

export type {
  PurgeExpiredSessionsInput,
  PurgeExpiredSessionsOutput,
  IPurgeExpiredSessionsHandler,
} from "./d/purge-expired-sessions.js";

export type {
  PurgeSignInEventsInput,
  PurgeSignInEventsOutput,
  IPurgeSignInEventsHandler,
} from "./d/purge-sign-in-events.js";

export type {
  PurgeExpiredInvitationsInput,
  PurgeExpiredInvitationsOutput,
  IPurgeExpiredInvitationsHandler,
} from "./d/purge-expired-invitations.js";

export type {
  PurgeExpiredEmulationConsentsInput,
  PurgeExpiredEmulationConsentsOutput,
  IPurgeExpiredEmulationConsentsHandler,
} from "./d/purge-expired-emulation-consents.js";

// --- Read (R) ---
export type { PingDbInput, PingDbOutput, IPingDbHandler } from "./r/ping-db.js";

// ---------------------------------------------------------------------------
// Bundle types — one per aggregate, used by app-layer factory functions
// ---------------------------------------------------------------------------

export interface SignInEventRepoHandlers {
  create: ICreateSignInEventHandler;
  findByUserId: IFindSignInEventsByUserIdHandler;
  countByUserId: ICountSignInEventsByUserIdHandler;
  getLatestEventDate: IGetLatestSignInEventDateHandler;
  updateWhoIsId: IUpdateSignInEventWhoIsIdHandler;
}

export interface EmulationConsentRepoHandlers {
  create: ICreateEmulationConsentRecordHandler;
  findById: IFindEmulationConsentByIdHandler;
  findActiveByUserId: IFindActiveConsentsByUserIdHandler;
  findActiveByUserIdAndOrg: IFindActiveConsentByUserIdAndOrgHandler;
  revoke: IRevokeEmulationConsentRecordHandler;
}

export interface OrgContactRepoHandlers {
  create: ICreateOrgContactRecordHandler;
  findById: IFindOrgContactByIdHandler;
  findByOrgId: IFindOrgContactsByOrgIdHandler;
  update: IUpdateOrgContactRecordHandler;
  delete: IDeleteOrgContactRecordHandler;
}
