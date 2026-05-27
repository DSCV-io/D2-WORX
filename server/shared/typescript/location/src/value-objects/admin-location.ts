// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CountryCode, SubdivisionCode } from "@d2/geo-abstractions";
import { tk } from "@d2/result";
import type { D2Result } from "@d2/result";
import { validationFailed, ok } from "@d2/result";
import { cleanStr, falsey, truthy } from "@d2/utilities";

import { sha256Hex } from "../internal/sha256-hex.js";
import type { IPostalCodeValidator } from "../postal-code-validator.js";
import { normalizeForHash } from "./street-address.js";

const TK_COUNTRY_SUBDIVISION_MISMATCH = tk(
  "geo_validation_admin_country_subdivision_mismatch",
);
const TK_ADMIN_EMPTY_RECORD = tk("geo_validation_admin_empty_record");

/**
 * Immutable administrative-hierarchy location value object: country,
 * subdivision, city, postal code (any subset). Mirrors
 * `D2.Shared.Location.ValueObjects.AdminLocation`.
 *
 * Coherence is enforced when both country and subdivision are supplied —
 * a mismatch returns `validationFailed`. A subdivision-only caller has
 * country auto-populated from the subdivision's parent-country prefix.
 * A country-only caller is valid (city / postal optional). The all-null
 * caller is rejected as a degenerate empty record.
 */
export interface AdminLocation {
  readonly city?: string;
  readonly postalCode?: string;
  readonly subdivisionIso31662Code?: SubdivisionCode;
  readonly countryIso31661Alpha2Code?: CountryCode;
  readonly hashId: string;
}

/**
 * Derives the parent-country alpha-2 code from a subdivision code's
 * ISO 3166-2 prefix (e.g. `"US-NY"` → `"US"`). Mirrors the .NET
 * `SubdivisionCode.ParentCountry` derivation.
 */
function deriveParentCountry(sub: SubdivisionCode): CountryCode {
  // ISO 3166-2 codes ALWAYS have the form `XX-...` where `XX` is the
  // alpha-2 parent country.
  const dashIdx = (sub as string).indexOf("-");
  if (dashIdx < 0) {
    throw new Error(`Invalid SubdivisionCode form: '${sub as string}'`);
  }
  return (sub as string).substring(0, dashIdx) as CountryCode;
}

/**
 * Creates an `AdminLocation` from any subset of the four administrative
 * fields. Enforces country/subdivision coherence and auto-populates
 * country from subdivision when applicable.
 */
export function createAdminLocation(
  countryIso31661Alpha2Code?: CountryCode | undefined,
  subdivisionIso31662Code?: SubdivisionCode | undefined,
  city?: string | undefined,
  postalCode?: string | undefined,
  postalCodeValidator?: IPostalCodeValidator | undefined,
): D2Result<AdminLocation> {
  const cleanedCity = cleanStr(city) ?? undefined;
  const cleanedPostal = cleanStr(postalCode) ?? undefined;

  // Coherence + auto-populate.
  let effectiveCountry = countryIso31661Alpha2Code;
  if (subdivisionIso31662Code !== undefined) {
    const derived = deriveParentCountry(subdivisionIso31662Code);
    if (
      countryIso31661Alpha2Code !== undefined &&
      countryIso31661Alpha2Code !== derived
    ) {
      return validationFailed<AdminLocation>({
        messages: [TK_COUNTRY_SUBDIVISION_MISMATCH],
      });
    }
    if (effectiveCountry === undefined) effectiveCountry = derived;
  }

  // Degenerate empty record — all four fields null/empty after cleaning.
  if (
    effectiveCountry === undefined &&
    subdivisionIso31662Code === undefined &&
    falsey(cleanedCity) &&
    falsey(cleanedPostal)
  ) {
    return validationFailed<AdminLocation>({
      messages: [TK_ADMIN_EMPTY_RECORD],
    });
  }

  // Postal-code validation (only when both validator + value supplied).
  let validatedPostal = cleanedPostal;
  if (truthy(cleanedPostal) && postalCodeValidator !== undefined) {
    const validation = postalCodeValidator.validate(
      cleanedPostal,
      effectiveCountry,
    );
    if (!validation.success) {
      return validationFailed<AdminLocation>({
        messages: validation.messages,
      });
    }
    validatedPostal = validation.data;
  }

  const hashInput =
    normalizeForHash(cleanedCity) +
    "|" +
    normalizeForHash(validatedPostal) +
    "|" +
    ((subdivisionIso31662Code as string | undefined) ?? "") +
    "|" +
    (effectiveCountry ?? "");

  const hashId = "v1." + sha256Hex(hashInput);

  const result: AdminLocation = {
    hashId,
    ...(cleanedCity !== undefined ? { city: cleanedCity } : {}),
    ...(validatedPostal !== undefined ? { postalCode: validatedPostal } : {}),
    ...(subdivisionIso31662Code !== undefined
      ? { subdivisionIso31662Code: subdivisionIso31662Code }
      : {}),
    ...(effectiveCountry !== undefined
      ? { countryIso31661Alpha2Code: effectiveCountry }
      : {}),
  };
  return ok<AdminLocation>(result);
}
