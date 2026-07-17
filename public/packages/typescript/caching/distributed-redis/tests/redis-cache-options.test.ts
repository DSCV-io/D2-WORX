// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { createRedisCacheOptions, REDIS_CACHE_DEFAULTS } from "../src/index.js";

describe("RedisCacheOptions", () => {
  it("createRedisCacheOptions_noArgs_usesDefaults", () => {
    const opts = createRedisCacheOptions();
    expect(opts).toEqual(REDIS_CACHE_DEFAULTS);
  });

  it("createRedisCacheOptions_partial_merges", () => {
    const opts = createRedisCacheOptions({
      keyPrefix: "app:",
      defaultExpirationMs: 1000,
    });
    expect(opts.keyPrefix).toBe("app:");
    expect(opts.defaultExpirationMs).toBe(1000);
    expect(opts.invalidationChannel).toBe("d2:cache:invalidations");
    expect(opts.commandTimeoutMs).toBe(2_000);
  });

  it("redisCacheDefaults_invalidationChannel_isD2CacheInvalidations", () => {
    expect(REDIS_CACHE_DEFAULTS.invalidationChannel).toBe(
      "d2:cache:invalidations",
    );
  });

  it("redisCacheDefaults_defaultExpirationMs_isOneHour", () => {
    expect(REDIS_CACHE_DEFAULTS.defaultExpirationMs).toBe(3_600_000);
  });
});
