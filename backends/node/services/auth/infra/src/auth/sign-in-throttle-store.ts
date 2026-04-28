import { SIGN_IN_THROTTLE } from "@d2/auth-domain";
import type { DistributedCache } from "@d2/interfaces";

/** Redis key prefixes — infra concern, not domain. */
const KEY_PREFIX = {
  KNOWN: "signin:known:",
  ATTEMPTS: "signin:attempts:",
  LOCKED: "signin:locked:",
} as const;

/**
 * Redis-backed sign-in throttle store.
 *
 * Structurally implements `ISignInThrottleStore` (defined in auth-app) without
 * importing it — avoids circular dependency (infra cannot import from app).
 *
 * Uses `@d2/cache-redis` handler abstractions (Exists, GetTtl, Set, Remove, Increment)
 * rather than direct ioredis calls — keeps the store testable and consistent
 * with the codebase's handler-per-operation pattern.
 */
export class SignInThrottleStore {
  constructor(
    private readonly exists: DistributedCache.IExistsHandler,
    private readonly getTtl: DistributedCache.IGetTtlHandler,
    private readonly set: DistributedCache.ISetHandler<string>,
    private readonly remove: DistributedCache.IRemoveHandler,
    private readonly increment: DistributedCache.IIncrementHandler,
  ) {}

  async isKnownGood(identifierHash: string, identityHash: string): Promise<boolean> {
    const key = `${KEY_PREFIX.KNOWN}${identifierHash}:${identityHash}`;
    const result = await this.exists.handleAsync({ key });
    return result.success === true && result.data?.exists === true;
  }

  async getLockedTtlSeconds(identifierHash: string, identityHash: string): Promise<number> {
    const key = `${KEY_PREFIX.LOCKED}${identifierHash}:${identityHash}`;
    const result = await this.getTtl.handleAsync({ key });
    if (result.success && result.data?.timeToLiveMs != null) {
      return result.data.timeToLiveMs / 1000;
    }
    return 0;
  }

  async markKnownGood(identifierHash: string, identityHash: string): Promise<void> {
    const key = `${KEY_PREFIX.KNOWN}${identifierHash}:${identityHash}`;
    await this.set.handleAsync({
      key,
      value: "1",
      expirationMs: SIGN_IN_THROTTLE.KNOWN_GOOD_TTL_SECONDS * 1000,
    });
  }

  async incrementFailures(identifierHash: string, identityHash: string): Promise<number> {
    const key = `${KEY_PREFIX.ATTEMPTS}${identifierHash}:${identityHash}`;
    // The Increment contract sets TTL on the first failure only — see
    // `IncrementInput` jsdoc. The sliding window starts on the first failed
    // attempt and ticks down naturally; subsequent failures count against the
    // same window without extending it, so a slow brute-force attacker can't
    // bypass the cap by pacing attempts under the refresh interval.
    const result = await this.increment.handleAsync({
      key,
      amount: 1,
      expirationMs: SIGN_IN_THROTTLE.ATTEMPT_WINDOW_SECONDS * 1000,
    });
    return result.success ? (result.data?.newValue ?? 1) : 1;
  }

  async setLocked(identifierHash: string, identityHash: string, delaySec: number): Promise<void> {
    const key = `${KEY_PREFIX.LOCKED}${identifierHash}:${identityHash}`;
    await this.set.handleAsync({
      key,
      value: "",
      expirationMs: delaySec * 1000,
    });
  }

  async clearFailureState(identifierHash: string, identityHash: string): Promise<void> {
    const attemptsKey = `${KEY_PREFIX.ATTEMPTS}${identifierHash}:${identityHash}`;
    const lockedKey = `${KEY_PREFIX.LOCKED}${identifierHash}:${identityHash}`;
    await Promise.all([
      this.remove.handleAsync({ key: attemptsKey }),
      this.remove.handleAsync({ key: lockedKey }),
    ]);
  }
}
