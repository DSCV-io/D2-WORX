// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { ILogger } from "@dcsv-io/d2-logging";

/**
 * Tunables for {@link WorkloadLeafClient}. All optional — the defaults mirror the
 * .NET `AuthOutboundResilienceDefaults` (5 consecutive failures → 30 s open) and a
 * 5-minute refresh-ahead margin. A test overrides `now` for deterministic timing.
 */
export interface WorkloadLeafClientOptions {
  /** Monotonic-ish clock in epoch milliseconds. Defaults to `Date.now`. */
  readonly now?: () => number;
  /**
   * Refresh-ahead margin in milliseconds: a cached leaf within this margin of its
   * not-after triggers a proactive reissue while still being served if reissue
   * fails. Defaults to 5 minutes.
   */
  readonly refreshMarginMs?: number;
  /** Consecutive transient failures before the reissue circuit opens. Defaults to 5. */
  readonly circuitFailureThreshold?: number;
  /** Circuit open→half-open cooldown in milliseconds. Defaults to 30 s. */
  readonly circuitCooldownMs?: number;
  /** Optional structured logger. Never receives key material — sanitized fields only. */
  readonly logger?: ILogger;
}
