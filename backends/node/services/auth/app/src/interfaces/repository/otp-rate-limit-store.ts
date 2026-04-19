import type { AccountChangeType } from "@d2/auth-domain";

/**
 * Repository interface for OTP send rate limiting.
 *
 * Implemented by Redis-backed store in auth-infra.
 * Consumed by RequestEmailChange and RequestPhoneChange handlers to throttle
 * abusive OTP-send patterns (per user, per type).
 *
 * Cooldown semantics:
 * - First N sends within the window are free.
 * - Each send sets a minimum debounce cooldown (prevents rapid double-clicks).
 * - Beyond N sends, exponential backoff up to a max.
 */
export interface IOtpRateLimitStore {
  /**
   * Returns remaining cooldown in seconds. 0 = caller is allowed to send.
   */
  getCooldownSeconds(userId: string, type: AccountChangeType): Promise<number>;

  /**
   * Records a send. Increments the per-window send counter and applies the
   * appropriate cooldown (debounce + backoff).
   */
  recordSend(userId: string, type: AccountChangeType): Promise<void>;

  /**
   * Clears the counter and cooldown for a user/type after successful verify.
   * Lets the user immediately request another OTP for a different change.
   */
  clearOnSuccess(userId: string, type: AccountChangeType): Promise<void>;
}
