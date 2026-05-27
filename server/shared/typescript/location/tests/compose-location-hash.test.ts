// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { CountryCode as CountryCodeConst } from "@d2/geo-abstractions";
import type { CountryCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import {
  composeLocationHash,
  createAdminLocation,
  createCoordinates,
  createStreetAddress,
  type AdminLocation,
  type Coordinates,
  type StreetAddress,
} from "../src/index.js";

const US: CountryCode = CountryCodeConst.US as CountryCode;

function coord(): Coordinates {
  return createCoordinates(40.7128, -74.006).data!;
}
function street(): StreetAddress {
  return createStreetAddress("123 Main St").data!;
}
function admin(): AdminLocation {
  return createAdminLocation(US, undefined, "Brooklyn").data!;
}

describe("composeLocationHash", () => {
  it("all-undefined → undefined", () => {
    expect(
      composeLocationHash(undefined, undefined, undefined),
    ).toBeUndefined();
  });

  it("all-undefined (alternate call site) → undefined", () => {
    expect(
      composeLocationHash(undefined, undefined, undefined),
    ).toBeUndefined();
  });

  it("only coords → v1.-prefixed length 67", () => {
    const h = composeLocationHash(coord(), undefined, undefined);
    expect(h).toMatch(/^v1\./);
    expect(h).toHaveLength(67);
  });

  it("only street → v1.-prefixed", () => {
    expect(composeLocationHash(undefined, street(), undefined)).toMatch(
      /^v1\./,
    );
  });

  it("only admin → v1.-prefixed", () => {
    expect(composeLocationHash(undefined, undefined, admin())).toMatch(/^v1\./);
  });

  it("2-of-3 (coords+street)", () => {
    expect(composeLocationHash(coord(), street(), undefined)).toMatch(/^v1\./);
  });

  it("2-of-3 (coords+admin)", () => {
    expect(composeLocationHash(coord(), undefined, admin())).toMatch(/^v1\./);
  });

  it("2-of-3 (street+admin)", () => {
    expect(composeLocationHash(undefined, street(), admin())).toMatch(/^v1\./);
  });

  it("all-3 → v1.-prefixed", () => {
    expect(composeLocationHash(coord(), street(), admin())).toMatch(/^v1\./);
  });

  it("same component in different slot → different outer hash", () => {
    const a = composeLocationHash(coord(), undefined, undefined);
    const b = composeLocationHash(undefined, street(), undefined);
    const c = composeLocationHash(undefined, undefined, admin());
    expect(a).not.toBe(b);
    expect(a).not.toBe(c);
    expect(b).not.toBe(c);
  });

  it("deterministic — same inputs produce byte-identical output", () => {
    expect(composeLocationHash(coord(), street(), admin())).toBe(
      composeLocationHash(coord(), street(), admin()),
    );
  });

  it("inner v1. prefix participates — changing component changes outer", () => {
    const c1 = createCoordinates(40.0, -74.0).data!;
    const c2 = createCoordinates(41.0, -74.0).data!;
    expect(c1.hashId).not.toBe(c2.hashId);
    expect(composeLocationHash(c1, undefined, undefined)).not.toBe(
      composeLocationHash(c2, undefined, undefined),
    );
  });
});
