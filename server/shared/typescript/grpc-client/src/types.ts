// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Internal token snapshot returned by KeyCustodian. Cached as a single
 * slot per process; refreshed-on-401 via Singleflight dedup.
 */
export interface InternalTokenSnapshot {
  /** The Bearer JWT minted by KeyCustodian. */
  readonly accessToken: string;
  /** Unix epoch milliseconds when the token expires. */
  readonly expiresAtMs: number;
  /** Audience claim value (informational; Edge does the real validation). */
  readonly audience: string;
}
