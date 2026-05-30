// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CountryCode } from "@d2/geo-abstractions";
import { ok, validationFailed } from "@d2/result";
import { falsey } from "@d2/utilities";
import { describe, expect, it } from "vitest";

import type {
  IEmailValidator,
  IPhoneValidator,
  IPostalCodeValidator,
} from "../src/index.js";

// Branded-string fixtures — mirrors the cast pattern the sibling
// `@d2/validation` default-impl tests use to materialize `CountryCode`
// values without importing the runtime catalog.
const US = "US" as CountryCode;
const CA = "CA" as CountryCode;

/**
 * Interface-shape coverage for the hand-written validator contracts. This is
 * a type-only package — there is no runtime behavior to exercise — so these
 * tests assert that a conforming implementation satisfies each interface's
 * surface (method arity, optional parameters, `D2Result` return) and that the
 * structural-typing contract holds. Behavioral parity with the .NET
 * `D2.Shared.Validation.Abstractions` interfaces is covered by the
 * `@d2/validation` default-impl + contract-tests parity suites.
 *
 * The package ships these tests (rather than `passWithNoTests`) to mirror the
 * sibling `@d2/geo-abstractions` package, which carries real test files; this
 * also keeps `vitest run` exiting 0 in CI.
 */

describe("IEmailValidator contract", () => {
  // A stub that conforms structurally to the interface. `email` is
  // `string | undefined` and the single argument is the only parameter.
  const stub: IEmailValidator = {
    validate(email) {
      return falsey(email)
        ? validationFailed<string>()
        : ok(email!.trim().toLowerCase());
    },
  };

  it("returns an ok result for well-formed input", () => {
    const result = stub.validate("  Foo@Example.COM  ");
    expect(result.success).toBe(true);
    expect(result.data).toBe("foo@example.com");
  });

  it("fails for undefined / empty / whitespace input", () => {
    expect(stub.validate(undefined).success).toBe(false);
    expect(stub.validate("").success).toBe(false);
    expect(stub.validate("   ").success).toBe(false);
  });
});

describe("IPhoneValidator contract", () => {
  // The optional `defaultRegion` parameter is `CountryCode | undefined`.
  const stub: IPhoneValidator = {
    validate(phone, defaultRegion) {
      void defaultRegion;
      return falsey(phone) ? validationFailed<string>() : ok(phone!.trim());
    },
  };

  it("accepts an optional default region argument", () => {
    const withRegion = stub.validate("+15551234567", US);
    const withoutRegion = stub.validate("+15551234567");
    expect(withRegion.success).toBe(true);
    expect(withoutRegion.success).toBe(true);
  });

  it("fails for undefined / empty / whitespace input", () => {
    expect(stub.validate(undefined).success).toBe(false);
    expect(stub.validate("").success).toBe(false);
    expect(stub.validate("   ").success).toBe(false);
  });
});

describe("IPostalCodeValidator contract", () => {
  // The optional `countryCode` parameter is `CountryCode | undefined`; a
  // conforming impl fails closed when it is omitted (no country-agnostic
  // fallback — mirrors the .NET contract).
  const stub: IPostalCodeValidator = {
    validate(postalCode, countryCode) {
      return falsey(postalCode) || countryCode === undefined
        ? validationFailed<string>()
        : ok(postalCode!.trim().toUpperCase());
    },
  };

  it("returns an ok result when a known country is supplied", () => {
    const result = stub.validate(" k1a 0b1 ", CA);
    expect(result.success).toBe(true);
    expect(result.data).toBe("K1A 0B1");
  });

  it("fails closed when the country code is omitted", () => {
    const result = stub.validate("K1A0B1");
    expect(result.success).toBe(false);
  });

  it("fails for undefined / empty / whitespace input", () => {
    expect(stub.validate(undefined, CA).success).toBe(false);
    expect(stub.validate("", CA).success).toBe(false);
    expect(stub.validate("   ", CA).success).toBe(false);
  });
});
