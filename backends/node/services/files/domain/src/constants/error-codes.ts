/**
 * Domain-specific error codes for the Files service.
 *
 * Convention: `FILES_{SPECIFIC_CODE}` — prefixed to avoid clashes with
 * generic error codes from @d2/result (NOT_FOUND, UNAUTHORIZED, etc.).
 *
 * These are returned as `errorCode` in D2Result.Fail() responses.
 */
export const FILES_ERROR_CODES = {
  FILE_TOO_LARGE: "FILES_FILE_TOO_LARGE",
  INVALID_CONTENT_TYPE: "FILES_INVALID_CONTENT_TYPE",
  CONTENT_TYPE_NOT_ALLOWED: "FILES_CONTENT_TYPE_NOT_ALLOWED",
  PROCESSING_FAILED: "FILES_PROCESSING_FAILED",
  INVALID_CONTEXT_KEY: "FILES_INVALID_CONTEXT_KEY",
  ACCESS_DENIED: "FILES_ACCESS_DENIED",
  VARIANT_NOT_FOUND: "FILES_VARIANT_NOT_FOUND",
  FILE_REJECTED: "FILES_FILE_REJECTED",
  INVALID_STATUS_TRANSITION: "FILES_INVALID_STATUS_TRANSITION",
} as const;

export type FilesErrorCode = (typeof FILES_ERROR_CODES)[keyof typeof FILES_ERROR_CODES];
