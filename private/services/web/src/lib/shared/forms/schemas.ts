// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

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
import { DISPLAY_NAME_INVALID_RE } from "@dcsv-io/d2-utilities";
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
    .pipe(
      z
        .string()
        .min(1, { error: () => m.webclient_forms_required() })
        .max(max, { error: () => m.webclient_forms_max_length({ max: String(max) }) }),
    );
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
  const base = z
    .string()
    .trim()
    .min(1, { error: () => m.webclient_forms_required() })
    .max(16, { error: () => m.webclient_forms_postal_code_too_long() });
  if (!countryCode) return base;
  return base.refine((val) => postcodeValidator(val, countryCode), {
    error: () => m.webclient_forms_postal_code_invalid_for_country({ country: countryCode }),
  });
}

/** Street address line. Geo DB: varchar(255). */
export function streetField(max = 255) {
  return z
    .string()
    .trim()
    .min(1, { error: () => m.webclient_forms_required() })
    .max(max, { error: () => m.webclient_forms_max_length({ max: String(max) }) });
}

/** URL field with optional protocol prefix. */
export function urlField() {
  return z
    .string()
    .max(2048, { error: () => m.webclient_forms_url_too_long() })
    .refine((val) => !val || /^https?:\/\/.+/.test(val), {
      error: () => m.webclient_forms_url_invalid(),
    })
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
    .min(min, { error: () => m.webclient_forms_password_min_length({ min: String(min) }) })
    .max(max, { error: () => m.webclient_forms_max_length({ max: String(max) }) })
    .refine((v) => !/^\d+$/.test(v), { error: () => m.auth_errors_PASSWORD_NUMERIC_ONLY() })
    .refine((v) => !/^[\d\-/.\s]+$/.test(v), { error: () => m.auth_errors_PASSWORD_DATE_LIKE() });
}

/** Currency code field (ISO 4217 alpha-3). */
export function currencyField() {
  return z
    .string()
    .length(3, { error: () => m.webclient_forms_currency_invalid_length() })
    .regex(/^[A-Z]{3}$/, { error: () => m.webclient_forms_currency_uppercase_required() });
}
