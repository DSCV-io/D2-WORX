import { COMMON_PASSWORDS } from "../data/common-passwords.js";

export interface PasswordValidationResult {
  valid: boolean;
  code?: string;
  /**
   * Translation key for the failure reason. MUST stay exactly in sync with
   * `TK.auth.errors.*` in `@d2/i18n` — defined as local constants here to
   * avoid an `@d2/auth-domain` → `@d2/i18n` dependency.
   */
  message?: string;
}

/**
 * Translation keys for password-policy violations.
 * MUST match `TK.auth.errors.PASSWORD_*` in `@d2/i18n` (translation-keys.ts).
 * Duplicated here only because `@d2/auth-domain` cannot depend on `@d2/i18n`.
 */
const PASSWORD_NUMERIC_ONLY_KEY = "auth_errors_PASSWORD_NUMERIC_ONLY";
const PASSWORD_DATE_LIKE_KEY = "auth_errors_PASSWORD_DATE_LIKE";
const PASSWORD_TOO_COMMON_KEY = "auth_errors_PASSWORD_TOO_COMMON";

/**
 * Pure synchronous password validation — no async, no network.
 *
 * Checks (in order — first failure wins):
 *   1. Numeric-only (e.g., "123456789012")
 *   2. Date-like (digits + date separators only, e.g., "2025-10-01", "25/01/1997")
 *   3. Common password blocklist (~1,000 entries, case-insensitive)
 *
 * Length validation is handled by BetterAuth natively (minPasswordLength / maxPasswordLength)
 * and runs BEFORE this function is called via the `hash` hook.
 *
 * `message` is a TK translation key (`auth_errors_PASSWORD_*`) so callers
 * (e.g. password-hooks → APIError) can hand it straight to `translateMessage()`
 * on the FE without leaking English to the user.
 */
export function validatePassword(password: string): PasswordValidationResult {
  // 1. Numeric-only — e.g. "123456789012"
  if (/^\d+$/.test(password)) {
    return {
      valid: false,
      code: "PASSWORD_NUMERIC_ONLY",
      message: PASSWORD_NUMERIC_ONLY_KEY,
    };
  }

  // 2. Date-like — only digits + date separators (- / . and whitespace)
  if (/^[\d\-/.\s]+$/.test(password)) {
    return {
      valid: false,
      code: "PASSWORD_DATE_LIKE",
      message: PASSWORD_DATE_LIKE_KEY,
    };
  }

  // 3. Common password blocklist (case-insensitive)
  if (COMMON_PASSWORDS.has(password.toLowerCase())) {
    return {
      valid: false,
      code: "PASSWORD_TOO_COMMON",
      message: PASSWORD_TOO_COMMON_KEY,
    };
  }

  return { valid: true };
}
