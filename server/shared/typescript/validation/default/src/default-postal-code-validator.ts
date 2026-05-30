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

import postalCodeRegexes from "../../../../../../contracts/validation/postal-code-regexes.json" with { type: "json" };

/**
 * Per-country postal-code regex map, compiled once at module load. Keys are
 * ISO 3166-1 alpha-2 country codes; each value is the anchored pattern from
 * the shared dataset, compiled with the `"i"` flag to mirror the .NET side's
 * `RegexOptions.IgnoreCase`. The `$comment` metadata key in the JSON is
 * skipped — it is documentation, not a country entry.
 *
 * Sourced from `contracts/validation/postal-code-regexes.json`, the SINGLE
 * cross-runtime source of truth (the .NET `D2.Shared.Validation` embeds the
 * same file). Building a `RegExp` per entry up front avoids recompiling on
 * every `validate` call.
 */
const COUNTRY_REGEXES: ReadonlyMap<string, RegExp> = new Map(
  Object.entries(postalCodeRegexes as Record<string, string>)
    .filter(([key]) => !key.startsWith("$"))
    .map(([key, pattern]) => [key, new RegExp(pattern, "i")] as const),
);

/**
 * Default `IPostalCodeValidator` implementation. Mirrors the .NET
 * `D2.Shared.Validation.DefaultPostalCodeValidator` — both compile the SAME
 * per-country patterns from `contracts/validation/postal-code-regexes.json`,
 * run a country-aware structural check, and normalize to trimmed + uppercased
 * form. An unknown / absent country fails closed (ValidationFailed, never a
 * throw) on both runtimes, so cross-language behavior on unsupported countries
 * is identical.
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
    // same form the .NET side matches against.
    const normalized = postalCode!.trim().toUpperCase();

    // The `CountryCode` brand erases to its underlying alpha-2 string at
    // runtime, the form the dataset keys its per-country regexes on.
    const regex = COUNTRY_REGEXES.get(countryCode as string);

    // Fail closed: an unknown / unsupported country has no compiled pattern.
    if (regex === undefined) return DefaultPostalCodeValidator.invalid();

    if (!regex.test(normalized)) return DefaultPostalCodeValidator.invalid();

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
