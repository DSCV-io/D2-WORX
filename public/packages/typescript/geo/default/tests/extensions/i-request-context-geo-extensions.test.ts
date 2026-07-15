// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { IRequestContext } from "@d2/request-context-abstractions";
import { describe, expect, it } from "vitest";

import {
  countryFor,
  subdivisionFor,
} from "../../src/extensions/i-request-context-geo-extensions.js";

// Minimal IRequestContext stub — only the geo fields are exercised here.
// Other members default to undefined because nothing in countryFor /
// subdivisionFor reads them. The `as IRequestContext` cast narrows from
// the partial stub to the full interface.
function ctx(overrides: {
  countryIso31661Alpha2Code?: string | undefined;
  subdivisionIso31662Code?: string | undefined;
}): IRequestContext {
  return {
    countryIso31661Alpha2Code: overrides.countryIso31661Alpha2Code,
    subdivisionIso31662Code: overrides.subdivisionIso31662Code,
  } as IRequestContext;
}

describe("Default-layer IRequestContext geo extensions", () => {
  // §1.2 category: Domain-specific — happy path.
  it("countryFor returns the Country record for a valid raw alpha-2", () => {
    const country = countryFor(ctx({ countryIso31661Alpha2Code: "US" }));
    expect(country).toBeDefined();
    expect(country?.iso31661Alpha2Code).toBe("US");
  });

  // §1.2 category: Input validation — boundary contract.
  it("countryFor returns undefined for undefined raw", () => {
    expect(
      countryFor(ctx({ countryIso31661Alpha2Code: undefined })),
    ).toBeUndefined();
  });

  it("countryFor returns undefined for empty raw", () => {
    expect(countryFor(ctx({ countryIso31661Alpha2Code: "" }))).toBeUndefined();
  });

  it("countryFor returns undefined for whitespace raw", () => {
    expect(
      countryFor(ctx({ countryIso31661Alpha2Code: "   " })),
    ).toBeUndefined();
  });

  it("countryFor returns undefined for unknown raw (ZZ)", () => {
    expect(
      countryFor(ctx({ countryIso31661Alpha2Code: "ZZ" })),
    ).toBeUndefined();
  });

  it("countryFor accepts lowercase raw via uppercase normalization", () => {
    // The helper uppercases the raw string before set / catalog lookup,
    // matching the .NET parser's `ignoreCase: true` contract so JWT
    // claims minted with lowercase / mixed-case codes resolve uniformly
    // across runtimes.
    const country = countryFor(ctx({ countryIso31661Alpha2Code: "us" }));
    expect(country).toBeDefined();
    expect(country?.iso31661Alpha2Code).toBe("US");
  });

  it("countryFor accepts mixed-case raw (Us / uS)", () => {
    expect(
      countryFor(ctx({ countryIso31661Alpha2Code: "Us" }))?.iso31661Alpha2Code,
    ).toBe("US");
    expect(
      countryFor(ctx({ countryIso31661Alpha2Code: "uS" }))?.iso31661Alpha2Code,
    ).toBe("US");
  });

  it("countryFor AQ returns Antarctica with no PII", () => {
    const aq = countryFor(ctx({ countryIso31661Alpha2Code: "AQ" }));
    expect(aq).toBeDefined();
    expect(aq?.iso31661Alpha2Code).toBe("AQ");
  });

  // §1.2 category: Domain-specific — subdivision happy path.
  it("subdivisionFor returns the Subdivision record for a valid raw ISO-3166-2", () => {
    const sub = subdivisionFor(ctx({ subdivisionIso31662Code: "US-NY" }));
    expect(sub).toBeDefined();
    expect(sub?.iso31662Code).toBe("US-NY");
  });

  it("subdivisionFor returns undefined for undefined raw", () => {
    expect(
      subdivisionFor(ctx({ subdivisionIso31662Code: undefined })),
    ).toBeUndefined();
  });

  it("subdivisionFor returns undefined for empty raw", () => {
    expect(
      subdivisionFor(ctx({ subdivisionIso31662Code: "" })),
    ).toBeUndefined();
  });

  it("subdivisionFor returns undefined for unknown raw", () => {
    expect(
      subdivisionFor(ctx({ subdivisionIso31662Code: "ZZ-99" })),
    ).toBeUndefined();
  });

  it("subdivisionFor accepts lowercase raw via uppercase normalization", () => {
    const sub = subdivisionFor(ctx({ subdivisionIso31662Code: "us-ny" }));
    expect(sub).toBeDefined();
    expect(sub?.iso31662Code).toBe("US-NY");
  });

  it("subdivisionFor accepts mixed-case raw (Us-Ny / uS-nY)", () => {
    expect(
      subdivisionFor(ctx({ subdivisionIso31662Code: "Us-Ny" }))?.iso31662Code,
    ).toBe("US-NY");
    expect(
      subdivisionFor(ctx({ subdivisionIso31662Code: "uS-nY" }))?.iso31662Code,
    ).toBe("US-NY");
  });
});
