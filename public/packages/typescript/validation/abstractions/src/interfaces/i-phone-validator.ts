// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { CountryCode } from "@dcsv-io/d2-geo-abstractions";
import type { D2Result } from "@dcsv-io/d2-result";

/**
 * Mirror of .NET `DcsvIo.D2.Validation.Abstractions.IPhoneValidator` —
 * validates a phone number and returns a normalized E.164 form on success.
 *
 * Implementations live in `@dcsv-io/d2-validation` backed by the default
 * normalization rules; tests can supply ad-hoc fixtures by implementing
 * this interface directly.
 */
export interface IPhoneValidator {
  /**
   * Validates the supplied phone number and returns the normalized E.164
   * representation on success.
   *
   * @param phone - The phone number to validate (may be `undefined`,
   *   empty, or whitespace).
   * @param defaultRegion - Optional default region used to interpret
   *   national-format numbers that lack an international `+` prefix. When
   *   omitted, only numbers already carrying an international prefix can be
   *   resolved.
   * @returns An `ok` `D2Result` wrapping the normalized E.164 phone number
   *   on success; a `validationFailed` `D2Result` with a per-field
   *   `InputError` keyed `"phone"` on `undefined`, empty, whitespace, or
   *   structurally invalid input.
   */
  validate(
    phone: string | undefined,
    defaultRegion?: CountryCode,
  ): D2Result<string>;
}
