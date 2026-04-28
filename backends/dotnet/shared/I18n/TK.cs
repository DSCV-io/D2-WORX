// -----------------------------------------------------------------------
// <copyright file="TK.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.I18n;

/// <summary>
/// Translation Key constants organized by domain and category.
/// Key values match the JSON keys in <c>contracts/messages/*.json</c>.
/// </summary>
public static class TK
{
    /// <summary>
    /// Common (cross-cutting) translation keys.
    /// </summary>
    public static class Common
    {
        /// <summary>
        /// Error message keys shared across all services.
        /// </summary>
        public static class Errors
        {
            /// <summary>The request was malformed or invalid.</summary>
            public const string BAD_REQUEST = "common_errors_BAD_REQUEST";

            /// <summary>The requested resource was not found.</summary>
            public const string NOT_FOUND = "common_errors_NOT_FOUND";

            /// <summary>The caller is not authorized to perform this action.</summary>
            public const string UNAUTHORIZED = "common_errors_UNAUTHORIZED";

            /// <summary>Access to the resource is forbidden.</summary>
            public const string FORBIDDEN = "common_errors_FORBIDDEN";

            /// <summary>A conflict occurred with the current state.</summary>
            public const string CONFLICT = "common_errors_CONFLICT";

            /// <summary>The caller has exceeded the request rate limit.</summary>
            public const string TOO_MANY_REQUESTS = "common_errors_TOO_MANY_REQUESTS";

            /// <summary>The request could not be completed.</summary>
            public const string REQUEST_FAILED = "common_errors_REQUEST_FAILED";

            /// <summary>Input validation failed.</summary>
            public const string VALIDATION_FAILED = "common_errors_VALIDATION_FAILED";

            /// <summary>The service is temporarily unavailable.</summary>
            public const string SERVICE_UNAVAILABLE = "common_errors_SERVICE_UNAVAILABLE";

            /// <summary>The request payload is too large.</summary>
            public const string PAYLOAD_TOO_LARGE = "common_errors_PAYLOAD_TOO_LARGE";

            /// <summary>The operation was cancelled.</summary>
            public const string CANCELLED = "common_errors_CANCELLED";

            /// <summary>Some items were found but not all requested items.</summary>
            public const string SOME_FOUND = "common_errors_SOME_FOUND";

            /// <summary>An unknown or unhandled error occurred.</summary>
            public const string UNKNOWN = "common_errors_unknown";

            /// <summary>Value could not be serialized.</summary>
            public const string COULD_NOT_BE_SERIALIZED = "common_errors_COULD_NOT_BE_SERIALIZED";

            /// <summary>Value could not be deserialized.</summary>
            public const string COULD_NOT_BE_DESERIALIZED = "common_errors_COULD_NOT_BE_DESERIALIZED";
        }

        /// <summary>
        /// Cross-cutting validator messages used by reusable .NET validators in
        /// <c>D2.Shared.Handler.Validators</c>. Mirrored in Node.js as
        /// <c>TK.common.validation.*</c>.
        /// </summary>
        public static class Validation
        {
            /// <summary>The value must be a valid IPv4 or IPv6 address.</summary>
            public const string IP_INVALID = "common_validation_IP_INVALID";

            /// <summary>The value must be a 64-character hex string.</summary>
            public const string HASH_ID_INVALID = "common_validation_HASH_ID_INVALID";

            /// <summary>The value must be a valid, non-empty GUID.</summary>
            public const string ID_INVALID = "common_validation_ID_INVALID";

            /// <summary>The value must be a valid email address.</summary>
            public const string EMAIL_INVALID = "common_validation_EMAIL_INVALID";

            /// <summary>The value must be 7-15 digits (E.164).</summary>
            public const string PHONE_INVALID = "common_validation_PHONE_INVALID";

            /// <summary>The value is not an allowed context key.</summary>
            public const string CONTEXT_KEY_NOT_ALLOWED = "common_validation_CONTEXT_KEY_NOT_ALLOWED";

            /// <summary>The list must contain at least one item.</summary>
            public const string NON_EMPTY_LIST = "common_validation_NON_EMPTY_LIST";
        }
    }

    /// <summary>
    /// Geo service translation keys.
    /// </summary>
    public static class Geo
    {
        /// <summary>
        /// Input validation error messages for geo-related operations.
        /// </summary>
        public static class Validation
        {
            /// <summary>IP address is required.</summary>
            public const string IP_REQUIRED = "geo_validation_ip_required";

            /// <summary>IP address format is invalid.</summary>
            public const string IP_INVALID = "geo_validation_ip_invalid";

            /// <summary>Month value is out of valid range (1-12).</summary>
            public const string MONTH_RANGE = "geo_validation_month_range";

