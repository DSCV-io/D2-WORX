import { z } from "zod";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { InMemoryCache } from "@d2/interfaces";
import type { ICheckEmailAvailabilityHandler as ICheckEmailAvailabilityRepoHandler } from "../../../../interfaces/repository/handlers/r/check-email-availability.js";
import { AUTH_CACHE_KEYS } from "../../../../cache-keys.js";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Queries.CheckEmailAvailabilityInput;
type Output = Queries.CheckEmailAvailabilityOutput;

const schema = z.object({
  email: z.string().trim().email().max(254),
});

/** TTL for "taken" results — email won't become available quickly. */
const TAKEN_CACHE_TTL_MS = 3_600_000; // 1 hour

/** TTL for "available" results — email could be taken any moment. */
const AVAILABLE_CACHE_TTL_MS = 30_000; // 30 seconds

/**
 * Checks whether an email address is available for registration.
 *
 * Uses an optional in-memory cache to avoid hammering the database.
 * "Taken" results are cached longer (1h) since they're stable.
 * "Available" results are cached briefly (30s) since they can change.
 *
 * Fail-open: cache errors are swallowed — falls through to repo query.
 */
export class CheckEmailAvailability
  extends BaseHandler<Input, Output>
  implements Queries.ICheckEmailAvailabilityHandler
{
  private readonly repo: ICheckEmailAvailabilityRepoHandler;
  private readonly cache?: {
    get: InMemoryCache.IGetHandler<boolean>;
    set: InMemoryCache.ISetHandler<boolean>;
  };

  constructor(
    repo: ICheckEmailAvailabilityRepoHandler,
    context: IHandlerContext,
    cache?: {
      get: InMemoryCache.IGetHandler<boolean>;
      set: InMemoryCache.ISetHandler<boolean>;
    },
  ) {
    super(context);
    this.repo = repo;
    this.cache = cache;
  }

  override get redaction() {
    return Queries.CHECK_EMAIL_AVAILABILITY_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const email = input.email.toLowerCase().trim();
    const cacheKey = AUTH_CACHE_KEYS.emailAvailability(email);

    // 1. Check in-memory cache (fail-open on error)
    if (this.cache) {
      const cached = await this.cache.get.handleAsync({ key: cacheKey }).catch((err: unknown) => {
        this.context.logger.debug("CheckEmailAvailability: cache get failed", {
          error: err instanceof Error ? err.message : String(err),
        });
        return undefined;
      });
      if (cached?.success && cached.data) {
        return D2Result.ok({ data: { available: cached.data.value } });
      }
    }

    // 2. Cache miss — query DB via repo handler
    const result = await this.repo.handleAsync({ email });
    if (!result.success || !result.data) return D2Result.bubbleFail(result);

    // 3. Populate cache (fire-and-forget)
    if (this.cache) {
      const ttl = result.data.available ? AVAILABLE_CACHE_TTL_MS : TAKEN_CACHE_TTL_MS;
      this.cache.set
        .handleAsync({ key: cacheKey, value: result.data.available, expirationMs: ttl })
        .catch((err: unknown) =>
          this.context.logger.debug("CheckEmailAvailability: cache set failed", {
            error: err instanceof Error ? err.message : String(err),
          }),
        );
    }

    return result;
  }
}

export type {
  CheckEmailAvailabilityInput,
  CheckEmailAvailabilityOutput,
} from "../../../../interfaces/cqrs/handlers/q/check-email-availability.js";
