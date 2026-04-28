/**
 * Pure OTP primitives for the account-change verification flow.
 *
 * No infrastructure dependencies (only Node's built-in `crypto`). Used by
 * RequestEmailChange, VerifyEmailChange, RequestPhoneChange, VerifyPhoneChange,
 * and RemovePhone handlers.
 *
 * The OTP code itself is sent in plain text via Comms (industry standard for
 * email/SMS delivery) but always hashed at rest in the verification table.
 */

import { createHash, randomInt } from "node:crypto";
import { OTP_VERIFY } from "../constants/otp-constants.js";

/** Account-change types tracked via the verification table. */
export type AccountChangeType = "email" | "phone";

/** Generate a zero-padded 6-digit numeric OTP. Uses crypto.randomInt for cryptographic randomness. */
export function generateOtpCode(): string {
  const max = 10 ** OTP_VERIFY.CODE_LENGTH;
  const code = randomInt(0, max).toString();
  return code.padStart(OTP_VERIFY.CODE_LENGTH, "0");
}

/** Hash an OTP code with SHA-256 for at-rest storage. Returns hex string. */
export function hashOtpCode(code: string): string {
  return createHash("sha256").update(code, "utf8").digest("hex");
}

/**
 * Stable verification table identifier for an account-change OTP. One record per
 * (user, type) — re-requests delete and replace.
 *
 * @example
 * pendingChangeIdentifier("email", "abc-123")
 * // → "account-change:email:abc-123"
 */
export function pendingChangeIdentifier(type: AccountChangeType, userId: string): string {
  return `account-change:${type}:${userId}`;
}

/**
 * Shape stored in the verification table's `value` column for account-change OTPs.
 * Encoded as JSON. Holds the hashed code, the pending value (new email/phone),
 * and the verify-attempt counter.
 */
export interface PendingChangeValue {
  /** sha256 hex of the OTP code. Never store the code itself. */
  readonly codeHash: string;
  /** The new email or phone the user is trying to change to. */
  readonly pendingValue: string;
  /** How many wrong codes have been entered. Burned at OTP_VERIFY.MAX_ATTEMPTS. */
  readonly attempts: number;
}

/** Encode a PendingChangeValue for storage in `verification.value`. */
export function encodePendingValue(value: PendingChangeValue): string {
  return JSON.stringify(value);
}

/**
 * Decode a PendingChangeValue from `verification.value`. Returns null on parse
 * failure or missing/malformed fields — callers treat null as "invalid record,
 * delete and force re-request".
 */
export function decodePendingValue(raw: string): PendingChangeValue | null {
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (
      typeof parsed === "object" &&
      parsed !== null &&
      typeof (parsed as Record<string, unknown>).codeHash === "string" &&
      typeof (parsed as Record<string, unknown>).pendingValue === "string" &&
      typeof (parsed as Record<string, unknown>).attempts === "number"
    ) {
      return parsed as PendingChangeValue;
    }
  } catch {
    // fall through
  }
  return null;
}
