// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Standardized error codes surfaced as `D2Result.errorCode`. Mirrors
 * .NET `D2.Shared.Result.ErrorCodes` 1:1 — same string values so the
 * cross-language wire stays interchangeable.
 */
export const ErrorCodes = {
  NOT_FOUND: "NOT_FOUND",
  FORBIDDEN: "FORBIDDEN",
  UNAUTHORIZED: "UNAUTHORIZED",
  VALIDATION_FAILED: "VALIDATION_FAILED",
  CONFLICT: "CONFLICT",
  UNHANDLED_EXCEPTION: "UNHANDLED_EXCEPTION",
  COULD_NOT_BE_SERIALIZED: "COULD_NOT_BE_SERIALIZED",
  COULD_NOT_BE_DESERIALIZED: "COULD_NOT_BE_DESERIALIZED",
  SERVICE_UNAVAILABLE: "SERVICE_UNAVAILABLE",
  SOME_FOUND: "SOME_FOUND",
  PARTIAL_SUCCESS: "PARTIAL_SUCCESS",
  RATE_LIMITED: "RATE_LIMITED",
  IDEMPOTENCY_IN_FLIGHT: "IDEMPOTENCY_IN_FLIGHT",
  PAYLOAD_TOO_LARGE: "PAYLOAD_TOO_LARGE",
  CANCELED: "CANCELED",
} as const;

export type ErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes];
