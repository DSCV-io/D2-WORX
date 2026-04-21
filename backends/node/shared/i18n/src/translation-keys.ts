/**
 * Translation key constants. Mirrors D2.Shared.I18n.TK in .NET.
 * Usage: TK.common.errors.NOT_FOUND
 */
export const TK = {
  common: {
    errors: {
      BAD_REQUEST: "common_errors_BAD_REQUEST",
      CANCELLED: "common_errors_CANCELLED",
      CONFLICT: "common_errors_CONFLICT",
      COULD_NOT_BE_DESERIALIZED: "common_errors_COULD_NOT_BE_DESERIALIZED",
      COULD_NOT_BE_SERIALIZED: "common_errors_COULD_NOT_BE_SERIALIZED",
      FORBIDDEN: "common_errors_FORBIDDEN",
      NOT_FOUND: "common_errors_NOT_FOUND",
      PAYLOAD_TOO_LARGE: "common_errors_PAYLOAD_TOO_LARGE",
      REQUEST_FAILED: "common_errors_REQUEST_FAILED",
      SERVICE_UNAVAILABLE: "common_errors_SERVICE_UNAVAILABLE",
      SOME_FOUND: "common_errors_SOME_FOUND",
      TOO_MANY_REQUESTS: "common_errors_TOO_MANY_REQUESTS",
      UNAUTHORIZED: "common_errors_UNAUTHORIZED",
      UNKNOWN: "common_errors_unknown",
      VALIDATION_FAILED: "common_errors_VALIDATION_FAILED",
    },
    validation: {
      CONTEXT_KEY_NOT_ALLOWED: "common_validation_CONTEXT_KEY_NOT_ALLOWED",
      EMAIL_INVALID: "common_validation_EMAIL_INVALID",
      HASH_ID_INVALID: "common_validation_HASH_ID_INVALID",
      ID_INVALID: "common_validation_ID_INVALID",
      IP_INVALID: "common_validation_IP_INVALID",
      NON_EMPTY_LIST: "common_validation_NON_EMPTY_LIST",
      PHONE_INVALID: "common_validation_PHONE_INVALID",
    },
  },
  geo: {
    validation: {
      addressLine1Required: "geo_validation_address_line1_required",
      addressLine2Required: "geo_validation_address_line2_required",
      companyNameRequired: "geo_validation_company_name_required",
      contextKeyRequired: "geo_validation_context_key_required",
      duplicateExtKeys: "geo_validation_duplicate_ext_keys",
      emailRequired: "geo_validation_email_required",
      firstNameRequired: "geo_validation_first_name_required",
      idInvalid: "geo_validation_id_invalid",
      ipInvalid: "geo_validation_ip_invalid",
      ipRequired: "geo_validation_ip_required",
      latitudeRange: "geo_validation_latitude_range",
      longitudeRange: "geo_validation_longitude_range",
      monthRange: "geo_validation_month_range",
      phoneRequired: "geo_validation_phone_required",
      relatedEntityIdRequired: "geo_validation_related_entity_id_required",
      yearRange: "geo_validation_year_range",
    },
    errors: {
      CORRUPTED_DATA_ON_DISK: "geo_errors_corrupted_data_on_disk",
      DISK_READ_FAILED: "geo_errors_disk_read_failed",
      DISK_WRITE_FAILED: "geo_errors_disk_write_failed",
    },
  },
  auth: {
    errors: {
      ACCOUNT_DELETED: "auth_errors_ACCOUNT_DELETED",
      EMAIL_ALREADY_TAKEN: "auth_errors_EMAIL_ALREADY_TAKEN",
      EMAIL_QUERY_REQUIRED: "auth_errors_EMAIL_QUERY_REQUIRED",
      EMAIL_REQUIRED: "auth_errors_EMAIL_REQUIRED",
      EMULATION_CONSENT_ALREADY_EXISTS: "auth_errors_EMULATION_CONSENT_ALREADY_EXISTS",
      EMULATION_CONSENT_ALREADY_REVOKED: "auth_errors_EMULATION_CONSENT_ALREADY_REVOKED",
      EMULATION_ORG_TYPE_NOT_ALLOWED: "auth_errors_EMULATION_ORG_TYPE_NOT_ALLOWED",
      INVALID_ROLE: "auth_errors_INVALID_ROLE",
      INVITATION_CREATION_FAILED: "auth_errors_INVITATION_CREATION_FAILED",
      INVITATION_ROLE_HIERARCHY: "auth_errors_INVITATION_ROLE_HIERARCHY",
      ORG_CONTACT_ORG_MISMATCH: "auth_errors_ORG_CONTACT_ORG_MISMATCH",
      ROLE_REQUIRED: "auth_errors_ROLE_REQUIRED",
      SIGN_IN_THROTTLED: "auth_errors_SIGN_IN_THROTTLED",
      SOLE_OWNER_OF_ORGS: "auth_errors_SOLE_OWNER_OF_ORGS",
      // Account-management routes (account-routes.ts)
      SESSION_TOKEN_REQUIRED: "auth_errors_SESSION_TOKEN_REQUIRED",
      PASSWORD_REQUIRED_FOR_CHANGE: "auth_errors_PASSWORD_REQUIRED_FOR_CHANGE",
      INCORRECT_PASSWORD: "auth_errors_INCORRECT_PASSWORD",
      SESSION_REVOKE_FAILED: "auth_errors_SESSION_REVOKE_FAILED",
      CHANGE_PASSWORD_REQUIRED_FIELDS: "auth_errors_CHANGE_PASSWORD_REQUIRED_FIELDS",
      CHANGE_PASSWORD_FAILED: "auth_errors_CHANGE_PASSWORD_FAILED",
      // Password-policy violations (returned from validatePassword + HIBP check)
      PASSWORD_NUMERIC_ONLY: "auth_errors_PASSWORD_NUMERIC_ONLY",
      PASSWORD_DATE_LIKE: "auth_errors_PASSWORD_DATE_LIKE",
      PASSWORD_TOO_COMMON: "auth_errors_PASSWORD_TOO_COMMON",
      PASSWORD_BREACHED: "auth_errors_PASSWORD_BREACHED",
    },
    email: {
      emailChanged: {
        subject: "auth_email_changed_subject",
        body: "auth_email_changed_body",
        plaintext: "auth_email_changed_plaintext",
      },
      phoneChanged: {
        subject: "auth_phone_changed_subject",
        body: "auth_phone_changed_body",
        plaintext: "auth_phone_changed_plaintext",
      },
      phoneRemoved: {
        subject: "auth_phone_removed_subject",
        body: "auth_phone_removed_body",
        plaintext: "auth_phone_removed_plaintext",
      },
      userDeletionScheduled: {
        subject: "auth_email_user_deletion_scheduled_subject",
        body: "auth_email_user_deletion_scheduled_body",
        plaintext: "auth_email_user_deletion_scheduled_plaintext",
      },
      userDeletionCancelled: {
        subject: "auth_email_user_deletion_cancelled_subject",
        body: "auth_email_user_deletion_cancelled_body",
        plaintext: "auth_email_user_deletion_cancelled_plaintext",
      },
      userDeletionComplete: {
        subject: "auth_email_user_deletion_complete_subject",
        body: "auth_email_user_deletion_complete_body",
        plaintext: "auth_email_user_deletion_complete_plaintext",
      },
    },
    otp: {
      email: {
        subject: "auth_otp_email_subject",
        body: "auth_otp_email_body",
        expiry: "auth_otp_email_expiry",
        disclaimer: "auth_otp_email_disclaimer",
        plaintext: "auth_otp_email_plaintext",
      },
      sms: {
        subject: "auth_otp_sms_subject",
        body: "auth_otp_sms_body",
        plaintext: "auth_otp_sms_plaintext",
      },
    },
  },
  middleware: {
    errors: {
      INSUFFICIENT_ROLE: "middleware_errors_INSUFFICIENT_ROLE",
      NO_ACTIVE_ORGANIZATION: "middleware_errors_NO_ACTIVE_ORGANIZATION",
      ORG_TYPE_NOT_AUTHORIZED: "middleware_errors_ORG_TYPE_NOT_AUTHORIZED",
    },
  },
  comms: {
    errors: {
      DELIVERY_RETRY_SCHEDULED: "comms_errors_DELIVERY_RETRY_SCHEDULED",
      NO_DELIVERABLE_CHANNELS: "comms_errors_NO_DELIVERABLE_CHANNELS",
      PROVIDER_UNKNOWN: "comms_errors_PROVIDER_UNKNOWN",
    },
  },
  files: {
    errors: {
      MISSING_RELATED_ENTITY: "files_errors_MISSING_RELATED_ENTITY",
      INVALID_UPLOAD_TARGET: "files_errors_INVALID_UPLOAD_TARGET",
      INVALID_JSON_BODY: "files_errors_INVALID_JSON_BODY",
      LIST_QUERY_PARAMS_REQUIRED: "files_errors_LIST_QUERY_PARAMS_REQUIRED",
      PARTIAL_STORAGE_DELETE: "files_errors_PARTIAL_STORAGE_DELETE",
    },
  },
} as const;
