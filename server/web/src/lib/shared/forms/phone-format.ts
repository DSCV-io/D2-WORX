/**
 * Phone number formatting utilities using libphonenumber-js.
 *
 * Provides display formatting, as-you-type formatting, and country prefix
 * lookup — all driven by ISO 3166-1 alpha-2 country codes.
 */
import {
  AsYouType,
  getCountryCallingCode as getCallingCode,
  parsePhoneNumberFromString,
  type CountryCode,
} from "libphonenumber-js";

/**
 * Format a phone number for display (e.g. `+15551234567` → `(555) 123-4567`).
 * Returns the original string if parsing fails.
 */
export function formatPhoneDisplay(number: string, countryCode: string): string {
  try {
    const parsed = parsePhoneNumberFromString(number, countryCode as CountryCode);
    return parsed?.formatNational() ?? number;
  } catch {
    return number;
  }
}

/**
 * Progressive as-you-type formatting.
 * Returns `{ formatted, national }` — the formatted display string and the
 * national number (digits only, for E.164 composition).
 */
export function formatPhoneAsYouType(
  number: string,
  countryCode: string,
): { formatted: string; national: string } {
  try {
    const formatter = new AsYouType(countryCode as CountryCode);
    const formatted = formatter.input(number);
    const nationalNumber = formatter.getNumber()?.nationalNumber ?? number.replace(/\D/g, "");
    return { formatted, national: nationalNumber };
  } catch {
    return { formatted: number, national: number.replace(/\D/g, "") };
  }
}

/**
 * Get the international calling code for a country.
 * `"US"` → `"+1"`, `"GB"` → `"+44"`, etc.
 * Returns empty string if the country code is invalid.
 */
export function getCountryCallingCode(countryCode: string): string {
  try {
    return `+${getCallingCode(countryCode as CountryCode)}`;
  } catch {
    return "";
  }
}

/**
 * Parse a phone number string and extract the country code and national number.
 * Returns null if parsing fails.
 */
export function parsePhone(
  number: string,
  defaultCountry?: string,
): { countryCode: string; nationalNumber: string; e164: string } | null {
  try {
    const parsed = parsePhoneNumberFromString(number, defaultCountry as CountryCode | undefined);
    if (!parsed?.isValid()) return null;
    return {
      countryCode: parsed.country ?? defaultCountry ?? "",
      nationalNumber: parsed.nationalNumber,
      e164: parsed.number,
    };
  } catch {
    return null;
  }
}
