// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { RETRY_DEFAULTS } from "../../src/retry/retry-defaults.js";

describe("RETRY_DEFAULTS", () => {
  it.each([
    ["maxAttempts", "maxAttempts", 3],
    ["baseDelayMs", "baseDelayMs", 100],
    ["backoffMultiplier", "backoffMultiplier", 2],
    ["maxDelayMs", "maxDelayMs", 5_000],
    ["jitter", "jitter", 0.2],
  ])("default %s = %s", (_label, key, value) => {
    expect((RETRY_DEFAULTS as unknown as Record<string, unknown>)[key]).toBe(
      value,
    );
  });
});
