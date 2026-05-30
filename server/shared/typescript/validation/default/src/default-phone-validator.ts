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
import type { IPhoneValidator } from "@d2/validation-abstractions";
import {
  parsePhoneNumberFromString,
  type CountryCode as LibCountryCode,
} from "libphonenumber-js";

/**
 * Default `IPhoneValidator` implementation. Mirrors the .NET
 * `D2.Shared.Validation.DefaultPhoneValidator` — both delegate to a
 * libphonenumber port (libphonenumber-js here, libphonenumber-csharp on the
 * .NET side), validate, and normalize to E.164. Same per-field `D2Result`
 * contract on both runtimes.
 */
export class DefaultPhoneValidator implements IPhoneValidator {
  /**
   * Validates the supplied phone number and returns its E.164 form on
   * success.
   *
   * @param phone - The phone number to validate (may be `undefined`, empty,
   *   or whitespace).
   * @param defaultRegion - Optional default region used to interpret
   *   national-format numbers lacking an international `+` prefix.
   * @returns `ok` wrapping the E.164-formatted number on success;
   *   `validationFailed` keyed `"phone"` with `PHONE_INVALID` on `undefined`,
   *   empty, whitespace, or structurally invalid input.
   */
  validate(
    phone: string | undefined,
    defaultRegion?: CountryCode,
  ): D2Result<string> {
    if (falsey(phone)) return DefaultPhoneValidator.invalid();

    // The `CountryCode` brand erases to its underlying alpha-2 string at
    // runtime, which is exactly the region identifier libphonenumber-js
    // expects; the cast is the compile-time bridge between the two brands.
    const region = defaultRegion as LibCountryCode | undefined;

    // `falsey` already excluded undefined / empty / whitespace.
    const parsed = parsePhoneNumberFromString(phone!, region);
    if (falsey(parsed) || !parsed!.isValid())
      return DefaultPhoneValidator.invalid();

    return ok<string>(parsed!.format("E.164"));
  }

  private static invalid(): D2Result<string> {
    return validationFailed<string>({
      inputErrors: [
        inputError("phone", [tk(TK.common.validation.PHONE_INVALID)]),
      ],
    });
  }
}
