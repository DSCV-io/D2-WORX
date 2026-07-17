// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { RetryOptions } from "./retry-options.js";

/**
 * Default retry options. Mirrors .NET `RetryDefaults`.
 */
export const RETRY_DEFAULTS: RetryOptions<unknown> = {
  maxAttempts: 3,
  baseDelayMs: 100,
  backoffMultiplier: 2,
  maxDelayMs: 5_000,
  jitter: 0.2,
};
