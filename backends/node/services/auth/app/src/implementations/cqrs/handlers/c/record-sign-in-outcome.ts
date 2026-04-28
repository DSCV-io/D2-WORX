import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { SIGN_IN_THROTTLE, computeSignInDelay } from "@d2/auth-domain";
import type { InMemoryCache } from "@d2/interfaces";
import { AUTH_CACHE_KEYS } from "../../../../cache-keys.js";
import type { ISignInThrottleStore } from "../../../../interfaces/repository/sign-in-throttle-store.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.RecordSignInOutcomeInput;
type Output = Commands.RecordSignInOutcomeOutput;

/**
 * Records the outcome of a sign-in attempt for brute-force throttling.
 *
 * - **Success (200):** marks identity as known-good, clears failure state,
 *   updates local cache.
 * - **Failure (401/400):** increments failure counter, computes delay,
 *   sets lockout if threshold exceeded.
 * - **Other status:** no-op.
 *
 * **Fail-open:** All store errors are swallowed — returns `{ recorded: false }`.
 */
export class RecordSignInOutcome
  extends BaseHandler<Input, Output>
  implements Commands.IRecordSignInOutcomeHandler
{
  private readonly store: ISignInThrottleStore;
  private readonly cache?: {
    get: InMemoryCache.IGetHandler<boolean>;
    set: InMemoryCache.ISetHandler<boolean>;
  };

  constructor(
    store: ISignInThrottleStore,
    context: IHandlerContext,
    cache?: {
      get: InMemoryCache.IGetHandler<boolean>;
      set: InMemoryCache.ISetHandler<boolean>;
    },
  ) {
    super(context);
    this.store = store;
    this.cache = cache;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    try {
      if (input.responseStatus === 200) {
        return await this.handleSuccess(input);
      }

      if (input.responseStatus === 401 || input.responseStatus === 400) {
        return await this.handleFailure(input);
      }

      // Other status codes (e.g. 500) — no-op
      return D2Result.ok({ data: { recorded: false } });
    } catch (err: unknown) {
      // Fail-open: swallow all store errors
      this.context.logger.warn("RecordSignInOutcome: throttle store error (fail-open)", {
        error: err instanceof Error ? err.message : String(err),
      });
      return D2Result.ok({ data: { recorded: false } });
    }
  }

  private async handleSuccess(input: Input): Promise<D2Result<Output | undefined>> {
    await Promise.all([
      this.store.markKnownGood(input.identifierHash, input.identityHash),
      this.store.clearFailureState(input.identifierHash, input.identityHash),
    ]);

    // Update local cache
    if (this.cache) {
      const cacheKey = AUTH_CACHE_KEYS.signInThrottle(input.identifierHash, input.identityHash);
      this.cache.set
        .handleAsync({
          key: cacheKey,
          value: true,
          expirationMs: SIGN_IN_THROTTLE.KNOWN_GOOD_CACHE_TTL_MS,
        })
        .catch((err: unknown) =>
          this.context.logger.debug("RecordSignInOutcome: cache set failed", {
            error: err instanceof Error ? err.message : String(err),
          }),
        );
    }

    return D2Result.ok({ data: { recorded: true } });
  }

  private async handleFailure(input: Input): Promise<D2Result<Output | undefined>> {
    const count = await this.store.incrementFailures(input.identifierHash, input.identityHash);
    const delayMs = computeSignInDelay(count);

    if (delayMs > 0) {
      const delaySec = Math.ceil(delayMs / 1000);
      await this.store.setLocked(input.identifierHash, input.identityHash, delaySec);
    }

    return D2Result.ok({ data: { recorded: true } });
  }
}

export type {
  RecordSignInOutcomeInput,
  RecordSignInOutcomeOutput,
} from "../../../../interfaces/cqrs/handlers/c/record-sign-in-outcome.js";