            /// <summary>Year value is out of valid range.</summary>
            public const string YEAR_RANGE = "geo_validation_year_range";

            /// <summary>Latitude value is out of valid range (-90 to 90).</summary>
            public const string LATITUDE_RANGE = "geo_validation_latitude_range";

            /// <summary>Longitude value is out of valid range (-180 to 180).</summary>
            public const string LONGITUDE_RANGE = "geo_validation_longitude_range";

            /// <summary>Address line 1 is required.</summary>
            public const string ADDRESS_LINE1_REQUIRED = "geo_validation_address_line1_required";

            /// <summary>Address line 2 is required.</summary>
            public const string ADDRESS_LINE2_REQUIRED = "geo_validation_address_line2_required";

            /// <summary>Context key is required.</summary>
            public const string CONTEXT_KEY_REQUIRED = "geo_validation_context_key_required";

            /// <summary>Related entity ID is required.</summary>
            public const string RELATED_ENTITY_ID_REQUIRED = "geo_validation_related_entity_id_required";

            /// <summary>First name is required.</summary>
            public const string FIRST_NAME_REQUIRED = "geo_validation_first_name_required";

            /// <summary>Company name is required.</summary>
            public const string COMPANY_NAME_REQUIRED = "geo_validation_company_name_required";

            /// <summary>Email address is required.</summary>
            public const string EMAIL_REQUIRED = "geo_validation_email_required";

            /// <summary>Phone number is required.</summary>
            public const string PHONE_REQUIRED = "geo_validation_phone_required";

            /// <summary>The ID must be a valid, non-empty GUID.</summary>
            public const string ID_INVALID = "geo_validation_id_invalid";

            /// <summary>Duplicate external keys are not allowed.</summary>
            public const string DUPLICATE_EXT_KEYS = "geo_validation_duplicate_ext_keys";
        }

        /// <summary>
        /// Geo service error message keys.
        /// </summary>
        public static class Errors
        {
            /// <summary>Corrupted data on disk.</summary>
            public const string CORRUPTED_DATA_ON_DISK = "geo_errors_corrupted_data_on_disk";

            /// <summary>Unable to read from disk.</summary>
            public const string DISK_READ_FAILED = "geo_errors_disk_read_failed";

            /// <summary>Unable to write to disk.</summary>
            public const string DISK_WRITE_FAILED = "geo_errors_disk_write_failed";
        }
    }

    /// <summary>
    /// Auth service translation keys.
    /// </summary>
    public static class Auth
    {
        /// <summary>
        /// Auth-specific error messages.
        /// </summary>
        public static class Errors
        {
            /// <summary>This email is already taken.</summary>
            public const string EMAIL_ALREADY_TAKEN = "auth_errors_EMAIL_ALREADY_TAKEN";

            /// <summary>Email query parameter is required.</summary>
            public const string EMAIL_QUERY_REQUIRED = "auth_errors_EMAIL_QUERY_REQUIRED";

            /// <summary>Email is required.</summary>
            public const string EMAIL_REQUIRED = "auth_errors_EMAIL_REQUIRED";

            /// <summary>An active emulation consent already exists for this organization.</summary>
            public const string EMULATION_CONSENT_ALREADY_EXISTS = "auth_errors_EMULATION_CONSENT_ALREADY_EXISTS";

            /// <summary>This emulation consent has already been revoked.</summary>
            public const string EMULATION_CONSENT_ALREADY_REVOKED = "auth_errors_EMULATION_CONSENT_ALREADY_REVOKED";

            /// <summary>Emulation is not allowed for this organization type.</summary>
            public const string EMULATION_ORG_TYPE_NOT_ALLOWED = "auth_errors_EMULATION_ORG_TYPE_NOT_ALLOWED";

            /// <summary>Invalid role specified.</summary>
            public const string INVALID_ROLE = "auth_errors_INVALID_ROLE";

            /// <summary>Failed to create the invitation.</summary>
            public const string INVITATION_CREATION_FAILED = "auth_errors_INVITATION_CREATION_FAILED";

            /// <summary>Inviter role cannot invite the specified role.</summary>
            public const string INVITATION_ROLE_HIERARCHY = "auth_errors_INVITATION_ROLE_HIERARCHY";

            /// <summary>The contact does not belong to this organization.</summary>
            public const string ORG_CONTACT_ORG_MISMATCH = "auth_errors_ORG_CONTACT_ORG_MISMATCH";

            /// <summary>Role is required.</summary>
            public const string ROLE_REQUIRED = "auth_errors_ROLE_REQUIRED";

            /// <summary>Too many sign-in attempts.</summary>
            public const string SIGN_IN_THROTTLED = "auth_errors_SIGN_IN_THROTTLED";

            /// <summary>The account has been deleted.</summary>
            public const string ACCOUNT_DELETED = "auth_errors_ACCOUNT_DELETED";

