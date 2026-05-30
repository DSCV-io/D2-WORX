// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CountryCode } from "@d2/geo-abstractions";
import { TK } from "@d2/i18n/keys";
import {
  inputError,
  ok,
  tk,
  validationFailed,
  type D2Result,
} from "@d2/result";
import { falsey } from "@d2/utilities";
import type { IPostalCodeValidator } from "@d2/validation-abstractions";
import { postcodeValidator } from "postcode-validator";

/**
 * Default `IPostalCodeValidator` implementation. Mirrors the .NET
 * `D2.Shared.Validation.DefaultPostalCodeValidator` — both run a
 * country-aware structural check and normalize to trimmed + uppercased form.
 * An unknown country fails closed (ValidationFailed, never a throw) on both
 * runtimes, so cross-language behavior on unsupported countries is identical.
 */
export class DefaultPostalCodeValidator implements IPostalCodeValidator {
  /**
   * Validates the supplied postal code and returns its normalized form on
   * success.
   *
   * @param postalCode - The postal code to validate (may be `undefined`,
   *   empty, or whitespace).
   * @param countryCode - Optional country whose postal-code format governs
   *   validation. An unknown / unsupported country fails closed.
   * @returns `ok` wrapping the trimmed + uppercased postal code on success;
   *   `validationFailed` keyed `"postalCode"` with `POSTAL_CODE_INVALID` on
   *   `undefined`, empty, whitespace, structurally invalid, or
   *   unknown-country input.
   */
  validate(
    postalCode: string | undefined,
    countryCode?: CountryCode,
  ): D2Result<string> {
    if (falsey(postalCode)) return DefaultPostalCodeValidator.invalid();

    // Fail closed when no country is supplied — an absent country cannot
    // select a per-country format, so there is nothing to validate against.
    // This mirrors the .NET `DefaultPostalCodeValidator`, which returns
    // `ValidationFailed` for a null country. There is deliberately NO
    // permissive country-agnostic fallback (e.g. the `"INTL"` pattern): an
    // unknown / absent country always fails closed on both runtimes.
    if (countryCode === undefined) return DefaultPostalCodeValidator.invalid();

    // `falsey` already excluded undefined / empty / whitespace.
    // Normalize (trim + uppercase) before validating so the regex sees the
    // same form the .NET side matches against. `postcode-validator` applies
    // the per-country regex verbatim, so the uppercase normalization here is
    // what keeps case-insensitive inputs (e.g. "k1a 0b1") matching.
    const normalized = postalCode!.trim().toUpperCase();

    // The `CountryCode` brand erases to its underlying alpha-2 string at
    // runtime, the form `postcode-validator` keys its per-country regexes on.
    const country = countryCode as string;

    // Fail closed: `postcodeValidator` throws for an unknown country code.
    // Treat any throw as a validation failure rather than letting it escape.
    let isValid: boolean;
    try {
      isValid = postcodeValidator(normalized, country);
    } catch {
      return DefaultPostalCodeValidator.invalid();
    }

    if (!isValid) return DefaultPostalCodeValidator.invalid();

    return ok<string>(normalized);
  }

  private static invalid(): D2Result<string> {
    return validationFailed<string>({
      inputErrors: [
        inputError("postalCode", [
          tk(TK.common.validation.POSTAL_CODE_INVALID),
        ]),
      ],
    });
  }
}
