import * as m from "$lib/paraglide/messages.js";

/**
 * Maps D2Result error codes to localized messages.
 *
 * Generic error codes come from @d2/result (NOT_FOUND, UNAUTHORIZED, etc.).
 * Domain-specific codes are prefixed with the service name (AUTH_, COMMS_).
 */

type MessageFn = () => string;

const COMMON_ERROR_MAP: Record<string, MessageFn> = {
  NOT_FOUND: () => m.common_errors_NOT_FOUND(),
  UNAUTHORIZED: () => m.common_errors_UNAUTHORIZED(),
  FORBIDDEN: () => m.common_errors_FORBIDDEN(),
  CONFLICT: () => m.common_errors_CONFLICT(),
  TOO_MANY_REQUESTS: () => m.common_errors_TOO_MANY_REQUESTS(),
};

const DOMAIN_ERROR_MAP: Record<string, MessageFn> = {
  AUTH_SIGN_IN_THROTTLED: () => m.auth_errors_SIGN_IN_THROTTLED(),
  AUTH_EMAIL_ALREADY_TAKEN: () => m.auth_errors_EMAIL_ALREADY_TAKEN(),
  AUTH_EMULATION_CONSENT_ALREADY_EXISTS: () => m.auth_errors_EMULATION_CONSENT_ALREADY_EXISTS(),
  AUTH_EMULATION_CONSENT_ALREADY_REVOKED: () => m.auth_errors_EMULATION_CONSENT_ALREADY_REVOKED(),
  AUTH_EMULATION_ORG_TYPE_NOT_ALLOWED: () => m.auth_errors_EMULATION_ORG_TYPE_NOT_ALLOWED(),
  AUTH_ORG_CONTACT_ORG_MISMATCH: () => m.auth_errors_ORG_CONTACT_ORG_MISMATCH(),
};

const ERROR_CODE_MAP = { ...COMMON_ERROR_MAP, ...DOMAIN_ERROR_MAP };

/**
 * Resolves a D2Result error code to a localized user-facing message.
 * Falls back to the provided fallback string or a generic error message.
 */
export function getErrorMessage(errorCode: string | undefined, fallback?: string): string {
  if (errorCode && errorCode in ERROR_CODE_MAP) {
    return ERROR_CODE_MAP[errorCode]();
  }
  return fallback ?? m.common_errors_unknown();
}
