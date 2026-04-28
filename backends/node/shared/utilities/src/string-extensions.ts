/**
 * String utility functions.
 * Mirrors D2.Shared.Utilities.Extensions.StringExtensions in .NET.
 */

const WHITESPACE_RE = /\s+/g;
const EMAIL_RE = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
const NON_DIGITS_RE = /[^\d]/g;
/**
 * Matches characters NOT allowed in display names.
 * Allowed: letters (any Unicode script), digits, spaces, hyphens,
 * apostrophes, periods, commas.
 *
 * Use with `str.replace(DISPLAY_NAME_INVALID_RE, "")` to strip, or
 * `DISPLAY_NAME_INVALID_RE.test(str)` to validate in form inputs.
 *
 * Exported for frontend form input masking (shared rules, no drift).
 */
export const DISPLAY_NAME_INVALID_RE = /[^\p{L}\p{N}\s\-'.,]/gu;

/**
 * Cleans a string by trimming leading/trailing whitespace and collapsing
 * duplicate whitespace into a single space.
 *
 * Returns `undefined` if the input is null, undefined, empty, or whitespace-only.
 */
export function cleanStr(str: string | null | undefined): string | undefined {
  const trimmed = str?.trim();
  if (!trimmed) return undefined;
  return trimmed.replace(WHITESPACE_RE, " ");
}

/**
 * Cleans a display name by stripping dangerous/unreasonable characters
 * (HTML tags, markdown syntax, brackets, quotes, backticks, etc.),
 * then trims whitespace and collapses duplicates.
 *
 * Allowed: letters (any Unicode script), digits, spaces, hyphens,
 * apostrophes, periods, commas.
 *
 * Returns `undefined` if the result is empty after cleaning.
 *
 * @example
 * cleanDisplayStr("John O'Brien-Smith") // "John O'Brien-Smith"
 * cleanDisplayStr("<script>alert(1)</script>") // "scriptalert1script"
 * cleanDisplayStr("**bold** [link](url)") // "bold linkurl"
 * cleanDisplayStr("Dr. José María") // "Dr. José María"
 */
export function cleanDisplayStr(name: string | null | undefined): string | undefined {
  if (name == null) return undefined;
  const stripped = name.replace(DISPLAY_NAME_INVALID_RE, "");
  return cleanStr(stripped);
}

/**
 * Cleans, normalizes, and validates the basic structure of an email address.
 *
 * @returns A normalized, cleaned, lowercased email address.
 * @throws {Error} If the email is null, empty, whitespace, or not in a valid format.
 */
export function cleanAndValidateEmail(email: string | null | undefined): string {
  const cleaned = cleanStr(email)?.toLowerCase();
  if (!cleaned || !EMAIL_RE.test(cleaned)) {
    throw new Error("Invalid email address format.");
  }
  return cleaned;
}

/**
 * Cleans and normalizes a phone number by removing all non-digit characters
 * and validating its length.
 *
 * @returns A normalized phone number (E.164 format — digits only, no leading "+").
 * @throws {Error} If the phone number is null, empty, or has fewer than 7 or more than 15 digits.
 */
export function cleanAndValidatePhoneNumber(phoneNumber: string | null | undefined): string {
  if (!phoneNumber?.trim()) {
    throw new Error("Phone number cannot be null or empty.");
  }

  const cleaned = phoneNumber.replace(NON_DIGITS_RE, "");

  if (!cleaned) {
    throw new Error("Invalid phone number format.");
  }

  if (cleaned.length < 7 || cleaned.length > 15) {
    throw new Error("Phone number must be between 7 and 15 digits in length.");
  }

  return cleaned;
}

/**
 * Returns the trimmed string if non-empty, or `undefined` if the value is
 * null, undefined, empty, or whitespace-only.
 *
 * Use at boundaries (user input, DB rows, proto mapping) to convert
 * empty strings to `undefined` before they propagate as "valid" data.
 *
 * Unlike {@link cleanStr}, this does NOT collapse internal whitespace —
 * it only trims and checks for emptiness.
 */
export function truthyOrUndefined(value: string | null | undefined): string | undefined {
  if (value == null) return undefined;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

/**
 * Generates a normalized string for hashing by cleaning and lowercasing each part,
 * then joining them with a pipe ("|") character.
 *
 * @example
 * getNormalizedStrForHashing([' Test One ', '   ', 'TEST3'])
 * // Returns: 'test one||test3'
 */
export function getNormalizedStrForHashing(parts: (string | null | undefined)[]): string {
  return parts.map((p) => cleanStr(p)?.toLowerCase() ?? "").join("|");
}
