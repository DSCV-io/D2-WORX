// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CountryCode } from "@d2/geo-abstractions";
import type { D2Result } from "@d2/result";

/**
 * Mirror of .NET `D2.Shared.Validation.Abstractions.IPostalCodeValidator` —
 * country-aware postal-code validator that returns a normalized form on
 * success.
 *
 * Implementations live in `@d2/validation` backed by the default
 * per-country normalization rules; tests can supply ad-hoc fixtures by
 * implementing this interface directly.
 */
export interface IPostalCodeValidator {
  /**
   * Validates the supplied postal code and returns the normalized
   * representation on success.
   *
   * @param postalCode - The postal code to validate (may be `undefined`,
   *   empty, or whitespace).
   * @param countryCode - Optional country whose postal-code format governs
   *   validation. When `undefined` / omitted, validation fails closed —
   *   there is NO country-agnostic structural fallback. An unknown /
   *   unsupported country also fails closed.
   * @returns An `ok` `D2Result` wrapping the normalized (trimmed and
   *   uppercased) postal code on success; a `validationFailed` `D2Result`
   *   with a per-field `InputError` keyed `"postalCode"` carrying
   *   `POSTAL_CODE_INVALID` on `undefined`, empty, whitespace, structurally
   *   invalid, omitted-country, or unknown-country input.
   */
  validate(
    postalCode: string | undefined,
    countryCode?: CountryCode,
  ): D2Result<string>;
}
