/**
 * OTP-related constants used by the request/verify handlers for sensitive
 * account changes (email, phone). Pure data — no infra coupling.
 */

/**
 * Rate limit policy for OTP send operations. Mirrors `SIGN_IN_THROTTLE` in
 * structure. Counter window resets after `ATTEMPT_WINDOW_SECONDS`.
 */
export const OTP_RATE_LIMIT = {
  /** Allow 3 sends in the window before any cooldown. */
  FREE_SEND_ATTEMPTS: 3,
  /** Counter window — failures and successes both increment. */
  ATTEMPT_WINDOW_SECONDS: 5 * 60,
  /** Minimum cooldown applied after each send (debounce against accidental double-clicks). */
  MIN_DELAY_MS: 30 * 1000,
  /** Maximum cooldown after exceeding the free-attempt budget. */
  MAX_DELAY_MS: 10 * 60 * 1000,
} as const;

/** TTL for OTP codes per channel. SMS shorter for security (interception risk). */
export const OTP_EXPIRY = {
  EMAIL_MS: 15 * 60 * 1000,
  SMS_MS: 5 * 60 * 1000,
} as const;

/** Verify constraints applied per pending record. */
export const OTP_VERIFY = {
  /** Max wrong-code attempts before the record is burned (caller must re-request). */
  MAX_ATTEMPTS: 5,
  /** Code length in digits. */
  CODE_LENGTH: 6,
} as const;
