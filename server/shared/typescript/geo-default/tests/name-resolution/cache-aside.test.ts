// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { CountryCode } from "@d2/geo-abstractions";
import { beforeEach, describe, expect, it } from "vitest";

import { CountryLookup } from "../../src/countries.js";
import {
  _internalCountryBuildCount,
  _internalResetCache,
  _internalSubdivisionBuildCount,
  tryResolveCountryByName,
  tryResolveSubdivisionByName,
} from "../../src/name-resolution/default-geo-name-resolver.js";

describe("DefaultGeoNameResolver cache-aside", () => {
  beforeEach(() => {
    _internalResetCache();
  });

  // §1.2 category: State-lifecycle — first call triggers build.
  it("first call builds the country cache (build count = 1)", () => {
    expect(_internalCountryBuildCount()).toBe(0);
    tryResolveCountryByName("United States");
    expect(_internalCountryBuildCount()).toBe(1);
  });

  // §1.2 category: State-lifecycle — second call reuses cache.
  it("subsequent calls do NOT rebuild the cache (build count stays 1)", () => {
    tryResolveCountryByName("United States");
    tryResolveCountryByName("Australia");
    tryResolveCountryByName("USA");
    expect(_internalCountryBuildCount()).toBe(1);
  });

  // §1.2 category: Concurrency — single-thread JS gives build-once.
  it("concurrent first-callers via Promise.all rebuild AT MOST once", async () => {
    const parallelism = 32;
    const promises = Array.from({ length: parallelism }, () =>
      Promise.resolve(tryResolveCountryByName("United States")),
    );
    await Promise.all(promises);
    expect(_internalCountryBuildCount()).toBe(1);
  });

  // §1.2 category: State-lifecycle — per-country subdivision cache.
  it("subdivision cache builds independently per parent country", () => {
    const us = CountryLookup.byCode[CountryCode.US];
    const ca = CountryLookup.byCode[CountryCode.CA];

    tryResolveSubdivisionByName("California", us!);
    expect(_internalSubdivisionBuildCount(CountryCode.US as CountryCode)).toBe(
      1,
    );
    expect(_internalSubdivisionBuildCount(CountryCode.CA as CountryCode)).toBe(
      0,
    );

    tryResolveSubdivisionByName("Ontario", ca!);
    expect(_internalSubdivisionBuildCount(CountryCode.US as CountryCode)).toBe(
      1,
    );
    expect(_internalSubdivisionBuildCount(CountryCode.CA as CountryCode)).toBe(
      1,
    );
  });

  it("multiple lookups for the same parent do NOT rebuild the subdivision cache", () => {
    const us = CountryLookup.byCode[CountryCode.US];
    tryResolveSubdivisionByName("California", us!);
    tryResolveSubdivisionByName("Texas", us!);
    tryResolveSubdivisionByName("Georgia", us!);
    expect(_internalSubdivisionBuildCount(CountryCode.US as CountryCode)).toBe(
      1,
    );
  });
});
