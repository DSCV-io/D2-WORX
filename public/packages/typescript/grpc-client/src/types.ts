// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Internal token snapshot returned by the OAuth token endpoint
 * (`grant_type=client_credentials`). Cached as a single slot per process;
 * refreshed-on-401 via Singleflight dedup.
 */
export interface InternalTokenSnapshot {
  /** The Bearer JWT minted by the OAuth token endpoint. */
  readonly accessToken: string;
  /** Unix epoch milliseconds when the token expires. */
  readonly expiresAtMs: number;
  /** Audience claim value (informational; Edge does the real validation). */
  readonly audience: string;
}
