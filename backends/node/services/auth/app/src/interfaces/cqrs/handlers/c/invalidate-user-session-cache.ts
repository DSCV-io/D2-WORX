import type { IHandler } from "@d2/handler";

export interface InvalidateUserSessionCacheInput {
  readonly userId: string;
}

export interface InvalidateUserSessionCacheOutput {}

/**
 * Invalidates BetterAuth's Redis-cached sessions for a user.
 *
 * Reads the `active-sessions-{userId}` key to discover session tokens,
 * then deletes each individual token cache entry. This forces BetterAuth
 * to re-read from the database (with fresh user data) on the next
 * `get-session` call. The active-sessions list itself is NOT deleted —
 * the user stays signed in.
 */
export type IInvalidateUserSessionCacheHandler = IHandler<
  InvalidateUserSessionCacheInput,
  InvalidateUserSessionCacheOutput
>;
