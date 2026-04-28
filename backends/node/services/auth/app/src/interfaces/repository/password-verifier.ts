/**
 * Repository interface for verifying a user's current password.
 *
 * Used by the email/phone change flows as the password gate that runs BEFORE
 * any state changes. Wrong password = entire flow aborts (no OTP issued, no
 * record created).
 *
 * Implementation in auth-infra reads the user's credential account hash via
 * BetterAuth's internalAdapter and compares using BetterAuth's bcrypt verify.
 */
export interface IVerifyUserPassword {
  /**
   * Returns true if the provided plaintext password matches the user's
   * current credential. Returns false if user has no credential account (e.g.
   * social-only sign-in) or if the password does not match.
   */
  verify(userId: string, plainPassword: string): Promise<boolean>;
}
