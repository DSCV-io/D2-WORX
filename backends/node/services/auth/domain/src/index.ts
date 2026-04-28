// @d2/auth-domain — Pure domain types for the Auth service.

// --- Constants ---
export {
  JWT_CLAIM_TYPES,
  SESSION_FIELDS,
  AUTH_POLICIES,
  REQUEST_HEADERS,
  PASSWORD_POLICY,
  SIGN_IN_THROTTLE,
  USER_DELETION,
  GEO_CONTEXT_KEYS,
  AUTH_FILE_CONTEXT_KEYS,
} from "./constants/auth-constants.js";
export { AUTH_MESSAGING } from "./constants/messaging.js";
export { AUTH_ERROR_CODES } from "./constants/error-codes.js";
export type { AuthErrorCode } from "./constants/error-codes.js";
export { OTP_RATE_LIMIT, OTP_EXPIRY, OTP_VERIFY } from "./constants/otp-constants.js";

// --- Enums ---
export { ORG_TYPES, isValidOrgType } from "./enums/org-type.js";
export type { OrgType } from "./enums/org-type.js";
export { ROLES, ROLE_HIERARCHY, isValidRole, rolesAtOrAbove } from "./enums/role.js";
export type { Role } from "./enums/role.js";
export {
  INVITATION_STATUSES,
  INVITATION_TRANSITIONS,
  isValidInvitationStatus,
} from "./enums/invitation-status.js";
export type { InvitationStatus } from "./enums/invitation-status.js";
export { USER_STATUS } from "./enums/user-status.js";
export type { UserStatus } from "./enums/user-status.js";

// --- Exceptions ---
export { AuthDomainError } from "./exceptions/auth-domain-error.js";
export { AuthValidationError } from "./exceptions/auth-validation-error.js";

// --- Entities ---
export type { Account } from "./entities/account.js";
export type { Session } from "./entities/session.js";
export { createUser, updateUser } from "./entities/user.js";
export type { User, CreateUserInput, UpdateUserInput } from "./entities/user.js";
export { createOrganization, updateOrganization } from "./entities/organization.js";
export type {
  Organization,
  CreateOrganizationInput,
  UpdateOrganizationInput,
} from "./entities/organization.js";
export { createMember } from "./entities/member.js";
export type { Member, CreateMemberInput } from "./entities/member.js";
export { createInvitation } from "./entities/invitation.js";
export type { Invitation, CreateInvitationInput } from "./entities/invitation.js";
export { createSignInEvent } from "./entities/sign-in-event.js";
export type { SignInEvent, CreateSignInEventInput } from "./entities/sign-in-event.js";
export {
  createEmulationConsent,
  revokeEmulationConsent,
  isConsentActive,
} from "./entities/emulation-consent.js";
export type {
  EmulationConsent,
  CreateEmulationConsentInput,
} from "./entities/emulation-consent.js";
export { createOrgContact, updateOrgContact } from "./entities/org-contact.js";
export type {
  OrgContact,
  CreateOrgContactInput,
  UpdateOrgContactInput,
} from "./entities/org-contact.js";

// --- Value Objects ---
export type { SessionContext } from "./value-objects/session-context.js";

// --- Data ---
export { COMMON_PASSWORDS } from "./data/common-passwords.js";
export { USERNAME_ADJECTIVES } from "./data/username-adjectives.js";
export { USERNAME_NOUNS } from "./data/username-nouns.js";

// --- Business Rules ---
export { resolveSessionContext, canEmulate } from "./rules/emulation.js";
export { isLastOwner, isMemberOfOrg } from "./rules/membership.js";
export { canCreateOrgType } from "./rules/org-creation.js";
export { transitionInvitationStatus, isInvitationExpired } from "./rules/invitation.js";
export { validatePassword } from "./rules/password-rules.js";
export type { PasswordValidationResult } from "./rules/password-rules.js";
export { computeSignInDelay } from "./rules/sign-in-throttle-rules.js";
export { generateUsername, USERNAME_RULES } from "./rules/username-rules.js";
export {
  generateOtpCode,
  hashOtpCode,
  pendingChangeIdentifier,
  encodePendingValue,
  decodePendingValue,
} from "./rules/otp-rules.js";
export type { AccountChangeType, PendingChangeValue } from "./rules/otp-rules.js";
