import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { SignInEvent } from "@d2/auth-domain";
import type { InMemoryCache } from "@d2/interfaces";
import type { Complex } from "@d2/geo-client";
import type { WhoIsDTO } from "@d2/protos";
import { AUTH_CACHE_KEYS } from "../../../../cache-keys.js";
import type {
  IFindSignInEventsByUserIdHandler,
  ICountSignInEventsByUserIdHandler,
  IGetLatestSignInEventDateHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Queries.GetSignInEventsInput;
type Output = Queries.GetSignInEventsOutput;
type EnrichedSignInEvent = Queries.EnrichedSignInEvent;

/**
 * Cache value — the FULL enriched response (events + WhoIs already resolved).
 * Sign-in events are append-only and WhoIs is content-addressable on (ip,
 * year, month) — neither changes once written, so caching the entire output
 * is safe. Hits skip both the DB row fetch AND the parallel WhoIs hydration;
 * only `getLatestEventDate` runs each call to validate freshness.
 */
interface CachedEvents {
  events: EnrichedSignInEvent[];
  total: number;
  latestDate?: string;
}

/** Cache TTL: 3 hours. Latest-date staleness check invalidates earlier when a new event lands. */
const CACHE_TTL_MS = 3 * 60 * 60 * 1000;

/**
 * Retrieves paginated sign-in events for a user.
 *
 * Uses local memory cache with staleness check: if the latest event date
 * for the user hasn't changed since the cache was populated, the cached
 * result is still valid (sign-in events are append-only, so older pages
 * are stable as long as no new events exist).
 */
export class GetSignInEvents
  extends BaseHandler<Input, Output>
  implements Queries.IGetSignInEventsHandler
{
  private readonly findByUserId: IFindSignInEventsByUserIdHandler;
  private readonly countByUserId: ICountSignInEventsByUserIdHandler;
  private readonly getLatestEventDate: IGetLatestSignInEventDateHandler;
  private readonly findWhoIs: Complex.IFindWhoIsHandler;

  override get redaction() {
    return Queries.GET_SIGN_IN_EVENTS_REDACTION;
  }
  private readonly cache?: {
    get: InMemoryCache.IGetHandler<CachedEvents>;
    set: InMemoryCache.ISetHandler<CachedEvents>;
  };

  constructor(
    findByUserId: IFindSignInEventsByUserIdHandler,
    countByUserId: ICountSignInEventsByUserIdHandler,
    getLatestEventDate: IGetLatestSignInEventDateHandler,
    findWhoIs: Complex.IFindWhoIsHandler,
    context: IHandlerContext,
    cache?: {
      get: InMemoryCache.IGetHandler<CachedEvents>;
      set: InMemoryCache.ISetHandler<CachedEvents>;
    },
  ) {
    super(context);
    this.findByUserId = findByUserId;
    this.countByUserId = countByUserId;
    this.getLatestEventDate = getLatestEventDate;
    this.findWhoIs = findWhoIs;
    this.cache = cache;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const limit = Math.min(input.limit ?? 50, 100);
    const offset = Math.max(input.offset ?? 0, 0);
    const cacheKey = AUTH_CACHE_KEYS.signInEvents(input.userId, limit, offset);

    // Cache hit path — the only DB call is the cheap latest-date check.
    if (this.cache) {
      const cacheResult = await this.cache.get.handleAsync({ key: cacheKey });
      if (cacheResult.success && cacheResult.data?.value) {
        const cached = cacheResult.data.value;
        const dateResult = await this.getLatestEventDate.handleAsync({
          userId: input.userId,
        });
        const latestStr = dateResult.success ? dateResult.data?.date?.toISOString() : undefined;
        if (latestStr === cached.latestDate) {
          return D2Result.ok({ data: { events: cached.events, total: cached.total } });
        }
      }
    }

    // Cache miss or stale — fetch rows + count + latest date in parallel.
    const [findResult, countResult, latestDateResult] = await Promise.all([
      this.findByUserId.handleAsync({ userId: input.userId, limit, offset }),
      this.countByUserId.handleAsync({ userId: input.userId }),
      this.getLatestEventDate.handleAsync({ userId: input.userId }),
    ]);

    if (!findResult.success) return D2Result.bubbleFail(findResult);
    if (!countResult.success) return D2Result.bubbleFail(countResult);
    const rawEvents: SignInEvent[] = findResult.data?.events ?? [];
    const total = countResult.data?.count ?? 0;

    // Hydrate WhoIs for unique IPs (geo-client multi-tier cache absorbs duplicates).
    const uniqueIps = Array.from(new Set(rawEvents.map((e) => e.ipAddress).filter(Boolean)));
    const whoIsByIp = new Map<string, WhoIsDTO>();
    if (uniqueIps.length > 0) {
      const results = await Promise.all(
        uniqueIps.map((ip) =>
          this.findWhoIs
            .handleAsync({ ipAddress: ip })
            .then((r) => ({ ip, whoIs: r.success ? r.data?.whoIs : undefined }))
            .catch(() => ({ ip, whoIs: undefined as WhoIsDTO | undefined })),
        ),
      );
      for (const { ip, whoIs } of results) {
        if (whoIs) whoIsByIp.set(ip, whoIs);
      }
    }

    const enriched: EnrichedSignInEvent[] = rawEvents.map((e) => ({
      event: e,
      whoIs: whoIsByIp.get(e.ipAddress),
    }));

    // Populate cache with the FULL enriched response. Use the global latest
    // event date so the staleness check works for every page (offset > 0).
    if (this.cache) {
      const globalLatestDate = latestDateResult.success
        ? latestDateResult.data?.date?.toISOString()
        : undefined;
      // Fire-and-forget — don't block the response on cache write.
      this.cache.set
        .handleAsync({
          key: cacheKey,
          value: { events: enriched, total, latestDate: globalLatestDate },
          expirationMs: CACHE_TTL_MS,
        })
        .catch((err: unknown) =>
          this.context.logger.debug("GetSignInEvents: cache set failed", {
            error: err instanceof Error ? err.message : String(err),
          }),
        );
    }

    return D2Result.ok({ data: { events: enriched, total } });
  }
}

export type {
  GetSignInEventsInput,
  GetSignInEventsOutput,
} from "../../../../interfaces/cqrs/handlers/q/get-sign-in-events.js";
