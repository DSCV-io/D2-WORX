import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { DistributedCache } from "@d2/interfaces";
import { z } from "zod";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.InvalidateUserSessionCacheInput;
type Output = Commands.InvalidateUserSessionCacheOutput;

const schema = z.object({
  userId: zodGuid,
});

/** Key prefix used by BetterAuth to store the list of active session tokens per user. */
const ACTIVE_SESSIONS_PREFIX = "active-sessions-";

interface ActiveSession {
  token: string;
  expiresAt: number;
}

/**
 * Invalidates BetterAuth's Redis-cached sessions for a user.
 *
 * Reads the `active-sessions-{userId}` key to discover session tokens,
 * then deletes each individual token cache entry. This forces BetterAuth
 * to re-read from the database (with fresh user data) on the next
 * `get-session` call. The active-sessions list itself is NOT deleted —
 * the user stays signed in.
 *
 * Designed for fire-and-forget — if Redis is unreachable, sessions just
 * expire naturally via TTL.
 */
export class InvalidateUserSessionCache
  extends BaseHandler<Input, Output>
  implements Commands.IInvalidateUserSessionCacheHandler
{
  private readonly cacheGet: DistributedCache.IGetHandler<string>;
  private readonly cacheRemove: DistributedCache.IRemoveHandler;

  constructor(
    cacheGet: DistributedCache.IGetHandler<string>,
    cacheRemove: DistributedCache.IRemoveHandler,
    context: IHandlerContext,
  ) {
    super(context);
    this.cacheGet = cacheGet;
    this.cacheRemove = cacheRemove;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const listKey = `${ACTIVE_SESSIONS_PREFIX}${input.userId}`;

    // Read the active-sessions list for this user
    const listResult = await this.cacheGet.handleAsync({ key: listKey });
    if (!listResult.success || !listResult.data?.value) {
      // No cached sessions — nothing to invalidate
      return D2Result.ok({ data: {} });
    }

    // Parse the session token list
    let sessions: ActiveSession[];
    try {
      sessions = JSON.parse(listResult.data.value) as ActiveSession[];
    } catch {
      // Malformed data — nothing we can do, don't crash
      return D2Result.ok({ data: {} });
    }

    if (!Array.isArray(sessions) || sessions.length === 0) {
      return D2Result.ok({ data: {} });
    }

    // Delete each individual session token cache entry.
    // This forces BetterAuth to fall back to the DB on next read.
    await Promise.all(sessions.map((s) => this.cacheRemove.handleAsync({ key: s.token })));

    return D2Result.ok({ data: {} });
  }
}
