import type Redis from "ioredis";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { redisErrorResult } from "../../redis-error-result.js";
import type { DistributedCache } from "@d2/interfaces";

type Input = DistributedCache.ReleaseLockInput;
type Output = DistributedCache.ReleaseLockOutput;

/** Lua script for atomic compare-and-delete. Only deletes if the stored value matches lockId. */
const _RELEASE_LOCK_SCRIPT = `
if redis.call("get", KEYS[1]) == ARGV[1] then
    return redis.call("del", KEYS[1])
else
    return 0
end
`;

export class ReleaseLock
  extends BaseHandler<Input, Output>
  implements DistributedCache.IReleaseLockHandler
{
  private readonly redis: Redis;

  constructor(redis: Redis, context: IHandlerContext) {
    super(context);
    this.redis = redis;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    try {
      const result = await this.redis.eval(_RELEASE_LOCK_SCRIPT, 1, input.key, input.lockId);
      return D2Result.ok({ data: { released: result === 1 } });
    } catch (err: unknown) {
      return redisErrorResult(err);
    }
  }
}
