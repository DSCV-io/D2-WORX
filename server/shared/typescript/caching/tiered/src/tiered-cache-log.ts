// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { ILogger } from "@d2/logging";

// Twin of .NET TieredCacheLog EventId meanings: Warning on L1 invalidation
// failure and on L1 write failure after L2 success. Structured redis-style
// message strings (not LoggerMessage template parity). Bindings carry
// errorCode strings only - no Exception. No OTel meters in this package.

const L1_INVALIDATION_FAILED_MESSAGE =
  "Tiered cache L1 invalidation handler failed.";

const L1_WRITE_FAILED_AFTER_L2_SUCCESS_MESSAGE =
  "Tiered cache L1 write failed after L2 success.";

/**
 * Sentinel errorCode when a failed L1 result has no code (§21.11 closed-set).
 */
export const TIERED_ERROR_CODE_UNKNOWN = "unknown";

/**
 * Closed-set write-path operation names for tiered L1-fail warn bindings
 * (§21.11). Single SoT for every emit site and pin test.
 */
export const TieredCacheOp = {
  SET: "set",
  SET_MANY: "setMany",
  REMOVE: "remove",
  REMOVE_MANY: "removeMany",
} as const;

/** Closed-set type for {@link TieredCacheOp} values. */
export type TieredCacheOpName =
  (typeof TieredCacheOp)[keyof typeof TieredCacheOp];

/**
 * Logs Warning when the backplane invalidation handler's L1 remove fails.
 */
export function logL1InvalidationFailed(
  logger: ILogger,
  key: string,
  errorCode: string,
): void {
  logger.warn(L1_INVALIDATION_FAILED_MESSAGE, { key, errorCode });
}

/**
 * Logs Warning when an L1 write/remove fails after L2 already succeeded.
 *
 * @param operation - One of {@link TieredCacheOp} values.
 * @param keyOrCount - Single key, or `"N entries"` / `"N keys"` bulk form.
 */
export function logL1WriteFailedAfterL2Success(
  logger: ILogger,
  operation: TieredCacheOpName,
  keyOrCount: string,
  errorCode: string,
): void {
  logger.warn(L1_WRITE_FAILED_AFTER_L2_SUCCESS_MESSAGE, {
    operation,
    keyOrCount,
    errorCode,
  });
}
