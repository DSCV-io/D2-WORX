/**
 * Composable Zod 4 schema builders for common form fields.
 *
 * Constraints are derived from the Geo service DB schema (ContactConfig.cs,
 * LocationConfig.cs) and geo-client contact-schemas.ts. When Auth imposes a
 * different limit, the MORE restrictive value wins.
 *
 * Key constraints from Geo infra:
 *   - firstName/lastName/city: varchar(255)
 *   - email (in contactMethods JSONB): max 254 (geo-client)
 *   - phone (in contactMethods JSONB): max 20 (geo-client)
 *   - postalCode: varchar(16)
 *   - address lines: varchar(255)
 *   - companyWebsite: varchar(2048)
 */
import { z } from "zod";
import { isValidPhoneNumber } from "libphonenumber-js";
import { postcodeValidator } from "postcode-validator";
import { DISPLAY_NAME_INVALID_RE } from "@d2/utilities";
import * as m from "$lib/paraglide/messages.js";

/**
 * General name field (first/last name, city, etc.). Geo DB: varchar(255).
 * Strips invalid display name characters (HTML tags, brackets, backticks, etc.)
 * via transform, then re-validates min/max after stripping. If the user enters
 * ONLY invalid chars, the result is empty and fails the min(1) check.
 */
export function nameField(max = 255) {
  return z
    .string()
    .trim()
    .transform((v) => v.replace(DISPLAY_NAME_INVALID_RE, ""))
    .pipe(z.string().min(1, "Required").max(max, `Must be ${max} characters or fewer`));
}

/** Email field with format validation. Geo contact-schemas: max 254. Lowercased to match backend normalization. */
export function emailField() {
  return z
    .string()
    .trim()
    .toLowerCase()
    .min(1, { error: () => m.webclient_forms_required() })
    .max(254, { error: () => m.webclient_forms_email_too_long() })
    .email({ error: () => m.webclient_forms_email_invalid() });
}

/**
 * Phone field — validates full international format via libphonenumber-js.
 * The stored value should be in E.164 format (e.g. `+15551234567`).
 * Geo contact-schemas: max 20 for the raw value.
 */
export function phoneField() {
  return z
    .string()
    .trim()
    .min(1, { error: () => m.webclient_forms_required() })
    .max(20, { error: () => m.webclient_forms_phone_too_long() })
    .refine((val) => isValidPhoneNumber(val), { error: () => m.webclient_forms_phone_invalid() });
}

/** Optional phone field — same rules but allows empty string. */
export function phoneFieldOptional() {
  return z
    .string()
    .trim()
    .max(20, { error: () => m.webclient_forms_phone_too_long() })
    .refine((val) => !val || isValidPhoneNumber(val), {
      error: () => m.webclient_forms_phone_invalid(),
    })
    .optional()
    .default("");
}

/**
 * Postal/zip code field with optional country-specific validation.
 * When `countryCode` is provided, validates against that country's format.
 * Geo DB: varchar(16).
 */
export function postcodeField(countryCode?: string) {
  const base = z.string().trim().min(1, "Required").max(16, "Postal code too long");
  if (!countryCode) return base;
  return base.refine(
    (val) => postcodeValidator(val, countryCode),
    `Invalid postal code for ${countryCode}`,
  );
}

/** Street address line. Geo DB: varchar(255). */
export function streetField(max = 255) {
  return z.string().trim().min(1, "Required").max(max, `Must be ${max} characters or fewer`);
}

/** URL field with optional protocol prefix. */
export function urlField() {
  return z
    .string()
    .max(2048, "URL too long")
    .refine((val) => !val || /^https?:\/\/.+/.test(val), "Invalid URL")
    .optional()
    .default("");
}

/**
 * Password field — mirrors auth-domain password rules (client-side subset).
 * Enforces min/max length, rejects numeric-only and date-like strings.
 * Common blocklist and HIBP stay server-side.
 */
export function passwordField(min = 12, max = 128) {
  return z
    .string()
    .min(min, `Password must be at least ${min} characters`)
    .max(max, `Password must be ${max} characters or fewer`)
    .refine((v) => !/^\d+$/.test(v), "Password cannot be only numbers")
    .refine((v) => !/^[\d\-/.\s]+$/.test(v), "Password cannot be only numbers and date separators");
}

/** Currency code field (ISO 4217 alpha-3). */
export function currencyField() {
  return z
    .string()
    .length(3, "Must be a 3-letter currency code")
    .regex(/^[A-Z]{3}$/, "Must be uppercase letters");
}
