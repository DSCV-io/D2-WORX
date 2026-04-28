import { D2Result } from "@d2/result";

/**
 * Returns a standard D2Result failure for Redis connection/operation errors.
 * Used by all Redis cache handlers as a catch-all for infrastructure failures.
 *
 * @param err - Optional caught error to include in the result messages for diagnostics.
 */
export function redisErrorResult<T>(err?: unknown): D2Result<T> {
  if (err !== undefined) {
    return D2Result.serviceUnavailable<T>({
      messages: [`Redis operation failed: ${err instanceof Error ? err.message : String(err)}`],
    });
  }
  return D2Result.serviceUnavailable<T>();
}
