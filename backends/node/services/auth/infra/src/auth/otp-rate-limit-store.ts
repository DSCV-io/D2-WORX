import { OTP_RATE_LIMIT } from "@d2/auth-domain";
import type { AccountChangeType } from "@d2/auth-domain";
import type { DistributedCache } from "@d2/interfaces";

/** Redis key prefixes — infra concern, not domain. */
const KEY_PREFIX = {
  ATTEMPTS: "otp:send:attempts:",
  COOLDOWN: "otp:send:cooldown:",
} as const;

function attemptsKey(type: AccountChangeType, userId: string): string {
  return `${KEY_PREFIX.ATTEMPTS}${type}:${userId}`;
}

function cooldownKey(type: AccountChangeType, userId: string): string {
  return `${KEY_PREFIX.COOLDOWN}${type}:${userId}`;
}

/**
 * Compute the cooldown duration for the Nth send within the window.
 * - Sends 1..FREE_SEND_ATTEMPTS get the minimum debounce (anti-doubleclick).
 * - Sends beyond that get exponential backoff capped at MAX_DELAY_MS.
 */
function computeCooldownMs(sendCount: number): number {
  if (sendCount <= OTP_RATE_LIMIT.FREE_SEND_ATTEMPTS) {
    return OTP_RATE_LIMIT.MIN_DELAY_MS;
  }
  const overage = sendCount - OTP_RATE_LIMIT.FREE_SEND_ATTEMPTS;
  // 30s base, doubles each excess send: 60s, 120s, 240s, ... capped.
  const backoffMs = OTP_RATE_LIMIT.MIN_DELAY_MS * 2 ** overage;
  return Math.min(backoffMs, OTP_RATE_LIMIT.MAX_DELAY_MS);
}

/**
 * Redis-backed OTP send rate limit store.
 *
 * Structurally implements `IOtpRateLimitStore` (defined in auth-app) without
 * importing it — avoids circular dependency (infra cannot import from app).
 *
 * Mirrors `SignInThrottleStore`'s structure: separate counter and cooldown keys,
 * fail-open on any Redis error. Consumed by RequestEmailChange and
 * RequestPhoneChange handlers.
 */
export class OtpRateLimitStore {
  constructor(
    private readonly getTtl: DistributedCache.IGetTtlHandler,
    private readonly set: DistributedCache.ISetHandler<string>,
    private readonly remove: DistributedCache.IRemoveHandler,
    private readonly increment: DistributedCache.IIncrementHandler,
  ) {}

  async getCooldownSeconds(userId: string, type: AccountChangeType): Promise<number> {
    const result = await this.getTtl.handleAsync({ key: cooldownKey(type, userId) });
    if (result.success && result.data?.timeToLiveMs != null) {
      return Math.ceil(result.data.timeToLiveMs / 1000);
    }
    return 0;
  }

  async recordSend(userId: string, type: AccountChangeType): Promise<void> {
    // The Increment contract sets TTL on the first send only — see
    // `IncrementInput` jsdoc. Subsequent sends count against the same window
    // without extending it, which is what makes the per-window cap enforceable.
    const incrementResult = await this.increment.handleAsync({
      key: attemptsKey(type, userId),
      amount: 1,
      expirationMs: OTP_RATE_LIMIT.ATTEMPT_WINDOW_SECONDS * 1000,
    });
    const sendCount = incrementResult.success ? (incrementResult.data?.newValue ?? 1) : 1;

    // Set cooldown (debounce + backoff)
    const cooldownMs = computeCooldownMs(sendCount);
    await this.set.handleAsync({
      key: cooldownKey(type, userId),
      value: "",
      expirationMs: cooldownMs,
    });
  }

  async clearOnSuccess(userId: string, type: AccountChangeType): Promise<void> {
    await Promise.all([
      this.remove.handleAsync({ key: attemptsKey(type, userId) }),
      this.remove.handleAsync({ key: cooldownKey(type, userId) }),
    ]);
  }
}
