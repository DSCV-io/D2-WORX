// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CountryCode } from "@d2/geo-abstractions";
import { tk } from "@d2/result";
import type { D2Result } from "@d2/result";
import { validationFailed, ok } from "@d2/result";
import { falsey } from "@d2/utilities";

const TK_POSTAL_CODE_INVALID = tk("geo_validation_postal_code_invalid");

/**
 * Boundary validator for postal codes. Mirrors .NET
 * `D2.Shared.Location.IPostalCodeValidator`. The default implementation
 * (`defaultPostalCodeValidator()`) is global-range only; consumer
 * implementations may use the `countryCode` argument for
 * country-specific validation.
 */
export interface IPostalCodeValidator {
  validate(
    postalCode: string | undefined,
    countryCode?: CountryCode | undefined,
  ): D2Result<string>;
}

// Global-range shape — anchored, bounded class, bounded repetition.
// B1 shape (no backtracking); kept as compiled regex literal.
const GLOBAL_SHAPE_RE = /^[A-Z0-9](?:[A-Z0-9 -]{1,8}[A-Z0-9])$/i;

class DefaultPostalCodeValidator implements IPostalCodeValidator {
  validate(
    postalCode: string | undefined,
    _countryCode?: CountryCode | undefined,
  ): D2Result<string> {
    if (falsey(postalCode))
      return validationFailed<string>({ messages: [TK_POSTAL_CODE_INVALID] });

    const trimmed = postalCode!.trim();
    if (!GLOBAL_SHAPE_RE.test(trimmed))
      return validationFailed<string>({ messages: [TK_POSTAL_CODE_INVALID] });

    return ok<string>(trimmed);
  }
}

const SR_SINGLETON: IPostalCodeValidator = new DefaultPostalCodeValidator();

/**
 * Returns the default postal-code validator — global-range shape check
 * only (3-10 alphanumeric characters; internal spaces and hyphens
 * allowed; alphanumeric at both ends). Country-blind by design;
 * consumers wanting strict per-country validation implement their own
 * `IPostalCodeValidator`.
 */
export function defaultPostalCodeValidator(): IPostalCodeValidator {
  return SR_SINGLETON;
}
