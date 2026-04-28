import type Redis from "ioredis";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { redisErrorResult } from "../../redis-error-result.js";
import type { DistributedCache } from "@d2/interfaces";

type Input = DistributedCache.GetTtlInput;
type Output = DistributedCache.GetTtlOutput;

export class GetTtl extends BaseHandler<Input, Output> implements DistributedCache.IGetTtlHandler {
  private readonly redis: Redis;

  constructor(redis: Redis, context: IHandlerContext) {
    super(context);
    this.redis = redis;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    try {
      const pttl = await this.redis.pttl(input.key);

      // -2 = key doesn't exist, -1 = no expiry
      const timeToLiveMs = pttl > 0 ? pttl : undefined;
      return D2Result.ok({ data: { timeToLiveMs } });
    } catch (err: unknown) {
      return redisErrorResult(err);
    }
  }
}
