import type Redis from "ioredis";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { redisErrorResult } from "../../redis-error-result.js";
import type { DistributedCache } from "@d2/interfaces";

type Input = DistributedCache.IncrementInput;
type Output = DistributedCache.IncrementOutput;

/**
 * Lua script: atomic INCRBY + first-write-only PEXPIRE.
 *
 * `PEXPIRE NX` is the load-bearing piece — it sets the TTL only when the key
 * has no expiration yet. Result: the window starts when the key is created
 * (first increment in a fresh window) and ticks down naturally; subsequent
 * increments in the same window leave the TTL alone.
 *
 * Why not unconditional PEXPIRE: refreshing on every call lets an attacker
 * keep a rate-limit counter alive forever by sending requests faster than
 * the window — the counter never expires, so the "max attempts per window"
 * cap is meaningless because there is no bounded window any more.
 *
 * Lua is used so the INCRBY + PEXPIRE pair is atomic — without that, the
 * key could briefly exist without a TTL if PEXPIRE failed independently.
 */
const _INCREMENT_SCRIPT = `
local result = redis.call('INCRBY', KEYS[1], ARGV[1])
if tonumber(ARGV[2]) > 0 then
  redis.call('PEXPIRE', KEYS[1], ARGV[2], 'NX')
end
return result
`;

export class Increment
  extends BaseHandler<Input, Output>
  implements DistributedCache.IIncrementHandler
{
  private readonly redis: Redis;

  constructor(redis: Redis, context: IHandlerContext) {
    super(context);
    this.redis = redis;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    try {
      const amount = input.amount ?? 1;
      const expirationMs = input.expirationMs ?? 0;

      const newValue = (await this.redis.eval(
        _INCREMENT_SCRIPT,
        1,
        input.key,
        amount,
        expirationMs,
      )) as number;

      return D2Result.ok({ data: { newValue } });
    } catch (err: unknown) {
      return redisErrorResult(err);
    }
  }
}