            /// <summary>The user is the sole owner of one or more orgs.</summary>
            public const string SOLE_OWNER_OF_ORGS = "auth_errors_SOLE_OWNER_OF_ORGS";

            /// <summary>Session token is required.</summary>
            public const string SESSION_TOKEN_REQUIRED = "auth_errors_SESSION_TOKEN_REQUIRED";

            /// <summary>Password is required to confirm this change.</summary>
            public const string PASSWORD_REQUIRED_FOR_CHANGE = "auth_errors_PASSWORD_REQUIRED_FOR_CHANGE";

            /// <summary>The current password is incorrect.</summary>
            public const string INCORRECT_PASSWORD = "auth_errors_INCORRECT_PASSWORD";

            /// <summary>Failed to revoke the session(s).</summary>
            public const string SESSION_REVOKE_FAILED = "auth_errors_SESSION_REVOKE_FAILED";

            /// <summary>Both current password and new password are required.</summary>
            public const string CHANGE_PASSWORD_REQUIRED_FIELDS = "auth_errors_CHANGE_PASSWORD_REQUIRED_FIELDS";

            /// <summary>Failed to change the password.</summary>
            public const string CHANGE_PASSWORD_FAILED = "auth_errors_CHANGE_PASSWORD_FAILED";

            /// <summary>Password cannot be only numbers.</summary>
            public const string PASSWORD_NUMERIC_ONLY = "auth_errors_PASSWORD_NUMERIC_ONLY";

            /// <summary>Password cannot be only numbers + date separators.</summary>
            public const string PASSWORD_DATE_LIKE = "auth_errors_PASSWORD_DATE_LIKE";

            /// <summary>Password is too common (matched the blocklist).</summary>
            public const string PASSWORD_TOO_COMMON = "auth_errors_PASSWORD_TOO_COMMON";

            /// <summary>Password has appeared in a known breach dataset (HIBP).</summary>
            public const string PASSWORD_BREACHED = "auth_errors_PASSWORD_BREACHED";
        }
    }

    /// <summary>
    /// Middleware translation keys.
    /// </summary>
    public static class Middleware
    {
        /// <summary>
        /// Middleware-specific error messages.
        /// </summary>
        public static class Errors
        {
            /// <summary>User has insufficient role for this operation.</summary>
            public const string INSUFFICIENT_ROLE = "middleware_errors_INSUFFICIENT_ROLE";

            /// <summary>No active organization on session.</summary>
            public const string NO_ACTIVE_ORGANIZATION = "middleware_errors_NO_ACTIVE_ORGANIZATION";

            /// <summary>Organization type is not authorized for this operation.</summary>
            public const string ORG_TYPE_NOT_AUTHORIZED = "middleware_errors_ORG_TYPE_NOT_AUTHORIZED";
        }
    }

    /// <summary>
    /// Files service translation keys.
    /// </summary>
    public static class Files
    {
        /// <summary>
        /// Files-specific error messages.
        /// </summary>
        public static class Errors
        {
            /// <summary>Missing related entity ID for the upload context.</summary>
            public const string MISSING_RELATED_ENTITY = "files_errors_MISSING_RELATED_ENTITY";

            /// <summary>The supplied context key is not a valid upload target.</summary>
            public const string INVALID_UPLOAD_TARGET = "files_errors_INVALID_UPLOAD_TARGET";

            /// <summary>The request body could not be parsed as JSON.</summary>
            public const string INVALID_JSON_BODY = "files_errors_INVALID_JSON_BODY";

            /// <summary>Required query parameters are missing on the list endpoint.</summary>
            public const string LIST_QUERY_PARAMS_REQUIRED = "files_errors_LIST_QUERY_PARAMS_REQUIRED";

            /// <summary>Storage delete completed for some objects but not all.</summary>
            public const string PARTIAL_STORAGE_DELETE = "files_errors_PARTIAL_STORAGE_DELETE";
        }
    }

    /// <summary>
    /// Comms service translation keys.
    /// </summary>
    public static class Comms
    {
        /// <summary>
        /// Comms-specific error messages.
        /// </summary>
        public static class Errors
        {
            /// <summary>Delivery failed on some channels, retry scheduled.</summary>
            public const string DELIVERY_RETRY_SCHEDULED = "comms_errors_DELIVERY_RETRY_SCHEDULED";

            /// <summary>No deliverable channels available for the recipient.</summary>
            public const string NO_DELIVERABLE_CHANNELS = "comms_errors_NO_DELIVERABLE_CHANNELS";

            /// <summary>Unknown provider error.</summary>
            public const string PROVIDER_UNKNOWN = "comms_errors_PROVIDER_UNKNOWN";
        }
    }
}
