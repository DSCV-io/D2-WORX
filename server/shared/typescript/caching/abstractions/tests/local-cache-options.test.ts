// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { createLocalCacheOptions, LOCAL_CACHE_DEFAULTS } from "../src/index.js";

describe("LOCAL_CACHE_DEFAULTS / createLocalCacheOptions", () => {
  it("localCacheDefaults_pinsMaxEntries100000_defaultExpirationMs3600000_keyPrefixEmptyString", () => {
    expect(LOCAL_CACHE_DEFAULTS.maxEntries).toBe(100_000);
    expect(LOCAL_CACHE_DEFAULTS.defaultExpirationMs).toBe(3_600_000);
    expect(LOCAL_CACHE_DEFAULTS.keyPrefix).toBe("");
  });

  it("createLocalCacheOptions_undefinedPartial_returnsDefaults", () => {
    const opts = createLocalCacheOptions();

    expect(opts).toEqual({
      maxEntries: 100_000,
      defaultExpirationMs: 3_600_000,
      keyPrefix: "",
    });
  });

  it("createLocalCacheOptions_emptyPartial_returnsDefaults", () => {
    const opts = createLocalCacheOptions({});

    expect(opts).toEqual({
      maxEntries: 100_000,
      defaultExpirationMs: 3_600_000,
      keyPrefix: "",
    });
  });

  it("createLocalCacheOptions_partialOverride_mergesOnlyProvidedFields", () => {
    const opts = createLocalCacheOptions({ maxEntries: 42 });

    expect(opts.maxEntries).toBe(42);
    expect(opts.defaultExpirationMs).toBe(
      LOCAL_CACHE_DEFAULTS.defaultExpirationMs,
    );
    expect(opts.keyPrefix).toBe(LOCAL_CACHE_DEFAULTS.keyPrefix);
  });

  it("createLocalCacheOptions_explicitEmptyKeyPrefix_keepsEmpty", () => {
    const opts = createLocalCacheOptions({ keyPrefix: "" });

    expect(opts.keyPrefix).toBe("");
  });

  it("createLocalCacheOptions_returnedObject_isMutableCopyNotSharedWithDefaults", () => {
    const a = createLocalCacheOptions();
    const b = createLocalCacheOptions();

    expect(a).not.toBe(LOCAL_CACHE_DEFAULTS);
    expect(a).not.toBe(b);

    a.maxEntries = 1;
    expect(LOCAL_CACHE_DEFAULTS.maxEntries).toBe(100_000);
    expect(b.maxEntries).toBe(100_000);
  });
});
